using System.Text.Json;

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for nested object structures.
/// </summary>
[Trait("Category", "Nested")]
public sealed class AutomergeNestedObjectsTests
{
    [Fact]
    public void NestedMap_InMap_ShouldCreateHierarchy()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        var userMap = doc.PutObject(root, "user", AutomergeContainerType.Map);
        doc.PutString(userMap, "name", "Alice");

        var addressMap = doc.PutObject(userMap, "address", AutomergeContainerType.Map);
        doc.PutString(addressMap, "city", "Seattle");
        doc.PutString(addressMap, "country", "USA");

        doc.Commit("nested map");

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        var user = parsed.RootElement.GetProperty("user");
        Assert.Equal("Alice", user.GetProperty("name").GetString());
        Assert.Equal("Seattle", user.GetProperty("address").GetProperty("city").GetString());
        Assert.Equal("USA", user.GetProperty("address").GetProperty("country").GetString());
    }

    [Fact]
    public void NestedList_InMap_ShouldWork()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        var listId = doc.PutObject(root, "tags", AutomergeContainerType.List);
        doc.ListInsertString(listId, 0, "csharp");
        doc.ListInsertString(listId, 1, "automerge");
        doc.Commit("list in map");

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        var tags = parsed.RootElement.GetProperty("tags");
        Assert.Equal(2, tags.GetArrayLength());
        Assert.Equal("csharp", tags[0].GetString());
        Assert.Equal("automerge", tags[1].GetString());
    }

    [Fact]
    public void NestedText_InMap_ShouldWork()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        var textId = doc.PutObject(root, "description", AutomergeContainerType.Text);
        doc.SpliceText(textId, 0, 0, "A collaborative document");
        doc.Commit("text in map");

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        var desc = parsed.RootElement.GetProperty("description").GetString();
        Assert.Equal("A collaborative document", desc);
    }

    [Fact]
    public void NestedMap_InList_ShouldWork()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        var listId = doc.PutObject(root, "users", AutomergeContainerType.List);
        var user1 = doc.ListInsertObject(listId, 0, AutomergeContainerType.Map);
        doc.PutString(user1, "name", "Alice");
        doc.PutInt64(user1, "age", 30);

        var user2 = doc.ListInsertObject(listId, 1, AutomergeContainerType.Map);
        doc.PutString(user2, "name", "Bob");
        doc.PutInt64(user2, "age", 25);

        doc.Commit("maps in list");

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        var users = parsed.RootElement.GetProperty("users");
        Assert.Equal("Alice", users[0].GetProperty("name").GetString());
        Assert.Equal(30, users[0].GetProperty("age").GetInt64());
        Assert.Equal("Bob", users[1].GetProperty("name").GetString());
        Assert.Equal(25, users[1].GetProperty("age").GetInt64());
    }

    [Fact]
    public void DeepNesting_ThreeLevels_ShouldWork()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        var level1 = doc.PutObject(root, "level1", AutomergeContainerType.Map);
        var level2 = doc.PutObject(level1, "level2", AutomergeContainerType.Map);
        var level3 = doc.PutObject(level2, "level3", AutomergeContainerType.Map);
        doc.PutString(level3, "deep", "value");

        doc.Commit("deep nesting");

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        var deepValue = parsed.RootElement
            .GetProperty("level1")
            .GetProperty("level2")
            .GetProperty("level3")
            .GetProperty("deep")
            .GetString();
        Assert.Equal("value", deepValue);
    }
}
