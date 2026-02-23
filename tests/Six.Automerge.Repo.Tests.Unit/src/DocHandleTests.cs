using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Six.Automerge;
using Six.Automerge.Repository;
using Xunit;

namespace Six.Automerge.Repo.Test.Unit;

[Trait("Category", "DocHandles")]
public sealed class DocHandleTests
{
  static readonly Dictionary<string, DocumentId> IdCache = new();

  static DocHandle<AutomergeDocument> CreateHandle(string id, AutomergeDocument? document = null, bool isNew = false)
  {
    var documentId = DocumentId.TryParse(id, out var parsed)
      ? parsed
      : IdCache.TryGetValue(id, out var cached)
        ? cached
        : IdCache[id] = DocumentId.Generate();

    return new DocHandle<AutomergeDocument>(documentId, new DocHandleOptions<AutomergeDocument> { IsNew = isNew }, document);
  }

  [Fact]
  public async Task Initialize_WithDocument_RaisesReadyEvent()
  {
    var document = AutomergeDocument.Create();
    var handle = CreateHandle("test-doc");

    try
    {
      var readySource = new TaskCompletionSource<DocHandleReadyEventArgs<AutomergeDocument>>(
        TaskCreationOptions.RunContinuationsAsynchronously);

      handle.Ready += (_, args) => readySource.TrySetResult(args);

      handle.Initialize(document, isNewDocument: true);

      var args = await readySource.Task.WaitAsync(TimeSpan.FromSeconds(2));
      Assert.True(handle.IsReady);
      Assert.True(args.IsNew);
      Assert.Same(handle, args.Handle);
    }
    finally
    {
      handle.Dispose();
    }
  }

  [Fact]
  public async Task ChangeAsync_WithMutation_RaisesChangedEventAndQueuesChange()
  {
    var document = AutomergeDocument.Create();
    var handle = CreateHandle("change-doc", document, true);

    try
    {
      var changeSource = new TaskCompletionSource<DocHandleChangeEventArgs<AutomergeDocument>>(
        TaskCreationOptions.RunContinuationsAsynchronously);

      handle.Changed += (_, args) =>
      {
        if(args.IsLocal)
        {
          changeSource.TrySetResult(args);
        }
      };

      var localChange = await handle.ChangeAsync(doc =>
      {
        var root = doc.RootId;
        doc.PutString(root, "name", "automerge");
      }, message: "init");

      Assert.NotNull(localChange);
      Assert.Single(localChange!.Changes);
      Assert.True(handle.Heads.Count > 0);

      var changeArgs = await changeSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
      Assert.Same(handle, changeArgs.Handle);
      Assert.True(changeArgs.IsLocal);
      Assert.NotNull(changeArgs.LocalChange);
      Assert.True(changeArgs.LocalChange!.Changes.Single().Length > 0);

      var drained = handle.DrainLocalChanges();
      Assert.Single(drained);
    }
    finally
    {
      handle.Dispose();
    }
  }

  [Fact]
  public async Task ApplyRemoteChangesAsync_RaisesChangedEvent()
  {
    var remote = AutomergeDocument.Create();
    var root = remote.RootId;
    remote.PutString(root, "title", "remote");
    remote.Commit();
    var change = remote.GetLastLocalChange();
    Assert.NotNull(change);

    var local = AutomergeDocument.Create();
    var handle = CreateHandle("remote-doc", local, true);

    try
    {
      var changeSource = new TaskCompletionSource<DocHandleChangeEventArgs<AutomergeDocument>>(
        TaskCreationOptions.RunContinuationsAsynchronously);

      handle.Changed += (_, args) =>
      {
        if(!args.IsLocal)
        {
          changeSource.TrySetResult(args);
        }
      };

      await handle.ApplyRemoteChangesAsync(new[] { change! });

      var args = await changeSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
      Assert.False(args.IsLocal);
      Assert.NotNull(args.AppliedChanges);
      Assert.Single(args.AppliedChanges!);

      var json = handle.Doc().HydrateJson();
      Assert.Contains("\"title\":\"remote\"", json, StringComparison.Ordinal);
    }
    finally
    {
      remote.Dispose();
      handle.Dispose();
    }
  }

  [Fact]
  public async Task DrainLocalChanges_ClearsQueue()
  {
    var document = AutomergeDocument.Create();
    var handle = CreateHandle("drain-doc", document, true);

    try
    {
      await handle.ChangeAsync(doc =>
      {
        var root = doc.RootId;
        doc.PutString(root, "one", "1");
      });

      await handle.ChangeAsync(doc =>
      {
        var root = doc.RootId;
        doc.PutString(root, "two", "2");
      });

      var first = handle.DrainLocalChanges();
      Assert.Equal(2, first.Count);

      var second = handle.DrainLocalChanges();
      Assert.Empty(second);
    }
    finally
    {
      handle.Dispose();
    }
  }

  [Fact]
  public async Task WaitForDocumentAsync_BlocksUntilInitialized()
  {
    var handle = CreateHandle("pending-doc");

    try
    {
      var waitTask = handle.WhenReadyAsync();
      Assert.False(waitTask.IsCompleted);

      var document = AutomergeDocument.Create();
      handle.Initialize(document, isNewDocument: false);

      await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
      Assert.True(handle.IsReady);
    }
    finally
    {
      handle.Dispose();
    }
  }

  [Fact]
  public void Initialize_Twice_Throws()
  {
    var document = AutomergeDocument.Create();
    var handle = CreateHandle("double-init", document, true);

    try
    {
      var second = AutomergeDocument.Create();
      Assert.Throws<InvalidOperationException>(() => handle.Initialize(second));
      second.Dispose();
    }
    finally
    {
      handle.Dispose();
    }
  }

  [Fact]
  public async Task Dispose_BlocksFurtherOperations()
  {
    var document = AutomergeDocument.Create();
    var handle = CreateHandle("dispose-doc", document, true);

    handle.Dispose();

    await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
      await handle.DocAsync());

    await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
      await handle.ChangeAsync(_ => { }));

    await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
      await handle.ApplyRemoteChangesAsync(Array.Empty<byte[]>()));
  }

  [Fact]
  public void Doc_WhenReady_ReturnsDocument()
  {
    var document = AutomergeDocument.Create();
    var handle = CreateHandle("doc-doc", document, true);

    try
    {
      var returned = handle.Doc();
      Assert.Same(document, returned);
    }
    finally
    {
      handle.Dispose();
    }
  }

  [Fact]
  public void DocSync_WhenNotReady_Throws()
  {
    var handle = CreateHandle("doc-sync");

    try
    {
      var ex = Assert.Throws<InvalidOperationException>(() => handle.DocSync());
      Assert.Contains("not ready", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
    finally
    {
      handle.Dispose();
    }
  }

  [Fact]
  public void Change_SynchronousWrapper_InvokesAsync()
  {
    var document = AutomergeDocument.Create();
    var handle = CreateHandle("sync-change", document, true);

    try
    {
      var result = handle.Change(doc =>
      {
        var root = doc.RootId;
        doc.PutString(root, "value", "sync");
      });

      Assert.NotNull(result);
      Assert.Single(result!.Changes);
      Assert.Contains("sync", handle.Doc().HydrateJson(), StringComparison.Ordinal);
    }
    finally
    {
      handle.Dispose();
    }
  }
}
