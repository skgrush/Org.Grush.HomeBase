using System.ComponentModel;
using System.Device.Spi;
using System.IO.Ports;
using Microsoft.Extensions.Logging;

namespace Org.Grush.HomeBase.WeatherStationLib.Ch432t;


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
//     /// <summary>Divisor Latch Low</summary>
//     DLL = 0x00,
//     /// <summary>Divisor Latch High</summary>
//     DLH = 0x01,
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

public class ModbusRtuDfrobotCh432tSpiPort(
  SpiConnectionSettings s,
  Ch432tPortNumber portNumber,
  ILogger logger,
  StopBits stopBits,
  Parity parity,
  int baudRate
) : ModbusRtuSpiPort(s)
{
  public const byte CH432T_REG_SHIFT = 2;
  public const byte CH432T_LCR_PARITY_EN_BIT = (1 << 3);
  public const byte CH432T_CHECKBIT_ODD = (0x00 << 4);
  public const byte CH432T_CHECKBIT_EVEN = (0x01 << 4);
  public const byte CH432T_CHECKBIT_MARK = (0x02 << 4);
  public const byte CH432T_CHECKBIT_SPACE = (0x03 << 4);


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

  public void GetIntStatus(IntStatusReg reg)
    => ReadRegister(Ch432tRegisterDefinition.IIR, reg);

  public void GetLinesStatus(LinesStatusReg reg)
    => ReadRegister(Ch432tRegisterDefinition.LSR, reg);

  public void GetModemStatus(ModemConfigReg reg)
    => ReadRegister(Ch432tRegisterDefinition.MSR, reg);

  public override void Open()
  {
    byte iir = ReadRegister(Ch432tRegisterDefinition.IIR);
    logger.LogInformation("CH432T_IIR_REG = {iir:x}", iir);
    byte lsr = ReadRegister(Ch432tRegisterDefinition.LSR);
    logger.LogInformation("CH432T_LSR_REG = {lsr:x}", lsr);

    WriteRegister(Ch432tRegisterDefinition.SCR, [0x66]);
    byte scr = ReadRegister(Ch432tRegisterDefinition.SCR);
    if (scr is not 0x66)
    {
      throw new InvalidOperationException("Failed to open port! Check whether the expansion board is properly connected and whether spidev0.0 is occupied.");
    }

    try
    {
      ReconfigurePort(forceUpdate: true);
    }
    catch (Exception)
    {
      // TODO
      SetLowPowerMode();
      throw;
    }

    base.Open();
  }

  private (byte CFlag, int BaudRate)? origAttr;

  protected void ReconfigurePort(bool forceUpdate = false)
  {
    byte cflag = ComputeLcrWordLengthFor(s.DataBitLength);

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
      WriteRegister(Ch432tRegisterDefinition.LCR, [cflag]);

      // # Reset FIFOs, Enable FIFOs and configure interrupt & flow control levels to 8
      val = CH432T_FCR_FIFO_BIT | CH432T_FCR_RXRESET_BIT | CH432T_FCR_TXRESET_BIT | CH432T_FCR_RECVTG_LEN_8
      self._write_reg(CH432T_FCR_REG, val)

      // # Enable RX, TX, CTS change interrupts
      // # val = CH432T_IER_RDI_BIT | CH432T_IER_THRI_BIT | CH432T_IER_RLSI_BIT | CH432T_IER_MSI_BIT;
      val = CH432T_IER_RDI_BIT | CH432T_IER_RLSI_BIT | CH432T_IER_MSI_BIT
      self._write_reg(CH432T_IER_REG, val)

      // # Enable Uart interrupts, and automatic flow control (automatically control pin RTS)
      self._write_reg(CH432T_MCR_REG, CH432T_MCR_RTS_BIT | CH432T_MCR_OUT2 | CH432T_MCR_AFE)

      // # Set baud rate
      self.set_baudrate(self._baudrate)

      // # self.set_sleep_mode(CH432T_STANDARD_MODE)
    }
  }

  public override int Read(Span<byte> buffer)
  {
    CancellationTokenSource cts = new(Timeout);
    PortIrq(buffer, cts.Token);
    return buffer.Length;
  }

  public override Task<int> ReadAsync(Span<byte> buffer, CancellationToken token = default)
  {
    CancellationTokenSource timeoutCts = new(Timeout);
    CancellationTokenSource cts = token == CancellationToken.None
      ? timeoutCts
      : CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, token);

    PortIrq(buffer, cts.Token);
    return Task.FromResult(buffer.Length);
  }

  public override async Task<int> ReadAsync(byte[] buffer, int offset = 0, int count = -1, CancellationToken token = default)
  {
    return await Task.Run(async () =>
    {
      await Task.Yield();
      return await ReadAsync(buffer.AsSpan(start: offset, length: count), token);
    }, token);
  }

  public override void Write(ReadOnlySpan<byte> buffer)
  {
    if (IsOpen is not true)
      throw new InvalidOperationException("closed");

    WriteRegister(Ch432tRegisterDefinition.THR, buffer);
  }

  protected void PortIrq(Span<byte> buff, CancellationToken cancellationToken)
  {
    IntStatusReg intStatus = new();
    LinesStatusReg linesStatus = new();

    while (true)
    {
      GetIntStatus(intStatus);

      switch (intStatus.IntType)
      {
        case (byte)Ch432tIirValue.NO_INT_BIT:
          break;

        case (byte)Ch432tIirValue.RLSE_SRC:
          // Interrupt for receiving line status, priority: 1
          // Line status register, used to analyze serial port status by query
          GetLinesStatus(linesStatus);
          logger.LogInformation("Unknown LSR interrupt state");
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
          HandleRx(buff, cancellationToken);
          return;

        case (byte)Ch432tIirValue.THRI_SRC:
          //   # THR register empty interrupt, priority: 3
          //   self._write_reg(CH432T_THR_REG, 0x66)
          break;

        case (byte)Ch432tIirValue.MSI_SRC:
          // MODEM output change interrupt, priority: 4
          ModemConfigReg modemConfigReg = new();
          ReadRegister(Ch432tRegisterDefinition.MSR, modemConfigReg);
          logger.LogInformation("msr---{modemReg:x}", modemConfigReg);
          break;

        default:
          logger.LogInformation("Unknown interrupt state");
          break;
      }

      cancellationToken.ThrowIfCancellationRequested();
    }
  }

  /// <summary>
  /// Receive interrupt handler function
  /// </summary>
  /// <param name="size">Read length of serial data</param>
  /// <returns>The read serial data</returns>
  protected Span<byte> HandleRx(int size, CancellationToken cancellationToken)
  {
    Span<byte> buffer = new byte[size];

    HandleRx(buffer, cancellationToken);
    return buffer;
  }

  protected void HandleRx(Span<byte> buffer, CancellationToken cancellationToken)
  {
    int idx = 0;

    LinesStatusReg linesStatus = new();

    while (true)
    {
      GetLinesStatus(linesStatus);

      if (linesStatus.RFifoErr)
      {
        logger.LogInformation("lines_status.r_fifo_err");
      }

      if (linesStatus.DataReady)
      {
        buffer[idx] = ReadRegister(Ch432tRegisterDefinition.RBR);
        ++idx;
      }
      else if (idx >= buffer.Length)
      {
        // logger.LogInformation("Data received: {data}", buffer);
        return;
      }

      cancellationToken.ThrowIfCancellationRequested();
    }
  }

  protected byte ReadRegister(Ch432tRegisterDefinition register)
  {
    // byte regAddrMsb = (byte)(0xFD & (((byte)register + (byte)PortNumber * 0x08) << CH432T_REG_SHIFT));
    byte regAddrReadRequest = (byte)register; // e.g. 0b0111 scratchPad
    if (PortNumber is Ch432tPortNumber.Port2)
      regAddrReadRequest &= 0x08;             // e.g. 0b1111 scratchPad on Port 2

    regAddrReadRequest <<= CH432T_REG_SHIFT;  // e.g. 0b0011_1100

    Span<byte> transferBytes = [regAddrReadRequest, 0xFF];
    TransferFullDuplex(transferBytes);

    return transferBytes[1];
  }

  protected void ReadRegister(Ch432tRegisterDefinition register, ByteStructure into)
    => into.Write(ReadRegister(register));

  protected void WriteRegister(Ch432tRegisterDefinition register, ReadOnlySpan<byte> data)
  {
    // reg_addr = [0x02 | ( (reg + self.portnum * 0x08) << CH432T_REG_SHIFT )]
    byte regAddrWriteRequest = (byte)register;
    if (PortNumber is Ch432tPortNumber.Port2)
      regAddrWriteRequest &= 0x08;

    regAddrWriteRequest <<= CH432T_REG_SHIFT;

    Span<byte> toWrite = [regAddrWriteRequest, ..data];

    base.Write(toWrite);
  }

  protected void WriteRegister(Ch432tRegisterDefinition register, ByteStructure data)
    => WriteRegister(register, [data]);

  protected void RegBitUpdate(Ch432tRegisterDefinition register, byte mask, byte value)
  {
    byte registerValue = ReadRegister(register);
    registerValue = (byte) ( (registerValue & ~mask) | (value & mask) );

    WriteRegister(register, [registerValue]);
  }




}
