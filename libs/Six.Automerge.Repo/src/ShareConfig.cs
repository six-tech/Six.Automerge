using Six.Automerge.Repository.Network;

namespace Six.Automerge.Repository;

/// <summary>
/// Configuration for document sharing policies.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed record ShareConfig
{
  /// <summary>
  /// Determines if a document should be proactively announced to a peer.
  /// If not specified, all documents are announced.
  /// </summary>
  public Func<PeerId, DocumentId?, Task<bool>>? Announce { get; init; }

  /// <summary>
  /// Determines if a peer can access a specific document.
  /// If not specified, all peers can access all documents.
  /// </summary>
  public Func<PeerId, DocumentId, Task<bool>>? Access { get; init; }

  /// <summary>
  /// Default share config that allows all sharing.
  /// </summary>
  public static ShareConfig AllowAll { get; } = new()
  {
    Announce = (_, _) => Task.FromResult(true), Access = (_, _) => Task.FromResult(true)
  };
}
