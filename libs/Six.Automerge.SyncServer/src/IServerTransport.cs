// Copyright (c) Six Technology, Inc. All rights reserved.
// Licensed under the MIT license.

using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;

namespace Six.Automerge.SyncServer;

/// <summary>
/// Transport layer interface for the sync server. Implementations bridge the server
/// to external clients via protocols like SignalR, WebSocket, gRPC, etc.
/// </summary>
/// <remarks>
/// <para>The transport is responsible for:</para>
/// <list type="bullet">
/// <item><description>Delivering messages to specific peers or broadcasting to document subscribers</description></item>
/// <item><description>Notifying the server of peer connect/disconnect lifecycle events</description></item>
/// <item><description>Forwarding incoming messages from peers to the server</description></item>
/// </list>
/// <para>Thread Safety: Implementations must be thread-safe. Events may be raised from any thread.</para>
/// </remarks>
[PublicAPI]
public interface IServerTransport
{
  /// <summary>
  /// Sends a message to a specific peer.
  /// </summary>
  /// <param name="peerId">The target peer.</param>
  /// <param name="message">The message to send.</param>
  /// <returns>A task representing the asynchronous send operation.</returns>
  Task SendAsync(PeerId peerId, IRepoMessage message);

  /// <summary>
  /// Broadcasts a message to all peers subscribed to a document.
  /// </summary>
  /// <param name="documentId">The document whose subscribers should receive the message.</param>
  /// <param name="message">The message to broadcast.</param>
  /// <param name="excludePeer">Optional peer to exclude from the broadcast (e.g. the sender).</param>
  /// <returns>A task representing the asynchronous broadcast operation.</returns>
  Task BroadcastAsync(DocumentId documentId, IRepoMessage message, PeerId? excludePeer = null);

  /// <summary>
  /// Raised when a new peer connects to the transport.
  /// </summary>
  event EventHandler<ServerPeerConnectedEventArgs>? PeerConnected;

  /// <summary>
  /// Raised when a peer disconnects from the transport.
  /// </summary>
  event EventHandler<ServerPeerDisconnectedEventArgs>? PeerDisconnected;

  /// <summary>
  /// Raised when a message is received from a peer.
  /// </summary>
  event EventHandler<ServerMessageReceivedEventArgs>? MessageReceived;
}
