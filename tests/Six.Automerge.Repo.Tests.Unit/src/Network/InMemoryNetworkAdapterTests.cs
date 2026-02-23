using Six.Automerge.Repo.Testing;
using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;
using Xunit;

namespace Six.Automerge.Repo.Test.Unit.Network;

[Trait("Category", "Network")]
public class InMemoryNetworkAdapterTests
{
    private const string DefaultDocId = TestDocumentIds.Default;
    private static DocumentId DefaultDocIdValue => TestDocumentIds.DefaultId;

    [Fact]
    public void Constructor_ShouldGeneratePeerIdWhenNotProvided()
    {
        var adapter = new InMemoryNetworkAdapter();

        Assert.NotNull(adapter.AdapterId);
        Assert.StartsWith("in-memory-", adapter.AdapterId);
    }

    [Fact]
    public void Constructor_ShouldUseProvidedPeerId()
    {
        var peerId = PeerId.Generate();
        var adapter = new InMemoryNetworkAdapter(peerId);

        Assert.Equal($"in-memory-{peerId}", adapter.AdapterId);
    }

    [Fact]
    public void IsReady_ShouldAlwaysReturnTrue()
    {
        var adapter = new InMemoryNetworkAdapter();

        Assert.True(adapter.IsReady);
    }

    [Fact]
    public async Task WhenReady_ShouldCompleteImmediately()
    {
        var adapter = new InMemoryNetworkAdapter();

        await adapter.WhenReady();

        Assert.True(adapter.IsReady);
    }

    [Fact]
    public void ConnectTo_ShouldEstablishBidirectionalConnection()
    {
        var adapter1 = new InMemoryNetworkAdapter();
        var adapter2 = new InMemoryNetworkAdapter();

        var peer1Candidate = false;
        var peer2Candidate = false;

        adapter1.PeerCandidate += (sender, e) => peer1Candidate = true;
        adapter2.PeerCandidate += (sender, e) => peer2Candidate = true;

        adapter1.ConnectTo(adapter2);

        Assert.True(peer1Candidate);
        Assert.True(peer2Candidate);
    }

    [Fact]
    public void ConnectTo_ShouldTriggerPeerCandidateEventsOnBothSides()
    {
        var adapter1 = new InMemoryNetworkAdapter(new PeerId("peer1"));
        var adapter2 = new InMemoryNetworkAdapter(new PeerId("peer2"));

        PeerId? peer1SeenPeerId = null;
        PeerId? peer2SeenPeerId = null;

        adapter1.PeerCandidate += (sender, e) => peer1SeenPeerId = e.PeerId;
        adapter2.PeerCandidate += (sender, e) => peer2SeenPeerId = e.PeerId;

        adapter1.ConnectTo(adapter2);

        Assert.Equal("peer2", peer1SeenPeerId);
        Assert.Equal("peer1", peer2SeenPeerId);
    }

    [Fact]
    public void ConnectTo_ShouldNotDuplicateConnectionWhenCalledTwice()
    {
        var adapter1 = new InMemoryNetworkAdapter();
        var adapter2 = new InMemoryNetworkAdapter();

        var eventCount = 0;
        adapter1.PeerCandidate += (sender, e) => eventCount++;

        adapter1.ConnectTo(adapter2);
        adapter1.ConnectTo(adapter2);

        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Send_ShouldDeliverMessageToConnectedPeer()
    {
        var adapter1 = new InMemoryNetworkAdapter(new PeerId("peer1"));
        var adapter2 = new InMemoryNetworkAdapter(new PeerId("peer2"));

        adapter1.ConnectTo(adapter2);

        IRepoMessage? receivedMessage = null;
        adapter2.MessageReceived += (sender, e) => receivedMessage = e.Message;

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
    public void Send_ShouldAddSenderIdToMessage()
    {
        var peerId1 = new PeerId("peer1");
        var adapter1 = new InMemoryNetworkAdapter(peerId1);
        var adapter2 = new InMemoryNetworkAdapter(new PeerId("peer2"));

        adapter1.ConnectTo(adapter2);

        IRepoMessage? receivedMessage = null;
        adapter2.MessageReceived += (sender, e) => receivedMessage = e.Message;

        var message = new SyncMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = new byte[] { 1, 2, 3 }
        };

        adapter1.Send(message);

        Assert.NotNull(receivedMessage);
        Assert.Equal(peerId1, receivedMessage.SenderId);
    }

    [Fact]
    public void Send_ShouldDeliverToMultipleConnectedPeers()
    {
        var adapter1 = new InMemoryNetworkAdapter(new PeerId("peer1"));
        var adapter2 = new InMemoryNetworkAdapter(new PeerId("peer2"));
        var adapter3 = new InMemoryNetworkAdapter(new PeerId("peer3"));

        adapter1.ConnectTo(adapter2);
        adapter1.ConnectTo(adapter3);

        IRepoMessage? receivedMessage2 = null;
        IRepoMessage? receivedMessage3 = null;

        adapter2.MessageReceived += (sender, e) => receivedMessage2 = e.Message;
        adapter3.MessageReceived += (sender, e) => receivedMessage3 = e.Message;

        var message = new SyncMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = new byte[] { 1, 2, 3 }
        };

        adapter1.Send(message);

        Assert.NotNull(receivedMessage2);
        Assert.NotNull(receivedMessage3);
        var syncMessage2 = Assert.IsType<SyncMessage>(receivedMessage2);
        var syncMessage3 = Assert.IsType<SyncMessage>(receivedMessage3);
        Assert.Equal(DefaultDocIdValue, syncMessage2.DocumentId);
        Assert.Equal(DefaultDocIdValue, syncMessage3.DocumentId);
    }

    [Fact]
    public async Task Disconnect_ShouldNotifyAllPeers()
    {
        var peerId1 = new PeerId("peer1");
        var adapter1 = new InMemoryNetworkAdapter(peerId1);
        var adapter2 = new InMemoryNetworkAdapter(new PeerId("peer2"));
        var adapter3 = new InMemoryNetworkAdapter(new PeerId("peer3"));

        adapter1.ConnectTo(adapter2);
        adapter1.ConnectTo(adapter3);

        PeerId? disconnectedPeerId2 = null;
        PeerId? disconnectedPeerId3 = null;

        adapter2.PeerDisconnected += (sender, e) => disconnectedPeerId2 = e.PeerId;
        adapter3.PeerDisconnected += (sender, e) => disconnectedPeerId3 = e.PeerId;

        await adapter1.Disconnect();

        Assert.Equal(peerId1, disconnectedPeerId2);
        Assert.Equal(peerId1, disconnectedPeerId3);
    }

    [Fact]
    public async Task Disconnect_ShouldTriggerClosedEvent()
    {
        var adapter = new InMemoryNetworkAdapter();

        var closedEventFired = false;
        adapter.Closed += (sender, e) => closedEventFired = true;

        await adapter.Disconnect();

        Assert.True(closedEventFired);
    }

    [Fact]
    public async Task Disconnect_ShouldPreventFurtherMessageDelivery()
    {
        var adapter1 = new InMemoryNetworkAdapter();
        var adapter2 = new InMemoryNetworkAdapter();

        adapter1.ConnectTo(adapter2);

        await adapter1.Disconnect();

        var messageReceived = false;
        adapter2.MessageReceived += (sender, e) => messageReceived = true;

        var message = new SyncMessage
        {
            DocumentId = DefaultDocIdValue,
            Data = new byte[] { 1, 2, 3 }
        };

        adapter1.Send(message);

        Assert.False(messageReceived);
    }

    [Fact]
    public void ConnectTo_ShouldThrowWhenConnectingToSelf()
    {
        var adapter = new InMemoryNetworkAdapter();

        Assert.Throws<ArgumentException>(() => adapter.ConnectTo(adapter));
    }

    [Fact]
    public void Send_ShouldThrowWhenMessageIsNull()
    {
        var adapter = new InMemoryNetworkAdapter();

        Assert.Throws<ArgumentNullException>(() => adapter.Send(null!));
    }
}
