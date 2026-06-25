using System.Text.Json.Serialization;

namespace Org.Grush.HomeBase.WeatherStation.Service;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ServiceOptions))]
public partial class WeatherStationServiceSerializerContext : JsonSerializerContext;
