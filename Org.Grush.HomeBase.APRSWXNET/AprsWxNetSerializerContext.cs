using System.Text.Json.Serialization;
using Org.Grush.HomeBase.WeatherStationLib.SEN0658;

namespace Org.Grush.HomeBase.APRSWXNET;

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AprsWxNetStationInformation))]
[JsonSerializable(typeof(AprsWxNetPacketBody))]
public partial class AprsWxNetSerializerContext : JsonSerializerContext;
