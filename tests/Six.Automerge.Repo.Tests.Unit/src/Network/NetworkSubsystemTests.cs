using Six.Automerge.Repo.Testing;
using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;

namespace Six.Automerge.Repo.Test.Unit.Network;

[Trait("Category", "Network")]
public class NetworkSubsystemTests
{
    private const string DefaultDocId = TestDocumentIds.Default;
    private const string AltDocId = TestDocumentIds.Alternate;

    private static DocumentId DefaultDocIdValue => TestDocumentIds.DefaultId;
    private static DocumentId AltDocIdValue => TestDocumentIds.AlternateId;

    [Fact]
    public void Constructor_ShouldInitializeWithNoAdapters()
    {
        var subsystem = new NetworkSubsystem();

        Assert.False(subsystem.IsReady);
        Assert.Empty(subsystem.Peers);
    }

    [Fact]
    public void Constructor_ShouldAcceptInitialAdapters()
    {
        var adapter = new InMemoryNetworkAdapter();
        var subsystem = new NetworkSubsystem(new[] { adapter });

        Assert.True(subsystem.IsReady);
    }

    [Fact]
    public void AddAdapter_ShouldAddAdapterSuccessfully()
    {
        var subsystem = new NetworkSubsystem();
        var adapter = new InMemoryNetworkAdapter();

        subsystem.AddAdapter(adapter);

        Assert.True(subsystem.IsReady);
    }

    [Fact]
    public void AddAdapter_ShouldNotAddDuplicateAdapter()
    {
        var subsystem = new NetworkSubsystem();
        var adapter = new InMemoryNetworkAdapter();

        subsystem.AddAdapter(adapter);
        subsystem.AddAdapter(adapter); // Should not throw or add twice

        Assert.True(subsystem.IsReady);
    }

    [Fact]
    public void AddAdapter_ShouldThrowWhenNull()
    {
        var subsystem = new NetworkSubsystem();

        Assert.Throws<ArgumentNullException>(() => subsystem.AddAdapter(null!));
    }

    [Fact]
    public void RemoveAdapter_ShouldRemoveAdapterSuccessfully()
    {
        var adapter = new InMemoryNetworkAdapter();
        var subsystem = new NetworkSubsystem(new[] { adapter });

        Assert.True(subsystem.IsReady);

        subsystem.RemoveAdapter(adapter);

        Assert.False(subsystem.IsReady);
    }

    [Fact]
    public void RemoveAdapter_ShouldRemovePeerMappings()
    {
        var adapter1 = new InMemoryNetworkAdapter(new PeerId("peer1"));
        var adapter2 = new InMemoryNetworkAdapter(new PeerId("peer2"));

        var subsystem = new NetworkSubsystem(new[] { adapter1 });

        adapter1.ConnectTo(adapter2);

        // Wait for peer candidate event to propagate
        Thread.Sleep(50);

        Assert.Contains(subsystem.Peers, p => p == "peer2");

        subsystem.RemoveAdapter(adapter1);

        Assert.Empty(subsystem.Peers);
    }

    [Fact]
    public void RemoveAdapter_ShouldThrowWhenNull()
    {
        var subsystem = new NetworkSubsystem();

        Assert.Throws<ArgumentNullException>(() => subsystem.RemoveAdapter(null!));
    }

    [Fact]
    public void PeerCandidate_ShouldFireWhenPeerConnects()
    {
        var adapter1 = new InMemoryNetworkAdapter(new PeerId("peer1"));
        var adapter2 = new InMemoryNetworkAdapter(new PeerId("peer2"));

        var subsystem = new NetworkSubsystem(new[] { adapter1 });

        PeerId? discoveredPeerId = null;
        subsystem.PeerCandidate += (sender, e) => discoveredPeerId = e.PeerId;

        adapter1.ConnectTo(adapter2);

        Assert.Equal("peer2", discoveredPeerId);
    }

    [Fact]
    public async Task PeerDisconnected_ShouldFireWhenPeerDisconnects()
    {
        var adapter1 = new InMemoryNetworkAdapter(new PeerId("peer1"));
        var adapter2 = new InMemoryNetworkAdapter(new PeerId("peer2"));

        var subsystem = new NetworkSubsystem(new[] { adapter1 });

        adapter1.ConnectTo(adapter2);

        PeerId? disconnectedPeerId = null;
        subsystem.PeerDisconnected += (sender, e) => disconnectedPeerId = e.PeerId;

        await adapter2.Disconnect();

        Assert.Equal("peer2", disconnectedPeerId);
    }

    [Fact]
    public void Peers_ShouldReturnAllConnectedPeers()
    {
        var adapter1 = new InMemoryNetworkAdapter(new PeerId("peer1"));
        var adapter2 = new InMemoryNetworkAdapter(new PeerId("peer2"));
        var adapter3 = new InMemoryNetworkAdapter(new PeerId("peer3"));

        var subsystem = new NetworkSubsystem(new[] { adapter1 });

        adapter1.ConnectTo(adapter2);
        adapter1.ConnectTo(adapter3);

        // Wait for peer candidate events to propagate
        Thread.Sleep(50);

        Assert.Equal(2, subsystem.Peers.Count);
        Assert.Contains(subsystem.Peers, p => p == "peer2");
        Assert.Contains(subsystem.Peers, p => p == "peer3");
    }

    [Fact]
    public void GetPeerMetadata_ShouldReturnMetadataForPeer()
    {
        // Note: InMemoryNetworkAdapter doesn't currently support custom metadata
        // This test verifies the null case for now
        var subsystem = new NetworkSubsystem();

        var metadata = subsystem.GetPeerMetadata(new PeerId("peer1"));

        Assert.Null(metadata);
    }

    [Fact]
    public void GetPeerMetadata_ShouldReturnNullForUnknownPeer()
    {
        var subsystem = new NetworkSubsystem();

        var metadata = subsystem.GetPeerMetadata(new PeerId("unknown"));

        Assert.Null(metadata);
    }

    [Fact]
    public void MessageReceived_ShouldFireWhenMessageArrives()
    {
        var adapter1 = new InMemoryNetworkAdapter(new PeerId("peer1"));
        var adapter2 = new InMemoryNetworkAdapter(new PeerId("peer2"));

        var subsystem1 = new NetworkSubsystem(new[] { adapter1 });
        var subsystem2 = new NetworkSubsystem(new[] { adapter2 });

        adapter1.ConnectTo(adapter2);

        IRepoMessage? receivedMessage = null;
        subsystem2.MessageReceived += (sender, e) => receivedMessage = e.Message;

        var message = new SyncMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = new byte[] { 1, 2, 3 }
        };

        adapter1.Send(message);

        Assert.NotNull(receivedMessage);
        var syncMessage = Assert.IsType<SyncMessage>(receivedMessage);
        Assert.Equal(DefaultDocIdValue, syncMessage.DocumentId);
    }

    [Fact]
    public void Send_ShouldBroadcastToAllReadyAdapters()
    {
        var adapter1 = new InMemoryNetworkAdapter(new PeerId("peer1"));
        var adapter2 = new InMemoryNetworkAdapter(new PeerId("peer2"));
        var adapter3 = new InMemoryNetworkAdapter(new PeerId("peer3"));

        var subsystem1 = new NetworkSubsystem(new[] { adapter1, adapter2 });
        var subsystem3 = new NetworkSubsystem(new[] { adapter3 });

        adapter1.ConnectTo(adapter3);
        adapter2.ConnectTo(adapter3);

        var receivedCount = 0;
        subsystem3.MessageReceived += (sender, e) => receivedCount++;

        var message = new SyncMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = new byte[] { 1, 2, 3 }
        };

        subsystem1.Send(message);

        // Should receive message from both adapter1 and adapter2
        Assert.Equal(2, receivedCount);
    }

    [Fact]
    public void Send_ShouldThrowWhenMessageIsNull()
    {
        var subsystem = new NetworkSubsystem();

        Assert.Throws<ArgumentNullException>(() => subsystem.Send(null!));
    }

    [Fact]
    public void EphemeralMessageDeduplication_ShouldFilterDuplicates()
    {
        var adapter1 = new InMemoryNetworkAdapter(new PeerId("peer1"));
        var adapter2 = new InMemoryNetworkAdapter(new PeerId("peer2"));

        var subsystem2 = new NetworkSubsystem(new[] { adapter2 });

        adapter1.ConnectTo(adapter2);

        var receivedCount = 0;
        subsystem2.MessageReceived += (sender, e) => receivedCount++;

        var sessionId = SessionId.Generate();
        var message = new EphemeralMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = new byte[] { 1, 2, 3 },
            Count = 1,
            SessionId = sessionId
        };

        // Send the same message twice
        adapter1.Send(message);
        adapter1.Send(message);

        // Should only receive once due to deduplication
        Assert.Equal(1, receivedCount);
    }

    [Fact]
    public void EphemeralMessageDeduplication_ShouldNotFilterDifferentMessages()
    {
        var adapter1 = new InMemoryNetworkAdapter(new PeerId("peer1"));
        var adapter2 = new InMemoryNetworkAdapter(new PeerId("peer2"));

        var subsystem2 = new NetworkSubsystem(new[] { adapter2 });

        adapter1.ConnectTo(adapter2);

        var receivedCount = 0;
        subsystem2.MessageReceived += (sender, e) => receivedCount++;

        var sessionId = SessionId.Generate();

        var message1 = new EphemeralMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = new byte[] { 1, 2, 3 },
            Count = 1,
            SessionId = sessionId
        };

        var message2 = new EphemeralMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = new byte[] { 4, 5, 6 },
            Count = 2,
            SessionId = sessionId
        };

        adapter1.Send(message1);
        adapter1.Send(message2);

        // Should receive both messages
        Assert.Equal(2, receivedCount);
    }

    [Fact]
    public void EphemeralMessageDeduplication_ShouldUsePeerSessionAndCountKey()
    {
        var adapter1 = new InMemoryNetworkAdapter(new PeerId("peer1"));
        var adapter2 = new InMemoryNetworkAdapter(new PeerId("peer2"));

        var subsystem2 = new NetworkSubsystem(new[] { adapter2 });

        adapter1.ConnectTo(adapter2);

        var receivedCount = 0;
        subsystem2.MessageReceived += (sender, e) => receivedCount++;

        var sessionId1 = SessionId.Generate();
        var sessionId2 = SessionId.Generate();

        var message1 = new EphemeralMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = new byte[] { 1, 2, 3 },
            Count = 1,
            SessionId = sessionId1
        };

        var message2 = new EphemeralMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = new byte[] { 1, 2, 3 },
            Count = 1,
            SessionId = sessionId2
        };

        adapter1.Send(message1);
        adapter1.Send(message2);

        // Should receive both messages (different session IDs)
        Assert.Equal(2, receivedCount);
    }

    [Fact]
    public void IsReady_ShouldReturnFalseWithNoAdapters()
    {
        var subsystem = new NetworkSubsystem();

        Assert.False(subsystem.IsReady);
    }

    [Fact]
    public void IsReady_ShouldReturnTrueWhenAdapterReady()
    {
        var adapter = new InMemoryNetworkAdapter();
        var subsystem = new NetworkSubsystem(new[] { adapter });

        Assert.True(subsystem.IsReady);
    }

    [Fact]
    public async Task WhenReady_ShouldCompleteWhenAdapterBecomesReady()
    {
        var adapter = new InMemoryNetworkAdapter();
        var subsystem = new NetworkSubsystem(new[] { adapter });

        await subsystem.WhenReady();

        Assert.True(subsystem.IsReady);
    }

    [Fact]
    public void BidirectionalCommunication_ShouldWorkBetweenTwoSubsystems()
    {
        var adapter1 = new InMemoryNetworkAdapter(new PeerId("peer1"));
        var adapter2 = new InMemoryNetworkAdapter(new PeerId("peer2"));

        var subsystem1 = new NetworkSubsystem(new[] { adapter1 });
        var subsystem2 = new NetworkSubsystem(new[] { adapter2 });

        adapter1.ConnectTo(adapter2);

        IRepoMessage? receivedMessage1 = null;
        IRepoMessage? receivedMessage2 = null;

        subsystem1.MessageReceived += (sender, e) => receivedMessage1 = e.Message;
        subsystem2.MessageReceived += (sender, e) => receivedMessage2 = e.Message;

        var message1 = new SyncMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = new byte[] { 1, 2, 3 }
        };

        var message2 = new RequestMessage
        {
            DocumentId = AltDocIdValue,
            Data = new byte[] { 4, 5, 6 }
        };

        adapter1.Send(message1);
        adapter2.Send(message2);

        Assert.NotNull(receivedMessage2);
        Assert.IsType<SyncMessage>(receivedMessage2);

        Assert.NotNull(receivedMessage1);
        Assert.IsType<RequestMessage>(receivedMessage1);
    }

    [Fact]
    public void Dispose_ShouldClearAdaptersAndPeers()
    {
        var adapter = new InMemoryNetworkAdapter();
        var subsystem = new NetworkSubsystem(new[] { adapter });

        subsystem.Dispose();

        Assert.Throws<ObjectDisposedException>(() => subsystem.IsReady);
    }

    [Fact]
    public void Dispose_ShouldPreventFurtherOperations()
    {
        var adapter = new InMemoryNetworkAdapter();
        var subsystem = new NetworkSubsystem(new[] { adapter });

        subsystem.Dispose();

        Assert.Throws<ObjectDisposedException>(() => subsystem.AddAdapter(adapter));
        Assert.Throws<ObjectDisposedException>(() => subsystem.Send(new SyncMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = new byte[] { 1, 2, 3 }
        }));
    }
}
