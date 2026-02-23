using Six.Automerge.Repository;

namespace Six.Automerge.Repository.Network;

/// <summary>
/// Request for a document (initial sync).
/// </summary>
[PublicAPI]
public sealed record RequestMessage : IRepoMessage
{
  public string MessageType => "request";
  public required DocumentId DocumentId { get; init; }
  public required byte[] Data { get; init; }
  public PeerId? SenderId { get; init; }
  public PeerId? TargetPeerId { get; init; }

  DocumentId? IRepoMessage.DocumentId => DocumentId;
}
