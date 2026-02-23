// Copyright (c) Six Technology, Inc. All rights reserved.
// Licensed under the MIT license.

using FluentAssertions;
using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;

namespace Six.Automerge.SyncServer.Tests.Unit;

[Trait("Category", "SyncServer")]
public sealed class TransportBridgeAdapterTests
{
  [Fact]
  public void IsReady_ReturnsTrue()
  {
    var transport = new MockServerTransport();
    var adapter = new TransportBridgeAdapter(transport, PeerId.Generate());

    adapter.IsReady.Should().BeTrue();
  }

  [Fact]
  public async Task WhenReady_CompletesImmediately()
  {
    var transport = new MockServerTransport();
    var adapter = new TransportBridgeAdapter(transport, PeerId.Generate());

    await adapter.WhenReady();
    // No exception means success
  }

  [Fact]
  public async Task Send_WithTargetPeerId_ForwardsToTransportSendAsync()
  {
    // Arrange
    var transport = new MockServerTransport();
    var adapter = new TransportBridgeAdapter(transport, PeerId.Generate());
    var targetPeer = PeerId.Generate();
    var docId = DocumentId.Generate();

    var message = new SyncMessage
    {
      DocumentId = docId,
      Data = [1, 2, 3],
      TargetPeerId = targetPeer
    };

    // Act
    adapter.Send(message);

    // Give fire-and-forget task time to complete
    await Task.Delay(100);

    // Assert
    transport.SentMessages.Should().ContainSingle()
      .Which.PeerId.Should().Be(targetPeer);
  }

  [Fact]
  public async Task Send_WithoutTargetPeerId_BroadcastsViaTransport()
  {
    // Arrange
    var transport = new MockServerTransport();
    var adapter = new TransportBridgeAdapter(transport, PeerId.Generate());
    var docId = DocumentId.Generate();

    var message = new SyncMessage
    {
      DocumentId = docId,
      Data = [1, 2, 3]
    };

    // Act
    adapter.Send(message);

    // Give fire-and-forget task time to complete
    await Task.Delay(100);

    // Assert
    transport.BroadcastMessages.Should().ContainSingle()
      .Which.DocumentId.Should().Be(docId);
  }

  [Fact]
  public void RaisePeerConnected_FiresPeerCandidateEvent()
  {
    // Arrange
    var transport = new MockServerTransport();
    var adapter = new TransportBridgeAdapter(transport, PeerId.Generate());
    PeerId? receivedPeerId = null;

    adapter.PeerCandidate += (_, e) => receivedPeerId = e.PeerId;

    var peerId = PeerId.Generate();

    // Act
    adapter.RaisePeerConnected(peerId, null);

    // Assert
    receivedPeerId.Should().Be(peerId);
  }

  [Fact]
  public void RaisePeerDisconnected_FiresPeerDisconnectedEvent()
  {
    // Arrange
    var transport = new MockServerTransport();
    var adapter = new TransportBridgeAdapter(transport, PeerId.Generate());
    PeerId? receivedPeerId = null;

    adapter.PeerDisconnected += (_, e) => receivedPeerId = e.PeerId;

    var peerId = PeerId.Generate();

    // Act
    adapter.RaisePeerDisconnected(peerId);

    // Assert
    receivedPeerId.Should().Be(peerId);
  }

  [Fact]
  public void RaiseMessageReceived_FiresMessageReceivedEvent()
  {
    // Arrange
    var transport = new MockServerTransport();
    var adapter = new TransportBridgeAdapter(transport, PeerId.Generate());
    IRepoMessage? receivedMessage = null;

    adapter.MessageReceived += (_, e) => receivedMessage = e.Message;

    var docId = DocumentId.Generate();
    var message = new SyncMessage
    {
      DocumentId = docId,
      Data = [4, 5, 6]
    };

    // Act
    adapter.RaiseMessageReceived(message);

    // Assert
    receivedMessage.Should().Be(message);
  }
}
