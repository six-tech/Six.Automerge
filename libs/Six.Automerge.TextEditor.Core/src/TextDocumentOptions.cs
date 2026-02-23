// Copyright (c) Six. Licensed under the MIT License.

using System.Reactive.Concurrency;

namespace Six.Automerge.TextEdit;

/// <summary>
/// Configuration options for creating or opening a <see cref="TextDocument"/>.
/// </summary>
[PublicAPI]
public sealed record TextDocumentOptions
{
  /// <summary>
  /// The key in the document root map where the text CRDT object is stored.
  /// Default is "content".
  /// </summary>
  public string TextFieldKey { get; init; } = "content";

  /// <summary>
  /// Debounce silence period in milliseconds for batching local text changes.
  /// Default is 50ms.
  /// </summary>
  public int ChangeDebounceMs { get; init; } = 50;

  /// <summary>
  /// Debounce silence period in milliseconds for coalescing remote refresh events.
  /// Prevents flooding from rapid <c>DocHandle.Changed</c> events during large pastes.
  /// Default is 100ms.
  /// </summary>
  public int RemoteRefreshDebounceMs { get; init; } = 100;

  /// <summary>
  /// Debounce rate in milliseconds for sync message sending.
  /// Maps to <c>RepoConfig.SyncDebounceRate</c> when used with factory methods.
  /// Default is 100ms.
  /// </summary>
  public int SyncDebounceRate { get; init; } = 100;

  /// <summary>
  /// Whether to flush pending debounced changes when the <see cref="TextDocument"/> is disposed.
  /// Default is true.
  /// </summary>
  public bool AutoFlushOnDispose { get; init; } = true;

  /// <summary>
  /// Scheduler for Rx pipelines. Defaults to <see cref="DefaultScheduler.Instance"/>.
  /// Override with <c>TestScheduler</c> for deterministic unit tests.
  /// </summary>
  internal IScheduler? Scheduler { get; init; }
}
