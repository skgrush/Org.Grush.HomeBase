using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Org.Grush.HomeBase.Lib.Cron;


public sealed class CronService(ILoggerFactory loggerFactory) : IAsyncDisposable
{
  private readonly SemaphoreSlim _semaphore = new(1, 1);
  private readonly Dictionary<string, CronJob> _jobs = [];

  public async Task<CronJob> AddParameterizedJob<TCallContext, TReturn>(
    CronJob.CronJobDefinition<TCallContext, TReturn> jobDefinition
  ) => await AddJob(jobDefinition);

  public async Task<CronJob> AddJob(
    CronJob.CronJobDefinition jobDefinition
  )
  {
    await _semaphore.WaitAsync();
    try
    {
      if (_jobs.ContainsKey(jobDefinition.JobDescription))
        throw new InvalidOperationException($"Job '{jobDefinition.JobDescription}' already exists");

      CronJobIMPL job = new(jobDefinition, loggerFactory.CreateLogger($"CronJob:{jobDefinition.JobDescription}"));

      _jobs.Add(jobDefinition.JobDescription, job);
      job.StateChanged += (_, e) =>
      {
        if (e.State is CronJob.StateEnum.Disposed)
        {
          _jobs.Remove(jobDefinition.JobDescription);
        }
      };

      return job;
    }
    finally
    {
      _semaphore.Release();
    }
  }

  public async ValueTask DisposeAsync()
  {
    _semaphore.Dispose();

    await Task.WhenAll(
      _jobs.Select(kvp => kvp.Value.DisposeAsync().AsTask())
    );
  }


  public abstract class CronJob : IAsyncDisposable
  {
    public abstract event EventHandler<StateChangedEventArgs> StateChanged;
    public abstract StateEnum State { get; }
    public abstract CronJobDefinition Definition { get;}
    public abstract uint FinishedRunCount { get; protected set; }
    public abstract ValueTask DisposeAsync();
    public abstract Task<object?> ExecuteManually();
    public abstract Task StartSchedule();
    public abstract Task Stop(bool cancelRunning = false);
    public abstract CancellationTokenRegistration? StopWhen(CancellationToken cancellationToken);
    /// <summary>
    ///
    /// </summary>
    /// <param name="waitUntilState"></param>
    /// <param name="throwOnDispose"></param>
    /// <returns>A task only after the <see cref="State"/> matches <paramref name="waitUntilState"/></returns>
    /// <exception cref="ObjectDisposedException">if-and-only-if <paramref name="throwOnDispose"/> is true, <paramref name="waitUntilState"/> is NOT <see cref="StateEnum.Disposed"/>, and the job is disposed.</exception>
    public abstract Task WaitUntil(StateEnum waitUntilState, bool throwOnDispose = true);

    public sealed class StateChangedEventArgs : EventArgs
    {
      public required StateEnum State { get; init; }
      public required StateEnum OldState { get; init; }
    }

    public record CronJobRunContext(
      CronJob Job,
      ILogger Logger,
      DateTimeOffset StartTime,
      uint RunNumber
    );

    public sealed record CronJobRunContext<TCallContext>(
      CronJob Job,
      ILogger Logger,
      DateTimeOffset StartTime,
      uint RunNumber,
      TCallContext CallContext
    ) : CronJobRunContext(Job, Logger, StartTime, RunNumber)
    {
      public static CronJobRunContext<TCallContext> From(CronJobRunContext jobRunContext, TCallContext callContext)
        => new(
          Job: jobRunContext.Job,
          Logger: jobRunContext.Logger,
          StartTime: jobRunContext.StartTime,
          RunNumber: jobRunContext.RunNumber,
          CallContext: callContext
        );
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="JobDescription">Unique string describing the job.</param>
    /// <param name="Interval">Interval between runs.</param>
    /// <param name="JobFn">The function to run on each interval.</param>
    /// <param name="StartImmediately">If true, don't delay the first scheduled run.</param>
    public record CronJobDefinition(
      string JobDescription,
      TimeSpan Interval,
      CronJobDefinition.JobDelegate<CronJobRunContext, object?> JobFn,
      bool StartImmediately
    )
    {
      public delegate Task<TRet> JobDelegate<TJobContext, TRet>(TJobContext ctx, CancellationToken cancellationToken)
        where TJobContext : CronJobRunContext;
    }

    public record CronJobDefinition<TCallContext, TReturn>(
      string JobDescription,
      TimeSpan Interval,
      CronJobDefinition.JobDelegate<CronJobRunContext<TCallContext>, TReturn> ParameterizedJobFn,
      bool StartImmediately,
      TCallContext CallContext
    )
      : CronJobDefinition(
        JobDescription,
        Interval,
        JobFn: async (ctx, token) => await ParameterizedJobFn(CronJobRunContext<TCallContext>.From(ctx, CallContext), token),
        StartImmediately
      );


    public enum StateEnum : UInt32
    {
      Unstarted = 0,
      Scheduled = 1 << 8,
      _Running = 1 << 9,
      RunningScheduled = _Running | 1,
      RunningManually = _Running | 2,

      Stopped = 1 << 10,
      Disposed = 1 << 30,
    }
  }

  private class CronJobIMPL(
    CronJob.CronJobDefinition jobDefinition,
    ILogger logger
  ) : CronJob
  {
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    /// <summary> Job-specific CTS. Cancels running jobs and future schedules. </summary>
    private readonly CancellationTokenSource _runningJobCts = new();
    private readonly CancellationTokenSource _schedulerCts = new();
    // private CronService? ParentService { get; set; }

    private readonly ConcurrentBag<CancellationTokenRegistration> _stopRegistrations = new();

    private StateEnum _state = StateEnum.Unstarted;
    public override StateEnum State => _state;

    private event EventHandler<StateChangedEventArgs>? _stateChanged;
    public override event EventHandler<StateChangedEventArgs> StateChanged
    {
      add => _stateChanged += value;
      remove => _stateChanged -= value;
    }

    public override CronJobDefinition Definition => jobDefinition;
    public override uint FinishedRunCount { get; protected set; }

    public override async ValueTask DisposeAsync()
    {
      var oldState = Interlocked.Exchange(ref _state, StateEnum.Disposed);
      if (oldState is StateEnum.Disposed) return;

      // in case the job takes a beat to cancel, just hold it for a moment
      var runningCancelTask = _runningJobCts.CancelAsync();
      var schedulerCancelTask = _schedulerCts.CancelAsync();

      _semaphore.Dispose();

      await runningCancelTask;
      await schedulerCancelTask;
      _runningJobCts.Dispose();
      _schedulerCts.Dispose();

      EmitChange(oldState, StateEnum.Disposed);
    }

    // public async Task _Init(CronService service)
    // {
    //   if (ParentService != null) throw new();
    //   ParentService = service;
    // }

    public override Task<object?> ExecuteManually() => Execute(isManualRun: true);

    public override async Task StartSchedule()
    {
      await _semaphore.WaitAsync(_schedulerCts.Token);

      StateEnum oldState;
      try
      {
        if (State is not (StateEnum.Unstarted or StateEnum.Stopped))
          throw new InvalidOperationException("Already running");
        oldState = State;
        _state = StateEnum.Scheduled;
      }
      finally
      {
        _semaphore.Release();
      }

      EmitChange(oldState, _state);

      if (!Definition.StartImmediately)
        await Task.Delay(Definition.Interval, _runningJobCts.Token);

      while (true)
      {
        await Execute(isManualRun: false);
        await Task.Delay(Definition.Interval, _runningJobCts.Token);
      }
    }

    public override async Task Stop(bool cancelRunning = false)
    {
      await _semaphore.WaitAsync(millisecondsTimeout: 1);
      var oldState = Interlocked.Exchange(ref _state, StateEnum.Stopped);

      try
      {
        if (oldState is StateEnum.Stopped)
          return;

        var schedT = _schedulerCts.CancelAsync();
        if (cancelRunning)
        {
          await _runningJobCts.CancelAsync();
          _runningJobCts.TryReset();
        }
        await schedT;
        _schedulerCts.TryReset();

        EmitChange(oldState, StateEnum.Stopped);
      }
      finally
      {
        _semaphore.Release();
      }
    }

    public override Task WaitUntil(StateEnum waitUntilState, bool throwOnDispose = true)
    {
      TaskCompletionSource tsc = new();

      Func<StateEnum, bool> matches =
        Enum.IsDefined(waitUntilState)
          ? es => es == waitUntilState
          : es => waitUntilState.HasFlag(es);

      StateChanged += LocalOnStateChanged;

      return tsc.Task;

      void LocalOnStateChanged(object? _, StateChangedEventArgs e)
      {
        if (matches(e.State))
        {
          tsc.SetResult();
          StateChanged -= LocalOnStateChanged;
        }
        else if (e.State is StateEnum.Disposed)
        {
          if (throwOnDispose)
            tsc.SetException(new ObjectDisposedException(nameof(CronService)));
          StateChanged -= LocalOnStateChanged;
        }
      }
    }

    public override CancellationTokenRegistration? StopWhen(CancellationToken cancellationToken)
      => cancellationToken.Register(() => _runningJobCts.Cancel());

    private async Task<object?> Execute(bool isManualRun)
    {
      StateEnum oldState;
      StateEnum newState = 0;
      await _semaphore.WaitAsync(_runningJobCts.Token);
      try
      {
        oldState = _state;
        if (_state.HasFlag(StateEnum._Running))
        {
          logger.LogWarning("Attempt to run (isManualRun={isManualRun}) while already running; skipping...", isManualRun);
          return null;
        }

        if (
          isManualRun
            ? _state is (StateEnum.Stopped or StateEnum.Disposed)
            : _state is not StateEnum.Scheduled
        )
          throw new InvalidOperationException($"Invalid existing state for isManualRun={isManualRun}: {_state}");

        newState = _state = isManualRun ? StateEnum.RunningManually : StateEnum.RunningScheduled;

        uint runNumber = FinishedRunCount + 1;

        try
        {
          using var _ = logger.BeginScope("[Execution {state} #{num}]", _state, runNumber);
          EmitChange(oldState, newState);

          object? result = await jobDefinition.JobFn(new(this, logger, DateTimeOffset.Now, runNumber), _runningJobCts.Token);
          Interlocked.CompareExchange(ref _state, oldState, newState);
          logger.LogDebug("Execution succeeded");
          if (_state == oldState)
            EmitChange(newState, oldState);

          return result;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
          logger.LogError(e, "Execution failed");
          if (newState is not StateEnum.Unstarted)
            Interlocked.CompareExchange(ref _state, oldState, newState);
          if (isManualRun)
            throw;
          return null;
        }
      }
      finally
      {
        ++FinishedRunCount;
        _semaphore.Release();
      }
    }

    private void EmitChange(StateEnum old, StateEnum newState)
      => _stateChanged?.Invoke(this, new()
      {
        OldState = old,
        State = newState,
      });
  }
}
