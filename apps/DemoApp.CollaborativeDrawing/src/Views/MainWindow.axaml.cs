// Copyright (c) Six. All rights reserved.

using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using DemoApp.CollaborativeDrawing.ViewModels;
using DemoApp.Shared;
using DemoApp.Shared.Controls;
using Six.Automerge.Drawing;
using Six.Automerge.Repository;

namespace DemoApp.CollaborativeDrawing.Views;

public partial class MainWindow : Window
{
  Canvas? _canvas;
  CanvasViewModel? _viewModel;
  DocumentSessionDialogViewModel? _sessionVm;
  ConnectionStatusBarViewModel? _statusBarVm;

  // Drag state
  bool _isDragging;
  Point _dragStart;
  ShapeViewModel? _dragTarget;

  // Draw state (click on empty canvas to create shape)
  bool _isDrawing;
  Point _drawStart;
  Border? _drawPreview;

  // Selection rectangle
  Border? _selectionBorder;

  // Map from ShapeViewModel to its rendered control
  readonly Dictionary<ShapeViewModel, Control> _shapeControls = [];

  public MainWindow()
  {
    InitializeComponent();
  }

  protected override async void OnLoaded(RoutedEventArgs e)
  {
    base.OnLoaded(e);
    _canvas = this.FindControl<Canvas>("DrawingCanvas");
    _viewModel = DataContext as CanvasViewModel;

    if(_viewModel is not null)
    {
      _viewModel.Shapes.CollectionChanged += OnShapesCollectionChanged;
      RenderAllShapes();
    }

    // Initialize session dialog
    _sessionVm = new DocumentSessionDialogViewModel();
    _sessionVm.ResultReady += OnSessionResultReady;

    var sessionDialog = this.FindControl<DocumentSessionDialog>("SessionDialog");
    if(sessionDialog is not null)
    {
      sessionDialog.DataContext = _sessionVm;
      sessionDialog.RefreshRequested += OnRefreshRequested;
    }

    // Initialize connection status bar
    _statusBarVm = new ConnectionStatusBarViewModel();
    var statusBar = this.FindControl<ContentControl>("ConnectionStatusBar");
    if(statusBar is not null)
    {
      statusBar.DataContext = _statusBarVm;
    }

    // Load stored documents, initialize connection, then fetch server documents
    if(_viewModel?.RepoService is not null)
    {
      _sessionVm.LoadStoredDocuments(_viewModel.RepoService.StorageDirectory);
      await _viewModel.RepoService.InitializeAsync();
      WireConnectionEvents(_viewModel.RepoService);
      await _sessionVm.LoadServerDocumentsAsync(_viewModel.RepoService);
      _sessionVm.SubscribeToDocumentsChanged(_viewModel.RepoService);
    }
  }

  async void OnRefreshRequested(object? sender, EventArgs e)
  {
    if(_sessionVm is null || _viewModel?.RepoService is null)
    {
      return;
    }

    await _sessionVm.LoadServerDocumentsAsync(_viewModel.RepoService);
  }

  async void OnSessionResultReady(object? sender, EventArgs e)
  {
    if(_sessionVm?.Result is null || _viewModel is null)
    {
      return;
    }

    var result = _sessionVm.Result;
    _sessionVm.IsLoading = true;
    _sessionVm.ErrorMessage = null;

    try
    {
      switch(result.Action)
      {
        case SessionAction.Create:
          await HandleCreateAsync();
          break;
        case SessionAction.Join:
          await HandleJoinAsync(result.DocumentId!);
          break;
        case SessionAction.Resume:
          await HandleResumeAsync(result.DocumentId!);
          break;
      }

      await Dispatcher.UIThread.InvokeAsync(() => _sessionVm.IsLoading = false);
    }
    catch(OperationCanceledException)
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        if(_viewModel is not null)
        {
          _viewModel.StatusText = "Operation cancelled";
        }

        _sessionVm.IsLoading = false;
      });
    }
    catch(TimeoutException)
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        if(_viewModel is not null)
        {
          _viewModel.StatusText = "Operation timed out";
        }

        _sessionVm.ErrorMessage = "The operation timed out. The document may be unavailable or the server is not responding.";
        _sessionVm.IsLoading = false;
      });
    }
    catch(Exception ex)
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        if(_viewModel is not null)
        {
          _viewModel.StatusText = $"Error: {ex.Message}";
        }

        _sessionVm.ErrorMessage = ex.Message;
        _sessionVm.IsLoading = false;
      });
    }
  }

  async Task HandleCreateAsync()
  {
    if(_viewModel is null)
    {
      return;
    }

    if(_viewModel.RepoService is null)
    {
      // Offline mode: create a standalone DrawingDocument
      var repo = new Repo(new RepoConfig());
      var drawingDoc = await DrawingDocument.CreateAsync(repo);
      await Dispatcher.UIThread.InvokeAsync(() => _viewModel.SetDrawingDocument(drawingDoc));
      await ShowCanvasAsync(null);
      return;
    }

    await _viewModel.RepoService.InitializeAsync();
    WireConnectionEvents(_viewModel.RepoService);

    var doc = await DrawingDocument.CreateAsync(_viewModel.RepoService.Repo!);
    await Dispatcher.UIThread.InvokeAsync(() => _viewModel.SetDrawingDocument(doc));

    await ShowCanvasAsync(doc.DocumentId);
    await FetchInitialDocumentPeersAsync(_viewModel.RepoService, doc.DocumentId);
  }

  async Task HandleJoinAsync(string documentId)
  {
    if(_viewModel?.RepoService is null)
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
        _sessionVm!.ErrorMessage = "Cannot join: no server connection");
      return;
    }

    await _viewModel.RepoService.InitializeAsync();
    WireConnectionEvents(_viewModel.RepoService);

    await Dispatcher.UIThread.InvokeAsync(() => _viewModel.StatusText = "Joining document...");

    var doc = await DrawingDocument.OpenAsync(
      _viewModel.RepoService.Repo!, documentId, timeout: TimeSpan.FromSeconds(30));
    if(doc is null)
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
        _sessionVm!.ErrorMessage = "Document not found");
      return;
    }

    await Dispatcher.UIThread.InvokeAsync(() => _viewModel.SetDrawingDocument(doc));

    await ShowCanvasAsync(doc.DocumentId);
    await FetchInitialDocumentPeersAsync(_viewModel.RepoService, doc.DocumentId);
  }

  async Task HandleResumeAsync(string documentId)
  {
    if(_viewModel?.RepoService is null)
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
        _sessionVm!.ErrorMessage = "Cannot resume: no server connection");
      return;
    }

    await _viewModel.RepoService.InitializeAsync();
    WireConnectionEvents(_viewModel.RepoService);

    await Dispatcher.UIThread.InvokeAsync(() => _viewModel.StatusText = "Resuming document...");

    var doc = await DrawingDocument.OpenAsync(
      _viewModel.RepoService.Repo!, documentId, timeout: TimeSpan.FromSeconds(10));
    if(doc is null)
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
        _sessionVm!.ErrorMessage = "Document not found in storage");
      return;
    }

    await Dispatcher.UIThread.InvokeAsync(() => _viewModel.SetDrawingDocument(doc));

    await ShowCanvasAsync(doc.DocumentId);
    await FetchInitialDocumentPeersAsync(_viewModel.RepoService, doc.DocumentId);
  }

  void WireConnectionEvents(RepoService repoService)
  {
    if(_statusBarVm is null)
    {
      return;
    }

    _statusBarVm.PeerId = repoService.PeerId?.ToString();
    _statusBarVm.ServerUrl = repoService is { IsInitialized: true }
      ? "Connected"
      : "Offline";

    // Set initial connection state (event handler below only fires on future changes)
    _statusBarVm.ConnectionState = repoService.ConnectionState switch
    {
      ConnectionState.Connected => DemoApp.Shared.ConnectionState.Connected,
      ConnectionState.Connecting => DemoApp.Shared.ConnectionState.Connecting,
      ConnectionState.Reconnecting => DemoApp.Shared.ConnectionState.Reconnecting,
      _ => DemoApp.Shared.ConnectionState.Disconnected
    };

    repoService.ConnectionStateChanged += (_, state) =>
      Dispatcher.UIThread.Post(() =>
      {
        if(_statusBarVm is null)
        {
          return;
        }

        _statusBarVm.ConnectionState = state switch
        {
          ConnectionState.Connected => ConnectionState.Connected,
          ConnectionState.Connecting => ConnectionState.Connecting,
          ConnectionState.Reconnecting => ConnectionState.Reconnecting,
          _ => ConnectionState.Disconnected
        };
      });

    repoService.DocumentPeerCountChanged += (_, count) =>
      Dispatcher.UIThread.Post(() =>
      {
        if(_statusBarVm is not null)
        {
          _statusBarVm.PeerCount = count;
        }
      });
  }

  async Task FetchInitialDocumentPeersAsync(RepoService repoService, string documentId)
  {
    try
    {
      var peers = await repoService.GetDocumentPeersAsync(documentId);
      int count = peers.Count + 1; // +1 to include self

      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        if(_statusBarVm is not null)
        {
          _statusBarVm.PeerCount = count;
        }
      });
    }
    catch
    {
      // Non-critical — peer count will update via events
    }
  }

  async Task ShowCanvasAsync(string? documentId)
  {
    await Dispatcher.UIThread.InvokeAsync(() =>
    {
      var sessionPanel = this.FindControl<Control>("SessionPanel");
      var canvasPanel = this.FindControl<Control>("CanvasPanel");

      if(sessionPanel is not null)
      {
        sessionPanel.IsVisible = false;
      }

      if(canvasPanel is not null)
      {
        canvasPanel.IsVisible = true;
      }

      if(_statusBarVm is not null)
      {
        _statusBarVm.DocumentId = documentId ?? "local";
      }
    });
  }

  // --- Shapes collection changed handler ---

  void OnShapesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
  {
    if(_canvas is null || _viewModel is null)
    {
      return;
    }

    switch(e.Action)
    {
      case NotifyCollectionChangedAction.Add:
        if(e.NewItems is not null)
        {
          foreach(ShapeViewModel shape in e.NewItems)
          {
            AddShapeToCanvas(shape);
          }
        }

        break;

      case NotifyCollectionChangedAction.Remove:
        if(e.OldItems is not null)
        {
          foreach(ShapeViewModel shape in e.OldItems)
          {
            RemoveShapeFromCanvas(shape);
          }
        }

        break;

      case NotifyCollectionChangedAction.Reset:
        ClearAllShapes();
        RenderAllShapes();
        break;
    }
  }

  void RenderAllShapes()
  {
    if(_viewModel is null)
    {
      return;
    }

    foreach(var shape in _viewModel.Shapes)
    {
      AddShapeToCanvas(shape);
    }
  }

  void AddShapeToCanvas(ShapeViewModel shape)
  {
    if(_canvas is null)
    {
      return;
    }

    Control shapeControl = CreateShapeControl(shape);
    Canvas.SetLeft(shapeControl, shape.X);
    Canvas.SetTop(shapeControl, shape.Y);
    _canvas.Children.Add(shapeControl);
    _shapeControls[shape] = shapeControl;

    // Listen for property changes to update rendering
    shape.PropertyChanged += (_, args) =>
    {
      if(args.PropertyName is "X" or "Y")
      {
        Canvas.SetLeft(shapeControl, shape.X);
        Canvas.SetTop(shapeControl, shape.Y);
      }
      else if(args.PropertyName is "Width" or "Height")
      {
        shapeControl.Width = shape.Width;
        shapeControl.Height = shape.Height;
      }
      else if(args.PropertyName == "Color")
      {
        UpdateShapeColor(shapeControl, shape);
      }
    };
  }

  Control CreateShapeControl(ShapeViewModel shape)
  {
    var brush = new SolidColorBrush(Color.Parse(shape.Color));

    Control inner;
    if(shape.Type == ShapeType.Ellipse)
    {
      inner = new Ellipse
      {
        Width = shape.Width,
        Height = shape.Height,
        Fill = brush,
        Stroke = Brushes.Transparent,
        StrokeThickness = 2,
      };
    }
    else
    {
      inner = new Rectangle
      {
        Width = shape.Width,
        Height = shape.Height,
        Fill = brush,
        Stroke = Brushes.Transparent,
        StrokeThickness = 2,
        RadiusX = 2,
        RadiusY = 2,
      };
    }

    var wrapper = new Border
    {
      Child = inner,
      Tag = shape,
      Cursor = new Cursor(StandardCursorType.Hand),
    };

    wrapper.PointerPressed += OnShapePointerPressed;

    return wrapper;
  }

  void UpdateShapeColor(Control control, ShapeViewModel shape)
  {
    if(control is Border { Child: Shape avShape })
    {
      avShape.Fill = new SolidColorBrush(Color.Parse(shape.Color));
    }
  }

  void RemoveShapeFromCanvas(ShapeViewModel shape)
  {
    if(_canvas is null || !_shapeControls.TryGetValue(shape, out var control))
    {
      return;
    }

    _canvas.Children.Remove(control);
    _shapeControls.Remove(shape);
  }

  void ClearAllShapes()
  {
    if(_canvas is null)
    {
      return;
    }

    foreach(var control in _shapeControls.Values)
    {
      _canvas.Children.Remove(control);
    }

    _shapeControls.Clear();
  }

  void UpdateSelectionVisual()
  {
    if(_canvas is null || _viewModel is null)
    {
      return;
    }

    // Remove old selection border
    if(_selectionBorder is not null)
    {
      _canvas.Children.Remove(_selectionBorder);
      _selectionBorder = null;
    }

    // Add selection border around selected shape
    if(_viewModel.SelectedShape is not null && _shapeControls.TryGetValue(_viewModel.SelectedShape, out var control))
    {
      _selectionBorder = new Border
      {
        Width = _viewModel.SelectedShape.Width + 4,
        Height = _viewModel.SelectedShape.Height + 4,
        BorderBrush = new SolidColorBrush(Color.Parse("#4A90D9")),
        BorderThickness = new Thickness(2),
        IsHitTestVisible = false,
      };

      Canvas.SetLeft(_selectionBorder, _viewModel.SelectedShape.X - 2);
      Canvas.SetTop(_selectionBorder, _viewModel.SelectedShape.Y - 2);
      _canvas.Children.Add(_selectionBorder);
    }
  }

  // --- Pointer event handlers ---

  void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
  {
    if(_canvas is null || _viewModel is null)
    {
      return;
    }

    var point = e.GetPosition(_canvas);

    // Deselect current shape if clicking on empty canvas
    _viewModel.SelectedShape = null;
    UpdateSelectionVisual();

    // Start drawing a new shape
    _isDrawing = true;
    _drawStart = point;

    _drawPreview = new Border
    {
      BorderBrush = new SolidColorBrush(Color.Parse("#999")),
      BorderThickness = new Thickness(1),
      Background = new SolidColorBrush(Color.Parse(_viewModel.SelectedColor), 0.3),
    };

    Canvas.SetLeft(_drawPreview, point.X);
    Canvas.SetTop(_drawPreview, point.Y);
    _canvas.Children.Add(_drawPreview);

    e.Handled = true;
  }

  void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
  {
    if(_canvas is null || _viewModel is null)
    {
      return;
    }

    var point = e.GetPosition(_canvas);

    if(_isDragging && _dragTarget is not null)
    {
      double dx = point.X - _dragStart.X;
      double dy = point.Y - _dragStart.Y;
      double newX = _dragTarget.X + dx;
      double newY = _dragTarget.Y + dy;

      // Update VM and visual immediately (commit on release)
      _dragTarget.X = newX;
      _dragTarget.Y = newY;
      _dragStart = point;

      // Update selection visual
      if(_selectionBorder is not null)
      {
        Canvas.SetLeft(_selectionBorder, newX - 2);
        Canvas.SetTop(_selectionBorder, newY - 2);
      }
    }
    else if(_isDrawing && _drawPreview is not null)
    {
      double x = Math.Min(_drawStart.X, point.X);
      double y = Math.Min(_drawStart.Y, point.Y);
      double w = Math.Abs(point.X - _drawStart.X);
      double h = Math.Abs(point.Y - _drawStart.Y);

      Canvas.SetLeft(_drawPreview, x);
      Canvas.SetTop(_drawPreview, y);
      _drawPreview.Width = w;
      _drawPreview.Height = h;
    }
  }

  void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
  {
    if(_canvas is null || _viewModel is null)
    {
      return;
    }

    var point = e.GetPosition(_canvas);

    if(_isDragging && _dragTarget is not null)
    {
      // Commit the move to the CRDT
      _viewModel.MoveSelectedShape(_dragTarget.X, _dragTarget.Y);
      _isDragging = false;
      _dragTarget = null;
    }
    else if(_isDrawing)
    {
      _isDrawing = false;

      // Remove preview
      if(_drawPreview is not null)
      {
        _canvas.Children.Remove(_drawPreview);
        _drawPreview = null;
      }

      double x = Math.Min(_drawStart.X, point.X);
      double y = Math.Min(_drawStart.Y, point.Y);
      double w = Math.Abs(point.X - _drawStart.X);
      double h = Math.Abs(point.Y - _drawStart.Y);

      // Minimum size to avoid accidental micro-shapes
      if(w < 10)
      {
        w = 80;
      }

      if(h < 10)
      {
        h = 60;
      }

      _viewModel.AddShape(x, y, w, h);

      // Select the newly created shape
      if(_viewModel.Shapes.Count > 0)
      {
        _viewModel.SelectedShape = _viewModel.Shapes[^1];
        UpdateSelectionVisual();
      }
    }
  }

  void OnShapePointerPressed(object? sender, PointerPressedEventArgs e)
  {
    if(_canvas is null || _viewModel is null)
    {
      return;
    }

    if(sender is Border { Tag: ShapeViewModel shape })
    {
      _viewModel.SelectedShape = shape;
      UpdateSelectionVisual();

      // Start dragging
      _isDragging = true;
      _isDrawing = false;
      _dragStart = e.GetPosition(_canvas);
      _dragTarget = shape;
      e.Handled = true;
    }
  }

  // --- Tool selection ---

  void OnRectangleToolClick(object? sender, RoutedEventArgs e)
  {
    if(_viewModel is null)
    {
      return;
    }

    _viewModel.SelectedTool = ShapeType.Rectangle;
    var rectTool = this.FindControl<ToggleButton>("RectangleTool");
    var ellipseTool = this.FindControl<ToggleButton>("EllipseTool");
    if(rectTool is not null)
    {
      rectTool.IsChecked = true;
    }

    if(ellipseTool is not null)
    {
      ellipseTool.IsChecked = false;
    }
  }

  void OnEllipseToolClick(object? sender, RoutedEventArgs e)
  {
    if(_viewModel is null)
    {
      return;
    }

    _viewModel.SelectedTool = ShapeType.Ellipse;
    var rectTool = this.FindControl<ToggleButton>("RectangleTool");
    var ellipseTool = this.FindControl<ToggleButton>("EllipseTool");
    if(rectTool is not null)
    {
      rectTool.IsChecked = false;
    }

    if(ellipseTool is not null)
    {
      ellipseTool.IsChecked = true;
    }
  }

  // --- Color selection ---

  void OnColorClick(object? sender, RoutedEventArgs e)
  {
    if(_viewModel is null || sender is not Button { Tag: string color })
    {
      return;
    }

    _viewModel.SelectedColor = color;

    // If a shape is selected, recolor it
    if(_viewModel.SelectedShape is not null)
    {
      _viewModel.RecolorSelectedShape();
      UpdateSelectionVisual();
    }
  }
}
