namespace Six.Automerge.Repository.Synchronizer;

/// <summary>
/// Tracks what we know about a peer's state for a specific document.
/// </summary>
[PublicAPI]
public enum PeerDocumentStatus
{
    /// <summary>
    /// We don't know if the peer has this document.
    /// </summary>
    Unknown,

    /// <summary>
    /// The peer has this document and can sync it.
    /// </summary>
    Has,

    /// <summary>
    /// The peer wants this document (has sent a request).
    /// </summary>
    Wants,

    /// <summary>
    /// The peer has indicated this document is unavailable.
    /// </summary>
    Unavailable
}
