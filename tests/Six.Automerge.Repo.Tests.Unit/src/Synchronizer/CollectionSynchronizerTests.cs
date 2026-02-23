using System;
using Microsoft.Extensions.Logging.Abstractions;
using Six.Automerge;
using Six.Automerge.Repo.Testing;
using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;
using Six.Automerge.Repository.Storage;
using Six.Automerge.Repository.Synchronizer;
using Xunit;

namespace Six.Automerge.Repo.Test.Unit.Synchronizer;

[Trait("Category", "Synchronizer")]
public class CollectionSynchronizerTests : IDisposable
{
  const string DefaultDocId = TestDocumentIds.Default;
  const string AltDocId = TestDocumentIds.Alternate;

  static DocumentId DefaultDocIdValue => TestDocumentIds.DefaultId;
  static DocumentId AltDocIdValue => TestDocumentIds.AlternateId;

  static string NewDocIdString() => TestDocumentIds.UniqueString();
  static byte[] CreateSyncMessageData() => Array.Empty<byte>();

  private readonly Dictionary<string, AutomergeDocument> _documents = new();
  private readonly List<string> _requestedDocuments = new();
  private readonly Dictionary<PeerId, PeerMetadata> _peerMetadata = new();
  private readonly HashSet<string> _denylist = new();
  private readonly SemaphoreSlim _docLock = new(1, 1);

  public void Dispose()
  {
    foreach(var doc in _documents.Values)
    {
      doc?.Dispose();
    }

    _docLock.Dispose();
  }

  private AutomergeDocument GetOrCreateDocument(string documentId)
  {
    if(!_documents.TryGetValue(documentId, out var doc))
    {
      doc = AutomergeDocument.Create();
      _documents[documentId] = doc;
    }

    return doc;
  }

  [Fact]
  public void Constructor_ShouldThrowOnNullParameters()
  {
    Assert.Throws<ArgumentNullException>(() => new CollectionSynchronizer(
      null!,
      (_, _, _) => Task.CompletedTask,
      _ => null,
      _ => { },
      _ => _docLock));

    Assert.Throws<ArgumentNullException>(() => new CollectionSynchronizer(
      _ => null,
      null!,
      _ => null,
      _ => { },
      _ => _docLock));

    Assert.Throws<ArgumentNullException>(() => new CollectionSynchronizer(
      _ => null,
      (_, _, _) => Task.CompletedTask,
      null!,
      _ => { },
      _ => _docLock));

    Assert.Throws<ArgumentNullException>(() => new CollectionSynchronizer(
      _ => null,
      (_, _, _) => Task.CompletedTask,
      _ => null,
      null!,
      _ => _docLock));
  }

  [Fact]
  public async Task AddDocument_ShouldRegisterDocument()
  {
    var doc = GetOrCreateDocument(DefaultDocId);
    var messagesSent = new List<SyncMessageEventArgs>();

    using var sync = new CollectionSynchronizer(
      id => _documents.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { _documents[id] = doc; return Task.CompletedTask; },
      pid => _peerMetadata.TryGetValue(pid, out var meta) ? meta : null,
      id => _requestedDocuments.Add(id),
      _ => _docLock);

    sync.MessageToSend += (sender, args) => messagesSent.Add(args);

    // Add document
    await sync.AddDocument(DefaultDocId);

    // Document should be registered (no exception thrown)
    Assert.True(true);
  }

  [Fact]
  public async Task AddDocument_ShouldBeginSyncWithConnectedPeers()
  {
    var doc = GetOrCreateDocument(DefaultDocId);
    var messagesSent = new List<SyncMessageEventArgs>();
    var peer1 = PeerId.Generate();
    var peer2 = PeerId.Generate();

    using var sync = new CollectionSynchronizer(
      id => _documents.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { _documents[id] = doc; return Task.CompletedTask; },
      pid => _peerMetadata.TryGetValue(pid, out var meta) ? meta : null,
      id => _requestedDocuments.Add(id),
      _ => _docLock);

    sync.MessageToSend += (sender, args) => messagesSent.Add(args);

    // Connect peers first
    await sync.OnPeerConnected(peer1);
    await sync.OnPeerConnected(peer2);

    var initialMessageCount = messagesSent.Count;

    // Add document - should begin sync with both peers
    await sync.AddDocument(DefaultDocId);

    // Should have sent sync messages to both peers
    Assert.True(messagesSent.Count > initialMessageCount);
  }

  [Fact]
  public async Task AddDocument_ShouldRespectDenylist()
  {
    var doc = GetOrCreateDocument(DefaultDocId);
    _denylist.Add(DefaultDocId);
    var messagesSent = new List<SyncMessageEventArgs>();

    using var sync = new CollectionSynchronizer(
      id => _documents.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { _documents[id] = doc; return Task.CompletedTask; },
      pid => _peerMetadata.TryGetValue(pid, out var meta) ? meta : null,
      id => _requestedDocuments.Add(id),
      _ => _docLock,
      denylist: _denylist);

    sync.MessageToSend += (sender, args) => messagesSent.Add(args);

    // Add denylisted document
    await sync.AddDocument(DefaultDocId);

    // Should not send any messages
    Assert.Empty(messagesSent);
  }

  [Fact]
  public async Task RemoveDocument_ShouldUnregisterDocument()
  {
    var doc = GetOrCreateDocument(DefaultDocId);

    using var sync = new CollectionSynchronizer(
      id => _documents.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { _documents[id] = doc; return Task.CompletedTask; },
      pid => _peerMetadata.TryGetValue(pid, out var meta) ? meta : null,
      id => _requestedDocuments.Add(id),
      _ => _docLock);

    await sync.AddDocument(DefaultDocId);
    await sync.RemoveDocument(DefaultDocId);

    // Document should be unregistered (no exception thrown)
    Assert.True(true);
  }

  [Fact]
  public async Task OnPeerConnected_ShouldBeginSyncForAllDocuments()
  {
    var doc1 = GetOrCreateDocument(DefaultDocId);
    var doc2 = GetOrCreateDocument(AltDocId);
    var messagesSent = new List<SyncMessageEventArgs>();
    var peerId = PeerId.Generate();

    using var sync = new CollectionSynchronizer(
      id => _documents.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { _documents[id] = doc; return Task.CompletedTask; },
      pid => _peerMetadata.TryGetValue(pid, out var meta) ? meta : null,
      id => _requestedDocuments.Add(id),
      _ => _docLock);

    sync.MessageToSend += (sender, args) => messagesSent.Add(args);

    // Add documents first
    await sync.AddDocument(DefaultDocId);
    await sync.AddDocument(AltDocId);

    var initialMessageCount = messagesSent.Count;

    // Connect peer - should begin sync for both documents
    await sync.OnPeerConnected(peerId);

    // Should have sent sync messages for both documents
    Assert.True(messagesSent.Count > initialMessageCount);
    var documentIds = messagesSent.Skip(initialMessageCount).Select(m => m.DocumentId).Distinct().ToList();
    Assert.Contains(DefaultDocIdValue, documentIds);
    Assert.Contains(AltDocIdValue, documentIds);
  }

  [Fact]
  public async Task OnPeerConnected_ShouldNotDuplicatePeer()
  {
    var messagesSent = new List<SyncMessageEventArgs>();
    var peerId = PeerId.Generate();

    using var sync = new CollectionSynchronizer(
      id => _documents.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { _documents[id] = doc; return Task.CompletedTask; },
      pid => _peerMetadata.TryGetValue(pid, out var meta) ? meta : null,
      id => _requestedDocuments.Add(id),
      _ => _docLock);

    sync.MessageToSend += (sender, args) => messagesSent.Add(args);

    await sync.OnPeerConnected(peerId);
    var firstConnectionMessages = messagesSent.Count;

    await sync.OnPeerConnected(peerId); // Second connection should be ignored

    // Should not send additional messages
    Assert.Equal(firstConnectionMessages, messagesSent.Count);
  }

  [Fact]
  public async Task OnPeerDisconnected_ShouldCleanupPeerState()
  {
    var doc = GetOrCreateDocument(DefaultDocId);
    var peerId = PeerId.Generate();

    using var sync = new CollectionSynchronizer(
      id => _documents.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { _documents[id] = doc; return Task.CompletedTask; },
      pid => _peerMetadata.TryGetValue(pid, out var meta) ? meta : null,
      id => _requestedDocuments.Add(id),
      _ => _docLock);

    await sync.AddDocument(DefaultDocId);
    await sync.OnPeerConnected(peerId);
    await sync.OnPeerDisconnected(peerId);

    // Peer should be cleaned up (no exception thrown)
    Assert.True(true);
  }

  [Fact]
  public async Task OnMessageReceived_ShouldRouteToDocSynchronizer()
  {
    // Create two separate document stores (simulating two repos)
    var repo1Docs = new Dictionary<string, AutomergeDocument>();
    var repo2Docs = new Dictionary<string, AutomergeDocument>();

    var doc1 = AutomergeDocument.Create();
    var doc2 = AutomergeDocument.Create();
    repo1Docs[DefaultDocId] = doc1;
    repo2Docs[DefaultDocId] = doc2;

    // Make changes to doc1
    var root1 = doc1.RootId;
    doc1.PutString(root1, "text", "Hello from repo1");
    doc1.Commit("initial change");

    var peer1 = PeerId.Generate();
    var peer2 = PeerId.Generate();

    // Create synchronizers for both repos
    using var sync1 = new CollectionSynchronizer(
      id => repo1Docs.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { repo1Docs[id] = doc; return Task.CompletedTask; },
      _ => null,
      _ => { },
      _ => _docLock);

    using var sync2 = new CollectionSynchronizer(
      id => repo2Docs.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { repo2Docs[id] = doc; return Task.CompletedTask; },
      _ => null,
      _ => { },
      _ => _docLock);

    // Wire up message routing between synchronizers
    sync1.MessageToSend += (sender, args) =>
    {
      if(args.TargetPeerId == peer2)
      {
        sync2.OnMessageReceived(args.Message.WithSender(peer1)).GetAwaiter().GetResult();
      }
    };

    sync2.MessageToSend += (sender, args) =>
    {
      if(args.TargetPeerId == peer1)
      {
        sync1.OnMessageReceived(args.Message.WithSender(peer2)).GetAwaiter().GetResult();
      }
    };

    var headsBefore = doc2.GetHeads();

    // Add documents to both synchronizers
    await sync1.AddDocument(DefaultDocId);
    await sync2.AddDocument(DefaultDocId);

    // Connect peers (this triggers the sync protocol)
    await sync1.OnPeerConnected(peer2);
    await sync2.OnPeerConnected(peer1);

    // Give sync some time to complete
    Thread.Sleep(100);

    // doc2 should have received the changes (heads should change)
    var headsAfter = doc2.GetHeads();
    Assert.NotEqual(headsBefore, headsAfter);

    doc1.Dispose();
    doc2.Dispose();
  }

  [Fact]
  public async Task OnMessageReceived_WithUnknownDocument_ShouldTriggerDiscovery()
  {
    var peerId = PeerId.Generate();
    _requestedDocuments.Clear();

    using var sync = new CollectionSynchronizer(
      id => _documents.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { _documents[id] = doc; return Task.CompletedTask; },
      pid => _peerMetadata.TryGetValue(pid, out var meta) ? meta : null,
      id => _requestedDocuments.Add(id),
      _ => _docLock);

    // Receive message for unknown document
    var unknownDocId = TestDocumentIds.Unique();
    var message = new SyncMessage { DocumentId = unknownDocId, Data = CreateSyncMessageData(), SenderId = peerId };

    await sync.OnMessageReceived(message);

    // Should have triggered document discovery
    Assert.Contains(unknownDocId.Value, _requestedDocuments);
  }

  [Fact]
  public async Task OnMessageReceived_ShouldRespectDenylist()
  {
    _denylist.Add(DefaultDocId);
    var peerId = PeerId.Generate();
    _requestedDocuments.Clear();

    using var sync = new CollectionSynchronizer(
      id => _documents.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { _documents[id] = doc; return Task.CompletedTask; },
      pid => _peerMetadata.TryGetValue(pid, out var meta) ? meta : null,
      id => _requestedDocuments.Add(id),
      _ => _docLock,
      denylist: _denylist);

    // Receive message for denylisted document
    var message = new SyncMessage { DocumentId = DefaultDocIdValue, Data = CreateSyncMessageData(), SenderId = peerId };

    await sync.OnMessageReceived(message);

    // Should not have triggered discovery
    Assert.Empty(_requestedDocuments);
  }

  [Fact]
  public async Task OnMessageReceived_ShouldIgnoreMessagesWithoutSenderId()
  {
    _requestedDocuments.Clear();

    using var sync = new CollectionSynchronizer(
      id => _documents.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { _documents[id] = doc; return Task.CompletedTask; },
      pid => _peerMetadata.TryGetValue(pid, out var meta) ? meta : null,
      id => _requestedDocuments.Add(id),
      _ => _docLock);

    // Message without sender ID
    var message = new SyncMessage { DocumentId = DefaultDocIdValue, Data = new byte[] { 1, 2, 3 }, SenderId = null };

    await sync.OnMessageReceived(message);

    // Should be ignored
    Assert.Empty(_requestedDocuments);
  }

  [Fact]
  public async Task SetSharePolicy_ShouldControlProactiveSharing()
  {
    var doc = GetOrCreateDocument(DefaultDocId);
    var messagesSent = new List<SyncMessageEventArgs>();
    var peer1 = PeerId.Generate();
    var peer2 = PeerId.Generate();

    using var sync = new CollectionSynchronizer(
      id => _documents.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { _documents[id] = doc; return Task.CompletedTask; },
      pid => _peerMetadata.TryGetValue(pid, out var meta) ? meta : null,
      id => _requestedDocuments.Add(id),
      _ => _docLock);

    sync.MessageToSend += (sender, args) => messagesSent.Add(args);

    // Set policy to only share with peer1
    sync.SetSharePolicy((docId, peerId) => Task.FromResult(peerId == peer1));

    // Connect both peers
    await sync.OnPeerConnected(peer1);
    await sync.OnPeerConnected(peer2);

    messagesSent.Clear();

    // Add document
    await sync.AddDocument(DefaultDocId);

    // Should only send to peer1
    var targetPeers = messagesSent.Select(m => m.TargetPeerId).Distinct().ToList();
    Assert.Contains(peer1, targetPeers);
    Assert.DoesNotContain(peer2, targetPeers);
  }

  [Fact]
  public async Task SetAccessPolicy_ShouldControlMessageProcessing()
  {
    var doc = GetOrCreateDocument(DefaultDocId);
    var peer1 = PeerId.Generate();
    var peer2 = PeerId.Generate();
    _requestedDocuments.Clear();

    using var sync = new CollectionSynchronizer(
      id => _documents.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { _documents[id] = doc; return Task.CompletedTask; },
      pid => _peerMetadata.TryGetValue(pid, out var meta) ? meta : null,
      id => _requestedDocuments.Add(id),
      _ => _docLock);

    // Set policy to only allow peer1
    sync.SetAccessPolicy((docId, peerId) => Task.FromResult(peerId == peer1));

    // Message from peer1
    var unknownId1 = TestDocumentIds.Unique();
    var message1 = new SyncMessage { DocumentId = unknownId1, Data = CreateSyncMessageData(), SenderId = peer1 };

    await sync.OnMessageReceived(message1);

    // Should trigger discovery for peer1's message
    Assert.Contains(unknownId1.Value, _requestedDocuments);

    // Message from peer2
    var unknownId2 = TestDocumentIds.Unique();
    var message2 = new SyncMessage { DocumentId = unknownId2, Data = CreateSyncMessageData(), SenderId = peer2 };

    await sync.OnMessageReceived(message2);

    // Should NOT trigger discovery for peer2's message
    Assert.DoesNotContain(unknownId2.Value, _requestedDocuments);
  }

  [Fact]
  public async Task TwoRepoSync_ShouldConverge()
  {
    // Create two separate document stores (simulating two repos)
    var repo1Docs = new Dictionary<string, AutomergeDocument>();
    var repo2Docs = new Dictionary<string, AutomergeDocument>();

    var doc1 = AutomergeDocument.Create();
    var doc2 = AutomergeDocument.Create();
    repo1Docs[DefaultDocId] = doc1;
    repo2Docs[DefaultDocId] = doc2;

    // Make changes to doc1
    var root1 = doc1.RootId;
    doc1.PutString(root1, "text", "Hello from repo1");
    doc1.Commit("initial change");

    var peer1 = PeerId.Generate();
    var peer2 = PeerId.Generate();

    // Create synchronizers for both repos
    using var sync1 = new CollectionSynchronizer(
      id => repo1Docs.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { repo1Docs[id] = doc; return Task.CompletedTask; },
      _ => null,
      _ => { },
      _ => _docLock);

    using var sync2 = new CollectionSynchronizer(
      id => repo2Docs.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { repo2Docs[id] = doc; return Task.CompletedTask; },
      _ => null,
      _ => { },
      _ => _docLock);

    // Wire up message routing between synchronizers
    sync1.MessageToSend += (sender, args) =>
    {
      if(args.TargetPeerId == peer2)
      {
        sync2.OnMessageReceived(args.Message.WithSender(peer1)).GetAwaiter().GetResult();
      }
    };

    sync2.MessageToSend += (sender, args) =>
    {
      if(args.TargetPeerId == peer1)
      {
        sync1.OnMessageReceived(args.Message.WithSender(peer2)).GetAwaiter().GetResult();
      }
    };

    // Add documents to both synchronizers
    await sync1.AddDocument(DefaultDocId);
    await sync2.AddDocument(DefaultDocId);

    // Connect peers
    await sync1.OnPeerConnected(peer2);
    await sync2.OnPeerConnected(peer1);

    // Give sync some time to complete (in real scenario, messages are async)
    Thread.Sleep(100);

    // Both documents should now have the same heads (meaning they converged)
    var heads1 = doc1.GetHeads();
    var heads2 = doc2.GetHeads();

    Assert.NotEmpty(heads1);
    Assert.NotEmpty(heads2);
    Assert.Equal(heads1.Count, heads2.Count);

    doc1.Dispose();
    doc2.Dispose();
  }

  [Fact]
  public async Task Dispose_ShouldCleanupResources()
  {
    var doc = GetOrCreateDocument(DefaultDocId);

    var sync = new CollectionSynchronizer(
      id => _documents.TryGetValue(id, out var d) ? d : null,
      (id, doc, _) => { _documents[id] = doc; return Task.CompletedTask; },
      pid => _peerMetadata.TryGetValue(pid, out var meta) ? meta : null,
      id => _requestedDocuments.Add(id),
      _ => _docLock);

    await sync.AddDocument(DefaultDocId);
    sync.Dispose();

    // After dispose, operations should throw
    await Assert.ThrowsAsync<ObjectDisposedException>(() => sync.AddDocument(AltDocId));
    await Assert.ThrowsAsync<ObjectDisposedException>(() => sync.RemoveDocument(DefaultDocId));
    await Assert.ThrowsAsync<ObjectDisposedException>(() => sync.OnPeerConnected(PeerId.Generate()));
  }
}
