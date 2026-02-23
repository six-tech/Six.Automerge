using System.Text.Json;

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for persistence and serialization.
/// </summary>
[Trait("Category", "Persistence")]
public sealed class AutomergePersistenceTests
{
    [Fact]
    public void SaveIncremental_ShouldProduceChanges()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        doc.PutString(root, "key", "value");
        doc.Commit("first");

        var incremental = doc.SaveIncremental();
        Assert.NotEmpty(incremental);
    }

    [Fact]
    public void LoadIncremental_ShouldApplyChanges()
    {
        using var doc1 = AutomergeDocument.Create();
        var root1 = doc1.RootId;
        doc1.PutString(root1, "shared", "initial");
        doc1.Commit("init");

        var baseBytes = doc1.Save();

        doc1.PutInt64(root1, "version", 2);
        doc1.Commit("update");
        var incremental = doc1.SaveIncremental();

        using var doc2 = AutomergeDocument.Load(baseBytes);
        var appliedCount = doc2.LoadIncremental(incremental);
        Assert.True(appliedCount > 0);

        var json = doc2.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(2, parsed.RootElement.GetProperty("version").GetInt64());
    }

    [Fact]
    public void IncrementalUpdates_AcrossMultipleCommits_ShouldAccumulate()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        doc.PutInt64(root, "count", 0);
        doc.Commit("init");
        var baseBytes = doc.Save();

        doc.PutInt64(root, "count", 1);
        doc.Commit("update1");

        doc.PutInt64(root, "count", 2);
        doc.Commit("update2");

        var incremental = doc.SaveIncremental();

        using var restored = AutomergeDocument.Load(baseBytes);
        restored.LoadIncremental(incremental);

        var json = restored.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(2, parsed.RootElement.GetProperty("count").GetInt64());
    }
}
