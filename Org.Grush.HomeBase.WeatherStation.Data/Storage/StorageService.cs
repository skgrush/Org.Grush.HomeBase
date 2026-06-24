using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Org.Grush.HomeBase.WeatherStation.Data.APRSWXNET;
using Org.Grush.HomeBase.WeatherStation.Data.Storage.Entities;

namespace Org.Grush.HomeBase.WeatherStation.Data.Storage;

public class StorageService : IAsyncDisposable
{
  private readonly StorageDbContext _dbContext;
  private readonly ILogger<StorageService> _logger;

  private readonly SemaphoreSlim _semaphore = new(1, 1);

  public static readonly TimeSpan SemaphorePeriod = TimeSpan.FromSeconds(30);

  internal StorageService(StorageDbContext dbContext, ILogger<StorageService> logger)
  {
    _dbContext = dbContext;
    _logger = logger;
  }

  public async Task AddEntryAsync(WeatherLogEntry entry, CancellationToken cancellationToken = default)
  {
    await using var tx = await TransactionManager.BeginTransactionAsync(this, cancellationToken);

    _dbContext.Add(entry);

    await _dbContext.SaveChangesAsync(cancellationToken);
    await tx.CommitAsync(cancellationToken);
  }

  public async Task<SubmittedReport> AddSuccessfulReportAsync(
    AprsWxNetStationInformation stationInformation,
    AprsWxNetPacketBody packetBody,
    CancellationToken cancellationToken = default
  )
  {
    await using var tx = await TransactionManager.BeginTransactionAsync(this, cancellationToken);

    var report = SubmittedReport.From(packetBody, stationInformation);

    _dbContext.Add(report);

    await _dbContext.SaveChangesAsync(cancellationToken);
    await tx.CommitAsync(cancellationToken);

    return report;
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

  public async Task<AggregateData> GetAggregateData(
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

  private class TransactionManager : IAsyncDisposable
  {
    private readonly StorageService _store;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task<IDbContextTransaction> _transactionTask;
    public IDbContextTransaction Transaction => _transactionTask.Result;

    public bool SuccessfullyCommitted { get; private set; }

    private TransactionManager(StorageService store, CancellationToken cancellationToken)
    {
      _store = store;

      if (ChangeTrackerChanges() is { Count: > 0 } bads )
        throw new InvalidOperationException($"{bads.Count} bads");

      if (store._dbContext.Database.CurrentTransaction is not null)
        throw new InvalidOperationException("Outstanding transaction");

      _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

      _transactionTask = store._dbContext.Database.BeginTransactionAsync(_cancellationTokenSource.Token);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
      await Transaction.CommitAsync(cancellationToken);
      SuccessfullyCommitted = true;
    }

    public static async Task<TransactionManager> BeginTransactionAsync(StorageService store, CancellationToken cancellationToken)
    {
      if (!await store._semaphore.WaitAsync(SemaphorePeriod, cancellationToken))
        throw new TimeoutException("BeginTransactionAsync timed out");

      TransactionManager manager;
      try
      {
        manager = new(store, cancellationToken);
      }
      catch
      {
        store._semaphore.Release();
        throw;
      }

      try
      {
        await manager._transactionTask;
        return manager;
      }
      catch
      {
        store._semaphore.Release();
        await manager.DisposeAsync();
        throw;
      }
    }

    public List<EntityEntry> ChangeTrackerChanges()
      => _store._dbContext.ChangeTracker.Entries()
        .Where(e => e.State is not (EntityState.Detached or EntityState.Unchanged))
        .ToList();

    public async ValueTask DisposeAsync()
    {
      if (_cancellationTokenSource.IsCancellationRequested)
        return;

      await _cancellationTokenSource.CancelAsync();
      _cancellationTokenSource.Dispose();

      if (!_transactionTask.IsCompletedSuccessfully)
        return;

      var bads = ChangeTrackerChanges();
      await Transaction.DisposeAsync();

      if (bads.Count is not 0)
      {
        _store._dbContext.ChangeTracker.Clear();
      }

      _store._semaphore.Release();
    }
  }

  public ValueTask DisposeAsync()
  {
    throw new NotImplementedException();
  }
}
