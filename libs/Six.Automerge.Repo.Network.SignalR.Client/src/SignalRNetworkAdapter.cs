using Six.Automerge.Repository;
using Six.Automerge.Repo.Network.SignalR.Client;

namespace Six.Automerge.Repo.Network.SignalR;

/// <summary>
/// SignalR-based network adapter for Automerge document synchronization.
/// </summary>
/// <remarks>
/// This adapter provides real-time collaborative editing over ASP.NET Core SignalR with:
/// <list type="bullet">
///   <item><description>Automatic reconnection and connection lifecycle management</description></item>
///   <item><description>MessagePack serialization with LZ4 compression for bandwidth efficiency</description></item>
///   <item><description>Document-based group subscriptions for targeted message delivery</description></item>
///   <item><description>Thread-safe operations with concurrent access support</description></item>
/// </list>
///
/// <para>
/// <strong>Usage Example:</strong>
/// <code>
/// var adapter = new SignalRNetworkAdapter(connectionService, loggerFactory);
/// adapter.MessageReceived += (sender, args) => {
///   Console.WriteLine($"Received: {args.Message.MessageType}");
/// };
/// adapter.Connect(PeerId.Generate(), null);
/// await adapter.WhenReady();
/// adapter.Send(syncMessage);
/// </code>
/// </para>
/// </remarks>
[PublicAPI]
public sealed class SignalRNetworkAdapter : INetworkAdapter, IDocumentsHubClient, IDisposable, IAsyncDisposable
{
  readonly ISignalRConnectionService _connectionService;
  readonly ILogger<SignalRNetworkAdapter> _logger;
  readonly string _adapterId;
  readonly SemaphoreSlim _lock = new(1, 1);
  readonly TaskCompletionSource _readyTcs = new();
  readonly ConcurrentDictionary<string, byte> _subscribedDocuments = new();

  HubConnection? _connection;
  PeerId? _peerId;
  bool _isReady;
  bool _disposed;

  /// <summary>
  /// Initializes a new instance of the <see cref="SignalRNetworkAdapter"/> class.
  /// </summary>
  /// <param name="connectionService">The SignalR connection service.</param>
  /// <param name="loggerFactory">The logger factory.</param>
  public SignalRNetworkAdapter(
    ISignalRConnectionService connectionService,
    ILoggerFactory loggerFactory)
  {
    _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
    ArgumentNullException.ThrowIfNull(loggerFactory);

    _logger = loggerFactory.CreateLogger<SignalRNetworkAdapter>();
    _adapterId = $"signalr-{Guid.NewGuid():N}";
  }

  /// <inheritdoc />
  public string AdapterId => _adapterId;

  /// <inheritdoc />
  public bool IsReady => _isReady;

  /// <inheritdoc />
  public Task WhenReady() => _readyTcs.Task;

  /// <inheritdoc />
  public event EventHandler<PeerCandidateEventArgs>? PeerCandidate;

  /// <inheritdoc />
  public event EventHandler<PeerDisconnectedEventArgs>? PeerDisconnected;

  /// <inheritdoc />
  public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

  /// <inheritdoc />
  public event EventHandler? Closed;

  /// <summary>
  /// Raised when the server notifies that the peer list for a document has changed.
  /// </summary>
  public event EventHandler<DocumentPeersChangedEventArgs>? DocumentPeersChanged;

  /// <summary>
  /// Raised when the server notifies that a new document is available.
  /// </summary>
  public event EventHandler<DocumentAvailableEventArgs>? DocumentAvailable;

  /// <inheritdoc />
  public void Connect(PeerId peerId, PeerMetadata? metadata = null)
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(SignalRNetworkAdapter));

    _ = ConnectAsyncInternal(peerId, metadata);
  }

  async Task ConnectAsyncInternal(PeerId peerId, PeerMetadata? metadata)
  {
    await _lock.WaitAsync();
    try
    {
      if (_isReady)
      {
        _logger.LogWarning("Adapter already connected. Ignoring duplicate Connect call.");
        return;
      }

      _logger.LogInformation("Connecting SignalR adapter for peer: {PeerId}", peerId);
      _peerId = peerId;

      // Get connection from service
      _connection = await _connectionService.GetConnection();
      _logger.LogDebug("SignalR connection established. ConnectionId={ConnectionId}", _connection.ConnectionId ?? "<pending>");

      // Register client methods for hub callbacks
      _logger.LogDebug("Registering hub callbacks for connection {ConnectionId}", _connection.ConnectionId ?? "<pending>");
      _connection.On<MessageDto>(
        "OnReceiveMessage",
        msg =>
        {
          _logger.LogTrace("Hub invoked OnReceiveMessage. DocumentId={DocumentId}, MessageType={MessageType}, SenderId={SenderId}",
            msg.DocumentId,
            msg.MessageType,
            msg.SenderId);
          _ = ((IDocumentsHubClient)this).OnReceiveMessage(msg);
        });
      _connection.On<PeerHandshakeDto>(
        "OnPeerJoined",
        handshake =>
        {
          _logger.LogTrace("Hub invoked OnPeerJoined for PeerId={PeerId}", handshake.PeerId);
          _ = ((IDocumentsHubClient)this).OnPeerJoined(handshake);
        });
      _connection.On<string>(
        "OnPeerLeft",
        peerId =>
        {
          _logger.LogTrace("Hub invoked OnPeerLeft for PeerId={PeerId}", peerId);
          _ = ((IDocumentsHubClient)this).OnPeerLeft(peerId);
        });
      _connection.On<string, List<PeerInfoDto>>(
        "OnDocumentPeersChanged",
        (documentId, peers) =>
        {
          _logger.LogTrace("Hub invoked OnDocumentPeersChanged for DocumentId={DocumentId}, PeerCount={PeerCount}", documentId, peers.Count);
          _ = ((IDocumentsHubClient)this).OnDocumentPeersChanged(documentId, peers);
        });
      _connection.On<string>(
        "OnDocumentAvailable",
        documentId =>
        {
          _logger.LogTrace("Hub invoked OnDocumentAvailable for DocumentId={DocumentId}", documentId);
          _ = ((IDocumentsHubClient)this).OnDocumentAvailable(documentId);
        });

      // Hook connection lifecycle events
      _connection.Closed += OnConnectionClosedAsync;

      // Send handshake and receive the server's identity
      var handshake = PeerHandshakeDto.Create(peerId, metadata);
      var serverHandshake = await _connection.InvokeAsync<PeerHandshakeDto>("Handshake", handshake);

      _logger.LogInformation(
        "Handshake completed for peer: {PeerId}. Server peer: {ServerPeerId}",
        peerId, serverHandshake.PeerId);

      // Mark ready
      _isReady = true;
      _readyTcs.TrySetResult();

      // Raise PeerCandidate with the SERVER's PeerId (our direct peer),
      // not our own PeerId
      var serverPeerId = new PeerId(serverHandshake.PeerId);
      var serverMetadata = serverHandshake.Metadata?.ToPeerMetadata();
      RaisePeerCandidate(serverPeerId, serverMetadata);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to connect SignalR adapter for peer: {PeerId}", peerId);
      _readyTcs.TrySetException(ex);
      throw;
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <inheritdoc />
  public void Send(IRepoMessage message)
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(SignalRNetworkAdapter));

    ArgumentNullException.ThrowIfNull(message);

    _ = SendAsyncInternal(message);
  }

  async Task SendAsyncInternal(IRepoMessage message)
  {
    await _lock.WaitAsync();
    try
    {
      if (!_isReady || _connection is null)
      {
        _logger.LogWarning("Cannot send message: adapter not ready or connection is null");
        return;
      }

      var documentId = message.DocumentId;

      if (documentId is null)
      {
        _logger.LogWarning("Cannot send message: DocumentId is null or empty");
        return;
      }

      if (_peerId is null)
      {
        _logger.LogWarning("Cannot send message type {MessageType}: adapter has no peer ID assigned", message.MessageType);
        return;
      }

      // Auto-subscribe to document if not already subscribed
      var documentIdValue = documentId.Value;
      var documentIdString = documentIdValue.Value;

      if (!_subscribedDocuments.ContainsKey(documentIdString))
      {
        _logger.LogDebug("Auto-subscribing to document: {DocumentId}", documentIdString);
        await SubscribeToDocumentInternalAsync(documentIdString);
      }

      var messageWithSender = EnsureSender(message, _peerId.Value);

      if (messageWithSender.SenderId is null && messageWithSender is DocumentSnapshotMessage snapshotWithoutSender)
      {
        messageWithSender = snapshotWithoutSender with { SenderId = _peerId.Value };
      }

      _logger.LogInformation(
        "Sending {MessageType} message for document {DocumentId} via SignalR connection {ConnectionId} as sender {SenderId}",
        messageWithSender.MessageType,
        messageWithSender.DocumentId?.Value,
        _connection.ConnectionId ?? "<pending>",
        messageWithSender.SenderId?.ToString());

      // Create MessageDto
      var messageData = GetMessageData(messageWithSender);
      var messageDto = MessageDto.FromRepoMessage(messageWithSender, messageData!);

      // Send to hub
      await _connection.InvokeAsync("SendMessage", messageDto);

      _logger.LogInformation("Hub accepted {MessageType} message for document {DocumentId} on connection {ConnectionId}",
        messageWithSender.MessageType,
        messageWithSender.DocumentId?.Value,
        _connection.ConnectionId ?? "<pending>");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to send message type {MessageType} for document {DocumentId}",
        message.MessageType, message.DocumentId?.Value);
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <summary>
  /// Subscribes to a document to receive sync messages.
  /// </summary>
  /// <param name="documentId">The document ID to subscribe to.</param>
  public async Task SubscribeToDocumentAsync(string documentId)
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(SignalRNetworkAdapter));

    ArgumentException.ThrowIfNullOrEmpty(documentId);

    await _lock.WaitAsync();
    try
    {
      await SubscribeToDocumentInternalAsync(documentId);
    }
    finally
    {
      _lock.Release();
    }
  }

  async Task SubscribeToDocumentInternalAsync(string documentId)
  {
    if (_subscribedDocuments.ContainsKey(documentId))
    {
      _logger.LogDebug("Already subscribed to document: {DocumentId}", documentId);
      return;
    }

    if (_connection is null)
    {
      _logger.LogWarning("Cannot subscribe: connection is null");
      return;
    }

    await _connection.InvokeAsync("SubscribeToDocument", documentId);
    _subscribedDocuments.TryAdd(documentId, 0);

    _logger.LogInformation("Subscribed to document: {DocumentId}", documentId);
  }

  /// <summary>
  /// Unsubscribes from a document.
  /// </summary>
  /// <param name="documentId">The document ID to unsubscribe from.</param>
  public async Task UnsubscribeFromDocumentAsync(string documentId)
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(SignalRNetworkAdapter));

    ArgumentException.ThrowIfNullOrEmpty(documentId);

    await _lock.WaitAsync();
    try
    {
      await UnsubscribeFromDocumentInternalAsync(documentId);
    }
    finally
    {
      _lock.Release();
    }
  }

  async Task UnsubscribeFromDocumentInternalAsync(string documentId)
  {
    if (!_subscribedDocuments.ContainsKey(documentId))
    {
      _logger.LogDebug("Not subscribed to document: {DocumentId}", documentId);
      return;
    }

    if (_connection is null)
    {
      _logger.LogWarning("Cannot unsubscribe: connection is null");
      return;
    }

    await _connection.InvokeAsync("UnsubscribeFromDocument", documentId);
    _subscribedDocuments.TryRemove(documentId, out _);

    _logger.LogInformation("Unsubscribed from document: {DocumentId}", documentId);
  }

  /// <summary>
  /// Queries the server for the list of peers currently on a document.
  /// </summary>
  /// <param name="documentId">The document ID to query.</param>
  /// <returns>A list of peer info DTOs for all other peers on the document.</returns>
  /// <exception cref="InvalidOperationException">Thrown when the adapter is not connected.</exception>
  public async Task<List<PeerInfoDto>> GetDocumentPeersAsync(string documentId)
  {
    if(_disposed)
      throw new ObjectDisposedException(nameof(SignalRNetworkAdapter));

    ArgumentException.ThrowIfNullOrEmpty(documentId);

    if(!_isReady || _connection is null)
      throw new InvalidOperationException("Cannot get document peers: adapter not connected.");

    return await _connection.InvokeAsync<List<PeerInfoDto>>("GetDocumentPeers", documentId);
  }

  /// <inheritdoc />
  public async Task Disconnect()
  {
    if (_disposed)
      return;

    await _lock.WaitAsync();
    try
    {
      if (!_isReady)
        return;

      _logger.LogInformation("Disconnecting SignalR adapter for peer: {PeerId}", _peerId);

      // Unsubscribe from all documents
      var documents = _subscribedDocuments.Keys.ToArray();
      foreach (var documentId in documents)
      {
        await UnsubscribeFromDocumentInternalAsync(documentId);
      }

      // Stop connection
      await _connectionService.Stop();

      // Clean up
      if (_connection is not null)
      {
        _connection.Closed -= OnConnectionClosedAsync;
        _connection = null;
      }

      _isReady = false;

      // Raise events
      if (_peerId is not null)
        RaisePeerDisconnected(_peerId.Value);

      RaiseClosed();

      _logger.LogInformation("Disconnected SignalR adapter for peer: {PeerId}", _peerId);
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <inheritdoc />
  Task IDocumentsHubClient.OnReceiveMessage(MessageDto messageDto)
  {
    try
    {
      _logger.LogInformation("SignalR hub delivered message type {MessageType} for document {DocumentId} from {SenderId}",
        messageDto.MessageType, messageDto.DocumentId, messageDto.SenderId);

      // Deserialize to IRepoMessage based on MessageType
      var message = DeserializeMessage(messageDto);
      if (message is null)
      {
        _logger.LogWarning("Failed to deserialize message type {MessageType}", messageDto.MessageType);
        return Task.CompletedTask;
      }

      // Raise MessageReceived event (deferred to avoid blocking SignalR thread)
      _ = Task.Run(() => RaiseMessageReceived(message));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing received message type {MessageType}", messageDto.MessageType);
    }

    return Task.CompletedTask;
  }

  /// <inheritdoc />
  Task IDocumentsHubClient.OnPeerJoined(PeerHandshakeDto handshake)
  {
    // In the server-as-participant model, other clients connecting to the same server
    // are NOT direct peers of this client — the server is the only direct peer.
    // Do not raise PeerCandidate here; the server handles relaying sync between clients.
    _logger.LogInformation("Peer joined notification (informational only): PeerId={PeerId}", handshake.PeerId);

    return Task.CompletedTask;
  }

  /// <inheritdoc />
  Task IDocumentsHubClient.OnDocumentPeersChanged(string documentId, List<PeerInfoDto> peers)
  {
    _logger.LogDebug(
      "Document peers changed: DocumentId={DocumentId}, PeerCount={PeerCount}",
      documentId, peers.Count);

    _ = Task.Run(() => RaiseDocumentPeersChanged(documentId, peers));

    return Task.CompletedTask;
  }

  /// <inheritdoc />
  Task IDocumentsHubClient.OnDocumentAvailable(string documentId)
  {
    _logger.LogDebug("Document available: DocumentId={DocumentId}", documentId);

    _ = Task.Run(() => RaiseDocumentAvailable(documentId));

    return Task.CompletedTask;
  }

  /// <inheritdoc />
  Task IDocumentsHubClient.OnPeerLeft(string peerId)
  {
    try
    {
      _logger.LogInformation("Peer left: PeerId={PeerId}", peerId);

      // Raise PeerDisconnected event to notify Repo
      // This will clean up synchronization state for the disconnected peer
      var peer = new PeerId(peerId);
      _ = Task.Run(() => RaisePeerDisconnected(peer));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing peer left notification for {PeerId}", peerId);
    }

    return Task.CompletedTask;
  }

  IRepoMessage? DeserializeMessage(MessageDto messageDto)
  {
    var senderId = new PeerId(messageDto.SenderId);

    DocumentId RequireDocumentId(string? value)
    {
      if(string.IsNullOrWhiteSpace(value))
      {
        _logger.LogWarning("Received {MessageType} message without a document ID", messageDto.MessageType);
        throw new InvalidOperationException($"Message type {messageDto.MessageType} missing document ID");
      }

      return DocumentId.Parse(value);
    }

    return messageDto.MessageType switch
    {
      "sync" => new SyncMessage
      {
        DocumentId = RequireDocumentId(messageDto.DocumentId),
        Data = messageDto.Data,
        SenderId = senderId
      },
      "request" => new RequestMessage
      {
        DocumentId = RequireDocumentId(messageDto.DocumentId),
        Data = messageDto.Data,
        SenderId = senderId
      },
      // Note: Ephemeral messages require additional fields (Count, SessionId) that aren't in MessageDto
      // These would need to be encoded in the Data field and deserialized here
      // For now, we log a warning and skip these messages
      "ephemeral" => LogUnsupportedMessage("ephemeral"),
      "doc-unavailable" => new DocumentUnavailableMessage
      {
        DocumentId = RequireDocumentId(messageDto.DocumentId),
        SenderId = senderId
      },
      "doc-snapshot" => new DocumentSnapshotMessage
      {
        DocumentId = RequireDocumentId(messageDto.DocumentId),
        Data = messageDto.Data ?? Array.Empty<byte>(),
        SenderId = senderId
      },
      // Note: RemoteHeadsChangedMessage requires NewHeads which isn't in MessageDto
      "remote-heads-changed" => LogUnsupportedMessage("remote-heads-changed"),
      // Note: RemoteSubscriptionChangeMessage has readonly DocumentId
      "remote-subscription-change" => LogUnsupportedMessage("remote-subscription-change"),
      _ => null
    };
  }

  IRepoMessage? LogUnsupportedMessage(string messageType)
  {
    _logger.LogWarning("Message type {MessageType} is not yet fully supported in wire protocol", messageType);
    return null;
  }

  Task OnConnectionClosedAsync(Exception? exception)
  {
    _logger.LogWarning(exception, "SignalR connection closed");

    _isReady = false;

    if (_peerId is not null)
      RaisePeerDisconnected(_peerId.Value);

    RaiseClosed();

    return Task.CompletedTask;
  }

  void RaisePeerCandidate(PeerId peerId, PeerMetadata? metadata)
  {
    try
    {
      PeerCandidate?.Invoke(this, new PeerCandidateEventArgs
      {
        PeerId = peerId,
        Metadata = metadata
      });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error raising PeerCandidate event");
    }
  }

  void RaisePeerDisconnected(PeerId peerId)
  {
    try
    {
      PeerDisconnected?.Invoke(this, new PeerDisconnectedEventArgs
      {
        PeerId = peerId
      });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error raising PeerDisconnected event");
    }
  }

  void RaiseMessageReceived(IRepoMessage message)
  {
    try
    {
      MessageReceived?.Invoke(this, new MessageReceivedEventArgs
      {
        Message = message
      });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error raising MessageReceived event");
    }
  }

  static byte[]? GetMessageData(IRepoMessage message)
  {
    return message switch
    {
      SyncMessage sync => sync.Data,
      RequestMessage request => request.Data,
      EphemeralMessage ephemeral => ephemeral.Data,
      DocumentUnavailableMessage _ => Array.Empty<byte>(),
      DocumentSnapshotMessage snapshot => snapshot.Data,
      _ => null
    };
  }

  static IRepoMessage EnsureSender(IRepoMessage message, PeerId senderId)
  {
    if (message.SenderId is not null)
      return message;

    return message switch
    {
      SyncMessage sync => sync with { SenderId = senderId },
      RequestMessage request => request with { SenderId = senderId },
      EphemeralMessage ephemeral => ephemeral with { SenderId = senderId },
      DocumentUnavailableMessage unavailable => unavailable with { SenderId = senderId },
      RemoteHeadsChangedMessage heads => heads with { SenderId = senderId },
      RemoteSubscriptionChangeMessage subscription => subscription with { SenderId = senderId },
      _ => message
    };
  }

  void RaiseDocumentPeersChanged(string documentId, List<PeerInfoDto> peers)
  {
    try
    {
      DocumentPeersChanged?.Invoke(this, new DocumentPeersChangedEventArgs
      {
        DocumentId = documentId,
        Peers = peers
      });
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Error raising DocumentPeersChanged event for document {DocumentId}", documentId);
    }
  }

  void RaiseDocumentAvailable(string documentId)
  {
    try
    {
      DocumentAvailable?.Invoke(this, new DocumentAvailableEventArgs
      {
        DocumentId = documentId
      });
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Error raising DocumentAvailable event for document {DocumentId}", documentId);
    }
  }

  void RaiseClosed()
  {
    try
    {
      Closed?.Invoke(this, EventArgs.Empty);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error raising Closed event");
    }
  }

  /// <inheritdoc />
  public void Dispose()
  {
    DisposeAsync().AsTask().GetAwaiter().GetResult();
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync()
  {
    if (_disposed)
      return;

    _disposed = true;

    await Disconnect();
    _lock.Dispose();
  }
}
