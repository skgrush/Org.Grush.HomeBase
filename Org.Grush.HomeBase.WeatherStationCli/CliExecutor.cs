using System.CommandLine;
using System.CommandLine.Invocation;
using System.Device.Gpio;
using System.IO.Ports;
using System.Text.Json;
using FluentModbus;
using Microsoft.Extensions.Logging;
using Org.Grush.HomeBase.WeatherStationLib;

namespace Org.Grush.HomeBase.WeatherStationCli;

public class CliExecutor(
  Stream stdout,
  ILoggerFactory loggerFactory
) : AsynchronousCommandLineAction
{
  public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
  {
    CliOptionResult result = CliOptionResult.From(parseResult);

    var programLogger = loggerFactory.CreateLogger<CliExecutor>();

    programLogger.LogDebug("Args: {0}", JsonSerializer.Serialize(result, WeatherStationCliSerializerContext.Default.CliOptionResult));

    // using LibGpiodV2Driver driver = new(4);
    // using GpioController controller = new(driver);

    using GpioController controller = new();

    programLogger.LogDebug(
      "GPIO Controller ({type}) pinCount={pinCount}",
      controller.GetType().Name,
      controller.PinCount
    );

    using SerialPort serialPort = new(
      result.Device,
      baudRate: result.BaudRate,
      parity: Parity.None,
      dataBits: 8,
      stopBits: StopBits.OnePointFive
    );

    ModbusRtuSerialPort modbusPort = new(serialPort);

    await using WeatherStationClient client = new(
      modbusPort,
      1,
      loggerFactory.CreateLogger<WeatherStationClient>()
    );

    Utf8JsonWriter stdoutWriterUtf8 = new(stdout);

    try
    {
      stdoutWriterUtf8.WriteStartObject();
      await stdoutWriterUtf8.FlushAsync(cancellationToken);
      while (!cancellationToken.IsCancellationRequested && !client.Cancelled)
      {
        var results = await client.ReadAllDataAsync(cancellationToken);

        stdoutWriterUtf8.WritePropertyName(DateTimeOffset.Now.ToString("u"));
        JsonSerializer.Serialize(writer: stdoutWriterUtf8, value: results, WeatherStationLibSerializerContext.Default.AllData);
        await stdoutWriterUtf8.FlushAsync(cancellationToken);

        if (result.Loop is null)
          break;
        await Task.Delay(result.Loop.Value, cancellationToken);
      }
    }
    catch (OperationCanceledException)
    {
    }

    stdout.Write("\n}\n"u8);

    return 0;
  }
}
