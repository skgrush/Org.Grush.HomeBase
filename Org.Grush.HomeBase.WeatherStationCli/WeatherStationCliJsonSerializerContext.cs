using System.Text.Json.Serialization;

namespace Org.Grush.HomeBase.WeatherStationCli;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CliOptionResult))]
public partial class WeatherStationCliSerializerContext : JsonSerializerContext;
