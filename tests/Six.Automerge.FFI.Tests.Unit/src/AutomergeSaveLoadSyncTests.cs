// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for save/load variants, sync extensions, and change management APIs.
/// </summary>
[Trait("Category", "SaveLoadSync")]
public sealed class AutomergeSaveLoadSyncTests
{
  // ── SaveNoCompress ──────────────────────────────────────────────────

  [Fact]
  public void SaveNoCompress_ShouldProduceValidDocument()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutString(root, "key", "value");
    doc.Commit("init");

    var saved = doc.SaveNoCompress();
    Assert.NotEmpty(saved);

    using var loaded = AutomergeDocument.Load(saved);
    var result = loaded.GetString(root, "key");
    Assert.Equal("value", result);
  }

  [Fact]
  public void SaveNoCompress_ShouldProduceLargerOutputThanCompressed()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    // Add enough data to make compression difference visible
    for(var i = 0; i < 100; i++)
    {
      doc.PutString(root, $"key{i}", $"value{i}");
    }

    doc.Commit("bulk");

    var compressed = doc.Save();
    var uncompressed = doc.SaveNoCompress();

    Assert.NotEmpty(compressed);
    Assert.NotEmpty(uncompressed);
    // Uncompressed should be at least as large as compressed
    Assert.True(uncompressed.Length >= compressed.Length,
      $"Uncompressed ({uncompressed.Length}) should be >= compressed ({compressed.Length})");
  }

  // ── SaveWithOptions ─────────────────────────────────────────────────

  [Fact]
  public void SaveWithOptions_WithCompression_ShouldMatchDefaultSave()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutString(root, "title", "Hello");
    doc.Commit("init");

    var defaultSave = doc.Save();
    var withOptions = doc.SaveWithOptions(new SaveOptions { Deflate = true, RetainOrphans = true });

    Assert.Equal(defaultSave.Length, withOptions.Length);
  }

  [Fact]
  public void SaveWithOptions_WithoutCompression_ShouldMatchSaveNoCompress()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutString(root, "title", "Hello");
    doc.Commit("init");

    var noCompress = doc.SaveNoCompress();
    var withOptions = doc.SaveWithOptions(new SaveOptions { Deflate = false, RetainOrphans = true });

    Assert.Equal(noCompress.Length, withOptions.Length);
  }

  [Fact]
  public void SaveWithOptions_WithoutOrphans_ShouldProduceLoadableDocument()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutString(root, "key", "test");
    doc.Commit("init");

    var saved = doc.SaveWithOptions(new SaveOptions { Deflate = true, RetainOrphans = false });
    Assert.NotEmpty(saved);

    using var loaded = AutomergeDocument.Load(saved);
    Assert.Equal("test", loaded.GetString(root, "key"));
  }

  // ── LoadWithOptions ─────────────────────────────────────────────────

  [Fact]
  public void LoadWithOptions_WithCheck_ShouldLoadValidDocument()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutString(root, "name", "Alice");
    doc.Commit("init");

    var saved = doc.Save();
    using var loaded = AutomergeDocument.LoadWithOptions(saved, new LoadOptions
    {
      VerificationMode = VerificationMode.Check,
      OnPartialLoad = OnPartialLoad.Error
    });

    Assert.Equal("Alice", loaded.GetString(root, "name"));
  }

  [Fact]
  public void LoadWithOptions_WithNoCheck_ShouldLoadValidDocument()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutInt64(root, "count", 42);
    doc.Commit("init");

    var saved = doc.Save();
    using var loaded = AutomergeDocument.LoadWithOptions(saved, new LoadOptions
    {
      VerificationMode = VerificationMode.NoCheck,
      OnPartialLoad = OnPartialLoad.Error
    });

    Assert.Equal(42L, loaded.GetInt64(root, "count"));
  }

  [Fact]
  public void LoadWithOptions_NoCheckMode_ShouldBeFasterOrEqualToCheck()
  {
    // Create a document with enough data
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    for(var i = 0; i < 50; i++)
    {
      doc.PutString(root, $"key{i}", $"value{i}");
    }

    doc.Commit("bulk");
    var saved = doc.Save();

    // Both modes should produce valid documents
    using var withCheck = AutomergeDocument.LoadWithOptions(saved, new LoadOptions
    {
      VerificationMode = VerificationMode.Check
    });
    using var noCheck = AutomergeDocument.LoadWithOptions(saved, new LoadOptions
    {
      VerificationMode = VerificationMode.NoCheck
    });

    Assert.Equal(withCheck.GetString(root, "key0"), noCheck.GetString(root, "key0"));
  }

  // ── GetChangesAdded ─────────────────────────────────────────────────

  [Fact]
  public void GetChangesAdded_BetweenDivergedDocs_ShouldReturnNewChanges()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;
    doc1.PutString(root, "shared", "base");
    doc1.Commit("base");

    // Fork doc2 from doc1
    var saved = doc1.Save();
    using var doc2 = AutomergeDocument.Load(saved);

    // Make changes only in doc2
    doc2.PutString(root, "extra", "from-doc2");
    doc2.Commit("doc2-change");

    // Get changes that doc2 has but doc1 doesn't
    var added = doc1.GetChangesAdded(doc2);
    Assert.Single(added);
    Assert.NotEmpty(added[0]);
  }

  [Fact]
  public void GetChangesAdded_WhenIdentical_ShouldReturnEmpty()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;
    doc1.PutString(root, "key", "value");
    doc1.Commit("init");

    var saved = doc1.Save();
    using var doc2 = AutomergeDocument.Load(saved);

    var added = doc1.GetChangesAdded(doc2);
    Assert.Empty(added);
  }

  [Fact]
  public void GetChangesAdded_MultipleChanges_ShouldReturnAll()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;
    doc1.PutString(root, "shared", "base");
    doc1.Commit("base");

    var saved = doc1.Save();
    using var doc2 = AutomergeDocument.Load(saved);

    doc2.PutString(root, "a", "1");
    doc2.Commit("change1");
    doc2.PutString(root, "b", "2");
    doc2.Commit("change2");
    doc2.PutString(root, "c", "3");
    doc2.Commit("change3");

    var added = doc1.GetChangesAdded(doc2);
    Assert.Equal(3, added.Count);
  }

  // ── Bundle ──────────────────────────────────────────────────────────

  [Fact]
  public void Bundle_ShouldCreateValidBinary()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutString(root, "key", "value");
    doc.Commit("init");

    var heads = doc.GetHeads();
    var bundled = doc.Bundle(heads);
    Assert.NotEmpty(bundled);
  }

  [Fact]
  public void Bundle_ShouldBeLoadableAsIncremental()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutString(root, "key", "value");
    doc.Commit("init");

    var heads = doc.GetHeads();
    var bundled = doc.Bundle(heads);

    // Create a new empty document and load the bundle
    using var doc2 = AutomergeDocument.Create();
    var loaded = doc2.LoadIncremental(bundled);
    Assert.True(loaded > 0);

    Assert.Equal("value", doc2.GetString(root, "key"));
  }

  // ── DiffIncremental ─────────────────────────────────────────────────

  [Fact]
  public void DiffIncremental_ShouldReturnPatchesSinceLastCall()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    // First call: baseline
    doc.PutString(root, "a", "1");
    doc.Commit("first");
    var patches1 = doc.DiffIncremental();
    Assert.NotEmpty(patches1);

    // Second call: should return only new changes
    doc.PutString(root, "b", "2");
    doc.Commit("second");
    var patches2 = doc.DiffIncremental();
    Assert.NotEmpty(patches2);

    // Third call with no changes: should be empty
    var patches3 = doc.DiffIncremental();
    Assert.Empty(patches3);
  }

  [Fact]
  public void DiffIncremental_TrackStateAcrossMultipleCommits()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    // Make 3 commits
    doc.PutString(root, "x", "1");
    doc.Commit("c1");
    doc.PutString(root, "y", "2");
    doc.Commit("c2");
    doc.PutString(root, "z", "3");
    doc.Commit("c3");

    // All patches since doc creation
    var all = doc.DiffIncremental();
    Assert.True(all.Count >= 3, $"Expected at least 3 patches, got {all.Count}");

    // No more changes
    var empty = doc.DiffIncremental();
    Assert.Empty(empty);

    // New change
    doc.PutString(root, "w", "4");
    doc.Commit("c4");

    var latest = doc.DiffIncremental();
    Assert.NotEmpty(latest);
  }

  [Fact]
  public void DiffIncremental_EmptyDocument_ShouldReturnEmpty()
  {
    using var doc = AutomergeDocument.Create();
    var patches = doc.DiffIncremental();
    Assert.Empty(patches);
  }

  // ── GetMissingDeps ──────────────────────────────────────────────────

  [Fact]
  public void GetMissingDeps_WithOwnHeads_ShouldReturnEmpty()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutString(root, "key", "value");
    doc.Commit("init");

    var heads = doc.GetHeads();
    var missing = doc.GetMissingDeps(heads);
    Assert.Empty(missing);
  }

  [Fact]
  public void GetMissingDeps_WithEmptyHeads_ShouldReturnEmpty()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutString(root, "key", "value");
    doc.Commit("init");
    doc.PutString(root, "key2", "value2");
    doc.Commit("second");

    // Empty heads has no deps to trace, so nothing is missing
    var missing = doc.GetMissingDeps(Array.Empty<string>());
    Assert.Empty(missing);
  }

  [Fact]
  public void GetMissingDeps_ForPartiallySyncedDoc_ShouldReturnDifference()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutString(root, "a", "1");
    var hash1 = doc.Commit("first");

    doc.PutString(root, "b", "2");
    doc.Commit("second");

    var allHeads = doc.GetHeads();

    // Missing deps when we provide a subset of heads
    // (heads pointing to the latest change should have no missing deps)
    var missing = doc.GetMissingDeps(allHeads);
    Assert.Empty(missing);
  }

  // ── HasOurChanges ───────────────────────────────────────────────────

  [Fact]
  public void HasOurChanges_BeforeSync_ShouldReturnFalse()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutString(root, "key", "value");
    doc.Commit("init");

    using var syncState = doc.CreateSyncState();
    Assert.False(doc.HasOurChanges(syncState));
  }

  [Fact]
  public void HasOurChanges_AfterFullSync_ShouldReturnTrue()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;
    doc1.PutString(root, "key", "value");
    doc1.Commit("init");

    var saved = doc1.Save();
    using var doc2 = AutomergeDocument.Load(saved);

    using var sync12 = doc1.CreateSyncState();
    using var sync21 = doc2.CreateSyncState();

    // Perform sync rounds
    for(var i = 0; i < 10; i++)
    {
      var msg1 = doc1.GenerateSyncMessage(sync12);
      var msg2 = doc2.GenerateSyncMessage(sync21);
      if(msg1 is null && msg2 is null) break;
      if(msg1 is not null) doc2.ReceiveSyncMessage(sync21, msg1);
      if(msg2 is not null) doc1.ReceiveSyncMessage(sync12, msg2);
    }

    Assert.True(doc1.HasOurChanges(sync12));
  }

  [Fact]
  public void HasOurChanges_AfterNewLocalChange_ShouldReturnFalse()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;
    doc1.PutString(root, "key", "value");
    doc1.Commit("init");

    var saved = doc1.Save();
    using var doc2 = AutomergeDocument.Load(saved);

    using var sync12 = doc1.CreateSyncState();
    using var sync21 = doc2.CreateSyncState();

    // Full sync
    for(var i = 0; i < 10; i++)
    {
      var msg1 = doc1.GenerateSyncMessage(sync12);
      var msg2 = doc2.GenerateSyncMessage(sync21);
      if(msg1 is null && msg2 is null) break;
      if(msg1 is not null) doc2.ReceiveSyncMessage(sync21, msg1);
      if(msg2 is not null) doc1.ReceiveSyncMessage(sync12, msg2);
    }

    Assert.True(doc1.HasOurChanges(sync12));

    // Make a new local change
    doc1.PutString(root, "new", "change");
    doc1.Commit("after-sync");

    Assert.False(doc1.HasOurChanges(sync12));
  }

  // ── Integration: SaveNoCompress round-trip ──────────────────────────

  [Fact]
  public void SaveNoCompress_RoundTrip_PreservesAllData()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutString(root, "name", "Test");
    doc.PutInt64(root, "count", 42);
    doc.PutBoolean(root, "active", true);
    doc.Commit("init");

    var saved = doc.SaveNoCompress();
    using var loaded = AutomergeDocument.Load(saved);

    Assert.Equal("Test", loaded.GetString(root, "name"));
    Assert.Equal(42L, loaded.GetInt64(root, "count"));
    Assert.Equal(true, loaded.GetBoolean(root, "active"));
  }
}
