// Copyright (c) Six. All rights reserved.

namespace Six.Automerge.Repo.Network.SignalR;

/// <summary>
/// Event arguments for when the list of peers on a document changes.
/// </summary>
/// <remarks>
/// Raised by <see cref="SignalRNetworkAdapter"/> when the server notifies the client
/// that the peer list for a document has been updated (peer joined or left).
/// The <see cref="Peers"/> list excludes the receiving client's own entry.
/// </remarks>
[PublicAPI]
public sealed class DocumentPeersChangedEventArgs : EventArgs
{
  /// <summary>
  /// Gets the document whose peer list changed.
  /// </summary>
  public required string DocumentId { get; init; }

  /// <summary>
  /// Gets the current list of peers on the document, excluding the receiving client.
  /// </summary>
  public required List<PeerInfoDto> Peers { get; init; }
}
