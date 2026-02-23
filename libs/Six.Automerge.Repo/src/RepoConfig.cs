using Six.Automerge.Repository.Network;
using Six.Automerge.Repository.Storage;

namespace Six.Automerge.Repository;

/// <summary>
/// Configuration for creating a Repo instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed record RepoConfig
{
  /// <summary>
  /// Unique identifier for this peer. If not specified, a new one will be generated.
  /// </summary>
  public PeerId? PeerId { get; init; }

  /// <summary>
  /// Storage adapter for document persistence. If null, documents are ephemeral.
  /// </summary>
  public IStorageAdapter? Storage { get; init; }

  /// <summary>
  /// Network adapters for peer-to-peer communication. Can be empty for local-only repos.
  /// </summary>
  public IReadOnlyList<INetworkAdapter>? NetworkAdapters { get; init; }

  /// <summary>
  /// If true, sync state will not be persisted even if storage is available.
  /// </summary>
  public bool IsEphemeral { get; init; }

  /// <summary>
  /// Debounce rate for document saves in milliseconds. Default: 100ms.
  /// </summary>
  public int SaveDebounceRate { get; init; } = 100;

  /// <summary>
  /// Debounce rate for sync message sending in milliseconds. Default: 100ms.
  /// When local changes occur rapidly, sync messages are batched and only sent
  /// after this quiet period to reduce network traffic.
  /// </summary>
  public int SyncDebounceRate { get; init; } = 100;

  /// <summary>
  /// Share configuration defining which documents to share with which peers.
  /// </summary>
  public ShareConfig? ShareConfig { get; init; }

  /// <summary>
  /// Set of document IDs that should never be synced.
  /// </summary>
  public ISet<string>? Denylist { get; init; }

  /// <summary>
  /// Optional custom document ID factory. If null, uses default UUID-based IDs.
  /// </summary>
  public Func<IReadOnlyList<string>, Task<byte[]>>? IdFactory { get; init; }

  /// <summary>
  /// Logger factory for creating loggers throughout the repo subsystems.
  /// If null, a NullLoggerFactory is used (no logging).
  /// </summary>
  public ILoggerFactory? LoggerFactory { get; init; }
}