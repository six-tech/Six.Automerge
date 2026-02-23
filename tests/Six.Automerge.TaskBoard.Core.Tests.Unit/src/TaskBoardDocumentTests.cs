// Copyright (c) Six. All rights reserved.

using System.Reactive.Linq;
using Six.Automerge.Repository;
using AutomergeRepo = Six.Automerge.Repository.Repo;

namespace Six.Automerge.TaskBoard.Tests;

[Trait("Category", "Unit")]
public sealed class TaskBoardDocumentTests : IAsyncLifetime
{
  readonly AutomergeRepo _repo = new(new RepoConfig());
  TaskBoardDocument _board = null!;

  public async Task InitializeAsync()
  {
    _board = await TaskBoardDocument.CreateAsync(_repo);
  }

  public Task DisposeAsync()
  {
    _board.Dispose();
    _repo.Dispose();
    return Task.CompletedTask;
  }

  [Fact]
  public async Task CreateAsync_ShouldCreateEmptyBoard()
  {
    var columns = await _board.ReadBoardAsync();

    columns.Should().BeEmpty();
  }

  [Fact]
  public async Task CreateAsync_ShouldHaveDocumentId()
  {
    _board.DocumentId.Should().NotBeNullOrWhiteSpace();
  }

  [Fact]
  public async Task SeedDefaultColumnsAsync_ShouldCreateThreeColumns()
  {
    await _board.SeedDefaultColumnsAsync();

    var columns = await _board.ReadBoardAsync();

    columns.Should().HaveCount(3);
    columns[0].Name.Should().Be("To Do");
    columns[1].Name.Should().Be("In Progress");
    columns[2].Name.Should().Be("Done");
  }

  [Fact]
  public async Task AddColumnAsync_ShouldCreateNamedColumn()
  {
    var column = await _board.AddColumnAsync("Backlog");

    column.Name.Should().Be("Backlog");
    column.Order.Should().Be(0);
    column.ObjectId.Should().NotBeNull();
    column.TasksListId.Should().NotBeNull();

    var columns = await _board.ReadBoardAsync();
    columns.Should().HaveCount(1);
    columns[0].Name.Should().Be("Backlog");
  }

  [Fact]
  public async Task RemoveColumnAsync_ShouldRemoveByIndex()
  {
    await _board.AddColumnAsync("To Do");
    await _board.AddColumnAsync("In Progress");
    await _board.AddColumnAsync("Done");

    await _board.RemoveColumnAsync(1);

    var columns = await _board.ReadBoardAsync();
    columns.Should().HaveCount(2);
    columns[0].Name.Should().Be("To Do");
    columns[1].Name.Should().Be("Done");
  }

  [Fact]
  public async Task AddTaskAsync_ShouldCreateTask()
  {
    var column = await _board.AddColumnAsync("To Do");

    var task = await _board.AddTaskAsync(column.TasksListId, "Write tests", "Add unit tests", "todo");

    task.Title.Should().Be("Write tests");
    task.Description.Should().Be("Add unit tests");
    task.Status.Should().Be("todo");
    task.Votes.Should().Be(0);
    task.ObjectId.Should().NotBeNull();

    var columns = await _board.ReadBoardAsync();
    columns[0].Tasks.Should().HaveCount(1);
    columns[0].Tasks[0].Title.Should().Be("Write tests");
  }

  [Fact]
  public async Task RemoveTaskAsync_ShouldRemoveFromColumn()
  {
    var column = await _board.AddColumnAsync("To Do");
    await _board.AddTaskAsync(column.TasksListId, "Task 1", "Desc 1", "todo");
    await _board.AddTaskAsync(column.TasksListId, "Task 2", "Desc 2", "todo");

    await _board.RemoveTaskAsync(column.TasksListId, 0);

    var columns = await _board.ReadBoardAsync();
    columns[0].Tasks.Should().HaveCount(1);
    columns[0].Tasks[0].Title.Should().Be("Task 2");
  }

  [Fact]
  public async Task MoveTaskAsync_ShouldMoveBetweenColumns()
  {
    var todo = await _board.AddColumnAsync("To Do");
    var done = await _board.AddColumnAsync("Done");
    var task = await _board.AddTaskAsync(todo.TasksListId, "Task 1", "Desc", "todo");

    var moved = await _board.MoveTaskAsync(todo.TasksListId, 0, done.TasksListId, 0, task);

    moved.Title.Should().Be("Task 1");

    var columns = await _board.ReadBoardAsync();
    columns[0].Tasks.Should().BeEmpty();
    columns[1].Tasks.Should().HaveCount(1);
    columns[1].Tasks[0].Title.Should().Be("Task 1");
  }

  [Fact]
  public async Task UpvoteTaskAsync_ShouldIncrementVotes()
  {
    var column = await _board.AddColumnAsync("To Do");
    var task = await _board.AddTaskAsync(column.TasksListId, "Task 1", "Desc", "todo");

    await _board.UpvoteTaskAsync(task.ObjectId);
    await _board.UpvoteTaskAsync(task.ObjectId);
    await _board.UpvoteTaskAsync(task.ObjectId);

    var columns = await _board.ReadBoardAsync();
    columns[0].Tasks[0].Votes.Should().Be(3);
  }

  [Fact]
  public async Task ReadBoardAsync_ShouldReturnAllColumns()
  {
    await _board.AddColumnAsync("Backlog");
    await _board.AddColumnAsync("To Do");
    await _board.AddColumnAsync("Done");

    var columns = await _board.ReadBoardAsync();

    columns.Should().HaveCount(3);
    columns[0].Name.Should().Be("Backlog");
    columns[1].Name.Should().Be("To Do");
    columns[2].Name.Should().Be("Done");
  }

  [Fact]
  public async Task BoardChanged_ShouldEmitForLocalChanges()
  {
    BoardChange? received = null;
    var tcs = new TaskCompletionSource<bool>();
    _board.BoardChanged.Subscribe(change =>
    {
      received = change;
      tcs.TrySetResult(true);
    });

    await _board.AddColumnAsync("To Do");

    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

    received.Should().NotBeNull();
    received!.Value.IsLocal.Should().BeTrue();
  }

  [Fact]
  public async Task Dispose_ShouldNotThrow()
  {
    using var repo = new AutomergeRepo(new RepoConfig());
    var board = await TaskBoardDocument.CreateAsync(repo);

    var act = () => board.Dispose();

    act.Should().NotThrow();
  }
}
