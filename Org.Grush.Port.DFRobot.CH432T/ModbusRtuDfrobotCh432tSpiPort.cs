using System;
using System.ComponentModel;
using System.Device.Spi;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
// ReSharper disable InconsistentNaming

namespace Org.Grush.Port.DFRobot.CH432T;


public enum Ch432tPortNumber : byte
{
  Port1 = 0,
  Port2 = 1,
}

public enum Ch432tRegisterDefinition : byte
{
  /// <summary>RX FIFO</summary>
  RBR = 0x00, // 0x08
  /// <summary>TX FIFO</summary>
  THR = 0x00,
  /// <summary>Interrupt enable</summary>
  IER = 0x01, // 0x09
  /// <summary>Interrupt Identification</summary>
  IIR = 0x02, // 0x0A
  /// <summary>FIFO control</summary>
  FCR = 0x02,
  /// <summary>Line Control</summary>
  LCR = 0x03, // 0x0B
  /// <summary>Modem Control</summary>
  MCR = 0x04,
  /// <summary>Line Status</summary>
  LSR = 0x05,
  /// <summary>Modem Status</summary>
  MSR = 0x06,
  /// <summary>Scratch Pad</summary>
  SCR = 0x07,

// # Special Register set: Only if (LCR[7] == 1)
  /// <summary>Divisor Latch Low</summary>
  DLL = 0x00,
  /// <summary>Divisor Latch High</summary>
  DLH = 0x01,
}

public static class Extensions
{
  extension(Ch432tRegisterDefinition reg)
  {
    public bool IsPortSpecific() => reg
      is Ch432tRegisterDefinition.RBR
      or Ch432tRegisterDefinition.THR
      or Ch432tRegisterDefinition.IER
      or Ch432tRegisterDefinition.IIR
      or Ch432tRegisterDefinition.LSR
      or Ch432tRegisterDefinition.LCR;
  }
}

public enum Ch432tIirValue : byte
{
  /// <summary>Mask for the interrupt ID</summary>
  ID_MASK = 0x0e,
  /// <summary>No interrupts pending</summary>
  NO_INT_BIT = (1 << 0),
  /// <summary>RX line status error</summary>
  RLSE_SRC = 0x06,
  /// <summary>RX data interrupt</summary>
  RDI_SRC = 0x04,
  /// <summary>RX time-out interrupt</summary>
  RTOI_SRC = 0x0c,
  /// <summary>TX holding register empty</summary>
  THRI_SRC = 0x02,
  /// <summary>Modem status interrupt</summary>
  MSI_SRC = 0x00,
}


public enum Ch432IerBits
{
  /// Enable RX data interrupt
  RDI = (1 << 0),
  /// Enable TX holding register interrupt
  THRI = (1 << 1),
  /// Enable RX line status interrupt
  RLSI = (1 << 2),
  /// Enable Modem status interrupt
  MSI = (1 << 3),

  #region IER enhanced register bits
  /// Enable Soft reset
  RESET = (1 << 7),
  /// Enable low power mode
  LOWPOWER = (1 << 6),
  /// Enable clk * 2
  CK2X = (1 << 5),
  #endregion
}


public class ModbusRtuDfrobotCh432tSpiPort(
  SpiConnectionSettings s,
  Ch432tPortNumber portNumber,
  ILogger logger,
  StopBits stopBits,
  Parity parity,
  int baudRate
) : ModbusRtuSpiPort(s)
{
  #region LCR register bits


  /// Divisor Latch enable
  public const byte CH432T_LCR_DLAB_BIT = (1 << 7);
  /// Special reg set
  public const byte CH432T_LCR_CONF_MODE_A = CH432T_LCR_DLAB_BIT;

  #endregion

  public const byte CH432T_REG_SHIFT = 2;
  public const byte CH432T_LCR_PARITY_EN_BIT = (1 << 3);
  public const byte CH432T_CHECKBIT_ODD = (0x00 << 4);
  public const byte CH432T_CHECKBIT_EVEN = (0x01 << 4);
  public const byte CH432T_CHECKBIT_MARK = (0x02 << 4);
  public const byte CH432T_CHECKBIT_SPACE = (0x03 << 4);

  /// Low power mode
  public const byte CH432T_LOW_POWER_MODE = (byte)Ch432IerBits.LOWPOWER;


  #region MCR register bits
  /// DTR complement
  public const byte CH432T_MCR_DTR_BIT = (1 << 0);
  /// RTS complement
  public const byte CH432T_MCR_RTS_BIT = (1 << 1);
  /// OUT1
  public const byte CH432T_MCR_OUT1 = (1 << 2);
  /// OUT2
  public const byte CH432T_MCR_OUT2 = (1 << 3);
  /// Enable loopback test mode
  public const byte CH432T_MCR_LOOP_BIT = (1 << 4);
  /// Enable Hardware Flow control
  public const byte CH432T_MCR_AFE = (1 << 5);
  #endregion


  // CH432T External input clock frequency or external crystal frequency
  public const int CH432T_CLOCK_FREQUENCY = 22118400;


  public static byte ComputeLcrWordLengthFor(int byteLength) => byteLength switch
  {
    5 => 0x00,
    6 => 0x01,
    7 => 0x02,
    8 => 0x03,
    _ => throw new NotSupportedException($"byteLength {byteLength} is not supported"),
  };

  public static byte ComputeStopBitsCFlag(StopBits stopBits) => stopBits switch
  {
    StopBits.One => 0,
    StopBits.Two => (1 << 2),
    StopBits.OnePointFive => (1 << 2), // XXX same as TWO.. there is no POSIX support for 1.5
    StopBits.None or _ => throw new InvalidEnumArgumentException(nameof(stopBits), (int)stopBits, typeof(StopBits)),
  };
  public static byte ComputeParityBitsCFlag(Parity parity)
  {
    return parity switch
    {
      Parity.Even => (CH432T_LCR_PARITY_EN_BIT | CH432T_CHECKBIT_EVEN),
      Parity.Odd => (CH432T_LCR_PARITY_EN_BIT | CH432T_CHECKBIT_ODD),
      Parity.Mark => (CH432T_LCR_PARITY_EN_BIT | CH432T_CHECKBIT_MARK),
      Parity.Space => (CH432T_LCR_PARITY_EN_BIT | CH432T_CHECKBIT_SPACE),
      _ => throw new NotSupportedException($"Unsupported parity bit {parity}"),
    };
  }


  public TimeSpan Timeout { get; } = TimeSpan.FromSeconds(2);

  public Ch432tPortNumber PortNumber => portNumber;

  public ValueTask GetIntStatus(IntStatusReg reg, bool isAsync, CancellationToken cancellationToken)
    => ReadRegister(Ch432tRegisterDefinition.IIR, reg, isAsync, cancellationToken);

  public ValueTask GetLinesStatus(LSRRegister reg, bool isAsync, CancellationToken cancellationToken)
    => ReadRegister(Ch432tRegisterDefinition.LSR, reg, isAsync, cancellationToken);

  public ValueTask GetModemStatus(ModemConfigReg reg, bool isAsync, CancellationToken cancellationToken)
    => ReadRegister(Ch432tRegisterDefinition.MSR, reg, isAsync, cancellationToken);

  public override async ValueTask Open(bool isAsync = true, CancellationToken cancellationToken = default)
  {
    if (IsOpen is true)
    {
      logger.LogInformation("[Open()] already open");
      return;
    }

    logger.LogInformation("[Open()] opening isAsync={isAsync}", isAsync);

    byte iir = await ReadRegister(Ch432tRegisterDefinition.IIR, isAsync, cancellationToken);
    logger.LogInformation("[Open()] CH432T_IIR_REG = {iir:x} = {v}", iir, (Ch432tIirValue)iir);
    LSRRegister lsr = new();
    await GetLinesStatus(lsr, isAsync, cancellationToken);
    logger.LogInformation("[Open()] CH432T_LSR_REG = {lsr:x}", lsr);

    await WriteRegister(Ch432tRegisterDefinition.SCR, 0x66, isAsync, cancellationToken);
    byte scr = await ReadRegister(Ch432tRegisterDefinition.SCR,  isAsync, cancellationToken);
    if (scr is not 0x66)
    {
      throw new InvalidOperationException($"Failed to open port! (Expected 0x66 but got 0x{scr:x}) Check whether the expansion board is properly connected and whether spidev0.0 is occupied.");
    }

    try
    {
      await ReconfigurePort(forceUpdate: true, isAsync: isAsync, cancellationToken: cancellationToken);
    }
    catch (Exception)
    {
      await SetLowPowerMode(CH432T_LOW_POWER_MODE, isAsync, cancellationToken);
      throw;
    }

    await base.Open(isAsync: isAsync,  cancellationToken: cancellationToken);
  }

  public override void Close()
  {
    SetLowPowerMode(CH432T_LOW_POWER_MODE, isAsync: false).GetAwaiter().GetResult();
    base.Close();
  }

  private (byte CFlag, int BaudRate)? origAttr;

  protected async ValueTask ReconfigurePort(bool forceUpdate = false, bool isAsync = true, CancellationToken cancellationToken = default)
  {
    byte cflag = ComputeLcrWordLengthFor(SpiConnectionSettings.DataBitLength);

    if (parity is Parity.None)
      unchecked
      {
        cflag &= (byte)~(CH432T_LCR_PARITY_EN_BIT | CH432T_CHECKBIT_SPACE);
      }
    else
    {
      cflag |= ComputeParityBitsCFlag(parity);
    }

    cflag |= ComputeStopBitsCFlag(stopBits);

    if (forceUpdate || (cflag, BaudRate: baudRate) != origAttr)
    {
      origAttr = (cflag, baudRate);
      // Now, initialize the UART
      await WriteRegister(Ch432tRegisterDefinition.LCR, cflag, isAsync, cancellationToken);

      // # Reset FIFOs, Enable FIFOs and configure interrupt & flow control levels to 8
      byte val = (byte)(Ch432tFcrBits.FIFO | Ch432tFcrBits.RXRESET | Ch432tFcrBits.TXRESET | Ch432tFcrBits.RECVTG_LEN_8);
      await WriteRegister(Ch432tRegisterDefinition.FCR, val, isAsync, cancellationToken);

      // # Enable RX, TX, CTS change interrupts
      // # val = Ch432IerBits.RDI | Ch432IerBits.THRI | Ch432IerBits.RLSI | Ch432IerBits.MSI;
      val = (byte)(Ch432IerBits.RDI | Ch432IerBits.RLSI | Ch432IerBits.MSI);
      await WriteRegister(Ch432tRegisterDefinition.IER, val, isAsync, cancellationToken);

      // # Enable Uart interrupts, and automatic flow control (automatically control pin RTS)
      val = CH432T_MCR_RTS_BIT | CH432T_MCR_OUT2 | CH432T_MCR_AFE;
      await WriteRegister(Ch432tRegisterDefinition.MCR, val, isAsync, cancellationToken);

      // # Set baud rate
      await SetBaudRate(baudRate, isAsync: isAsync, cancellationToken);

      // # self.set_sleep_mode(CH432T_STANDARD_MODE)
    }
  }

  public override int Read(Memory<byte> buffer)
  {
    CancellationTokenSource cts = new(Timeout);
    var t = PortIrq(buffer, isAsync: false, cts.Token);
    if (!t.IsCompleted)
      throw new InvalidOperationException("async");
    t.GetAwaiter().GetResult();

    return buffer.Length;
  }

  public override async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
  {
    CancellationTokenSource timeoutCts = new(Timeout);
    CancellationTokenSource cts = token == CancellationToken.None
      ? timeoutCts
      : CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, token);

    await Task.Yield();

    await PortIrq(buffer, isAsync: true, cts.Token);
    return buffer.Length;
  }

  public override void Write(ReadOnlyMemory<byte> buffer)
  {
    if (IsOpen is not true)
      throw new InvalidOperationException("closed");

    var t = WriteRegister(Ch432tRegisterDefinition.THR, buffer, isAsync: false);
    if (!t.IsCompleted)
      throw new InvalidOperationException("failed");
    t.GetAwaiter().GetResult();
  }

  public override async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
  {
    if (IsOpen is not true)
      throw new InvalidOperationException("closed");

    await WriteRegister(Ch432tRegisterDefinition.THR, buffer, isAsync: true, token);
  }

  protected async Task PortIrq(Memory<byte> buff, bool isAsync, CancellationToken cancellationToken)
  {
    IntStatusReg intStatus = new();
    LSRRegister linesStatus = new();

    while (true)
    {
      await GetIntStatus(intStatus, isAsync, cancellationToken);

      switch (intStatus.IntType)
      {
        case (byte)Ch432tIirValue.NO_INT_BIT:
          break;

        case (byte)Ch432tIirValue.RLSE_SRC:
          // Interrupt for receiving line status, priority: 1
          // Line status register, used to analyze serial port status by query
          await GetLinesStatus(linesStatus, isAsync, cancellationToken);
          logger.LogWarning("[PortIrq()] Unknown LSR interrupt state");
          if (linesStatus.RFifoErr)
          {
            // logger.info("lines_status.r_fifo_err")
            // logger.info("lines_status.r_fifo_err(CH432T_RBR_REG)---%#x", self._read_reg(CH432T_RBR_REG, 1)[0])
          }
          else if (linesStatus.FifoOver)
          {
            // logger.info("lines_status.fifo_over")
          }
          else
          {
            // logger.info("Unknown LSR interrupt state")
          }

          break;

        case (byte)Ch432tIirValue.RDI_SRC or (byte)Ch432tIirValue.RTOI_SRC:
          // Interrupt for receiving data, priority: 2; interrupt for receiving data timeout priority: 2
          // logger.info("CH432T_IIR_RTOI_SRC")
          await HandleRx(buff, isAsync, cancellationToken);
          return;

        case (byte)Ch432tIirValue.THRI_SRC:
          //   # THR register empty interrupt, priority: 3
          //   self._write_reg(CH432T_THR_REG, 0x66)
          break;

        case (byte)Ch432tIirValue.MSI_SRC:
          // MODEM output change interrupt, priority: 4
          ModemConfigReg modemConfigReg = new();
          await ReadRegister(Ch432tRegisterDefinition.MSR, modemConfigReg,  isAsync, cancellationToken);
          logger.LogInformation("[PortIrq()] msr---{modemReg:x}", modemConfigReg);
          break;

        case var intType:
          logger.LogWarning("[PortIrq()] Unknown interrupt state {int}", intType);
          break;
      }

      cancellationToken.ThrowIfCancellationRequested();
    }
  }

  public ValueTask SetSleepMode(Ch432tSleepMode mode, bool isAsync = true, CancellationToken cancellationToken = default)
  {
    Ch432tRegisterDefinition reg = Ch432tRegisterDefinition.IER;

    return RegBitUpdate(reg, (byte)Ch432IerBits.CK2X, (byte)Ch432IerBits.CK2X, isAsync, cancellationToken);
  }

  public ValueTask SetLowPowerMode(byte mode, bool isAsync = true, CancellationToken cancellationToken = default)
    => RegBitUpdate(Ch432tRegisterDefinition.IER, (byte)Ch432IerBits.LOWPOWER, mode, isAsync, cancellationToken);

  public async ValueTask SetBaudRate(int baud, bool isAsync = true, CancellationToken token = default)
  {
    // CK2X=0, internal 1/12 frequency division
    byte prescaler = 0;
    int clock_rate = CH432T_CLOCK_FREQUENCY / 12;
    if (baud > 115200)
    {
      // CK2X=1, internal double frequency
      prescaler = (byte)Ch432IerBits.CK2X;
      clock_rate *= 24;
    }
    // Set prescaler
    byte reg = (byte)Ch432tRegisterDefinition.IER;
    // TODO: why do we offset by 0x08 for port ONE here where we normally offset by 0x08 for port TWO???
    if (PortNumber is Ch432tPortNumber.Port1)   // regular register
    {
      reg += 0x08;
    }

    await RegBitUpdate((Ch432tRegisterDefinition)reg, (byte)Ch432IerBits.CK2X, prescaler,  isAsync, token);

    // Save raw value of LCR register
    byte lcr = await ReadRegister(Ch432tRegisterDefinition.LCR, isAsync, token);
    logger.LogInformation("[SetBaudRate()] lcr = {lcr:x}", lcr);
    // Open the LCR divisors for configuration
    await WriteRegister(Ch432tRegisterDefinition.LCR, CH432T_LCR_CONF_MODE_A, isAsync, token);
    await Sleeper(TimeSpan.FromMilliseconds(2), isAsync, token);

    // Set new baud rate
    byte mode = (byte)(clock_rate / 16f / baud);   // The set value corresponding to the baud rate in the current mode
    await WriteRegister(Ch432tRegisterDefinition.DLL, mode, isAsync, token);
    await WriteRegister(Ch432tRegisterDefinition.DLH, (byte)(mode >> 8), isAsync, token);
    // logger.info( "CH432T_DLL_REG = %#x", self._read_reg(CH432T_DLL_REG, 1)[0])
    // logger.info( "CH432T_DLH_REG = %#x", self._read_reg(CH432T_DLH_REG, 1)[0])

    // Put LCR back to the normal mode
    await WriteRegister(Ch432tRegisterDefinition.LCR, lcr, isAsync, token);
  }

  /// <summary>
  /// Receive interrupt handler function
  /// </summary>
  /// <param name="size">Read length of serial data</param>
  /// <returns>The read serial data</returns>
  protected async Task<Memory<byte>> HandleRx(int size, bool isAsync, CancellationToken cancellationToken)
  {
    var buffer = new byte[size];

    await HandleRx(buffer.AsMemory(), isAsync, cancellationToken);
    return buffer;
  }

  protected async Task HandleRx(Memory<byte> buffer, bool isAsync, CancellationToken cancellationToken)
  {
    int idx = 0;

    LSRRegister linesStatus = new();

    while (true)
    {
      await GetLinesStatus(linesStatus, isAsync, cancellationToken);

      if (linesStatus.RFifoErr)
      {
        logger.LogInformation("lines_status.r_fifo_err");
      }

      if (linesStatus.DataReady)
      {
        // [...] Memory<byte> buffer [...]
        byte byteValue = await ReadRegister(Ch432tRegisterDefinition.RBR, isAsync, cancellationToken);
        buffer.Span[idx] = byteValue;
        ++idx;
      }
      else if (idx >= buffer.Length)
      {
        // logger.LogInformation("Data received: {data}", buffer);
        return;
      }
    }
  }

  protected async ValueTask<byte> ReadRegister(Ch432tRegisterDefinition register, bool isAsync, CancellationToken token)
  {
    // byte regAddrMsb = (byte)(0xFD & (((byte)register + (byte)PortNumber * 0x08) << CH432T_REG_SHIFT));
    byte regAddrReadRequest = (byte)register; // e.g. 0b0111 scratchPad
    if (PortNumber is Ch432tPortNumber.Port2 && register.IsPortSpecific())
      regAddrReadRequest &= 0x08;             // e.g. 0b1111 scratchPad on Port 2

    regAddrReadRequest <<= CH432T_REG_SHIFT;  // e.g. 0b0011_1100

    logger.LogInformation("[ReadRegister()] portnum = {portNum}, reg = {reg}, reg_addr = 0x{regAddr:x}",
      PortNumber, register, regAddrReadRequest);

    byte[] transferBytes = [regAddrReadRequest, 0xFF];
    TransferFullDuplex(transferBytes);
    logger.LogInformation("[ReadRegister()] transferBytes = [0x{first:x}, 0x{second:x}]", transferBytes[0],  transferBytes[1]);

    await Sleeper(TimeSpan.FromMilliseconds(0.1), isAsync, token);

    return transferBytes[1];
  }

  protected async ValueTask ReadRegister(Ch432tRegisterDefinition register, ByteStructure into, bool isAsync, CancellationToken token)
  {
    var value = await ReadRegister(register, isAsync, token);
    into.Write(value);
  }

  struct ByteArrayWrapper(ReadOnlyMemory<byte> bytes) : IFormattable
  {
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
      ReadOnlyMemory<byte> bytesCopy = bytes;
      Memory<byte> outArray = new byte[2 * bytes.Length];

      var r = Parallel.For(0, bytes.Length, i =>
      {
        byte b = bytesCopy.Span[i];
        b.TryFormat(outArray.Span[(2 * i)..(2*i + 2)], out _, format, formatProvider);
      });

      return Encoding.UTF8.GetString(outArray.Span);
    }
  }

  protected ValueTask WriteRegister(Ch432tRegisterDefinition register, byte data, bool isAsync = true, CancellationToken cancellationToken = default)
    => WriteRegister(register, new[] { data }, isAsync, cancellationToken);
  protected async ValueTask WriteRegister(Ch432tRegisterDefinition register, ReadOnlyMemory<byte> data, bool isAsync = true, CancellationToken cancellationToken = default)
  {
    // reg_addr = [0x02 | ( (reg + self.portnum * 0x08) << CH432T_REG_SHIFT )]
    byte regAddrWriteRequest = (byte)register;
    if (PortNumber is Ch432tPortNumber.Port2 && register.IsPortSpecific())
      regAddrWriteRequest &= 0x08;

    regAddrWriteRequest <<= CH432T_REG_SHIFT;
    regAddrWriteRequest |= 0x02;

    logger.LogInformation("[WriteRegister()] portnum = {portNum}, reg = 0x{reg:x}, reg_addr = 0x{regAddr:x}, data = 0x{data:x}",
      PortNumber, register, regAddrWriteRequest, new ByteArrayWrapper(data));

    byte[] toWrite = [regAddrWriteRequest, ..data.Span];
    // ReSharper disable once MethodHasAsyncOverloadWithCancellatio
    // base.Write(toWrite.AsMemory());

    base.TransferFullDuplex(toWrite);

    await Sleeper(TimeSpan.FromMilliseconds(1), isAsync, cancellationToken);
  }

  protected ValueTask Sleeper(TimeSpan time, bool isAsync, CancellationToken cancellationToken)
  {
    if (isAsync)
      return new ValueTask(Task.Delay(time, cancellationToken));

    Thread.Sleep(time);
    cancellationToken.ThrowIfCancellationRequested();
    return default;
  }

  protected ValueTask WriteRegister(Ch432tRegisterDefinition register, ByteStructure data, bool isAsync = true, CancellationToken cancellationToken = default)
    => WriteRegister(register, data.Value,  isAsync, cancellationToken);

  protected async ValueTask RegBitUpdate(Ch432tRegisterDefinition register, byte mask, byte value, bool isAsync = true, CancellationToken cancellationToken = default)
  {
    byte registerValue = await ReadRegister(register,  isAsync, cancellationToken);
    registerValue = (byte) ( (registerValue & ~mask) | (value & mask) );

    await WriteRegister(register, registerValue,  isAsync, cancellationToken);
  }

}

public enum Ch432tSleepMode : byte
{
  Standard = 0,
  Sleep = (1 << 5),
}

public enum Ch432tFcrBits : byte
{
  /// Enable FIFO
  FIFO = (1 << 0),
  /// Reset RX FIFO
  RXRESET =(1 << 1),
  /// Reset TX FIFO
  TXRESET =(1 << 2),
  /// RX Trigger level
  RXLVL = (0x03 << 6),

  #region Set the trigger point for receiving FIFO interrupt and hardware flow control
  RECVTG_LEN_1 = (0x00 << 6),
  RECVTG_LEN_4 = (0x01 << 6),
  RECVTG_LEN_8 = (0x02 << 6),
  RECVTG_LEN_14 = (0x03 << 6),
  #endregion
}
