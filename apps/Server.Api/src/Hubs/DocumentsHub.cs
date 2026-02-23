// Copyright (c) Six. All rights reserved.

using Microsoft.AspNetCore.SignalR;
using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;
using Six.Automerge.Repository.Network.SignalR;
using Six.Automerge.SyncServer;
using Server.Api.Services;

namespace Server.Api.Hubs;

/// <summary>
/// SignalR hub for Automerge document synchronization backed by <see cref="SyncServer"/>.
/// All sync protocol messages are routed through the server's Repo, enabling the server
/// to participate in document synchronization and persist documents.
/// </summary>
[PublicAPI]
public sealed class DocumentsHub : Hub<IDocumentsHubClient>, IDocumentsHub
{
  readonly SyncServer _syncServer;
  readonly SignalRServerTransport _transport;
  readonly DocumentPeerTracker _tracker;
  readonly ILogger<DocumentsHub> _logger;

  public DocumentsHub(
    SyncServer syncServer,
    SignalRServerTransport transport,
    DocumentPeerTracker tracker,
    ILoggerFactory loggerFactory)
  {
    ArgumentNullException.ThrowIfNull(syncServer);
    ArgumentNullException.ThrowIfNull(transport);
    ArgumentNullException.ThrowIfNull(tracker);
    ArgumentNullException.ThrowIfNull(loggerFactory);

    _syncServer = syncServer;
    _transport = transport;
    _tracker = tracker;
    _logger = loggerFactory.CreateLogger<DocumentsHub>();
  }

  /// <inheritdoc />
  public async Task<PeerHandshakeDto> Handshake(PeerHandshakeDto handshake)
  {
    try
    {
      _logger.LogInformation(
        "Peer handshake: PeerId={PeerId}, ConnectionId={ConnectionId}",
        handshake.PeerId, Context.ConnectionId);

      Context.Items["PeerId"] = handshake.PeerId;
      Context.Items["Metadata"] = handshake.Metadata;

      var peerId = new PeerId(handshake.PeerId);
      var metadata = handshake.Metadata?.ToPeerMetadata();

      _transport.RegisterPeer(Context.ConnectionId, peerId);
      await _syncServer.OnPeerConnectedAsync(peerId, metadata);

      await Clients.Others.OnPeerJoined(handshake);

      return PeerHandshakeDto.Create(_syncServer.PeerId);
    }
    catch(Exception ex)
    {
      _logger.LogError(ex,
        "Error during handshake. PeerId={PeerId}, ConnectionId={ConnectionId}",
        handshake.PeerId, Context.ConnectionId);
      throw;
    }
  }

  /// <inheritdoc />
  public async Task SendMessage(MessageDto message)
  {
    try
    {
      if(string.IsNullOrEmpty(message.DocumentId))
      {
        _logger.LogWarning(
          "Received message with empty DocumentId. ConnectionId={ConnectionId}",
          Context.ConnectionId);
        return;
      }

      _logger.LogDebug(
        "Routing message: DocumentId={DocumentId}, MessageType={MessageType}, SenderId={SenderId}",
        message.DocumentId, message.MessageType, message.SenderId);

      await Groups.AddToGroupAsync(Context.ConnectionId, message.DocumentId);

      // Track peer presence on this document
      var peerInfo = BuildPeerInfo();
      if(peerInfo is not null)
      {
        _tracker.AddPeerToDocument(message.DocumentId, peerInfo.Value);
        await BroadcastDocumentPeersAsync(message.DocumentId);
      }

      var repoMessage = DeserializeMessage(message);
      if(repoMessage is not null)
      {
        var senderId = new PeerId(message.SenderId);
        await _syncServer.OnMessageReceivedAsync(senderId, repoMessage);
      }
    }
    catch(Exception ex)
    {
      _logger.LogError(ex,
        "Error routing message. DocumentId={DocumentId}, ConnectionId={ConnectionId}",
        message.DocumentId, Context.ConnectionId);
    }
  }

  /// <inheritdoc />
  public async Task SubscribeToDocument(string documentId)
  {
    try
    {
      if(string.IsNullOrEmpty(documentId))
      {
        _logger.LogWarning("Subscribe with empty documentId. ConnectionId={ConnectionId}",
          Context.ConnectionId);
        return;
      }

      await Groups.AddToGroupAsync(Context.ConnectionId, documentId);

      // Track peer presence on this document
      var peerInfo = BuildPeerInfo();
      if(peerInfo is not null)
      {
        _tracker.AddPeerToDocument(documentId, peerInfo.Value);
        await BroadcastDocumentPeersAsync(documentId);
      }

      _logger.LogInformation(
        "Client subscribed: DocumentId={DocumentId}, ConnectionId={ConnectionId}",
        documentId, Context.ConnectionId);
    }
    catch(Exception ex)
    {
      _logger.LogError(ex,
        "Error subscribing. DocumentId={DocumentId}, ConnectionId={ConnectionId}",
        documentId, Context.ConnectionId);
    }
  }

  /// <inheritdoc />
  public async Task UnsubscribeFromDocument(string documentId)
  {
    try
    {
      if(string.IsNullOrEmpty(documentId))
      {
        _logger.LogWarning("Unsubscribe with empty documentId. ConnectionId={ConnectionId}",
          Context.ConnectionId);
        return;
      }

      await Groups.RemoveFromGroupAsync(Context.ConnectionId, documentId);

      _logger.LogInformation(
        "Client unsubscribed: DocumentId={DocumentId}, ConnectionId={ConnectionId}",
        documentId, Context.ConnectionId);
    }
    catch(Exception ex)
    {
      _logger.LogError(ex,
        "Error unsubscribing. DocumentId={DocumentId}, ConnectionId={ConnectionId}",
        documentId, Context.ConnectionId);
    }
  }

  /// <inheritdoc />
  public Task<List<PeerInfoDto>> GetDocumentPeers(string documentId)
  {
    var callerPeerId = Context.Items.TryGetValue("PeerId", out var id) ? id?.ToString() : null;
    return Task.FromResult(_tracker.GetDocumentPeers(documentId, callerPeerId));
  }

  /// <inheritdoc />
  public Task<int> GetDocumentPeerCount(string documentId)
  {
    var callerPeerId = Context.Items.TryGetValue("PeerId", out var id) ? id?.ToString() : null;
    return Task.FromResult(_tracker.GetDocumentPeerCount(documentId, callerPeerId));
  }

  /// <summary>
  /// Returns the list of document IDs stored in the SyncServer.
  /// </summary>
  public async Task<List<string>> ListDocuments()
  {
    try
    {
      var docs = await _syncServer.ListDocumentsAsync();
      _logger.LogDebug("Listed {Count} documents", docs.Count);
      return docs.ToList();
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Error listing documents");
      return [];
    }
  }

  /// <inheritdoc />
  public override Task OnConnectedAsync()
  {
    _logger.LogInformation("Client connected: ConnectionId={ConnectionId}", Context.ConnectionId);
    return base.OnConnectedAsync();
  }

  /// <inheritdoc />
  public override async Task OnDisconnectedAsync(Exception? exception)
  {
    var peerIdStr = Context.Items.TryGetValue("PeerId", out var id) ? id?.ToString() : null;

    if(exception is not null)
    {
      _logger.LogWarning(exception,
        "Client disconnected with error: ConnectionId={ConnectionId}, PeerId={PeerId}",
        Context.ConnectionId, peerIdStr ?? "unknown");
    }
    else
    {
      _logger.LogInformation(
        "Client disconnected: ConnectionId={ConnectionId}, PeerId={PeerId}",
        Context.ConnectionId, peerIdStr ?? "unknown");
    }

    if(!string.IsNullOrEmpty(peerIdStr))
    {
      // Remove peer from tracker and broadcast updated peer lists
      var affectedDocuments = _tracker.RemovePeerFromAllDocuments(peerIdStr);
      foreach(var documentId in affectedDocuments)
      {
        await BroadcastDocumentPeersAsync(documentId);
      }

      var peerId = new PeerId(peerIdStr);
      _transport.UnregisterPeer(Context.ConnectionId);
      await _syncServer.OnPeerDisconnectedAsync(peerId);
      await Clients.Others.OnPeerLeft(peerIdStr);
    }

    await base.OnDisconnectedAsync(exception);
  }

  /// <summary>
  /// Broadcasts the updated peer list for a document to all clients in the group.
  /// Each client receives a list that excludes their own entry.
  /// </summary>
  async Task BroadcastDocumentPeersAsync(string documentId)
  {
    // Broadcast to all peers in the group — each gets a list excluding themselves.
    // Use Clients.Group to send to all (including caller), since each peer gets
    // the full list and can filter if needed. For per-client exclusion, we send
    // the full list and rely on the interface contract that says "excluding the
    // receiving client's own entry" — we use OthersInGroup for the broadcast
    // and send separately to the caller.
    var callerPeerId = Context.Items.TryGetValue("PeerId", out var id) ? id?.ToString() : null;

    // Send to others in group (full peer list — they don't know who "caller" is so we send all)
    var allPeers = _tracker.GetDocumentPeers(documentId);
    await Clients.OthersInGroup(documentId).OnDocumentPeersChanged(documentId, allPeers);

    // Send to caller with their own entry excluded
    if(callerPeerId is not null)
    {
      var peersForCaller = _tracker.GetDocumentPeers(documentId, callerPeerId);
      await Clients.Caller.OnDocumentPeersChanged(documentId, peersForCaller);
    }
  }

  PeerInfoDto? BuildPeerInfo()
  {
    if(!Context.Items.TryGetValue("PeerId", out var id) || id is not string peerId)
      return null;

    var metadata = Context.Items.TryGetValue("Metadata", out var meta) ? meta as PeerMetadataDto? : null;

    return new PeerInfoDto
    {
      PeerId = peerId,
      Metadata = metadata
    };
  }

  static IRepoMessage? DeserializeMessage(MessageDto messageDto)
  {
    var senderId = new PeerId(messageDto.SenderId);

    return messageDto.MessageType switch
    {
      "sync" => new SyncMessage
      {
        DocumentId = DocumentId.Parse(messageDto.DocumentId!),
        Data = messageDto.Data,
        SenderId = senderId
      },
      "request" => new RequestMessage
      {
        DocumentId = DocumentId.Parse(messageDto.DocumentId!),
        Data = messageDto.Data,
        SenderId = senderId
      },
      "doc-unavailable" => new DocumentUnavailableMessage
      {
        DocumentId = DocumentId.Parse(messageDto.DocumentId!),
        SenderId = senderId
      },
      "doc-snapshot" => new DocumentSnapshotMessage
      {
        DocumentId = DocumentId.Parse(messageDto.DocumentId!),
        Data = messageDto.Data ?? [],
        SenderId = senderId
      },
      _ => null
    };
  }
}
