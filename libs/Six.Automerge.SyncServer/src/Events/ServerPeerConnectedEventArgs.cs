// Copyright (c) Six Technology, Inc. All rights reserved.
// Licensed under the MIT license.

using Six.Automerge.Repository.Network;

namespace Six.Automerge.SyncServer;

/// <summary>
/// Event args raised when a peer connects to the server transport.
/// </summary>
[PublicAPI]
public sealed class ServerPeerConnectedEventArgs : EventArgs
{
  /// <summary>
  /// The peer that connected.
  /// </summary>
  public required PeerId PeerId { get; init; }

  /// <summary>
  /// Optional metadata about the connecting peer.
  /// </summary>
  public PeerMetadata? Metadata { get; init; }
}
