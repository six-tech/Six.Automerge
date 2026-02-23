namespace Six.Automerge.Repository.Helpers;

/// <summary>
/// Throttles function execution to prevent excessive calls.
/// When scheduled multiple times for the same key, only the last call executes after the delay.
/// </summary>
/// <typeparam name="T">The argument type (used as throttle key).</typeparam>
internal sealed class Throttle<T> : IDisposable where T : notnull
{
  readonly Func<T, Task> _action;
  readonly int _delayMilliseconds;
  readonly Dictionary<T, CancellationTokenSource> _pending = [];
  readonly SemaphoreSlim _lock = new(1, 1);
  bool _disposed;

  /// <summary>
  /// Creates a new throttle instance.
  /// </summary>
  /// <param name="action">The action to execute (receives the key as parameter).</param>
  /// <param name="delayMilliseconds">The delay in milliseconds before execution.</param>
  public Throttle(Func<T, Task> action, int delayMilliseconds)
  {
    _action = action ?? throw new ArgumentNullException(nameof(action));
    if(delayMilliseconds < 0)
      throw new ArgumentOutOfRangeException(nameof(delayMilliseconds), "Delay must be non-negative");

    _delayMilliseconds = delayMilliseconds;
  }

  /// <summary>
  /// Schedules the action to run after the delay period.
  /// If called multiple times for the same key, only the last call will execute.
  /// </summary>
  /// <param name="key">The key identifying this throttled operation.</param>
  public void Schedule(T key)
  {
    if(_disposed)
      throw new ObjectDisposedException(nameof(Throttle<T>));

    _lock.Wait();
    try
    {
      // Cancel existing pending action for this key
      if(_pending.TryGetValue(key, out var existingCts))
      {
        existingCts.Cancel();
        existingCts.Dispose();
      }

      // Schedule new action
      var cts = new CancellationTokenSource();
      _pending[key] = cts;

      Task.Delay(_delayMilliseconds, cts.Token)
        .ContinueWith(async _ =>
        {
          if(cts.IsCancellationRequested) return;

          try
          {
            await _action(key);
          }
          catch
          {
            // Swallow exceptions from the action to prevent unobserved task exceptions
            // The action should handle its own exceptions
          }
          finally
          {
            _lock.Wait();
            try
            {
              _pending.Remove(key);
              cts.Dispose();
            }
            finally
            {
              _lock.Release();
            }
          }
        }, TaskScheduler.Default);
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <summary>
  /// Cancels any pending action for the specified key.
  /// </summary>
  /// <param name="key">The key identifying the operation to cancel.</param>
  public void Cancel(T key)
  {
    if(_disposed) return;

    _lock.Wait();
    try
    {
      if(_pending.TryGetValue(key, out var cts))
      {
        cts.Cancel();
        cts.Dispose();
        _pending.Remove(key);
      }
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <summary>
  /// Disposes the throttle, canceling all pending operations.
  /// </summary>
  public void Dispose()
  {
    if(_disposed) return;

    _lock.Wait();
    try
    {
      foreach(var cts in _pending.Values)
      {
        cts.Cancel();
        cts.Dispose();
      }
      _pending.Clear();
      _disposed = true;
    }
    finally
    {
      _lock.Release();
    }

    _lock.Dispose();
  }
}
