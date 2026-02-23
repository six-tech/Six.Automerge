// Copyright (c) Six. All rights reserved.

using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;
using Six.Automerge.Repository.Network.SignalR;
using Six.Automerge.SyncServer;
using Server.Api.Hubs;
using Xunit;

namespace Server.Api.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class SignalRServerTransportTests : IDisposable
{
  readonly IHubContext<DocumentsHub, IDocumentsHubClient> _hubContext;
  readonly IHubClients<IDocumentsHubClient> _clients;
  readonly SignalRServerTransport _transport;

  public SignalRServerTransportTests()
  {
    _hubContext = Substitute.For<IHubContext<DocumentsHub, IDocumentsHubClient>>();
    _clients = Substitute.For<IHubClients<IDocumentsHubClient>>();
    _hubContext.Clients.Returns(_clients);

    _transport = new SignalRServerTransport(
      _hubContext,
      NullLoggerFactory.Instance);
  }

  public void Dispose()
  {
    _transport.Dispose();
  }

  #region SendAsync — targeted peer delivery

  [Fact]
  public async Task SendAsync_WithRegisteredPeer_SendsToConnection()
  {
    var peerId = PeerId.Generate();
    var connectionId = "conn-1";
    _transport.RegisterPeer(connectionId, peerId);

    var clientProxy = Substitute.For<IDocumentsHubClient>();
    _clients.Client(connectionId).Returns(clientProxy);

    var message = new SyncMessage
    {
      DocumentId = DocumentId.Generate(),
      Data = [1, 2, 3],
      SenderId = PeerId.Generate()
    };

    await _transport.SendAsync(peerId, message);

    await clientProxy.Received(1).OnReceiveMessage(Arg.Is<MessageDto>(m =>
      m.MessageType == "sync" &&
      m.Data.Length == 3));
  }

  [Fact]
  public async Task SendAsync_WithUnregisteredPeer_DoesNotThrow()
  {
    var peerId = PeerId.Generate();
    var message = new SyncMessage
    {
      DocumentId = DocumentId.Generate(),
      Data = [1, 2, 3],
      SenderId = PeerId.Generate()
    };

    var act = () => _transport.SendAsync(peerId, message);

    await act.Should().NotThrowAsync();
  }

  #endregion

  #region BroadcastAsync — document group delivery

  [Fact]
  public async Task BroadcastAsync_SendsToDocumentGroup()
  {
    var docId = DocumentId.Generate();
    var groupProxy = Substitute.For<IDocumentsHubClient>();
    _clients.Group(docId.Value).Returns(groupProxy);

    var message = new SyncMessage
    {
      DocumentId = docId,
      Data = [4, 5, 6],
      SenderId = PeerId.Generate()
    };

    await _transport.BroadcastAsync(docId, message);

    await groupProxy.Received(1).OnReceiveMessage(Arg.Is<MessageDto>(m =>
      m.MessageType == "sync" &&
      m.DocumentId == docId.Value));
  }

  [Fact]
  public async Task BroadcastAsync_WithExcludePeer_UsesGroupExcept()
  {
    var docId = DocumentId.Generate();
    var excludePeerId = PeerId.Generate();
    var connectionId = "conn-exclude";
    _transport.RegisterPeer(connectionId, excludePeerId);

    var groupExceptProxy = Substitute.For<IDocumentsHubClient>();
    _clients.GroupExcept(docId.Value, Arg.Is<IReadOnlyList<string>>(l => l.Contains(connectionId)))
      .Returns(groupExceptProxy);

    var message = new SyncMessage
    {
      DocumentId = docId,
      Data = [1, 2, 3],
      SenderId = PeerId.Generate()
    };

    await _transport.BroadcastAsync(docId, message, excludePeerId);

    await groupExceptProxy.Received(1).OnReceiveMessage(Arg.Is<MessageDto>(m =>
      m.MessageType == "sync" &&
      m.DocumentId == docId.Value));
  }

  [Fact]
  public async Task BroadcastAsync_WithUnknownExcludePeer_FallsBackToGroup()
  {
    var docId = DocumentId.Generate();
    var unknownPeerId = PeerId.Generate();
    var groupProxy = Substitute.For<IDocumentsHubClient>();
    _clients.Group(docId.Value).Returns(groupProxy);

    var message = new SyncMessage
    {
      DocumentId = docId,
      Data = [1, 2, 3],
      SenderId = PeerId.Generate()
    };

    await _transport.BroadcastAsync(docId, message, unknownPeerId);

    await groupProxy.Received(1).OnReceiveMessage(Arg.Any<MessageDto>());
  }

  #endregion

  #region Peer registration

  [Fact]
  public void RegisterPeer_TracksPeerId()
  {
    var peerId = PeerId.Generate();
    _transport.RegisterPeer("conn-1", peerId);

    _transport.GetConnectedPeerCount().Should().Be(1);
  }

  [Fact]
  public void UnregisterPeer_RemovesPeerId()
  {
    var peerId = PeerId.Generate();
    _transport.RegisterPeer("conn-1", peerId);
    _transport.UnregisterPeer("conn-1");

    _transport.GetConnectedPeerCount().Should().Be(0);
  }

  [Fact]
  public void UnregisterPeer_UnknownConnection_DoesNotThrow()
  {
    var act = () => _transport.UnregisterPeer("unknown");

    act.Should().NotThrow();
  }

  [Fact]
  public void TryGetPeerId_ReturnsRegisteredPeer()
  {
    var peerId = PeerId.Generate();
    _transport.RegisterPeer("conn-1", peerId);

    _transport.TryGetPeerId("conn-1", out var result).Should().BeTrue();
    result.Should().Be(peerId);
  }

  [Fact]
  public void TryGetPeerId_UnknownConnection_ReturnsFalse()
  {
    _transport.TryGetPeerId("unknown", out _).Should().BeFalse();
  }

  #endregion

  #region Dispose

  [Fact]
  public void Dispose_ClearsPeerMappings()
  {
    _transport.RegisterPeer("conn-1", PeerId.Generate());
    _transport.Dispose();

    _transport.GetConnectedPeerCount().Should().Be(0);
  }

  #endregion

  #region Message serialization

  [Fact]
  public async Task SendAsync_SyncMessage_SerializesCorrectly()
  {
    var peerId = PeerId.Generate();
    _transport.RegisterPeer("conn-1", peerId);

    var clientProxy = Substitute.For<IDocumentsHubClient>();
    _clients.Client("conn-1").Returns(clientProxy);

    var docId = DocumentId.Generate();
    var senderId = PeerId.Generate();
    var message = new SyncMessage
    {
      DocumentId = docId,
      Data = [10, 20, 30],
      SenderId = senderId
    };

    await _transport.SendAsync(peerId, message);

    await clientProxy.Received(1).OnReceiveMessage(Arg.Is<MessageDto>(m =>
      m.MessageType == "sync" &&
      m.DocumentId == docId.Value &&
      m.SenderId == (string)senderId &&
      m.Data.SequenceEqual(new byte[] { 10, 20, 30 })));
  }

  [Fact]
  public async Task BroadcastAsync_RequestMessage_SerializesCorrectly()
  {
    var docId = DocumentId.Generate();
    var groupProxy = Substitute.For<IDocumentsHubClient>();
    _clients.Group(docId.Value).Returns(groupProxy);

    var senderId = PeerId.Generate();
    var message = new RequestMessage
    {
      DocumentId = docId,
      Data = [7, 8, 9],
      SenderId = senderId
    };

    await _transport.BroadcastAsync(docId, message);

    await groupProxy.Received(1).OnReceiveMessage(Arg.Is<MessageDto>(m =>
      m.MessageType == "request" &&
      m.DocumentId == docId.Value &&
      m.SenderId == (string)senderId));
  }

  [Fact]
  public async Task BroadcastAsync_DocumentUnavailableMessage_SerializesCorrectly()
  {
    var docId = DocumentId.Generate();
    var groupProxy = Substitute.For<IDocumentsHubClient>();
    _clients.Group(docId.Value).Returns(groupProxy);

    var senderId = PeerId.Generate();
    var message = new DocumentUnavailableMessage
    {
      DocumentId = docId,
      SenderId = senderId
    };

    await _transport.BroadcastAsync(docId, message);

    await groupProxy.Received(1).OnReceiveMessage(Arg.Is<MessageDto>(m =>
      m.MessageType == "doc-unavailable" &&
      m.DocumentId == docId.Value));
  }

  [Fact]
  public async Task BroadcastAsync_DocumentSnapshotMessage_SerializesCorrectly()
  {
    var docId = DocumentId.Generate();
    var groupProxy = Substitute.For<IDocumentsHubClient>();
    _clients.Group(docId.Value).Returns(groupProxy);

    var senderId = PeerId.Generate();
    var message = new DocumentSnapshotMessage
    {
      DocumentId = docId,
      Data = [42],
      SenderId = senderId
    };

    await _transport.BroadcastAsync(docId, message);

    await groupProxy.Received(1).OnReceiveMessage(Arg.Is<MessageDto>(m =>
      m.MessageType == "doc-snapshot" &&
      m.Data.SequenceEqual(new byte[] { 42 })));
  }

  #endregion
}
