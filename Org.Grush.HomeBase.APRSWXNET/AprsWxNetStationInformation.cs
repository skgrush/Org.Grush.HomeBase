namespace Org.Grush.HomeBase.APRSWXNET;

public readonly record struct AprsWxNetStationInformation(
  string CwNumber,
  float Latitude,
  float Longitude,
  int Altitude
);
