using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Org.Grush.HomeBase.APRSWXNET.Storage.Entities;

namespace Org.Grush.HomeBase.APRSWXNET.Storage;

public class StorageDbContext(StorageDbSettings settings) : DbContext
{
  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
  {
    if (settings.DbFile is null)
      throw new InvalidOperationException($"{nameof(StorageDbSettings)}.{nameof(settings.DbFile)} is not configured");

    DbConnectionStringBuilder builder = new()
    {
      { "Data Source", settings.DbFile.FullName },
    };

    optionsBuilder.UseSqlite(builder.ConnectionString);
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<WeatherLogEntry>(WeatherLogEntry.Configure);
  }
}
