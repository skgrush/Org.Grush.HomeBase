// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using System.CommandLine.Help;
using System.Device.Spi;
using System.Text.Json;
using Iot.Device.Board;
using Org.Grush.HomeBase.WeatherStationLib;

Option<int> busIdOption = new("--bus")
{
  Description = "Specifies the bus identifier, e.g. the X in /dev/spidevX.Y",
  Arity = ArgumentArity.ExactlyOne,
  Required = true,
};

Option<int?> chipSelectLineOption = new("--chip-select-line")
{
  Description = "Specifies the bus identifier, e.g. the Y in /dev/spidevX.Y",
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
HelpOption helpOption = new();

RootCommand command = new("HomeBase WeatherStationCli")
{
  busIdOption,
  baudRateOption,
  helpOption,
};

Console.Error.WriteLine($"Args: {string.Join(' ', args)}");
ParseResult parseResult = command.Parse(args);
var invoked = await parseResult.InvokeAsync();
Console.Error.WriteLine($"Invoked: {invoked}");

if (parseResult.GetResult(helpOption) is not null)
  return await parseResult.InvokeAsync();

if (parseResult.Errors.Count is not 0)
{
  foreach (var error in parseResult.Errors)
    Console.WriteLine(error);
  return 1;
}



using Board board = Board.Create();

try
{
  var componentInformation = board.QueryComponentInformation();
  BoardPrinter.PrintComponentInfo(componentInformation, "");
}
catch {}

if (board is RaspberryPiBoard piBoard)
{
  if (!piBoard.IsSpiActivated())
  {
    Console.Error.WriteLine("SPI is not activated");
    return 2;
  }
}

using SpiDevice spiDevice = board.CreateSpiDevice(new(
  busId: parseResult.GetRequiredValue(busIdOption),
  chipSelectLine: parseResult.GetValue(chipSelectLineOption) ?? -1
));
using ModbusRtuSpiPort rtuSpiPort = new(spiDevice);

await using WeatherStationClient client = new(
  rtuSpiPort,
  1,
  parseResult.GetValue(baudRateOption)
);

byte? loopTimeout =
  parseResult.GetResult(loopOption) is null
    ? null
    : (parseResult.GetValue(loopOption) ?? 5)
;

CancellationTokenSource cts = new();
Console.CancelKeyPress += (_, _) => cts.Cancel();

try
{
  Console.WriteLine("{\n");
  while (!cts.Token.IsCancellationRequested && !client.Cancelled)
  {
    var results = await client.ReadAllDataAsync();
    Console.WriteLine(
      "\"{0}\": {1},\n",
      DateTimeOffset.Now,
      JsonSerializer.Serialize(results, WeatherStationLibSerializerContext.Default.AllData)
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
