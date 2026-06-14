// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using Iot.Device.Common;
using Org.Grush.HomeBase.WeatherStationCli;

await using Stream stdoutStream = Console.OpenStandardOutput();
Console.SetOut(Console.Error);
using SimpleConsoleLoggerFactory loggerFactory = new();

RootCommand command = new("HomeBase WeatherStationCli");
CliOptionResult.AddOptions(command.Options);
command.Action = new CliExecutor(stdoutStream, loggerFactory);

InvocationConfiguration invocationConfiguration = new()
{
  Output = Console.Error,
};

CancellationTokenSource cts = new();
Console.CancelKeyPress += (x, y) =>
{
  Console.Error.WriteLine("<CancelKeyPress>");
  // cancel the cancelling, then cancel the cancellation (token)
  y.Cancel = true;
  cts.Cancel();
};

Console.Error.WriteLine($"Args: {string.Join(' ', args)}");

ParseResult parseResult = command.Parse(args);
if (parseResult.GetValue(CliOptionResult.LogLevelOption) is { } logLevel)
  loggerFactory.MinLogLevel = logLevel;

int invoked = await parseResult.InvokeAsync(invocationConfiguration, cts.Token);
Console.Error.WriteLine($"Invoked: {invoked}");

return invoked;
