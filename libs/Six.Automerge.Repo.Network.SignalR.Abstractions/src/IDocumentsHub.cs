namespace Six.Automerge.Repository.Network.SignalR;

/// <summary>
/// Defines the server-side interface for the SignalR documents hub.
/// </summary>
/// <remarks>
/// This interface is implemented by the server hub to handle client-to-server RPC calls.
/// Clients invoke these methods to manage document subscriptions and send sync messages.
///
/// <para>
/// <strong>Document Groups:</strong><br/>
/// Each document has a corresponding SignalR group identified by its <c>DocumentId</c>.
/// Clients subscribe to groups to receive messages for specific documents.
/// The hub automatically routes messages to all group members except the sender.
/// </para>
///
/// <para>
/// <strong>Usage Example (Client Calling Hub):</strong>
/// <code>
/// var connection = new HubConnectionBuilder()
///   .WithUrl("https://server/hubs/documents")
///   .Build();
///
/// await connection.InvokeAsync("Handshake", new PeerHandshakeDto
/// {
///   PeerId = "peer-123",
///   Metadata = null
/// });
///
/// await connection.InvokeAsync("SubscribeToDocument", "doc-abc");
///
/// await connection.InvokeAsync("SendMessage", new MessageDto
/// {
///   MessageType = "sync",
///   DocumentId = "doc-abc",
///   SenderId = "peer-123",
///   Data = syncPayload
/// });
/// </code>
/// </para>
///
/// <para>
/// <strong>Usage Example (Server Hub Implementation):</strong>
/// <code>
/// public class DocumentsHub : Hub&lt;IDocumentsHubClient&gt;, IDocumentsHub
/// {
///   public async Task SendMessage(MessageDto message)
///   {
///     // Broadcast to all in group except sender
///     await Clients.OthersInGroup(message.DocumentId).OnReceiveMessage(message);
///   }
/// }
/// </code>
/// </para>
/// </remarks>
[PublicAPI]
public interface IDocumentsHub
{
  /// <summary>
  /// Establishes peer identity and metadata during connection.
  /// </summary>
  /// <param name="handshake">The handshake data containing peer ID and optional metadata.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  /// <remarks>
  /// This method should be called by the client immediately after connecting to the hub.
  /// The server uses the peer ID for tracking connections and preventing message echo-back.
  ///
  /// <para>
  /// Typical flow:
  /// <list type="number">
  ///   <item><description>Client connects to hub</description></item>
  ///   <item><description>Client calls <c>Handshake</c> with peer ID</description></item>
  ///   <item><description>Server logs connection and stores peer metadata</description></item>
  ///   <item><description>Client is ready to subscribe and send messages</description></item>
  /// </list>
  /// </para>
  /// </remarks>
  Task<PeerHandshakeDto> Handshake(PeerHandshakeDto handshake);

  /// <summary>
  /// Subscribes the calling peer to a document group to receive sync messages.
  /// </summary>
  /// <param name="documentId">The unique identifier of the document to subscribe to.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  /// <remarks>
  /// After subscribing, the client will receive all messages sent to this document via
  /// <see cref="IDocumentsHubClient.OnReceiveMessage"/>.
  ///
  /// <para>
  /// Subscriptions are automatically managed:
  /// <list type="bullet">
  ///   <item><description>Duplicate subscriptions are idempotent (no error)</description></item>
  ///   <item><description>Auto-subscribe occurs on first <see cref="SendMessage"/> if not already subscribed</description></item>
  ///   <item><description>All subscriptions are cleared on disconnect</description></item>
  /// </list>
  /// </para>
  /// </remarks>
  Task SubscribeToDocument(string documentId);

  /// <summary>
  /// Unsubscribes the calling peer from a document group.
  /// </summary>
  /// <param name="documentId">The unique identifier of the document to unsubscribe from.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  /// <remarks>
  /// After unsubscribing, the client will no longer receive messages for this document.
  /// Unsubscribing from a non-subscribed document is safe and does nothing.
  ///
  /// <para>
  /// Clients typically unsubscribe when:
  /// <list type="bullet">
  ///   <item><description>No longer editing a document</description></item>
  ///   <item><description>Reducing resource usage (memory, bandwidth)</description></item>
  ///   <item><description>Disconnecting gracefully (though automatic cleanup occurs on disconnect)</description></item>
  /// </list>
  /// </para>
  /// </remarks>
  Task UnsubscribeFromDocument(string documentId);

  /// <summary>
  /// Sends a sync message to all peers subscribed to the document.
  /// </summary>
  /// <param name="message">The message data transfer object containing the serialized repo message.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  /// <remarks>
  /// This is the primary method for Automerge document synchronization. The hub:
  ///
  /// <list type="number">
  ///   <item><description>Validates the message has a valid <see cref="MessageDto.DocumentId"/></description></item>
  ///   <item><description>Auto-subscribes the sender to the document group (if not already subscribed)</description></item>
  ///   <item><description>Broadcasts the message to all group members <strong>except the sender</strong></description></item>
  /// </list>
  ///
  /// <para>
  /// The message is sent to other peers via <see cref="IDocumentsHubClient.OnReceiveMessage"/>.
  /// The sender never receives their own message (SignalR's <c>OthersInGroup</c> behavior).
  /// </para>
  ///
  /// <para>
  /// <strong>Error Handling:</strong><br/>
  /// If <see cref="MessageDto.DocumentId"/> is null or empty, the message is logged and dropped.
  /// Serialization errors are logged but do not throw exceptions to avoid disrupting other clients.
  /// </para>
  /// </remarks>
  Task SendMessage(MessageDto message);

  /// <summary>
  /// Returns the list of peers currently editing a document, excluding the calling peer.
  /// </summary>
  /// <param name="documentId">The document to query.</param>
  /// <returns>A list of peer info DTOs for all other peers on the document.</returns>
  Task<List<PeerInfoDto>> GetDocumentPeers(string documentId);

  /// <summary>
  /// Returns the count of peers currently editing a document, excluding the calling peer.
  /// </summary>
  /// <param name="documentId">The document to query.</param>
  /// <returns>The number of other peers on the document.</returns>
  Task<int> GetDocumentPeerCount(string documentId);
}
