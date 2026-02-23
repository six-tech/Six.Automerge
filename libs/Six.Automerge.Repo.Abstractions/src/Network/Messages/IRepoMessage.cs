using Six.Automerge.Repository;

namespace Six.Automerge.Repository.Network;

/// <summary>
/// Base interface for all repo messages.
/// </summary>
public interface IRepoMessage
{
  /// <summary>
  /// The type discriminator for this message.
  /// </summary>
  string MessageType { get; }

  /// <summary>
  /// The document this message relates to (if applicable).
  /// </summary>
  DocumentId? DocumentId { get; }

  /// <summary>
  /// The peer who sent this message (set by NetworkSubsystem).
  /// </summary>
  PeerId? SenderId { get; init; }

  /// <summary>
  /// The specific peer this message is intended for (set by Repo from synchronizer).
  /// When set, the network layer should route the message only to this peer
  /// instead of broadcasting to all peers subscribed to the document.
  /// </summary>
  PeerId? TargetPeerId { get; init; }
}
