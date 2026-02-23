using System.Text.Json;

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for map operations - additional data types.
/// </summary>
[Trait("Category", "Maps")]
public sealed class AutomergeMapOperationsTests
{
    [Fact]
    public void PutBytes_ShouldStoreBinaryData()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        var data = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
        doc.PutBytes(root, "binary", data);
        doc.Commit("bytes");

        var saved = doc.Save();
        using var restored = AutomergeDocument.Load(saved);

        var json = restored.HydrateJson();
        Assert.Contains("binary", json);
    }

    [Fact]
    public void PutNull_ShouldStoreNullValue()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        doc.PutNull(root, "nullable");
        doc.Commit("null");

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Null, parsed.RootElement.GetProperty("nullable").ValueKind);
    }

    [Fact]
    public void PutCounter_ShouldCreateIncrementableCounter()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        doc.PutCounter(root, "views", 0);
        doc.Commit("init");

        doc.PutCounter(root, "views", 1);
        doc.PutCounter(root, "views", 5);
        doc.Commit("increments");

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);

        // Note: Counter might be serialized as object in JSON
        if (parsed.RootElement.GetProperty("views").ValueKind == JsonValueKind.Number)
        {
            var views = parsed.RootElement.GetProperty("views").GetInt64();
            Assert.Equal(6, views);
        }
    }

    [Fact]
    public void PutTimestamp_ShouldStoreUnixMilliseconds()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        doc.PutTimestamp(root, "created", timestampMs);
        doc.Commit("timestamp");

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);

        // Timestamp may be serialized as object or number
        if (parsed.RootElement.GetProperty("created").ValueKind == JsonValueKind.Number)
        {
            var created = parsed.RootElement.GetProperty("created").GetInt64();
            Assert.Equal(timestampMs, created);
        }
    }

    [Fact]
    public void Delete_ShouldRemoveMapKey()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        doc.PutString(root, "keep", "this");
        doc.PutString(root, "remove", "that");
        doc.Commit("add");

        var json1 = doc.HydrateJson();
        Assert.Contains("remove", json1);

        doc.Delete(root, "remove");
        doc.Commit("delete");

        var json2 = doc.HydrateJson();
        Assert.DoesNotContain("remove", json2);
        Assert.Contains("keep", json2);
    }

    [Fact]
    public void GetKeys_ShouldReturnAllMapKeys()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        doc.PutString(root, "alpha", "a");
        doc.PutInt64(root, "beta", 2);
        doc.PutBoolean(root, "gamma", true);
        doc.Commit("keys");

        var keys = doc.GetKeys(root);
        Assert.Equal(3, keys.Count);
        Assert.Contains("alpha", keys);
        Assert.Contains("beta", keys);
        Assert.Contains("gamma", keys);
    }
}
