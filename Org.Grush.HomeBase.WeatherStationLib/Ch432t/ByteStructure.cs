using System.ComponentModel.DataAnnotations;

namespace Org.Grush.HomeBase.WeatherStationLib.Ch432t;

public abstract class ByteStructure(byte initial)
{
  private byte _value = initial;

  public byte Read() => _value;
  public void Write(byte value) => _value = value;

  public bool ReadBit([Range(0, 7)] byte lsbIdx)
    => ReadBitFromMask((byte)(1 << lsbIdx));

  protected bool ReadBitFromMask(byte mask) => mask == (_value & mask);

  public void WriteBit(bool bit, [Range(0, 7)] byte lsbIdx)
    => WriteBitFromMask(bit, (byte)(1 << lsbIdx));

  protected void WriteBitFromMask(bool bit, byte mask)
  {
    if (bit)
      _value |= mask;
    else
      _value &= (byte)(~mask);
  }

  public static implicit operator byte(ByteStructure bs) => bs._value;
}
