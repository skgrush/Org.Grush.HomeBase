// using System.ComponentModel.DataAnnotations;

using System;

namespace Org.Grush.Port.DFRobot.CH432T;

public abstract class ByteStructure(byte initial) : IFormattable
{
  public byte Value { get; private set; } = initial;

  public void Write(byte value) => Value = value;

  public bool ReadBit(/*[Range(0, 7)]*/ byte lsbIdx)
    => ReadBitFromMask((byte)(1 << lsbIdx));

  protected bool ReadBitFromMask(byte mask) => mask == (Value & mask);

  public void WriteBit(bool bit, /*[Range(0, 7)]*/ byte lsbIdx)
    => WriteBitFromMask(bit, (byte)(1 << lsbIdx));

  protected void WriteBitFromMask(bool bit, byte mask)
  {
    if (bit)
      Value |= mask;
    else
      Value &= (byte)(~mask);
  }

  public static implicit operator byte(ByteStructure bs) => bs.Value;

  public abstract string ToString(string? format, IFormatProvider? formatProvider);
}
