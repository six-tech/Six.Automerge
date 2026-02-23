// Copyright (c) Six. All rights reserved.

using Six.Automerge;
using Six.Automerge.Drawing;
using ReactiveUI;

namespace DemoApp.CollaborativeDrawing.ViewModels;

public class ShapeViewModel : ViewModelBase
{
  string _name;
  double _x;
  double _y;
  double _width;
  double _height;
  string _color;
  long _zOrder;

  public ShapeViewModel(
    AutomergeObjectId objectId,
    string name,
    ShapeType type,
    double x,
    double y,
    double width,
    double height,
    string color,
    long zOrder)
  {
    ObjectId = objectId;
    _name = name;
    Type = type;
    _x = x;
    _y = y;
    _width = width;
    _height = height;
    _color = color;
    _zOrder = zOrder;
  }

  public AutomergeObjectId ObjectId { get; }

  public ShapeType Type { get; }

  public string Name
  {
    get => _name;
    set => this.RaiseAndSetIfChanged(ref _name, value);
  }

  public double X
  {
    get => _x;
    set => this.RaiseAndSetIfChanged(ref _x, value);
  }

  public double Y
  {
    get => _y;
    set => this.RaiseAndSetIfChanged(ref _y, value);
  }

  public double Width
  {
    get => _width;
    set => this.RaiseAndSetIfChanged(ref _width, value);
  }

  public double Height
  {
    get => _height;
    set => this.RaiseAndSetIfChanged(ref _height, value);
  }

  public string Color
  {
    get => _color;
    set => this.RaiseAndSetIfChanged(ref _color, value);
  }

  public long ZOrder
  {
    get => _zOrder;
    set => this.RaiseAndSetIfChanged(ref _zOrder, value);
  }
}
