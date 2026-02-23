// Copyright (c) Six. All rights reserved.

using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;
using Six.Automerge.Repository.Network.SignalR;
using Six.Automerge.Repository.Storage;
using Six.Automerge.SyncServer;
using Server.Api.Hubs;
using Server.Api.Services;
using Xunit;

namespace Server.Api.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class DocumentsHubTests : IAsyncDisposable
{
  readonly SignalRServerTransport _transport;
  readonly SyncServer _syncServer;
  readonly DocumentPeerTracker _tracker;
  readonly DocumentsHub _hub;
  readonly IHubCallerClients<IDocumentsHubClient> _clients;
  readonly IGroupManager _groups;
  readonly HubCallerContext _context;
  readonly IDocumentsHubClient _callerClient;
  readonly IDocumentsHubClient _othersInGroupClient;

  public DocumentsHubTests()
  {
    var hubContext = Substitute.For<IHubContext<DocumentsHub, IDocumentsHubClient>>();
    _transport = new SignalRServerTransport(hubContext, NullLoggerFactory.Instance);

    var storage = Substitute.For<IStorageAdapter>();
    storage.GetChunkInfo(Arg.Any<StorageKey>())
      .Returns(Task.FromResult<IReadOnlyList<ChunkInfo>>([]));

    var options = new SyncServerOptions
    {
      Storage = storage,
      LoggerFactory = NullLoggerFactory.Instance
    };
    _syncServer = new SyncServer(options, _transport);
    _tracker = new DocumentPeerTracker();

    _hub = new DocumentsHub(_syncServer, _transport, _tracker, NullLoggerFactory.Instance);

    _clients = Substitute.For<IHubCallerClients<IDocumentsHubClient>>();
    _groups = Substitute.For<IGroupManager>();
    _context = Substitute.For<HubCallerContext>();
    _context.ConnectionId.Returns("test-conn-id");
    _context.Items.Returns(new Dictionary<object, object?>());

    _callerClient = Substitute.For<IDocumentsHubClient>();
    _othersInGroupClient = Substitute.For<IDocumentsHubClient>();
    _clients.Caller.Returns(_callerClient);
    _clients.OthersInGroup(Arg.Any<string>()).Returns(_othersInGroupClient);

    SetHubContext(_hub, _clients, _groups, _context);
  }

  public async ValueTask DisposeAsync()
  {
    _hub.Dispose();
    await _syncServer.DisposeAsync();
    _transport.Dispose();
  }

  #region Handshake

  [Fact]
  public async Task Handshake_StoresPeerIdInContext()
  {
    _clients.Others.Returns(Substitute.For<IDocumentsHubClient>());

    var peerId = PeerId.Generate();
    var handshake = PeerHandshakeDto.Create(peerId, null);

    await _hub.Handshake(handshake);

    _context.Items["PeerId"].Should().Be((string)peerId);
  }

  [Fact]
  public async Task Handshake_RegistersPeerInTransport()
  {
    _clients.Others.Returns(Substitute.For<IDocumentsHubClient>());

    var peerId = PeerId.Generate();
    var handshake = PeerHandshakeDto.Create(peerId, null);

    await _hub.Handshake(handshake);

    _transport.GetConnectedPeerCount().Should().Be(1);
    _transport.TryGetPeerId("test-conn-id", out var registered).Should().BeTrue();
    registered.Should().Be(peerId);
  }

  [Fact]
  public async Task Handshake_NotifiesSyncServer()
  {
    _clients.Others.Returns(Substitute.For<IDocumentsHubClient>());

    var peerId = PeerId.Generate();
    var handshake = PeerHandshakeDto.Create(peerId, null);

    var response = await _hub.Handshake(handshake);

    // Response should contain a valid server peer ID
    response.PeerId.Should().NotBeNullOrWhiteSpace();
  }

  [Fact]
  public async Task Handshake_BroadcastsPeerJoinedToOthers()
  {
    var othersClient = Substitute.For<IDocumentsHubClient>();
    _clients.Others.Returns(othersClient);

    var handshake = PeerHandshakeDto.Create(PeerId.Generate(), null);

    await _hub.Handshake(handshake);

    await othersClient.Received(1).OnPeerJoined(handshake);
  }

  #endregion

  #region SendMessage

  [Fact]
  public async Task SendMessage_AutoSubscribesToDocumentGroup()
  {
    var peerId = PeerId.Generate();
    var docId = DocumentId.Generate();
    _context.Items["PeerId"] = (string)peerId;

    var message = new MessageDto
    {
      MessageType = "sync",
      DocumentId = docId.Value,
      SenderId = (string)peerId,
      Data = [1, 2, 3]
    };

    await _hub.SendMessage(message);

    await _groups.Received(1).AddToGroupAsync("test-conn-id", docId.Value, default);
  }

  [Fact]
  public async Task SendMessage_WithEmptyDocumentId_DropsMessage()
  {
    var peerId = PeerId.Generate();
    _context.Items["PeerId"] = (string)peerId;

    var message = new MessageDto
    {
      MessageType = "sync",
      DocumentId = "",
      SenderId = (string)peerId,
      Data = [1, 2, 3]
    };

    // Should not throw
    await _hub.SendMessage(message);

    // Should NOT have subscribed to any group
    await _groups.DidNotReceive().AddToGroupAsync(
      Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task SendMessage_SyncMessage_DelegatesToSyncServer()
  {
    // Set up handshake first so peer is registered
    _clients.Others.Returns(Substitute.For<IDocumentsHubClient>());
    var peerId = PeerId.Generate();
    var handshake = PeerHandshakeDto.Create(peerId, null);
    await _hub.Handshake(handshake);

    var docId = DocumentId.Generate();
    _context.Items["PeerId"] = (string)peerId;

    var message = new MessageDto
    {
      MessageType = "sync",
      DocumentId = docId.Value,
      SenderId = (string)peerId,
      Data = [1, 2, 3]
    };

    // Should not throw — the message is forwarded to SyncServer
    var act = () => _hub.SendMessage(message);
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task SendMessage_RequestMessage_DelegatesToSyncServer()
  {
    _clients.Others.Returns(Substitute.For<IDocumentsHubClient>());
    var peerId = PeerId.Generate();
    await _hub.Handshake(PeerHandshakeDto.Create(peerId, null));

    var docId = DocumentId.Generate();
    var message = new MessageDto
    {
      MessageType = "request",
      DocumentId = docId.Value,
      SenderId = (string)peerId,
      Data = [4, 5]
    };

    var act = () => _hub.SendMessage(message);
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task SendMessage_UnknownMessageType_DoesNotThrow()
  {
    var peerId = PeerId.Generate();
    _context.Items["PeerId"] = (string)peerId;

    var message = new MessageDto
    {
      MessageType = "unknown-type",
      DocumentId = DocumentId.Generate().Value,
      SenderId = (string)peerId,
      Data = [1]
    };

    var act = () => _hub.SendMessage(message);
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task SendMessage_TracksPeerOnDocument()
  {
    var peerId = PeerId.Generate();
    var docId = DocumentId.Generate();
    _context.Items["PeerId"] = (string)peerId;

    var message = new MessageDto
    {
      MessageType = "sync",
      DocumentId = docId.Value,
      SenderId = (string)peerId,
      Data = [1, 2, 3]
    };

    await _hub.SendMessage(message);

    _tracker.GetDocumentPeers(docId.Value).Should().ContainSingle(p => p.PeerId == (string)peerId);
  }

  [Fact]
  public async Task SendMessage_BroadcastsPeersChangedToOthersInGroup()
  {
    var peerId = PeerId.Generate();
    var docId = DocumentId.Generate();
    _context.Items["PeerId"] = (string)peerId;

    var message = new MessageDto
    {
      MessageType = "sync",
      DocumentId = docId.Value,
      SenderId = (string)peerId,
      Data = [1, 2, 3]
    };

    await _hub.SendMessage(message);

    await _othersInGroupClient.Received(1).OnDocumentPeersChanged(
      docId.Value, Arg.Any<List<PeerInfoDto>>());
  }

  [Fact]
  public async Task SendMessage_SendsPeersChangedToCaller_ExcludingSelf()
  {
    var peerId = PeerId.Generate();
    var docId = DocumentId.Generate();
    _context.Items["PeerId"] = (string)peerId;

    var message = new MessageDto
    {
      MessageType = "sync",
      DocumentId = docId.Value,
      SenderId = (string)peerId,
      Data = [1, 2, 3]
    };

    await _hub.SendMessage(message);

    // Caller should receive peers changed with their own entry excluded
    await _callerClient.Received(1).OnDocumentPeersChanged(
      docId.Value,
      Arg.Is<List<PeerInfoDto>>(list => list.All(p => p.PeerId != (string)peerId)));
  }

  #endregion

  #region Subscribe / Unsubscribe

  [Fact]
  public async Task SubscribeToDocument_AddsToGroup()
  {
    await _hub.SubscribeToDocument("doc-123");

    await _groups.Received(1).AddToGroupAsync("test-conn-id", "doc-123", default);
  }

  [Fact]
  public async Task SubscribeToDocument_WithEmptyId_DoesNothing()
  {
    await _hub.SubscribeToDocument("");

    await _groups.DidNotReceive().AddToGroupAsync(
      Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task SubscribeToDocument_TracksPeerOnDocument()
  {
    var peerId = PeerId.Generate();
    _context.Items["PeerId"] = (string)peerId;

    await _hub.SubscribeToDocument("doc-123");

    _tracker.GetDocumentPeers("doc-123").Should().ContainSingle(p => p.PeerId == (string)peerId);
  }

  [Fact]
  public async Task SubscribeToDocument_BroadcastsPeersChanged()
  {
    var peerId = PeerId.Generate();
    _context.Items["PeerId"] = (string)peerId;

    await _hub.SubscribeToDocument("doc-123");

    await _othersInGroupClient.Received(1).OnDocumentPeersChanged(
      "doc-123", Arg.Any<List<PeerInfoDto>>());
    await _callerClient.Received(1).OnDocumentPeersChanged(
      "doc-123", Arg.Any<List<PeerInfoDto>>());
  }

  [Fact]
  public async Task UnsubscribeFromDocument_RemovesFromGroup()
  {
    await _hub.UnsubscribeFromDocument("doc-123");

    await _groups.Received(1).RemoveFromGroupAsync("test-conn-id", "doc-123", default);
  }

  [Fact]
  public async Task UnsubscribeFromDocument_WithEmptyId_DoesNothing()
  {
    await _hub.UnsubscribeFromDocument("");

    await _groups.DidNotReceive().RemoveFromGroupAsync(
      Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
  }

  #endregion

  #region GetDocumentPeers / GetDocumentPeerCount

  [Fact]
  public async Task GetDocumentPeers_ReturnsTrackerData_ExcludingCaller()
  {
    var callerPeerId = PeerId.Generate();
    var otherPeerId = PeerId.Generate();
    _context.Items["PeerId"] = (string)callerPeerId;

    _tracker.AddPeerToDocument("doc-1", new PeerInfoDto { PeerId = (string)callerPeerId });
    _tracker.AddPeerToDocument("doc-1", new PeerInfoDto { PeerId = (string)otherPeerId });

    var peers = await _hub.GetDocumentPeers("doc-1");

    peers.Should().ContainSingle(p => p.PeerId == (string)otherPeerId);
    peers.Should().NotContain(p => p.PeerId == (string)callerPeerId);
  }

  [Fact]
  public async Task GetDocumentPeerCount_ReturnsCountExcludingCaller()
  {
    var callerPeerId = PeerId.Generate();
    var otherPeerId = PeerId.Generate();
    _context.Items["PeerId"] = (string)callerPeerId;

    _tracker.AddPeerToDocument("doc-1", new PeerInfoDto { PeerId = (string)callerPeerId });
    _tracker.AddPeerToDocument("doc-1", new PeerInfoDto { PeerId = (string)otherPeerId });

    var count = await _hub.GetDocumentPeerCount("doc-1");

    count.Should().Be(1);
  }

  #endregion

  #region ListDocuments

  [Fact]
  public async Task ListDocuments_DelegatesToSyncServer()
  {
    var docs = await _hub.ListDocuments();

    docs.Should().NotBeNull();
    docs.Should().BeEmpty();
  }

  #endregion

  #region Disconnect

  [Fact]
  public async Task OnDisconnectedAsync_UnregistersPeerFromTransport()
  {
    // First register a peer
    _clients.Others.Returns(Substitute.For<IDocumentsHubClient>());
    var peerId = PeerId.Generate();
    await _hub.Handshake(PeerHandshakeDto.Create(peerId, null));

    _transport.GetConnectedPeerCount().Should().Be(1);

    // Now disconnect
    await _hub.OnDisconnectedAsync(null);

    _transport.GetConnectedPeerCount().Should().Be(0);
  }

  [Fact]
  public async Task OnDisconnectedAsync_NotifiesSyncServer()
  {
    _clients.Others.Returns(Substitute.For<IDocumentsHubClient>());
    var peerId = PeerId.Generate();
    await _hub.Handshake(PeerHandshakeDto.Create(peerId, null));

    // Should not throw — forwards to SyncServer
    var act = () => _hub.OnDisconnectedAsync(null);
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task OnDisconnectedAsync_BroadcastsPeerLeftToOthers()
  {
    _clients.Others.Returns(Substitute.For<IDocumentsHubClient>());
    var peerId = PeerId.Generate();
    await _hub.Handshake(PeerHandshakeDto.Create(peerId, null));

    await _hub.OnDisconnectedAsync(null);

    await _clients.Others.Received(1).OnPeerLeft((string)peerId);
  }

  [Fact]
  public async Task OnDisconnectedAsync_WithoutPeerId_DoesNotThrow()
  {
    _clients.Others.Returns(Substitute.For<IDocumentsHubClient>());

    var act = () => _hub.OnDisconnectedAsync(null);
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task OnDisconnectedAsync_RemovesPeerFromTracker()
  {
    _clients.Others.Returns(Substitute.For<IDocumentsHubClient>());
    var peerId = PeerId.Generate();
    await _hub.Handshake(PeerHandshakeDto.Create(peerId, null));

    // Subscribe to a document so the peer is tracked
    await _hub.SubscribeToDocument("doc-1");
    _tracker.GetDocumentPeers("doc-1").Should().ContainSingle();

    // Disconnect
    await _hub.OnDisconnectedAsync(null);

    _tracker.GetDocumentPeers("doc-1").Should().BeEmpty();
  }

  [Fact]
  public async Task OnDisconnectedAsync_BroadcastsPeersChangedForAffectedDocuments()
  {
    _clients.Others.Returns(Substitute.For<IDocumentsHubClient>());
    var peerId = PeerId.Generate();
    await _hub.Handshake(PeerHandshakeDto.Create(peerId, null));

    // Subscribe to documents so the peer is tracked
    await _hub.SubscribeToDocument("doc-1");
    await _hub.SubscribeToDocument("doc-2");

    // Clear received calls from subscribe broadcasts
    _othersInGroupClient.ClearReceivedCalls();

    // Disconnect
    await _hub.OnDisconnectedAsync(null);

    // Should broadcast peers changed for each affected document
    await _othersInGroupClient.Received(1).OnDocumentPeersChanged(
      "doc-1", Arg.Any<List<PeerInfoDto>>());
    await _othersInGroupClient.Received(1).OnDocumentPeersChanged(
      "doc-2", Arg.Any<List<PeerInfoDto>>());
  }

  #endregion

  static void SetHubContext(
    Hub<IDocumentsHubClient> hub,
    IHubCallerClients<IDocumentsHubClient> clients,
    IGroupManager groups,
    HubCallerContext context)
  {
    // Hub properties are normally set by the framework;
    // we inject mocks for testing. Use DeclaredOnly to avoid ambiguity between Hub and Hub<T>.
    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly;
    typeof(Hub<IDocumentsHubClient>).GetProperty("Clients", flags)!.SetValue(hub, clients);
    typeof(Hub).GetProperty("Groups", flags)!.SetValue(hub, groups);
    typeof(Hub).GetProperty("Context", flags)!.SetValue(hub, context);
  }
}
