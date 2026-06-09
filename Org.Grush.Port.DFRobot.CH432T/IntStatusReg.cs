using System;
using System.ComponentModel;
using System.Text.Json;

namespace Org.Grush.Port.DFRobot.CH432T;

/// <summary>Interrupt identity register, used to analyze and process the interrupt source</summary>
/// <remarks>
///   @note Register struct:
///   @n -----------------------------------------------------------------------------------
///   @n |    b7    |    b6    |   b5    |   b4    |    b3    |    b2    |   b1   |   b0   |
///   @n -----------------------------------------------------------------------------------
///   @n |      fifo_ENS       |     reserved      |   IID3   |   IID2   |  IID1  | NOINT  |
///   @n -----------------------------------------------------------------------------------
///   fifo_ENS: This bit is for FIFO enabling status, 1: FIFO enabled
///   IID3 + IID2 + IID1 + NOINT:
///     0001: priority: none; interrupt type: no interrupt occurs;
///       Interrupt source: no interrupt; method to clear interrupt: none;
///     0110: priority: 1;  interrupt type: receive line status;
///       Interrupt source: OVERR, PARERR, FRAMEERR, BREAKINT; method to clear interrupt: read LSR;
///     0100: priority: 2;  interrupt type: for receiving data;
///       Interrupt source: The number of received bytes reaches the trigger point of FIFO; method to clear interrupt: read RBR;
///     1100: priority: 2;  interrupt type: receiving data timeout;
///       Interrupt source: next data has not been received for over 4 data periods; method to clear interrupt: read RBR;
///     0010: priority: 3;  interrupt type: THR register empty;
///       Interrupt source: transmit holding register empty, re-enable interrupt when IETHRE changes from 0 to 1; method to clear interrupt: read IIR or write THR;
///     0000: priority: 4;  interrupt type: MODEM input changes;
///       Interrupt source: △CTS、△DSR、△RI、△DCD; method to clear interrupt: read MSR;
/// </remarks>
public class IntStatusReg(byte initial = 0b0000_0001) : ByteStructure(initial)
{
  public bool FifoENS
  {
    get => ReadBitFromMask(0b1000_0000);
    set => WriteBitFromMask(value, 0b1000_0000);
  }

  public byte IntType => (byte)(Value & 0x0000_1111);

  public byte? Priority
  {
    get
    {
      byte currentValue = IntType;
      return currentValue switch
      {
        0b0001 => null,
        0b0110 => 1,
        0b0100 or 0b1100 => 2,
        0b0010 => 3,
        0b0000 => 4,
        _ => throw new InvalidEnumArgumentException(nameof(currentValue), currentValue, typeof(byte)),
      };
    }
    set
    {
      byte currentFullValue = (byte)this;
      byte newValue = (byte)(currentFullValue & 0xF0);

      if (value is null)
        newValue |= 0x01;
      else if (value is 1)
        newValue |= 0b0110;
      else if (value is 2)
        newValue = (byte)((currentFullValue & 0b1111_1100) | 0b0100);
      else if (value is 3)
        newValue |= 0b0010;
      else if (value is 4)
      { }
      else
        throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(byte));

      Write(newValue);
    }
  }

  public override string ToString(string? format, IFormatProvider? formatProvider)
    => JsonSerializer.Serialize(this, Ch432tSerializerContext.Default.IntStatusReg);
}
