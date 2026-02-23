using Six.Automerge.Repository;

namespace Six.Automerge.Repository.Network;

/// <summary>
/// Ephemeral (transient) message not persisted in document history.
/// Used for presence, cursors, etc.
/// </summary>
[PublicAPI]
public sealed record EphemeralMessage : IRepoMessage
{
  public string MessageType => "ephemeral";
  public required DocumentId DocumentId { get; init; }
  public required byte[] Data { get; init; }
  public PeerId? SenderId { get; init; }
  public PeerId? TargetPeerId { get; init; }

  /// <summary>
  /// Sequence number for deduplication.
  /// </summary>
  public required int Count { get; init; }

  /// <summary>
  /// Session ID for deduplication.
  /// </summary>
  public required SessionId SessionId { get; init; }

  DocumentId? IRepoMessage.DocumentId => DocumentId;
}
