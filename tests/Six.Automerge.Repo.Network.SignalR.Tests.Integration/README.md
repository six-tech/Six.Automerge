# SignalR Integration Tests

This directory contains integration tests for the SignalR network adapter implementation in Six.Automerge.

## Overview

These integration tests validate the SignalR network adapter against a production-like environment managed by **[.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/)**. 
The tests use Aspire's orchestration capabilities to spin up a real SignalR hub server and verify that:

- Clients can connect to the hub and complete handshakes
- Multiple clients can sync documents in real-time
- Document subscription and group isolation work correctly
- Error handling and edge cases are properly managed

## Architecture

The test suite uses the following components:

1. **Six.Tests.AppHost**: Aspire AppHost that manages the SignalR hub server lifecycle
2. **Six.SignalR.TestServer**: A minimal ASP.NET Core application hosting the `DocumentsHub`
3. **AspireTestFixture**: xUnit collection fixture that initializes Aspire and provides the hub URL to tests
4. **Test Classes**: `BasicConnectionTests`, `MultiClientSyncTests`, and `ErrorHandlingTests`

### Key Benefits of Aspire-Based Testing

- **Production Parity**: Tests run against the same infrastructure as production (same hub, same configuration)
- **CI/CD Reliability**: Aspire orchestration provides deterministic resource management and health checks
- **Reduced Flakiness**: Fixed ports and proper resource lifecycle eliminate timing issues
- **Developer Experience**: Local test runs match production behavior exactly

## Prerequisites

Before running the tests, ensure you have:

- **.NET 9 SDK or later** (preferably .NET 10 RC as used in this project)
- **Aspire SDK**: The .NET Aspire workload must be installed
  ```bash
  dotnet workload install aspire
  ```
- **Docker** (optional, only if using containerized resources in the future)

## Running the Tests

### Run All Integration Tests

```bash
dotnet test tests/Six.Automerge.Repo.Network.SignalR.Tests.Integration
```

### Run Specific Test Class

```bash
# Run only BasicConnectionTests
dotnet test tests/Six.Automerge.Repo.Network.SignalR.Tests.Integration --filter "FullyQualifiedName~BasicConnectionTests"

# Run only MultiClientSyncTests
dotnet test tests/Six.Automerge.Repo.Network.SignalR.Tests.Integration --filter "FullyQualifiedName~MultiClientSyncTests"

# Run only ErrorHandlingTests
dotnet test tests/Six.Automerge.Repo.Network.SignalR.Tests.Integration --filter "FullyQualifiedName~ErrorHandlingTests"
```

### Run a Single Test

```bash
dotnet test tests/Six.Automerge.Repo.Network.SignalR.Tests.Integration \
  --filter "FullyQualifiedName~Six.Automerge.Repo.Network.SignalR.Tests.Integration.BasicConnectionTests.CanConnectToTestServer"
```

### Run with Verbose Logging

```bash
dotnet test tests/Six.Automerge.Repo.Network.SignalR.Tests.Integration --verbosity normal
```

## Test Structure

### Test Fixture (AspireTestFixture)

The `AspireTestFixture` is an xUnit collection fixture that:

1. Starts the Aspire `DistributedApplication` with the SignalR hub server
2. Waits for the hub to be healthy (via health check endpoint)
3. Provides the hub URL (`http://localhost:8080/hubs/documents`) to all test classes
4. Cleans up resources after all tests complete

**Important**: The fixture dynamically retrieves the endpoint URL from the Aspire resource after it starts. This ensures tests work correctly in both command-line and IDE environments (like Rider or Visual Studio). The port configuration uses `isProxied: false` to bind directly to port 8080, avoiding Aspire's reverse proxy.

### Test Classes

All test classes are decorated with `[Collection("Aspire Test Collection")]` to share the same `AspireTestFixture` instance. This means:

- The SignalR hub server is started **once** for all tests
- Tests run faster because they don't need to spin up/tear down the server for each test class
- Tests within the same class run **sequentially**, but classes can run in **parallel**

## Aspire Configuration

The Aspire AppHost configuration can be found in:

- **AppHost Configuration**: `tests/Six.Tests.AppHost/AppHost.cs`
- **Test Server Project**: `tests/Six.SignalR.TestServer/`

The AppHost configures the SignalR hub as follows:

```csharp
builder.AddProject<Projects.Six_SignalR_TestServer>("signalr-hub")
  .WithHttpEndpoint(port: 8080, name: "http", isProxied: false)
  .WithHttpHealthCheck("/health");
```

**Important**: The `isProxied: false` parameter is crucial - it ensures the server binds directly to port 8080 instead of using a random port with Aspire's reverse proxy. This provides deterministic port allocation for tests.

The test server (`Six.SignalR.TestServer`) is a minimal ASP.NET Core application that:

- Configures SignalR with MessagePack protocol and LZ4 compression
- Maps the `DocumentsHub` at `/hubs/documents`
- Provides a health check endpoint at `/health`

## Troubleshooting

### Running Tests in IDEs (Rider, Visual Studio)

The tests are designed to work in both command-line and IDE environments. The fixture dynamically retrieves the endpoint URL from Aspire, so tests should work regardless of how they're executed.

If tests fail in your IDE but pass on the command line:

1. **Clean and Rebuild**: Sometimes IDE caches can cause issues
   - In Rider: Right-click solution → Clean → Rebuild All
   - In Visual Studio: Build → Clean Solution → Rebuild Solution

2. **Check Test Runner Settings**: Ensure the IDE test runner isn't modifying environment variables or arguments

3. **Verify Aspire Workload**: Make sure the .NET Aspire workload is installed
   ```bash
   dotnet workload list
   ```

4. **Check IDE Logs**: Look for Aspire-specific errors in the IDE's test output window

### Port Already in Use

If you see errors about port 8080 being in use:

1. Check if another process is using port 8080:
   ```bash
   # On Linux/macOS
   lsof -i :8080

   # On Windows
   netstat -ano | findstr :8080
   ```

2. Stop the conflicting process or change the port in `tests/Six.Tests.AppHost/AppHost.cs`

### Tests Timeout

If tests fail with timeout errors:

1. Increase the timeout in `AspireTestFixture.cs` (currently 120 seconds)
2. Check Aspire logs for startup issues (logs are output to console during test run)
3. Verify that the Aspire workload is installed: `dotnet workload list`

### Health Check Failures

If the health check fails:

1. Check that the test server project builds successfully:
   ```bash
   dotnet build tests/Six.SignalR.TestServer
   ```

2. Verify the health endpoint is responding:
   ```bash
   # After tests start, in another terminal
   curl http://localhost:8080/health
   ```

### Resource Cleanup Issues

If Aspire resources don't clean up properly:

1. Manually stop any running DCP processes:
   ```bash
   # On Linux/macOS
   pkill -f "Six.Tests.AppHost"

   # On Windows
   taskkill /F /IM "Six.Tests.AppHost.exe"
   ```

2. Check for orphaned processes:
   ```bash
   ps aux | grep "Six.SignalR.TestServer"
   ```

## CI/CD Considerations

When running these tests in GitHub Actions or other CI/CD environments:

1. **Ensure Aspire SDK is installed** in the CI environment:
   ```yaml
   - name: Install Aspire workload
     run: dotnet workload install aspire
   ```

2. **Port Conflicts**: The fixed port (8080) should be available in CI runners. If not, adjust the port in AppHost.cs

3. **Timeouts**: CI environments may be slower; consider increasing timeouts if needed

4. **Resource Limits**: Ensure the CI runner has sufficient memory and CPU for Aspire orchestration (recommended: 2GB+ RAM)

## Test Coverage

The integration tests cover the following scenarios:

### BasicConnectionTests
- ✅ Single client can connect to the test server
- ✅ Multiple clients can connect simultaneously
- ✅ Clients can subscribe to documents

### MultiClientSyncTests
- ✅ Three clients can sync a shared document
- ✅ Messages are delivered to the correct document group
- ✅ Auto-subscribe works when sending messages
- ✅ Multiple messages are delivered in order

### ErrorHandlingTests
- ✅ Disconnected adapter does not crash on send
- ✅ Sending message with null DocumentId logs warning
- ✅ Concurrent subscribe operations do not deadlock
- ✅ Unsubscribe from non-subscribed document succeeds
- ✅ Multiple disconnects are idempotent

## Further Reading

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Aspire Testing Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/testing)
- [xUnit Collection Fixtures](https://xunit.net/docs/shared-context#collection-fixture)
- [SignalR Documentation](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction)
