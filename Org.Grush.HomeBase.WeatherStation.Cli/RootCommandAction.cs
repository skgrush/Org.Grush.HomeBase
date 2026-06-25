using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;
namespace Org.Grush.HomeBase.WeatherStation.Cli;

public class RootCommandAction(
  IServiceScopeFactory scopeFactory
) : AsynchronousCommandLineAction
{
  public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
  {
    CliOptionResult result = CliOptionResult.From(parseResult);

    await using var scope = scopeFactory.CreateAsyncScope();

    var executor = scope.ServiceProvider.GetRequiredService<Service.Service>();

    return await executor.ExecuteAsync(result);
  }
}
