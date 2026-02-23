// Copyright (c) Six Technology, Inc. All rights reserved.
// Licensed under the MIT license.

using Six.Automerge.Repository.Network;

namespace Six.Automerge.SyncServer;

/// <summary>
/// Event args raised when a peer disconnects from the server transport.
/// </summary>
[PublicAPI]
public sealed class ServerPeerDisconnectedEventArgs : EventArgs
{
  /// <summary>
  /// The peer that disconnected.
  /// </summary>
  public required PeerId PeerId { get; init; }
}
