using System.Text.Json;

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for commit history and rollback functionality.
/// </summary>
[Trait("Category", "History")]
public sealed class AutomergeHistoryTests
{
    [Fact]
    public void Commit_WithMessageAndTimestamp_ShouldReturnHash()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;
        doc.PutString(root, "data", "test");

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var hash = doc.Commit("Custom message", timestamp);

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void GetLastLocalChange_ShouldReturnRecentCommit()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        doc.PutString(root, "key", "value");
        doc.Commit("change 1");

        var lastChange = doc.GetLastLocalChange();
        Assert.NotNull(lastChange);
        Assert.NotEmpty(lastChange);
    }

    [Fact]
    public void Rollback_WithPendingOps_ShouldDiscardChanges()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        doc.PutString(root, "committed", "yes");
        doc.Commit("save this");

        var headsAfterCommit = doc.GetHeads();

        doc.PutString(root, "pending", "no");
        Assert.True(doc.PendingOperations > 0);

        var rolledBack = doc.Rollback();
        Assert.True(rolledBack > 0);
        Assert.Equal(0UL, doc.PendingOperations);

        var json = doc.HydrateJson();
        Assert.DoesNotContain("pending", json);
        Assert.Contains("committed", json);

        var headsAfterRollback = doc.GetHeads();
        Assert.Equal(headsAfterCommit, headsAfterRollback);
    }

  [Fact]
  public void GetHeads_ShouldTrackDocumentState()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "v1", "data");
    doc.Commit("commit 1");
    var heads1 = doc.GetHeads();
    Assert.NotEmpty(heads1);

    doc.PutString(root, "v2", "more");
    doc.Commit("commit 2");
    var heads2 = doc.GetHeads();
    Assert.NotEmpty(heads2);

    Assert.NotEqual(heads1, heads2);
  }

  [Fact]
  public void Diff_BetweenHeads_ShouldReturnPatches()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "title", "Hello");
    doc.Commit("initial");
    var initialHeads = doc.GetHeads().ToArray();

    doc.PutString(root, "title", "Greetings");
    doc.Commit("update");
    var updatedHeads = doc.GetHeads().ToArray();

    var patches = doc.Diff(initialHeads, updatedHeads);
    Assert.Single(patches);

    var patch = patches[0];
    Assert.Empty(patch.Path);
    Assert.Equal(AutomergePatchActionKind.PutMap, patch.Action.Kind);
    Assert.Equal("title", patch.Action.Key);
    Assert.NotNull(patch.Action.Value);
    Assert.Equal(AutomergeValueType.Scalar, patch.Action.Value!.Type);
    var scalar = patch.Action.Value.Scalar;
    Assert.NotNull(scalar);
    Assert.Equal(AutomergeScalarValueKind.String, scalar!.Kind);
    Assert.Equal("Greetings", scalar.StringValue);
  }

  [Fact]
  public void TopoHistoryTraversal_ShouldReturnHashes()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "value", "one");
    doc.Commit("change 1");
    doc.PutString(root, "value", "two");
    doc.Commit("change 2");

    var history = doc.TopoHistoryTraversal();
    Assert.True(history.Count >= 2);
    Assert.All(history, hash => Assert.False(string.IsNullOrWhiteSpace(hash)));
  }

  [Fact]
  public void IsolateAndIntegrate_ShouldToggleDocumentView()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "value", "initial");
    doc.Commit("initial");
    var baselineHeads = doc.GetHeads().ToArray();

    doc.PutString(root, "value", "updated");
    doc.Commit("update");
    var currentHeads = doc.GetHeads().ToArray();

    // Ensure we see the latest value.
    Assert.Contains("updated", doc.HydrateJson(), StringComparison.Ordinal);

    // Isolate to baseline heads
    doc.Isolate(baselineHeads);
    // After isolating, we need to explicitly read at the isolated heads
    var isolatedJson = doc.HydrateJsonAtHeads(baselineHeads);
    Assert.Contains("initial", isolatedJson, StringComparison.Ordinal);
    Assert.DoesNotContain("updated", isolatedJson, StringComparison.Ordinal);

    // Integrate back to current heads
    doc.Integrate();
    var integratedJson = doc.HydrateJson();
    Assert.Contains("updated", integratedJson, StringComparison.Ordinal);

    // sanity check diff between baseline and current still works
    var patches = doc.Diff(baselineHeads, currentHeads);
    Assert.Single(patches);
  }

  [Fact]
  public void HydrateJsonAtHeads_ShouldShowHistoricalState()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "value", "initial");
    doc.Commit("initial");
    var baselineHeads = doc.GetHeads().ToArray();

    doc.PutString(root, "value", "updated");
    doc.Commit("update");

    // Ensure we see the latest value in the original document
    Assert.Contains("updated", doc.HydrateJson(), StringComparison.Ordinal);

    // Read at the baseline heads using HydrateJsonAtHeads
    var viewJson = doc.HydrateJsonAtHeads(baselineHeads);
    Assert.Contains("initial", viewJson, StringComparison.Ordinal);
    Assert.DoesNotContain("updated", viewJson, StringComparison.Ordinal);

    // Ensure original document is still at current heads
    Assert.Contains("updated", doc.HydrateJson(), StringComparison.Ordinal);
  }
}
