namespace Six.Automerge.Repository.Helpers;

/// <summary>
/// Helper methods for working with tasks and cancellation.
/// </summary>
internal static class TaskHelper
{
  /// <summary>
  /// Wraps a task with a timeout.
  /// </summary>
  /// <typeparam name="T">The task result type.</typeparam>
  /// <param name="task">The task to wrap.</param>
  /// <param name="timeout">The timeout duration.</param>
  /// <returns>The task result.</returns>
  /// <exception cref="TimeoutException">Thrown if the task times out.</exception>
  public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout)
  {
    if(task is null)
      throw new ArgumentNullException(nameof(task));

    using var cts = new CancellationTokenSource();
    var delayTask = Task.Delay(timeout, cts.Token);
    var completedTask = await Task.WhenAny(task, delayTask);

    if(completedTask == delayTask)
    {
      throw new TimeoutException($"Task timed out after {timeout.TotalSeconds:F1} seconds");
    }

    cts.Cancel(); // Cancel the delay task
    return await task;
  }

  /// <summary>
  /// Wraps a task with a timeout.
  /// </summary>
  /// <param name="task">The task to wrap.</param>
  /// <param name="timeout">The timeout duration.</param>
  /// <exception cref="TimeoutException">Thrown if the task times out.</exception>
  public static async Task WithTimeout(this Task task, TimeSpan timeout)
  {
    if(task is null)
      throw new ArgumentNullException(nameof(task));

    using var cts = new CancellationTokenSource();
    var delayTask = Task.Delay(timeout, cts.Token);
    var completedTask = await Task.WhenAny(task, delayTask);

    if(completedTask == delayTask)
    {
      throw new TimeoutException($"Task timed out after {timeout.TotalSeconds:F1} seconds");
    }

    cts.Cancel(); // Cancel the delay task
    await task;
  }

  /// <summary>
  /// Creates a task that completes when an event fires.
  /// Useful for implementing WhenReady patterns.
  /// </summary>
  /// <typeparam name="TEventArgs">The event args type.</typeparam>
  /// <param name="subscribe">Action to subscribe to the event.</param>
  /// <param name="unsubscribe">Action to unsubscribe from the event.</param>
  /// <param name="cancellationToken">Optional cancellation token.</param>
  /// <returns>A task that completes with the event args when the event fires.</returns>
  /// <example>
  /// <code>
  /// var task = TaskHelper.FromEvent&lt;ReadyEventArgs&gt;(
  ///     handler => handle.Ready += handler,
  ///     handler => handle.Ready -= handler);
  /// await task;
  /// </code>
  /// </example>
  public static Task<TEventArgs> FromEvent<TEventArgs>(
    Action<EventHandler<TEventArgs>> subscribe,
    Action<EventHandler<TEventArgs>> unsubscribe,
    CancellationToken cancellationToken = default)
    where TEventArgs : EventArgs
  {
    if(subscribe is null)
      throw new ArgumentNullException(nameof(subscribe));
    if(unsubscribe is null)
      throw new ArgumentNullException(nameof(unsubscribe));

    var tcs = new TaskCompletionSource<TEventArgs>();

    EventHandler<TEventArgs>? handler = null;
    handler = (sender, args) =>
    {
      unsubscribe(handler!);
      tcs.TrySetResult(args);
    };

    subscribe(handler);

    if(cancellationToken.CanBeCanceled)
    {
      cancellationToken.Register(() =>
      {
        unsubscribe(handler);
        tcs.TrySetCanceled(cancellationToken);
      });
    }

    return tcs.Task;
  }

  /// <summary>
  /// Creates a task that completes when an event fires (non-generic EventArgs).
  /// </summary>
  /// <param name="subscribe">Action to subscribe to the event.</param>
  /// <param name="unsubscribe">Action to unsubscribe from the event.</param>
  /// <param name="cancellationToken">Optional cancellation token.</param>
  /// <returns>A task that completes when the event fires.</returns>
  public static Task FromEvent(
    Action<EventHandler> subscribe,
    Action<EventHandler> unsubscribe,
    CancellationToken cancellationToken = default)
  {
    if(subscribe is null)
      throw new ArgumentNullException(nameof(subscribe));
    if(unsubscribe is null)
      throw new ArgumentNullException(nameof(unsubscribe));

    var tcs = new TaskCompletionSource<bool>();

    EventHandler? handler = null;
    handler = (sender, args) =>
    {
      unsubscribe(handler!);
      tcs.TrySetResult(true);
    };

    subscribe(handler);

    if(cancellationToken.CanBeCanceled)
    {
      cancellationToken.Register(() =>
      {
        unsubscribe(handler);
        tcs.TrySetCanceled(cancellationToken);
      });
    }

    return tcs.Task;
  }
}
