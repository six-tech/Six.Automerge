namespace Six.Automerge.Repository.Network;

/// <summary>
/// In-memory network adapter for testing. Messages are delivered synchronously.
/// </summary>
/// <remarks>
/// This adapter is useful for unit and integration tests where you need to simulate
/// network communication without actual network infrastructure.
/// Messages are delivered immediately and synchronously to all connected peers.
/// </remarks>
[PublicAPI]
public sealed class InMemoryNetworkAdapter : INetworkAdapter
{
  readonly List<InMemoryNetworkAdapter> _connectedAdapters = [];
  readonly PeerId _localPeerId;
  readonly object _lock = new();
  
  public string AdapterId { get; }
  public bool IsReady => true;
  public Task WhenReady() => Task.CompletedTask;

  public event EventHandler<PeerCandidateEventArgs>? PeerCandidate;
  public event EventHandler<PeerDisconnectedEventArgs>? PeerDisconnected;
  public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
  public event EventHandler? Closed;
  
  

  /// <summary>
  /// Initializes a new instance of the InMemoryNetworkAdapter.
  /// </summary>
  /// <param name="localPeerId">The peer ID for this adapter. If null, a new ID will be generated.</param>
  public InMemoryNetworkAdapter(PeerId? localPeerId = null)
  {
    _localPeerId = localPeerId ?? PeerId.Generate();
    AdapterId = $"in-memory-{_localPeerId}";
  }


  /// <summary>
  /// Connects this adapter to another in-memory adapter.
  /// This establishes a bidirectional connection between the two adapters.
  /// </summary>
  /// <param name="otherAdapter">The adapter to connect to.</param>
  public void ConnectTo(InMemoryNetworkAdapter otherAdapter)
  {
    if(otherAdapter is null) throw new ArgumentNullException(nameof(otherAdapter));
    if(otherAdapter == this) throw new ArgumentException("Cannot connect to self", nameof(otherAdapter));

    lock(_lock)
    {
      if(_connectedAdapters.Contains(otherAdapter)) return;

      _connectedAdapters.Add(otherAdapter);
    }

    lock(otherAdapter._lock)
    {
      if(!otherAdapter._connectedAdapters.Contains(this))
      {
        otherAdapter._connectedAdapters.Add(this);
      }
    }

    // Notify peer candidate events
    PeerCandidate?.Invoke(this, new PeerCandidateEventArgs { PeerId = otherAdapter._localPeerId, Metadata = null });

    otherAdapter.PeerCandidate?.Invoke(otherAdapter,
      new PeerCandidateEventArgs { PeerId = _localPeerId, Metadata = null });
  }

  /// <inheritdoc />
  public void Connect(PeerId peerId, PeerMetadata? metadata = null)
  {
    // No-op for in-memory adapter (use ConnectTo instead)
  }

  /// <inheritdoc />
  public void Send(IRepoMessage message)
  {
    if(message is null) throw new ArgumentNullException(nameof(message));

    // Add sender ID
    var messageWithSender = message.WithSender(_localPeerId);

    // Deliver to all connected adapters
    List<InMemoryNetworkAdapter> adapters;
    lock(_lock)
    {
      adapters = _connectedAdapters.ToList();
    }

    foreach(var adapter in adapters)
    {
      adapter.MessageReceived?.Invoke(adapter, new MessageReceivedEventArgs { Message = messageWithSender });
    }
  }

  /// <inheritdoc />
  public Task Disconnect()
  {
    List<InMemoryNetworkAdapter> adapters;
    lock(_lock)
    {
      adapters = _connectedAdapters.ToList();
      _connectedAdapters.Clear();
    }

    foreach(var adapter in adapters)
    {
      lock(adapter._lock)
      {
        adapter._connectedAdapters.Remove(this);
      }

      adapter.PeerDisconnected?.Invoke(adapter, new PeerDisconnectedEventArgs { PeerId = _localPeerId });
    }

    Closed?.Invoke(this, EventArgs.Empty);

    return Task.CompletedTask;
  }
}