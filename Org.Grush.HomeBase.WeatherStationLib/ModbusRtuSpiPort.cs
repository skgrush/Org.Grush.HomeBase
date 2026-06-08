using System.Device.Spi;
using FluentModbus;

namespace Org.Grush.HomeBase.WeatherStationLib;

public class ModbusRtuSpiPort(SpiConnectionSettings spiConnectionSettings) : IModbusRtuSerialPort, IDisposable
{
  private bool _disposed;
  public bool? IsOpen { get; private set; }
  bool IModbusRtuSerialPort.IsOpen => IsOpen is true;

  private readonly SpiDevice _spiDevice = SpiDevice.Create(spiConnectionSettings);

  protected void TransferFullDuplex(ReadOnlySpan<byte> writeBuffer, Span<byte> readBuffer)
  {
    _spiDevice.TransferFullDuplex(writeBuffer, readBuffer);
  }
  protected void TransferFullDuplex(Span<byte> duplexBuffer)
  {
    Span<byte> readBuffer = stackalloc byte[duplexBuffer.Length];
    _spiDevice.TransferFullDuplex(duplexBuffer, readBuffer);
    readBuffer.CopyTo(duplexBuffer);
  }

  public int Read(byte[] buffer, int offset = 0, int count = -1) => Read(buffer.AsSpan(offset, count));
  public virtual int Read(Span<byte> buffer)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    _spiDevice.Read(buffer);
    return buffer.Length;
  }

  public virtual Task<int> ReadAsync(byte[] buffer, int offset = 0, int count = -1, CancellationToken token = default)
    => ReadAsync(buffer.AsSpan(offset, count), token);
  public virtual Task<int> ReadAsync(Span<byte> buffer, CancellationToken token = default)
  {
    token.ThrowIfCancellationRequested();
    return Task.FromResult(Read(buffer));
  }

  public void Write(byte[] buffer, int offset = 0, int count = -1) => Write(buffer.AsSpan(offset, count));
  public virtual void Write(ReadOnlySpan<byte> buffer)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    _spiDevice.Write(buffer);
  }

  public virtual Task WriteAsync(byte[] buffer, int offset = 0, int count = -1, CancellationToken token = default)
    => WriteAsync(buffer.AsSpan(offset, count), token);
  public virtual Task WriteAsync(ReadOnlySpan<byte> buffer, CancellationToken token = default)
  {
    token.ThrowIfCancellationRequested();

    Write(buffer);
    return Task.CompletedTask;
  }

  public virtual void Open()
  {
    IsOpen = true;
  }

  public void Close()
  {
    if (IsOpen is not true)
      return;

    IsOpen = false;
  }

  public string PortName =>
    $"/dev/spidev{_spiDevice.ConnectionSettings.BusId}.{_spiDevice.ConnectionSettings.ChipSelectLine}";

  public void Dispose() {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!disposing)
      return;

    Close();
    _disposed = true;

    _spiDevice.Dispose();
  }
}
