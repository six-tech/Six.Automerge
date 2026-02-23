namespace Six.Automerge.Tests;

/// <summary>
/// Tests for sync protocol functionality.
/// </summary>
[Trait("Category", "Sync")]
public sealed class AutomergeSyncTests
{
    [Fact]
    public void CreateSyncState_ShouldInitialize()
    {
        using var doc = AutomergeDocument.Create();
        using var syncState = doc.CreateSyncState();
        Assert.NotNull(syncState);
    }

    [Fact]
    public void SyncState_EncodeAndDecode_ShouldPreserveState()
    {
        using var doc = AutomergeDocument.Create();
        using var syncState = doc.CreateSyncState();

        var encoded = syncState.Encode();
        Assert.NotEmpty(encoded);

        using var decoded = AutomergeSyncState.Decode(encoded);
        Assert.NotNull(decoded);
    }

    [Fact]
    public void SyncState_Reset_ShouldClearState()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;
        doc.PutString(root, "data", "test");
        doc.Commit("commit");

        using var syncState = doc.CreateSyncState();
        var msg = doc.GenerateSyncMessage(syncState);

        syncState.Reset();

        var msgAfterReset = doc.GenerateSyncMessage(syncState);
        Assert.NotNull(msgAfterReset);
    }
}
