using Six.Automerge.Repository.Storage;

namespace Six.Automerge.Repository.Network;

/// <summary>
/// Metadata about a peer connection.
/// </summary>
[PublicAPI]
public sealed record PeerMetadata
{
  /// <summary>
  /// Storage identifier for this peer. If provided, sync state will be persisted.
  /// </summary>
  public StorageId? StorageId { get; init; }

  /// <summary>
  /// If true, sync state will not be persisted for this peer.
  /// </summary>
  public bool IsEphemeral { get; init; }

  /// <summary>
  /// Custom metadata for application use.
  /// </summary>
  public IReadOnlyDictionary<string, object>? CustomData { get; init; }
}