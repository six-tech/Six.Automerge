// Copyright (c) Six. All rights reserved.

using System.Collections.ObjectModel;
using Six.Automerge;
using ReactiveUI;

namespace DemoApp.CollaborativeTaskBoard.ViewModels;

public class ColumnViewModel : ViewModelBase
{
  string _name;

  public ColumnViewModel(AutomergeObjectId objectId, AutomergeObjectId tasksListId, string name, long order)
  {
    ObjectId = objectId;
    TasksListId = tasksListId;
    _name = name;
    Order = order;
  }

  public AutomergeObjectId ObjectId { get; }

  public AutomergeObjectId TasksListId { get; }

  public string Name
  {
    get => _name;
    set => this.RaiseAndSetIfChanged(ref _name, value);
  }

  public long Order { get; }

  public ObservableCollection<TaskItemViewModel> Tasks { get; } = [];
}
