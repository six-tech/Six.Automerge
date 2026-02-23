namespace Six.Automerge.Repository.Network.SignalR;

/// <summary>
/// Data transfer object representing a peer currently connected to a document.
/// </summary>
/// <remarks>
/// Used by the server to broadcast peer presence information to clients.
/// Clients receive lists of PeerInfoDto via <see cref="IDocumentsHubClient.OnDocumentPeersChanged"/>
/// to display who is currently editing a document.
/// </remarks>
[PublicAPI]
[MessagePackObject]
public readonly record struct PeerInfoDto
{
  /// <summary>
  /// The unique identifier for this peer.
  /// </summary>
  [Key(0)]
  public required string PeerId { get; init; }

  /// <summary>
  /// Optional metadata about this peer.
  /// </summary>
  [Key(1)]
  public PeerMetadataDto? Metadata { get; init; }
}
