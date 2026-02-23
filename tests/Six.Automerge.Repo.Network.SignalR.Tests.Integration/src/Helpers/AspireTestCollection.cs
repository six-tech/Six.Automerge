namespace Six.Automerge.Repo.Network.SignalR.Tests.Integration;

/// <summary>
/// Defines an xUnit collection for SignalR integration tests using the AspireTestFixture.
/// All test classes decorated with [Collection("Aspire Test Collection")] will share a single
/// instance of the AspireTestFixture, which manages the lifecycle of the SignalR hub server.
/// </summary>
/// <remarks>
/// This collection-level fixture provides the following benefits:
/// <list type="bullet">
///   <item><description>The Aspire application and SignalR hub are started once for all tests in the collection</description></item>
///   <item><description>Tests run faster because they don't need to start/stop the server for each test class</description></item>
///   <item><description>Better resource utilization and isolation between test classes</description></item>
///   <item><description>Production-like environment with proper health checks and resource management</description></item>
/// </list>
/// </remarks>
[CollectionDefinition("Aspire Test Collection")]
public class AspireTestCollection : ICollectionFixture<AspireTestFixture>
{
  // This class has no code, it's just a marker for xUnit to identify the collection fixture.
  // xUnit will automatically create a single instance of AspireTestFixture and pass it to
  // all test classes that are part of this collection.
}
