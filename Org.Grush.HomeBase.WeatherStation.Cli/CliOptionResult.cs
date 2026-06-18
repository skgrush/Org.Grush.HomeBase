using System.CommandLine;
using System.CommandLine.Help;
using System.Device.Spi;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Org.Grush.HomeBase.WeatherStation.Lib.SEN0658;

namespace Org.Grush.HomeBase.WeatherStation.Cli;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicFields)]
public record CliOptionResult(
  // int? BusId,
  // int ChipSelectLine,
  int BaudRate,
  string Device,
  int? Loop,
  // SpiMode? SpiMode,
  LogLevel LogLevel
)
{
  public const int MinLoopMs = 100;
  public const int DefaultLoopMS = 1000;

  internal static CliOptionResult From(ParseResult parseResult)
    => new(
      // BusId: parseResult.GetValue(BusIdOption),
      // ChipSelectLine: parseResult.GetValue(ChipSelectLineOption) ?? -1,
      BaudRate: parseResult.GetValue(BaudRateOption),
      Device: parseResult.GetRequiredValue(DeviceOption),
      Loop: parseResult.GetValue(LoopOption) is int loopValue
        ? (loopValue < MinLoopMs ? null : loopValue)
        : DefaultLoopMS,
      // SpiMode: parseResult.GetValue(SpiModeOption),
      LogLevel: parseResult.GetValue(LogLevelOption)
    );

  internal static void AddOptions(IList<Option> toAddTo)
  {
    Type t = typeof(CliOptionResult);
    var options = t
      .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
      .Where(m => m.Name.EndsWith("Option"))
      .Select(m => (Option)m.GetValue(t)!);

    foreach (var option in options)
      toAddTo.Add(option);
  }

  // internal static readonly Option<int?> BusIdOption = new("--bus")
  // {
  //   Description = "Specifies the bus identifier, e.g. the X in /dev/spidevX.Y",
  //   Arity = ArgumentArity.ExactlyOne,
  // };
  //
  // internal static readonly Option<int?> ChipSelectLineOption = new("--chip-select-line")
  // {
  //   Description = "Specifies the chip select, e.g. the Y in /dev/spidevX.Y",
  //   Arity = ArgumentArity.ExactlyOne,
  // };

  internal static readonly Option<int> BaudRateOption = new Option<int>("--baud")
    {
      DefaultValueFactory = _ => WeatherStationClient.DefaultBaud,
    }
    .AcceptOnlyFromAmong(WeatherStationClient.SupportedBauds.Values.Select(v => v.ToString()).ToArray());

  internal static readonly Option<string> DeviceOption = new("--device")
  {
    Arity = ArgumentArity.ExactlyOne,
    Description = "File path to the TTY device, e.g. /dev/ttySC0"
  };

  internal static readonly Option<int?> LoopOption = new("--loop")
  {
    Arity = ArgumentArity.ZeroOrOne,
    Description = "Milliseconds to loop",
    DefaultValueFactory = _ => -1,
  };

  internal static readonly Option<SpiMode?> SpiModeOption = new Option<SpiMode?>("--spi-mode")
    {
      Arity = ArgumentArity.ZeroOrOne,
      CustomParser = argumentResult => argumentResult.Tokens.FirstOrDefault()?.Value is {} value
        ? (SpiMode)int.Parse(value)
        : null
    }
    .AcceptOnlyFromAmong("0", "1", "2", "3");

  internal static readonly HelpOption HelpOption = new();

  internal static readonly Option<LogLevel> LogLevelOption = new Option<LogLevel>("--log-level")
  {
    Arity = ArgumentArity.ExactlyOne,
    DefaultValueFactory = _ => LogLevel.Information,
    CustomParser = argResult => Enum.TryParse(argResult.Tokens[0].Value, ignoreCase: true, out LogLevel logLevel)
      ? logLevel
      : throw new(),
  }
    .AcceptOnlyFromAmong(Enum.GetValues<LogLevel>().Select(l => l.ToString()).ToArray());
}
