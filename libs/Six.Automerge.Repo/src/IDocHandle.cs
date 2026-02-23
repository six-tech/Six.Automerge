using Six.Automerge.Repository.Network;

namespace Six.Automerge.Repository;

/// <summary>
/// Non-generic interface for DocHandle operations.
/// Allows storage of DocHandle instances with different generic type parameters in the same collection.
/// </summary>
/// <remarks>
/// This interface provides type-safe storage for generic DocHandle instances by exposing
/// only the non-generic members. This follows standard C# patterns for handling generic
/// type variance and mirrors TypeScript's use of `DocHandle&lt;any&gt;` in the cache.
/// </remarks>
[PublicAPI]
public interface IDocHandle : IDisposable
{
  #region Properties

  /// <summary>
  /// The unique identifier for this document.
  /// </summary>
  DocumentId DocumentId { get; }

  /// <summary>
  /// The current state of this handle.
  /// </summary>
  HandleState State { get; }

  /// <summary>
  /// Whether the document is ready for use.
  /// </summary>
  bool IsReady { get; }

  /// <summary>
  /// Whether the document has been unloaded.
  /// </summary>
  bool IsUnloaded { get; }

  /// <summary>
  /// Whether the document has been deleted.
  /// </summary>
  bool IsDeleted { get; }

  /// <summary>
  /// Whether the document is unavailable.
  /// </summary>
  bool IsUnavailable { get; }

  /// <summary>
  /// Whether this handle is read-only (fixed at specific heads).
  /// </summary>
  bool IsReadOnly { get; }

  /// <summary>
  /// The current heads (changesets) of the document.
  /// </summary>
  IReadOnlyList<string> Heads { get; }

  /// <summary>
  /// The lock that protects concurrent access to the underlying native document.
  /// Synchronizers must acquire this lock before calling FFI methods on the document.
  /// </summary>
  SemaphoreSlim DocumentLock { get; }

  #endregion

  #region Lifecycle Methods

  /// <summary>
  /// Marks the handle as beginning to load.
  /// </summary>
  void Begin();

  /// <summary>
  /// Initializes the handle with a document.
  /// </summary>
  void Initialize(AutomergeDocument document, bool isNewDocument = false);

  /// <summary>
  /// Marks the handle as done loading.
  /// </summary>
  void DoneLoading();

  /// <summary>
  /// Requests the document from the network.
  /// </summary>
  void Request();

  /// <summary>
  /// Marks the document as unavailable.
  /// </summary>
  void MarkUnavailable();

  /// <summary>
  /// Unloads the document from memory.
  /// </summary>
  void Unload();

  /// <summary>
  /// Reloads the document.
  /// </summary>
  void Reload();

  /// <summary>
  /// Marks the document as deleted.
  /// </summary>
  void Delete();

  #endregion

  #region Async Operations

  /// <summary>
  /// Waits for the document to become ready.
  /// </summary>
  Task WhenReadyAsync(CancellationToken cancellationToken = default);

  #endregion

  #region Document Access

  /// <summary>
  /// Gets the underlying Automerge document.
  /// </summary>
  /// <returns>The Automerge document, or null if not available.</returns>
  /// <remarks>
  /// This non-generic method allows accessing the document without knowing the view type.
  /// </remarks>
  AutomergeDocument? GetDocument();

  #endregion

  #region Sync Operations

  /// <summary>
  /// Notifies the handle that the document was updated remotely (by sync protocol).
  /// Updates cached heads and fires change notifications.
  /// </summary>
  Task NotifyRemoteChangeAsync(AutomergeDocument document, PeerId? senderId = null);

  /// <summary>
  /// Sets sync information for a storage peer.
  /// </summary>
  void SetSyncInfo(string storageId, SyncInfo info);

  /// <summary>
  /// Gets sync information for a storage peer.
  /// </summary>
  SyncInfo? GetSyncInfo(string storageId);

  /// <summary>
  /// Drains all pending local changes.
  /// </summary>
  IReadOnlyList<byte[]> DrainLocalChanges();

  /// <summary>
  /// Broadcasts a payload to all peers.
  /// </summary>
  void Broadcast(object? payload);

  /// <summary>
  /// Receives an ephemeral message from a peer.
  /// </summary>
  void ReceiveEphemeralMessage(string senderId, object? payload);

  #endregion
}
