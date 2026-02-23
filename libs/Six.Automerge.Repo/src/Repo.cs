using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Six.Automerge;
using Six.Automerge.Repository.Network;
using Six.Automerge.Repository.Storage;
using Six.Automerge.Repository.Synchronizer;

namespace Six.Automerge.Repository;

/// <summary>
/// Central repository for managing Automerge documents with persistence and sync.
/// </summary>
/// <remarks>
/// <para>
/// The Repo is the main entry point of this library. To construct a Repo you will need
/// an IStorageAdapter and one or more INetworkAdapters. Once you have a Repo you can
/// use it to obtain DocHandles.
/// </para>
/// <para>
/// This is a C# port of the TypeScript automerge-repo Repo class, maintaining API compatibility.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class Repo : IDisposable
{
  // Subsystems
  readonly StorageSubsystem? _storage;
  readonly NetworkSubsystem _network;
  readonly CollectionSynchronizer _synchronizer;

  // Configuration
  readonly PeerId _peerId;
  readonly bool _isEphemeral;
  readonly int _saveDebounceRate;
  readonly ShareConfig _shareConfig;
  readonly Func<IReadOnlyList<string>, Task<byte[]>>? _idFactory;
  readonly ILoggerFactory _loggerFactory;
  readonly ILogger<Repo> _logger;

  // Handle management
  readonly Dictionary<string, IDocHandle> _handleCache = [];
  readonly Dictionary<string, Task<IDocHandle>> _findInProgress = [];
  readonly Dictionary<string, SemaphoreSlim> _documentLockFallbacks = [];
  readonly SemaphoreSlim _handleLock = new(1, 1);

  // Save throttling
  readonly Dictionary<string, CancellationTokenSource> _pendingSaves = [];
  readonly SemaphoreSlim _saveLock = new(1, 1);

  bool _isDisposed;


  #region Events

  /// <summary>
  /// Raised when a document is loaded and ready.
  /// </summary>
  public event EventHandler<DocumentEventArgs>? DocumentLoaded;

  /// <summary>
  /// Raised when a document is deleted.
  /// </summary>
  public event EventHandler<DocumentDeletedEventArgs>? DocumentDeleted;

  /// <summary>
  /// Raised when a document is unavailable from all peers.
  /// </summary>
  public event EventHandler<DocumentUnavailableEventArgs>? DocumentUnavailable;

  /// <summary>
  /// Raised when a peer connects.
  /// </summary>
  public event EventHandler<PeerEventArgs>? PeerConnected;

  /// <summary>
  /// Raised when a peer disconnects.
  /// </summary>
  public event EventHandler<PeerEventArgs>? PeerDisconnected;

  #endregion

  #region Properties

  /// <summary>
  /// Gets the peer ID of this repository.
  /// </summary>
  public PeerId PeerId => _peerId;

  /// <summary>
  /// Gets the list of currently connected peers.
  /// </summary>
  public IReadOnlyList<PeerId> Peers => _network.Peers;

  /// <summary>
  /// Gets a read-only snapshot of all cached handles.
  /// </summary>
  public async Task<IReadOnlyDictionary<string, IDocHandle>> GetHandlesAsync()
  {
    await _handleLock.WaitAsync().ConfigureAwait(false);
    try
    {
      return new ReadOnlyDictionary<string, IDocHandle>(
        new Dictionary<string, IDocHandle>(_handleCache));
    }
    finally
    {
      _handleLock.Release();
    }
  }

  #endregion


  /// <summary>
  /// Initializes a new repository with the specified configuration.
  /// </summary>
  /// <param name="config">Configuration for the repository.</param>
  /// <exception cref="ArgumentNullException">Thrown if config is null.</exception>
  public Repo(RepoConfig config)
  {
    if(config is null)
      throw new ArgumentNullException(nameof(config));

    // Initialize identity
    _peerId = config.PeerId ?? PeerId.Generate();
    _isEphemeral = config.IsEphemeral;
    _saveDebounceRate = config.SaveDebounceRate;
    _shareConfig = config.ShareConfig ?? ShareConfig.AllowAll;
    _idFactory = config.IdFactory;
    _loggerFactory = config.LoggerFactory ?? NullLoggerFactory.Instance;
    _logger = _loggerFactory.CreateLogger<Repo>();

    // Initialize storage subsystem
    if(config.Storage is not null)
    {
      _storage = new StorageSubsystem(config.Storage);
      _storage.DocumentLoaded += OnStorageDocumentLoaded;
      _storage.DocumentSaved += OnStorageDocumentSaved;
    }

    // Initialize network subsystem
    _network = new NetworkSubsystem(config.NetworkAdapters);
    _network.PeerCandidate += OnNetworkPeerCandidate;
    _network.PeerDisconnected += OnNetworkPeerDisconnected;
    _network.MessageReceived += OnNetworkMessageReceived;

    // Initialize synchronizer
    _synchronizer = new CollectionSynchronizer(
      GetDocument,
      UpdateDocumentAsync,
      GetPeerMetadata,
      RequestDocument,
      GetDocumentLock,
      config.SyncDebounceRate,
      config.Denylist,
      config.LoggerFactory);

    _synchronizer.MessageToSend += OnSynchronizerMessage;
    _synchronizer.SyncStateChanged += OnSynchronizerSyncStateChanged;
    _synchronizer.DocumentUnavailable += OnSynchronizerDocumentUnavailable;

    // Set share and access policies
    if(_shareConfig.Announce is not null)
    {
      _synchronizer.SetSharePolicy(AdaptAnnouncePolicyAsync);
    }

    if(_shareConfig.Access is not null)
    {
      _synchronizer.SetAccessPolicy(AdaptAccessPolicyAsync);
    }
  }


  /// <summary>
  /// Disposes the repository and releases all resources.
  /// </summary>
  public void Dispose()
  {
    if(_isDisposed) return;

    _isDisposed = true;

    // Cancel all pending saves
    _saveLock.Wait();
    try
    {
      foreach(var cts in _pendingSaves.Values)
      {
        cts.Cancel();
        cts.Dispose();
      }

      _pendingSaves.Clear();
    }
    finally
    {
      _saveLock.Release();
    }

    // Dispose synchronizer
    _synchronizer.Dispose();

    // Dispose network subsystem
    _network.Dispose();

    // Dispose storage subsystem
    _storage?.Dispose();

    // Dispose all handles

    _handleLock.Wait();
    try
    {
      foreach(var handle in _handleCache.Values)
      {
        handle.Dispose();
      }

      _handleCache.Clear();

      // Dispose fallback document locks created for document discovery
      foreach(var fallbackLock in _documentLockFallbacks.Values)
      {
        fallbackLock.Dispose();
      }

      _documentLockFallbacks.Clear();
    }
    finally
    {
      _handleLock.Release();
    }

    // Dispose locks
    _handleLock.Dispose();
    _saveLock.Dispose();
  }


  // Policy adapter: Announce(peerId, docId?) → SharePolicy(docId, peerId)
  async Task<bool> AdaptAnnouncePolicyAsync(DocumentId documentId, PeerId peerId)
  {
    if(_shareConfig.Announce is null) return true;
    return await _shareConfig.Announce(peerId, documentId).ConfigureAwait(false);
  }

  // Policy adapter: Access(peerId, docId) → AccessPolicy(docId, peerId)
  async Task<bool> AdaptAccessPolicyAsync(DocumentId documentId, PeerId peerId)
  {
    if(_shareConfig.Access is null) return true;
    return await _shareConfig.Access(peerId, documentId).ConfigureAwait(false);
  }


  #region Document Lifecycle - Create

  /// <summary>
  /// Creates a new document with optional initial value.
  /// Uses default document ID generation (not the idFactory).
  /// For custom ID generation via idFactory, use CreateAsync{T}().
  /// </summary>
  /// <typeparam name="T">The document content type.</typeparam>
  /// <param name="initialValue">Optional initial value for the document.</param>
  /// <returns>A task that resolves to a handle to the newly created document.</returns>
  public async Task<DocHandle<T>> Create<T>(T? initialValue = default)
  {
    ThrowIfDisposed();

    // Generate document ID using default generator (not idFactory)
    // idFactory is async, so it's handled in CreateAsync() instead
    var documentId = DocumentId.Generate();

    // Validate document ID
    if(!DocumentId.IsValid(documentId))
    {
      throw new ArgumentException($"Invalid document ID: {documentId}");
    }

    DocHandle<T> handle;

    await _handleLock.WaitAsync().ConfigureAwait(false);
    try
    {
      // Check if already exists
      if(_handleCache.ContainsKey(documentId))
      {
        throw new InvalidOperationException($"Document already exists: {documentId}");
      }

      // Create document
      var doc = AutomergeDocument.Create();

      // Create handle
      handle = new DocHandle<T>(
        documentId,
        new DocHandleOptions<T> { IsNew = true, ViewFactory = CreateViewFactory<T>() },
        doc,
        CreateDocHandleLogger<T>());

      // Cache handle
      _handleCache[documentId] = handle;
    }
    finally
    {
      _handleLock.Release();
    }

    // Register outside the lock to prevent deadlocks
    // (RegisterHandleAsync → synchronizer.AddDocument → docSync.BeginSync → GetDocument callback → _handleLock)
    await RegisterHandleAsync(documentId, handle).ConfigureAwait(false);

    // Emit event
    DocumentLoaded?.Invoke(this,
                           new DocumentEventArgs { DocumentId = documentId, Handle = handle });

    return handle;
  }

  /// <summary>
  /// Creates a new document asynchronously with optional initial value.
  /// Uses idFactory for document ID generation if configured.
  /// This mirrors TypeScript's experimental Repo.create2() method.
  /// </summary>
  /// <typeparam name="T">The document content type.</typeparam>
  /// <param name="initialValue">Optional initial value for the document.</param>
  /// <returns>A task that resolves to a handle to the newly created document.</returns>
  public async Task<DocHandle<T>> CreateAsync<T>(T? initialValue = default)
  {
    ThrowIfDisposed();

    // Generate document ID
    DocumentId documentId;
    if(_idFactory is not null)
    {
      // Use idFactory for ID generation
      var initialDoc = AutomergeDocument.Create();
      var heads = initialDoc.GetHeads();
      var binaryId = await _idFactory(heads).ConfigureAwait(false);
      documentId = DocumentId.FromBinary(binaryId);
    }
    else
    {
      // Fall back to default ID generation
      documentId = DocumentId.Generate();
    }

    // Validate document ID
    if(!DocumentId.IsValid(documentId))
    {
      throw new ArgumentException($"Invalid document ID: {documentId}");
    }

    DocHandle<T> handle;

    await _handleLock.WaitAsync().ConfigureAwait(false);
    try
    {
      // Check if already exists
      if(_handleCache.ContainsKey(documentId))
      {
        throw new InvalidOperationException($"Document already exists: {documentId}");
      }

      // Create document
      var doc = AutomergeDocument.Create();

      // Create handle
      handle = new DocHandle<T>(
        documentId,
        new DocHandleOptions<T> { IsNew = true, ViewFactory = CreateViewFactory<T>() },
        doc,
        CreateDocHandleLogger<T>());

      // Cache handle
      _handleCache[documentId] = handle;
    }
    finally
    {
      _handleLock.Release();
    }

    // Register outside the lock to prevent deadlocks
    await RegisterHandleAsync(documentId, handle).ConfigureAwait(false);

    // Emit event
    DocumentLoaded?.Invoke(this,
                           new DocumentEventArgs { DocumentId = documentId, Handle = handle });

    return handle;
  }

  /// <summary>
  /// Creates a new document with a specific document ID and optional initial value.
  /// </summary>
  /// <typeparam name="T">The document content type.</typeparam>
  /// <param name="documentId">The document ID to use for the new document.</param>
  /// <param name="initialValue">Optional initial value for the document.</param>
  /// <returns>A task that resolves to a handle to the newly created document.</returns>
  /// <exception cref="ArgumentException">Thrown if document ID is invalid.</exception>
  /// <exception cref="InvalidOperationException">Thrown if a document with this ID already exists.</exception>
  public async Task<DocHandle<T>> Create<T>(string documentId, T? initialValue = default)
  {
    ThrowIfDisposed();

    if(string.IsNullOrWhiteSpace(documentId))
      throw new ArgumentException("Document ID cannot be null or whitespace", nameof(documentId));

    // Validate document ID
    if(!DocumentId.IsValid(documentId))
    {
      throw new ArgumentException($"Invalid document ID: {documentId}");
    }

    var documentIdValue = DocumentId.Parse(documentId);

    DocHandle<T> handle;

    await _handleLock.WaitAsync().ConfigureAwait(false);
    try
    {
      // Check if already exists
      if(_handleCache.ContainsKey(documentId))
      {
        throw new InvalidOperationException($"Document already exists: {documentId}");
      }

      // Create document
      var doc = AutomergeDocument.Create();

      // Create handle
      handle = new DocHandle<T>(
        documentId,
        new DocHandleOptions<T> { IsNew = true, ViewFactory = CreateViewFactory<T>() },
        doc,
        CreateDocHandleLogger<T>());

      // Cache handle
      _handleCache[documentId] = handle;
    }
    finally
    {
      _handleLock.Release();
    }

    // Register outside the lock to prevent deadlocks
    await RegisterHandleAsync(documentId, handle).ConfigureAwait(false);

    // Emit event
    DocumentLoaded?.Invoke(this,
                           new DocumentEventArgs { DocumentId = documentIdValue, Handle = handle });

    return handle;
  }

  #endregion

  #region Document Lifecycle - FindAsync

  /// <summary>
  /// Finds an existing document, loading from storage or requesting from network.
  /// </summary>
  /// <typeparam name="T">The document content type.</typeparam>
  /// <param name="documentId">The document ID to find.</param>
  /// <param name="timeout">Optional timeout for loading. Default: 10 seconds.</param>
  /// <returns>
  /// A <see cref="DocHandle{T}"/> if the document exists or was loaded successfully, or null if the document
  /// does not exist and could not be loaded from the network within the timeout period.
  /// </returns>
  /// <exception cref="ArgumentException">Thrown if document ID is invalid.</exception>
  public async Task<DocHandle<T>?> FindAsync<T>(string documentId, TimeSpan? timeout = null)
  {
    if(string.IsNullOrWhiteSpace(documentId))
      throw new ArgumentException("Document ID cannot be null or whitespace", nameof(documentId));

    if(!DocumentId.IsValid(documentId))
      throw new ArgumentException($"Invalid document ID: {documentId}", nameof(documentId));

    var documentIdValue = DocumentId.Parse(documentId);

    ThrowIfDisposed();

    timeout ??= TimeSpan.FromSeconds(10);

    await _handleLock.WaitAsync().ConfigureAwait(false);
    bool lockHeld = true;
    Task<IDocHandle>? findTask;
    try
    {
      // Check cache first
      if(_handleCache.TryGetValue(documentId, out var cachedHandle))
      {
        return (DocHandle<T>)cachedHandle;
      }

      // Check if find already in progress
      if(_findInProgress.TryGetValue(documentId, out findTask))
      {
        // Release lock before awaiting another find to avoid holding it long-term
        _handleLock.Release();
        lockHeld = false;
        var result = await findTask.ConfigureAwait(false);
        return (DocHandle<T>)result;
      }

      // Start new find operation
      var tcs = new TaskCompletionSource<IDocHandle>();
      _findInProgress[documentId] = tcs.Task;
      findTask = tcs.Task;
    }
    finally
    {
      if(lockHeld)
        _handleLock.Release();
    }

    try
    {
      // Attempt to load from storage first
      AutomergeDocument? doc = null;
      if(_storage is not null)
      {
        doc = await _storage.LoadDoc(documentId).ConfigureAwait(false);
      }

      // Create handle (without document - we'll initialize explicitly)
      var handle = new DocHandle<T>(
        documentId,
        new DocHandleOptions<T> { IsNew = false, ViewFactory = CreateViewFactory<T>(), Timeout = timeout.Value },
        document: null,
        logger: CreateDocHandleLogger<T>());

      // Cache handle
      await _handleLock.WaitAsync().ConfigureAwait(false);
      try
      {
        _handleCache[documentId] = handle;
        _findInProgress.Remove(documentId);

        // Complete the TaskCompletionSource
        if(findTask is Task<IDocHandle> task &&
           task.AsyncState is TaskCompletionSource<IDocHandle> taskSource)
        {
          taskSource.SetResult(handle);
        }
      }
      finally
      {
        _handleLock.Release();
      }

      // Begin lifecycle: Idle → Loading
      handle.Begin();

      // Split into two paths based on whether a document was loaded from storage
      if(doc is not null)
      {
        // PATH A: Document found in storage
        // Initialize the handle with the loaded document
        handle.Initialize(doc, isNewDocument: false);
        handle.DoneLoading(); // Loading → Ready

        // Now register with subsystems
        await RegisterHandleAsync(documentId, handle).ConfigureAwait(false);

        // Emit loaded event
        DocumentLoaded?.Invoke(this,
                               new DocumentEventArgs { DocumentId = documentIdValue, Handle = handle });

        return handle; // Already ready, return immediately
      }
      else
      {
        // PATH B: Document must be loaded from network (or created if unavailable)
        // Wait for network to be ready before requesting
        await _network.WhenReady().ConfigureAwait(false);

        // Transition to requesting state: Loading → Requesting
        handle.Request();

        // Now register with subsystems (synchronizer sees Requesting state)
        await RegisterHandleAsync(documentId, handle).ConfigureAwait(false);

        // Wait for the handle to be ready (or timeout)
        using var cts = new CancellationTokenSource(timeout.Value);
        try
        {
          await handle.WhenReadyAsync(cts.Token).ConfigureAwait(false);
          return handle; // Document loaded successfully from network
        }
        catch(OperationCanceledException)
        {
          // Timeout: document not available from network
          // Return null to indicate document doesn't exist
          // Caller should use Create() to create a new document
          return null;
        }
      }
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "FindAsync failed [DocumentId={DocumentId}]", documentId);
      // Clean up on error
      await _handleLock.WaitAsync().ConfigureAwait(false);
      try
      {
        _findInProgress.Remove(documentId);
      }
      finally
      {
        _handleLock.Release();
      }

      throw;
    }
  }

  #endregion

  #region Document Lifecycle - DeleteAsync, CloneAsync, ImportAsync, ExportAsync

  /// <summary>
  /// Deletes a document from the repository.
  /// </summary>
  /// <param name="documentId">The document ID to delete.</param>
  public async Task DeleteAsync(string documentId)
  {
    if(string.IsNullOrWhiteSpace(documentId))
      throw new ArgumentException("Document ID cannot be null or whitespace", nameof(documentId));

    ThrowIfDisposed();

    IDocHandle? handle;

    await _handleLock.WaitAsync().ConfigureAwait(false);
    try
    {
      if(!_handleCache.TryGetValue(documentId, out handle))
      {
        return;
      }

      _handleCache.Remove(documentId);
    }
    finally
    {
      _handleLock.Release();
    }

    // Unregister from subsystems
    await _synchronizer.RemoveDocument(documentId).ConfigureAwait(false);

    // Mark handle as deleted
    handle.Delete();

    // Emit event
    DocumentDeleted?.Invoke(this, new DocumentDeletedEventArgs { DocumentId = handle.DocumentId });

    // Fire-and-forget storage deletion to maintain sync API
    if(_storage is not null)
    {
      _ = Task.Run(async () =>
      {
        try
        {
          await _storage.RemoveDoc(documentId).ConfigureAwait(false);
        }
        catch(Exception ex)
        {
          _logger.LogError(ex, "DeleteAsync: storage removal failed [DocumentId={DocumentId}]", documentId);
        }
      });
    }
  }

  /// <summary>
  /// Creates a new document as a clone of an existing document.
  /// </summary>
  /// <typeparam name="T">The document content type.</typeparam>
  /// <param name="source">The source handle to clone.</param>
  /// <returns>A task that resolves to a handle to the cloned document.</returns>
  public async Task<DocHandle<T>> CloneAsync<T>(DocHandle<T> source)
  {
    if(source is null)
      throw new ArgumentNullException(nameof(source));

    ThrowIfDisposed();

    // Generate new document ID
    var newDocumentId = _idFactory is not null
                          ? DocumentId.FromBinary(await _idFactory(Array.Empty<string>()).ConfigureAwait(false))
                          : DocumentId.Generate();

    // CloneAsync the underlying document
    var sourceDocHandle = source;
    var sourceDoc = sourceDocHandle.Doc();
    var clonedDocBytes = (sourceDoc as AutomergeDocument)!.Save();
    var clonedDoc = AutomergeDocument.Load(clonedDocBytes);

    // Create new handle
    var handle = new DocHandle<T>(
      newDocumentId,
      new DocHandleOptions<T> { IsNew = true, ViewFactory = CreateViewFactory<T>() },
      clonedDoc,
      CreateDocHandleLogger<T>());

    await _handleLock.WaitAsync().ConfigureAwait(false);
    try
    {
      _handleCache[newDocumentId] = handle;
    }
    finally
    {
      _handleLock.Release();
    }

    // Register outside the lock to prevent deadlocks
    await RegisterHandleAsync(newDocumentId, handle).ConfigureAwait(false);

    // Emit event
    DocumentLoaded?.Invoke(this,
                           new DocumentEventArgs { DocumentId = newDocumentId, Handle = handle });

    return handle;
  }

  /// <summary>
  /// Exports a document as binary data.
  /// </summary>
  /// <param name="documentId">The document ID to export.</param>
  /// <returns>The binary representation of the document, or null if not found.</returns>
  public async Task<byte[]?> ExportAsync(string documentId)
  {
    if(string.IsNullOrWhiteSpace(documentId))
      throw new ArgumentException("Document ID cannot be null or whitespace", nameof(documentId));

    ThrowIfDisposed();

    // Try to get from cache first
    await _handleLock.WaitAsync().ConfigureAwait(false);
    IDocHandle? handle;
    try
    {
      if(!_handleCache.TryGetValue(documentId, out handle))
      {
        // Not in cache, try loading from storage
        if(_storage is not null)
        {
          var doc = await _storage.LoadDoc(documentId).ConfigureAwait(false);
          if(doc is not null)
          {
            return doc.Save();
          }
        }

        return null;
      }
    }
    finally
    {
      _handleLock.Release();
    }

    // ExportAsync from handle
    var document = handle.GetDocument();
    return document!.Save();
  }

  /// <summary>
  /// Imports a document from binary data.
  /// </summary>
  /// <typeparam name="T">The document content type.</typeparam>
  /// <param name="data">The binary data to import.</param>
  /// <param name="documentId">Optional document ID. If null, a new ID is generated.</param>
  /// <returns>A task that resolves to a handle to the imported document.</returns>
  public async Task<DocHandle<T>> ImportAsync<T>(byte[] data, string? documentId = null)
  {
    if(data is null || data.Length == 0)
      throw new ArgumentException("Data cannot be null or empty", nameof(data));

    ThrowIfDisposed();

    // Load document from binary
    var doc = AutomergeDocument.Load(data);

    // Generate or validate document ID
    if(documentId is null)
    {
      // Extract ID from document heads
      var heads = doc.GetHeads();
      documentId = _idFactory is not null
                     ? DocumentId.FromBinary(await _idFactory(heads).ConfigureAwait(false))
                     : DocumentId.Generate();
    }
    else if(!DocumentId.IsValid(documentId))
    {
      throw new ArgumentException($"Invalid document ID: {documentId}", nameof(documentId));
    }

    // Create handle
    var handle = new DocHandle<T>(
      documentId,
      new DocHandleOptions<T> { IsNew = false, ViewFactory = CreateViewFactory<T>() },
      doc,
      CreateDocHandleLogger<T>());

    await _handleLock.WaitAsync().ConfigureAwait(false);
    try
    {
      // Check if already exists
      if(_handleCache.ContainsKey(documentId))
      {
        throw new InvalidOperationException($"Document already exists: {documentId}");
      }

      _handleCache[documentId] = handle;
    }
    finally
    {
      _handleLock.Release();
    }

    // Register outside the lock to prevent deadlocks
    await RegisterHandleAsync(documentId, handle).ConfigureAwait(false);

    // Emit event
    DocumentLoaded?.Invoke(this,
                           new DocumentEventArgs { DocumentId = DocumentId.Parse(documentId), Handle = handle });

    return handle;
  }

  #endregion

  #region Subsystem Wiring

  /// <summary>
  /// Registers a handle with all subsystems and wires up events.
  /// </summary>
  async Task RegisterHandleAsync<T>(string documentId, DocHandle<T> handle)
  {
    // Wire up handle events
    handle.HeadsChanged += (sender, args) => OnHandleHeadsChangedAsync(documentId, args);
    handle.Deleted += (sender, args) => OnHandleDeletedAsync(documentId);
    handle.EphemeralMessageOutbound += (sender, args) => OnHandleEphemeralMessage(documentId, args);

    // Register with synchronizer
    await _synchronizer.AddDocument(documentId).ConfigureAwait(false);
  }

  /// <summary>
  /// Handles document changes, triggering throttled save.
  /// </summary>
  async void OnHandleHeadsChangedAsync<T>(string documentId, DocHandleHeadsChangedEventArgs<T> args)
  {
    try
    {
      // Trigger throttled save
      await ScheduleSaveAsync(documentId).ConfigureAwait(false);

      await _synchronizer.NotifyDocumentChanged(documentId, args.OriginPeerId).ConfigureAwait(false);
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "OnHandleHeadsChangedAsync failed [DocumentId={DocumentId}]", documentId);
    }
  }

  /// <summary>
  /// Handles document deletion.
  /// </summary>
  async void OnHandleDeletedAsync(string documentId)
  {
    // Remove from cache
    await _handleLock.WaitAsync().ConfigureAwait(false);
    try
    {
      _handleCache.Remove(documentId);
    }
    finally
    {
      _handleLock.Release();
    }

    // Unregister from synchronizer
    await _synchronizer.RemoveDocument(documentId).ConfigureAwait(false);
  }

  /// <summary>
  /// Handles outbound ephemeral messages.
  /// </summary>
  static void OnHandleEphemeralMessage<T>(string documentId, DocHandleEphemeralMessageEventArgs<T> args)
  {
    // Forward to network - this would need proper message construction
    // For now, we'll skip as ephemeral messages need more infrastructure
  }

  #endregion

  #region Throttled Save

  /// <summary>
  /// Schedules a throttled save for a document.
  /// </summary>
  async Task ScheduleSaveAsync(string documentId)
  {
    if(_storage is null) return;

    await _saveLock.WaitAsync().ConfigureAwait(false);
    try
    {
      // Cancel existing pending save
      if(_pendingSaves.TryGetValue(documentId, out var existingCts))
      {
        await existingCts.CancelAsync();
        existingCts.Dispose();
      }

      // Schedule new save after debounce period
      var cts = new CancellationTokenSource();
      _pendingSaves[documentId] = cts;

      _ = Task.Delay(_saveDebounceRate, cts.Token)
        .ContinueWith(async _ =>
        {
          if(cts.IsCancellationRequested) return;

          await PerformSaveAsync(documentId);

          await _saveLock.WaitAsync(cts.Token);
          try
          {
            _pendingSaves.Remove(documentId);
            cts.Dispose();
          }
          finally
          {
            _saveLock.Release();
          }
        }, TaskScheduler.Default);
    }
    finally
    {
      _saveLock.Release();
    }
  }

  /// <summary>
  /// Performs the actual save operation.
  /// </summary>
  async Task PerformSaveAsync(string documentId)
  {
    if(_storage is null) return;

    await _handleLock.WaitAsync().ConfigureAwait(false);
    IDocHandle? handle;
    try
    {
      if(!_handleCache.TryGetValue(documentId, out handle))
      {
        return; // Document no longer exists
      }
    }
    finally
    {
      _handleLock.Release();
    }

    var doc = handle.GetDocument();
    if(doc is not null)
    {
      await _storage.SaveDoc(documentId, doc).ConfigureAwait(false);
    }
  }

  #endregion

  #region Subsystem Event Handlers

  async void OnNetworkPeerCandidate(object? sender, PeerCandidateEventArgs e)
  {
    // Notify synchronizer
    await _synchronizer.OnPeerConnected(e.PeerId).ConfigureAwait(false);

    // Emit repo event
    PeerConnected?.Invoke(this, new PeerEventArgs { PeerId = e.PeerId, Metadata = e.Metadata });
  }

  async void OnNetworkPeerDisconnected(object? sender, PeerDisconnectedEventArgs e)
  {
    // Notify synchronizer
    await _synchronizer.OnPeerDisconnected(e.PeerId).ConfigureAwait(false);

    // Emit repo event
    PeerDisconnected?.Invoke(this, new PeerEventArgs { PeerId = e.PeerId, Metadata = null });
  }

  async void OnNetworkMessageReceived(object? sender, MessageReceivedEventArgs e)
  {
    // Forward to synchronizer
    await _synchronizer.OnMessageReceived(e.Message).ConfigureAwait(false);
  }

  void OnSynchronizerMessage(object? sender, SyncMessageEventArgs e)
  {
    // Stamp our PeerId as sender before forwarding to network.
    // DocSynchronizer creates messages without SenderId (it doesn't know the Repo identity).
    var message = e.Message.SenderId is null
                    ? e.Message.WithSender(_peerId)
                    : e.Message;

    // Thread TargetPeerId from the synchronizer so the network layer can route
    // the message to the specific peer instead of broadcasting to all peers.
    if(message.TargetPeerId is null)
    {
      message = message.WithTarget(e.TargetPeerId);
    }

    _network.Send(message);
  }

  void OnSynchronizerSyncStateChanged(object? sender, SyncStateChangedEventArgs e)
  {
    if(_storage is null || e.StorageId is null) return;

    // Trigger throttled sync state save
    _ = Task.Run(async () =>
    {
      try
      {
        await Task.Delay(_saveDebounceRate).ConfigureAwait(false);
        await _storage.SaveSyncState(e.DocumentId.Value, e.StorageId.Value, e.SyncState).ConfigureAwait(false);
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "OnSynchronizerSyncStateChanged: sync state save failed [DocumentId={DocumentId}]",
                         e.DocumentId.Value);
      }
    });
  }

  void OnSynchronizerDocumentUnavailable(object? sender,
                                         Six.Automerge.Repository.Synchronizer.DocumentUnavailableEventArgs e)
  {
    // Emit repo event
    DocumentUnavailable?.Invoke(this, new DocumentUnavailableEventArgs { DocumentId = e.DocumentId });
  }

  void OnStorageDocumentLoaded(object? sender, DocumentLoadedEventArgs e)
  {
    // Optional: Emit metrics or logging
  }

  void OnStorageDocumentSaved(object? sender, DocumentSavedEventArgs e)
  {
    // Optional: Emit metrics or logging
  }

  #endregion

  #region Helper Methods

  /// <summary>
  /// Callback for synchronizer to get the document lock for serializing FFI access.
  /// Returns the handle's lock if available, otherwise a per-document fallback lock
  /// for documents being discovered or not yet loaded.
  /// </summary>
  SemaphoreSlim GetDocumentLock(string documentId)
  {
    _handleLock.Wait();
    try
    {
      if(_handleCache.TryGetValue(documentId, out var handle))
      {
        return handle.DocumentLock;
      }

      // Return a stable fallback lock for documents not yet in the handle cache
      // (e.g., during discovery when a sync message arrives before the handle is created).
      if(!_documentLockFallbacks.TryGetValue(documentId, out var fallback))
      {
        fallback = new SemaphoreSlim(1, 1);
        _documentLockFallbacks[documentId] = fallback;
      }

      return fallback;
    }
    finally
    {
      _handleLock.Release();
    }
  }

  /// <summary>
  /// Callback for synchronizer to get document.
  /// Uses synchronous Wait() because this is passed as a sync delegate to the synchronizer.
  /// The lock is only held briefly for dictionary lookup after restructuring all public methods
  /// to release the lock before calling RegisterHandleAsync.
  /// </summary>
  AutomergeDocument? GetDocument(string documentId)
  {
    _handleLock.Wait();
    try
    {
      if(_handleCache.TryGetValue(documentId, out var handle))
      {
        return handle.GetDocument();
      }

      return null;
    }
    finally
    {
      _handleLock.Release();
    }
  }

  /// <summary>
  /// Async callback for synchronizer to update document.
  /// </summary>
  async Task UpdateDocumentAsync(string documentId, AutomergeDocument document, PeerId? senderId = null)
  {
    await _handleLock.WaitAsync().ConfigureAwait(false);
    IDocHandle? handle;
    bool isNewHandle = false;
    try
    {
      if(!_handleCache.TryGetValue(documentId, out handle))
      {
        // Document received from sync but no handle exists yet.
        // This happens when a peer sends sync data for a document the server hasn't seen.
        // RequestDocument runs FindAsync() in Task.Run (async), so the handle may not be cached yet.
        // Create a handle now to prevent the synced data from being silently discarded.
        var newHandle = new DocHandle<object>(
          documentId,
          new DocHandleOptions<object> { IsNew = false, ViewFactory = CreateViewFactory<object>() },
          document: null,
          logger: CreateDocHandleLogger<object>());

        _handleCache[documentId] = newHandle;
        handle = newHandle;
        isNewHandle = true;
      }
    }
    finally
    {
      _handleLock.Release();
    }

    if(isNewHandle)
    {
      // New handle discovered via sync — initialize lifecycle: Idle → Loading → Ready
      handle.Begin();
      handle.Initialize(document, isNewDocument: false);
      handle.DoneLoading();

      // Wire up events for persistence and synchronizer notifications.
      // RegisterHandleAsync is async; fire-and-forget since this callback is synchronous.
      _ = Task.Run(async () =>
      {
        try
        {
          await RegisterHandleAsync(documentId, (DocHandle<object>)handle).ConfigureAwait(false);

          DocumentLoaded?.Invoke(
            this, new DocumentEventArgs { DocumentId = DocumentId.Parse(documentId), Handle = handle });
        }
        catch(Exception ex)
        {
          _logger.LogError(ex, "UpdateDocument: sync-discovered handle registration failed [DocumentId={DocumentId}]",
                           documentId);
        }
      });
    }
    else if(handle.State == HandleState.Requesting || handle.State == HandleState.Loading)
    {
      // Handle exists but waiting for data — initialize with received document
      handle.Initialize(document, isNewDocument: false);
      handle.DoneLoading(); // Transition to Ready
    }
    else if(handle.State == HandleState.Ready)
    {
      // Handle already initialized — notify about remote change (triggers HeadsChanged → save)
      await handle.NotifyRemoteChangeAsync(document, senderId).ConfigureAwait(false);
    }
  }

  /// <summary>
  /// Callback for synchronizer to get peer metadata.
  /// </summary>
  PeerMetadata? GetPeerMetadata(PeerId peerId)
  {
    return _network.GetPeerMetadata(peerId);
  }

  /// <summary>
  /// Callback for synchronizer to request unknown document.
  /// </summary>
  void RequestDocument(string documentId)
  {
    // Trigger find operation in background
    _ = Task.Run(async () =>
    {
      try
      {
        await FindAsync<object>(documentId).ConfigureAwait(false);
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "RequestDocument: background find failed [DocumentId={DocumentId}]", documentId);
      }
    });
  }

  /// <summary>
  /// Creates a logger for DocHandle instances.
  /// </summary>
  ILogger CreateDocHandleLogger<T>() =>
    _loggerFactory.CreateLogger($"Six.Automerge.Repository.DocHandle<{typeof(T).Name}>");

  /// <summary>
  /// Creates a view factory for the specified type.
  /// </summary>
  static Func<AutomergeDocument, T> CreateViewFactory<T>()
  {
    if(typeof(T) == typeof(object) || typeof(T) == typeof(AutomergeDocument))
    {
      return doc => (T)(object)doc;
    }

    // For other types, would need proper deserialization
    return doc => (T)(object)doc;
  }

  #endregion

  #region Persistence and Cleanup

  /// <summary>
  /// Flushes pending saves for specified documents (or all documents).
  /// </summary>
  /// <param name="documentIds">Optional list of document IDs to flush. If null, flushes all.</param>
  public async Task FlushAsync(IEnumerable<string>? documentIds = null)
  {
    if(_storage is null) return;

    ThrowIfDisposed();

    var idsToFlush = documentIds?.ToList() ?? _handleCache.Keys.ToList();

    var saveTasks = new List<Task>();

    foreach(var documentId in idsToFlush)
    {
      // Cancel pending throttled save
      await _saveLock.WaitAsync().ConfigureAwait(false);
      try
      {
        if(_pendingSaves.TryGetValue(documentId, out var cts))
        {
          cts.Cancel();
          _pendingSaves.Remove(documentId);
        }
      }
      finally
      {
        _saveLock.Release();
      }

      // Perform immediate save
      saveTasks.Add(PerformSaveAsync(documentId));
    }

    await Task.WhenAll(saveTasks).ConfigureAwait(false);
  }

  /// <summary>
  /// Removes a document from the cache without deleting from storage.
  /// Useful for memory management.
  /// </summary>
  /// <param name="documentId">The document ID to remove from cache.</param>
  public async Task RemoveFromCacheAsync(string documentId)
  {
    if(string.IsNullOrWhiteSpace(documentId))
      throw new ArgumentException("Document ID cannot be null or whitespace", nameof(documentId));

    ThrowIfDisposed();

    // FlushAsync first
    await FlushAsync(new[] { documentId }).ConfigureAwait(false);

    await _handleLock.WaitAsync().ConfigureAwait(false);
    IDocHandle? handle;
    try
    {
      if(!_handleCache.TryGetValue(documentId, out handle))
      {
        return;
      }

      _handleCache.Remove(documentId);
    }
    finally
    {
      _handleLock.Release();
    }

    // Unregister from synchronizer
    await _synchronizer.RemoveDocument(documentId).ConfigureAwait(false);

    // Unload handle
    handle.Unload();
  }

  /// <summary>
  /// Gracefully shuts down the repository, flushing all pending saves.
  /// </summary>
  public async Task ShutdownAsync()
  {
    if(_isDisposed) return;

    // FlushAsync all documents
    await FlushAsync().ConfigureAwait(false);

    // Dispose
    Dispose();
  }


  void ThrowIfDisposed()
  {
    if(_isDisposed)
    {
      throw new ObjectDisposedException(nameof(Repo));
    }
  }

  #endregion
}