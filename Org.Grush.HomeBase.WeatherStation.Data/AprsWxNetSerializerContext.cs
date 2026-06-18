using System.Text.Json.Serialization;
using Org.Grush.HomeBase.WeatherStation.Lib.SEN0658;

namespace Org.Grush.HomeBase.WeatherStation.Data;

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AprsWxNetStationInformation))]
[JsonSerializable(typeof(AprsWxNetPacketBody))]
public partial class AprsWxNetSerializerContext : JsonSerializerContext;
