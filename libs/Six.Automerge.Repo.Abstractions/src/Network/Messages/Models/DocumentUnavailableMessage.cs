using Six.Automerge.Repository;

namespace Six.Automerge.Repository.Network;

/// <summary>
/// Indicates a document is not available from this peer.
/// </summary>
[PublicAPI]
public sealed record DocumentUnavailableMessage : IRepoMessage
{
  public string MessageType => "doc-unavailable";
  public required DocumentId DocumentId { get; init; }
  public PeerId? SenderId { get; init; }
  public PeerId? TargetPeerId { get; init; }

  DocumentId? IRepoMessage.DocumentId => DocumentId;
}
