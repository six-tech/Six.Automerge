using Six.Automerge.Repository.Network;

namespace Six.Automerge.Repository;

/// <summary>
/// Event args for document lifecycle events.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class DocumentEventArgs : EventArgs
{
  public required DocumentId DocumentId { get; init; }
  public required IDocHandle Handle { get; init; }
}

/// <summary>
/// Event args for document deletion.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class DocumentDeletedEventArgs : EventArgs
{
  public required DocumentId DocumentId { get; init; }
}

/// <summary>
/// Event args for document unavailability.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class DocumentUnavailableEventArgs : EventArgs
{
  public required DocumentId DocumentId { get; init; }
}

/// <summary>
/// Event args for peer connection events.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PeerEventArgs : EventArgs
{
  public required PeerId PeerId { get; init; }
  public required PeerMetadata? Metadata { get; init; }
}
