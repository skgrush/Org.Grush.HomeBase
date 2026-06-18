namespace Org.Grush.HomeBase.WeatherStation.Data;

public readonly record struct AprsWxNetStationInformation(
  string CwNumber,
  float Latitude,
  float Longitude,
  int Altitude
);
