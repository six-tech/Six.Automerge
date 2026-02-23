// Copyright (c) Six. All rights reserved.

using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;
using Six.Automerge.Repository.Network.SignalR;
using Six.Automerge.SyncServer;

namespace Server.Api.Hubs;

/// <summary>
/// <see cref="IServerTransport"/> implementation backed by SignalR.
/// Routes outgoing messages from <see cref="SyncServer"/> to connected clients
/// via <see cref="IHubContext{THub,T}"/>.
/// </summary>
[PublicAPI]
public sealed class SignalRServerTransport : IServerTransport, IDisposable
{
  readonly IHubContext<DocumentsHub, IDocumentsHubClient> _hubContext;
  readonly ILogger<SignalRServerTransport> _logger;
  readonly ConcurrentDictionary<string, PeerId> _connectionToPeer = new();
  readonly ConcurrentDictionary<string, string> _peerToConnection = new(); // (string)PeerId → ConnectionId

  bool _disposed;

  public SignalRServerTransport(
    IHubContext<DocumentsHub, IDocumentsHubClient> hubContext,
    ILoggerFactory loggerFactory)
  {
    ArgumentNullException.ThrowIfNull(hubContext);
    ArgumentNullException.ThrowIfNull(loggerFactory);

    _hubContext = hubContext;
    _logger = loggerFactory.CreateLogger<SignalRServerTransport>();
  }

  // Events required by IServerTransport but not raised in the hub-driven flow.
  // The hub calls SyncServer methods directly rather than going through transport events.
#pragma warning disable CS0067
  public event EventHandler<ServerPeerConnectedEventArgs>? PeerConnected;
  public event EventHandler<ServerPeerDisconnectedEventArgs>? PeerDisconnected;
  public event EventHandler<ServerMessageReceivedEventArgs>? MessageReceived;
#pragma warning restore CS0067

  /// <inheritdoc />
  public async Task SendAsync(PeerId peerId, IRepoMessage message)
  {
    if(_disposed) return;

    if(!_peerToConnection.TryGetValue((string)peerId, out var connectionId))
    {
      _logger.LogDebug("SendAsync: No connection found for peer {PeerId}, dropping message", peerId);
      return;
    }

    var dto = ToDto(message);
    await _hubContext.Clients.Client(connectionId).OnReceiveMessage(dto).ConfigureAwait(false);

    _logger.LogDebug(
      "Sent {MessageType} to peer {PeerId} (connection {ConnectionId})",
      message.MessageType, peerId, connectionId);
  }

  /// <inheritdoc />
  public async Task BroadcastAsync(DocumentId documentId, IRepoMessage message, PeerId? excludePeer = null)
  {
    if(_disposed) return;

    var dto = ToDto(message);

    if(excludePeer is { } excluded && _peerToConnection.TryGetValue((string)excluded, out var excludedConnectionId))
    {
      await _hubContext.Clients.GroupExcept(documentId.Value, [excludedConnectionId]).OnReceiveMessage(dto).ConfigureAwait(false);

      _logger.LogDebug(
        "Broadcast {MessageType} to group {DocumentId} (excluding peer {ExcludedPeerId})",
        message.MessageType, documentId.Value, excluded);
    }
    else
    {
      await _hubContext.Clients.Group(documentId.Value).OnReceiveMessage(dto).ConfigureAwait(false);

      _logger.LogDebug(
        "Broadcast {MessageType} to group {DocumentId}",
        message.MessageType, documentId.Value);
    }
  }

  /// <summary>
  /// Registers a peer-to-connection mapping so <see cref="SendAsync"/> can
  /// target specific peers. Called by the hub on handshake.
  /// </summary>
  public void RegisterPeer(string connectionId, PeerId peerId)
  {
    _connectionToPeer[connectionId] = peerId;
    _peerToConnection[(string)peerId] = connectionId;

    _logger.LogInformation(
      "Peer registered: PeerId={PeerId}, ConnectionId={ConnectionId}",
      peerId, connectionId);
  }

  /// <summary>
  /// Removes a peer-to-connection mapping. Called by the hub on disconnect.
  /// </summary>
  public void UnregisterPeer(string connectionId)
  {
    if(_connectionToPeer.TryRemove(connectionId, out var peerId))
    {
      _peerToConnection.TryRemove((string)peerId, out _);

      _logger.LogInformation(
        "Peer unregistered: PeerId={PeerId}, ConnectionId={ConnectionId}",
        peerId, connectionId);
    }
  }

  /// <summary>
  /// Looks up the peer ID for a given SignalR connection.
  /// </summary>
  public bool TryGetPeerId(string connectionId, out PeerId peerId)
  {
    if(_connectionToPeer.TryGetValue(connectionId, out var id))
    {
      peerId = id;
      return true;
    }

    peerId = default;
    return false;
  }

  /// <summary>
  /// Gets the number of currently connected peers.
  /// </summary>
  public int GetConnectedPeerCount() => _connectionToPeer.Count;

  /// <inheritdoc />
  public void Dispose()
  {
    if(_disposed) return;
    _disposed = true;

    _connectionToPeer.Clear();
    _peerToConnection.Clear();
  }

  static MessageDto ToDto(IRepoMessage message)
  {
    var data = message switch
    {
      SyncMessage sync => sync.Data ?? [],
      RequestMessage request => request.Data ?? [],
      EphemeralMessage ephemeral => ephemeral.Data ?? [],
      DocumentSnapshotMessage snapshot => snapshot.Data,
      DocumentUnavailableMessage => Array.Empty<byte>(),
      _ => Array.Empty<byte>()
    };

    return MessageDto.FromRepoMessage(message, data);
  }
}
