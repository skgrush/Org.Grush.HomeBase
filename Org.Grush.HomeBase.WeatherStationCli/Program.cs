// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using System.CommandLine.Help;
using System.Device.Gpio;
using System.Device.Spi;
using System.IO.Ports;
using System.Text.Json;
using FluentModbus;
using Iot.Device.Common;
using Microsoft.Extensions.Logging;
using Org.Grush.HomeBase.WeatherStationLib;
using Org.Grush.Port.DFRobot.CH432T;

Option<int?> busIdOption = new("--bus")
{
  Description = "Specifies the bus identifier, e.g. the X in /dev/spidevX.Y",
  Arity = ArgumentArity.ExactlyOne,
};

Option<int?> chipSelectLineOption = new("--chip-select-line")
{
  Description = "Specifies the chip select, e.g. the Y in /dev/spidevX.Y",
  Arity = ArgumentArity.ExactlyOne,
};

Option<int> baudRateOption = new("--baud")
{
  DefaultValueFactory = _ => WeatherStationClient.DefaultBaud,
};
baudRateOption.AcceptOnlyFromAmong(WeatherStationClient.SupportedBauds.Values.Select(v => v.ToString()).ToArray());

Option<string?> deviceOption = new("--device")
{
  Arity = ArgumentArity.ExactlyOne,
};

Option<byte?> loopOption = new("--loop")
{
  Arity = ArgumentArity.ZeroOrOne,
};

Option<SpiMode?> spiModeOption = new("--spi-mode")
{
  Arity = ArgumentArity.ZeroOrOne,
  CustomParser = argumentResult => argumentResult.Tokens.FirstOrDefault()?.Value is {} value
    ? (SpiMode)int.Parse(value)
    : (SpiMode?)null
};
spiModeOption.AcceptOnlyFromAmong("0", "1", "2", "3");

HelpOption helpOption = new();
RootCommand command = new("HomeBase WeatherStationCli")
{
  busIdOption,
  chipSelectLineOption,
  deviceOption,
  baudRateOption,
  spiModeOption,
  loopOption,
  helpOption,
};
command.SetAction(Run);

Console.Error.WriteLine($"Args: {string.Join(' ', args)}");
ParseResult parseResult = command.Parse(args);
var invoked = await parseResult.InvokeAsync();
Console.Error.WriteLine($"Invoked: {invoked}");

return invoked;

// if (parseResult.GetResult(helpOption) is not null)
//   return await parseResult.InvokeAsync();
//
// if (parseResult.Errors.Count is not 0)
// {
//   foreach (var error in parseResult.Errors)
//     Console.WriteLine(error);
//   return 1;
// }

async Task<int> Run(ParseResult parseResult)
{
  // using RaspberryPiBoard testBoard = new();
  // Console.Error.WriteLine("Tested board");
  // using var driver = new RaspberryPi3Driver();
  // Console.Error.WriteLine("Tested driver");
  // var controller = testBoard.CreateGpioController();
  // Console.Error.WriteLine($"Tested gpio controller {controller.GetType()}");
  //
  // using Board board = Board.Create();
  //
  // try
  // {
  //   var componentInformation = board.QueryComponentInformation();
  //   BoardPrinter.PrintComponentInfo(componentInformation, "");
  // }
  // catch
  // {
  // }
  //
  // if (board is RaspberryPiBoard piBoard)
  // {
  //   if (!piBoard.IsSpiActivated())
  //   {
  //     Console.Error.WriteLine("SPI is not activated");
  //     return 2;
  //   }
  // }

  TextWriter stdout = Console.Out;
  Console.SetOut(Console.Error);

  using SimpleConsoleLoggerFactory loggerFactory = new(LogLevel.Trace);

  var programLogger = loggerFactory.CreateLogger<Program>();

  string? device = parseResult.GetValue(deviceOption);
  int? busId = parseResult.GetValue(busIdOption);
  int chipSelectLine = parseResult.GetValue(chipSelectLineOption) ?? -1;
  SpiMode? spiMode = parseResult.GetValue(spiModeOption);
  int baudRate = parseResult.GetValue(baudRateOption);
  byte? loopTimeout =
      parseResult.GetResult(loopOption) is null
        ? null
        : (parseResult.GetValue(loopOption) ?? 5)
    ;

  programLogger.LogDebug("device={device}  busId={busId}  chipSelectLine={chipSelectLine}  spiMode={spiMode}  baudRate={baudRate}  loopTimeout={loopTimeout}",
    device, busId, chipSelectLine, spiMode, baudRate, loopTimeout
  );

  // using LibGpiodV2Driver driver = new(4);
  // using GpioController controller = new(driver);

  using GpioController controller = new();

  programLogger.LogDebug(
    "GPIO Controller ({type}) pinCount={pinCount}",
    controller.GetType().Name,
    controller.PinCount
  );

  CancellationTokenSource cts = new();

  // using ModbusRtuDfrobotCh432tSpiPort rtuSpiPort = new(
  //   spiConnectionSettings,
  //   Ch432tPortNumber.Port1,
  //   logger,
  //   stopBits: StopBits.One,
  //   parity: Parity.None,
  //   baudRate: baudRate
  // );
  // logger.LogInformation("Opening rtuSpiPort");
  // await rtuSpiPort.Open(cancellationToken: cts.Token);
  // logger.LogInformation("Opened rtuSpiPort");

  IModbusRtuSerialPort modbusPort;
  IDisposable disposable;

  if (device is not null)
  {
    var serialPort = new SerialPort(
      device,
      baudRate: baudRate,
      parity: Parity.None,
      dataBits: 8,
      stopBits: StopBits.OnePointFive
    );

    ModbusRtuSerialPort rtuSerialPort = new(serialPort);
    modbusPort = rtuSerialPort;
    disposable = serialPort;
  }
  else if (busId is not null)
  {

    SpiConnectionSettings spiConnectionSettings = new(
      busId: busId.Value,
      chipSelectLine: chipSelectLine
    )
    {
      Mode = spiMode ?? default,
    };

    ModbusRtuSpiPort rtuSpiPort = new(spiConnectionSettings, loggerFactory.CreateLogger<ModbusRtuSpiPort>());
    modbusPort = rtuSpiPort;
    disposable = rtuSpiPort;
  }
  else
  {
    throw new InvalidOperationException("No device or busId");
  }

  using IDisposable? _ = disposable;

  // using ModbusRtuSpiPort rtuSpiPort = new(spiConnectionSettings, logger);

  await using WeatherStationClient client = new(
    modbusPort,
    1,
    loggerFactory.CreateLogger<WeatherStationClient>()
  );

  Console.CancelKeyPress += (x, y) =>
  {
    programLogger.LogInformation("<CancelKeyPress>");
    // cancel the cancelling, then cancel the cancellation (token)
    y.Cancel = true;
    cts.Cancel();
  };

  try
  {
    stdout.WriteLine("{\n");
    while (!cts.Token.IsCancellationRequested && !client.Cancelled)
    {
      var results = await client.ReadAllDataAsync(cts.Token);
      stdout.WriteLine(
        "\"{0}\": {1},\n",
        DateTimeOffset.Now,
        JsonSerializer.Serialize(results, WeatherStationLibSerializerContext.Default.AllData)
      );
      // var results = await client.ReadWindSpeedAndDirectionAsync(cts.Token);
      // stdout.WriteLine(
      //   "\"{0}\": {1},\n",
      //   DateTimeOffset.Now,
      //   JsonSerializer.Serialize(results, WeatherStationLibSerializerContext.Default.WindSpeedAndDirection)
      // );

      if (loopTimeout is null)
        break;
      await Task.Delay(loopTimeout.Value, cts.Token);
    }
  }
  catch (OperationCanceledException)
  {
  }

  stdout.WriteLine("}");

  return 0;
}
