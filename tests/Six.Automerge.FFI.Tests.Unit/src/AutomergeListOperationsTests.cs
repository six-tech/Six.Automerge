using System.Text.Json;

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for list operations.
/// </summary>
[Trait("Category", "Lists")]
public sealed class AutomergeListOperationsTests
{
    [Fact]
    public void ListInsertString_ShouldAddStringToList()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;
        var listId = doc.PutObject(root, "items", AutomergeContainerType.List);

        doc.ListInsertString(listId, 0, "first");
        doc.ListInsertString(listId, 1, "second");
        doc.ListInsertString(listId, 1, "middle");
        doc.Commit("add strings");

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        var items = parsed.RootElement.GetProperty("items");
        Assert.Equal(3, items.GetArrayLength());
        Assert.Equal("first", items[0].GetString());
        Assert.Equal("middle", items[1].GetString());
        Assert.Equal("second", items[2].GetString());
    }

    [Fact]
    public void ListInsertPrimitiveTypes_ShouldAddAllTypes()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;
        var listId = doc.PutObject(root, "mixed", AutomergeContainerType.List);

        doc.ListInsertBoolean(listId, 0, true);
        doc.ListInsertInt64(listId, 1, -42);
        doc.ListInsertUInt64(listId, 2, 100);
        doc.ListInsertDouble(listId, 3, 3.14);
        doc.ListInsertBytes(listId, 4, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
        doc.ListInsertNull(listId, 5);
        doc.Commit("mixed types");

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        var mixed = parsed.RootElement.GetProperty("mixed");
        Assert.Equal(6, mixed.GetArrayLength());
        Assert.True(mixed[0].GetBoolean());
        Assert.Equal(-42, mixed[1].GetInt64());
        Assert.Equal(100UL, mixed[2].GetUInt64());
        Assert.Equal(3.14, mixed[3].GetDouble(), precision: 2);
        Assert.Equal(JsonValueKind.Null, mixed[5].ValueKind);
    }

    [Fact]
    public void ListRemove_ShouldDeleteElement()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;
        var listId = doc.PutObject(root, "numbers", AutomergeContainerType.List);

        doc.ListInsertInt64(listId, 0, 10);
        doc.ListInsertInt64(listId, 1, 20);
        doc.ListInsertInt64(listId, 2, 30);
        doc.Commit("add");

        Assert.Equal(3UL, doc.ListLength(listId));

        doc.ListRemove(listId, 1);
        doc.Commit("remove");

        Assert.Equal(2UL, doc.ListLength(listId));

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        var numbers = parsed.RootElement.GetProperty("numbers");
        Assert.Equal(2, numbers.GetArrayLength());
        Assert.Equal(10, numbers[0].GetInt64());
        Assert.Equal(30, numbers[1].GetInt64());
    }

    [Fact]
    public void ListLength_ShouldReturnCorrectSize()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;
        var listId = doc.PutObject(root, "items", AutomergeContainerType.List);

        Assert.Equal(0UL, doc.ListLength(listId));

        doc.ListInsertString(listId, 0, "a");
        Assert.Equal(1UL, doc.ListLength(listId));

        doc.ListInsertString(listId, 1, "b");
        doc.ListInsertString(listId, 2, "c");
        Assert.Equal(3UL, doc.ListLength(listId));

        doc.ListRemove(listId, 0);
        Assert.Equal(2UL, doc.ListLength(listId));
    }

    [Fact]
    public void ListInsertObject_ShouldCreateNestedContainers()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;
        var listId = doc.PutObject(root, "items", AutomergeContainerType.List);

        var nestedMap = doc.ListInsertObject(listId, 0, AutomergeContainerType.Map);
        doc.PutString(nestedMap, "name", "nested");

        var nestedList = doc.ListInsertObject(listId, 1, AutomergeContainerType.List);
        doc.ListInsertInt64(nestedList, 0, 42);

        doc.Commit("nested");

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        var items = parsed.RootElement.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal("nested", items[0].GetProperty("name").GetString());
        Assert.Equal(42, items[1][0].GetInt64());
    }

    [Fact]
    public void ListOperations_WithEmptyList_ShouldHandleCorrectly()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;
        var listId = doc.PutObject(root, "empty", AutomergeContainerType.List);

        Assert.Equal(0UL, doc.ListLength(listId));

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        var empty = parsed.RootElement.GetProperty("empty");
        Assert.Equal(0, empty.GetArrayLength());
    }

    [Fact]
    public void ListInsertBytes_ShouldStoreBinaryData()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;
        var listId = doc.PutObject(root, "binaries", AutomergeContainerType.List);

        var bytes1 = new byte[] { 0x01, 0x02, 0x03 };
        var bytes2 = new byte[] { 0xFF, 0xFE };
        doc.ListInsertBytes(listId, 0, bytes1);
        doc.ListInsertBytes(listId, 1, bytes2);
        doc.Commit("bytes");

        Assert.Equal(2UL, doc.ListLength(listId));

        var saved = doc.Save();
        using var restored = AutomergeDocument.Load(saved);
        Assert.Equal(2UL, restored.ListLength(listId));
    }
}
