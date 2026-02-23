// Copyright (c) Six. All rights reserved.

using DemoApp.CollaborativeTaskBoard.ViewModels;
using FluentAssertions;
using Six.Automerge.Repository;
using Six.Automerge.TaskBoard;
using Xunit;

namespace DemoApp.CollaborativeTaskBoard.Tests;

[Trait("Category", "Unit")]
public sealed class BoardViewModelTests : IDisposable
{
  readonly BoardViewModel _viewModel;
  readonly TaskBoardDocument _doc;

  public BoardViewModelTests()
  {
    var repo = new Repo(new RepoConfig());
    _doc = TaskBoardDocument.CreateAsync(repo).GetAwaiter().GetResult();
    _viewModel = new BoardViewModel(dispatcher: action => action());
    _viewModel.SetTaskBoardDocument(_doc);
  }

  [Fact]
  public void Constructor_WithoutRepoService_ShouldHaveEmptyColumns()
  {
    using var vm = new BoardViewModel();
    vm.Columns.Should().BeEmpty();
    vm.StatusText.Should().Be("Ready");
  }

  [Fact]
  public void TaskBoardDocument_ShouldBeAccessibleAfterSet()
  {
    _viewModel.TaskBoardDocument.Should().BeSameAs(_doc);
  }

  [Fact]
  public async Task SeedAndRefresh_ShouldPopulateColumns()
  {
    _viewModel.SeedAndRefresh();
    await Task.Delay(100);

    _viewModel.Columns.Should().HaveCount(3);
    _viewModel.Columns[0].Name.Should().Be("To Do");
    _viewModel.Columns[1].Name.Should().Be("In Progress");
    _viewModel.Columns[2].Name.Should().Be("Done");
  }

  [Fact]
  public async Task AddColumn_ShouldAppendNewColumn()
  {
    _viewModel.SeedAndRefresh();
    await Task.Delay(100);

    _viewModel.AddColumn();
    await Task.Delay(100);

    _viewModel.Columns.Should().HaveCount(4);
    _viewModel.Columns[3].Name.Should().Be("Column 4");
  }

  [Fact]
  public async Task RemoveColumn_ShouldRemoveSpecifiedColumn()
  {
    _viewModel.SeedAndRefresh();
    await Task.Delay(100);

    var column = _viewModel.Columns[1];
    _viewModel.RemoveColumn(column);
    await Task.Delay(100);

    _viewModel.Columns.Should().HaveCount(2);
    _viewModel.Columns[0].Name.Should().Be("To Do");
    _viewModel.Columns[1].Name.Should().Be("Done");
  }

  [Fact]
  public async Task AddTask_ShouldAddTaskToColumn()
  {
    _viewModel.SeedAndRefresh();
    await Task.Delay(100);

    var column = _viewModel.Columns[0];
    _viewModel.AddTask(column, "New task");
    await Task.Delay(100);

    column.Tasks.Should().HaveCount(1);
    column.Tasks[0].Title.Should().Be("New task");
    column.Tasks[0].Status.Should().Be("todo");
    column.Tasks[0].Votes.Should().Be(0);
  }

  [Fact]
  public async Task RemoveTask_ShouldRemoveTaskFromColumn()
  {
    _viewModel.SeedAndRefresh();
    await Task.Delay(100);

    var column = _viewModel.Columns[0];
    _viewModel.AddTask(column, "Task 1");
    await Task.Delay(100);
    _viewModel.AddTask(column, "Task 2");
    await Task.Delay(100);

    _viewModel.RemoveTask(column, column.Tasks[0]);
    await Task.Delay(100);

    column.Tasks.Should().HaveCount(1);
    column.Tasks[0].Title.Should().Be("Task 2");
  }

  [Fact]
  public async Task MoveTask_ShouldMoveTaskBetweenColumns()
  {
    _viewModel.SeedAndRefresh();
    await Task.Delay(100);

    var todo = _viewModel.Columns[0];
    var done = _viewModel.Columns[2];
    _viewModel.AddTask(todo, "Task to move");
    await Task.Delay(100);

    var task = todo.Tasks[0];
    _viewModel.MoveTask(todo, task, done, 0);
    await Task.Delay(100);

    todo.Tasks.Should().BeEmpty();
    done.Tasks.Should().HaveCount(1);
    done.Tasks[0].Title.Should().Be("Task to move");
  }

  [Fact]
  public async Task UpvoteTask_ShouldIncrementVoteCount()
  {
    _viewModel.SeedAndRefresh();
    await Task.Delay(100);

    var column = _viewModel.Columns[0];
    _viewModel.AddTask(column, "Voteable task");
    await Task.Delay(100);

    var task = column.Tasks[0];
    _viewModel.UpvoteTask(task);
    await Task.Delay(100);
    _viewModel.UpvoteTask(task);
    await Task.Delay(100);

    task.Votes.Should().Be(2);
  }

  [Fact]
  public async Task StatusText_ShouldUpdateAfterOperations()
  {
    _viewModel.SeedAndRefresh();
    await Task.Delay(100);

    _viewModel.AddColumn();
    await Task.Delay(100);

    _viewModel.StatusText.Should().Contain("Added column");
  }

  [Fact]
  public async Task RefreshFromDocument_ShouldRebuildColumnsFromCrdtState()
  {
    // Directly modify the CRDT without going through ViewModel
    await _doc.AddColumnAsync("Alpha");
    await _doc.AddColumnAsync("Beta");

    _viewModel.RefreshFromDocument();
    await Task.Delay(100);

    _viewModel.Columns.Should().HaveCount(2);
    _viewModel.Columns[0].Name.Should().Be("Alpha");
    _viewModel.Columns[1].Name.Should().Be("Beta");
  }

  [Fact]
  public void RepoService_ShouldBeExposedForConnectionWiring()
  {
    _viewModel.RepoService.Should().BeNull();
  }

  [Fact]
  public async Task OnRemoteChange_ShouldRefreshColumnsAfterDocumentChange()
  {
    _viewModel.SeedAndRefresh();
    await Task.Delay(100);
    _viewModel.Columns.Should().HaveCount(3);

    // Simulate a remote change by directly modifying the document
    await _doc.AddColumnAsync("Remote Column");

    _viewModel.RefreshFromDocument();
    await Task.Delay(100);

    _viewModel.Columns.Should().HaveCount(4);
    _viewModel.Columns[3].Name.Should().Be("Remote Column");
  }

  [Fact]
  public async Task OnRemoteChange_ShouldPreserveTaskState()
  {
    _viewModel.SeedAndRefresh();
    await Task.Delay(100);

    // Add a task via the VM
    _viewModel.AddTask(_viewModel.Columns[0], "My Task");
    await Task.Delay(100);
    _viewModel.Columns[0].Tasks.Should().HaveCount(1);

    // Read the board to get the second column's TasksListId, then add a task directly
    var board = await _doc.ReadBoardAsync();
    await _doc.AddTaskAsync(board[1].TasksListId, "Remote Task", "From peer", "todo");

    _viewModel.RefreshFromDocument();
    await Task.Delay(100);

    // Both tasks should be present after refresh
    _viewModel.Columns[0].Tasks.Should().HaveCount(1);
    _viewModel.Columns[0].Tasks[0].Title.Should().Be("My Task");
    _viewModel.Columns[1].Tasks.Should().HaveCount(1);
    _viewModel.Columns[1].Tasks[0].Title.Should().Be("Remote Task");
  }

  [Fact]
  public void Dispose_ShouldNotThrow()
  {
    var act = () => _viewModel.Dispose();
    act.Should().NotThrow();
  }

  public void Dispose()
  {
    _viewModel.Dispose();
  }
}
