// Copyright (c) Six Technology, Inc. All rights reserved.
// Licensed under the MIT license.

using FluentAssertions;
using Six.Automerge;
using AutomergeRepo = Six.Automerge.Repository.Repo;

namespace Six.Automerge.SyncServer.Tests.Unit;

[Trait("Category", "SyncServer")]
public sealed class DebouncedPersistenceTests : IAsyncDisposable
{
  readonly InMemoryStorageAdapter _storage = new();
  readonly InMemoryNetworkAdapter _networkAdapter = new();
  readonly AutomergeRepo _repo;

  public DebouncedPersistenceTests()
  {
    _repo = new AutomergeRepo(new RepoConfig
    {
      Storage = _storage,
      NetworkAdapters = [_networkAdapter],
      SaveDebounceRate = 1,
      SyncDebounceRate = 1
    });
  }

  public async ValueTask DisposeAsync()
  {
    await _repo.Shutdown();
    _storage.Dispose();
  }

  [Fact]
  public async Task MarkDirty_TracksDirtyDocuments()
  {
    // Arrange
    await using var persistence = new DebouncedPersistence(
      _repo, TimeSpan.FromSeconds(60), null);

    // Act
    persistence.MarkDirty("doc-1");
    persistence.MarkDirty("doc-2");

    // Assert
    persistence.DirtyCount.Should().Be(2);
  }

  [Fact]
  public async Task MarkDirty_IgnoresDuplicates()
  {
    // Arrange
    await using var persistence = new DebouncedPersistence(
      _repo, TimeSpan.FromSeconds(60), null);

    // Act
    persistence.MarkDirty("doc-1");
    persistence.MarkDirty("doc-1");
    persistence.MarkDirty("doc-1");

    // Assert
    persistence.DirtyCount.Should().Be(1);
  }

  [Fact]
  public async Task FlushAsync_ClearsDirtyDocuments()
  {
    // Arrange
    await using var persistence = new DebouncedPersistence(
      _repo, TimeSpan.FromSeconds(60), null);

    persistence.MarkDirty("doc-1");
    persistence.MarkDirty("doc-2");

    // Act
    await persistence.FlushAsync();

    // Assert
    persistence.DirtyCount.Should().Be(0);
  }

  [Fact]
  public async Task FlushAsync_PersistsDocumentsToStorage()
  {
    // Arrange: create a document and make changes via Change API
    var handle = await _repo.Create<AutomergeDocument>();
    handle.Change(doc =>
    {
      doc.PutString(doc.RootId, "key", "value");
    });

    await using var persistence = new DebouncedPersistence(
      _repo, TimeSpan.FromSeconds(60), null);

    persistence.MarkDirty(handle.DocumentId.Value);

    // Act
    await persistence.FlushAsync();

    // Wait for save to propagate
    await Task.Delay(100);

    // Assert: storage should have the document
    var prefix = new StorageKey(new[] { handle.DocumentId.Value });
    IReadOnlyList<Chunk> chunks = await _storage.LoadRange(prefix);
    chunks.Should().NotBeEmpty("document should be flushed to storage");
  }

  [Fact]
  public async Task Timer_FlushesAutomatically()
  {
    // Arrange: use a short interval
    var handle = await _repo.Create<AutomergeDocument>();
    handle.Change(doc =>
    {
      doc.PutString(doc.RootId, "auto", "flush");
    });

    await using var persistence = new DebouncedPersistence(
      _repo, TimeSpan.FromMilliseconds(50), null);

    persistence.MarkDirty(handle.DocumentId.Value);

    // Act: wait for the timer to fire
    await Task.Delay(200);

    // Assert
    persistence.DirtyCount.Should().Be(0, "timer should have flushed dirty documents");
  }

  [Fact]
  public async Task Dispose_FlushesRemainingDirtyDocuments()
  {
    // Arrange
    var handle = await _repo.Create<AutomergeDocument>();
    handle.Change(doc =>
    {
      doc.PutString(doc.RootId, "dispose", "test");
    });

    var persistence = new DebouncedPersistence(
      _repo, TimeSpan.FromSeconds(60), null);

    persistence.MarkDirty(handle.DocumentId.Value);

    // Act: dispose should flush remaining documents
    await persistence.DisposeAsync();

    // Wait for save to propagate
    await Task.Delay(100);

    // Assert
    var prefix = new StorageKey(new[] { handle.DocumentId.Value });
    IReadOnlyList<Chunk> chunks = await _storage.LoadRange(prefix);
    chunks.Should().NotBeEmpty("dispose should flush remaining dirty documents");
  }
}
