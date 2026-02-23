// Copyright (c) Six. All rights reserved.

using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Six.Automerge.Repository;

namespace Six.Automerge.Drawing;

/// <summary>
/// High-level wrapper around an Automerge document for collaborative canvas drawing.
/// Provides cached canvas state, debounced remote change notification,
/// and observable streams for canvas changes.
/// </summary>
[PublicAPI]
public sealed class DrawingDocument : IDisposable
{
  readonly DocHandle<object> _handle;
  readonly AutomergeObjectId _layersListId;
  readonly ILogger _logger;

  readonly Subject<Unit> _remoteRefreshes = new();
  readonly Subject<CanvasChange> _canvasChanged = new();
  readonly IDisposable _remoteSubscription;

  int _localChangeDepth;
  bool _isDisposed;

  /// <summary>
  /// Observable stream of canvas changes (local and remote).
  /// </summary>
  public IObservable<CanvasChange> CanvasChanged => _canvasChanged;

  /// <summary>
  /// Gets the underlying document handle.
  /// </summary>
  public DocHandle<object> Handle => _handle;

  /// <summary>
  /// Gets the document ID.
  /// </summary>
  public string DocumentId => _handle.DocumentId;

  /// <summary>
  /// Gets the layers list object ID.
  /// </summary>
  internal AutomergeObjectId LayersListId => _layersListId;

  DrawingDocument(DocHandle<object> handle, AutomergeObjectId layersListId, ILoggerFactory? loggerFactory)
  {
    _handle = handle;
    _layersListId = layersListId;
    _logger = (loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance)
      .CreateLogger<DrawingDocument>();

    _remoteSubscription = _remoteRefreshes
      .Throttle(TimeSpan.FromMilliseconds(50))
      .Select(_ => Observable.FromAsync(RefreshAndNotifyRemoteAsync))
      .Concat()
      .Subscribe();

    _handle.Changed += OnDocHandleChanged;
  }

  /// <summary>
  /// Creates a new document with a default layer and returns a wired <see cref="DrawingDocument"/>.
  /// </summary>
  public static async Task<DrawingDocument> CreateAsync(
    Repository.Repo repo,
    ILoggerFactory? loggerFactory = null,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(repo);

    var handle = await repo.Create<object>();

    AutomergeObjectId layersListId = default!;
    await handle.ChangeAsync(doc =>
    {
      layersListId = doc.PutObject(doc.RootId, "layers", AutomergeContainerType.List);
      var layerId = doc.ListInsertObject(layersListId, 0, AutomergeContainerType.Map);
      doc.PutString(layerId, "name", "Layer 1");
      doc.PutBoolean(layerId, "visible", true);
      doc.PutObject(layerId, "shapes", AutomergeContainerType.List);
    }, cancellationToken: cancellationToken);

    return new DrawingDocument(handle, layersListId, loggerFactory);
  }

  /// <summary>
  /// Opens an existing drawing document.
  /// Returns null if the document is not found within the timeout period.
  /// </summary>
  public static async Task<DrawingDocument?> OpenAsync(
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

    var layersListId = await handle.ReadAsync(
      doc => doc.GetObject(doc.RootId, "layers"),
      cancellationToken);

    if(layersListId is null)
      return null;

    return new DrawingDocument(handle, layersListId, loggerFactory);
  }

  /// <summary>
  /// Adds a shape to the default (first) layer.
  /// </summary>
  public async Task<CanvasObject> AddShapeAsync(
    string name, ShapeType type, double x, double y, double width, double height, string color,
    CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();

    AutomergeObjectId shapeId = default!;
    long zOrder = 0;
    var layersListId = _layersListId;

    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle.ChangeAsync(doc =>
      {
        var defaultLayer = doc.ListGetObject(layersListId, 0)
          ?? throw new InvalidOperationException("No default layer found.");
        var shapesListId = doc.GetObject(defaultLayer, "shapes")
          ?? throw new InvalidOperationException("Default layer has no shapes list.");
        ulong index = doc.ListLength(shapesListId);
        shapeId = doc.ListInsertObject(shapesListId, index, AutomergeContainerType.Map);
        doc.PutString(shapeId, "name", name);
        doc.PutString(shapeId, "type", type.ToString());
        doc.PutDouble(shapeId, "x", x);
        doc.PutDouble(shapeId, "y", y);
        doc.PutDouble(shapeId, "width", width);
        doc.PutDouble(shapeId, "height", height);
        doc.PutString(shapeId, "color", color);
        doc.PutInt64(shapeId, "zOrder", (long)index);
        zOrder = (long)index;
      }, cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    var result = new CanvasObject
    {
      ObjectId = shapeId, Name = name, Type = type,
      X = x, Y = y, Width = width, Height = height,
      Color = color, ZOrder = zOrder,
    };

    NotifyLocalChange();
    return result;
  }

  /// <summary>
  /// Moves a shape to a new position.
  /// </summary>
  public async Task MoveShapeAsync(AutomergeObjectId shapeId, double x, double y,
    CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();
    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle.ChangeAsync(doc =>
      {
        doc.PutDouble(shapeId, "x", x);
        doc.PutDouble(shapeId, "y", y);
      }, cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    NotifyLocalChange();
  }

  /// <summary>
  /// Resizes a shape.
  /// </summary>
  public async Task ResizeShapeAsync(AutomergeObjectId shapeId, double width, double height,
    CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();
    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle.ChangeAsync(doc =>
      {
        doc.PutDouble(shapeId, "width", width);
        doc.PutDouble(shapeId, "height", height);
      }, cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    NotifyLocalChange();
  }

  /// <summary>
  /// Changes a shape's fill color.
  /// </summary>
  public async Task RecolorShapeAsync(AutomergeObjectId shapeId, string color,
    CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();
    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle.ChangeAsync(doc =>
      {
        doc.PutString(shapeId, "color", color);
      }, cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    NotifyLocalChange();
  }

  /// <summary>
  /// Removes a shape from a layer by layer index and shape index.
  /// </summary>
  public async Task DeleteShapeAsync(int layerIndex, int shapeIndex,
    CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();
    var layersListId = _layersListId;

    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle.ChangeAsync(doc =>
      {
        var layerId = doc.ListGetObject(layersListId, (ulong)layerIndex)
          ?? throw new InvalidOperationException($"No layer at index {layerIndex}.");
        var shapesListId = doc.GetObject(layerId, "shapes")
          ?? throw new InvalidOperationException("Layer has no shapes list.");
        doc.ListRemove(shapesListId, (ulong)shapeIndex);
      }, cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    NotifyLocalChange();
  }

  /// <summary>
  /// Adds a new layer at the end of the layers list.
  /// </summary>
  public async Task<Layer> AddLayerAsync(string name,
    CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();
    var layersListId = _layersListId;

    AutomergeObjectId layerId = default!;
    AutomergeObjectId shapesListId = default!;

    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle.ChangeAsync(doc =>
      {
        ulong index = doc.ListLength(layersListId);
        layerId = doc.ListInsertObject(layersListId, index, AutomergeContainerType.Map);
        doc.PutString(layerId, "name", name);
        doc.PutBoolean(layerId, "visible", true);
        shapesListId = doc.PutObject(layerId, "shapes", AutomergeContainerType.List);
      }, cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    var result = new Layer
    {
      ObjectId = layerId, ShapesListId = shapesListId,
      Name = name, IsVisible = true,
    };

    NotifyLocalChange();
    return result;
  }

  /// <summary>
  /// Reads the full canvas state from the document.
  /// </summary>
  public async Task<List<Layer>> ReadCanvasAsync(CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();
    var layersListId = _layersListId;

    return await _handle.ReadAsync(doc => ReadCanvasFromDoc(doc, layersListId), cancellationToken);
  }

  static List<Layer> ReadCanvasFromDoc(AutomergeDocument doc, AutomergeObjectId layersListId)
  {
    var layers = new List<Layer>();
    ulong layerCount = doc.ListLength(layersListId);

    for(ulong i = 0; i < layerCount; i++)
    {
      var layerId = doc.ListGetObject(layersListId, i);
      if(layerId is null) continue;

      var shapesListId = doc.GetObject(layerId, "shapes");
      if(shapesListId is null) continue;

      var layer = new Layer
      {
        ObjectId = layerId, ShapesListId = shapesListId,
        Name = doc.GetString(layerId, "name") ?? string.Empty,
        IsVisible = doc.GetBoolean(layerId, "visible") ?? true,
      };

      ulong shapeCount = doc.ListLength(shapesListId);
      for(ulong j = 0; j < shapeCount; j++)
      {
        var shapeId = doc.ListGetObject(shapesListId, j);
        if(shapeId is null) continue;

        string typeStr = doc.GetString(shapeId, "type") ?? "Rectangle";
        var shapeType = Enum.TryParse<ShapeType>(typeStr, out var parsed) ? parsed : ShapeType.Rectangle;

        layer.Shapes.Add(new CanvasObject
        {
          ObjectId = shapeId,
          Name = doc.GetString(shapeId, "name") ?? string.Empty,
          Type = shapeType,
          X = doc.GetDouble(shapeId, "x") ?? 0,
          Y = doc.GetDouble(shapeId, "y") ?? 0,
          Width = doc.GetDouble(shapeId, "width") ?? 0,
          Height = doc.GetDouble(shapeId, "height") ?? 0,
          Color = doc.GetString(shapeId, "color") ?? "#000000",
          ZOrder = doc.GetInt64(shapeId, "zOrder") ?? 0,
        });
      }

      layers.Add(layer);
    }

    return layers;
  }

  void OnDocHandleChanged(object? sender, DocHandleChangeEventArgs<object> e)
  {
    if(_isDisposed || Volatile.Read(ref _localChangeDepth) > 0)
      return;

    _remoteRefreshes.OnNext(Unit.Default);
  }

  async Task RefreshAndNotifyRemoteAsync()
  {
    var layers = await ReadCanvasAsync(CancellationToken.None);
    _canvasChanged.OnNext(new CanvasChange(layers, false));
  }

  void NotifyLocalChange()
  {
    // For local changes we don't re-read the document; the caller knows the state
    // Emit a local notification so ViewModel can react if needed
    // We fire with empty layers since the caller already has the data
    _canvasChanged.OnNext(new CanvasChange([], true));
  }

  void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);

  public void Dispose()
  {
    if(_isDisposed) return;
    _isDisposed = true;

    _handle.Changed -= OnDocHandleChanged;

    _remoteRefreshes.OnCompleted();
    _remoteSubscription.Dispose();

    _canvasChanged.OnCompleted();

    _remoteRefreshes.Dispose();
    _canvasChanged.Dispose();
  }
}
