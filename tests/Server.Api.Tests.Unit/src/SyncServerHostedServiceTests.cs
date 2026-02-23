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
using Xunit;

namespace Server.Api.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class SyncServerHostedServiceTests
{
  [Fact]
  public async Task StartAsync_CompletesImmediately()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await using var fixture = CreateFixture();

    await fixture.Service.StartAsync(cts.Token);

    // StartAsync should complete without throwing
  }

  [Fact]
  public async Task StopAsync_DisposeSyncServer()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await using var fixture = CreateFixture();

    await fixture.Service.StopAsync(cts.Token);

    // After StopAsync, the SyncServer should be disposed.
    // Calling any method should throw ObjectDisposedException.
    var act = () => fixture.SyncServer.OnPeerConnectedAsync(
      PeerId.Generate());
    await act.Should().ThrowAsync<ObjectDisposedException>();
  }

  [Fact]
  public async Task StopAsync_CalledMultipleTimes_DoesNotThrow()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await using var fixture = CreateFixture();

    await fixture.Service.StopAsync(cts.Token);

    // Second call should also be safe (SyncServer.DisposeAsync is idempotent)
    var act = () => fixture.Service.StopAsync(cts.Token);
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task StartAsync_SubscribesToDocumentAvailable_BroadcastsToClients()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await using var fixture = CreateFixture();

    await fixture.Service.StartAsync(cts.Token);

    // Simulate a new document being seen by SyncServer
    var peerId = PeerId.Generate();
    await fixture.SyncServer.OnPeerConnectedAsync(peerId);

    using var clientDoc = Six.Automerge.AutomergeDocument.Create();
    clientDoc.PutString(clientDoc.RootId, "k", "v");
    clientDoc.Commit("init", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    using var syncState = clientDoc.CreateSyncState();
    byte[]? syncMsg = clientDoc.GenerateSyncMessage(syncState);

    var docId = DocumentId.Generate();
    var message = new RequestMessage
    {
      DocumentId = docId,
      Data = syncMsg!
    };

    await fixture.SyncServer.OnMessageReceivedAsync(peerId, message);

    // Assert: hub context should have been called with the new document ID
    await fixture.HubClients.All.Received(1).OnDocumentAvailable(docId.Value);
  }

  static Fixture CreateFixture()
  {
    var hubContext = Substitute.For<IHubContext<DocumentsHub, IDocumentsHubClient>>();
    var hubClients = Substitute.For<IHubClients<IDocumentsHubClient>>();
    var allClients = Substitute.For<IDocumentsHubClient>();
    hubContext.Clients.Returns(hubClients);
    hubClients.All.Returns(allClients);

    var transport = new SignalRServerTransport(hubContext, NullLoggerFactory.Instance);

    var storage = Substitute.For<IStorageAdapter>();
    storage.GetChunkInfo(Arg.Any<StorageKey>())
      .Returns(Task.FromResult<IReadOnlyList<ChunkInfo>>([]));

    var options = new SyncServerOptions
    {
      Storage = storage,
      LoggerFactory = NullLoggerFactory.Instance
    };
    var syncServer = new SyncServer(options, transport);
    var service = new SyncServerHostedService(syncServer, hubContext);

    return new Fixture(service, syncServer, transport, hubClients);
  }

  sealed class Fixture : IAsyncDisposable
  {
    public SyncServerHostedService Service { get; }
    public SyncServer SyncServer { get; }
    public IHubClients<IDocumentsHubClient> HubClients { get; }
    readonly SignalRServerTransport _transport;
    bool _disposed;

    public Fixture(
      SyncServerHostedService service,
      SyncServer syncServer,
      SignalRServerTransport transport,
      IHubClients<IDocumentsHubClient> hubClients)
    {
      Service = service;
      SyncServer = syncServer;
      HubClients = hubClients;
      _transport = transport;
    }

    public async ValueTask DisposeAsync()
    {
      if(_disposed) return;
      _disposed = true;

      await SyncServer.DisposeAsync();
      _transport.Dispose();
    }
  }
}
