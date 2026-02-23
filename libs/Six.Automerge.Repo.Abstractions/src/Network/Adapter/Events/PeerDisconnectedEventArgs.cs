namespace Six.Automerge.Repository.Network;

/// <summary>
/// Event args for peer disconnection.
/// </summary>
[PublicAPI]
public sealed class PeerDisconnectedEventArgs : EventArgs
{
  public required PeerId PeerId { get; init; }
}