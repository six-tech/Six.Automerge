namespace Six.Automerge.Repository.Network;

/// <summary>
/// Manages network communication across multiple adapters with message routing and deduplication.
/// </summary>
/// <remarks>
/// <para>NetworkSubsystem provides:</para>
/// <list type="bullet">
/// <item><description>Support for multiple concurrent network adapters</description></item>
/// <item><description>Message routing between adapters and the repo</description></item>
/// <item><description>Ephemeral message deduplication to prevent gossip loops</description></item>
/// <item><description>Peer connection lifecycle management</description></item>
/// <item><description>Peer metadata storage</description></item>
/// </list>
/// <para>Thread Safety: This class is thread-safe. All public methods and properties can be called from any thread.</para>
/// </remarks>
[PublicAPI]
public sealed class NetworkSubsystem : IDisposable
{
  readonly List<INetworkAdapter> _adapters = [];
  readonly Dictionary<PeerId, INetworkAdapter> _peerToAdapter = [];
  readonly Dictionary<string, PeerMetadata> _peerMetadata = [];
  readonly HashSet<string> _seenEphemeralMessages = [];
  readonly SemaphoreSlim _lock = new(1, 1);
  bool _isDisposed;


  /// <summary>
  /// Raised when a new peer candidate is discovered by any adapter.
  /// </summary>
  public event EventHandler<PeerCandidateEventArgs>? PeerCandidate;

  /// <summary>
  /// Raised when a peer disconnects from any adapter.
  /// </summary>
  public event EventHandler<PeerDisconnectedEventArgs>? PeerDisconnected;

  /// <summary>
  /// Raised when a message is received from any adapter.
  /// </summary>
  public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

  /// <summary>
  /// Returns true if at least one adapter is ready.
  /// </summary>
  public bool IsReady
  {
    get
    {
      ThrowIfDisposed();
      _lock.Wait();
      try
      {
        return _adapters.Any(a => a.IsReady);
      }
      finally
      {
        _lock.Release();
      }
    }
  }

  /// <summary>
  /// Returns all currently connected peers.
  /// </summary>
  public IReadOnlyList<PeerId> Peers
  {
    get
    {
      ThrowIfDisposed();
      _lock.Wait();
      try
      {
        return _peerToAdapter.Keys.ToList();
      }
      finally
      {
        _lock.Release();
      }
    }
  }


  /// <summary>
  /// Initializes a new instance of the NetworkSubsystem.
  /// </summary>
  /// <param name="adapters">Optional initial set of network adapters.</param>
  public NetworkSubsystem(IEnumerable<INetworkAdapter>? adapters = null)
  {
    if(adapters is not null)
    {
      foreach(var adapter in adapters)
      {
        AddAdapter(adapter);
      }
    }
  }

  /// <summary>
  /// Disposes the NetworkSubsystem, disconnecting all adapters and releasing resources.
  /// </summary>
  public void Dispose()
  {
    if(_isDisposed) return;

    _lock.Wait();
    try
    {
      _isDisposed = true;

      // Disconnect and unwire all adapters
      foreach(var adapter in _adapters.ToList())
      {
        adapter.PeerCandidate -= OnAdapterPeerCandidate;
        adapter.PeerDisconnected -= OnAdapterPeerDisconnected;
        adapter.MessageReceived -= OnAdapterMessageReceived;

        // Note: We don't call Disconnect() here as that's the responsibility of the adapter owner
      }

      _adapters.Clear();
      _peerToAdapter.Clear();
      _peerMetadata.Clear();
      _seenEphemeralMessages.Clear();
    }
    finally
    {
      _lock.Release();
      _lock.Dispose();
    }
  }

  
  // PUBLIC METHODS
  // ---------------------------------------------------------------------------------------------------------

  /// <summary>
  /// Adds a network adapter to the subsystem.
  /// </summary>
  /// <param name="adapter">The adapter to add.</param>
  /// <exception cref="ArgumentNullException">Thrown if adapter is null.</exception>
  /// <exception cref="ObjectDisposedException">Thrown if the subsystem has been disposed.</exception>
  public void AddAdapter(INetworkAdapter adapter)
  {
    if(adapter is null) throw new ArgumentNullException(nameof(adapter));
    ThrowIfDisposed();

    _lock.Wait();
    try
    {
      if(_adapters.Contains(adapter)) return;

      _adapters.Add(adapter);

      // Wire up events
      adapter.PeerCandidate += OnAdapterPeerCandidate;
      adapter.PeerDisconnected += OnAdapterPeerDisconnected;
      adapter.MessageReceived += OnAdapterMessageReceived;
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <summary>
  /// Removes a network adapter from the subsystem.
  /// </summary>
  /// <param name="adapter">The adapter to remove.</param>
  /// <exception cref="ArgumentNullException">Thrown if adapter is null.</exception>
  /// <exception cref="ObjectDisposedException">Thrown if the subsystem has been disposed.</exception>
  public void RemoveAdapter(INetworkAdapter adapter)
  {
    if(adapter is null) throw new ArgumentNullException(nameof(adapter));
    ThrowIfDisposed();

    _lock.Wait();
    try
    {
      if(!_adapters.Remove(adapter)) return;

      // Unwire events
      adapter.PeerCandidate -= OnAdapterPeerCandidate;
      adapter.PeerDisconnected -= OnAdapterPeerDisconnected;
      adapter.MessageReceived -= OnAdapterMessageReceived;

      // Remove peers associated with this adapter
      var peersToRemove = _peerToAdapter
        .Where(kvp => kvp.Value == adapter)
        .Select(kvp => kvp.Key)
        .ToList();

      foreach(var peer in peersToRemove)
      {
        _peerToAdapter.Remove(peer);
        _peerMetadata.Remove(peer);
      }
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <summary>
  /// Sends a message through the appropriate adapter(s).
  /// For messages with a target peer, sends via that peer's adapter.
  /// For broadcast messages, sends via all ready adapters.
  /// </summary>
  /// <param name="message">The message to send.</param>
  /// <exception cref="ArgumentNullException">Thrown if message is null.</exception>
  /// <exception cref="ObjectDisposedException">Thrown if the subsystem has been disposed.</exception>
  public void Send(IRepoMessage message)
  {
    if(message is null)
      throw new ArgumentNullException(nameof(message));

    ThrowIfDisposed();

    // Collect target adapters under lock, then send outside the lock.
    // This prevents deadlocks when adapters deliver synchronously (e.g., InMemoryNetworkAdapter)
    // causing reentrant Send() calls on the same thread.
    List<INetworkAdapter> targets;

    _lock.Wait();
    try
    {
      targets = new List<INetworkAdapter>();

      if(message.TargetPeerId is { } targetPeerId &&
         _peerToAdapter.TryGetValue(targetPeerId, out var targetAdapter))
      {
        if(targetAdapter.IsReady)
        {
          targets.Add(targetAdapter);
        }
      }
      else
      {
        foreach(var adapter in _adapters)
        {
          if(adapter.IsReady)
          {
            targets.Add(adapter);
          }
        }
      }
    }
    finally
    {
      _lock.Release();
    }

    // Send outside the lock to allow reentrant message delivery
    foreach(var adapter in targets)
    {
      adapter.Send(message);
    }
  }

  /// <summary>
  /// Waits for at least one adapter to be ready.
  /// </summary>
  /// <returns>A task that completes when at least one adapter is ready.</returns>
  /// <exception cref="ObjectDisposedException">Thrown if the subsystem has been disposed.</exception>
  public async Task WhenReady()
  {
    ThrowIfDisposed();

    // Poll adapters until one is ready
    while(true)
    {
      await _lock.WaitAsync();
      try
      {
        if(_adapters.Any(a => a.IsReady))
          return;
      }
      finally
      {
        _lock.Release();
      }

      // Wait a bit before checking again
      await Task.Delay(100);
    }
  }

  /// <summary>
  /// Returns metadata for a peer, if available.
  /// </summary>
  /// <param name="peerId">The peer ID to look up.</param>
  /// <returns>The peer metadata, or null if not found.</returns>
  /// <exception cref="ObjectDisposedException">Thrown if the subsystem has been disposed.</exception>
  public PeerMetadata? GetPeerMetadata(PeerId peerId)
  {
    ThrowIfDisposed();

    _lock.Wait();
    try
    {
      return _peerMetadata.TryGetValue(peerId, out var metadata) ? metadata : null;
    }
    finally
    {
      _lock.Release();
    }
  }


  // PRIVATE METHODS
  // ---------------------------------------------------------------------------------------------------------
  void OnAdapterPeerCandidate(object? sender, PeerCandidateEventArgs e)
  {
    if(sender is not INetworkAdapter adapter) return;

    _lock.Wait();
    try
    {
      // Track peer-to-adapter mapping
      _peerToAdapter[e.PeerId] = adapter;

      // Store peer metadata
      if(e.Metadata is not null)
      {
        _peerMetadata[e.PeerId] = e.Metadata;
      }
    }
    finally
    {
      _lock.Release();
    }

    // Forward event to subscribers
    PeerCandidate?.Invoke(this, e);
  }

  void OnAdapterPeerDisconnected(object? sender, PeerDisconnectedEventArgs e)
  {
    _lock.Wait();
    try
    {
      _peerToAdapter.Remove(e.PeerId);
      _peerMetadata.Remove(e.PeerId);
    }
    finally
    {
      _lock.Release();
    }

    // Forward event to subscribers
    PeerDisconnected?.Invoke(this, e);
  }

  void OnAdapterMessageReceived(object? sender, MessageReceivedEventArgs e)
  {
    var message = e.Message;

    // Deduplicate ephemeral messages
    if(message is EphemeralMessage ephemeral)
    {
      if(!ShouldProcessEphemeralMessage(ephemeral))
      {
        // Already seen this message, skip
        return;
      }
    }

    // Forward message to subscribers
    MessageReceived?.Invoke(this, e);
  }

  /// <summary>
  /// Checks if ephemeral message should be processed (deduplication).
  /// Returns true if message is new, false if already seen.
  /// </summary>
  bool ShouldProcessEphemeralMessage(EphemeralMessage message)
  {
    // Create the unique key: peerId:sessionId:count
    var key = $"{message.SenderId}:{message.SessionId}:{message.Count}";

    _lock.Wait();
    try
    {
      if(!_seenEphemeralMessages.Add(key))
        return false; // Already seen

      // Add to seen set
      // Cleanup old entries periodically (keep last 1000)
      if(_seenEphemeralMessages.Count > 1000)
      {
        // Remove oldest half
        var toRemove = _seenEphemeralMessages.Take(500).ToList();
        foreach(var item in toRemove)
        {
          _seenEphemeralMessages.Remove(item);
        }
      }

      return true; // New message
    }
    finally
    {
      _lock.Release();
    }
  }

  void ThrowIfDisposed()
  {
    if(_isDisposed)
    {
      throw new ObjectDisposedException(nameof(NetworkSubsystem));
    }
  }
}