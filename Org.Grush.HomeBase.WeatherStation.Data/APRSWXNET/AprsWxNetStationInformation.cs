namespace Org.Grush.HomeBase.WeatherStation.Data.APRSWXNET;

public readonly record struct AprsWxNetStationInformation(
  string CwNumber,
  float Latitude,
  float Longitude,
  int Altitude
);
