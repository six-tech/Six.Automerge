using Six.Automerge.Repository;

namespace Six.Automerge.Repository.Network;

/// <summary>
/// Controls remote heads subscriptions (optional feature).
/// </summary>
[PublicAPI]
public sealed record RemoteSubscriptionChangeMessage : IRepoMessage
{
  public string MessageType => "remote-subscription-change";

  /// <summary>
  /// Document IDs to add to subscription.
  /// </summary>
  public IReadOnlyList<DocumentId>? Add { get; init; }

  /// <summary>
  /// Document IDs to remove from subscription.
  /// </summary>
  public IReadOnlyList<DocumentId>? Remove { get; init; }

  public PeerId? SenderId { get; init; }
  public PeerId? TargetPeerId { get; init; }

  DocumentId? IRepoMessage.DocumentId => null;
}
