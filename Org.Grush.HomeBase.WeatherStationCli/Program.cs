// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using System.CommandLine.Help;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.Device.Spi;
using System.IO.Ports;
using System.Text.Json;
using Iot.Device.Common;
using Microsoft.Extensions.Logging;
using Org.Grush.HomeBase.WeatherStationLib;
using Org.Grush.Port.DFRobot.CH432T;

Option<int> busIdOption = new("--bus")
{
  Description = "Specifies the bus identifier, e.g. the X in /dev/spidevX.Y",
  Arity = ArgumentArity.ExactlyOne,
  Required = true,
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
  baudRateOption,
  spiModeOption,
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

  int busId = parseResult.GetRequiredValue(busIdOption);
  int chipSelectLine = parseResult.GetValue(chipSelectLineOption) ?? -1;
  SpiMode? spiMode = parseResult.GetValue(spiModeOption);
  int baudRate = parseResult.GetValue(baudRateOption);
  byte? loopTimeout =
      parseResult.GetResult(loopOption) is null
        ? null
        : (parseResult.GetValue(loopOption) ?? 5)
    ;

  using LibGpiodV2Driver driver = new(4);
  using GpioController controller = new(driver);

  SimpleConsoleLogger logger = new("Program");

  SpiConnectionSettings spiConnectionSettings = new(
    busId: busId,
    chipSelectLine: chipSelectLine
  )
  {
    ClockFrequency = 1_000_000,
    DataBitLength = 8,
  };
  if (spiMode is not null)
    spiConnectionSettings.Mode = spiMode.Value;

  CancellationTokenSource cts = new();

  using ModbusRtuDfrobotCh432tSpiPort rtuSpiPort = new(
    spiConnectionSettings,
    Ch432tPortNumber.Port1,
    logger,
    stopBits: StopBits.One,
    parity: Parity.None,
    baudRate: baudRate
  );
  logger.LogInformation("Opening rtuSpiPort");
  await rtuSpiPort.Open(cancellationToken: cts.Token);
  logger.LogInformation("Opened rtuSpiPort");

  await using WeatherStationClient client = new(
    rtuSpiPort,
    1
  );

  Console.CancelKeyPress += (x, y) =>
  {
    logger.LogInformation("<CancelKeyPress>");
    // cancel the cancelling, then cancel the cancellation (token)
    y.Cancel = true;
    cts.Cancel();
  };

  try
  {
    Console.WriteLine("{\n");
    while (!cts.Token.IsCancellationRequested && !client.Cancelled)
    {
      // var results = await client.ReadAllDataAsync();
      // Console.WriteLine(
      //   "\"{0}\": {1},\n",
      //   DateTimeOffset.Now,
      //   JsonSerializer.Serialize(results, WeatherStationLibSerializerContext.Default.AllData)
      // );
      var results = await client.ReadWindSpeedAndDirectionAsync(cts.Token);
      Console.WriteLine(
        "\"{0}\": {1},\n",
        DateTimeOffset.Now,
        JsonSerializer.Serialize(results, WeatherStationLibSerializerContext.Default.WindSpeedAndDirection)
      );

      if (loopTimeout is null)
        break;
      await Task.Delay(loopTimeout.Value, cts.Token);
    }
  }
  catch (OperationCanceledException)
  {
  }

  Console.WriteLine("}");

  return 0;
}
