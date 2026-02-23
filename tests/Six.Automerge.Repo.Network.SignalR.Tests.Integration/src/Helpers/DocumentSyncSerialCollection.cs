namespace Six.Automerge.Repo.Network.SignalR.Tests.Integration;

/// <summary>
/// Forces DocumentSyncTests to run serially to avoid automerge Rust FFI concurrency issues.
/// </summary>
/// <remarks>
/// The automerge Rust library has threading/global state issues that cause crashes when
/// tests run concurrently. This collection ensures all tests in DocumentSyncTests run
/// one at a time.
/// </remarks>
[CollectionDefinition("DocumentSync Serial Tests", DisableParallelization = true)]
public class DocumentSyncSerialCollection
{
  // This class has no code - it's just a marker for xUnit to identify the collection
  // and enforce serial execution of all tests in this collection.
}
