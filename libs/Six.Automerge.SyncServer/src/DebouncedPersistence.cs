// Copyright (c) Six Technology, Inc. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Concurrent;
using AutomergeRepo = Six.Automerge.Repository.Repo;

namespace Six.Automerge.SyncServer;

/// <summary>
/// Tracks dirty documents and periodically flushes them to storage using the Repo's
/// persistence mechanism. Runs a background timer at the configured persistence interval.
/// </summary>
internal sealed class DebouncedPersistence : IAsyncDisposable
{
  readonly ConcurrentDictionary<string, bool> _dirtyDocuments = new();
  readonly AutomergeRepo _repo;
  readonly ILogger _logger;
  readonly CancellationTokenSource _cts = new();
  readonly Task _timerTask;

  /// <summary>
  /// Creates a new debounced persistence tracker.
  /// </summary>
  /// <param name="repo">The repo to flush documents through.</param>
  /// <param name="interval">The interval between flush cycles.</param>
  /// <param name="loggerFactory">Logger factory for diagnostics.</param>
  public DebouncedPersistence(AutomergeRepo repo, TimeSpan interval, ILoggerFactory? loggerFactory)
  {
    _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    _logger = loggerFactory?.CreateLogger<DebouncedPersistence>()
              ?? NullLoggerFactory.Instance.CreateLogger<DebouncedPersistence>();
    _timerTask = RunTimerAsync(interval, _cts.Token);
  }

  /// <summary>
  /// Marks a document as dirty so it will be flushed on the next timer tick.
  /// </summary>
  /// <param name="documentId">The document ID to mark dirty.</param>
  public void MarkDirty(string documentId)
  {
    _dirtyDocuments.TryAdd(documentId, true);
  }

  /// <summary>
  /// Gets the number of documents currently marked dirty.
  /// </summary>
  internal int DirtyCount => _dirtyDocuments.Count;

  /// <summary>
  /// Immediately flushes all dirty documents without waiting for the timer.
  /// </summary>
  public async Task FlushAsync()
  {
    var dirtyIds = DrainDirtyDocuments();
    if(dirtyIds.Count == 0) return;

    _logger.LogDebug("Flushing {Count} dirty documents", dirtyIds.Count);

    try
    {
      await _repo.FlushAsync(dirtyIds).ConfigureAwait(false);
    }
    catch(ObjectDisposedException)
    {
      // Repo was disposed during flush — safe to ignore
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Error flushing dirty documents");
    }
  }

  async Task RunTimerAsync(TimeSpan interval, CancellationToken cancellationToken)
  {
    using var timer = new PeriodicTimer(interval);

    try
    {
      while(await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
      {
        await FlushAsync().ConfigureAwait(false);
      }
    }
    catch(OperationCanceledException)
    {
      // Expected on disposal
    }
  }

  List<string> DrainDirtyDocuments()
  {
    var ids = new List<string>();

    foreach(var key in _dirtyDocuments.Keys)
    {
      if(_dirtyDocuments.TryRemove(key, out _))
      {
        ids.Add(key);
      }
    }

    return ids;
  }

  /// <summary>
  /// Stops the timer and flushes any remaining dirty documents.
  /// </summary>
  public async ValueTask DisposeAsync()
  {
    _cts.Cancel();

    try
    {
      await _timerTask.ConfigureAwait(false);
    }
    catch(OperationCanceledException)
    {
      // Expected
    }

    // Final flush of any remaining dirty documents
    await FlushAsync().ConfigureAwait(false);

    _cts.Dispose();
  }
}
