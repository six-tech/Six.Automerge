using System.Text.Json;

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for text operations.
/// </summary>
[Trait("Category", "Text")]
public sealed class AutomergeTextOperationsTests
{
    [Fact]
    public void SpliceText_InsertText_ShouldAddCharacters()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;
        var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);

        doc.SpliceText(textId, 0, 0, "Hello");
        doc.SpliceText(textId, 5, 0, " World");
        doc.Commit("insert text");

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        var content = parsed.RootElement.GetProperty("content").GetString();
        Assert.Equal("Hello World", content);
    }

    [Fact]
    public void SpliceText_DeleteText_ShouldRemoveCharacters()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;
        var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);

        doc.SpliceText(textId, 0, 0, "Hello World");
        doc.Commit("initial");

        doc.SpliceText(textId, 5, 6, "");
        doc.Commit("delete");

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        var content = parsed.RootElement.GetProperty("content").GetString();
        Assert.Equal("Hello", content);
    }

    [Fact]
    public void SpliceText_ReplaceText_ShouldModifyCharacters()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;
        var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);

        doc.SpliceText(textId, 0, 0, "Hello World");
        doc.Commit("initial");

        doc.SpliceText(textId, 6, 5, "C#");
        doc.Commit("replace");

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        var content = parsed.RootElement.GetProperty("content").GetString();
        Assert.Equal("Hello C#", content);
    }

    [Fact]
    public void Text_ComplexEditSequence_ShouldMaintainCorrectState()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;
        var textId = doc.PutObject(root, "doc", AutomergeContainerType.Text);

        doc.SpliceText(textId, 0, 0, "The quick brown fox");
        doc.SpliceText(textId, 4, 6, "slow");
        doc.SpliceText(textId, 13, 0, " lazy");
        doc.SpliceText(textId, 0, 0, "Title: ");
        doc.Commit("edits");

        var json = doc.HydrateJson();
        using var parsed = JsonDocument.Parse(json);
        var docText = parsed.RootElement.GetProperty("doc").GetString();
        Assert.Equal("Title: The slowbrown lazy fox", docText);
    }
}
