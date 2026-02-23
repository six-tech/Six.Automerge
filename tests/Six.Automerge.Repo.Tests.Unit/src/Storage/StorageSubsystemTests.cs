using Six.Automerge;
using Six.Automerge.Repository.Storage;
using Xunit;

namespace Six.Automerge.Repo.Test.Unit.Storage;

[Trait("Category", "Storage")]
public class StorageSubsystemTests
{
    [Fact]
    public async Task SaveDoc_ThenLoadDoc_ReturnsEquivalentDocument()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);
        var documentId = TestDocumentIds.UniqueString();

        using var originalDoc = AutomergeDocument.Create();
        originalDoc.PutString(originalDoc.RootId, "name", "Alice");
        originalDoc.PutInt64(originalDoc.RootId, "age", 30);
        originalDoc.Commit("Initial commit");

        // Act
        await storage.SaveDoc(documentId, originalDoc);
        using var loadedDoc = await storage.LoadDoc(documentId);

        // Assert
        Assert.NotNull(loadedDoc);
        var originalJson = originalDoc.HydrateJson();
        var loadedJson = loadedDoc.HydrateJson();
        Assert.Equal(originalJson, loadedJson);
    }

    [Fact]
    public async Task LoadDoc_NonExistentDocument_ReturnsNull()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);

        // Act
        var result = await storage.LoadDoc(TestDocumentIds.UniqueString());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveDoc_FirstSave_CreatesSnapshot()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);
        var documentId = TestDocumentIds.UniqueString();

        using var doc = AutomergeDocument.Create();
        doc.PutString(doc.RootId, "test", "value");
        doc.Commit("First commit");

        // Act
        await storage.SaveDoc(documentId, doc);

        // Assert
        var prefix = new StorageKey(new[] { documentId });
        var chunks = await adapter.LoadRange(prefix);
        Assert.Single(chunks);
        Assert.Equal(ChunkType.Snapshot, chunks[0].Type);
    }

    [Fact]
    public async Task SaveDoc_MultipleSaves_CreatesIncrementalChunks()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);
        var documentId = TestDocumentIds.UniqueString();

        using var doc = AutomergeDocument.Create();

        // First save - creates snapshot
        doc.PutString(doc.RootId, "name", "Alice");
        doc.Commit("First commit");
        await storage.SaveDoc(documentId, doc);

        // Second save - creates incremental
        doc.PutInt64(doc.RootId, "age", 30);
        doc.Commit("Second commit");
        await storage.SaveDoc(documentId, doc);

        // Assert
        var prefix = new StorageKey(new[] { documentId });
        var chunks = await adapter.LoadRange(prefix);
        Assert.Equal(2, chunks.Count);
        Assert.Contains(chunks, c => c.Type == ChunkType.Snapshot);
        Assert.Contains(chunks, c => c.Type == ChunkType.Incremental);
    }

    [Fact]
    public async Task SaveDoc_WithManyChanges_TriggersCompaction()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);
        var documentId = TestDocumentIds.UniqueString();
        using var doc = AutomergeDocument.Create();

        // Create initial snapshot
        doc.PutString(doc.RootId, "initial", "value");
        doc.Commit("Initial");
        await storage.SaveDoc(documentId, doc);

        var savedEventCount = 0;
        var compactedCount = 0;
        storage.DocumentSaved += (sender, e) =>
        {
            savedEventCount++;
            if (e.Compacted) compactedCount++;
        };

        // Add many small changes to trigger compaction
        for (int i = 0; i < 50; i++)
        {
            doc.PutInt64(doc.RootId, $"field{i}", i);
            doc.Commit($"Change {i}");
            await storage.SaveDoc(documentId, doc);
        }

        // Assert - at some point compaction should have occurred
        Assert.True(compactedCount > 0, "Compaction should have occurred");

        // Verify document can still be loaded correctly
        using var loadedDoc = await storage.LoadDoc(documentId);
        Assert.NotNull(loadedDoc);
        Assert.Equal(doc.HydrateJson(), loadedDoc.HydrateJson());
    }

    [Fact]
    public async Task RemoveDoc_DeletesAllChunks()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);
        var documentId = TestDocumentIds.UniqueString();

        using var doc = AutomergeDocument.Create();
        doc.PutString(doc.RootId, "test", "value");
        doc.Commit("Commit");
        await storage.SaveDoc(documentId, doc);

        // Act
        await storage.RemoveDoc(documentId);

        // Assert
        var loaded = await storage.LoadDoc(documentId);
        Assert.Null(loaded);

        var prefix = new StorageKey(new[] { documentId });
        var chunks = await adapter.LoadRange(prefix);
        Assert.Empty(chunks);
    }

    [Fact]
    public async Task SaveSyncState_ThenLoadSyncState_ReturnsEquivalentState()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);
        var documentId = TestDocumentIds.UniqueString();
        var storageId = new StorageId("peer1");

        using var doc = AutomergeDocument.Create();
        using var originalState = doc.CreateSyncState();

        // Generate a sync message to modify the state
        doc.GenerateSyncMessage(originalState);

        // Act
        await storage.SaveSyncState(documentId, storageId, originalState);
        using var loadedState = await storage.LoadSyncState(documentId, storageId);

        // Assert
        Assert.NotNull(loadedState);
        // Sync states should have the same shared heads if they're equivalent
        Assert.Equal(originalState.SharedHeads, loadedState.SharedHeads);
    }

    [Fact]
    public async Task LoadSyncState_NonExistent_ReturnsNull()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);

        // Act
        var result = await storage.LoadSyncState(TestDocumentIds.UniqueString(), new StorageId("peer1"));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveSyncState_MultiplePeers_StoresIndependently()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);
        var documentId = TestDocumentIds.UniqueString();
        var peer1 = new StorageId("peer1");
        var peer2 = new StorageId("peer2");

        using var doc = AutomergeDocument.Create();
        using var state1 = doc.CreateSyncState();
        using var state2 = doc.CreateSyncState();

        // Act
        await storage.SaveSyncState(documentId, peer1, state1);
        await storage.SaveSyncState(documentId, peer2, state2);

        // Assert
        using var loaded1 = await storage.LoadSyncState(documentId, peer1);
        using var loaded2 = await storage.LoadSyncState(documentId, peer2);
        Assert.NotNull(loaded1);
        Assert.NotNull(loaded2);
    }

    [Fact]
    public async Task DocumentLoaded_Event_FiresOnSuccessfulLoad()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);
        var documentId = TestDocumentIds.UniqueString();

        using var doc = AutomergeDocument.Create();
        doc.PutString(doc.RootId, "test", "value");
        doc.Commit();
        await storage.SaveDoc(documentId, doc);

        string? loadedDocId = null;
        storage.DocumentLoaded += (sender, e) => loadedDocId = e.DocumentId.Value;

        // Act
        using var loaded = await storage.LoadDoc(documentId);

        // Assert
        Assert.Equal(documentId, loadedDocId);
    }

    [Fact]
    public async Task DocumentSaved_Event_FiresOnSuccessfulSave()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);
        var documentId = TestDocumentIds.UniqueString();

        string? savedDocId = null;
        bool? wasCompacted = null;
        storage.DocumentSaved += (sender, e) =>
        {
            savedDocId = e.DocumentId.Value;
            wasCompacted = e.Compacted;
        };

        using var doc = AutomergeDocument.Create();
        doc.PutString(doc.RootId, "test", "value");
        doc.Commit();

        // Act
        await storage.SaveDoc(documentId, doc);

        // Assert
        Assert.Equal(documentId, savedDocId);
        Assert.NotNull(wasCompacted);
    }

    [Fact]
    public async Task GenericLoad_ThenSave_WorksCorrectly()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);
        var ns = "custom";
        var key = "mykey";
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        await storage.Save(ns, key, data);
        var loaded = await storage.Load(ns, key);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(data, loaded);
    }

    [Fact]
    public async Task GenericLoad_NonExistent_ReturnsNull()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);

        // Act
        var result = await storage.Load("namespace", "nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenericRemove_DeletesData()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);
        var ns = "custom";
        var key = "mykey";
        var data = new byte[] { 1, 2, 3 };

        await storage.Save(ns, key, data);

        // Act
        await storage.Remove(ns, key);
        var loaded = await storage.Load(ns, key);

        // Assert
        Assert.Null(loaded);
    }

    [Fact]
    public async Task LoadDoc_AfterCompaction_LoadsCorrectly()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);
        var documentId = "doc1";

        using var doc = AutomergeDocument.Create();

        // Create initial snapshot
        doc.PutString(doc.RootId, "name", "Alice");
        doc.Commit("Initial");
        await storage.SaveDoc(documentId, doc);

        // Make many changes to trigger compaction
        for (int i = 0; i < 50; i++)
        {
            doc.PutInt64(doc.RootId, $"field{i}", i);
            doc.Commit($"Change {i}");
            await storage.SaveDoc(documentId, doc);
        }

        // Act - load after compaction
        using var loadedDoc = await storage.LoadDoc(documentId);

        // Assert
        Assert.NotNull(loadedDoc);
        Assert.Equal(doc.HydrateJson(), loadedDoc.HydrateJson());
    }

    [Fact]
    public void Constructor_NullAdapter_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new StorageSubsystem(null!));
    }

    [Fact]
    public async Task SaveDoc_EmptyDocumentId_ThrowsArgumentException()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);
        using var doc = AutomergeDocument.Create();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await storage.SaveDoc("", doc));
    }

    [Fact]
    public async Task SaveDoc_NullDocument_ThrowsArgumentNullException()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        using var storage = new StorageSubsystem(adapter);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await storage.SaveDoc("doc1", null!));
    }

    [Fact]
    public async Task Dispose_SubsequentOperations_ThrowObjectDisposedException()
    {
        // Arrange
        var adapter = new InMemoryStorageAdapter();
        var storage = new StorageSubsystem(adapter);
        storage.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await storage.LoadDoc("doc1"));

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await storage.SaveDoc("doc1", AutomergeDocument.Create()));
    }
}
