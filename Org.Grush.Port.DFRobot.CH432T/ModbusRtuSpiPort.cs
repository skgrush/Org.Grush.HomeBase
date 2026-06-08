using System;
using System.Device.Spi;
using System.Threading;
using System.Threading.Tasks;
using FluentModbus;

namespace Org.Grush.Port.DFRobot.CH432T;

public class ModbusRtuSpiPort(SpiConnectionSettings spiConnectionSettings) : IModbusRtuSerialPort, IDisposable
{
  private bool _disposed;
  public bool? IsOpen { get; private set; }
  bool IModbusRtuSerialPort.IsOpen => IsOpen is true;

  protected SpiConnectionSettings SpiConnectionSettings => spiConnectionSettings;

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

  public int Read(byte[] buffer, int offset = 0, int count = -1) =>
    Read(buffer.AsMemory(offset, count < 0 ? buffer.Length : count));
  public virtual int Read(Memory<byte> buffer)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    _spiDevice.Read(buffer.Span);
    return buffer.Length;
  }

  public Task<int> ReadAsync(byte[] buffer, int offset = 0, int count = -1, CancellationToken token = default) =>
    ReadAsync(buffer.AsMemory(offset, count < 0 ? buffer.Length : count), token);
  public virtual Task<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
  {
    token.ThrowIfCancellationRequested();
    return Task.FromResult(Read(buffer));
  }

  public void Write(byte[] buffer, int offset = 0, int count = -1)
    => Write(buffer.AsMemory(offset, count < 0 ? buffer.Length : count));
  public virtual void Write(ReadOnlyMemory<byte> buffer)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    _spiDevice.Write(buffer.Span);
  }

  public Task WriteAsync(byte[] buffer, int offset = 0, int count = -1, CancellationToken token = default)
    => WriteAsync(buffer.AsMemory(offset, count < 0 ? buffer.Length : count), token);
  public virtual Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
  {
    token.ThrowIfCancellationRequested();

    Write(buffer);
    return Task.CompletedTask;
  }

  void IModbusRtuSerialPort.Open()
  {
    var task = Open(isAsync: false);
    if (!task.IsCompleted)
      throw new InvalidOperationException("Open(isAsync: false) did not complete synchronously!");
    task.GetAwaiter().GetResult();
  }

  public virtual async ValueTask Open(bool isAsync = true, CancellationToken cancellationToken = default)
  {
    IsOpen = true;
  }

  public virtual void Close()
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
