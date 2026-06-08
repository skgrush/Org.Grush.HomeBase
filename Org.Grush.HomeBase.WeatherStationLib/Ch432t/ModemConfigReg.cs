namespace Org.Grush.HomeBase.WeatherStationLib.Ch432t;

/// <remarks>
///       @brief MODEM control register, control MODEM output
/// @note Register struct:
/// @n -------------------------------------------------------------------------------------
///   @n |   b7  |  b6  |    b5    |    b4     |    b3     |    b2    |    b1    |     b0    |
///   @n -------------------------------------------------------------------------------------
///   @n |   reserved   |   AFE    |   loop    |   out2    |   out1   |   RTS    |    DTR    |
///   @n -------------------------------------------------------------------------------------
///   AFE: when the bit is set to 1, allow CTS & RTS hardware automatic flow control
///   loop: when the bit is set to 1, enable test mode for internal loop
///   out2: when the bit is set to 1, allow interrupt request output of the serial port, otherwise no actual interrupt request of the serial port will occur.
///   out1: this bit is user-definable MODEM control bit and is not connected to the actual output pin.
///   RTS: when the bit is set to 1, RTS pin output is valid (active low), otherwise it's invalid.
///   DTR: when the bit is set to 1, DTR pin output is valid (active low), otherwise it's invalid.
/// </remarks>
public class ModemConfigReg(byte initial = 0b0) : ByteStructure(initial)
{

  public bool Afe
  {
    get => ReadBitFromMask(0b0010_0000);
    set => WriteBitFromMask(value, 0b0010_0000);
  }

  public bool Loop
  {
    get => ReadBitFromMask(0b0001_0000);
    set => WriteBitFromMask(value, 0b0001_0000);
  }

  public bool Out2
  {
    get => ReadBitFromMask(0b0000_1000);
    set => WriteBitFromMask(value, 0b0000_1000);
  }

  public bool Out1
  {
    get => ReadBitFromMask(0b0000_0100);
    set => WriteBitFromMask(value, 0b0000_0100);
  }

  public bool Rts
  {
    get => ReadBitFromMask(0b0000_0010);
    set => WriteBitFromMask(value, 0b0000_0010);
  }

  public bool Dtr
  {
    get => ReadBitFromMask(0b0000_0001);
    set => WriteBitFromMask(value, 0b0000_0001);
  }
}
