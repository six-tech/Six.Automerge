using Six.Automerge;

namespace Six.Automerge.Repository.Storage;

/// <summary>
/// Manages document persistence and sync state storage with automatic compaction.
/// </summary>
/// <remarks>
/// <para>
/// The storage subsystem implements an efficient storage strategy:
/// </para>
/// <list type="number">
/// <item>First save: stores full snapshot</item>
/// <item>Subsequent saves: stores incremental changes</item>
/// <item>When incremental size >= snapshot size: compact (new snapshot, remove old chunks)</item>
/// </list>
/// <para>
/// This balances storage size vs. load performance. Loading is always: snapshot + incrementals.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class StorageSubsystem : IDisposable
{
  readonly IStorageAdapter _adapter;
  readonly Dictionary<string, Task> _pendingSaves = [];
  readonly Dictionary<string, IReadOnlyList<string>> _lastSavedHeads = [];
  readonly SemaphoreSlim _saveLock = new(1, 1);
  bool _isDisposed;
  
  
  
  /// <summary>
  /// Event raised when a document is successfully loaded from storage.
  /// </summary>
  public event EventHandler<DocumentLoadedEventArgs>? DocumentLoaded;

  /// <summary>
  /// Event raised when a document is successfully saved to storage.
  /// </summary>
  public event EventHandler<DocumentSavedEventArgs>? DocumentSaved;
  
  

  /// <summary>
  /// Initializes a new instance of <see cref="StorageSubsystem"/> with the specified storage adapter.
  /// </summary>
  /// <param name="adapter">The storage adapter to use for persistence.</param>
  /// <exception cref="ArgumentNullException">Thrown if adapter is null.</exception>
  public StorageSubsystem(IStorageAdapter adapter)
  {
    _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
  }
  
  /// <summary>
  /// Disposes this storage subsystem and releases resources.
  /// </summary>
  public void Dispose()
  {
    if(_isDisposed)
      return;

    _isDisposed = true;
    _saveLock.Dispose();
  }
  
  

  /// <summary>
  /// Loads data from a generic namespace/key pair.
  /// </summary>
  /// <param name="namespace">The namespace for organizing keys.</param>
  /// <param name="key">The key within the namespace.</param>
  /// <returns>The data if found; null otherwise.</returns>
  /// <exception cref="ArgumentException">Thrown if namespace or key is null or whitespace.</exception>
  public async Task<byte[]?> Load(string @namespace, string key)
  {
    ThrowIfDisposed();

    if(string.IsNullOrWhiteSpace(@namespace))
      throw new ArgumentException("Namespace cannot be null or whitespace.", nameof(@namespace));
    if(string.IsNullOrWhiteSpace(key))
      throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

    var storageKey = StorageKey.Generic(@namespace, key);
    return await _adapter.Load(storageKey);
  }

  /// <summary>
  /// Saves data to a generic namespace/key pair.
  /// </summary>
  /// <param name="namespace">The namespace for organizing keys.</param>
  /// <param name="key">The key within the namespace.</param>
  /// <param name="data">The data to save.</param>
  /// <exception cref="ArgumentException">Thrown if namespace or key is null or whitespace.</exception>
  /// <exception cref="ArgumentNullException">Thrown if data is null.</exception>
  public async Task Save(string @namespace, string key, byte[] data)
  {
    ThrowIfDisposed();

    if(string.IsNullOrWhiteSpace(@namespace))
      throw new ArgumentException("Namespace cannot be null or whitespace.", nameof(@namespace));
    if(string.IsNullOrWhiteSpace(key))
      throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
    if(data is null)
      throw new ArgumentNullException(nameof(data));

    var storageKey = StorageKey.Generic(@namespace, key);
    await _adapter.Save(storageKey, data);
  }

  /// <summary>
  /// Removes data from a generic namespace/key pair.
  /// </summary>
  /// <param name="namespace">The namespace for organizing keys.</param>
  /// <param name="key">The key within the namespace.</param>
  /// <exception cref="ArgumentException">Thrown if namespace or key is null or whitespace.</exception>
  public async Task Remove(string @namespace, string key)
  {
    ThrowIfDisposed();

    if(string.IsNullOrWhiteSpace(@namespace))
      throw new ArgumentException("Namespace cannot be null or whitespace.", nameof(@namespace));
    if(string.IsNullOrWhiteSpace(key))
      throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

    var storageKey = StorageKey.Generic(@namespace, key);
    await _adapter.Remove(storageKey);
  }

  /// <summary>
  /// Loads a document from storage.
  /// </summary>
  /// <param name="documentId">The document identifier.</param>
  /// <returns>The loaded document, or null if not found in storage.</returns>
  /// <exception cref="ArgumentException">Thrown if documentId is null or whitespace.</exception>
  /// <exception cref="InvalidOperationException">Thrown if document data is corrupted.</exception>
  public async Task<AutomergeDocument?> LoadDoc(string documentId)
  {
    ThrowIfDisposed();

    if(string.IsNullOrWhiteSpace(documentId))
      throw new ArgumentException("Document ID cannot be null or whitespace.", nameof(documentId));

    // Create key prefix for all chunks of this document
    var prefix = new StorageKey(new[] { documentId });

    // Load all chunks for this document
    var chunks = await _adapter.LoadRange(prefix);

    if(chunks.Count == 0)
      return null;

    // Separate snapshots and incrementals
    var snapshots = chunks.Where(c => c.Type == ChunkType.Snapshot).ToList();
    var incrementals = chunks.Where(c => c.Type == ChunkType.Incremental).ToList();

    if(snapshots.Count == 0)
      throw new InvalidOperationException($"Document '{documentId}' has no snapshot in storage.");

    // Load from the snapshot (should be only one after compaction, but take the first if multiple)
    var snapshot = snapshots[0];
    AutomergeDocument doc;

    try
    {
      doc = AutomergeDocument.Load(snapshot.Data);
    }
    catch(Exception ex)
    {
      throw new InvalidOperationException($"Failed to load document '{documentId}' from snapshot.", ex);
    }

    // Apply incremental changes in order
    foreach(var incremental in incrementals)
    {
      try
      {
        doc.LoadIncremental(incremental.Data);
      }
      catch(Exception ex)
      {
        doc.Dispose();
        throw new InvalidOperationException($"Failed to load incremental data for document '{documentId}'.", ex);
      }
    }

    // Update tracked heads
    _lastSavedHeads[documentId] = doc.GetHeads();

    // Emit event
    DocumentLoaded?.Invoke(this, new DocumentLoadedEventArgs(DocumentId.Parse(documentId)));

    return doc;
  }

  /// <summary>
  /// Saves a document to storage with automatic compaction.
  /// </summary>
  /// <param name="documentId">The document identifier.</param>
  /// <param name="doc">The document to save.</param>
  /// <exception cref="ArgumentException">Thrown if documentId is null or whitespace.</exception>
  /// <exception cref="ArgumentNullException">Thrown if doc is null.</exception>
  public async Task SaveDoc(string documentId, AutomergeDocument doc)
  {
    ThrowIfDisposed();

    if(string.IsNullOrWhiteSpace(documentId))
      throw new ArgumentException("Document ID cannot be null or whitespace.", nameof(documentId));
    if(doc is null)
      throw new ArgumentNullException(nameof(doc));

    await _saveLock.WaitAsync();
    try
    {
      var currentHeads = doc.GetHeads();
      if(currentHeads.Count == 0)
      {
        // No changes to save
        return;
      }

      // Check if we should compact
      bool shouldCompact = await ShouldCompact(documentId);
      bool compacted = false;

      if(shouldCompact)
      {
        // Compact: save new full snapshot and remove old chunks
        await CompactStorage(documentId, doc);
        compacted = true;
      }
      else
      {
        // Save incremental
        var lastHeads = _lastSavedHeads.TryGetValue(documentId, out var heads)
          ? heads
          : Array.Empty<string>();

        byte[] incrementalData;
        try
        {
          incrementalData = doc.SaveAfter(lastHeads);
        }
        catch(Exception ex)
        {
          throw new InvalidOperationException($"Failed to save incremental data for document '{documentId}'.", ex);
        }

        if(incrementalData.Length > 0)
        {
          // Generate key using current heads
          var headHash = currentHeads.Count > 0 ? currentHeads[0] : "empty";
          var incrementalKey = StorageKey.DocumentIncremental(documentId, headHash);
          await _adapter.Save(incrementalKey, incrementalData);
        }
      }

      // Update last saved heads
      _lastSavedHeads[documentId] = currentHeads;

      // Emit event
      DocumentSaved?.Invoke(this, new DocumentSavedEventArgs(DocumentId.Parse(documentId), compacted));
    }
    finally
    {
      _saveLock.Release();
    }
  }

  /// <summary>
  /// Removes all storage for a document (snapshots, incrementals, sync states).
  /// </summary>
  /// <param name="documentId">The document identifier.</param>
  /// <exception cref="ArgumentException">Thrown if documentId is null or whitespace.</exception>
  public async Task RemoveDoc(string documentId)
  {
    ThrowIfDisposed();

    if(string.IsNullOrWhiteSpace(documentId))
      throw new ArgumentException("Document ID cannot be null or whitespace.", nameof(documentId));

    var prefix = new StorageKey(new[] { documentId });
    await _adapter.RemoveRange(prefix);

    // Clear cached state
    _lastSavedHeads.Remove(documentId);
  }

  /// <summary>
  /// Loads persisted sync state for a document and peer.
  /// </summary>
  /// <param name="documentId">The document identifier.</param>
  /// <param name="storageId">The storage or peer identifier.</param>
  /// <returns>The sync state if found; null otherwise.</returns>
  /// <exception cref="ArgumentException">Thrown if documentId is null or whitespace.</exception>
  public async Task<AutomergeSyncState?> LoadSyncState(string documentId, StorageId storageId)
  {
    ThrowIfDisposed();

    if(string.IsNullOrWhiteSpace(documentId))
      throw new ArgumentException("Document ID cannot be null or whitespace.", nameof(documentId));

    var key = StorageKey.DocumentSyncState(documentId, storageId);
    var data = await _adapter.Load(key);

    if(data is null)
      return null;

    try
    {
      return AutomergeSyncState.Decode(data);
    }
    catch(Exception ex)
    {
      throw new InvalidOperationException(
        $"Failed to decode sync state for document '{documentId}' and storage '{storageId}'.", ex);
    }
  }

  /// <summary>
  /// Persists sync state for a document and peer.
  /// </summary>
  /// <param name="documentId">The document identifier.</param>
  /// <param name="storageId">The storage or peer identifier.</param>
  /// <param name="syncState">The sync state to persist.</param>
  /// <exception cref="ArgumentException">Thrown if documentId is null or whitespace.</exception>
  /// <exception cref="ArgumentNullException">Thrown if syncState is null.</exception>
  public async Task SaveSyncState(string documentId, StorageId storageId, AutomergeSyncState syncState)
  {
    ThrowIfDisposed();

    if(string.IsNullOrWhiteSpace(documentId))
      throw new ArgumentException("Document ID cannot be null or whitespace.", nameof(documentId));
    if(syncState is null)
      throw new ArgumentNullException(nameof(syncState));

    var key = StorageKey.DocumentSyncState(documentId, storageId);
    var data = syncState.Encode();
    await _adapter.Save(key, data);
  }

  
  
  /// <summary>
  /// Determines if document storage should be compacted.
  /// Compaction replaces snapshot + incrementals with a single new snapshot.
  /// </summary>
  /// <param name="documentId">The document identifier.</param>
  /// <returns>True if compaction should occur; false otherwise.</returns>
  async Task<bool> ShouldCompact(string documentId)
  {
    var prefix = new StorageKey(new[] { documentId });
    var chunks = await _adapter.GetChunkInfo(prefix);

    var snapshotSize = chunks
      .Where(c => c.Type == ChunkType.Snapshot)
      .Sum(c => c.Size);

    var incrementalSize = chunks
      .Where(c => c.Type == ChunkType.Incremental)
      .Sum(c => c.Size);

    // Compact when incrementals are as large as snapshot
    // Also compact if there's no snapshot yet (first save)
    return snapshotSize == 0 || incrementalSize >= snapshotSize;
  }

  /// <summary>
  /// Compacts storage by saving a new full snapshot and removing old chunks.
  /// </summary>
  /// <param name="documentId">The document identifier.</param>
  /// <param name="doc">The document to save.</param>
  async Task CompactStorage(string documentId, AutomergeDocument doc)
  {
    // Save full snapshot
    byte[] snapshotData;
    try
    {
      snapshotData = doc.Save();
    }
    catch(Exception ex)
    {
      throw new InvalidOperationException($"Failed to save snapshot for document '{documentId}'.", ex);
    }

    // Generate snapshot key using current heads
    var currentHeads = doc.GetHeads();
    var headHash = currentHeads.Count > 0 ? currentHeads[0] : "empty";
    var snapshotKey = StorageKey.DocumentSnapshot(documentId, headHash);

    // Remove old chunks first (all chunks for this document except sync states)
    var prefix = new StorageKey(new[] { documentId });
    var existingChunks = await _adapter.LoadRange(prefix);

    // Remove snapshots and incrementals, but keep sync states
    foreach(var chunk in existingChunks)
    {
      if(chunk.Type == ChunkType.Snapshot || chunk.Type == ChunkType.Incremental)
      {
        await _adapter.Remove(chunk.Key);
      }
    }

    // Save new snapshot
    await _adapter.Save(snapshotKey, snapshotData);
  }

  /// <summary>
  /// Throws if this instance has been disposed.
  /// </summary>
  void ThrowIfDisposed()
  {
    if(_isDisposed)
      throw new ObjectDisposedException(nameof(StorageSubsystem));
  }

}
