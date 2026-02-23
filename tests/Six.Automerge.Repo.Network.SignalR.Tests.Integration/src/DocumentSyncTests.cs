namespace Six.Automerge.Repo.Network.SignalR.Tests.Integration;

/// <summary>
/// Full end-to-end integration tests for document synchronization via SignalR.
/// Tests the complete flow: Client → Server → Other Clients with real Automerge document merging.
/// Each test creates its own isolated test infrastructure for complete test isolation.
/// </summary>
/// <remarks>
/// KNOWN ISSUE: These tests suffer from a critical bug in the automerge Rust FFI library (v0.7.0).
/// When running tests as a group, the second test crashes with:
///   - "AUTOMERGE :: unused actor found when none expected"
///   - "assertion failed: !panic" at automerge-0.7.0/src/automerge.rs:300:13
///   - Crash in automerge_doc_sync_generate_message
///
/// The tests MUST run serially (enforced via xUnit collection) and even with 8-second delays
/// between tests, the issue persists. This indicates a fundamental bug in either:
///   1. The automerge Rust library's internal state management
///   2. The C# FFI wrapper's interaction with the Rust library
///   3. Thread safety violations at the FFI boundary
///
/// Individual tests pass reliably. The crash only occurs when running multiple tests sequentially.
/// See OpenSpec proposal for investigation and fix.
/// </remarks>
[Collection("DocumentSync Serial Tests")]
[Trait("Category", "SignalR")]
public class DocumentSyncTests : IAsyncLifetime
{
  DistributedApplication? _app;
  string? _hubUrl;
  readonly List<Six.Automerge.Repository.Repo> _repos = [];
  readonly List<SignalRNetworkAdapter> _adapters = [];
  readonly List<ISignalRConnectionService> _connectionServices = [];

  [Fact]
  public async Task TwoClientsAndServer_ClientMakesChange_ChangePropagesToServer()
  {
    // Arrange
    // Create server repo (central source of truth)
    var serverRepo = await CreateRepoAsync("server");

    // Server creates and initializes the document
    var serverHandle = await serverRepo.Create<AutomergeDocument>();
    await serverHandle.ChangeAsync(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "title", "Server Document");
    }, message: "Initialize server document");

    // Get the document ID from server's created document
    var documentId = serverHandle.DocumentId;

    // Create two client repos
    var client1Repo = await CreateRepoAsync("client1");
    var client2Repo = await CreateRepoAsync("client2");

    // Wait for document to be announced and clients to be ready
    await Task.Delay(1000);

    // Clients must successfully find the server's document
    var client1Handle = await client1Repo.FindAsync<AutomergeDocument>(documentId);
    var client2Handle = await client2Repo.FindAsync<AutomergeDocument>(documentId);

    // Verify sync is working - clients should have found the document
    client1Handle.Should().NotBeNull("Client 1 should have found server's document - sync may be broken");
    client2Handle.Should().NotBeNull("Client 2 should have found server's document - sync may be broken");

    // Act: Client 1 makes a change
    await client1Handle.ChangeAsync(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "author", "Client 1");
    }, message: "Client 1 adds author");

    // Wait for sync to propagate
    await Task.Delay(1000);

    // Assert: Server should have merged the change
    var serverDoc = serverHandle.Doc();
    var serverJson = serverDoc.HydrateJson();
    serverJson.Should().Contain("\"author\":\"Client 1\"");
    serverJson.Should().Contain("\"title\":\"Server Document\"");

    // Assert: Client 2 should have received the change
    var client2Doc = client2Handle.Doc();
    var client2Json = client2Doc.HydrateJson();
    client2Json.Should().Contain("\"author\":\"Client 1\"");
    client2Json.Should().Contain("\"title\":\"Server Document\"");

    // Verify all three have the same heads (converged state)
    var serverHeads = serverHandle.Heads;
    var client1Heads = client1Handle.Heads;
    var client2Heads = client2Handle.Heads;

    serverHeads.Should().NotBeEmpty();
    client1Heads.Should().BeEquivalentTo(serverHeads);
    client2Heads.Should().BeEquivalentTo(serverHeads);
  }

  [Fact]
  public async Task MultipleClients_ConcurrentChanges_EventuallyConverge()
  {
    // Arrange
    var serverRepo = await CreateRepoAsync("server");

    // Server creates and initializes the document
    var serverHandle = await serverRepo.Create<AutomergeDocument>();
    await serverHandle.ChangeAsync(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "status", "initialized");
    }, message: "Initialize");

    // Get the document ID from server's created document
    var documentId = serverHandle.DocumentId;

    var client1Repo = await CreateRepoAsync("client1");
    var client2Repo = await CreateRepoAsync("client2");

    // Wait for document to be announced and clients to be ready
    await Task.Delay(1000);

    // Clients must successfully find the server's document
    var client1Handle = await client1Repo.FindAsync<AutomergeDocument>(documentId);
    var client2Handle = await client2Repo.FindAsync<AutomergeDocument>(documentId);

    // Verify sync is working
    client1Handle.Should().NotBeNull("Client 1 should have found server's document - sync may be broken");
    client2Handle.Should().NotBeNull("Client 2 should have found server's document - sync may be broken");

    // Act: Both clients make concurrent changes
    var client1Task = client1Handle.ChangeAsync(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "field1", "from client 1");
    }, message: "Client 1 change").AsTask();

    var client2Task = client2Handle.ChangeAsync(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "field2", "from client 2");
    }, message: "Client 2 change").AsTask();

    await Task.WhenAll(client1Task, client2Task);

    // Wait for sync (may take a bit for concurrent changes)
    await Task.Delay(2000);

    // Assert: All three repos should eventually converge
    var serverJson = serverHandle.Doc().HydrateJson();
    var client1Json = client1Handle.Doc().HydrateJson();
    var client2Json = client2Handle.Doc().HydrateJson();

    // All should have both changes
    serverJson.Should().Contain("\"field1\":\"from client 1\"");
    serverJson.Should().Contain("\"field2\":\"from client 2\"");

    client1Json.Should().Contain("\"field1\":\"from client 1\"");
    client1Json.Should().Contain("\"field2\":\"from client 2\"");

    client2Json.Should().Contain("\"field1\":\"from client 1\"");
    client2Json.Should().Contain("\"field2\":\"from client 2\"");

    // All should have converged to the same state
    var serverHeads = serverHandle.Heads;
    client1Handle.Heads.Should().BeEquivalentTo(serverHeads);
    client2Handle.Heads.Should().BeEquivalentTo(serverHeads);
  }

  [Fact]
  public async Task Server_MergesClientChanges_MaintainsCentralState()
  {
    // Arrange
    var serverRepo = await CreateRepoAsync("server");

    // Server creates and initializes the document
    var serverHandle = await serverRepo.Create<AutomergeDocument>();
    await serverHandle.ChangeAsync(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "version", "1.0");
      doc.PutString(root, "lastModified", "server");
    }, message: "Server baseline");

    // Get the document ID from server's created document
    var documentId = serverHandle.DocumentId;

    var clientRepo = await CreateRepoAsync("client");

    // Wait for document to be announced and client to be ready
    await Task.Delay(1000);

    // Client must successfully find the server's document
    var clientHandle = await clientRepo.FindAsync<AutomergeDocument>(documentId);

    // Verify sync is working
    clientHandle.Should().NotBeNull("Client should have found server's document - sync may be broken");

    // Act: Client makes multiple sequential changes
    for (int i = 1; i <= 3; i++)
    {
      await clientHandle.ChangeAsync(doc =>
      {
        var root = doc.RootId;
        doc.PutString(root, $"update{i}", $"value{i}");
      }, message: $"Client update {i}");

      await Task.Delay(300);
    }

    // Wait for final sync
    await Task.Delay(1000);

    // Assert: Server should have all client changes merged
    var serverDoc = serverHandle.Doc();
    var serverJson = serverDoc.HydrateJson();

    serverJson.Should().Contain("\"version\":\"1.0\"");
    serverJson.Should().Contain("\"lastModified\":\"server\"");
    serverJson.Should().Contain("\"update1\":\"value1\"");
    serverJson.Should().Contain("\"update2\":\"value2\"");
    serverJson.Should().Contain("\"update3\":\"value3\"");

    // Client should also have server's baseline data
    var clientJson = clientHandle.Doc().HydrateJson();
    clientJson.Should().Contain("\"version\":\"1.0\"");
    clientJson.Should().Contain("\"lastModified\":\"server\"");

    // Both should be in sync
    serverHandle.Heads.Should().BeEquivalentTo(clientHandle.Heads);
  }

  [Fact]
  public async Task ThreeClients_OneServerActsAsHub_AllReceiveChanges()
  {
    // Arrange
    var serverRepo = await CreateRepoAsync("hub-server");

    // Server creates the document first
    var serverHandle = await serverRepo.Create<AutomergeDocument>();

    // Get the document ID from server's created document
    var documentId = serverHandle.DocumentId;

    var client1Repo = await CreateRepoAsync("client1");
    var client2Repo = await CreateRepoAsync("client2");
    var client3Repo = await CreateRepoAsync("client3");

    // Wait for document to be announced and all clients to be ready
    await Task.Delay(1000);

    // All clients must successfully find the server's document
    var client1Handle = await client1Repo.FindAsync<AutomergeDocument>(documentId);
    var client2Handle = await client2Repo.FindAsync<AutomergeDocument>(documentId);
    var client3Handle = await client3Repo.FindAsync<AutomergeDocument>(documentId);

    // Verify sync is working
    client1Handle.Should().NotBeNull("Client 1 should have found server's document - sync may be broken");
    client2Handle.Should().NotBeNull("Client 2 should have found server's document - sync may be broken");
    client3Handle.Should().NotBeNull("Client 3 should have found server's document - sync may be broken");

    // Act: Each client makes a unique change
    await client1Handle.ChangeAsync(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "client1", "hello from 1");
    }, message: "Client 1");

    await Task.Delay(500);

    await client2Handle.ChangeAsync(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "client2", "hello from 2");
    }, message: "Client 2");

    await Task.Delay(500);

    await client3Handle.ChangeAsync(doc =>
    {
      var root = doc.RootId;
      doc.PutString(root, "client3", "hello from 3");
    }, message: "Client 3");

    // Wait for all changes to propagate through the hub
    await Task.Delay(2000);

    // Assert: Server (hub) should have all changes
    var serverJson = serverHandle.Doc().HydrateJson();
    serverJson.Should().Contain("\"client1\":\"hello from 1\"");
    serverJson.Should().Contain("\"client2\":\"hello from 2\"");
    serverJson.Should().Contain("\"client3\":\"hello from 3\"");

    // Assert: All clients should have all changes
    var client1Json = client1Handle.Doc().HydrateJson();
    client1Json.Should().Contain("\"client1\":\"hello from 1\"");
    client1Json.Should().Contain("\"client2\":\"hello from 2\"");
    client1Json.Should().Contain("\"client3\":\"hello from 3\"");

    var client2Json = client2Handle.Doc().HydrateJson();
    client2Json.Should().Contain("\"client1\":\"hello from 1\"");
    client2Json.Should().Contain("\"client2\":\"hello from 2\"");
    client2Json.Should().Contain("\"client3\":\"hello from 3\"");

    var client3Json = client3Handle.Doc().HydrateJson();
    client3Json.Should().Contain("\"client1\":\"hello from 1\"");
    client3Json.Should().Contain("\"client2\":\"hello from 2\"");
    client3Json.Should().Contain("\"client3\":\"hello from 3\"");

    // All four repos should have converged
    var serverHeads = serverHandle.Heads;
    client1Handle.Heads.Should().BeEquivalentTo(serverHeads);
    client2Handle.Heads.Should().BeEquivalentTo(serverHeads);
    client3Handle.Heads.Should().BeEquivalentTo(serverHeads);
  }

  /// <summary>
  /// Initializes test infrastructure before each test.
  /// Creates a fresh DistributedApplication instance for complete test isolation.
  /// </summary>
  public async Task InitializeAsync()
  {
    // CRITICAL: Wait before initializing to ensure previous test's Rust FFI state is fully cleaned
    await Task.Delay(3000);

    var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;

    var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Six_Tests_AppHost>(
      args: ["DcpPublisher:RandomizePorts=false"],
      cancellationToken: cancellationToken);

    appHost.Services.AddLogging(logging =>
    {
      logging.SetMinimumLevel(LogLevel.Debug);
      logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
      logging.AddFilter("Aspire.", LogLevel.Debug);
    });

    appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
    {
      clientBuilder.AddStandardResilienceHandler();
    });

    _app = await appHost.BuildAsync(cancellationToken);
    await _app.StartAsync(cancellationToken);

    // Wait for SignalR hub to be healthy
    await _app.ResourceNotifications
      .WaitForResourceHealthyAsync("signalr-hub", cancellationToken);

    // Get the hub URL
    var resourceModel = _app.Services.GetRequiredService<DistributedApplicationModel>();
    var signalRResource = resourceModel.Resources.Single(r => r.Name == "signalr-hub");
    var httpEndpoint = signalRResource.Annotations.OfType<EndpointAnnotation>()
      .First(e => e.Name == "http");
    var allocatedEndpoint = httpEndpoint.AllocatedEndpoint
      ?? throw new InvalidOperationException("HTTP endpoint was not allocated");

    _hubUrl = $"{allocatedEndpoint.UriString}/hubs/documents";
  }

  /// <summary>
  /// Creates a Repo with in-memory storage and SignalR network adapter.
  /// </summary>
  async Task<Six.Automerge.Repository.Repo> CreateRepoAsync(string peerName)
  {
    var loggerFactory = LoggerFactory.Create(builder =>
    {
      builder.AddConsole();
      builder.SetMinimumLevel(LogLevel.Debug);
      builder.AddFilter("Six.Automerge", LogLevel.Debug);
    });
    
    if(string.IsNullOrEmpty(_hubUrl))
      throw new InvalidOperationException("SignalR hub URL is not available");

    // Create SignalR connection service
    var connectionService = TestConnectionServiceFactory.Create(_hubUrl);
    _connectionServices.Add(connectionService);

    // Create SignalR network adapter
    var adapter = new SignalRNetworkAdapter(connectionService, loggerFactory);
    _adapters.Add(adapter);

    // Connect the adapter and wait for it to be ready
    var peerId = PeerId.Generate();
    adapter.Connect(peerId, null);
    await adapter.WhenReady().WaitAsync(TimeSpan.FromSeconds(5));

    // Create repo with in-memory storage
    var repo = new Six.Automerge.Repository.Repo(new RepoConfig
    {
      PeerId = peerId,
      Storage = new InMemoryStorageAdapter(),
      NetworkAdapters = [adapter],
      IsEphemeral = false
    });

    _repos.Add(repo);

    return repo;
  }

  public async Task DisposeAsync()
  {
    // Dispose repos first
    foreach (var repo in _repos)
    {
      try { repo.Dispose(); }
      catch { /* Ignore disposal errors */ }
    }
    _repos.Clear();

    // Dispose adapters
    foreach (var adapter in _adapters)
    {
      try { await adapter.DisposeAsync(); }
      catch { /* Ignore disposal errors */ }
    }
    _adapters.Clear();

    // Dispose connection services
    foreach (var connectionService in _connectionServices)
    {
      try { await connectionService.DisposeAsync(); }
      catch { /* Ignore disposal errors */ }
    }
    _connectionServices.Clear();

    // Dispose the entire app infrastructure (SignalR hub, etc.)
    if (_app is not null)
    {
      await _app.DisposeAsync();
      _app = null;
    }

    // CRITICAL: Force garbage collection to finalize all native resources
    // This ensures all AutomergeDocument instances are properly disposed before the next test.
    // The Rust FFI layer (automerge-0.7.0) has a known issue where actor removal during cleanup
    // panics if there are lingering uncommitted transactions in the Rust heap.
    GC.Collect();
    GC.WaitForPendingFinalizers();

    // CRITICAL: Wait for the automerge Rust FFI library to fully cleanup
    // Without this delay, the next test may crash due to lingering state in the native library
    // Increased to 5 seconds due to persistent threading issues in the Rust FFI
    await Task.Delay(5000);
  }
}
