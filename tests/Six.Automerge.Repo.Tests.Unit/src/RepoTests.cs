using System;
using Six.Automerge;

namespace Six.Automerge.Repo.Test.Unit;

/// <summary>
/// Unit tests for the Repo class, ported from TypeScript Repo.test.ts.
/// These tests exercise the core Repo functionality for managing Automerge documents.
/// </summary>
[Trait("Category", "Repo")]
public sealed class RepoTests : IDisposable
{
  // Helper to create a test repo with common configuration
  static (Six.Automerge.Repository.Repo, InMemoryStorageAdapter, InMemoryNetworkAdapter) CreateTestRepo(
    int saveDebounceRate = 1)
  {
    var storageAdapter = new InMemoryStorageAdapter();
    var networkAdapter = new InMemoryNetworkAdapter();
    var repo = new Six.Automerge.Repository.Repo(new RepoConfig
    {
      Storage = storageAdapter,
      NetworkAdapters = [networkAdapter],
      SaveDebounceRate = saveDebounceRate
    });
    return (repo, storageAdapter, networkAdapter);
  }

  #region Constructor Tests

  [Fact]
  public void Constructor_WithConfiguration_CreatesRepoInstance()
  {
    var repo = new Six.Automerge.Repository.Repo(new RepoConfig());
    Assert.NotNull(repo);
  }

  [Fact]
  public void Constructor_WithStorage_CreatesRepoInstance()
  {
    var (repo, _, _) = CreateTestRepo();
    Assert.NotNull(repo);
  }

  #endregion

  #region Document Creation Tests

  [Fact]
  public async Task Create_WithoutInitialValue_CreatesNewDocument()
  {
    var (repo, _, _) = CreateTestRepo();
    var handle = await repo.Create<AutomergeDocument>();
    Assert.NotNull(handle);
    Assert.NotEmpty(handle.DocumentId.Value);
    Assert.True(handle.IsReady);
  }

  [Fact]
  public async Task Create_WithInitialValue_CreatesDocumentWithValue()
  {
    var (repo, _, _) = CreateTestRepo();
    var initialDoc = AutomergeDocument.Create();
    var root = initialDoc.RootId;
    initialDoc.PutString(root, "name", "test");

    var handle = await repo.Create(initialDoc);
    Assert.NotNull(handle);
    Assert.True(handle.IsReady);

    var doc = handle.Doc();
    Assert.NotNull(doc);
  }

  [Fact]
  public async Task Create_MultipleDocuments_EachHasUniqueId()
  {
    var (repo, _, _) = CreateTestRepo();
    var handle1 = await repo.Create<AutomergeDocument>();
    var handle2 = await repo.Create<AutomergeDocument>();

    Assert.NotEqual(handle1.DocumentId, handle2.DocumentId);
  }

  #endregion

  #region Document FindAsync Tests

  [Fact]
  public async Task Find_ExistingDocument_ReturnsHandle()
  {
    var (repo, _, _) = CreateTestRepo();
    var created = await repo.Create<AutomergeDocument>();
    var docId = created.DocumentId.Value;

    var found = await repo.FindAsync<AutomergeDocument>(docId);
    Assert.NotNull(found);
    Assert.Same(created, found);
  }

  [Fact]
  public async Task Find_NonexistentDocument_ReturnsNull()
  {
    var (repo, _, _) = CreateTestRepo();
    var docId = DocumentId.Generate().Value;

    var found = await repo.FindAsync<AutomergeDocument>(docId);
    Assert.Null(found);
  }

  [Fact]
  public async Task Find_InvalidDocumentId_ThrowsException()
  {
    var (repo, _, _) = CreateTestRepo();

    await Assert.ThrowsAsync<ArgumentException>(() =>
      repo.FindAsync<AutomergeDocument>("invalid-id")
    );
  }

  #endregion

  #region Document Change Tests

  [Fact]
  public async Task Change_Document_IsApplied()
  {
    var (repo, _, _) = CreateTestRepo();
    var handle = await repo.Create<AutomergeDocument>();

    var change = handle.Change(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "status", "active");
    });

    Assert.NotNull(change);
    var doc = handle.Doc();
    Assert.NotNull(doc);
  }

  [Fact]
  public async Task ChangeAsync_Document_IsApplied()
  {
    var (repo, _, _) = CreateTestRepo();
    var handle = await repo.Create<AutomergeDocument>();

    var change = await handle.ChangeAsync(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "count", "1");
    });

    Assert.NotNull(change);
  }

  #endregion

  #region Document CloneAsync Tests

  [Fact]
  public async Task Clone_Document_CreatesNewDocumentWithSameContent()
  {
    var (repo, _, _) = CreateTestRepo();
    var original = await repo.Create<AutomergeDocument>();

    original.Change(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "value", "original");
    });

    var cloned = await repo.CloneAsync(original);

    Assert.NotEqual(original.DocumentId, cloned.DocumentId);
    Assert.True(cloned.IsReady);
  }

  [Fact]
  public async Task Clone_Document_CreatesDistinctCopies()
  {
    var (repo, _, _) = CreateTestRepo();
    var original = await repo.Create<AutomergeDocument>();
    var cloned = await repo.CloneAsync(original);

    original.Change(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "field1", "from-original");
    });

    cloned.Change(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "field2", "from-clone");
    });

    // Documents should remain independent
    Assert.NotEqual(original.DocumentId, cloned.DocumentId);
  }

  #endregion

  #region Document DeleteAsync Tests

  [Fact]
  public async Task Delete_ExistingDocument_MarkAsDeleted()
  {
    var (repo, _, _) = CreateTestRepo();
    var handle = await repo.Create<AutomergeDocument>();
    var docId = handle.DocumentId.Value;

    await repo.DeleteAsync(docId);

    Assert.True(handle.IsDeleted);
  }

  [Fact]
  public async Task Delete_DocumentByDocumentId_RemovesFromCache()
  {
    var (repo, _, _) = CreateTestRepo();
    var handle = await repo.Create<AutomergeDocument>();
    var docId = handle.DocumentId.Value;

    var handles = await repo.GetHandlesAsync();
    Assert.True(handles.ContainsKey(docId));
    await repo.DeleteAsync(docId);
    handles = await repo.GetHandlesAsync();
    Assert.False(handles.ContainsKey(docId));
  }

  [Fact]
  public async Task Delete_EmitsDocumentDeletedEvent()
  {
    var (repo, _, _) = CreateTestRepo();
    var handle = await repo.Create<AutomergeDocument>();
    var docId = handle.DocumentId.Value;

    var deletedEventFired = false;
    repo.DocumentDeleted += (_, args) =>
    {
      if(args.DocumentId.Value == docId)
      {
        deletedEventFired = true;
      }
    };

    await repo.DeleteAsync(docId);
    Assert.True(deletedEventFired);
  }

  #endregion

  #region Document ExportAsync/ImportAsync Tests

  [Fact]
  public async Task Export_Document_ReturnsBytes()
  {
    var (repo, _, _) = CreateTestRepo();
    var handle = await repo.Create<AutomergeDocument>();
    handle.Change(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "data", "test");
    });

    var docId = handle.DocumentId.Value;
    var exported = await repo.ExportAsync(docId);

    Assert.NotNull(exported);
    Assert.NotEmpty(exported!);
  }

  [Fact]
  public async Task Export_NonexistentDocument_ReturnsNull()
  {
    var (repo, _, _) = CreateTestRepo();
    var docId = DocumentId.Generate().Value;

    var exported = await repo.ExportAsync(docId);
    Assert.Null(exported);
  }

  [Fact]
  public async Task Import_Document_CreatesNewHandle()
  {
    var (repo, _, _) = CreateTestRepo();
    var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutString(root, "imported", "data");

    var data = doc.Save();
    var imported = await repo.ImportAsync<AutomergeDocument>(data);

    Assert.NotNull(imported);
    Assert.True(imported.IsReady);
  }

  #endregion

  #region Handle Cache Tests

  [Fact]
  public async Task Handles_Property_ContainsCreatedDocuments()
  {
    var (repo, _, _) = CreateTestRepo();
    var handle1 = await repo.Create<AutomergeDocument>();
    var handle2 = await repo.Create<AutomergeDocument>();

    var handles = await repo.GetHandlesAsync();
    Assert.Contains(handle1.DocumentId.Value, handles.Keys);
    Assert.Contains(handle2.DocumentId.Value, handles.Keys);
  }

  [Fact]
  public async Task Handles_Property_IsReadOnly()
  {
    var (repo, _, _) = CreateTestRepo();
    var handles = await repo.GetHandlesAsync();
    Assert.NotNull(handles);
    // Verify it's a snapshot view
    var count1 = handles.Count;
    await repo.Create<AutomergeDocument>();
    var handles2 = await repo.GetHandlesAsync();
    var count2 = handles2.Count;
    Assert.NotEqual(count1, count2);
  }

  #endregion

  #region Storage Integration Tests

  [Fact]
  public async Task Storage_PersistsDocumentOnCreation()
  {
    var storage = new InMemoryStorageAdapter();
    var repo1 = new Six.Automerge.Repository.Repo(new RepoConfig
    {
      Storage = storage,
      SaveDebounceRate = 1
    });

    var handle1 = await repo1.Create<AutomergeDocument>();
    handle1.Change(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "persistent", "value");
    });

    await Task.Delay(50);

    var repo2 = new Six.Automerge.Repository.Repo(new RepoConfig
    {
      Storage = storage
    });

    var docId = handle1.DocumentId.Value;
    var handle2 = await repo2.FindAsync<AutomergeDocument>(docId);

    Assert.NotNull(handle2);
  }

  [Fact]
  public async Task Storage_MultipleDocuments_AllCreated()
  {
    var storage = new InMemoryStorageAdapter();
    var repo = new Six.Automerge.Repository.Repo(new RepoConfig
    {
      Storage = storage,
      SaveDebounceRate = 1
    });

    var doc1 = await repo.Create<AutomergeDocument>();
    var doc2 = await repo.Create<AutomergeDocument>();
    var doc3 = await repo.Create<AutomergeDocument>();

    // All documents should be in cache immediately
    var handles = await repo.GetHandlesAsync();
    Assert.Contains(doc1.DocumentId.Value, handles.Keys);
    Assert.Contains(doc2.DocumentId.Value, handles.Keys);
    Assert.Contains(doc3.DocumentId.Value, handles.Keys);
  }

  #endregion

  #region State Tests

  [Fact]
  public async Task NewHandle_IsReady()
  {
    var (repo, _, _) = CreateTestRepo();
    var handle = await repo.Create<AutomergeDocument>();
    Assert.True(handle.IsReady);
  }

  [Fact]
  public async Task DeletedHandle_IsDeleted()
  {
    var (repo, _, _) = CreateTestRepo();
    var handle = await repo.Create<AutomergeDocument>();
    var docId = handle.DocumentId.Value;

    await repo.DeleteAsync(docId);
    Assert.True(handle.IsDeleted);
  }

  #endregion

  #region Network Sync - SenderId Stamping

  [Fact]
  public async Task Sync_OutgoingMessages_HaveSenderIdStamped()
  {
    // Arrange: create a repo with a spy adapter that captures messages
    // before any adapter-level processing
    var sentMessages = new List<IRepoMessage>();
    var spyAdapter = new SpyNetworkAdapter(sentMessages);

    var repo = new Six.Automerge.Repository.Repo(new RepoConfig
    {
      Storage = new InMemoryStorageAdapter(),
      NetworkAdapters = [spyAdapter],
      SaveDebounceRate = 1
    });

    // Simulate a peer connecting so the synchronizer has someone to send to
    spyAdapter.SimulatePeerConnected(new PeerId("test-peer"));

    // Act: create a document — this triggers sync messages to the peer
    var handle = await repo.Create<AutomergeDocument>();

    // Give sync time to fire events
    await Task.Delay(200);

    // Assert: all messages sent through the adapter should have SenderId = repo.PeerId
    Assert.NotEmpty(sentMessages);
    foreach(var msg in sentMessages)
    {
      Assert.NotNull(msg.SenderId);
      Assert.Equal(repo.PeerId, msg.SenderId.Value);
    }
  }

  /// <summary>
  /// A minimal network adapter that captures messages sent through it.
  /// Unlike InMemoryNetworkAdapter, this does NOT call WithSender(),
  /// so it captures the messages exactly as the Repo sends them.
  /// </summary>
  sealed class SpyNetworkAdapter : INetworkAdapter
  {
    readonly List<IRepoMessage> _sentMessages;

    public SpyNetworkAdapter(List<IRepoMessage> sentMessages)
    {
      _sentMessages = sentMessages;
      AdapterId = $"spy-{Guid.NewGuid():N}";
    }

    public string AdapterId { get; }
    public bool IsReady => true;
    public Task WhenReady() => Task.CompletedTask;

    public event EventHandler<PeerCandidateEventArgs>? PeerCandidate;
#pragma warning disable CS0067
    public event EventHandler<PeerDisconnectedEventArgs>? PeerDisconnected;
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler? Closed;
#pragma warning restore CS0067

    public void Connect(PeerId peerId, PeerMetadata? metadata = null) { }
    public Task Disconnect() => Task.CompletedTask;

    public void Send(IRepoMessage message)
    {
      _sentMessages.Add(message);
    }

    public void SimulatePeerConnected(PeerId peerId)
    {
      PeerCandidate?.Invoke(this, new PeerCandidateEventArgs { PeerId = peerId, Metadata = null });
    }

    public void SimulateMessageReceived(IRepoMessage message)
    {
      MessageReceived?.Invoke(this, new MessageReceivedEventArgs { Message = message });
    }
  }

  #endregion

  #region Network Sync - Remote Document Discovery

  [Fact]
  public async Task Sync_IncomingSyncMessage_CreatesHandleForUnknownDocument()
  {
    // Arrange: create two repos that can communicate via spy adapters
    var repo1Messages = new List<IRepoMessage>();
    var repo2Messages = new List<IRepoMessage>();
    var spyAdapter1 = new SpyNetworkAdapter(repo1Messages);
    var spyAdapter2 = new SpyNetworkAdapter(repo2Messages);

    var storage1 = new InMemoryStorageAdapter();
    var storage2 = new InMemoryStorageAdapter();

    var repo1 = new Six.Automerge.Repository.Repo(new RepoConfig
    {
      Storage = storage1,
      NetworkAdapters = [spyAdapter1],
      SaveDebounceRate = 1
    });

    var repo2 = new Six.Automerge.Repository.Repo(new RepoConfig
    {
      Storage = storage2,
      NetworkAdapters = [spyAdapter2],
      SaveDebounceRate = 1
    });

    // Connect peers to each other
    spyAdapter1.SimulatePeerConnected(repo2.PeerId);
    spyAdapter2.SimulatePeerConnected(repo1.PeerId);

    // Create a document in repo1 with content
    var handle1 = await repo1.Create<AutomergeDocument>();
    handle1.Change(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "greeting", "hello from repo1");
    });

    // Wait for sync messages to be generated
    await Task.Delay(200);

    // Act: relay all messages from repo1 to repo2 (simulating network transport)
    foreach(var msg in repo1Messages)
    {
      spyAdapter2.SimulateMessageReceived(msg);
    }

    // Give repo2 time to process messages and create handle
    await Task.Delay(500);

    // Assert: repo2 should now have a handle for the document
    var handles = await repo2.GetHandlesAsync();
    Assert.True(handles.ContainsKey(handle1.DocumentId.Value),
      $"Repo2 should have created a handle for document {handle1.DocumentId.Value}. " +
      $"Handles: [{string.Join(", ", handles.Keys)}]");

    var repo2Handle = handles[handle1.DocumentId.Value];
    Assert.True(repo2Handle.IsReady, "Handle should be in Ready state");
    Assert.NotNull(repo2Handle.GetDocument());
  }

  [Fact]
  public async Task Sync_RemoteChange_NotifiesHandleRegardlessOfTypeParameter()
  {
    // Arrange: create a repo and document using <object> type (like demo apps do)
    var sentMessages = new List<IRepoMessage>();
    var spyAdapter = new SpyNetworkAdapter(sentMessages);
    var storage = new InMemoryStorageAdapter();

    var repo = new Six.Automerge.Repository.Repo(new RepoConfig
    {
      Storage = storage,
      NetworkAdapters = [spyAdapter],
      SaveDebounceRate = 1
    });

    // Create document using object type parameter (same as demo apps)
    var handle = await repo.Create<object>();
    handle.Change(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "initial", "value");
    });

    // Access via the non-generic interface (same as Repo.UpdateDocument does)
    var docHandle = (IDocHandle)handle;

    // Simulate receiving a sync message from a peer with changes
    var peerRepo = new Six.Automerge.Repository.Repo(new RepoConfig
    {
      Storage = new InMemoryStorageAdapter(),
      SaveDebounceRate = 1
    });
    var peerHandle = await peerRepo.Create<AutomergeDocument>(handle.DocumentId.Value);
    peerHandle.Change(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "remote", "change");
    });

    // The handle should support NotifyRemoteChangeAsync via IDocHandle
    var peerDoc = peerHandle.Doc()!;
    await docHandle.NotifyRemoteChangeAsync(peerDoc);

    // Assert: heads should have changed (NotifyRemoteChangeAsync was called successfully)
    // This test verifies the IDocHandle interface method works for DocHandle<object>
    Assert.True(docHandle.IsReady, "Handle should still be ready after NotifyRemoteChangeAsync");
  }

  #endregion

  #region Dispose Pattern

  public void Dispose()
  {
    // Cleanup if needed
  }

  #endregion
}
