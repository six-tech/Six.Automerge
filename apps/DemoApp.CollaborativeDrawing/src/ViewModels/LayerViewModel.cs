// Copyright (c) Six. All rights reserved.

using System.Collections.ObjectModel;
using Six.Automerge;
using ReactiveUI;

namespace DemoApp.CollaborativeDrawing.ViewModels;

public class LayerViewModel : ViewModelBase
{
  string _name;
  bool _isVisible;

  public LayerViewModel(AutomergeObjectId objectId, AutomergeObjectId shapesListId, string name, bool isVisible)
  {
    ObjectId = objectId;
    ShapesListId = shapesListId;
    _name = name;
    _isVisible = isVisible;
  }

  public AutomergeObjectId ObjectId { get; }

  public AutomergeObjectId ShapesListId { get; }

  public string Name
  {
    get => _name;
    set => this.RaiseAndSetIfChanged(ref _name, value);
  }

  public bool IsVisible
  {
    get => _isVisible;
    set => this.RaiseAndSetIfChanged(ref _isVisible, value);
  }

  public ObservableCollection<ShapeViewModel> Shapes { get; } = [];
}
