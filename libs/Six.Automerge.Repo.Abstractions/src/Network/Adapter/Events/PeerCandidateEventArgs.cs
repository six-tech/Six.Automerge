namespace Six.Automerge.Repository.Network;

/// <summary>
/// Event args for peer candidate (new peer discovered).
/// </summary>
[PublicAPI]
public sealed class PeerCandidateEventArgs : EventArgs
{
  public required PeerId PeerId { get; init; }
  public required PeerMetadata? Metadata { get; init; }
}