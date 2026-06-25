using System.Runtime.CompilerServices;

namespace Org.Grush.HomeBase.WeatherStation.Lib.SEN0575;

public class RainSenseService(double standardMillimetersPerTip = RainSenseService.DefaultMillimetersPerTip)
{
  public const double DefaultMillimetersPerTip = 0.2794;

  private readonly Lock _lock = new();
  private readonly Stack<SavedEntry> _savedEntries = [];
  private uint _latestTips = 0;

  public EventSubScope Start(
    Action startup,
    Func<Task?> cleanup
  )
  {
    var now = DateTimeOffset.Now;
    _savedEntries.Push(new(
      now,
      now,
      RainfallInMillimeters: 0
    ));

    return new EventSubScope(
      startup,
      cleanup
    );
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void AddRainTipInterrupt()
  {
    Interlocked.Increment(ref _latestTips);
  }

  public SavedEntry? SaveRainfall()
  {
    uint currentTips = Interlocked.Exchange(ref _latestTips, 0);
    lock (_lock)
    {
      if (!_savedEntries.TryPeek(out SavedEntry last))
        return null;

      DateTimeOffset now = DateTimeOffset.Now;

      SavedEntry saved = new(
        Start: last.End,
        End: now,
        RainfallInMillimeters: currentTips * standardMillimetersPerTip
      );
      _savedEntries.Push(saved);
      return saved;
    }
  }

  public sealed record SavedEntry(
    DateTimeOffset Start,
    DateTimeOffset End,
    double RainfallInMillimeters
  ) // : IEntry
  {
    //DateTimeOffset? IEntry.End => End;
    public bool Complete => true;
  }

  public readonly struct EventSubScope : IAsyncDisposable
  {
    private readonly Func<Task?> _cleanup;

    public EventSubScope(Action startup, Func<Task?> cleanup)
    {
      startup();
      _cleanup = cleanup;
    }

    public ValueTask DisposeAsync()
    {
      var returned = _cleanup();
      if (returned != null)
        return new(returned);
      return ValueTask.CompletedTask;
    }
  }
}
