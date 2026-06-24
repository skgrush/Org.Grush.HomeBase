using System.Text.Json;
using Org.Grush.HomeBase.WeatherStation.Lib.SEN0658;

namespace Org.Grush.HomeBase.WeatherStation.Data.APRSWXNET;

public readonly record struct AprsWxNetPacketBody(
  DateTimeOffset Time,
  SEN0658AllData StationData,
  float? AverageWindSpeed, // TODO: over what time period
  float? PeakWindGust, // TODO: over what time period
  double? LastHourRainfallMillimeters,
  double? LastDayRainfallMillimeters,
  double? TodayRainfallMillimeters
)
{
  public string Serialize()
    => JsonSerializer.Serialize(this, AprsWxNetSerializerContext.Default.AprsWxNetPacketBody);
}
