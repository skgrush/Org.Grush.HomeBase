using System.Text.Json.Serialization;

namespace Org.Grush.HomeBase.WeatherStationLib;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AllData))]
[JsonSerializable(typeof(WindSpeedAndDirection))]
public partial class WeatherStationLibSerializerContext : JsonSerializerContext;
