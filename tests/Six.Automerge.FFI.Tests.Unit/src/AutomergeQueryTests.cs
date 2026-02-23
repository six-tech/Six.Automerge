namespace Six.Automerge.Tests;

/// <summary>
/// Tests for object type queries.
/// </summary>
[Trait("Category", "Queries")]
public sealed class AutomergeQueryTests
{
    [Fact]
    public void GetObjectType_ShouldReturnCorrectTypes()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        var mapId = doc.PutObject(root, "map", AutomergeContainerType.Map);
        var listId = doc.PutObject(root, "list", AutomergeContainerType.List);
        var textId = doc.PutObject(root, "text", AutomergeContainerType.Text);

        Assert.Equal(AutomergeContainerType.Map, doc.GetObjectType(mapId));
        Assert.Equal(AutomergeContainerType.List, doc.GetObjectType(listId));
        Assert.Equal(AutomergeContainerType.Text, doc.GetObjectType(textId));
    }

    [Fact]
    public void RootId_ShouldBeConsistent()
    {
        using var doc = AutomergeDocument.Create();
        var root1 = doc.RootId;
        var root2 = doc.RootId;

        Assert.Equal(root1, root2);

        doc.PutString(root1, "test", "data");
        doc.Commit("commit");
        var bytes = doc.Save();

        using var restored = AutomergeDocument.Load(bytes);
        var rootRestored = restored.RootId;

        Assert.Equal(root1.ToString(), rootRestored.ToString());
    }
}
