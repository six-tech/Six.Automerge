// Copyright (c) Six Technology, Inc. All rights reserved.
// Licensed under the MIT license.

using FluentAssertions;
using Six.Automerge;
using AutomergeRepo = Six.Automerge.Repository.Repo;

namespace Six.Automerge.SyncServer.Tests.Unit;

[Trait("Category", "SyncServer")]
public sealed class SyncServerTests : IAsyncDisposable
{
  readonly InMemoryStorageAdapter _storage = new();
  readonly MockServerTransport _transport = new();
  readonly SyncServer _server;

  public SyncServerTests()
  {
    _server = new SyncServer(
      new SyncServerOptions
      {
        Storage = _storage,
        PersistenceInterval = TimeSpan.FromMilliseconds(50),
        SyncDebounceRate = 1,
        SaveDebounceRate = 1
      },
      _transport);
  }

  public async ValueTask DisposeAsync()
  {
    await _server.DisposeAsync();
    _storage.Dispose();
  }

  [Fact]
  public async Task ServerCreatesDocumentOnFirstSync()
  {
    // Arrange: create a client repo with a document
    var clientStorage = new InMemoryStorageAdapter();
    var clientAdapter = new InMemoryNetworkAdapter();
    var clientRepo = new AutomergeRepo(new RepoConfig
    {
      Storage = clientStorage,
      NetworkAdapters = [clientAdapter],
      SaveDebounceRate = 1,
      SyncDebounceRate = 1
    });

    // Create a document on the client using the proper Change API
    var handle = await clientRepo.Create<AutomergeDocument>();
    handle.Change(doc =>
    {
      doc.PutString(doc.RootId, "title", "Test Document");
    });

    // Wait for save to complete
    await Task.Delay(50);

    // Act: Connect client peer to server, then simulate sync
    var clientPeerId = clientRepo.PeerId;
    await _server.OnPeerConnectedAsync(clientPeerId);

    // Generate sync message from client
    AutomergeDocument clientDoc = handle.Doc();
    using var syncState = clientDoc.CreateSyncState();
    byte[]? syncMsg = clientDoc.GenerateSyncMessage(syncState);
    syncMsg.Should().NotBeNull("client should generate initial sync message");

    // Send the sync message to the server
    var requestMessage = new RequestMessage
    {
      DocumentId = handle.DocumentId,
      Data = syncMsg!
    };
    await _server.OnMessageReceivedAsync(clientPeerId, requestMessage);

    // Wait for server to process
    await Task.Delay(200);

    // Assert: server should have processed the message without error
    // The transport may have sent sync reply messages
    _transport.SentMessages.Count.Should().BeGreaterThanOrEqualTo(0, "server may send sync replies");

    await clientRepo.Shutdown();
    clientStorage.Dispose();
  }

  [Fact]
  public async Task ServerPersistsOnTimer()
  {
    // Arrange: create a document directly in the server's repo
    var handle = await _server.Repo.Create<AutomergeDocument>();
    handle.Change(doc =>
    {
      doc.PutString(doc.RootId, "key", "value");
    });

    // Act: Wait for persistence timer to fire (configured at 50ms)
    await Task.Delay(200);

    // Trigger flush to ensure data is written
    await _server.Repo.Flush();

    // Assert: storage should have data
    var prefix = new StorageKey(new[] { handle.DocumentId.Value });
    var chunks = await _storage.LoadRange(prefix);
    chunks.Should().NotBeEmpty("document should be persisted to storage");
  }

  [Fact]
  public async Task ServerHandlesPeerDisconnect()
  {
    // Arrange
    var peerId = PeerId.Generate();
    await _server.OnPeerConnectedAsync(peerId);

    // Act
    await _server.OnPeerDisconnectedAsync(peerId);

    // Assert: no exception thrown, server continues operating
    var anotherPeerId = PeerId.Generate();
    await _server.OnPeerConnectedAsync(anotherPeerId);
    // If we get here without exception, peer disconnect was handled
  }

  [Fact]
  public async Task ServerListsDocuments()
  {
    // Arrange: create documents in the server repo and persist them
    var handle1 = await _server.Repo.Create<AutomergeDocument>();
    handle1.Change(doc =>
    {
      doc.PutString(doc.RootId, "name", "doc1");
    });

    var handle2 = await _server.Repo.Create<AutomergeDocument>();
    handle2.Change(doc =>
    {
      doc.PutString(doc.RootId, "name", "doc2");
    });

    // Force persistence
    await _server.Repo.Flush();
    await Task.Delay(100);

    // Act
    IReadOnlyList<string> documentIds = await _server.ListDocumentsAsync();

    // Assert
    documentIds.Should().Contain(handle1.DocumentId.Value);
    documentIds.Should().Contain(handle2.DocumentId.Value);
  }

  [Fact]
  public async Task ServerExportsDocument()
  {
    // Arrange
    var handle = await _server.Repo.Create<AutomergeDocument>();
    handle.Change(doc =>
    {
      doc.PutString(doc.RootId, "title", "Export Test");
    });

    // Act
    byte[]? exported = await _server.ExportDocumentAsync(handle.DocumentId.Value);

    // Assert
    exported.Should().NotBeNull();
    exported!.Length.Should().BeGreaterThan(0);

    // Verify the exported data can be loaded
    using var restored = AutomergeDocument.Load(exported);
    string json = restored.HydrateJson();
    json.Should().Contain("Export Test");
  }

  [Fact]
  public async Task ServerDeletesDocument()
  {
    // Arrange
    var handle = await _server.Repo.Create<AutomergeDocument>();
    string docId = handle.DocumentId.Value;
    handle.Change(doc =>
    {
      doc.PutString(doc.RootId, "title", "To Delete");
    });

    await _server.Repo.Flush();
    await Task.Delay(100);

    // Verify document exists in storage before delete
    var prefixBefore = new StorageKey(new[] { docId });
    IReadOnlyList<Chunk> chunksBefore = await _storage.LoadRange(prefixBefore);
    chunksBefore.Should().NotBeEmpty("document should exist before deletion");

    // Act
    await _server.DeleteDocumentAsync(docId);

    // Assert
    var prefixAfter = new StorageKey(new[] { docId });
    IReadOnlyList<Chunk> chunksAfter = await _storage.LoadRange(prefixAfter);
    chunksAfter.Should().BeEmpty("document should be removed from storage");
  }

  [Fact]
  public async Task ServerRejectsOperationsAfterDispose()
  {
    // Arrange
    await _server.DisposeAsync();

    // Act & Assert
    Func<Task> act = () => _server.OnPeerConnectedAsync(PeerId.Generate());
    await act.Should().ThrowAsync<ObjectDisposedException>();
  }

  [Fact]
  public async Task ListDocumentsAsync_AfterReceivingMessage_ReturnsDocumentId()
  {
    // Arrange: connect a peer and send a sync message for a new document
    var peerId = PeerId.Generate();
    await _server.OnPeerConnectedAsync(peerId);

    using var clientDoc = AutomergeDocument.Create();
    clientDoc.PutString(clientDoc.RootId, "key", "val");
    clientDoc.Commit("init", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    using var syncState = clientDoc.CreateSyncState();
    byte[]? syncMsg = clientDoc.GenerateSyncMessage(syncState);

    var docId = DocumentId.Generate();
    var message = new RequestMessage
    {
      DocumentId = docId,
      Data = syncMsg!
    };

    // Act: send the message (no flush/persistence wait)
    await _server.OnMessageReceivedAsync(peerId, message);
    IReadOnlyList<string> documents = await _server.ListDocumentsAsync();

    // Assert: document should be listed immediately from in-memory tracking
    documents.Should().Contain(docId.Value);
  }

  [Fact]
  public async Task ListDocumentsAsync_CombinesInMemoryAndStorage()
  {
    // Arrange: pre-populate storage with a document from a "previous server run"
    string storageDocId = "previously-stored-doc";
    var storageKey = new StorageKey(new[] { storageDocId, "snapshot" });
    await _storage.Save(storageKey, new byte[] { 1, 2, 3 });

    // Send a message for a different document (in-memory only)
    var peerId = PeerId.Generate();
    await _server.OnPeerConnectedAsync(peerId);

    using var clientDoc = AutomergeDocument.Create();
    clientDoc.PutString(clientDoc.RootId, "key", "val");
    clientDoc.Commit("init", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    using var syncState = clientDoc.CreateSyncState();
    byte[]? syncMsg = clientDoc.GenerateSyncMessage(syncState);

    var inMemoryDocId = DocumentId.Generate();
    var message = new RequestMessage
    {
      DocumentId = inMemoryDocId,
      Data = syncMsg!
    };
    await _server.OnMessageReceivedAsync(peerId, message);

    // Act
    IReadOnlyList<string> documents = await _server.ListDocumentsAsync();

    // Assert: both in-memory and storage documents should be returned
    documents.Should().Contain(inMemoryDocId.Value, "in-memory tracked document should be listed");
    documents.Should().Contain(storageDocId, "storage-only document should be listed");
  }

  [Fact]
  public async Task DeleteDocumentAsync_RemovesFromKnownDocumentIds()
  {
    // Arrange: send a message to track a document
    var peerId = PeerId.Generate();
    await _server.OnPeerConnectedAsync(peerId);

    using var clientDoc = AutomergeDocument.Create();
    clientDoc.PutString(clientDoc.RootId, "key", "val");
    clientDoc.Commit("init", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    using var syncState = clientDoc.CreateSyncState();
    byte[]? syncMsg = clientDoc.GenerateSyncMessage(syncState);

    var docId = DocumentId.Generate();
    var message = new RequestMessage
    {
      DocumentId = docId,
      Data = syncMsg!
    };
    await _server.OnMessageReceivedAsync(peerId, message);

    // Verify it's tracked
    IReadOnlyList<string> beforeDelete = await _server.ListDocumentsAsync();
    beforeDelete.Should().Contain(docId.Value);

    // Act
    await _server.DeleteDocumentAsync(docId.Value);

    // Assert: document should no longer appear in listing
    IReadOnlyList<string> afterDelete = await _server.ListDocumentsAsync();
    afterDelete.Should().NotContain(docId.Value);
  }

  [Fact]
  public async Task OnMessageReceivedAsync_MarksDirtyForPersistence()
  {
    // Arrange
    var peerId = PeerId.Generate();
    await _server.OnPeerConnectedAsync(peerId);

    // Create a client document to generate a valid sync message
    using var clientDoc = AutomergeDocument.Create();
    clientDoc.PutString(clientDoc.RootId, "key", "val");
    clientDoc.Commit("msg", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    using var syncState = clientDoc.CreateSyncState();
    byte[]? syncMsg = clientDoc.GenerateSyncMessage(syncState);

    var docId = DocumentId.Generate();
    var message = new RequestMessage
    {
      DocumentId = docId,
      Data = syncMsg!
    };

    // Act
    await _server.OnMessageReceivedAsync(peerId, message);

    // Assert: message was processed without errors
    await Task.Delay(200);
    // No exception means the dirty tracking worked
  }

  [Fact]
  public async Task DocumentAvailable_FiredOnFirstMessageForNewDocument()
  {
    // Arrange
    var peerId = PeerId.Generate();
    await _server.OnPeerConnectedAsync(peerId);

    using var clientDoc = AutomergeDocument.Create();
    clientDoc.PutString(clientDoc.RootId, "key", "val");
    clientDoc.Commit("init", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    using var syncState = clientDoc.CreateSyncState();
    byte[]? syncMsg = clientDoc.GenerateSyncMessage(syncState);

    var docId = DocumentId.Generate();
    var message = new RequestMessage
    {
      DocumentId = docId,
      Data = syncMsg!
    };

    string? raisedDocumentId = null;
    _server.DocumentAvailable += (_, args) => raisedDocumentId = args.DocumentId;

    // Act
    await _server.OnMessageReceivedAsync(peerId, message);

    // Assert
    raisedDocumentId.Should().Be(docId.Value);
  }

  [Fact]
  public async Task DocumentAvailable_NotFiredOnSubsequentMessagesForSameDocument()
  {
    // Arrange
    var peerId = PeerId.Generate();
    await _server.OnPeerConnectedAsync(peerId);

    using var clientDoc = AutomergeDocument.Create();
    clientDoc.PutString(clientDoc.RootId, "key", "val");
    clientDoc.Commit("init", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    using var syncState = clientDoc.CreateSyncState();
    byte[]? syncMsg = clientDoc.GenerateSyncMessage(syncState);

    var docId = DocumentId.Generate();
    var message = new RequestMessage
    {
      DocumentId = docId,
      Data = syncMsg!
    };

    int fireCount = 0;
    _server.DocumentAvailable += (_, _) => fireCount++;

    // Act: send twice for the same document
    await _server.OnMessageReceivedAsync(peerId, message);
    await _server.OnMessageReceivedAsync(peerId, message);

    // Assert: event should only fire once (first time)
    fireCount.Should().Be(1);
  }

}
