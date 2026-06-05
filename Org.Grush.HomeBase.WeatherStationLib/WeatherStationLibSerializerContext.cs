using System.Text.Json.Serialization;

namespace Org.Grush.HomeBase.WeatherStationLib;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AllData))]
public partial class WeatherStationLibSerializerContext : JsonSerializerContext;
