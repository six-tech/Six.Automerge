namespace Six.Automerge.Repository.Network;

/// <summary>
/// Optional callback-based alternative to events for network adapters.
/// Useful for functional-style composition.
/// </summary>
[PublicAPI]
public sealed class NetworkAdapterCallbacks
{
  /// <summary>
  /// Called when a new peer candidate is discovered.
  /// </summary>
  public Action<PeerId, PeerMetadata?>? OnPeerCandidate { get; init; }

  /// <summary>
  /// Called when a peer disconnects.
  /// </summary>
  public Action<PeerId>? OnPeerDisconnected { get; init; }

  /// <summary>
  /// Called when a message is received.
  /// </summary>
  public Action<IRepoMessage>? OnMessageReceived { get; init; }

  /// <summary>
  /// Called when the adapter closes.
  /// </summary>
  public Action? OnClosed { get; init; }
}