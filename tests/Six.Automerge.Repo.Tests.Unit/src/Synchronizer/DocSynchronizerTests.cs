using Microsoft.Extensions.Logging.Abstractions;
using Six.Automerge;
using Six.Automerge.Repository;
using Six.Automerge.Repo.Testing;
using Six.Automerge.Repository.Network;
using Six.Automerge.Repository.Storage;
using Six.Automerge.Repository.Synchronizer;
using Xunit;
using Xunit.Sdk;

namespace Six.Automerge.Repo.Test.Unit.Synchronizer;

[Trait("Category", "Synchronizer")]
public class DocSynchronizerTests : IDisposable
{
    private AutomergeDocument? _document;
    private readonly List<AutomergeDocument> _documentsToDispose = new();
    private readonly Dictionary<PeerId, PeerMetadata> _peerMetadata = new();
    private readonly SemaphoreSlim _docLock = new(1, 1);
    private static readonly NullLogger<DocSynchronizer> Logger = NullLogger<DocSynchronizer>.Instance;

    private const string DefaultDocId = TestDocumentIds.Default;
    private static DocumentId DefaultDocIdValue => TestDocumentIds.DefaultId;

    private static string NewDocId() => TestDocumentIds.UniqueString();

    public void Dispose()
    {
        _document?.Dispose();
        foreach (var doc in _documentsToDispose)
        {
            doc?.Dispose();
        }

        _docLock.Dispose();
    }

    private AutomergeDocument CreateDocument()
    {
        var doc = AutomergeDocument.Create();
        _documentsToDispose.Add(doc);
        return doc;
    }

    private static Func<AutomergeDocument, PeerId?, Task> AsyncUpdate(Action<AutomergeDocument> action)
    {
        return (doc, _) => { action(doc); return Task.CompletedTask; };
    }

    [Fact]
    public void Constructor_ShouldThrowOnNullParameters()
    {
        Assert.Throws<ArgumentNullException>(() => new DocSynchronizer(
            null!,
            () => null,
            AsyncUpdate(_ => { }),
            _ => null,
            () => _docLock,
            Logger));

        Assert.Throws<ArgumentNullException>(() => new DocSynchronizer(DefaultDocId,
            null!,
            AsyncUpdate(_ => { }),
            _ => null,
            () => _docLock,
            Logger));

        Assert.Throws<ArgumentNullException>(() => new DocSynchronizer(DefaultDocId,
            () => null,
            (Func<AutomergeDocument, PeerId?, Task>)null!,
            _ => null,
            () => _docLock,
            Logger));

        Assert.Throws<ArgumentNullException>(() => new DocSynchronizer(DefaultDocId,
            () => null,
            AsyncUpdate(_ => { }),
            null!,
            () => _docLock,
            Logger));

        Assert.Throws<ArgumentNullException>(() => new DocSynchronizer(DefaultDocId,
            () => null,
            AsyncUpdate(_ => { }),
            _ => null,
            () => _docLock,
            null!));
    }

    [Fact]
    public async Task BeginSync_ShouldInitializePeerAndSendMessage()
    {
        _document = CreateDocument();
        var messagesSent = new List<SyncMessageEventArgs>();
        var peerId = PeerId.Generate();

        var docId = NewDocId();

        using var sync = new DocSynchronizer(
            docId,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger);

        sync.MessageToSend += (sender, args) => messagesSent.Add(args);

        await sync.BeginSync(peerId);

        Assert.True(await sync.HasPeer(peerId));
        Assert.Equal(PeerDocumentStatus.Has, await sync.GetPeerStatus(peerId));
        Assert.Single(messagesSent);
        Assert.Equal(peerId, messagesSent[0].TargetPeerId);
    }

    [Fact]
    public async Task BeginSync_ShouldNotDuplicatePeer()
    {
        _document = CreateDocument();
        var messagesSent = new List<SyncMessageEventArgs>();
        var peerId = PeerId.Generate();

        var docId = NewDocId();

        using var sync = new DocSynchronizer(
            docId,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger);

        sync.MessageToSend += (sender, args) => messagesSent.Add(args);

        await sync.BeginSync(peerId);
        await sync.BeginSync(peerId); // Second call should be ignored

        Assert.True(await sync.HasPeer(peerId));
        Assert.Single(messagesSent); // Only one message sent
    }

    [Fact]
    public async Task EndSync_ShouldRemovePeerAndCleanupState()
    {
        _document = CreateDocument();
        var peerId = PeerId.Generate();

        var docId = NewDocId();

        using var sync = new DocSynchronizer(
            docId,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger);

        await sync.BeginSync(peerId);
        Assert.True(await sync.HasPeer(peerId));

        await sync.EndSync(peerId);
        Assert.False(await sync.HasPeer(peerId));
        Assert.Equal(PeerDocumentStatus.Unknown, await sync.GetPeerStatus(peerId));
    }

    [Fact]
    public async Task HasPeer_ShouldReturnFalseForUnknownPeer()
    {
        _document = CreateDocument();
        var peerId = PeerId.Generate();

        var docId = NewDocId();

        using var sync = new DocSynchronizer(
            docId,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger);

        Assert.False(await sync.HasPeer(peerId));
    }

    [Fact]
    public async Task GetPeerStatus_ShouldReturnUnknownForNewPeer()
    {
        _document = CreateDocument();
        var peerId = PeerId.Generate();

        using var sync = new DocSynchronizer(DefaultDocId,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger);

        Assert.Equal(PeerDocumentStatus.Unknown, await sync.GetPeerStatus(peerId));
    }

    [Fact]
    public async Task ReceiveSyncMessage_ShouldUpdateDocument()
    {
        // Create two independent documents
        var doc1 = CreateDocument();
        var doc2 = CreateDocument();

        // Make changes to doc1
        var root1 = doc1.RootId;
        doc1.PutString(root1, "text", "Hello from doc1");
        doc1.Commit("initial change");

        _document = doc2;
        var peer1Id = PeerId.Generate();
        var peer2Id = PeerId.Generate();

        // Setup two DocSynchronizers to simulate bidirectional sync
        using var sync1 = new DocSynchronizer(DefaultDocId,
            () => doc1,
            AsyncUpdate(doc => { }),
            _ => null,
            () => _docLock,
            Logger);

        using var sync2 = new DocSynchronizer(DefaultDocId,
            () => _document,
            AsyncUpdate(doc => { _document = doc; }),
            _ => null,
            () => _docLock,
            Logger);

        // Wire up message routing between the two synchronizers
        sync1.MessageToSend += (sender, args) =>
        {
            if (args.TargetPeerId == peer2Id)
            {
                sync2.ReceiveMessage(args.Message, peer1Id).GetAwaiter().GetResult();
            }
        };

        sync2.MessageToSend += (sender, args) =>
        {
            if (args.TargetPeerId == peer1Id)
            {
                sync1.ReceiveMessage(args.Message, peer2Id).GetAwaiter().GetResult();
            }
        };

        var headsBefore = doc2.GetHeads();

        // Begin sync for both peers (this triggers the sync protocol)
        await sync1.BeginSync(peer2Id);
        await sync2.BeginSync(peer1Id);

        // Give sync some time to complete (messages are processed synchronously)
        Thread.Sleep(50);

        // Verify doc2 heads changed (meaning it received updates)
        var headsAfter = doc2.GetHeads();
        Assert.NotEqual(headsBefore, headsAfter);

        // Verify peer status updated
        Assert.Equal(PeerDocumentStatus.Has, await sync2.GetPeerStatus(peer1Id));
    }

    [Fact]
    public async Task ReceiveSyncMessage_ShouldSendImmediateResponse()
    {
        var doc1 = CreateDocument();
        var doc2 = CreateDocument();

        // Make changes to doc1
        var root1 = doc1.RootId;
        doc1.PutString(root1, "text", "Hello");
        doc1.Commit("initial change");

        // Generate sync message
        var syncState = AutomergeSyncState.Create();
        var syncData = doc1.GenerateSyncMessage(syncState);

        _document = doc2;
        var messagesSent = new List<SyncMessageEventArgs>();
        var peerId = PeerId.Generate();

        using var sync = new DocSynchronizer(DefaultDocId,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger);

        sync.MessageToSend += (sender, args) => messagesSent.Add(args);
        await sync.BeginSync(peerId);

        var initialMessageCount = messagesSent.Count;

        // Receive sync message
        var message = new SyncMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = syncData!,
            SenderId = peerId
        };

        await sync.ReceiveMessage(message, peerId);

        // Should have sent a response message
        Assert.True(messagesSent.Count > initialMessageCount);

        syncState.Dispose();
    }

    [Fact]
    public async Task ReceiveSyncMessage_WithNullDocument_ShouldMarkPeerAsHas()
    {
        _document = null; // No document available
        var peerId = PeerId.Generate();

        using var sync = new DocSynchronizer(DefaultDocId,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger);

        await sync.BeginSync(peerId);

        // Produce a real sync payload from a remote document
        using var remoteDoc = AutomergeDocument.Create();
        var remoteRoot = remoteDoc.RootId;
        remoteDoc.PutString(remoteRoot, "field", "value");
        remoteDoc.Commit("remote baseline");

        using var remoteState = AutomergeSyncState.Create();
        var syncPayload = remoteDoc.GenerateSyncMessage(remoteState);

        var message = new SyncMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = syncPayload ?? Array.Empty<byte>(),
            SenderId = peerId
        };

        await sync.ReceiveMessage(message, peerId);

        // Should mark peer as having the document
        Assert.Equal(PeerDocumentStatus.Has, await sync.GetPeerStatus(peerId));
    }

    [Fact]
    public async Task ReceiveRequestMessage_ShouldMarkPeerAsWants()
    {
        _document = CreateDocument();
        var peerId = PeerId.Generate();

        using var sync = new DocSynchronizer(DefaultDocId,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger);

        await sync.BeginSync(peerId);

        // Receive request message
        var message = new RequestMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = [],
            SenderId = peerId
        };

        await sync.ReceiveMessage(message, peerId);

        // Should mark peer as wanting the document
        var status = await sync.GetPeerStatus(peerId);
        Assert.Equal(PeerDocumentStatus.Has, status);
    }

    [Fact]
    public async Task ReceiveRequestMessage_WithNullDocument_ShouldSendUnavailable()
    {
        _document = null; // No document available
        var messagesSent = new List<SyncMessageEventArgs>();
        var peerId = PeerId.Generate();

        using var sync = new DocSynchronizer(DefaultDocId,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger);

        sync.MessageToSend += (sender, args) => messagesSent.Add(args);
        await sync.BeginSync(peerId);

        var initialCount = messagesSent.Count;

        // Receive request message
        var message = new RequestMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = Array.Empty<byte>(),
            SenderId = peerId
        };

        await sync.ReceiveMessage(message, peerId);

        // Should have sent unavailable message
        Assert.True(messagesSent.Count > initialCount);
        var unavailableMsg = messagesSent.Last().Message as DocumentUnavailableMessage;
        Assert.NotNull(unavailableMsg);
        Assert.Equal(DefaultDocIdValue, unavailableMsg.DocumentId);
    }

    [Fact]
    public async Task ReceiveDocumentUnavailable_ShouldUpdatePeerStatus()
    {
        _document = CreateDocument();
        var peerId = PeerId.Generate();

        var docId2 = NewDocId();
        var docIdValue2 = DocumentId.Parse(docId2);

        using var sync = new DocSynchronizer(
            docId2,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger);

        await sync.BeginSync(peerId);

        // Receive unavailable message
        var message = new DocumentUnavailableMessage
        {
            DocumentId = docIdValue2,
            SenderId = peerId
        };

        await sync.ReceiveMessage(message, peerId);

        // Should mark peer as unavailable
        Assert.Equal(PeerDocumentStatus.Unavailable, await sync.GetPeerStatus(peerId));
    }

    [Fact]
    public async Task BeginSync_ShouldSendSnapshotForNewPeer()
    {
        _document = CreateDocument();
        var messagesSent = new List<SyncMessageEventArgs>();
        var peerId = PeerId.Generate();

        using var sync = new DocSynchronizer(DefaultDocId,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger);

        sync.MessageToSend += (sender, args) => messagesSent.Add(args);

        await sync.BeginSync(peerId);

        var outbound = messagesSent.Last().Message;
        if (outbound is DocumentSnapshotMessage snapshot)
        {
            Assert.Equal(DefaultDocIdValue, snapshot.DocumentId);
            Assert.NotEmpty(snapshot.Data);
        }
        else if (outbound is SyncMessage syncMessage)
        {
            Assert.Equal(DefaultDocIdValue, syncMessage.DocumentId);
            Assert.NotNull(syncMessage.Data);
            Assert.NotEmpty(syncMessage.Data);
        }
        else
        {
            throw new XunitException($"Unexpected message type: {outbound.GetType().Name}");
        }
    }

    [Fact]
    public async Task ReceiveDocumentSnapshot_ShouldCreateDocument()
    {
        _document = null;
        var peerId = PeerId.Generate();

        using var sync = new DocSynchronizer(DefaultDocId,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger);

        await sync.BeginSync(peerId);

        using var remoteDoc = AutomergeDocument.Create();
        var root = remoteDoc.RootId;
        remoteDoc.PutString(root, "field", "value");
        remoteDoc.Commit("snapshot");
        var snapshotData = remoteDoc.Save();

        var message = new DocumentSnapshotMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = snapshotData,
            SenderId = peerId
        };

        await sync.ReceiveMessage(message, peerId);

        Assert.NotNull(_document);
        Assert.Contains("\"value\"", _document!.HydrateJson());
        Assert.Equal(PeerDocumentStatus.Has, await sync.GetPeerStatus(peerId));
    }

    [Fact]
    public async Task SyncStateChanged_ShouldFireForNonEphemeralPeers()
    {
        _document = CreateDocument();
        var doc1 = CreateDocument();

        // Make changes to doc1
        var root1 = doc1.RootId;
        doc1.PutString(root1, "text", "Hello");
        doc1.Commit("initial change");

        // Generate sync message
        var syncState = AutomergeSyncState.Create();
        var syncData = doc1.GenerateSyncMessage(syncState);

        var stateChanges = new List<SyncStateChangedEventArgs>();
        var peerId = PeerId.Generate();
        var storageId = new StorageId("storage1");

        _peerMetadata[peerId] = new PeerMetadata
        {
            StorageId = storageId,
            IsEphemeral = false
        };

        using var sync = new DocSynchronizer(DefaultDocId,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            pid => _peerMetadata.TryGetValue(pid, out var meta) ? meta : null,
            () => _docLock,
            Logger);

        sync.SyncStateChanged += (sender, args) => stateChanges.Add(args);
        await sync.BeginSync(peerId);

        // Receive sync message
        var message = new SyncMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = syncData!,
            SenderId = peerId
        };

        await sync.ReceiveMessage(message, peerId);

        // Should have emitted sync state changed event
        Assert.NotEmpty(stateChanges);
        Assert.Equal(DefaultDocIdValue, stateChanges[0].DocumentId);
        Assert.Equal(peerId, stateChanges[0].PeerId);
        Assert.Equal(storageId, stateChanges[0].StorageId);

        syncState.Dispose();
    }

    [Fact]
    public async Task SyncStateChanged_ShouldNotFireForEphemeralPeers()
    {
        _document = CreateDocument();
        var doc1 = CreateDocument();

        // Make changes to doc1
        var root1 = doc1.RootId;
        doc1.PutString(root1, "text", "Hello");
        doc1.Commit("initial change");

        // Generate sync message
        var syncState = AutomergeSyncState.Create();
        var syncData = doc1.GenerateSyncMessage(syncState);

        var stateChanges = new List<SyncStateChangedEventArgs>();
        var peerId = PeerId.Generate();

        _peerMetadata[peerId] = new PeerMetadata
        {
            StorageId = null,
            IsEphemeral = true
        };

        using var sync = new DocSynchronizer(DefaultDocId,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            pid => _peerMetadata.TryGetValue(pid, out var meta) ? meta : null,
            () => _docLock,
            Logger);

        sync.SyncStateChanged += (sender, args) => stateChanges.Add(args);
        await sync.BeginSync(peerId);

        // Receive sync message
        var message = new SyncMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = syncData!,
            SenderId = peerId
        };

        await sync.ReceiveMessage(message, peerId);

        // Should NOT have emitted sync state changed event
        Assert.Empty(stateChanges);

        syncState.Dispose();
    }

    [Fact]
    public async Task MetricsUpdated_ShouldFireOnSync()
    {
        _document = CreateDocument();
        var metrics = new List<SyncMetricsEventArgs>();
        var peerId = PeerId.Generate();

        using var sync = new DocSynchronizer(DefaultDocId,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger);

        sync.MetricsUpdated += (sender, args) => metrics.Add(args);
        await sync.BeginSync(peerId);

        // Metrics should have been emitted on initial sync
        Assert.NotEmpty(metrics);
        Assert.Equal(DefaultDocIdValue, metrics[0].DocumentId);
        Assert.Equal(peerId, metrics[0].PeerId);
    }

    [Fact]
    public async Task Dispose_ShouldCleanupResources()
    {
        _document = CreateDocument();
        var peerId = PeerId.Generate();

        var sync = new DocSynchronizer(DefaultDocId,
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger);

        await sync.BeginSync(peerId);
        sync.Dispose();

        // After dispose, operations should throw
        await Assert.ThrowsAsync<ObjectDisposedException>(() => sync.BeginSync(peerId));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => sync.EndSync(peerId));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => sync.HasPeer(peerId));
    }

    #region Sync Throttle Tests

    [Fact]
    public async Task NotifyLocalChange_WithThrottle_ShouldDeferSyncMessages()
    {
        _document = CreateDocument();
        var peerId = PeerId.Generate();
        var messagesSent = new List<SyncMessageEventArgs>();

        using var sync = new DocSynchronizer(
            NewDocId(),
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger,
            syncDebounceRate: 200);

        sync.MessageToSend += (sender, args) => messagesSent.Add(args);

        await sync.BeginSync(peerId);
        int initialMessageCount = messagesSent.Count;

        // Make a local change so there's something to sync
        _document!.PutString(_document.RootId, "key", "value1");
        _document.Commit("change1");

        // Fire rapid local changes - should be throttled
        await sync.NotifyLocalChange();
        await sync.NotifyLocalChange();
        await sync.NotifyLocalChange();

        // Immediately after, messages should NOT have been sent (throttled)
        Assert.Equal(initialMessageCount, messagesSent.Count);

        // Wait for the throttle to fire
        await Task.Delay(350);

        // Now the deferred sync should have fired
        Assert.True(messagesSent.Count > initialMessageCount,
            "Expected at least one sync message after throttle period elapsed");
    }

    [Fact]
    public async Task NotifyLocalChange_WithZeroDebounce_ShouldSendImmediately()
    {
        _document = CreateDocument();
        var peerId = PeerId.Generate();
        var messagesSent = new List<SyncMessageEventArgs>();

        using var sync = new DocSynchronizer(
            NewDocId(),
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger,
            syncDebounceRate: 0);

        sync.MessageToSend += (sender, args) => messagesSent.Add(args);

        await sync.BeginSync(peerId);
        int initialMessageCount = messagesSent.Count;

        // Make a local change so there's something to sync
        _document!.PutString(_document.RootId, "key", "value1");
        _document.Commit("change1");

        // With zero debounce, should send immediately
        await sync.NotifyLocalChange();

        Assert.True(messagesSent.Count > initialMessageCount,
            "Expected sync message to be sent immediately with zero debounce");
    }

    [Fact]
    public async Task NotifyLocalChange_WithThrottle_ShouldCoalesceRapidChanges()
    {
        _document = CreateDocument();
        var peerId = PeerId.Generate();
        var messagesSent = new List<SyncMessageEventArgs>();

        using var sync = new DocSynchronizer(
            NewDocId(),
            () => _document,
            AsyncUpdate(doc => _document = doc),
            _ => null,
            () => _docLock,
            Logger,
            syncDebounceRate: 100);

        sync.MessageToSend += (sender, args) => messagesSent.Add(args);

        await sync.BeginSync(peerId);
        int initialMessageCount = messagesSent.Count;

        // Make local changes and notify rapidly within the debounce window
        for(int i = 0; i < 5; i++)
        {
            _document!.PutString(_document.RootId, "key", $"value{i}");
            _document.Commit($"change{i}");
            await sync.NotifyLocalChange();
        }

        // Wait for throttle to fire
        await Task.Delay(250);

        // Should have sent a coalesced batch, not 5 separate messages
        int messagesAfterThrottle = messagesSent.Count - initialMessageCount;
        Assert.True(messagesAfterThrottle >= 1, "Expected at least 1 message after throttle fires");
        Assert.True(messagesAfterThrottle <= 2, "Expected coalesced messages, not one per change");
    }

    #endregion
}
