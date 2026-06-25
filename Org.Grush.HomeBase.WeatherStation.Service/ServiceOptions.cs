using System.IO.Ports;
using Microsoft.Extensions.Logging;

namespace Org.Grush.HomeBase.WeatherStation.Service;

public record ServiceOptions(
  int BaudRate,
  byte ModbusUnitIdentifier,
  string Device,
  int? RainPin,
  TimeSpan ReportInterval,
  TimeSpan QueryInterval,
  Uri? CwopUri,
  bool PrintJson,
  LogLevel LogLevel
)
{
  public const byte DefaultModbusUnitIdentifier = 1;
  public static readonly TimeSpan DefaultReportInterval = new(hours: 0, minutes: 7, seconds: 53);
  public static readonly TimeSpan DefaultQueryInterval = new(0, 0, seconds: 2);
  public static readonly Uri DefaultCwopUri = new("tcp://cwop.aprs.net:14580");

  public static IEnumerable<string> GetSerialPorts(string? wordToComplete = null)
  {
    var ports = SerialPort.GetPortNames();

    if (wordToComplete is null or "")
      return ports;

    Func<string, bool> filter
      = wordToComplete.Length > 0 && !wordToComplete.Contains('/')
        ? portName => portName.Contains(wordToComplete)
        : portName => portName.StartsWith(wordToComplete);

    return ports
      .Where(filter);
  }
}
