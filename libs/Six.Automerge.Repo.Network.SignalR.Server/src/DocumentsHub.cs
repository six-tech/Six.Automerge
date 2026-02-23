namespace Six.Automerge.Repository.Network.SignalR.Server;

/// <summary>
/// SignalR hub for Automerge document synchronization.
/// </summary>
/// <remarks>
/// This hub provides real-time message routing between connected peers for document collaboration.
/// Messages are organized into document-specific groups, ensuring efficient targeted delivery.
///
/// <para>
/// <strong>Key Features:</strong>
/// <list type="bullet">
///   <item><description>Document-based group management (one group per document ID)</description></item>
///   <item><description>Automatic subscription on first message send</description></item>
///   <item><description>Echo prevention (senders don't receive their own messages)</description></item>
///   <item><description>Graceful error handling with comprehensive logging</description></item>
/// </list>
/// </para>
///
/// <para>
/// <strong>Usage Example (ASP.NET Core):</strong>
/// <code>
/// app.MapHub&lt;DocumentsHub&gt;("/hubs/documents");
/// </code>
/// </para>
/// </remarks>
[PublicAPI]
public class DocumentsHub : Hub<IDocumentsHubClient>, IDocumentsHub
{
  readonly ILogger<DocumentsHub> _logger;

  /// <summary>
  /// Initializes a new instance of the <see cref="DocumentsHub"/> class.
  /// </summary>
  /// <param name="logger">The logger instance.</param>
  public DocumentsHub(ILogger<DocumentsHub> logger)
  {
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
  }

  /// <inheritdoc />
  public async Task<PeerHandshakeDto> Handshake(PeerHandshakeDto handshake)
  {
    try
    {
      _logger.LogInformation(
        "Peer handshake: PeerId={PeerId}, ConnectionId={ConnectionId}, IsEphemeral={IsEphemeral}",
        handshake.PeerId,
        Context.ConnectionId,
        handshake.Metadata?.IsEphemeral ?? false);

      // Store peer ID in connection context for later use
      Context.Items["PeerId"] = handshake.PeerId;

      // Broadcast peer join to all OTHER clients (not the joining client itself)
      // This enables peer discovery for document synchronization
      await Clients.Others.OnPeerJoined(handshake);

      _logger.LogDebug(
        "Peer join broadcasted to all other clients: PeerId={PeerId}",
        handshake.PeerId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during handshake for ConnectionId={ConnectionId}", Context.ConnectionId);
    }

    // Relay-mode hub has no Repo; return a stable server identity so clients
    // know who their direct peer is.
    return PeerHandshakeDto.Create(new PeerId($"relay-{Context.ConnectionId}"), null);
  }

  /// <inheritdoc />
  public async Task SubscribeToDocument(string documentId)
  {
    try
    {
      if (string.IsNullOrEmpty(documentId))
      {
        _logger.LogWarning("Attempt to subscribe with null/empty documentId. ConnectionId={ConnectionId}",
          Context.ConnectionId);
        return;
      }

      await Groups.AddToGroupAsync(Context.ConnectionId, documentId);

      _logger.LogInformation(
        "Peer subscribed to document: DocumentId={DocumentId}, ConnectionId={ConnectionId}, PeerId={PeerId}",
        documentId,
        Context.ConnectionId,
        Context.Items.TryGetValue("PeerId", out var peerId) ? peerId : "unknown");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error subscribing to document {DocumentId} for ConnectionId={ConnectionId}",
        documentId, Context.ConnectionId);
    }
  }

  /// <inheritdoc />
  public async Task UnsubscribeFromDocument(string documentId)
  {
    try
    {
      if (string.IsNullOrEmpty(documentId))
      {
        _logger.LogWarning("Attempt to unsubscribe with null/empty documentId. ConnectionId={ConnectionId}",
          Context.ConnectionId);
        return;
      }

      await Groups.RemoveFromGroupAsync(Context.ConnectionId, documentId);

      _logger.LogInformation(
        "Peer unsubscribed from document: DocumentId={DocumentId}, ConnectionId={ConnectionId}, PeerId={PeerId}",
        documentId,
        Context.ConnectionId,
        Context.Items.TryGetValue("PeerId", out var peerId) ? peerId : "unknown");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error unsubscribing from document {DocumentId} for ConnectionId={ConnectionId}",
        documentId, Context.ConnectionId);
    }
  }

  /// <inheritdoc />
  public async Task SendMessage(MessageDto message)
  {
    try
    {
      if (string.IsNullOrEmpty(message.DocumentId))
      {
        _logger.LogWarning(
          "Received message with null/empty DocumentId. MessageType={MessageType}, SenderId={SenderId}, ConnectionId={ConnectionId}",
          message.MessageType,
          message.SenderId,
          Context.ConnectionId);
        return;
      }

      _logger.LogInformation(
        "Received message from client. DocumentId={DocumentId}, MessageType={MessageType}, SenderId={SenderId}, ConnectionId={ConnectionId}",
        message.DocumentId,
        message.MessageType,
        message.SenderId,
        Context.ConnectionId);

      _logger.LogDebug(
        "Routing message: DocumentId={DocumentId}, MessageType={MessageType}, SenderId={SenderId}, ConnectionId={ConnectionId}, DataSize={DataSize}",
        message.DocumentId,
        message.MessageType,
        message.SenderId,
        Context.ConnectionId,
        message.Data?.Length ?? 0);

      // Auto-subscribe sender to document group if not already subscribed
      // This ensures messages are delivered even if explicit subscription wasn't called
      await Groups.AddToGroupAsync(Context.ConnectionId, message.DocumentId);

      // Broadcast to all group members EXCEPT the sender (prevents echo-back)
      await Clients.OthersInGroup(message.DocumentId).OnReceiveMessage(message);

      _logger.LogDebug(
        "Message routed successfully: DocumentId={DocumentId}, MessageType={MessageType}",
        message.DocumentId,
        message.MessageType);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex,
        "Error routing message: DocumentId={DocumentId}, MessageType={MessageType}, SenderId={SenderId}, ConnectionId={ConnectionId}",
        message.DocumentId,
        message.MessageType,
        message.SenderId,
        Context.ConnectionId);
    }
  }

  /// <inheritdoc />
  public Task<List<PeerInfoDto>> GetDocumentPeers(string documentId)
  {
    // Relay-mode hub does not track peer presence — return empty list.
    return Task.FromResult(new List<PeerInfoDto>());
  }

  /// <inheritdoc />
  public Task<int> GetDocumentPeerCount(string documentId)
  {
    // Relay-mode hub does not track peer presence — return zero.
    return Task.FromResult(0);
  }

  /// <summary>
  /// Called when a new connection is established.
  /// </summary>
  public override Task OnConnectedAsync()
  {
    _logger.LogInformation("Client connected: ConnectionId={ConnectionId}", Context.ConnectionId);
    return base.OnConnectedAsync();
  }

  /// <summary>
  /// Called when a connection is terminated.
  /// </summary>
  /// <param name="exception">The exception that caused the disconnect, if any.</param>
  public override async Task OnDisconnectedAsync(Exception? exception)
  {
    var peerId = Context.Items.TryGetValue("PeerId", out var id) ? id?.ToString() : null;

    if (exception is not null)
    {
      _logger.LogWarning(exception,
        "Client disconnected with error: ConnectionId={ConnectionId}, PeerId={PeerId}",
        Context.ConnectionId,
        peerId ?? "unknown");
    }
    else
    {
      _logger.LogInformation(
        "Client disconnected: ConnectionId={ConnectionId}, PeerId={PeerId}",
        Context.ConnectionId,
        peerId ?? "unknown");
    }

    // Broadcast peer left to all other clients if we have a peer ID
    if (!string.IsNullOrEmpty(peerId))
    {
      await Clients.Others.OnPeerLeft(peerId);

      _logger.LogDebug(
        "Peer left broadcasted to all other clients: PeerId={PeerId}",
        peerId);
    }

    await base.OnDisconnectedAsync(exception);
  }
}
