// Copyright (c) Six Technology, Inc. All rights reserved.
// Licensed under the MIT license.

using Six.Automerge.Repository.Network;

namespace Six.Automerge.SyncServer;

/// <summary>
/// An <see cref="INetworkAdapter"/> that bridges between the internal Repo and an
/// <see cref="IServerTransport"/>. When the Repo sends a message, this adapter
/// forwards it to the transport. When the transport raises events, this adapter
/// relays them to the Repo via its own events.
/// </summary>
internal sealed class TransportBridgeAdapter : INetworkAdapter
{
  readonly IServerTransport _transport;
  readonly PeerId _localPeerId;

  public string AdapterId { get; }
  public bool IsReady => true;

  public event EventHandler<PeerCandidateEventArgs>? PeerCandidate;
  public event EventHandler<PeerDisconnectedEventArgs>? PeerDisconnected;
  public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
  public event EventHandler? Closed;

  public TransportBridgeAdapter(IServerTransport transport, PeerId localPeerId)
  {
    _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    _localPeerId = localPeerId;
    AdapterId = $"transport-bridge-{_localPeerId}";
  }

  public Task WhenReady() => Task.CompletedTask;

  public void Connect(PeerId peerId, PeerMetadata? metadata = null)
  {
    // No-op — peer connections are driven by the transport events
  }

  /// <summary>
  /// Called by the Repo when it wants to send a message. Routes to the transport.
  /// </summary>
  public void Send(IRepoMessage message)
  {
    if(message is null) throw new ArgumentNullException(nameof(message));

    // Fire-and-forget: the transport's SendAsync/BroadcastAsync is async,
    // but INetworkAdapter.Send is synchronous by design.
    _ = SendViaTransportAsync(message);
  }

  async Task SendViaTransportAsync(IRepoMessage message)
  {
    try
    {
      if(message.TargetPeerId is { } targetPeerId)
      {
        await _transport.SendAsync(targetPeerId, message).ConfigureAwait(false);
      }
      else if(message.DocumentId is { } documentId)
      {
        await _transport.BroadcastAsync(documentId, message, message.SenderId).ConfigureAwait(false);
      }
    }
    catch(Exception)
    {
      // Transport errors should not crash the adapter
    }
  }

  public Task Disconnect()
  {
    Closed?.Invoke(this, EventArgs.Empty);
    return Task.CompletedTask;
  }

  /// <summary>
  /// Injects a peer-connected event into the Repo's network subsystem.
  /// </summary>
  internal void RaisePeerConnected(PeerId peerId, PeerMetadata? metadata)
  {
    PeerCandidate?.Invoke(this, new PeerCandidateEventArgs { PeerId = peerId, Metadata = metadata });
  }

  /// <summary>
  /// Injects a peer-disconnected event into the Repo's network subsystem.
  /// </summary>
  internal void RaisePeerDisconnected(PeerId peerId)
  {
    PeerDisconnected?.Invoke(this, new PeerDisconnectedEventArgs { PeerId = peerId });
  }

  /// <summary>
  /// Injects a message-received event into the Repo's network subsystem.
  /// </summary>
  internal void RaiseMessageReceived(IRepoMessage message)
  {
    MessageReceived?.Invoke(this, new MessageReceivedEventArgs { Message = message });
  }
}
