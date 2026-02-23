// Copyright (c) Six Technology, Inc. All rights reserved.
// Licensed under the MIT license.

using Six.Automerge.Repository.Storage;

namespace Six.Automerge.SyncServer;

/// <summary>
/// Configuration options for <see cref="SyncServer"/>.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed record SyncServerOptions
{
  /// <summary>
  /// Storage adapter for document persistence. Required.
  /// </summary>
  public required IStorageAdapter Storage { get; init; }

  /// <summary>
  /// Interval between persistence flushes for dirty documents. Default: 5 seconds.
  /// </summary>
  public TimeSpan PersistenceInterval { get; init; } = TimeSpan.FromSeconds(5);

  /// <summary>
  /// Debounce rate for sync message sending in milliseconds. Default: 100ms.
  /// </summary>
  public int SyncDebounceRate { get; init; } = 100;

  /// <summary>
  /// Debounce rate for document saves in milliseconds. Default: 100ms.
  /// </summary>
  public int SaveDebounceRate { get; init; } = 100;

  /// <summary>
  /// Logger factory for creating loggers. If null, no logging is performed.
  /// </summary>
  public ILoggerFactory? LoggerFactory { get; init; }
}
