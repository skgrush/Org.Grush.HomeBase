using Microsoft.Extensions.DependencyInjection;
using Org.Grush.HomeBase.WeatherStation.Data.Storage;

namespace Org.Grush.HomeBase.WeatherStation.Data;

public static class StartupExtensions
{
  extension(IServiceCollection serviceCollection)
  {
    public IServiceCollection AddAprsWxNetServices()
      => serviceCollection
        .AddSingleton<AprsWxNetPacketSerializer>()
        .AddScoped<AprsWxNetReporterService>()
      ;

    public IServiceCollection AddStorageDb()
      => serviceCollection
        .AddScoped<StorageDbSettings>() // defaults
        .AddDbContext<StorageDbContext>()
        .AddScoped<StorageService>()
      ;

    public IServiceCollection AddStorageDb(Func<StorageDbSettings, StorageDbSettings> configure)
    {
      return serviceCollection
        .AddStorageDb()
        .AddScoped<StorageDbSettings>(_ => configure(new()));
    }
  }
}
