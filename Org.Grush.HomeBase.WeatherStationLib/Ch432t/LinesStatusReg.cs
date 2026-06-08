namespace Org.Grush.HomeBase.WeatherStationLib.Ch432t;

/// <summary>
///
/// </summary>
/// <remarks>
///      @brief Line status register, used to analyze serial port status by query.
///      @note Register struct:
///      @n -----------------------------------------------------------------------------------------------
///      @n |     b7     |   b6    |   b5   |    b4     |    b3     |     b2     |    b1     |     b0     |
///      @n -----------------------------------------------------------------------------------------------
///      @n | r_fifo_err | t_empty | THR_EN | break_INT | frame_err | parity_err | fifo_over | data_ready |
///      @n -----------------------------------------------------------------------------------------------
///       r_fifo_err: set to 1, indicates there is at least one parity_err, frame_err, or break_INT error in the received FIFO.
///       t_empty: set to 1, indicates transmitting holding register THR and shift register TSR are both empty.
///       THR_EN: set to 1, indicates transmitting holding register THR is empty.
///       break_INT: set to 1, indicates BREAK line interval is detected.
///       frame_err: set to 1, indicates the frame error of the data read from the received FIFO, lack of valid stop bit.
///       parity_err: set to 1, indicates parity check error of the data read from the received FIFO occurred.
///       fifo_over: set to 1, indicates the received FIFO buffer overflow
///       data_ready: set to 1, indicates there is data received from the FIFO, after reading all the data in FIFO, the bit will automatically reset.
/// </remarks>
public class LinesStatusReg(byte initial = 0b0110_0000) : ByteStructure(initial)
{
  public bool RFifoErr
  {
    get => ReadBitFromMask(0b1000_0000);
    set => WriteBitFromMask(value, 0b1000_0000);
  }

  public bool TEmpty
  {
    get => ReadBitFromMask(0b0100_0000);
    set => WriteBitFromMask(value, 0b0100_0000);
  }

  public bool ThrEn
  {
    get => ReadBitFromMask(0b0010_0000);
    set => WriteBitFromMask(value, 0b0010_0000);
  }

  public bool BreakInt
  {
    get => ReadBitFromMask(0b0001_0000);
    set => WriteBitFromMask(value, 0b0001_0000);
  }

  public bool FrameErr
  {
    get => ReadBitFromMask(0b0000_1000);
    set => WriteBitFromMask(value, 0b0000_1000);
  }

  public bool ParityErr
  {
    get => ReadBitFromMask(0b0000_0100);
    set => WriteBitFromMask(value, 0b0000_0100);
  }

  public bool FifoOver
  {
    get => ReadBitFromMask(0b0000_0010);
    set => WriteBitFromMask(value, 0b0000_0010);
  }

  public bool DataReady
  {
    get => ReadBitFromMask(0b0000_0001);
    set => WriteBitFromMask(value, 0b0000_0001);
  }
}
