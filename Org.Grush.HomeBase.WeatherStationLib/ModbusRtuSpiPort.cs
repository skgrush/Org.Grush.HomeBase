using System.Device.Spi;
using FluentModbus;

namespace Org.Grush.HomeBase.WeatherStationLib;

public sealed class ModbusRtuSpiPort(SpiDevice spiDevice) : IModbusRtuSerialPort, IDisposable
{
  private bool _disposed;

  public int Read(byte[] buffer, int offset, int count)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    spiDevice.Read(buffer.AsSpan(start: offset, length: count));
    return count;
  }

  public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();
    return Task.FromResult(Read(buffer, offset, count));
  }

  public void Write(byte[] buffer, int offset, int count)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    spiDevice.Write(buffer.AsSpan(start: offset, length: count));
  }

  public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();
    Write(buffer, offset, count);
    return Task.CompletedTask;
  }

  public void Open() { }

  public void Close()
  {
    if (_disposed) return;
    _disposed = true;
    spiDevice.Dispose();
  }

  public void Dispose()
  {
    Close();
  }

  public string PortName { get; } =
    $"/dev/spidev{spiDevice.ConnectionSettings.BusId}.{spiDevice.ConnectionSettings.ChipSelectLine}";

  public bool IsOpen => !_disposed;
}
