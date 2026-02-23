namespace Six.Automerge.Repository.Network.SignalR;

/// <summary>
/// Defines the client-side interface for SignalR hub callbacks.
/// </summary>
/// <remarks>
/// This interface is implemented by SignalR clients to receive messages from the server hub.
/// The server hub uses this interface via strongly-typed client proxies to invoke methods on connected clients.
///
/// <para>
/// <strong>Usage Example (Client Implementation):</strong>
/// <code>
/// public class MyAdapter : IDocumentsHubClient
/// {
///   public Task OnReceiveMessage(MessageDto message)
///   {
///     // Deserialize message.Data based on message.MessageType
///     // Raise MessageReceived event with IRepoMessage
///     return Task.CompletedTask;
///   }
/// }
/// </code>
/// </para>
///
/// <para>
/// <strong>Usage Example (Server Broadcasting):</strong>
/// <code>
/// // In DocumentsHub
/// await Clients.Group(documentId).OnReceiveMessage(messageDto);
/// </code>
/// </para>
/// </remarks>
[PublicAPI]
public interface IDocumentsHubClient
{
  /// <summary>
  /// Called by the server when a message is received for a document the client is subscribed to.
  /// </summary>
  /// <param name="message">The message data transfer object containing the serialized repo message.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  /// <remarks>
  /// This method is invoked by the SignalR hub when another peer sends a message to a document group
  /// that this client is subscribed to. The client should:
  ///
  /// <list type="number">
  ///   <item><description>Deserialize <see cref="MessageDto.Data"/> based on <see cref="MessageDto.MessageType"/></description></item>
  ///   <item><description>Reconstruct the appropriate <see cref="IRepoMessage"/> subtype</description></item>
  ///   <item><description>Raise the <c>MessageReceived</c> event for the network adapter</description></item>
  /// </list>
  ///
  /// The method should handle errors gracefully and return quickly to avoid blocking the SignalR pipeline.
  /// </remarks>
  Task OnReceiveMessage(MessageDto message);

  /// <summary>
  /// Called by the server when a new peer joins the network.
  /// </summary>
  /// <param name="handshake">The handshake information for the new peer.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  /// <remarks>
  /// This method is invoked by the SignalR hub when a new peer completes their handshake.
  /// The client should raise a <c>PeerCandidate</c> event to notify the Repo about the new peer,
  /// which will trigger document synchronization with the newly connected peer.
  /// </remarks>
  Task OnPeerJoined(PeerHandshakeDto handshake);

  /// <summary>
  /// Called by the server when a peer disconnects from the network.
  /// </summary>
  /// <param name="peerId">The ID of the peer that disconnected.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  /// <remarks>
  /// This method is invoked by the SignalR hub when a peer disconnects.
  /// The client should raise a <c>PeerDisconnected</c> event to notify the Repo,
  /// which will clean up synchronization state for the disconnected peer.
  /// </remarks>
  Task OnPeerLeft(string peerId);

  /// <summary>
  /// Called by the server when the list of peers editing a document changes.
  /// </summary>
  /// <param name="documentId">The document whose peer list changed.</param>
  /// <param name="peers">
  /// The current list of peers editing the document, excluding the receiving client's own entry.
  /// </param>
  /// <returns>A task representing the asynchronous operation.</returns>
  /// <remarks>
  /// This method is invoked whenever a peer subscribes to, sends a message to,
  /// or disconnects from a document. The peer list excludes the receiving client
  /// so it only contains "other" peers.
  /// </remarks>
  Task OnDocumentPeersChanged(string documentId, List<PeerInfoDto> peers);

  /// <summary>
  /// Called by the server when a new document becomes available (first seen by the server).
  /// </summary>
  /// <param name="documentId">The ID of the newly available document.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  Task OnDocumentAvailable(string documentId);
}
