using System.Collections.Concurrent;
using Six.Automerge.Repo.Testing;

namespace Six.Automerge.Repo.Network.SignalR.Tests.Integration;

/// <summary>
/// Multi-client sync tests for SignalR integration.
/// </summary>
[Collection("Aspire Test Collection")]
[Trait("Category", "SignalR")]
public class MultiClientSyncTests
{
  readonly AspireTestFixture _fixture;
  readonly string _hubUrl;

  public MultiClientSyncTests(AspireTestFixture fixture)
  {
    _fixture = fixture;
    _hubUrl = fixture.GetHubUrl();
  }

  [Fact]
  public async Task ThreeClientsCanSyncDocument()
  {
    // Arrange
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    var documentId = TestDocumentIds.UniqueString();
    var documentIdValue = DocumentId.Parse(documentId);

    // Create three clients
    var connectionService1 = TestConnectionServiceFactory.Create(_hubUrl);
    var adapter1 = new SignalRNetworkAdapter(connectionService1, loggerFactory);
    var receivedMessages1 = new ConcurrentBag<IRepoMessage>();

    var connectionService2 = TestConnectionServiceFactory.Create(_hubUrl);
    var adapter2 = new SignalRNetworkAdapter(connectionService2, loggerFactory);
    var receivedMessages2 = new ConcurrentBag<IRepoMessage>();

    var connectionService3 = TestConnectionServiceFactory.Create(_hubUrl);
    var adapter3 = new SignalRNetworkAdapter(connectionService3, loggerFactory);
    var receivedMessages3 = new ConcurrentBag<IRepoMessage>();

    // Set up message handlers
    adapter1.MessageReceived += (_, args) => receivedMessages1.Add(args.Message);
    adapter2.MessageReceived += (_, args) => receivedMessages2.Add(args.Message);
    adapter3.MessageReceived += (_, args) => receivedMessages3.Add(args.Message);

    // Connect all clients
    var peerId1 = PeerId.Generate();
    var peerId2 = PeerId.Generate();
    var peerId3 = PeerId.Generate();

    adapter1.Connect(peerId1);
    adapter2.Connect(peerId2);
    adapter3.Connect(peerId3);

    await Task.WhenAll(
      adapter1.WhenReady().WaitAsync(TimeSpan.FromSeconds(5)),
      adapter2.WhenReady().WaitAsync(TimeSpan.FromSeconds(5)),
      adapter3.WhenReady().WaitAsync(TimeSpan.FromSeconds(5))
    );

    // Subscribe all clients to the same document
    await adapter1.SubscribeToDocumentAsync(documentId);
    await adapter2.SubscribeToDocumentAsync(documentId);
    await adapter3.SubscribeToDocumentAsync(documentId);

    await Task.Delay(100); // Give subscriptions time to propagate

    // Act: Client 1 sends a sync message
    var syncData1 = new byte[] { 1, 2, 3, 4, 5 };
    var syncMessage1 = new SyncMessage
    {
      DocumentId = documentIdValue,
      Data = syncData1,
      SenderId = peerId1
    };
    adapter1.Send(syncMessage1);

    await Task.Delay(500); // Give message time to propagate

    // Assert: Clients 2 and 3 should receive the message, but not client 1 (echo prevention)
    receivedMessages1.Should().BeEmpty("sender should not receive their own message");
    receivedMessages2.Should().ContainSingle();
    var msg2 = receivedMessages2.Single();
    msg2.DocumentId.Should().Be(documentIdValue);
    msg2.MessageType.Should().Be("sync");
    ((SyncMessage)msg2).Data.Should().Equal(syncData1);

    receivedMessages3.Should().ContainSingle();
    var msg3 = receivedMessages3.Single();
    msg3.DocumentId.Should().Be(documentIdValue);
    msg3.MessageType.Should().Be("sync");
    ((SyncMessage)msg3).Data.Should().Equal(syncData1);

    // Clear received messages
    receivedMessages1.Clear();
    receivedMessages2.Clear();
    receivedMessages3.Clear();

    // Act: Client 2 sends a sync message
    var syncData2 = new byte[] { 10, 20, 30 };
    var syncMessage2 = new SyncMessage
    {
      DocumentId = documentIdValue,
      Data = syncData2,
      SenderId = peerId2
    };
    adapter2.Send(syncMessage2);

    await Task.Delay(500); // Give message time to propagate

    // Assert: Clients 1 and 3 should receive the message, but not client 2
    receivedMessages1.Should().ContainSingle();
    var msg1 = receivedMessages1.Single();
    msg1.DocumentId.Should().Be(documentIdValue);
    msg1.MessageType.Should().Be("sync");
    ((SyncMessage)msg1).Data.Should().Equal(syncData2);

    receivedMessages2.Should().BeEmpty("sender should not receive their own message");

    receivedMessages3.Should().ContainSingle();
    var msg3_2 = receivedMessages3.Single();
    msg3_2.DocumentId.Should().Be(documentIdValue);
    msg3_2.MessageType.Should().Be("sync");
    ((SyncMessage)msg3_2).Data.Should().Equal(syncData2);

    // Cleanup
    await adapter1.DisposeAsync();
    await adapter2.DisposeAsync();
    await adapter3.DisposeAsync();
    await connectionService1.DisposeAsync();
    await connectionService2.DisposeAsync();
    await connectionService3.DisposeAsync();
  }

  [Fact]
  public async Task MessagesSentToCorrectDocumentGroup()
  {
    // Arrange
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    var documentId1 = TestDocumentIds.Unique();
    var documentId2 = TestDocumentIds.Unique();

    // Create two clients
    var connectionService1 = TestConnectionServiceFactory.Create(_hubUrl!);
    var adapter1 = new SignalRNetworkAdapter(connectionService1, loggerFactory);
    var receivedMessages1 = new ConcurrentBag<IRepoMessage>();

    var connectionService2 = TestConnectionServiceFactory.Create(_hubUrl!);
    var adapter2 = new SignalRNetworkAdapter(connectionService2, loggerFactory);
    var receivedMessages2 = new ConcurrentBag<IRepoMessage>();

    adapter1.MessageReceived += (_, args) => receivedMessages1.Add(args.Message);
    adapter2.MessageReceived += (_, args) => receivedMessages2.Add(args.Message);

    // Connect both clients
    var peerId1 = PeerId.Generate();
    var peerId2 = PeerId.Generate();

    adapter1.Connect(peerId1, null);
    adapter2.Connect(peerId2, null);

    await Task.WhenAll(
      adapter1.WhenReady().WaitAsync(TimeSpan.FromSeconds(5)),
      adapter2.WhenReady().WaitAsync(TimeSpan.FromSeconds(5))
    );

    // Client 1 subscribes to document alpha, Client 2 subscribes to document beta
    await adapter1.SubscribeToDocumentAsync(documentId1.Value);
    await adapter2.SubscribeToDocumentAsync(documentId2.Value);

    await Task.Delay(100);

    // Act: Client 1 sends message to document alpha
    var syncData = new byte[] { 99, 88, 77 };
    var syncMessage = new SyncMessage
    {
      DocumentId = documentId1,
      Data = syncData,
      SenderId = peerId1
    };
    adapter1.Send(syncMessage);

    await Task.Delay(500);

    // Assert: Client 2 should NOT receive the message (subscribed to different document)
    receivedMessages1.Should().BeEmpty("sender should not receive their own message");
    receivedMessages2.Should().BeEmpty("client subscribed to different document should not receive message");

    // Cleanup
    await adapter1.DisposeAsync();
    await adapter2.DisposeAsync();
    await connectionService1.DisposeAsync();
    await connectionService2.DisposeAsync();
  }

  [Fact]
  public async Task AutoSubscribeWorksOnFirstSend()
  {
    // Arrange
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    var documentId = TestDocumentIds.Unique();

    var connectionService1 = TestConnectionServiceFactory.Create(_hubUrl!);
    var adapter1 = new SignalRNetworkAdapter(connectionService1, loggerFactory);

    var connectionService2 = TestConnectionServiceFactory.Create(_hubUrl!);
    var adapter2 = new SignalRNetworkAdapter(connectionService2, loggerFactory);
    var receivedMessages2 = new ConcurrentBag<IRepoMessage>();

    adapter2.MessageReceived += (_, args) => receivedMessages2.Add(args.Message);

    var peerId1 = PeerId.Generate();
    var peerId2 = PeerId.Generate();

    adapter1.Connect(peerId1, null);
    adapter2.Connect(peerId2, null);

    await Task.WhenAll(
      adapter1.WhenReady().WaitAsync(TimeSpan.FromSeconds(5)),
      adapter2.WhenReady().WaitAsync(TimeSpan.FromSeconds(5))
    );

    // Only client 2 explicitly subscribes
    await adapter2.SubscribeToDocumentAsync(documentId.Value);

    await Task.Delay(100);

    // Act: Client 1 sends message WITHOUT explicit subscription (should auto-subscribe)
    var syncData = new byte[] { 111, 222 };
    var syncMessage = new SyncMessage
    {
      DocumentId = documentId,
      Data = syncData,
      SenderId = peerId1
    };
    adapter1.Send(syncMessage);

    await Task.Delay(500);

    // Assert: Client 2 should receive the message (auto-subscribe worked)
    receivedMessages2.Should().ContainSingle();
    var msg2 = receivedMessages2.Single();
    msg2.DocumentId.Should().Be(documentId);
    msg2.MessageType.Should().Be("sync");

    // Cleanup
    await adapter1.DisposeAsync();
    await adapter2.DisposeAsync();
    await connectionService1.DisposeAsync();
    await connectionService2.DisposeAsync();
  }

  [Fact]
  public async Task MultipleMessagesAreDeliveredInOrder()
  {
    // Arrange
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    var documentId = TestDocumentIds.Unique();

    var connectionService1 = TestConnectionServiceFactory.Create(_hubUrl!);
    var adapter1 = new SignalRNetworkAdapter(connectionService1, loggerFactory);

    var connectionService2 = TestConnectionServiceFactory.Create(_hubUrl!);
    var adapter2 = new SignalRNetworkAdapter(connectionService2, loggerFactory);
    var receivedMessages2 = new List<IRepoMessage>();
    var receiveLock = new object();

    adapter2.MessageReceived += (_, args) =>
    {
      lock (receiveLock)
      {
        receivedMessages2.Add(args.Message);
      }
    };

    var peerId1 = PeerId.Generate();
    var peerId2 = PeerId.Generate();

    adapter1.Connect(peerId1, null);
    adapter2.Connect(peerId2, null);

    await Task.WhenAll(
      adapter1.WhenReady().WaitAsync(TimeSpan.FromSeconds(5)),
      adapter2.WhenReady().WaitAsync(TimeSpan.FromSeconds(5))
    );

    await adapter1.SubscribeToDocumentAsync(documentId.Value);
    await adapter2.SubscribeToDocumentAsync(documentId.Value);

    await Task.Delay(100);

    // Act: Send 5 messages in quick succession
    for (int i = 0; i < 5; i++)
    {
      var syncMessage = new SyncMessage
      {
        DocumentId = documentId,
        Data = new byte[] { (byte)i },
        SenderId = peerId1
      };
      adapter1.Send(syncMessage);
      await Task.Delay(50); // Small delay between sends
    }

    await Task.Delay(1000); // Wait for all messages to arrive

    // Assert: All 5 messages should be received in order
    lock (receiveLock)
    {
      receivedMessages2.Should().HaveCount(5);
      for (int i = 0; i < 5; i++)
      {
        receivedMessages2[i].Should().BeOfType<SyncMessage>();
        ((SyncMessage)receivedMessages2[i]).Data.Should().Equal(new byte[] { (byte)i });
      }
    }

    // Cleanup
    await adapter1.DisposeAsync();
    await adapter2.DisposeAsync();
    await connectionService1.DisposeAsync();
    await connectionService2.DisposeAsync();
  }
}
