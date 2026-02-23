namespace Six.Automerge.Repository.Network;

/// <summary>
/// Interface for network adapters that transport messages between peers.
/// Implementations can use any transport: WebSocket, WebRTC, SignalR, MQTT, etc.
/// </summary>
/// <remarks>
/// <para>Network adapters are responsible for:</para>
/// <list type="bullet">
/// <item><description>Establishing and maintaining peer connections</description></item>
/// <item><description>Serializing and deserializing messages</description></item>
/// <item><description>Delivering messages reliably to connected peers</description></item>
/// <item><description>Notifying the subsystem of peer lifecycle events</description></item>
/// </list>
/// <para>Thread Safety: Implementations must be thread-safe. Methods may be called from any thread,
/// and events may be raised from any thread.</para>
/// </remarks>
[PublicAPI]
public interface INetworkAdapter
{
  /// <summary>
  /// Unique identifier for this adapter instance.
  /// </summary>
  string AdapterId { get; }

  /// <summary>
  /// Checks if the adapter is ready to send/receive messages.
  /// </summary>
  bool IsReady { get; }

  /// <summary>
  /// Returns a task that completes when the adapter is ready.
  /// </summary>
  Task WhenReady();

  /// <summary>
  /// Initiates connection to a peer.
  /// </summary>
  /// <param name="peerId">The peer to connect to.</param>
  /// <param name="metadata">Optional metadata about the peer connection.</param>
  void Connect(PeerId peerId, PeerMetadata? metadata = null);

  /// <summary>
  /// Sends a message through this adapter.
  /// </summary>
  /// <param name="message">The message to send.</param>
  void Send(IRepoMessage message);

  /// <summary>
  /// Disconnects this adapter and cleans up resources.
  /// </summary>
  Task Disconnect();

  /// <summary>
  /// Raised when a new peer is discovered or connected.
  /// </summary>
  event EventHandler<PeerCandidateEventArgs>? PeerCandidate;

  /// <summary>
  /// Raised when a peer disconnects.
  /// </summary>
  event EventHandler<PeerDisconnectedEventArgs>? PeerDisconnected;

  /// <summary>
  /// Raised when a message is received from a peer.
  /// </summary>
  event EventHandler<MessageReceivedEventArgs>? MessageReceived;

  /// <summary>
  /// Raised when the adapter is closed.
  /// </summary>
  event EventHandler? Closed;
}