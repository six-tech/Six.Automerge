namespace Six.Automerge.Repository.Network.SignalR;

/// <summary>
/// Data transfer object for peer handshake sent during connection establishment.
/// </summary>
/// <remarks>
/// Sent by the client when connecting to the SignalR hub to establish peer identity and metadata.
/// The server uses this information to track peer connections and routing.
/// </remarks>
[PublicAPI]
[MessagePackObject]
public readonly record struct PeerHandshakeDto
{
  /// <summary>
  /// The unique identifier for this peer.
  /// </summary>
  /// <remarks>
  /// Must be unique across all connected peers. Typically a GUID.
  /// Used for message routing and preventing echo-back of own messages.
  /// </remarks>
  [Key(0)]
  public required string PeerId { get; init; }

  /// <summary>
  /// Optional metadata about this peer.
  /// </summary>
  /// <remarks>
  /// Contains information about sync state persistence and custom application data.
  /// Null if no metadata is provided.
  /// </remarks>
  [Key(1)]
  public PeerMetadataDto? Metadata { get; init; }

  /// <summary>
  /// Creates a PeerHandshakeDto from a peer ID and optional metadata.
  /// </summary>
  /// <param name="peerId">The peer ID.</param>
  /// <param name="metadata">Optional peer metadata.</param>
  /// <returns>A new PeerHandshakeDto instance.</returns>
  /// <exception cref="ArgumentException">Thrown if peerId is null or whitespace.</exception>
  public static PeerHandshakeDto Create(PeerId peerId, PeerMetadata? metadata = null)
  {
    if (string.IsNullOrWhiteSpace(peerId))
      throw new ArgumentException("PeerId cannot be null or whitespace", nameof(peerId));

    return new PeerHandshakeDto
    {
      PeerId = peerId,
      Metadata = metadata is not null ? PeerMetadataDto.FromPeerMetadata(metadata) : null
    };
  }
}
