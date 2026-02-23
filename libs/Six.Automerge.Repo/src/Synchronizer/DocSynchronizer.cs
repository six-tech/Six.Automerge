using Six.Automerge.Repository.Helpers;
using Six.Automerge.Repository.Network;

namespace Six.Automerge.Repository.Synchronizer;

/// <summary>
/// Manages synchronization for a single document across multiple peers.
/// Maintains per-peer sync state and implements the Automerge sync protocol.
/// </summary>
internal sealed partial class DocSynchronizer : IDisposable
{
  readonly string _documentId;
  readonly DocumentId _documentIdValue;
  readonly Func<AutomergeDocument?> _getDocument;
  readonly Func<AutomergeDocument, PeerId?, Task> _updateDocument;
  readonly Func<PeerId, PeerMetadata?> _getPeerMetadata;
  readonly Func<SemaphoreSlim?> _getDocumentLock;
  readonly int _syncDebounceRate;
  readonly ILogger<DocSynchronizer> _logger;

  readonly Dictionary<PeerId, AutomergeSyncState> _syncStates = [];
  readonly Dictionary<PeerId, PeerDocumentStatus> _peerStatuses = [];
  readonly HashSet<PeerId> _activePeers = [];
  readonly SemaphoreSlim _lock = new(1, 1);
  readonly Throttle<PeerId>? _syncThrottle;

  int _messagesSent;
  int _messagesReceived;
  bool _disposed;

  /// <summary>
  /// Event fired when a message should be sent to a peer.
  /// </summary>
  public event EventHandler<SyncMessageEventArgs>? MessageToSend;

  /// <summary>
  /// Event fired when sync state changes (for persistence).
  /// </summary>
  public event EventHandler<SyncStateChangedEventArgs>? SyncStateChanged;

  /// <summary>
  /// Event fired when sync metrics are updated.
  /// </summary>
  public event EventHandler<SyncMetricsEventArgs>? MetricsUpdated;

  /// <summary>
  /// Creates a new DocSynchronizer for a specific document.
  /// </summary>
  /// <param name="documentId">The document ID being synchronized</param>
  /// <param name="getDocument">Callback to retrieve the current document</param>
  /// <param name="updateDocument">Async callback to update the document after receiving changes</param>
  /// <param name="getPeerMetadata">Callback to retrieve peer metadata</param>
  /// <param name="getDocumentLock">Callback to retrieve the document lock for serializing FFI access</param>
  /// <param name="logger">Logger for diagnostics and tracing</param>
  /// <param name="syncDebounceRate">Debounce rate in milliseconds for sync message sending on local changes. 0 disables throttling.</param>
  public DocSynchronizer(
    string documentId,
    Func<AutomergeDocument?> getDocument,
    Func<AutomergeDocument, PeerId?, Task> updateDocument,
    Func<PeerId, PeerMetadata?> getPeerMetadata,
    Func<SemaphoreSlim?> getDocumentLock,
    ILogger<DocSynchronizer> logger,
    int syncDebounceRate = 0)
  {
    _documentId = documentId ?? throw new ArgumentNullException(nameof(documentId));
    _documentIdValue = DocumentId.Parse(documentId);
    _getDocument = getDocument ?? throw new ArgumentNullException(nameof(getDocument));
    _updateDocument = updateDocument ?? throw new ArgumentNullException(nameof(updateDocument));
    _getPeerMetadata = getPeerMetadata ?? throw new ArgumentNullException(nameof(getPeerMetadata));
    _getDocumentLock = getDocumentLock ?? throw new ArgumentNullException(nameof(getDocumentLock));
    _syncDebounceRate = syncDebounceRate;
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    if(_syncDebounceRate > 0)
    {
      _syncThrottle = new Throttle<PeerId>(SendThrottledSyncMessage, _syncDebounceRate);
    }
  }

  /// <summary>
  /// Begins synchronization with a peer. Sends initial sync message.
  /// </summary>
  public async Task BeginSync(PeerId peerId)
  {
    ThrowIfDisposed();

    // Collect events to fire after releasing lock
    var deferredEvents = new List<Action>();

    await _lock.WaitAsync().ConfigureAwait(false);
    try
    {
      if(_activePeers.Contains(peerId))
      {
        return;
      }

      _activePeers.Add(peerId);

      // Initialize status as unknown
      if(!_peerStatuses.ContainsKey(peerId))
      {
        _peerStatuses[peerId] = PeerDocumentStatus.Unknown;
      }

      // Send initial sync message
      await SendSyncMessage(peerId, deferredEvents).ConfigureAwait(false);
    }
    finally
    {
      _lock.Release();
    }

    // Fire all deferred events after releasing the lock
    foreach(var eventAction in deferredEvents)
    {
      eventAction();
    }
  }

  /// <summary>
  /// Ends synchronization with a peer. Cleans up state.
  /// </summary>
  public async Task EndSync(PeerId peerId)
  {
    ThrowIfDisposed();

    await _lock.WaitAsync().ConfigureAwait(false);
    try
    {
      _activePeers.Remove(peerId);

      // Dispose sync state before removing
      if(_syncStates.TryGetValue(peerId, out var state))
      {
        state.Dispose();
      }

      _syncStates.Remove(peerId);
      _peerStatuses.Remove(peerId);
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <summary>
  /// Checks if we're actively syncing with a peer.
  /// </summary>
  public async Task<bool> HasPeer(PeerId peerId)
  {
    ThrowIfDisposed();

    await _lock.WaitAsync().ConfigureAwait(false);
    try
    {
      return _activePeers.Contains(peerId);
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <summary>
  /// Gets the current status of a peer for this document.
  /// </summary>
  public async Task<PeerDocumentStatus> GetPeerStatus(PeerId peerId)
  {
    ThrowIfDisposed();

    await _lock.WaitAsync().ConfigureAwait(false);
    try
    {
      return _peerStatuses.TryGetValue(peerId, out var status)
        ? status
        : PeerDocumentStatus.Unknown;
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <summary>
  /// Receives and routes a message to appropriate handler.
  /// </summary>
  public async Task ReceiveMessage(IRepoMessage message, PeerId senderId)
  {
    ThrowIfDisposed();

    _messagesReceived++;

    // Collect events to fire after releasing lock
    var deferredEvents = new List<Action>();

    await _lock.WaitAsync().ConfigureAwait(false);
    try
    {
      switch(message)
      {
        case SyncMessage sync:
          await ReceiveSyncMessage(sync, senderId, deferredEvents).ConfigureAwait(false);
          break;

        case RequestMessage request:
          await ReceiveRequestMessage(request, senderId, deferredEvents).ConfigureAwait(false);
          break;

        case DocumentUnavailableMessage unavail:
          ReceiveDocumentUnavailable(unavail, senderId);
          break;
        case DocumentSnapshotMessage snapshot:
          await ReceiveDocumentSnapshot(snapshot, senderId).ConfigureAwait(false);
          break;

        default:
          // Unknown message type, ignore
          break;
      }
    }
    finally
    {
      _lock.Release();
    }

    // Fire all deferred events after releasing the lock
    foreach(var eventAction in deferredEvents)
    {
      eventAction();
    }
  }

  /// <summary>
  /// Processes incoming sync message using Automerge sync protocol.
  /// </summary>
  private async Task ReceiveSyncMessage(SyncMessage message, PeerId senderId, List<Action> deferredEvents,
    bool preserveWantsStatus = false)
  {
    // Validate message data
    if(message.Data is null)
    {
      return;
    }

    // Get document (or create empty one if we don't have it yet)
    var doc = _getDocument();
    var isNewDocument = doc is null;
    if(doc is null)
    {
      // We don't have the document yet, but the peer is sending us data
      // Create an empty document to receive the sync data
      doc = AutomergeDocument.Create();
    }

    // Get sync state for this peer
    var syncState = GetOrCreateSyncState(senderId);

    var shouldRespond = true;

    // Apply sync message using Automerge protocol
    // Note: Empty messages are valid in the sync protocol (they indicate "I have nothing new")
    // Note: The current API mutates both doc and syncState in place
    if(message.Data.Length > 0)
    {
      var docLock = _getDocumentLock()
        ?? throw new InvalidOperationException("Document lock must not be null. Ensure the lock provider returns a valid SemaphoreSlim.");

      LogSyncMessageReceived(_documentId, senderId.ToString(), message.Data.Length);

      await docLock.WaitAsync().ConfigureAwait(false);
      LogLockAcquired("ReceiveSyncMessage", _documentId, senderId.ToString());
      try
      {
        doc.ReceiveSyncMessage(syncState, message.Data);
      }
      catch(Exception ex)
      {
        // Upstream automerge panics are caught by the FFI layer and surfaced as exceptions.
        // Log and continue so the process stays alive; the sync will retry on the next round.
        LogSyncError(ex, "ReceiveSyncMessage", _documentId, senderId.ToString());
        return;
      }
      finally
      {
        docLock.Release();
        LogLockReleased("ReceiveSyncMessage", _documentId, senderId.ToString());
      }

      // Notify owner that document was updated AFTER releasing the document lock
      // to prevent deadlock (NotifyRemoteChangeAsync acquires the same lock).
      // Pass senderId so the Repo can exclude the originator from re-sync.
      await _updateDocument(doc, senderId).ConfigureAwait(false);
    }
    else if(isNewDocument)
    {
      // No payload but we created a local document instance; ensure repo registers it.
      await _updateDocument(doc, senderId).ConfigureAwait(false);
      // Nothing additional to send back unless we still owe data (e.g., due to request).
      if(!preserveWantsStatus)
      {
        shouldRespond = false;
      }
    }

    // Mark peer as having the document (unless we need to preserve Wants status from RequestMessage)
    if(!preserveWantsStatus || _peerStatuses[senderId] != PeerDocumentStatus.Wants)
    {
      _peerStatuses[senderId] = PeerDocumentStatus.Has;
    }

    // Defer sync state changed event (for persistence)
    var peerMetadata = _getPeerMetadata(senderId);
    if(peerMetadata?.StorageId is not null && !peerMetadata.IsEphemeral)
    {
      var syncStateChangedArgs = new SyncStateChangedEventArgs
      {
        DocumentId = _documentIdValue, PeerId = senderId, SyncState = syncState, StorageId = peerMetadata.StorageId
      };
      deferredEvents.Add(() => SyncStateChanged?.Invoke(this, syncStateChangedArgs));
    }

    // Send response sync message immediately (immediate feedback)
    // Note: SendSyncMessage will add its own metrics event to deferredEvents
    if(shouldRespond)
    {
      await SendSyncMessage(senderId, deferredEvents).ConfigureAwait(false);
    }
  }

  /// <summary>
  /// Processes incoming request message (peer wants the document).
  /// </summary>
  private async Task ReceiveRequestMessage(RequestMessage message, PeerId senderId, List<Action> deferredEvents)
  {
    // Mark peer as wanting the document
    _peerStatuses[senderId] = PeerDocumentStatus.Wants;

    // Get document
    var doc = _getDocument();
    if(doc is null)
    {
      // We don't have it either, defer sending unavailable message
      var unavailMessage = new DocumentUnavailableMessage { DocumentId = _documentIdValue };

      var messageArgs = new SyncMessageEventArgs
      {
        DocumentId = _documentIdValue, TargetPeerId = senderId, Message = unavailMessage
      };
      deferredEvents.Add(() => MessageToSend?.Invoke(this, messageArgs));
      return;
    }

    // Process as sync message (request is just a sync with empty state)
    // Pass preserveWantsStatus=true to keep the Wants status we just set
    var syncMsg = new SyncMessage { DocumentId = message.DocumentId, Data = message.Data, SenderId = senderId };

    await ReceiveSyncMessage(syncMsg, senderId, deferredEvents, preserveWantsStatus: true).ConfigureAwait(false);
  }

  /// <summary>
  /// Processes document unavailable message from peer.
  /// </summary>
  private void ReceiveDocumentUnavailable(DocumentUnavailableMessage message, PeerId senderId)
  {
    // Mark peer as unavailable
    _peerStatuses[senderId] = PeerDocumentStatus.Unavailable;

    // Note: Unavailability checking will be handled by CollectionSynchronizer
  }

  /// <summary>
  /// Applies a full document snapshot from a peer.
  /// </summary>
  private async Task ReceiveDocumentSnapshot(DocumentSnapshotMessage message, PeerId senderId)
  {
    var doc = AutomergeDocument.Load(message.Data);
    await _updateDocument(doc, senderId).ConfigureAwait(false);
    _peerStatuses[senderId] = PeerDocumentStatus.Has;

    if(_syncStates.TryGetValue(senderId, out var existingState))
    {
      existingState.Dispose();
    }

    _syncStates[senderId] = AutomergeSyncState.Create();
  }

  /// <summary>
  /// Generates and sends a sync message to a peer.
  /// If we don't have the document, sends a RequestMessage instead.
  /// </summary>
  private async Task SendSyncMessage(PeerId peerId, List<Action> deferredEvents)
  {
    // Get document
    var doc = _getDocument();
    if(doc is null)
    {
      // We don't have the document - send a RequestMessage to request it from the peer
      // Create an empty sync state to generate the initial request
      var syncState = AutomergeSyncState.Create();
      var emptyDoc = AutomergeDocument.Create();
      var requestData = emptyDoc.GenerateSyncMessage(syncState);

      // Dispose temporary objects
      syncState.Dispose();

      // Create and defer sending the request message
      var requestMessage = new RequestMessage
      {
        DocumentId = _documentIdValue,
        Data = requestData ?? Array.Empty<byte>()
      };

      _messagesSent++;
      var requestArgs = new SyncMessageEventArgs
      {
        DocumentId = _documentIdValue,
        TargetPeerId = peerId,
        Message = requestMessage
      };
      deferredEvents.Add(() => MessageToSend?.Invoke(this, requestArgs));

      // Defer metrics (not complete - we're requesting)
      var metricsArgs = new SyncMetricsEventArgs
      {
        DocumentId = _documentIdValue,
        PeerId = peerId,
        MessagesSent = _messagesSent,
        MessagesReceived = _messagesReceived,
        IsComplete = false
      };
      deferredEvents.Add(() => MetricsUpdated?.Invoke(this, metricsArgs));
      return;
    }

    // Get sync state for this peer
    var peerSyncState = GetOrCreateSyncState(peerId);

    // Single lock scope covering both GenerateSyncMessage and the conditional Save
    // to ensure document consistency between the two operations.
    var docLock = _getDocumentLock()
      ?? throw new InvalidOperationException("Document lock must not be null. Ensure the lock provider returns a valid SemaphoreSlim.");
    byte[]? messageData;
    byte[]? snapshotData = null;

    await docLock.WaitAsync().ConfigureAwait(false);
    LogLockAcquired("SendSyncMessage", _documentId, peerId.ToString());
    try
    {
      messageData = doc.GenerateSyncMessage(peerSyncState);

      // If no sync data and peer doesn't have the document, save a full snapshot
      if((messageData is null || messageData.Length == 0)
        && (!_peerStatuses.TryGetValue(peerId, out var status) || status != PeerDocumentStatus.Has))
      {
        snapshotData = doc.Save();
      }
    }
    catch(Exception ex)
    {
      LogSyncError(ex, "GenerateSyncMessage", _documentId, peerId.ToString());
      return;
    }
    finally
    {
      docLock.Release();
      LogLockReleased("SendSyncMessage", _documentId, peerId.ToString());
    }

    if(messageData is not null && messageData.Length > 0)
    {
      LogSyncMessageSent(_documentId, peerId.ToString(), messageData.Length);
    }

    // Process results outside the document lock (but still inside _lock)
    if(messageData is null || messageData.Length == 0)
    {
      if(snapshotData is not null)
      {
        var snapshotMessage = new DocumentSnapshotMessage
        {
          DocumentId = _documentIdValue,
          Data = snapshotData
        };

        if(_syncStates.TryGetValue(peerId, out var existingState))
        {
          existingState.Dispose();
        }

        _syncStates[peerId] = AutomergeSyncState.Create();

        _messagesSent++;
        _peerStatuses[peerId] = PeerDocumentStatus.Has;

        var snapshotArgs = new SyncMessageEventArgs
        {
          DocumentId = _documentIdValue,
          TargetPeerId = peerId,
          Message = snapshotMessage
        };
        deferredEvents.Add(() => MessageToSend?.Invoke(this, snapshotArgs));

        var metricsArgsSnapshot = new SyncMetricsEventArgs
        {
          DocumentId = _documentIdValue,
          PeerId = peerId,
          MessagesSent = _messagesSent,
          MessagesReceived = _messagesReceived,
          IsComplete = true
        };
        deferredEvents.Add(() => MetricsUpdated?.Invoke(this, metricsArgsSnapshot));

        return;
      }

      var metricsArgsCompleted = new SyncMetricsEventArgs
      {
        DocumentId = _documentIdValue,
        PeerId = peerId,
        MessagesSent = _messagesSent,
        MessagesReceived = _messagesReceived,
        IsComplete = true
      };
      deferredEvents.Add(() => MetricsUpdated?.Invoke(this, metricsArgsCompleted));
      return;
    }

    var message = new SyncMessage { DocumentId = _documentIdValue, Data = messageData };

    _messagesSent++;
    _peerStatuses[peerId] = PeerDocumentStatus.Has;
    var messageArgs = new SyncMessageEventArgs { DocumentId = _documentIdValue, TargetPeerId = peerId, Message = message };
    deferredEvents.Add(() => MessageToSend?.Invoke(this, messageArgs));

    // Defer metrics (not complete if we're sending data)
    var metricsArgsIncomplete = new SyncMetricsEventArgs
    {
      DocumentId = _documentIdValue,
      PeerId = peerId,
      MessagesSent = _messagesSent,
      MessagesReceived = _messagesReceived,
      IsComplete = false
    };
    deferredEvents.Add(() => MetricsUpdated?.Invoke(this, metricsArgsIncomplete));
  }

  /// <summary>
  /// Notifies all connected peers that the document changed locally.
  /// When a sync debounce rate is configured, sync messages are deferred
  /// and coalesced per peer to reduce network traffic during rapid changes.
  /// </summary>
  public async Task NotifyLocalChange(PeerId? excludePeer = null)
  {
    ThrowIfDisposed();

    if(_syncThrottle is not null)
    {
      // Throttled path: schedule per-peer sync after debounce period
      await _lock.WaitAsync().ConfigureAwait(false);
      try
      {
        if(_activePeers.Count == 0)
        {
          return;
        }

        foreach(var peer in _activePeers)
        {
          if(excludePeer is not null && peer == excludePeer.Value)
            continue;

          _syncThrottle.Schedule(peer);
        }
      }
      finally
      {
        _lock.Release();
      }

      return;
    }

    // Immediate path: send sync messages directly (no throttle)
    var deferredEvents = new List<Action>();

    await _lock.WaitAsync().ConfigureAwait(false);
    try
    {
      if(_activePeers.Count == 0)
      {
        return;
      }

      foreach(var peer in _activePeers)
      {
        if(excludePeer is not null && peer == excludePeer.Value)
          continue;

        await SendSyncMessage(peer, deferredEvents).ConfigureAwait(false);
      }
    }
    finally
    {
      _lock.Release();
    }

    foreach(var action in deferredEvents)
    {
      action();
    }
  }

  /// <summary>
  /// Callback for the throttle: sends a sync message to a specific peer.
  /// </summary>
  async Task SendThrottledSyncMessage(PeerId peerId)
  {
    if(_disposed) return;

    var deferredEvents = new List<Action>();

    await _lock.WaitAsync().ConfigureAwait(false);
    try
    {
      if(!_activePeers.Contains(peerId))
      {
        return;
      }

      await SendSyncMessage(peerId, deferredEvents).ConfigureAwait(false);
    }
    finally
    {
      _lock.Release();
    }

    foreach(var action in deferredEvents)
    {
      action();
    }
  }

  /// <summary>
  /// Gets existing sync state for peer, or creates new one.
  /// </summary>
  private AutomergeSyncState GetOrCreateSyncState(PeerId peerId)
  {
    // Check cache first
    if(_syncStates.TryGetValue(peerId, out var cachedState))
    {
      return cachedState;
    }

    // Create new sync state
    var newState = AutomergeSyncState.Create();
    _syncStates[peerId] = newState;

    // Note: Storage loading is handled by CollectionSynchronizer on initialization
    // This method only creates in-memory state

    return newState;
  }

  /// <summary>
  /// Emits sync metrics for monitoring.
  /// </summary>
  private void EmitMetrics(PeerId peerId, bool isComplete)
  {
    MetricsUpdated?.Invoke(this,
      new SyncMetricsEventArgs
      {
        DocumentId = _documentIdValue,
        PeerId = peerId,
        MessagesSent = _messagesSent,
        MessagesReceived = _messagesReceived,
        IsComplete = isComplete
      });
  }

  private void ThrowIfDisposed()
  {
    if(_disposed)
    {
      throw new ObjectDisposedException(nameof(DocSynchronizer));
    }
  }

  public void Dispose()
  {
    if(_disposed) return;

    _disposed = true;

    _lock.Wait();
    try
    {
      // Dispose all sync states
      foreach(var state in _syncStates.Values)
      {
        state.Dispose();
      }

      _syncStates.Clear();
      _peerStatuses.Clear();
      _activePeers.Clear();
    }
    finally
    {
      _lock.Release();
    }

    _syncThrottle?.Dispose();
    _lock.Dispose();
  }

  // --- High-performance source-generated log methods ---

  [LoggerMessage(Level = LogLevel.Debug, Message = "Document lock acquired for {Operation} [DocumentId={DocumentId}, PeerId={PeerId}]")]
  partial void LogLockAcquired(string operation, string documentId, string peerId);

  [LoggerMessage(Level = LogLevel.Debug, Message = "Document lock released for {Operation} [DocumentId={DocumentId}, PeerId={PeerId}]")]
  partial void LogLockReleased(string operation, string documentId, string peerId);

  [LoggerMessage(Level = LogLevel.Debug, Message = "Sync message received [DocumentId={DocumentId}, PeerId={PeerId}, ByteLength={ByteLength}]")]
  partial void LogSyncMessageReceived(string documentId, string peerId, int byteLength);

  [LoggerMessage(Level = LogLevel.Debug, Message = "Sync message sent [DocumentId={DocumentId}, PeerId={PeerId}, ByteLength={ByteLength}]")]
  partial void LogSyncMessageSent(string documentId, string peerId, int byteLength);

  [LoggerMessage(Level = LogLevel.Error, Message = "Error during sync operation {Operation} [DocumentId={DocumentId}, PeerId={PeerId}]")]
  partial void LogSyncError(Exception exception, string operation, string documentId, string peerId);
}
