using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Device.Spi;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Org.Grush.HomeBase.WeatherStation.Lib.SEN0658;

namespace Org.Grush.HomeBase.WeatherStation.Cli;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicFields)]
public partial record CliOptionResult(
  // int? BusId,
  // int ChipSelectLine,
  int BaudRate,
  string Device,
  TimeSpan ReportInterval,
  TimeSpan QueryInterval,
  Uri CwopUri,
  // SpiMode? SpiMode,
  LogLevel LogLevel
)
{
  public static readonly TimeSpan DefaultReportInterval = new(hours: 0, minutes: 7, seconds: 53);
  public static readonly TimeSpan DefaultQueryInterval = new(0, 0, seconds: 2);
  public static readonly Uri DefaultCwopUri = new("tcp://cwop.aprs.net:14580");

  internal static CliOptionResult From(ParseResult parseResult)
    => new(
      // BusId: parseResult.GetValue(BusIdOption),
      // ChipSelectLine: parseResult.GetValue(ChipSelectLineOption) ?? -1,
      BaudRate: parseResult.GetValue(BaudRateOption),
      Device: parseResult.GetRequiredValue(DeviceOption),
      ReportInterval: parseResult.GetValue(ReportIntervalOption),
      QueryInterval: parseResult.GetValue(QueryIntervalOption),
      CwopUri: parseResult.GetValue(CwopUriOption) ?? DefaultCwopUri,
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


  internal static readonly Option<TimeSpan> ReportIntervalOption = new("--report-interval")
  {
    Arity = ArgumentArity.ExactlyOne,
    CustomParser = TimeSpanCustomParser,
    DefaultValueFactory = _ => DefaultReportInterval,
  };
  internal static readonly Option<TimeSpan> QueryIntervalOption = new("--query-interval")
  {
    Arity = ArgumentArity.ExactlyOne,
    CustomParser = TimeSpanCustomParser,
    DefaultValueFactory = _ => DefaultQueryInterval,
  };

  internal static readonly Option<Uri> CwopUriOption = new("--cwop-uri")
  {
    Arity = ArgumentArity.ExactlyOne,
    DefaultValueFactory = _ => DefaultCwopUri,
  };

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

  public static TimeSpan TimeSpanCustomParser(ArgumentResult argResult)
  {
    var str = argResult.Tokens[0].Value;
    if (!str.IsWhiteSpace())
    {
      if (TimeSpan.TryParse(str, out var stdSpan))
        return stdSpan;

      if (TimeSpanRe.Match(str) is { Success: true } match)
      {
        return TimeSpanGroupParsers
          .Select(kvp =>
            match.Groups[kvp.Key] is { Success: true, ValueSpan: { } vs }
              ? kvp.Value(double.Parse(vs))
              : TimeSpan.Zero
          )
          .Where(t => t != TimeSpan.Zero)
          .Aggregate((x, y) => x + y);
      }
    }

    argResult.AddError("Invalid TimeSpan, should be a standard TimeSpan format or `[[00hr]00min]00sec`");
    return TimeSpan.Zero;
  }

  private static readonly Regex TimeSpanRe = Compile_TimeSpanRe();
  private static readonly IReadOnlyDictionary<string, Func<double, TimeSpan>> TimeSpanGroupParsers = new Dictionary<string, Func<double, TimeSpan>>
  {
    { "second", TimeSpan.FromSeconds },
    { "minute", TimeSpan.FromMinutes },
    { "hour",   TimeSpan.FromHours },
  }.AsReadOnly();

  [GeneratedRegex(
    """
    ^\ *
    (?:(?<hour>   \d+(?:\.\d+)? ) \ *(?:hours?|hrs?|h)  )?\ *
    (?:(?<minute> \d+(?:\.\d+)? ) \ *(?:minutes?|mins?|m)  )?\ *
    (?:(?<second> \d+(?:\.\d+)? ) \ *(?:seconds?|secs?|s)  )?\ *
    $
    """, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant)]
  private static partial Regex Compile_TimeSpanRe();
}
