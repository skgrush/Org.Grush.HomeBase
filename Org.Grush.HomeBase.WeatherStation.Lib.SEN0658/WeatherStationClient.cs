using System.Collections.Immutable;
using System.IO.Ports;
using System.Runtime.InteropServices;
using FluentModbus;
using Microsoft.Extensions.Logging;

namespace Org.Grush.HomeBase.WeatherStation.Lib.SEN0658;

/// <summary>
///
///
/// </summary>
/// <remarks>
///   Addresses we know:
///     0x01_F4 : WindSpeed in 0.01m/s
///       ....
///     0x01_F6 : WindDirection (?)
///     0x01_F7 : WindDirectionAngle in degrees
///     0x01_F8 : RelativeHumidity in 0.1%
///     0x01_F9 : Temperature in 0.1ºC
///     0x01_FA : Noise in 0.1 dB
///       ....
///     0x01_FE : Lux MS2B
///     0x01_FF : Lux LS2B
/// </remarks>
public sealed class WeatherStationClient : IAsyncDisposable
{
  private readonly CancellationTokenSource cts = new();

  private readonly ModbusRtuClient rtuClient = new()
  {
    Parity = Parity.None,
    StopBits = StopBits.One,
    Handshake = Handshake.None,
  };
  private readonly byte _modbusUnitIdentifier;
  private readonly ILogger<WeatherStationClient> _logger;

  public const int DefaultBaud = 4800;
  public static readonly ImmutableDictionary<byte, int> SupportedBauds =
  [
    new(0, 2400),
    new(1, 4800),
    new(2, 9600),
    new(3, 19200),
    new(4, 38400),
    new(5, 57600),
    new(6, 115200),
    new(7, 1200),
  ];

  public WeatherStationClient(
    IModbusRtuSerialPort modbusPort,
    byte modbusUnitIdentifier,
    ILogger<WeatherStationClient> logger
  )
  {
    _modbusUnitIdentifier = modbusUnitIdentifier;
    _logger = logger;

    rtuClient.Initialize(modbusPort, ModbusEndianness.BigEndian);
  }

  public bool Cancelled => cts.IsCancellationRequested;

  public async Task<SEN0658AllData> ReadAllDataAsync(CancellationToken cancellationToken)
  {
    var ct = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token).Token;

    var result = await rtuClient.ReadHoldingRegistersAsync<ushort>(
      unitIdentifier: _modbusUnitIdentifier,
      startingAddress: SEN0658AllData.StartingAddress,
      count: SEN0658AllData.AddressCount,
      cancellationToken: ct
    );

    return new(
      WindSpeed: result.Span[0x4] / 100.0f,
      _regF5: result.Span[0x5],
      WindDirection: (WindDirection)result.Span[0x6],
      WindDirectionAngle: result.Span[0x7],
      RelativeHumidity: result.Span[0x8] / 10.0f,
      Temperature: result.Span[0x9] / 10.0f,
      NoiseDb: result.Span[0xA] / 10.0f,
      Pm2_5: result.Span[0xB],
      Pm10: result.Span[0xC],
      AtmosphericPressure: result.Span[0xD],
      Lux: MemoryMarshal.Cast<ushort, UInt32>(result.Span[0xE..])[0]
    );
  }

  public async ValueTask DisposeAsync()
  {
    if (Cancelled)
      return;

    await cts.CancelAsync();
    cts.Dispose();
    rtuClient.Dispose();
  }
}
