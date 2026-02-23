namespace Six.Automerge.Repository.Storage;

/// <summary>
/// Represents a hierarchical storage key in the format [component1, component2, ...].
/// Used to organize documents, snapshots, incremental changes, and sync state.
/// </summary>
[PublicAPI]
public readonly record struct StorageKey
{
  /// <summary>
  /// Gets the hierarchical components of this storage key.
  /// </summary>
  public IReadOnlyList<string> Components { get; init; }

  /// <summary>
  /// Initializes a new instance of <see cref="StorageKey"/> with the specified components.
  /// </summary>
  /// <param name="components">The hierarchical key components.</param>
  public StorageKey(IReadOnlyList<string> components)
  {
    Components = components ?? Array.Empty<string>();
  }

  /// <summary>
  /// Creates a storage key for a document snapshot.
  /// Format: [documentId, "snapshot", snapshotHash]
  /// </summary>
  /// <param name="documentId">The document identifier.</param>
  /// <param name="snapshotHash">The hash of the snapshot (typically from document heads).</param>
  /// <returns>A storage key for the snapshot.</returns>
  public static StorageKey DocumentSnapshot(string documentId, string snapshotHash)
  {
    return new StorageKey(new[] { documentId, "snapshot", snapshotHash });
  }

  /// <summary>
  /// Creates a storage key for an incremental document change.
  /// Format: [documentId, "incremental", changeHash]
  /// </summary>
  /// <param name="documentId">The document identifier.</param>
  /// <param name="changeHash">The hash of the incremental change (typically from document heads).</param>
  /// <returns>A storage key for the incremental change.</returns>
  public static StorageKey DocumentIncremental(string documentId, string changeHash)
  {
    return new StorageKey(new[] { documentId, "incremental", changeHash });
  }

  /// <summary>
  /// Creates a storage key for document sync state.
  /// Format: [documentId, "sync-state", storageId]
  /// </summary>
  /// <param name="documentId">The document identifier.</param>
  /// <param name="storageId">The storage or peer identifier.</param>
  /// <returns>A storage key for the sync state.</returns>
  public static StorageKey DocumentSyncState(string documentId, string storageId)
  {
    return new StorageKey(new[] { documentId, "sync-state", storageId });
  }

  /// <summary>
  /// Creates a generic storage key.
  /// Format: [namespace, key]
  /// </summary>
  /// <param name="namespace">The namespace for organizing keys.</param>
  /// <param name="key">The key within the namespace.</param>
  /// <returns>A generic storage key.</returns>
  public static StorageKey Generic(string @namespace, string key)
  {
    return new StorageKey(new[] { @namespace, key });
  }

  /// <summary>
  /// Converts the storage key to a debug-friendly string representation.
  /// Components are joined with forward slashes.
  /// </summary>
  /// <returns>A string representation of the key.</returns>
  public string ToDebugString()
  {
    return Components == null || Components.Count == 0
      ? string.Empty
      : string.Join("/", Components);
  }

  /// <summary>
  /// Determines whether this storage key starts with the specified prefix.
  /// Used for hierarchical key filtering.
  /// </summary>
  /// <param name="prefix">The prefix to check.</param>
  /// <returns>True if this key starts with the prefix; otherwise, false.</returns>
  public bool StartsWith(StorageKey prefix)
  {
    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
    if(prefix.Components == null || prefix.Components.Count == 0)
      return true;

    if(Components == null || Components.Count < prefix.Components.Count)
      return false;

    for(int i = 0; i < prefix.Components.Count; i++)
    {
      if(Components[i] != prefix.Components[i])
        return false;
    }

    return true;
  }

  /// <summary>
  /// Returns a string representation of this storage key.
  /// </summary>
  /// <returns>A string representation of the key.</returns>
  public override string ToString() => ToDebugString();
}