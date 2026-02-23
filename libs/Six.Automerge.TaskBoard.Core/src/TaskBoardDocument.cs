// Copyright (c) Six. All rights reserved.

using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Six.Automerge.Repository;

namespace Six.Automerge.TaskBoard;

/// <summary>
/// High-level wrapper around an Automerge document for a collaborative task board.
/// </summary>
[PublicAPI]
public sealed class TaskBoardDocument : IDisposable
{
  readonly DocHandle<object> _handle;
  readonly AutomergeObjectId _columnsListId;
  readonly ILogger _logger;

  readonly Subject<Unit> _remoteRefreshes = new();
  readonly Subject<BoardChange> _boardChanged = new();
  readonly IDisposable _remoteSubscription;

  int _localChangeDepth;
  bool _isDisposed;

  /// <summary>
  /// Observable stream of board changes (local and remote).
  /// </summary>
  public IObservable<BoardChange> BoardChanged => _boardChanged;

  /// <summary>
  /// Gets the underlying document handle.
  /// </summary>
  public DocHandle<object> Handle => _handle;

  /// <summary>
  /// Gets the document ID.
  /// </summary>
  public string DocumentId => _handle.DocumentId;

  /// <summary>
  /// Gets the columns list object ID.
  /// </summary>
  internal AutomergeObjectId ColumnsListId => _columnsListId;

  TaskBoardDocument(DocHandle<object> handle, AutomergeObjectId columnsListId, ILoggerFactory? loggerFactory)
  {
    _handle = handle;
    _columnsListId = columnsListId;
    _logger = (loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance)
      .CreateLogger<TaskBoardDocument>();

    _remoteSubscription = _remoteRefreshes
      .Throttle(TimeSpan.FromMilliseconds(50))
      .Select(_ => Observable.FromAsync(RefreshAndNotifyRemoteAsync))
      .Concat()
      .Subscribe();

    _handle.Changed += OnDocHandleChanged;
  }

  /// <summary>
  /// Creates a new document with a columns list and returns a wired <see cref="TaskBoardDocument"/>.
  /// </summary>
  public static async Task<TaskBoardDocument> CreateAsync(
    Repository.Repo repo,
    ILoggerFactory? loggerFactory = null,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(repo);

    var handle = await repo.Create<object>();

    AutomergeObjectId columnsListId = default!;
    await handle.ChangeAsync(doc =>
    {
      columnsListId = doc.PutObject(doc.RootId, "columns", AutomergeContainerType.List);
    }, cancellationToken: cancellationToken);

    return new TaskBoardDocument(handle, columnsListId, loggerFactory);
  }

  /// <summary>
  /// Opens an existing task board document.
  /// Returns null if the document is not found within the timeout period.
  /// </summary>
  public static async Task<TaskBoardDocument?> OpenAsync(
    Repository.Repo repo,
    string documentId,
    ILoggerFactory? loggerFactory = null,
    TimeSpan? timeout = null,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(repo);
    ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

    var handle = await repo.FindAsync<object>(documentId, timeout);
    if(handle is null)
      return null;

    var columnsListId = await handle.ReadAsync(
      doc => doc.GetObject(doc.RootId, "columns"),
      cancellationToken);

    if(columnsListId is null)
      return null;

    return new TaskBoardDocument(handle, columnsListId, loggerFactory);
  }

  /// <summary>
  /// Adds a new column at the end of the board.
  /// </summary>
  public async Task<Column> AddColumnAsync(string name, CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();
    var columnsListId = _columnsListId;

    AutomergeObjectId columnId = default!;
    AutomergeObjectId tasksListId = default!;
    long order = 0;

    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle.ChangeAsync(doc =>
      {
        ulong index = doc.ListLength(columnsListId);
        columnId = doc.ListInsertObject(columnsListId, index, AutomergeContainerType.Map);
        doc.PutString(columnId, "name", name);
        doc.PutInt64(columnId, "order", (long)index);
        tasksListId = doc.PutObject(columnId, "tasks", AutomergeContainerType.List);
        order = (long)index;
      }, cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    var result = new Column
    {
      ObjectId = columnId, TasksListId = tasksListId,
      Name = name, Order = order,
    };

    NotifyLocalChange();
    return result;
  }

  /// <summary>
  /// Removes a column by its index in the columns list.
  /// </summary>
  public async Task RemoveColumnAsync(int index, CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();
    var columnsListId = _columnsListId;

    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle.ChangeAsync(doc =>
      {
        doc.ListRemove(columnsListId, (ulong)index);
      }, cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    NotifyLocalChange();
  }

  /// <summary>
  /// Adds a new task to the specified column's task list.
  /// </summary>
  public async Task<TaskItem> AddTaskAsync(
    AutomergeObjectId tasksListId, string title, string description, string status,
    CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();

    AutomergeObjectId taskId = default!;

    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle.ChangeAsync(doc =>
      {
        ulong index = doc.ListLength(tasksListId);
        taskId = doc.ListInsertObject(tasksListId, index, AutomergeContainerType.Map);
        doc.PutString(taskId, "title", title);
        doc.PutString(taskId, "description", description);
        doc.PutString(taskId, "status", status);
        doc.PutCounter(taskId, "votes", 0);
      }, cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    var result = new TaskItem
    {
      ObjectId = taskId, Title = title,
      Description = description, Status = status, Votes = 0,
    };

    NotifyLocalChange();
    return result;
  }

  /// <summary>
  /// Removes a task by its index from the specified column's task list.
  /// </summary>
  public async Task RemoveTaskAsync(AutomergeObjectId tasksListId, int index,
    CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();
    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle.ChangeAsync(doc =>
      {
        doc.ListRemove(tasksListId, (ulong)index);
      }, cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    NotifyLocalChange();
  }

  /// <summary>
  /// Moves a task from one column to another.
  /// </summary>
  public async Task<TaskItem> MoveTaskAsync(
    AutomergeObjectId sourceTasksListId, int sourceIndex,
    AutomergeObjectId destTasksListId, int destIndex,
    TaskItem task,
    CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();

    AutomergeObjectId newTaskId = default!;

    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle.ChangeAsync(doc =>
      {
        doc.ListRemove(sourceTasksListId, (ulong)sourceIndex);
        newTaskId = doc.ListInsertObject(destTasksListId, (ulong)destIndex, AutomergeContainerType.Map);
        doc.PutString(newTaskId, "title", task.Title);
        doc.PutString(newTaskId, "description", task.Description);
        doc.PutString(newTaskId, "status", task.Status);
        doc.PutCounter(newTaskId, "votes", task.Votes);
      }, cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    var result = new TaskItem
    {
      ObjectId = newTaskId, Title = task.Title,
      Description = task.Description, Status = task.Status, Votes = task.Votes,
    };

    NotifyLocalChange();
    return result;
  }

  /// <summary>
  /// Increments the vote counter on a task.
  /// </summary>
  public async Task UpvoteTaskAsync(AutomergeObjectId taskId, long delta = 1,
    CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();
    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle.ChangeAsync(doc =>
      {
        doc.Increment(taskId, "votes", delta);
      }, cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    NotifyLocalChange();
  }

  /// <summary>
  /// Seeds default columns ("To Do", "In Progress", "Done").
  /// </summary>
  public async Task SeedDefaultColumnsAsync(CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();
    var columnsListId = _columnsListId;

    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle.ChangeAsync(doc =>
      {
        string[] names = ["To Do", "In Progress", "Done"];
        foreach(string name in names)
        {
          ulong index = doc.ListLength(columnsListId);
          var columnId = doc.ListInsertObject(columnsListId, index, AutomergeContainerType.Map);
          doc.PutString(columnId, "name", name);
          doc.PutInt64(columnId, "order", (long)index);
          doc.PutObject(columnId, "tasks", AutomergeContainerType.List);
        }
      }, cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    NotifyLocalChange();
  }

  /// <summary>
  /// Reads the full board state from the document.
  /// </summary>
  public async Task<List<Column>> ReadBoardAsync(CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();
    var columnsListId = _columnsListId;

    return await _handle.ReadAsync(doc => ReadBoardFromDoc(doc, columnsListId), cancellationToken);
  }

  static List<Column> ReadBoardFromDoc(AutomergeDocument doc, AutomergeObjectId columnsListId)
  {
    var columns = new List<Column>();
    ulong columnCount = doc.ListLength(columnsListId);

    for(ulong i = 0; i < columnCount; i++)
    {
      var columnId = doc.ListGetObject(columnsListId, i);
      if(columnId is null) continue;

      var tasksListId = doc.GetObject(columnId, "tasks");
      if(tasksListId is null) continue;

      var column = new Column
      {
        ObjectId = columnId, TasksListId = tasksListId,
        Name = doc.GetString(columnId, "name") ?? string.Empty,
        Order = doc.GetInt64(columnId, "order") ?? 0,
      };

      ulong taskCount = doc.ListLength(tasksListId);
      for(ulong j = 0; j < taskCount; j++)
      {
        var taskId = doc.ListGetObject(tasksListId, j);
        if(taskId is null) continue;

        column.Tasks.Add(new TaskItem
        {
          ObjectId = taskId,
          Title = doc.GetString(taskId, "title") ?? string.Empty,
          Description = doc.GetString(taskId, "description") ?? string.Empty,
          Status = doc.GetString(taskId, "status") ?? string.Empty,
          Votes = doc.GetCounter(taskId, "votes") ?? 0,
        });
      }

      columns.Add(column);
    }

    return columns;
  }

  void OnDocHandleChanged(object? sender, DocHandleChangeEventArgs<object> e)
  {
    if(_isDisposed || Volatile.Read(ref _localChangeDepth) > 0)
      return;

    _remoteRefreshes.OnNext(Unit.Default);
  }

  async Task RefreshAndNotifyRemoteAsync()
  {
    var columns = await ReadBoardAsync(CancellationToken.None);
    _boardChanged.OnNext(new BoardChange(columns, false));
  }

  void NotifyLocalChange()
  {
    _boardChanged.OnNext(new BoardChange([], true));
  }

  void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);

  public void Dispose()
  {
    if(_isDisposed) return;
    _isDisposed = true;

    _handle.Changed -= OnDocHandleChanged;

    _remoteRefreshes.OnCompleted();
    _remoteSubscription.Dispose();

    _boardChanged.OnCompleted();

    _remoteRefreshes.Dispose();
    _boardChanged.Dispose();
  }
}
