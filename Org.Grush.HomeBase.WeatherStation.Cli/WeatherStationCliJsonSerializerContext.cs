using System.Text.Json.Serialization;

namespace Org.Grush.HomeBase.WeatherStation.Cli;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CliOptionResult))]
public partial class WeatherStationCliSerializerContext : JsonSerializerContext;
