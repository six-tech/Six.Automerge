using Six.Automerge.Repository;
using Six.Automerge.Repo.Testing;

namespace Six.Automerge.Repo.Network.SignalR.Tests.Integration;

/// <summary>
/// Error handling tests for SignalR integration.
/// </summary>
[Collection("Aspire Test Collection")]
[Trait("Category", "SignalR")]
public class ErrorHandlingTests
{
  readonly AspireTestFixture _fixture;
  readonly string _hubUrl;

  public ErrorHandlingTests(AspireTestFixture fixture)
  {
    _fixture = fixture;
    _hubUrl = fixture.GetHubUrl();
  }

  [Fact]
  public async Task DisconnectedAdapterDoesNotCrashOnSend()
  {
    // Arrange
    var connectionService = TestConnectionServiceFactory.Create(_hubUrl!);
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    var adapter = new SignalRNetworkAdapter(connectionService, loggerFactory);

    adapter.Connect(PeerId.Generate(), null);
    await adapter.WhenReady().WaitAsync(TimeSpan.FromSeconds(5));

    // Disconnect
    await adapter.DisposeAsync();
    await connectionService.DisposeAsync();

    // Act & Assert - sending after disconnect should not crash
    var act = () =>
    {
      var documentId = DocumentId.Generate();
      var syncMessage = new SyncMessage
      {
        DocumentId = documentId,
        Data = new byte[] { 1, 2, 3 },
        SenderId = PeerId.Generate()
      };

      // This should not throw because the adapter is disposed
      var exception = Record.Exception(() => adapter.Send(syncMessage));
      exception.Should().BeOfType<ObjectDisposedException>();
    };

    act.Should().NotThrow();
  }

  [Fact]
  public async Task SendingMessageWithNullDocumentIdLogsWarning()
  {
    // Arrange
    var connectionService = TestConnectionServiceFactory.Create(_hubUrl!);
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    var adapter = new SignalRNetworkAdapter(connectionService, loggerFactory);

    adapter.Connect(PeerId.Generate(), null);
    await adapter.WhenReady().WaitAsync(TimeSpan.FromSeconds(5));

    // Act - send message with null DocumentId (should log warning and not crash)
    var syncMessage = new SyncMessage
    {
      DocumentId = default,
      Data = new byte[] { 1, 2, 3 },
      SenderId = PeerId.Generate()
    };

    // This should not throw, but log a warning internally
    adapter.Send(syncMessage);

    await Task.Delay(200); // Give time for async send to complete

    // Assert - no exception should have been thrown
    adapter.IsReady.Should().BeTrue("adapter should still be ready after sending invalid message");

    // Cleanup
    await adapter.DisposeAsync();
    await connectionService.DisposeAsync();
  }

  [Fact]
  public async Task ConcurrentSubscribeOperationsDoNotDeadlock()
  {
    // Arrange
    var connectionService = TestConnectionServiceFactory.Create(_hubUrl!);
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    var adapter = new SignalRNetworkAdapter(connectionService, loggerFactory);

    adapter.Connect(PeerId.Generate(), null);
    await adapter.WhenReady().WaitAsync(TimeSpan.FromSeconds(5));

    // Act - concurrently subscribe to multiple documents
    var tasks = new List<Task>();
    for (int i = 0; i < 10; i++)
    {
      var documentId = TestDocumentIds.UniqueString();
      tasks.Add(adapter.SubscribeToDocumentAsync(documentId));
    }

    // Assert - all subscriptions should complete without deadlock
    var allTasksTask = Task.WhenAll(tasks);
    var completedTask = await Task.WhenAny(allTasksTask, Task.Delay(TimeSpan.FromSeconds(10)));
    completedTask.Should().Be(allTasksTask, "subscriptions should complete within timeout");

    // Cleanup
    await adapter.DisposeAsync();
    await connectionService.DisposeAsync();
  }

  [Fact]
  public async Task UnsubscribeFromNonSubscribedDocumentSucceeds()
  {
    // Arrange
    var connectionService = TestConnectionServiceFactory.Create(_hubUrl!);
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    var adapter = new SignalRNetworkAdapter(connectionService, loggerFactory);

    adapter.Connect(PeerId.Generate(), null);
    await adapter.WhenReady().WaitAsync(TimeSpan.FromSeconds(5));

    // Act - unsubscribe from a document we never subscribed to
    var exception = await Record.ExceptionAsync(async () =>
      await adapter.UnsubscribeFromDocumentAsync(TestDocumentIds.UniqueString()));

    // Assert - should not throw
    exception.Should().BeNull();

    // Cleanup
    await adapter.DisposeAsync();
    await connectionService.DisposeAsync();
  }

  [Fact]
  public async Task MultipleDisconnectsAreIdempotent()
  {
    // Arrange
    var connectionService = TestConnectionServiceFactory.Create(_hubUrl!);
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    var adapter = new SignalRNetworkAdapter(connectionService, loggerFactory);

    adapter.Connect(PeerId.Generate(), null);
    await adapter.WhenReady().WaitAsync(TimeSpan.FromSeconds(5));

    // Act - call Disconnect multiple times
    await adapter.Disconnect();
    await adapter.Disconnect();
    await adapter.Disconnect();

    // Assert - should not throw or cause issues
    adapter.IsReady.Should().BeFalse();

    // Cleanup
    await connectionService.DisposeAsync();
  }
}
