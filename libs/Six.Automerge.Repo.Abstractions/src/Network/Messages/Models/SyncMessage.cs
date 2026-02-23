using Six.Automerge.Repository;

namespace Six.Automerge.Repository.Network;

/// <summary>
/// Automerge sync protocol message.
/// </summary>
[PublicAPI]
public sealed record SyncMessage : IRepoMessage
{
  public string MessageType => "sync";
  public required DocumentId DocumentId { get; init; }
  public required byte[] Data { get; init; }
  public PeerId? SenderId { get; init; }
  public PeerId? TargetPeerId { get; init; }

  DocumentId? IRepoMessage.DocumentId => DocumentId;
}
