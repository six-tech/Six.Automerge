// Copyright (c) Six Technology, Inc. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Concurrent;
using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;

namespace Six.Automerge.SyncServer.Tests.Unit;

/// <summary>
/// Mock transport for testing SyncServer. Records all sent/broadcast messages
/// and allows simulating peer connections and message delivery.
/// </summary>
internal sealed class MockServerTransport : IServerTransport
{
  readonly ConcurrentBag<(PeerId PeerId, IRepoMessage Message)> _sentMessages = new();
  readonly ConcurrentBag<(DocumentId DocumentId, IRepoMessage Message)> _broadcastMessages = new();

  public IReadOnlyCollection<(PeerId PeerId, IRepoMessage Message)> SentMessages => _sentMessages;
  public IReadOnlyCollection<(DocumentId DocumentId, IRepoMessage Message)> BroadcastMessages => _broadcastMessages;

  public event EventHandler<ServerPeerConnectedEventArgs>? PeerConnected;
  public event EventHandler<ServerPeerDisconnectedEventArgs>? PeerDisconnected;
  public event EventHandler<ServerMessageReceivedEventArgs>? MessageReceived;

  public Task SendAsync(PeerId peerId, IRepoMessage message)
  {
    _sentMessages.Add((peerId, message));
    return Task.CompletedTask;
  }

  public Task BroadcastAsync(DocumentId documentId, IRepoMessage message, PeerId? excludePeer = null)
  {
    _broadcastMessages.Add((documentId, message));
    return Task.CompletedTask;
  }

  // Suppress unused event warnings — these are part of the interface contract
  // and may be used by production code subscribing to this transport.
  internal void SimulatePeerConnected(PeerId peerId, PeerMetadata? metadata = null)
  {
    PeerConnected?.Invoke(this, new ServerPeerConnectedEventArgs { PeerId = peerId, Metadata = metadata });
  }

  internal void SimulatePeerDisconnected(PeerId peerId)
  {
    PeerDisconnected?.Invoke(this, new ServerPeerDisconnectedEventArgs { PeerId = peerId });
  }

  internal void SimulateMessageReceived(PeerId peerId, IRepoMessage message)
  {
    MessageReceived?.Invoke(this, new ServerMessageReceivedEventArgs { PeerId = peerId, Message = message });
  }
}
