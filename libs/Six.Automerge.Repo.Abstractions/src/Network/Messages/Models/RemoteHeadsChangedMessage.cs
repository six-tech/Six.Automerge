using Six.Automerge.Repository;

namespace Six.Automerge.Repository.Network;

/// <summary>
/// Notifies peers that remote heads have changed (optional feature).
/// </summary>
[PublicAPI]
public sealed record RemoteHeadsChangedMessage : IRepoMessage
{
  public string MessageType => "remote-heads-changed";
  public required DocumentId DocumentId { get; init; }
  public required IReadOnlyList<string> NewHeads { get; init; }
  public PeerId? SenderId { get; init; }
  public PeerId? TargetPeerId { get; init; }

  DocumentId? IRepoMessage.DocumentId => DocumentId;
}
