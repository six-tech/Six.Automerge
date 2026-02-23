// Copyright (c) Six. All rights reserved.

using Six.Automerge;
using ReactiveUI;

namespace DemoApp.CollaborativeTaskBoard.ViewModels;

public class TaskItemViewModel : ViewModelBase
{
  string _title;
  string _description;
  string _status;
  long _votes;

  public TaskItemViewModel(
    AutomergeObjectId objectId,
    string title,
    string description,
    string status,
    long votes)
  {
    ObjectId = objectId;
    _title = title;
    _description = description;
    _status = status;
    _votes = votes;
  }

  public AutomergeObjectId ObjectId { get; set; }

  public string Title
  {
    get => _title;
    set => this.RaiseAndSetIfChanged(ref _title, value);
  }

  public string Description
  {
    get => _description;
    set => this.RaiseAndSetIfChanged(ref _description, value);
  }

  public string Status
  {
    get => _status;
    set => this.RaiseAndSetIfChanged(ref _status, value);
  }

  public long Votes
  {
    get => _votes;
    set => this.RaiseAndSetIfChanged(ref _votes, value);
  }
}
