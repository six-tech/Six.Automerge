// Copyright (c) Six Technology, Inc. All rights reserved.
// Licensed under the MIT license.

using Six.Automerge.Repository.Network;

namespace Six.Automerge.SyncServer;

/// <summary>
/// Event args raised when a message is received from a peer via the server transport.
/// </summary>
[PublicAPI]
public sealed class ServerMessageReceivedEventArgs : EventArgs
{
  /// <summary>
  /// The peer that sent the message.
  /// </summary>
  public required PeerId PeerId { get; init; }

  /// <summary>
  /// The received message.
  /// </summary>
  public required IRepoMessage Message { get; init; }
}
