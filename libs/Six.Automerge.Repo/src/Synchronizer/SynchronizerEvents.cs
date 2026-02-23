using Six.Automerge.Repository.Network;
using Six.Automerge.Repository.Storage;

namespace Six.Automerge.Repository.Synchronizer;

/// <summary>
/// Event args for outgoing sync messages.
/// </summary>
[PublicAPI]
public sealed class SyncMessageEventArgs : EventArgs
{
  public required DocumentId DocumentId { get; init; }
  public required PeerId TargetPeerId { get; init; }
  public required IRepoMessage Message { get; init; }
}

/// <summary>
/// Event args for sync state changes (for persistence).
/// </summary>
[PublicAPI]
public sealed class SyncStateChangedEventArgs : EventArgs
{
  public required DocumentId DocumentId { get; init; }
  public required PeerId PeerId { get; init; }
  public required AutomergeSyncState SyncState { get; init; }
  public required StorageId? StorageId { get; init; }
}

/// <summary>
/// Event args for sync metrics.
/// </summary>
[PublicAPI]
public sealed class SyncMetricsEventArgs : EventArgs
{
  public required DocumentId DocumentId { get; init; }
  public required PeerId PeerId { get; init; }
  public required int MessagesSent { get; init; }
  public required int MessagesReceived { get; init; }
  public required bool IsComplete { get; init; }
}

/// <summary>
/// Event args for document unavailability.
/// </summary>
[PublicAPI]
public sealed class DocumentUnavailableEventArgs : EventArgs
{
  public required DocumentId DocumentId { get; init; }
}
