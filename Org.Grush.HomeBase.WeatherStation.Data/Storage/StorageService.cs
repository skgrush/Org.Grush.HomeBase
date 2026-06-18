using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Org.Grush.HomeBase.WeatherStation.Data.Storage.Entities;

namespace Org.Grush.HomeBase.WeatherStation.Data.Storage;

public class StorageService : IAsyncDisposable
{
  private readonly StorageDbContext _dbContext;
  private readonly ILogger<StorageService> _logger;

  internal StorageService(StorageDbContext dbContext, ILogger<StorageService> logger)
  {
    _dbContext = dbContext;
    _logger = logger;
  }

  public async Task AddEntryAsync(WeatherLogEntry entry, CancellationToken cancellationToken = default)
  {
    var dbe = _dbContext.Add(entry);
    try
    {
      await _dbContext.SaveChangesAsync(cancellationToken);
    }
    finally
    {
      if (dbe.State is EntityState.Added)
        dbe.State = EntityState.Detached;
    }
  }

  public readonly record struct AggregateData(
    float? AverageWindSpeed,
    float? PeakWindGust,
    double? LastHourRainfallMillimeters,
    double? LastDayRainfallMillimeters,
    double? TodayRainfallMillimeters
  );

  public async Task<WeatherLogEntry?> GetLastLogEntry(CancellationToken cancellationToken = default)
    => await _dbContext.Set<WeatherLogEntry>()
      .Where(e => e.StationData != null)
      .AsNoTracking()
      .LastOrDefaultAsync(cancellationToken);

  public async Task<AggregateData> GetDatumz(
    DateTime avgWindSpeedPeriod,
    DateTime maxWindGustPeriod,
    CancellationToken cancellationToken
  )
  {
    var now = DateTimeOffset.UtcNow;
    var lastHour = now.AddHours(-1);
    var last24hrs = now.AddDays(-1);
    var startOfDay = now.Date;

    DateTime minDate = maxWindGustPeriod < avgWindSpeedPeriod ? maxWindGustPeriod : avgWindSpeedPeriod;

    var speedData = await _dbContext.Set<WeatherLogEntry>()
      .Where(e => e.UtcTimestamp >= minDate && e.StationData != null)
      .GroupBy(_ => 1)
      .AsNoTracking()
      .Select(grouped => new
      {
        avgSpeed = grouped.Where(e => e.UtcTimestamp >= avgWindSpeedPeriod).Average(e => e.StationData!.WindSpeed),
        maxGust = grouped.Where(e => e.UtcTimestamp >= maxWindGustPeriod).Average(e => e.StationData!.WindSpeed),
      })
      .FirstOrDefaultAsync(cancellationToken);

    var sumData = await _dbContext.Set<WeatherLogEntry>()
      .Where(e => e.UtcTimestamp >= last24hrs && e.RainfallMillimetersSinceLastEntry != null)
      .GroupBy(_ => 1)
      .AsNoTracking()
      .Select(entries => new
      {
        lastHr = entries.Where(e => e.UtcTimestamp >= lastHour).Sum(e => e.RainfallMillimetersSinceLastEntry.Value),
        last24 = entries.Sum(e => e.RainfallMillimetersSinceLastEntry),
        today = entries.Where(e => e.UtcTimestamp >= startOfDay).Sum(e => e.RainfallMillimetersSinceLastEntry.Value),
      })
      .FirstOrDefaultAsync(cancellationToken);

    return new AggregateData(
      AverageWindSpeed: speedData?.avgSpeed,
      PeakWindGust: speedData?.maxGust,
      LastHourRainfallMillimeters: sumData?.lastHr,
      LastDayRainfallMillimeters: sumData?.last24,
      TodayRainfallMillimeters: sumData?.today
    );
  }

  public IAsyncEnumerable<WeatherLogEntry> GetLogEntriesSince(TimeSpan since)
    => GetLogEntriesSince(DateTimeOffset.UtcNow.Subtract(since));

  public IAsyncEnumerable<WeatherLogEntry> GetLogEntriesSince(DateTimeOffset since)
  {
    DateTime sinceUtc = since.UtcDateTime;

    return _dbContext.Set<WeatherLogEntry>()
      .Where(e => e.UtcTimestamp >= sinceUtc)
      .OrderBy(e => e.UtcTimestamp)
      .AsAsyncEnumerable();
  }

  public ValueTask DisposeAsync()
  {
    throw new NotImplementedException();
  }
}
