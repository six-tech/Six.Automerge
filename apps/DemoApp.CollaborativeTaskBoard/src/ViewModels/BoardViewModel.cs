// Copyright (c) Six. All rights reserved.

using System.Collections.ObjectModel;
using System.Reactive;
using DemoApp.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Six.Automerge;
using Six.Automerge.TaskBoard;
using ReactiveUI;

namespace DemoApp.CollaborativeTaskBoard.ViewModels;

public class BoardViewModel : ViewModelBase, IDisposable
{
  readonly RepoService? _repoService;
  readonly ILogger<BoardViewModel> _logger;
  readonly Action<Action> _dispatcher;
  TaskBoardDocument? _document;
  IDisposable? _boardSubscription;
  string _statusText = "Ready";
  bool _disposed;

  public BoardViewModel(
    RepoService? repoService = null,
    ILoggerFactory? loggerFactory = null,
    Action<Action>? dispatcher = null)
  {
    _repoService = repoService;
    _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<BoardViewModel>();
    _dispatcher = dispatcher ?? (action => action());

    AddColumnCommand = ReactiveCommand.Create(AddColumn);
    SaveCommand = ReactiveCommand.Create(SaveBoard);
  }

  public ObservableCollection<ColumnViewModel> Columns { get; } = [];

  public string StatusText
  {
    get => _statusText;
    set => this.RaiseAndSetIfChanged(ref _statusText, value);
  }

  public ReactiveCommand<Unit, Unit> AddColumnCommand { get; }

  public ReactiveCommand<Unit, Unit> SaveCommand { get; }

  /// <summary>
  /// Gets the RepoService for connection wiring.
  /// </summary>
  internal RepoService? RepoService => _repoService;

  /// <summary>
  /// Gets the TaskBoardDocument for CRDT operations.
  /// </summary>
  internal TaskBoardDocument? TaskBoardDocument => _document;

  /// <summary>
  /// Sets the TaskBoardDocument for board operations. Subscribes to BoardChanged observable.
  /// </summary>
  internal void SetTaskBoardDocument(TaskBoardDocument document)
  {
    _boardSubscription?.Dispose();
    _document = document;

    _boardSubscription = _document.BoardChanged.Subscribe(change =>
    {
      if(!change.IsLocal)
      {
        _dispatcher(RefreshFromDocument);
      }
    });

    RefreshFromDocument();
  }

  /// <summary>
  /// Seeds default columns and refreshes the view from document state.
  /// Called after creating a new document.
  /// </summary>
  internal void SeedAndRefresh()
  {
    if(_document is null)
    {
      return;
    }

    _ = SeedAndRefreshAsync();
  }

  async Task SeedAndRefreshAsync()
  {
    try
    {
      await _document!.SeedDefaultColumnsAsync();
      _dispatcher(() =>
      {
        RefreshFromDocument();
        StatusText = "Board initialized with default columns";
      });
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Error seeding default columns");
    }
  }

  public void AddColumn()
  {
    if(_document is null)
    {
      return;
    }

    string name = $"Column {Columns.Count + 1}";
    _ = AddColumnInternalAsync(name);
  }

  async Task AddColumnInternalAsync(string name)
  {
    try
    {
      var column = await _document!.AddColumnAsync(name);

      _dispatcher(() =>
      {
        Columns.Add(ToColumnViewModel(column));
        StatusText = $"Added column: {name}";
      });
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Error adding column");
    }
  }

  public void RemoveColumn(ColumnViewModel columnVm)
  {
    if(_document is null)
    {
      return;
    }

    int index = Columns.IndexOf(columnVm);
    if(index < 0)
    {
      return;
    }

    _ = RemoveColumnInternalAsync(index, columnVm);
  }

  async Task RemoveColumnInternalAsync(int index, ColumnViewModel columnVm)
  {
    try
    {
      await _document!.RemoveColumnAsync(index);

      _dispatcher(() =>
      {
        Columns.RemoveAt(index);
        StatusText = $"Removed column: {columnVm.Name}";
      });
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Error removing column");
    }
  }

  public void AddTask(ColumnViewModel columnVm, string title)
  {
    if(_document is null)
    {
      return;
    }

    _ = AddTaskInternalAsync(columnVm, title);
  }

  async Task AddTaskInternalAsync(ColumnViewModel columnVm, string title)
  {
    try
    {
      var task = await _document!.AddTaskAsync(columnVm.TasksListId, title, string.Empty, "todo");

      _dispatcher(() =>
      {
        columnVm.Tasks.Add(ToTaskItemViewModel(task));
        StatusText = $"Added task: {title}";
      });
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Error adding task");
    }
  }

  public void RemoveTask(ColumnViewModel columnVm, TaskItemViewModel taskVm)
  {
    if(_document is null)
    {
      return;
    }

    int index = columnVm.Tasks.IndexOf(taskVm);
    if(index < 0)
    {
      return;
    }

    _ = RemoveTaskInternalAsync(columnVm, taskVm, index);
  }

  async Task RemoveTaskInternalAsync(ColumnViewModel columnVm, TaskItemViewModel taskVm, int index)
  {
    try
    {
      await _document!.RemoveTaskAsync(columnVm.TasksListId, index);

      _dispatcher(() =>
      {
        columnVm.Tasks.RemoveAt(index);
        StatusText = $"Removed task: {taskVm.Title}";
      });
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Error removing task");
    }
  }

  public void MoveTask(
    ColumnViewModel sourceColumn,
    TaskItemViewModel taskVm,
    ColumnViewModel destColumn,
    int destIndex)
  {
    if(_document is null)
    {
      return;
    }

    int sourceIndex = sourceColumn.Tasks.IndexOf(taskVm);
    if(sourceIndex < 0)
    {
      return;
    }

    _ = MoveTaskInternalAsync(sourceColumn, taskVm, destColumn, sourceIndex, destIndex);
  }

  async Task MoveTaskInternalAsync(
    ColumnViewModel sourceColumn,
    TaskItemViewModel taskVm,
    ColumnViewModel destColumn,
    int sourceIndex,
    int destIndex)
  {
    try
    {
      var task = new TaskItem
      {
        ObjectId = taskVm.ObjectId,
        Title = taskVm.Title,
        Description = taskVm.Description,
        Status = taskVm.Status,
        Votes = taskVm.Votes,
      };

      var moved = await _document!.MoveTaskAsync(
        sourceColumn.TasksListId, sourceIndex,
        destColumn.TasksListId, destIndex,
        task);

      _dispatcher(() =>
      {
        sourceColumn.Tasks.RemoveAt(sourceIndex);
        destColumn.Tasks.Insert(destIndex, ToTaskItemViewModel(moved));
        StatusText = $"Moved task: {taskVm.Title}";
      });
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Error moving task");
    }
  }

  public void UpvoteTask(TaskItemViewModel taskVm)
  {
    if(_document is null)
    {
      return;
    }

    _ = UpvoteTaskInternalAsync(taskVm);
  }

  async Task UpvoteTaskInternalAsync(TaskItemViewModel taskVm)
  {
    try
    {
      await _document!.UpvoteTaskAsync(taskVm.ObjectId);

      _dispatcher(() =>
      {
        taskVm.Votes++;
        StatusText = $"Upvoted: {taskVm.Title} ({taskVm.Votes} votes)";
      });
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Error upvoting task");
    }
  }

  void SaveBoard()
  {
    StatusText = "Save not supported in library mode";
  }

  /// <summary>
  /// Rebuilds the Columns collection from the CRDT document state.
  /// </summary>
  internal void RefreshFromDocument()
  {
    if(_document is null)
    {
      return;
    }

    _ = RefreshFromDocumentAsync();
  }

  async Task RefreshFromDocumentAsync()
  {
    try
    {
      var columns = await _document!.ReadBoardAsync();

      _dispatcher(() =>
      {
        Columns.Clear();
        foreach(var column in columns)
        {
          Columns.Add(ToColumnViewModel(column));
        }
      });
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Error refreshing from document");
    }
  }

  static ColumnViewModel ToColumnViewModel(Column column)
  {
    var vm = new ColumnViewModel(column.ObjectId, column.TasksListId, column.Name, column.Order);
    foreach(var task in column.Tasks)
    {
      vm.Tasks.Add(ToTaskItemViewModel(task));
    }

    return vm;
  }

  static TaskItemViewModel ToTaskItemViewModel(TaskItem task)
  {
    return new TaskItemViewModel(task.ObjectId, task.Title, task.Description, task.Status, task.Votes);
  }

  public void Dispose()
  {
    if(!_disposed)
    {
      _disposed = true;
      _boardSubscription?.Dispose();
      _document?.Dispose();
      _repoService?.Dispose();
    }
  }
}
