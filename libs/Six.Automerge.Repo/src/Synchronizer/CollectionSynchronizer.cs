using Microsoft.Extensions.Logging.Abstractions;
using Six.Automerge.Repository.Network;

namespace Six.Automerge.Repository.Synchronizer;

/// <summary>
/// Manages synchronization for all documents in a repository.
/// Routes messages to appropriate DocSynchronizers and enforces policies.
/// </summary>
[PublicAPI]
public sealed class CollectionSynchronizer : IDisposable
{
  readonly Dictionary<string, DocSynchronizer> _docSynchronizers = [];
  readonly HashSet<PeerId> _connectedPeers = [];
  readonly SemaphoreSlim _lock = new(1, 1);
  readonly ISet<string> _denylist;

  Func<DocumentId, PeerId, Task<bool>>? _sharePolicy;
  Func<DocumentId, PeerId, Task<bool>>? _accessPolicy;
  readonly Func<string, AutomergeDocument?> _getDocument;
  readonly Func<string, AutomergeDocument, PeerId?, Task> _updateDocument;
  readonly Func<PeerId, PeerMetadata?> _getPeerMetadata;
  readonly Action<string> _requestDocument;
  readonly Func<string, SemaphoreSlim?> _getDocumentLock;
  readonly int _syncDebounceRate;
  readonly ILoggerFactory _loggerFactory;
  readonly ILogger<CollectionSynchronizer> _logger;

  bool _isDisposed;

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
  /// Event fired when a document is unavailable from all peers.
  /// </summary>
#pragma warning disable CS0067 // Event is never used - will be used in future implementation
  public event EventHandler<DocumentUnavailableEventArgs>? DocumentUnavailable;
#pragma warning restore CS0067


  /// <summary>
  /// Creates a new CollectionSynchronizer.
  /// </summary>
  /// <param name="getDocument">Callback to retrieve a document by ID</param>
  /// <param name="updateDocument">Callback to update a document after receiving changes</param>
  /// <param name="getPeerMetadata">Callback to retrieve peer metadata</param>
  /// <param name="requestDocument">Callback to request a document (for discovery)</param>
  /// <param name="getDocumentLock">Callback to retrieve the document lock for a given document ID</param>
  /// <param name="syncDebounceRate">Debounce rate in milliseconds for sync message sending on local changes. Default: 100ms.</param>
  /// <param name="denylist">Optional set of document IDs to never sync</param>
  /// <param name="loggerFactory">Logger factory for creating per-document loggers. If null, logging is disabled.</param>
  public CollectionSynchronizer(
    Func<string, AutomergeDocument?> getDocument,
    Func<string, AutomergeDocument, PeerId?, Task> updateDocument,
    Func<PeerId, PeerMetadata?> getPeerMetadata,
    Action<string> requestDocument,
    Func<string, SemaphoreSlim?> getDocumentLock,
    int syncDebounceRate = 100,
    ISet<string>? denylist = null,
    ILoggerFactory? loggerFactory = null)
  {
    _getDocument = getDocument ?? throw new ArgumentNullException(nameof(getDocument));
    _updateDocument = updateDocument ?? throw new ArgumentNullException(nameof(updateDocument));
    _getPeerMetadata = getPeerMetadata ?? throw new ArgumentNullException(nameof(getPeerMetadata));
    _requestDocument = requestDocument ?? throw new ArgumentNullException(nameof(requestDocument));
    _getDocumentLock = getDocumentLock ?? throw new ArgumentNullException(nameof(getDocumentLock));
    _syncDebounceRate = syncDebounceRate;
    _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    _logger = _loggerFactory.CreateLogger<CollectionSynchronizer>();
    _denylist = denylist ?? new HashSet<string>();
  }


  /// <summary>
  /// Registers a document for synchronization with all connected peers.
  /// </summary>
  public async Task AddDocument(string documentId)
  {
    if(string.IsNullOrWhiteSpace(documentId))
      throw new ArgumentException("Document ID cannot be null or whitespace", nameof(documentId));

    ThrowIfDisposed();

    if(IsInDenylist(documentId)) return;

    _logger.LogInformation("Adding document {DocumentId} for synchronization", documentId);

    DocSynchronizer? docSync;
    var peersToSync = new List<PeerId>();

    await _lock.WaitAsync().ConfigureAwait(false);
    try
    {
      docSync = GetOrCreateDocSync(documentId);

      // Collect peers to sync with
      foreach(var peer in _connectedPeers)
      {
        if(await ShouldShare(documentId, peer).ConfigureAwait(false))
        {
          peersToSync.Add(peer);
        }
      }
    }
    finally
    {
      _lock.Release();
    }

    // Begin syncing outside the lock to prevent deadlocks
    foreach(var peer in peersToSync)
    {
      await docSync.BeginSync(peer).ConfigureAwait(false);
    }
  }

  /// <summary>
  /// Unregisters a document from synchronization.
  /// </summary>
  public async Task RemoveDocument(string documentId)
  {
    ThrowIfDisposed();

    await _lock.WaitAsync().ConfigureAwait(false);
    try
    {
      if(_docSynchronizers.TryGetValue(documentId, out var docSync))
      {
        docSync.Dispose();
        _docSynchronizers.Remove(documentId);
      }
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <summary>
  /// Called when a new peer connects. Begins syncing all documents with this peer.
  /// </summary>
  public async Task OnPeerConnected(PeerId peerId)
  {
    ThrowIfDisposed();

    _logger.LogInformation("Peer {PeerId} connected", peerId);

    // Collect deferred operations
    var docsToSync = new List<(string documentId, DocSynchronizer docSync)>();

    await _lock.WaitAsync().ConfigureAwait(false);
    try
    {
      if(_connectedPeers.Contains(peerId)) return;

      _connectedPeers.Add(peerId);

      // Collect documents to sync with this peer
      foreach(var (documentId, docSync) in _docSynchronizers)
      {
        if(await ShouldShare(documentId, peerId).ConfigureAwait(false))
        {
          docsToSync.Add((documentId, docSync));
        }
      }
    }
    finally
    {
      _lock.Release();
    }

    // Begin syncing outside the lock to prevent deadlocks
    foreach(var (documentId, docSync) in docsToSync)
    {
      await docSync.BeginSync(peerId).ConfigureAwait(false);
    }
  }

  /// <summary>
  /// Called when a peer disconnects. Cleans up sync state for this peer.
  /// </summary>
  public async Task OnPeerDisconnected(PeerId peerId)
  {
    ThrowIfDisposed();

    _logger.LogInformation("Peer {PeerId} disconnected", peerId);

    await _lock.WaitAsync().ConfigureAwait(false);
    try
    {
      _connectedPeers.Remove(peerId);

      // End sync for all documents
      foreach(var docSync in _docSynchronizers.Values)
      {
        await docSync.EndSync(peerId).ConfigureAwait(false);
      }
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <summary>
  /// Routes incoming message to appropriate DocSynchronizer.
  /// Handles document discovery for unknown documents.
  /// </summary>
  public async Task OnMessageReceived(IRepoMessage message)
  {
    ThrowIfDisposed();

    if(message is null) return;
    if(message.SenderId is null) return; // Must have sender
    var documentIdValue = message.DocumentId?.Value;
    if(documentIdValue is null) return; // Must have document ID

    var documentId = documentIdValue;
    var senderId = message.SenderId.Value;

    // Check denylist
    if(IsInDenylist(documentId)) return;

    // Check access policy
    if(!await CanAccess(documentId, senderId).ConfigureAwait(false))
    {
      // Access denied, don't process message
      return;
    }

    DocSynchronizer? docSync;
    bool needsBeginSync = false;

    await _lock.WaitAsync().ConfigureAwait(false);
    try
    {
      // Get or create doc synchronizer
      docSync = GetOrCreateDocSync(documentId);

      // If this is a new document we discovered, request it
      var doc = _getDocument(documentId);
      if(doc is null && !await docSync.HasPeer(senderId).ConfigureAwait(false))
      {
        // We don't have this document, trigger discovery
        _requestDocument(documentId);
      }

      // Check if peer needs to be tracked
      if(!await docSync.HasPeer(senderId).ConfigureAwait(false))
      {
        needsBeginSync = true;
      }
    }
    finally
    {
      _lock.Release();
    }

    // Begin sync outside the lock to prevent deadlocks
    if(needsBeginSync)
    {
      await docSync.BeginSync(senderId).ConfigureAwait(false);
    }

    // Process message (outside lock to prevent deadlocks)
    await docSync.ReceiveMessage(message, senderId).ConfigureAwait(false);
  }

  /// <summary>
  /// Notifies the synchronizer that a local document change occurred.
  /// </summary>
  /// <param name="documentId">The document identifier.</param>
  /// <param name="excludePeer">Optional peer to exclude from sync notifications (the originator of the change).</param>
  public async Task NotifyDocumentChanged(string documentId, PeerId? excludePeer = null)
  {
    ThrowIfDisposed();

    DocSynchronizer? docSync;

    await _lock.WaitAsync().ConfigureAwait(false);
    try
    {
      if(!_docSynchronizers.TryGetValue(documentId, out docSync))
      {
        return;
      }
    }
    finally
    {
      _lock.Release();
    }

    await docSync.NotifyLocalChange(excludePeer).ConfigureAwait(false);
  }

  /// <summary>
  /// Sets the share policy (which documents to proactively share with which peers).
  /// </summary>
  public void SetSharePolicy(Func<DocumentId, PeerId, Task<bool>> policy)
  {
    _sharePolicy = policy;
  }

  /// <summary>
  /// Sets the access policy (which peers can access which documents).
  /// </summary>
  public void SetAccessPolicy(Func<DocumentId, PeerId, Task<bool>> policy)
  {
    _accessPolicy = policy;
  }

  /// <summary>
  /// Gets existing DocSynchronizer or creates new one.
  /// Wires up events on creation.
  /// </summary>
  private DocSynchronizer GetOrCreateDocSync(string documentId)
  {
    if(_docSynchronizers.TryGetValue(documentId, out var existing))
    {
      return existing;
    }

    // Create new doc synchronizer
    var docSync = new DocSynchronizer(
      documentId,
      () => _getDocument(documentId),
      (updatedDoc, senderId) => _updateDocument(documentId, updatedDoc, senderId),
      _getPeerMetadata,
      () => _getDocumentLock(documentId),
      _loggerFactory.CreateLogger<DocSynchronizer>(),
      _syncDebounceRate);


    // Wire up events
    docSync.MessageToSend += (sender, args) => MessageToSend?.Invoke(this, args);
    docSync.SyncStateChanged += (sender, args) => SyncStateChanged?.Invoke(this, args);
    docSync.MetricsUpdated += (sender, args) => MetricsUpdated?.Invoke(this, args);

    _docSynchronizers[documentId] = docSync;
    return docSync;
  }

  /// <summary>
  /// Checks if document should be proactively shared with peer.
  /// Default: share all documents.
  /// </summary>
  private async Task<bool> ShouldShare(string documentId, PeerId peerId)
  {
    if(_sharePolicy is null) return true;

    try
    {
      var docId = DocumentId.Parse(documentId);
      return await _sharePolicy(docId, peerId).ConfigureAwait(false);
    }
    catch
    {
      // Policy failure, default to not sharing
      return false;
    }
  }

  /// <summary>
  /// Checks if peer can access document.
  /// Default: allow all access.
  /// </summary>
  private async Task<bool> CanAccess(string documentId, PeerId peerId)
  {
    if(_accessPolicy is null) return true;

    try
    {
      var docId = DocumentId.Parse(documentId);
      return await _accessPolicy(docId, peerId).ConfigureAwait(false);
    }
    catch
    {
      // Policy failure, default to denying access
      return false;
    }
  }

  /// <summary>
  /// Checks if document is in denylist.
  /// </summary>
  private bool IsInDenylist(string documentId)
  {
    return _denylist.Contains(documentId);
  }

  private void ThrowIfDisposed()
  {
    if(_isDisposed)
    {
      throw new ObjectDisposedException(nameof(CollectionSynchronizer));
    }
  }

  public void Dispose()
  {
    if(_isDisposed) return;

    _isDisposed = true;

    _lock.Wait();
    try
    {
      // Dispose all doc synchronizers
      foreach(var docSync in _docSynchronizers.Values)
      {
        docSync.Dispose();
      }

      _docSynchronizers.Clear();
      _connectedPeers.Clear();
    }
    finally
    {
      _lock.Release();
    }

    _lock.Dispose();
  }
}
