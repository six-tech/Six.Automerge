// Copyright (c) Six. All rights reserved.

using System.Collections.ObjectModel;
using System.Reactive;
using DemoApp.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Six.Automerge;
using Six.Automerge.Drawing;
using ReactiveUI;

namespace DemoApp.CollaborativeDrawing.ViewModels;

public class CanvasViewModel : ViewModelBase, IDisposable
{
  readonly RepoService? _repoService;
  readonly ILogger<CanvasViewModel> _logger;
  readonly Action<Action> _dispatcher;
  DrawingDocument? _document;
  IDisposable? _canvasSubscription;
  ShapeViewModel? _selectedShape;
  ShapeType _selectedTool = ShapeType.Rectangle;
  string _selectedColor = "#4A90D9";
  string _statusText = "Ready";
  int _shapeCounter;
  bool _disposed;

  public CanvasViewModel(
    RepoService? repoService = null,
    ILoggerFactory? loggerFactory = null,
    Action<Action>? dispatcher = null)
  {
    _repoService = repoService;
    _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<CanvasViewModel>();
    _dispatcher = dispatcher ?? (action => action());

    AddRectangleCommand = ReactiveCommand.Create(() => SelectedTool = ShapeType.Rectangle);
    AddEllipseCommand = ReactiveCommand.Create(() => SelectedTool = ShapeType.Ellipse);
    DeleteCommand = ReactiveCommand.Create(DeleteSelectedShape);
    AddLayerCommand = ReactiveCommand.Create(AddLayer);
    SaveCommand = ReactiveCommand.Create(Save);
  }

  /// <summary>
  /// All shapes across all layers, flattened for canvas rendering.
  /// </summary>
  public ObservableCollection<ShapeViewModel> Shapes { get; } = [];

  /// <summary>
  /// Layers for the layer panel.
  /// </summary>
  public ObservableCollection<LayerViewModel> Layers { get; } = [];

  public ShapeViewModel? SelectedShape
  {
    get => _selectedShape;
    set => this.RaiseAndSetIfChanged(ref _selectedShape, value);
  }

  public ShapeType SelectedTool
  {
    get => _selectedTool;
    set => this.RaiseAndSetIfChanged(ref _selectedTool, value);
  }

  public string SelectedColor
  {
    get => _selectedColor;
    set => this.RaiseAndSetIfChanged(ref _selectedColor, value);
  }

  public string StatusText
  {
    get => _statusText;
    set => this.RaiseAndSetIfChanged(ref _statusText, value);
  }

  public ReactiveCommand<Unit, ShapeType> AddRectangleCommand { get; }

  public ReactiveCommand<Unit, ShapeType> AddEllipseCommand { get; }

  public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

  public ReactiveCommand<Unit, Unit> AddLayerCommand { get; }

  public ReactiveCommand<Unit, Unit> SaveCommand { get; }

  /// <summary>
  /// Gets the RepoService for document operations.
  /// </summary>
  internal RepoService? RepoService => _repoService;

  /// <summary>
  /// Gets the DrawingDocument for canvas CRDT operations.
  /// </summary>
  internal DrawingDocument? DrawingDocument => _document;

  /// <summary>
  /// Sets the DrawingDocument for canvas operations. Subscribes to CanvasChanged observable.
  /// </summary>
  internal void SetDrawingDocument(DrawingDocument document)
  {
    _canvasSubscription?.Dispose();
    _document = document;

    _canvasSubscription = _document.CanvasChanged.Subscribe(change =>
    {
      if(!change.IsLocal)
      {
        _dispatcher(RefreshFromDocument);
      }
    });

    RefreshFromDocument();
  }

  /// <summary>
  /// Adds a shape at the specified canvas position using the current tool and color.
  /// </summary>
  public void AddShape(double x, double y, double width, double height)
  {
    if(_document is null)
    {
      return;
    }

    _shapeCounter++;
    string name = $"{_selectedTool} {_shapeCounter}";

    _ = AddShapeInternalAsync(name, _selectedTool, x, y, width, height, _selectedColor);
  }

  async Task AddShapeInternalAsync(string name, ShapeType type, double x, double y, double width, double height, string color)
  {
    try
    {
      var shape = await _document!.AddShapeAsync(name, type, x, y, width, height, color);

      _dispatcher(() =>
      {
        var vm = ToShapeViewModel(shape);
        Shapes.Add(vm);

        if(Layers.Count > 0)
        {
          Layers[0].Shapes.Add(vm);
        }

        StatusText = $"Added {name}";
      });
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Error adding shape");
    }
  }

  /// <summary>
  /// Moves the selected shape to a new position.
  /// </summary>
  public void MoveSelectedShape(double x, double y)
  {
    if(_selectedShape is null || _document is null)
    {
      return;
    }

    var shapeId = _selectedShape.ObjectId;
    _ = _document.MoveShapeAsync(shapeId, x, y);

    _selectedShape.X = x;
    _selectedShape.Y = y;
    StatusText = $"Moved {_selectedShape.Name}";
  }

  /// <summary>
  /// Resizes the selected shape.
  /// </summary>
  public void ResizeSelectedShape(double width, double height)
  {
    if(_selectedShape is null || _document is null)
    {
      return;
    }

    var shapeId = _selectedShape.ObjectId;
    _ = _document.ResizeShapeAsync(shapeId, width, height);

    _selectedShape.Width = width;
    _selectedShape.Height = height;
    StatusText = $"Resized {_selectedShape.Name}";
  }

  /// <summary>
  /// Deletes the currently selected shape.
  /// </summary>
  public void DeleteSelectedShape()
  {
    if(_selectedShape is null || _document is null)
    {
      return;
    }

    int shapeIndex = Shapes.IndexOf(_selectedShape);
    if(shapeIndex < 0)
    {
      return;
    }

    int layerIndex = FindLayerIndexForShape(_selectedShape);
    if(layerIndex >= 0)
    {
      int indexInLayer = FindShapeIndexInLayer(layerIndex, _selectedShape);
      if(indexInLayer >= 0)
      {
        _ = _document.DeleteShapeAsync(layerIndex, indexInLayer);
        Layers[layerIndex].Shapes.Remove(_selectedShape);
      }
    }

    string name = _selectedShape.Name;
    Shapes.Remove(_selectedShape);
    SelectedShape = null;
    StatusText = $"Deleted {name}";
  }

  /// <summary>
  /// Recolors the selected shape using the current selected color.
  /// </summary>
  public void RecolorSelectedShape()
  {
    if(_selectedShape is null || _document is null)
    {
      return;
    }

    var shapeId = _selectedShape.ObjectId;
    var color = _selectedColor;
    _ = _document.RecolorShapeAsync(shapeId, color);

    _selectedShape.Color = _selectedColor;
    StatusText = $"Recolored {_selectedShape.Name}";
  }

  void AddLayer()
  {
    if(_document is null)
    {
      return;
    }

    string name = $"Layer {Layers.Count + 1}";
    _ = AddLayerInternalAsync(name);
  }

  async Task AddLayerInternalAsync(string name)
  {
    try
    {
      var layer = await _document!.AddLayerAsync(name);

      _dispatcher(() =>
      {
        Layers.Add(ToLayerViewModel(layer));
        StatusText = $"Added layer: {name}";
      });
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Error adding layer");
    }
  }

  void Save()
  {
    StatusText = "Save not supported in library mode";
  }

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
      var layers = await _document!.ReadCanvasAsync();

      _dispatcher(() =>
      {
        Shapes.Clear();
        Layers.Clear();
        _shapeCounter = 0;
        foreach(var layer in layers)
        {
          var layerVm = ToLayerViewModel(layer);
          Layers.Add(layerVm);
          foreach(var shapeVm in layerVm.Shapes)
          {
            Shapes.Add(shapeVm);
            _shapeCounter++;
          }
        }

        SelectedShape = null;
      });
    }
    catch(Exception ex)
    {
      _logger.LogError(ex, "Error refreshing from document");
    }
  }

  int FindLayerIndexForShape(ShapeViewModel shape)
  {
    for(int i = 0; i < Layers.Count; i++)
    {
      if(Layers[i].Shapes.Contains(shape))
      {
        return i;
      }
    }

    return -1;
  }

  int FindShapeIndexInLayer(int layerIndex, ShapeViewModel shape)
  {
    return Layers[layerIndex].Shapes.IndexOf(shape);
  }

  static LayerViewModel ToLayerViewModel(Layer layer)
  {
    var vm = new LayerViewModel(layer.ObjectId, layer.ShapesListId, layer.Name, layer.IsVisible);
    foreach(var shape in layer.Shapes)
    {
      vm.Shapes.Add(ToShapeViewModel(shape));
    }

    return vm;
  }

  static ShapeViewModel ToShapeViewModel(CanvasObject shape)
  {
    return new ShapeViewModel(
      shape.ObjectId,
      shape.Name,
      shape.Type,
      shape.X,
      shape.Y,
      shape.Width,
      shape.Height,
      shape.Color,
      shape.ZOrder);
  }

  public void Dispose()
  {
    if(!_disposed)
    {
      _disposed = true;
      _canvasSubscription?.Dispose();
      _document?.Dispose();
      _repoService?.Dispose();
    }
  }
}
