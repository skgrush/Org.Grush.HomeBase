// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using Iot.Device.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Org.Grush.HomeBase.Lib.Cron;
using Org.Grush.HomeBase.WeatherStation.Data;
using Org.Grush.HomeBase.WeatherStation.Cli;
using Org.Grush.HomeBase.WeatherStation.Service;

Console.SetOut(Console.Error);

ServiceCollection services = [];
services
  .AddKeyedTransient<Stream>(serviceKey: "stdout", implementationFactory: (_, _) => Console.OpenStandardOutput())
  .AddLogging()
  .AddSingleton<CronService>()
  .AddSingleton<SimpleConsoleLoggerFactory>()
  .AddSingleton<ILoggerFactory>(sp => sp.GetRequiredService<SimpleConsoleLoggerFactory>())
  .AddSingleton<RootCommandAction>()
  .AddSingleton<Factory>()
  .AddScoped<Service>()
  .AddAprsWxNetServices()
  .AddStorageDb(o => o.WithDbFile("Org.Grush.HomeBase.WeatherStation.Cli"))
;

await using var serviceProvider = services.BuildServiceProvider();


RootCommand command = new("HomeBase WeatherStationCli");
CliOptionResult.AddOptions(command.Options);
command.Action = serviceProvider.GetRequiredService<RootCommandAction>();

InvocationConfiguration invocationConfiguration = new()
{
  Output = Console.Error,
};

Console.Error.WriteLine($"Args: {string.Join(' ', args)}");

ParseResult parseResult = command.Parse(args);

if (parseResult.GetValue(CliOptionResult.LogLevelOption) is { } logLevel)
  serviceProvider.GetRequiredService<SimpleConsoleLoggerFactory>().MinLogLevel = logLevel;

int invoked = await parseResult.InvokeAsync(invocationConfiguration);
Console.Error.WriteLine($"Invoked: {invoked}");

return invoked;
