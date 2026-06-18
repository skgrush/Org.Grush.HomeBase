using System.Text.Json.Serialization;

namespace Org.Grush.HomeBase.WeatherStationLib.SEN0658;

/// <summary>
///
/// <b>Size: approx. 32 bytes</b>
///
/// </summary>
/// <param name="WindSpeed">Meters per second</param>
/// <param name="_regF5"></param>
/// <param name="WindDirection">8-gear wind direction</param>
/// <param name="WindDirectionAngle">360º angle from North</param>
/// <param name="RelativeHumidity">%RH</param>
/// <param name="Temperature">ºC</param>
/// <param name="NoiseDb">dB</param>
/// <param name="Pm2_5">μg/m³</param>
/// <param name="Pm10">μg/m³</param>
/// <param name="AtmosphericPressure">kPa</param>
/// <param name="Lux">Light level in Lux.</param>
/// <seealso href="https://wiki.dfrobot.com/sen0658/docs/21684#:~:text=General%20register%20address"/>
public record SEN0658AllData(
  float WindSpeed,
  [property:JsonIgnore]
  ushort _regF5,
  WindDirection WindDirection,
  ushort WindDirectionAngle,
  float RelativeHumidity,
  float Temperature,
  float NoiseDb,
  ushort Pm2_5,
  ushort Pm10,
  ushort AtmosphericPressure,
  UInt32 Lux
)
{
  public const ushort StartingAddress = 0x01_F0;
  public const int AddressCount = 16;
}
