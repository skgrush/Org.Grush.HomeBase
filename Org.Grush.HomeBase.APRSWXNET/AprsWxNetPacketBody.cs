using Org.Grush.HomeBase.WeatherStationLib.SEN0658;

namespace Org.Grush.HomeBase.APRSWXNET;

public readonly record struct AprsWxNetPacketBody(
  DateTimeOffset Time,
  SEN0658AllData StationData,
  float? AverageWindSpeed, // TODO: over what time period
  float? PeakWindGust, // TODO: over what time period
  double? LastHourRainfallMillimeters,
  double? LastDayRainfallMillimeters,
  double? TodayRainfallMillimeters
);
