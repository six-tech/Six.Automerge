namespace Six.Automerge.Repository.Network.SignalR;

/// <summary>
/// Data transfer object for peer metadata sent during handshake.
/// </summary>
/// <remarks>
/// Represents metadata about a peer connection for transport over SignalR.
/// Supports both JSON and MessagePack serialization.
/// </remarks>
[PublicAPI]
[MessagePackObject]
public readonly record struct PeerMetadataDto
{
  /// <summary>
  /// Storage identifier for this peer. If provided, sync state will be persisted.
  /// </summary>
  [Key(0)]
  public string? StorageId { get; init; }

  /// <summary>
  /// If true, sync state will not be persisted for this peer.
  /// </summary>
  [Key(1)]
  public bool IsEphemeral { get; init; }

  /// <summary>
  /// Custom metadata for application use (serialized as JSON or MessagePack).
  /// </summary>
  /// <remarks>
  /// Contains application-specific data about the peer.
  /// Dictionary keys and values should be JSON/MessagePack serializable.
  /// </remarks>
  [Key(2)]
  public Dictionary<string, object>? CustomData { get; init; }

  /// <summary>
  /// Creates a PeerMetadataDto from a <see cref="PeerMetadata"/> instance.
  /// </summary>
  /// <param name="metadata">The peer metadata to convert. Null returns default instance.</param>
  /// <returns>A new PeerMetadataDto instance.</returns>
  public static PeerMetadataDto FromPeerMetadata(PeerMetadata? metadata)
  {
    if (metadata is null)
      return default;

    return new PeerMetadataDto
    {
      StorageId = metadata.StorageId?.ToString(),
      IsEphemeral = metadata.IsEphemeral,
      CustomData = metadata.CustomData is not null
        ? new Dictionary<string, object>(metadata.CustomData)
        : null
    };
  }

  /// <summary>
  /// Converts this DTO to a <see cref="PeerMetadata"/> instance.
  /// </summary>
  /// <returns>A new PeerMetadata instance.</returns>
  public PeerMetadata ToPeerMetadata()
  {
    StorageId? storageId = null;
    if (!string.IsNullOrEmpty(StorageId))
      storageId = new StorageId(StorageId);

    return new PeerMetadata
    {
      StorageId = storageId,
      IsEphemeral = IsEphemeral,
      CustomData = CustomData
    };
  }
}
