using System.Text.Json.Serialization;

namespace Org.Grush.HomeBase.WeatherStation.Data.APRSWXNET;

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AprsWxNetStationInformation))]
[JsonSerializable(typeof(AprsWxNetPacketBody))]
public partial class AprsWxNetSerializerContext : JsonSerializerContext;
