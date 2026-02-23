using Six.Automerge.Repository;

namespace Six.Automerge.Repository.Network;

/// <summary>
/// Contains a serialized Automerge document snapshot for initial sync.
/// </summary>
[PublicAPI]
public sealed record DocumentSnapshotMessage : IRepoMessage
{
  public string MessageType => "doc-snapshot";
  public required DocumentId DocumentId { get; init; }
  public required byte[] Data { get; init; }
  public PeerId? SenderId { get; init; }
  public PeerId? TargetPeerId { get; init; }

  DocumentId? IRepoMessage.DocumentId => DocumentId;
}
