using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.IO.Ports;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Org.Grush.HomeBase.WeatherStation.Lib.SEN0658;
using Org.Grush.HomeBase.WeatherStation.Service;

namespace Org.Grush.HomeBase.WeatherStation.Cli;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicFields)]
public record CliOptionResult(
  int BaudRate,
  byte ModbusUnitIdentifier,
  string Device,
  int? RainPin,
  TimeSpan ReportInterval,
  TimeSpan QueryInterval,
  Uri CwopUri,
  LogLevel LogLevel,
  bool PrintJson
) : ServiceOptions(
  BaudRate: BaudRate,
  ModbusUnitIdentifier: ModbusUnitIdentifier,
  Device: Device,
  RainPin: RainPin,
  ReportInterval: ReportInterval,
  QueryInterval: QueryInterval,
  CwopUri: CwopUri,
  PrintJson: PrintJson,
  LogLevel: LogLevel
)
{
  internal static CliOptionResult From(ParseResult parseResult)
    => new(
      BaudRate: parseResult.GetValue(BaudRateOption),
      ModbusUnitIdentifier: parseResult.GetValue(ModbusUnitIdentifierOption),
      Device: parseResult.GetRequiredValue(DeviceOption),
      RainPin: parseResult.GetValue(RainPinOption),
      ReportInterval: parseResult.GetValue(ReportIntervalOption),
      QueryInterval: parseResult.GetValue(QueryIntervalOption),
      CwopUri: parseResult.GetValue(CwopUriOption) ?? DefaultCwopUri,
      PrintJson: parseResult.GetValue(PrintJsonOption),
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

  internal static readonly Option<int> BaudRateOption = new Option<int>("--baud")
    {
      DefaultValueFactory = _ => WeatherStationClient.DefaultBaud,
    }
    .AcceptOnlyFromAmong(WeatherStationClient.SupportedBauds.Values.Select(v => v.ToString()).ToArray());

  internal static readonly Option<string> DeviceOption = new Option<string>("--device")
  {
    Arity = ArgumentArity.ExactlyOne,
    Description = "File path to the TTY device, e.g. /dev/ttySC0",
    CompletionSources =
    {
      completionCtx => GetSerialPorts(completionCtx.WordToComplete),
    }
  }
  .AcceptLegalFilePathsOnly();

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

  internal static readonly Option<byte> ModbusUnitIdentifierOption = new("--modbus-unit-identifier")
  {
    Arity = ArgumentArity.ExactlyOne,
    DefaultValueFactory = _ => DefaultModbusUnitIdentifier,
  };

  internal static readonly Option<int?> RainPinOption = new("--rain-pin")
  {
    Arity = ArgumentArity.ExactlyOne,
    CompletionSources =
    {
      completionCtx => SerialPort.GetPortNames()
    }
  };

  internal static readonly Option<bool> PrintJsonOption = new("--print-json")
  {
    Arity = ArgumentArity.ExactlyOne,
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

  private static TimeSpan TimeSpanCustomParser(ArgumentResult argResult)
  {
    CustomParsing.TryParseTimeSpan(argResult.Tokens[0].Value, out var result, argResult.AddError);
    return result;
  }

}
