using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Six.Automerge.Repository.Network;

namespace Six.Automerge.Repository;

/// <summary>
/// C# port of the Automerge Repo <c>DocHandle&lt;T&gt;</c> API.
/// </summary>
[PublicAPI]
public sealed class DocHandle<T> : IDocHandle
{
  readonly SemaphoreSlim _documentLock = new(1, 1);
  readonly TaskCompletionSource<AutomergeDocument> _readySource =
    new(TaskCreationOptions.RunContinuationsAsynchronously);
  readonly List<byte[]> _pendingLocalChanges = [];
  readonly object _stateGate = new();
  readonly Func<AutomergeDocument, T> _viewFactory;
  readonly TimeSpan _timeout;
  readonly ILogger _logger;

  AutomergeDocument? _document;
  HandleState _state = HandleState.Idle;
  IReadOnlyList<string> _currentHeads = [];
  bool _isDisposed;
  bool _isInitialized;
  bool _isNewDocument;
  IReadOnlyList<string>? _fixedHeads;

  public DocHandle(
    DocumentId documentId,
    DocHandleOptions<T>? options = null,
    AutomergeDocument? document = null,
    ILogger? logger = null)
  {
    DocumentId = documentId;
    options ??= DocHandleOptions<T>.Default;

    _timeout = options.Timeout;
    _viewFactory = options.ViewFactory ?? DefaultViewFactory;
    _fixedHeads = options.Heads;
    _isNewDocument = options.IsNew;
    _logger = logger ?? NullLogger.Instance;

    if(document is not null)
    {
      Initialize(document, options.IsNew);
    }
  }

  #region Events

  public event EventHandler<DocHandleReadyEventArgs<T>>? Ready;

  public event EventHandler<DocHandleChangeEventArgs<T>>? Changed;

  public event EventHandler<DocHandleHeadsChangedEventArgs<T>>? HeadsChanged;

  public event EventHandler<DocHandleDeletedEventArgs<T>>? Deleted;

  public event EventHandler<DocHandleUnavailableEventArgs<T>>? Unavailable;

  public event EventHandler<DocHandleRemoteHeadsEventArgs<T>>? RemoteHeadsUpdated;

  public event EventHandler<DocHandleEphemeralMessageEventArgs<T>>? EphemeralMessageReceived;

  public event EventHandler<DocHandleEphemeralMessageEventArgs<T>>? EphemeralMessageOutbound;

  #endregion

  #region Properties

  public DocumentId DocumentId { get; }

  public HandleState State
  {
    get
    {
      lock(_stateGate)
      {
        return _state;
      }
    }
  }

  public bool IsReady => State == HandleState.Ready;

  public bool IsUnloaded => State == HandleState.Unloaded;

  public bool IsDeleted => State == HandleState.Deleted;

  public bool IsUnavailable => State == HandleState.Unavailable;

  public bool IsReadOnly => _fixedHeads is not null;

  public IReadOnlyList<string> Heads => Volatile.Read(ref _currentHeads);

  SemaphoreSlim IDocHandle.DocumentLock => _documentLock;

  #endregion

  #region Lifecycle

  public void Begin()
  {
    lock(_stateGate)
    {
      EnsureNotDisposed();
      if(_state == HandleState.Idle)
      {
        _state = HandleState.Loading;
        _logger.LogInformation("State transition Idle → Loading [DocumentId={DocumentId}]", DocumentId);
      }
    }
  }

  public void Initialize(AutomergeDocument document, bool isNewDocument = false)
  {
    ArgumentNullException.ThrowIfNull(document);
    EnsureNotDisposed();

    lock(_stateGate)
    {
      if(_isInitialized)
      {
        throw new InvalidOperationException($"DocHandle '{DocumentId}' is already initialized.");
      }

      var previousState = _state;
      _document = document;
      _isInitialized = true;
      _isNewDocument = isNewDocument;
      _state = HandleState.Ready;
      UpdateCachedHeads(document.GetHeads());
      _readySource.TrySetResult(document);
      _logger.LogInformation("State transition {PreviousState} → Ready [DocumentId={DocumentId}, IsNew={IsNew}]",
        previousState, DocumentId, isNewDocument);
    }

    Ready?.Invoke(this, new DocHandleReadyEventArgs<T>(this, isNewDocument));
  }

  public void DoneLoading()
  {
    lock(_stateGate)
    {
      if(_state == HandleState.Loading || _state == HandleState.Requesting)
      {
        var previousState = _state;
        _state = HandleState.Ready;
        _logger.LogInformation("State transition {PreviousState} → Ready [DocumentId={DocumentId}]",
          previousState, DocumentId);
      }
    }
  }

  public void Request()
  {
    lock(_stateGate)
    {
      if(_state == HandleState.Loading)
      {
        _state = HandleState.Requesting;
        _logger.LogInformation("State transition Loading → Requesting [DocumentId={DocumentId}]", DocumentId);
      }
    }
  }

  public void MarkUnavailable()
  {
    lock(_stateGate)
    {
      var previousState = _state;
      _state = HandleState.Unavailable;
      _logger.LogInformation("State transition {PreviousState} → Unavailable [DocumentId={DocumentId}]",
        previousState, DocumentId);
    }

    Unavailable?.Invoke(this, new DocHandleUnavailableEventArgs<T>(this));
  }

  public void Unload()
  {
    lock(_stateGate)
    {
      var previousState = _state;
      _state = HandleState.Unloaded;
      _document = null;
      _currentHeads = Array.Empty<string>();
      _logger.LogInformation("State transition {PreviousState} → Unloaded [DocumentId={DocumentId}]",
        previousState, DocumentId);
    }
  }

  public void Reload()
  {
    lock(_stateGate)
    {
      if(_state == HandleState.Unloaded)
      {
        _state = HandleState.Loading;
        _logger.LogInformation("State transition Unloaded → Loading [DocumentId={DocumentId}]", DocumentId);
      }
    }
  }

  public void Delete()
  {
    lock(_stateGate)
    {
      var previousState = _state;
      _state = HandleState.Deleted;
      _logger.LogInformation("State transition {PreviousState} → Deleted [DocumentId={DocumentId}]",
        previousState, DocumentId);
    }

    Deleted?.Invoke(this, new DocHandleDeletedEventArgs<T>(this));
  }

  public void Dispose()
  {
    if(Interlocked.Exchange(ref _isDisposed, true))
    {
      return;
    }

    _logger.LogDebug("Disposing DocHandle [DocumentId={DocumentId}]", DocumentId);

    AutomergeDocument? document;
    lock(_stateGate)
    {
      document = _document;
      _document = null;
      _state = HandleState.Unloaded;
      _currentHeads = Array.Empty<string>();
      _pendingLocalChanges.Clear();
    }

    document?.Dispose();
    _documentLock.Dispose();
    _readySource.TrySetCanceled();
  }

  #endregion

  #region Document access

  public async ValueTask<T> DocAsync(CancellationToken cancellationToken = default)
  {
    var doc = await WaitForDocumentAsync(cancellationToken).ConfigureAwait(false);
    return _viewFactory(doc);
  }

  public T Doc()
  {
    var doc = Volatile.Read(ref _document);
    if(doc is null)
    {
      throw new InvalidOperationException($"DocHandle '{DocumentId}' is not ready. Await WhenReadyAsync before calling Doc().");
    }

    return _viewFactory(doc);
  }

  public T DocSync() => Doc();

  /// <summary>
  /// Gets the underlying Automerge document (non-generic IDocHandle implementation).
  /// </summary>
  AutomergeDocument? IDocHandle.GetDocument() => _document;

  public Task WhenReadyAsync(CancellationToken cancellationToken = default) =>
    WaitForDocumentAsync(cancellationToken).AsTask();

  public async ValueTask<TResult> ReadAsync<TResult>(
    Func<AutomergeDocument, TResult> reader,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(reader);
    EnsureReady();

    var document = await WaitForDocumentAsync(cancellationToken).ConfigureAwait(false);

    await _documentLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    _logger.LogDebug("Document lock acquired for ReadAsync [DocumentId={DocumentId}]", DocumentId);
    try
    {
      EnsureNotDisposed();
      return reader(document);
    }
    finally
    {
      _documentLock.Release();
      _logger.LogDebug("Document lock released for ReadAsync [DocumentId={DocumentId}]", DocumentId);
    }
  }

  public async ValueTask ReadAsync(
    Action<AutomergeDocument> reader,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(reader);
    EnsureReady();

    var document = await WaitForDocumentAsync(cancellationToken).ConfigureAwait(false);

    await _documentLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    _logger.LogDebug("Document lock acquired for ReadAsync [DocumentId={DocumentId}]", DocumentId);
    try
    {
      EnsureNotDisposed();
      reader(document);
    }
    finally
    {
      _documentLock.Release();
      _logger.LogDebug("Document lock released for ReadAsync [DocumentId={DocumentId}]", DocumentId);
    }
  }

  public IReadOnlyList<string> GetHeads()
  {
    EnsureReady();
    return Heads;
  }

  public IReadOnlyList<IReadOnlyList<string>> History()
  {
    EnsureReady();

    return ReadAsync(static doc =>
    {
      var hashes = doc.TopoHistoryTraversal();
      if(hashes.Count == 0)
      {
        return [];
      }

      return hashes
        .Select(hash => (IReadOnlyList<string>)[hash])
        .ToArray();
    }).GetAwaiter().GetResult();
  }

  public DocHandle<T> View(IReadOnlyList<string> heads)
  {
    if(heads is null)
      throw new ArgumentNullException(nameof(heads));

    if(heads.Count == 0)
      throw new ArgumentException("Heads cannot be empty.", nameof(heads));

    EnsureReady();

    // CloneAsync the document by saving and loading
    var documentData = ReadAsync(doc => doc.Save()).GetAwaiter().GetResult();
    var clonedDoc = AutomergeDocument.Load(documentData);

    // Isolate the clone to the specified heads
    clonedDoc.Isolate(heads);

    // Create a new read-only handle with fixed heads
    var viewHandle = new DocHandle<T>(
      DocumentId,
      new DocHandleOptions<T>
      {
        IsNew = false,
        Heads = heads,
        Timeout = _timeout,
        ViewFactory = _viewFactory
      },
      clonedDoc);

    return viewHandle;
  }

  public IReadOnlyList<AutomergePatch> Diff(IReadOnlyList<string> fromHeads, IReadOnlyList<string> toHeads)
  {
    if(fromHeads is null)
    {
      throw new ArgumentNullException(nameof(fromHeads));
    }

    if(toHeads is null)
    {
      throw new ArgumentNullException(nameof(toHeads));
    }

    EnsureReady();

    return ReadAsync(doc => doc.Diff(fromHeads, toHeads)).GetAwaiter().GetResult();
  }

  /// <summary>
  /// Gets the metadata for a specific change.
  /// </summary>
  /// <param name="changeHash">
  /// The hash of the change to inspect. If null, uses the first head of the document.
  /// </param>
  /// <returns>
  /// A <see cref="DecodedChange"/> containing the change metadata, or null if the change doesn't exist.
  /// </returns>
  /// <exception cref="InvalidOperationException">Thrown if the document is not ready.</exception>
  public DecodedChange? Metadata(string? changeHash = null)
  {
    EnsureReady();

    return ReadAsync(doc =>
    {
      var hash = changeHash;
      if(string.IsNullOrEmpty(hash))
      {
        var heads = doc.GetHeads();
        if(heads.Count == 0)
        {
          return null;
        }
        hash = heads[0];
      }

      return doc.GetChangeMetadata(hash);
    }).GetAwaiter().GetResult();
  }

  #endregion

  #region Mutation helpers

  public DocLocalChange<T>? Change(
    Action<AutomergeDocument> mutation,
    string? message = null,
    long? timestampMillis = null,
    CancellationToken cancellationToken = default) =>
    ChangeAsync(mutation, message, timestampMillis, cancellationToken).GetAwaiter().GetResult();

  public async ValueTask<DocLocalChange<T>?> ChangeAsync(
    Action<AutomergeDocument> mutation,
    string? message = null,
    long? timestampMillis = null,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(mutation);

    // Check if we need to initialize the document (virtual document pattern)
    AutomergeDocument document;
    var existingDoc = Volatile.Read(ref _document);
    if(existingDoc is null)
    {
      // Document doesn't exist yet - create it (handles the case where FindAsync() timed out)
      lock(_stateGate)
      {
        if(_state == HandleState.Loading || _state == HandleState.Requesting)
        {
          var newDoc = AutomergeDocument.Create();
          Initialize(newDoc, isNewDocument: true);
          DoneLoading();
        }
      }
      document = await WaitForDocumentAsync(cancellationToken).ConfigureAwait(false);
    }
    else
    {
      document = existingDoc;
    }

    EnsureEditable();

    DocLocalChange<T>? change;
    await _documentLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    _logger.LogDebug("Document lock acquired for ChangeAsync [DocumentId={DocumentId}]", DocumentId);
    try
    {
      EnsureEditable();
      mutation(document);
      var changeHash = document.Commit(message, timestampMillis);
      UpdateCachedHeads(document.GetHeads());

      if(changeHash is null)
      {
        return null;
      }

      var lastChange = document.GetLastLocalChange();
      if(lastChange is null)
      {
        return null;
      }

      var clonedChange = lastChange.ToArray();
      _pendingLocalChanges.Add(clonedChange);
      var readOnlyChange = new ReadOnlyCollection<byte[]>([clonedChange]);
      change = new DocLocalChange<T>(this, changeHash, readOnlyChange);
    }
    finally
    {
      _documentLock.Release();
      _logger.LogDebug("Document lock released for ChangeAsync [DocumentId={DocumentId}]", DocumentId);
    }

    OnChanged(change, isLocal: true, null);
    return change;
  }

  /// <summary>
  /// Makes a change to the document as if it were at the specified heads, creating a concurrent branch.
  /// </summary>
  /// <param name="heads">The heads to fork at before making the change.</param>
  /// <param name="mutation">The mutation to apply to the forked document.</param>
  /// <returns>
  /// A list of binary changes representing the concurrent change that was made.
  /// </returns>
  /// <remarks>
  /// <para>
  /// This method creates a fork of the document at the specified heads, applies the mutation,
  /// commits the change, and then merges the forked document back into the main document.
  /// This creates a concurrent branch in the document history.
  /// </para>
  /// <para>
  /// This is useful for reconciling external state that was modified independently from the document.
  /// For example, if you have a text editor that was modified while disconnected from the document,
  /// you can use ChangeAt to apply those changes as if they happened at the point when the editor
  /// last synced.
  /// </para>
  /// </remarks>
  /// <exception cref="ArgumentNullException">Thrown when heads or mutation is null.</exception>
  /// <exception cref="ArgumentException">Thrown when heads is empty.</exception>
  /// <exception cref="InvalidOperationException">Thrown when the document is not ready or is read-only.</exception>
  public IReadOnlyList<byte[]> ChangeAt(
    IReadOnlyList<string> heads,
    Action<AutomergeDocument> mutation)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    if(heads.Count == 0)
    {
      throw new ArgumentException("Heads cannot be empty.", nameof(heads));
    }

    ArgumentNullException.ThrowIfNull(mutation);
    EnsureEditable();

    return ChangeAtAsync(heads, mutation, CancellationToken.None).GetAwaiter().GetResult();
  }

  async ValueTask<IReadOnlyList<byte[]>> ChangeAtAsync(
    IReadOnlyList<string> heads,
    Action<AutomergeDocument> mutation,
    CancellationToken cancellationToken)
  {
    var document = await WaitForDocumentAsync(cancellationToken).ConfigureAwait(false);

    byte[]? changeBytes;
    await _documentLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    _logger.LogDebug("Document lock acquired for ChangeAtAsync [DocumentId={DocumentId}]", DocumentId);
    try
    {
      EnsureEditable();

      // Fork the document at the specified heads
      using var forkedDoc = document.ForkAtHeads(heads);

      // Apply the mutation to the forked document
      mutation(forkedDoc);

      // Commit the changes in the forked document
      var changeHash = forkedDoc.Commit();
      if(changeHash is null)
      {
        // No changes were made, return empty
        return Array.Empty<byte[]>();
      }

      // Get the change bytes from the forked document
      var lastChange = forkedDoc.GetLastLocalChange();
      if(lastChange is null)
      {
        return Array.Empty<byte[]>();
      }

      // Merge the forked document back into the main document
      var mergedHashes = document.Merge(forkedDoc);

      // Update cached heads
      UpdateCachedHeads(document.GetHeads());

      changeBytes = lastChange.ToArray();
    }
    finally
    {
      _documentLock.Release();
      _logger.LogDebug("Document lock released for ChangeAtAsync [DocumentId={DocumentId}]", DocumentId);
    }

    // Emit change event outside lock
    OnChanged(null, isLocal: true, new[] { changeBytes });

    // Return the change as a single-item list
    return new ReadOnlyCollection<byte[]>(new[] { changeBytes });
  }

  public async ValueTask ApplyRemoteChangesAsync(
    IEnumerable<byte[]> changes,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(changes);
    EnsureReady();

    var changeBatch = changes.Select(change => change.ToArray()).ToArray();
    if(changeBatch.Length == 0)
    {
      return;
    }

    var document = await WaitForDocumentAsync(cancellationToken).ConfigureAwait(false);

    await _documentLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    _logger.LogDebug("Document lock acquired for ApplyRemoteChangesAsync [DocumentId={DocumentId}]", DocumentId);
    try
    {
      document.ApplyChanges(changeBatch);
      UpdateCachedHeads(document.GetHeads());
    }
    finally
    {
      _documentLock.Release();
      _logger.LogDebug("Document lock released for ApplyRemoteChangesAsync [DocumentId={DocumentId}]", DocumentId);
    }

    OnChanged(null, isLocal: false, changeBatch);
  }

  /// <summary>
  /// Notifies the handle that the document was updated externally (e.g., by sync protocol).
  /// Updates cached heads and triggers change events.
  /// </summary>
  public async Task NotifyRemoteChangeAsync(AutomergeDocument document, PeerId? senderId = null)
  {
    EnsureNotDisposed();

    lock(_stateGate)
    {
      if(_state != HandleState.Ready)
      {
        return;
      }
    }

    await _documentLock.WaitAsync().ConfigureAwait(false);
    _logger.LogDebug("Document lock acquired for NotifyRemoteChangeAsync [DocumentId={DocumentId}]", DocumentId);
    try
    {
      UpdateCachedHeads(document.GetHeads());
    }
    finally
    {
      _documentLock.Release();
      _logger.LogDebug("Document lock released for NotifyRemoteChangeAsync [DocumentId={DocumentId}]", DocumentId);
    }

    OnChanged(null, isLocal: false, null, senderId);
  }

  public async Task Merge(DocHandle<T> other)
  {
    if(other is null)
    {
      throw new ArgumentNullException(nameof(other));
    }

    EnsureEditable();

    bool hasChanges;
    await using(var mergeLock = await CompositeSemaphore.AcquireAsync(_documentLock, other._documentLock).ConfigureAwait(false))
    {
      var doc = DocInternal();
      var otherDoc = other.DocInternal();

      var hashes = doc.Merge(otherDoc);
      hasChanges = hashes.Count > 0;
      if(hasChanges)
      {
        UpdateCachedHeads(doc.GetHeads());
      }
    }

    if(hasChanges)
    {
      OnChanged(null, isLocal: true, null);
    }
  }

  public IReadOnlyList<byte[]> DrainLocalChanges()
  {
    EnsureReady();

    lock(_stateGate)
    {
      if(_pendingLocalChanges.Count == 0)
      {
        return Array.Empty<byte[]>();
      }

      var drained = _pendingLocalChanges
        .Select(change => change.ToArray())
        .ToArray();

      _pendingLocalChanges.Clear();
      return new ReadOnlyCollection<byte[]>(drained);
    }
  }

  #endregion

  #region Sync helpers

  public void SetSyncInfo(string storageId, SyncInfo info) =>
    RemoteHeadsUpdated?.Invoke(this, new DocHandleRemoteHeadsEventArgs<T>(this, storageId, info));

  public SyncInfo? GetSyncInfo(string storageId) => null;

  public void Broadcast(object? payload)
  {
    EphemeralMessageOutbound?.Invoke(this,
      new DocHandleEphemeralMessageEventArgs<T>(this, null, payload));
  }

  public void ReceiveEphemeralMessage(string senderId, object? payload)
  {
    EphemeralMessageReceived?.Invoke(this,
      new DocHandleEphemeralMessageEventArgs<T>(this, senderId, payload));
  }

  #endregion

  #region Internal helpers

  AutomergeDocument DocInternal()
  {
    var document = Volatile.Read(ref _document);
    if(document is null)
    {
      throw new InvalidOperationException($"DocHandle '{DocumentId}' does not have an attached document.");
    }

    return document;
  }

  ValueTask<AutomergeDocument> WaitForDocumentAsync(CancellationToken cancellationToken)
  {
    EnsureNotDisposed();

    var existing = Volatile.Read(ref _document);
    if(existing is not null)
    {
      return ValueTask.FromResult(existing);
    }

    return new ValueTask<AutomergeDocument>(_readySource.Task.WaitAsync(cancellationToken));
  }

  void OnChanged(DocLocalChange<T>? localChange, bool isLocal, IReadOnlyList<byte[]>? appliedChanges, PeerId? originPeerId = null)
  {
    // Always fire HeadsChanged — callers only invoke OnChanged after a meaningful change
    // (local Change(), ApplyRemoteChanges(), NotifyRemoteChange(), or Merge()).
    HeadsChanged?.Invoke(this, new DocHandleHeadsChangedEventArgs<T>(this, Heads, originPeerId));

    var doc = Volatile.Read(ref _document);
    if(doc is null)
    {
      return;
    }

    Changed?.Invoke(
      this,
      new DocHandleChangeEventArgs<T>(
        this,
        _viewFactory(doc),
        isLocal,
        localChange,
        appliedChanges));
  }

  void UpdateCachedHeads(IReadOnlyList<string> heads)
  {
    var copy = heads.Count == 0
      ? (IReadOnlyList<string>)Array.Empty<string>()
      : new ReadOnlyCollection<string>(heads.ToArray());

    Volatile.Write(ref _currentHeads, copy);
  }

  void EnsureEditable()
  {
    EnsureReady();
    if(IsReadOnly)
    {
      throw new InvalidOperationException("Handle is view-only and cannot be changed.");
    }
  }

  void EnsureReady()
  {
    EnsureNotDisposed();
    if(!IsReady)
    {
      throw new InvalidOperationException($"DocHandle '{DocumentId}' is not ready.");
    }
  }

  void EnsureNotDisposed()
  {
    if(Volatile.Read(ref _isDisposed))
    {
      throw new ObjectDisposedException(nameof(DocHandle<T>));
    }
  }

  static T DefaultViewFactory(AutomergeDocument doc)
  {
    if(typeof(T) == typeof(AutomergeDocument))
    {
      return (T)(object)doc;
    }

    throw new InvalidOperationException(
      $"Provide a view factory when constructing DocHandle<{typeof(T).Name}> so the document can be projected.");
  }

  #endregion

  sealed class CompositeSemaphore : IAsyncDisposable
  {
    readonly SemaphoreSlim _first;
    readonly SemaphoreSlim _second;

    CompositeSemaphore(SemaphoreSlim first, SemaphoreSlim second)
    {
      _first = first;
      _second = second;
    }

    public static async Task<CompositeSemaphore> AcquireAsync(SemaphoreSlim first, SemaphoreSlim second)
    {
      var instance = new CompositeSemaphore(first, second);
      await first.WaitAsync().ConfigureAwait(false);
      if(!ReferenceEquals(first, second))
      {
        await second.WaitAsync().ConfigureAwait(false);
      }

      return instance;
    }

    public ValueTask DisposeAsync()
    {
      _second.Release();
      _first.Release();
      return ValueTask.CompletedTask;
    }
  }
}

public enum HandleState
{
  Idle,
  Loading,
  Requesting,
  Ready,
  Unloaded,
  Deleted,
  Unavailable
}

public sealed record DocHandleOptions<T>
{
  public static DocHandleOptions<T> Default { get; } = new();

  public bool IsNew { get; init; }

  public IReadOnlyList<string>? Heads { get; init; }

  public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

  public Func<AutomergeDocument, T>? ViewFactory { get; init; }
}

public sealed record DocHandleReadyEventArgs<T>(DocHandle<T> Handle, bool IsNew);

public sealed record DocHandleChangeEventArgs<T>(
  DocHandle<T> Handle,
  T Document,
  bool IsLocal,
  DocLocalChange<T>? LocalChange,
  IReadOnlyList<byte[]>? AppliedChanges);

public sealed record DocHandleHeadsChangedEventArgs<T>(
  DocHandle<T> Handle,
  IReadOnlyList<string> Heads,
  PeerId? OriginPeerId = null);

public sealed record DocHandleDeletedEventArgs<T>(DocHandle<T> Handle);

public sealed record DocHandleUnavailableEventArgs<T>(DocHandle<T> Handle);

public sealed record DocHandleRemoteHeadsEventArgs<T>(
  DocHandle<T> Handle,
  string StorageId,
  SyncInfo SyncInfo);

public sealed record DocHandleEphemeralMessageEventArgs<T>(
  DocHandle<T> Handle,
  string? SenderId,
  object? Payload);

public sealed record DocLocalChange<T>(
  DocHandle<T> Handle,
  string ChangeHash,
  IReadOnlyList<byte[]> Changes);

public sealed record SyncInfo(
  IReadOnlyList<string> LastHeads,
  DateTimeOffset LastUpdated);
