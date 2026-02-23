namespace Six.Automerge.Repo.Network.SignalR.Tests.Integration;

/// <summary>
/// Basic connection tests for SignalR integration.
/// </summary>
[Collection("Aspire Test Collection")]
[Trait("Category", "SignalR")]
public class BasicConnectionTests
{
  readonly AspireTestFixture _fixture;
  readonly string _hubUrl;

  public BasicConnectionTests(AspireTestFixture fixture)
  {
    _fixture = fixture;
    _hubUrl = fixture.GetHubUrl();
  }

  [Fact]
  public async Task CanConnectToTestServer()
  {
    // Arrange
    var connectionService = TestConnectionServiceFactory.Create(_hubUrl!);
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    var adapter = new SignalRNetworkAdapter(connectionService, loggerFactory);

    // Act
    var peerId = PeerId.Generate();
    adapter.Connect(peerId);

    // Assert
    await adapter.WhenReady().WaitAsync(TimeSpan.FromSeconds(5));
    adapter.IsReady.Should().BeTrue();
    adapter.AdapterId.Should().StartWith("signalr-");

    // Cleanup
    await adapter.DisposeAsync();
    await connectionService.DisposeAsync();
  }

  [Fact]
  public async Task CanConnectMultipleClients()
  {
    // Arrange
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

    var connectionService1 = TestConnectionServiceFactory.Create(_hubUrl);
    var adapter1 = new SignalRNetworkAdapter(connectionService1, loggerFactory);

    var connectionService2 = TestConnectionServiceFactory.Create(_hubUrl);
    var adapter2 = new SignalRNetworkAdapter(connectionService2, loggerFactory);

    var connectionService3 = TestConnectionServiceFactory.Create(_hubUrl);
    var adapter3 = new SignalRNetworkAdapter(connectionService3, loggerFactory);

    // Act
    adapter1.Connect(PeerId.Generate());
    adapter2.Connect(PeerId.Generate());
    adapter3.Connect(PeerId.Generate());

    // Assert
    await Task.WhenAll(
      adapter1.WhenReady().WaitAsync(TimeSpan.FromSeconds(5)),
      adapter2.WhenReady().WaitAsync(TimeSpan.FromSeconds(5)),
      adapter3.WhenReady().WaitAsync(TimeSpan.FromSeconds(5))
    );

    adapter1.IsReady.Should().BeTrue();
    adapter2.IsReady.Should().BeTrue();
    adapter3.IsReady.Should().BeTrue();

    // Cleanup
    await adapter1.DisposeAsync();
    await adapter2.DisposeAsync();
    await adapter3.DisposeAsync();
    await connectionService1.DisposeAsync();
    await connectionService2.DisposeAsync();
    await connectionService3.DisposeAsync();
  }

  [Fact]
  public async Task CanSubscribeToDocument()
  {
    // Arrange
    var connectionService = TestConnectionServiceFactory.Create(_hubUrl);
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    var adapter = new SignalRNetworkAdapter(connectionService, loggerFactory);

    adapter.Connect(PeerId.Generate());
    await adapter.WhenReady().WaitAsync(TimeSpan.FromSeconds(5));

    // Act
    var documentId = "test-document-123";
    await adapter.SubscribeToDocumentAsync(documentId);

    // Assert - no exception means success
    adapter.IsReady.Should().BeTrue();

    // Cleanup
    await adapter.DisposeAsync();
    await connectionService.DisposeAsync();
  }
}
