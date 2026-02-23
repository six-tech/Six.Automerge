using System.Text.Json;


namespace Six.Automerge.Tests;

/// <summary>
/// Basic smoke tests for core Automerge document operations.
/// </summary>
[Trait("Category", "Basic")]
public sealed class AutomergeDocumentBasicTests
{
  [Fact]
  public void CreateCommitAndHydrate_ShouldExposeWrittenValue()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "title", "hello world");
    var commitHash = doc.Commit("initial");

    Assert.NotNull(commitHash);
    Assert.NotEmpty(commitHash);

    var json = doc.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    Assert.Equal("hello world", parsed.RootElement.GetProperty("title").GetString());
  }

  [Fact]
  public void SaveAndLoad_ShouldPreserveState()
  {
    using var original = AutomergeDocument.Create();
    var root = original.RootId;
    original.PutInt64(root, "counter", 42);
    original.PutBoolean(root, "flag", true);
    original.Commit("snapshot");

    var headsBefore = original.GetHeads();
    Assert.NotEmpty(headsBefore);

    var bytes = original.Save();
    Assert.NotEmpty(bytes);

    using var restored = AutomergeDocument.Load(bytes);
    var json = restored.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    var rootElement = parsed.RootElement;
    Assert.Equal(42, rootElement.GetProperty("counter").GetInt64());
    Assert.True(rootElement.GetProperty("flag").GetBoolean());
    Assert.NotEmpty(restored.GetHeads());
  }

  [Fact]
  public void ApplyChanges_ShouldMergeRemoteEdits()
  {
    using var author = AutomergeDocument.Create();
    var authorRoot = author.RootId;
    author.PutString(authorRoot, "author_message", "from author");
    author.PutInt64(authorRoot, "author_value", 42);
    author.Commit("author commit");

    using var collaborator = AutomergeDocument.Create();
    var collabRoot = collaborator.RootId;
    collaborator.PutString(collabRoot, "collab_message", "from collaborator");
    collaborator.PutInt64(collabRoot, "collab_value", 100);
    collaborator.Commit("collab commit");

    // Pull the latest changes from author into collaborator
    var authorChanges = author.GetChanges([]);
    collaborator.ApplyChanges(authorChanges);

    // After merge, collaborator should have both sets of data
    var hydrated = collaborator.HydrateJson();
    using var parsed = JsonDocument.Parse(hydrated);

    Assert.Equal("from author", parsed.RootElement.GetProperty("author_message").GetString());
    Assert.Equal(42, parsed.RootElement.GetProperty("author_value").GetInt64());
    Assert.Equal("from collaborator", parsed.RootElement.GetProperty("collab_message").GetString());
    Assert.Equal(100, parsed.RootElement.GetProperty("collab_value").GetInt64());
  }

  [Fact]
  public void Create_WithFriendlyActorId_ShouldRoundTripAlias()
  {
    const string actorId = "user-alice-device-1";
    using var doc = AutomergeDocument.Create(actorId);

    Assert.Equal(actorId, doc.ActorId);
  }

  [Fact]
  public void Create_WithHexActorId_ShouldReturnHex()
  {
    const string actorHex = "0123456789abcdef0123456789abcdef";
    using var doc = AutomergeDocument.Create(actorHex);

    Assert.Equal(actorHex, doc.ActorId);
  }
}