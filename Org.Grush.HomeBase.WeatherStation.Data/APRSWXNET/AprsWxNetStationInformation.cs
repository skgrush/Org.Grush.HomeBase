using System.Text.Json;

namespace Org.Grush.HomeBase.WeatherStation.Data.APRSWXNET;

public record AprsWxNetStationInformation(
  string CwNumber,
  float Latitude,
  float Longitude,
  int Altitude
)
{

  public string Serialize()
    => JsonSerializer.Serialize(this, AprsWxNetSerializerContext.Default.AprsWxNetStationInformation);
}
