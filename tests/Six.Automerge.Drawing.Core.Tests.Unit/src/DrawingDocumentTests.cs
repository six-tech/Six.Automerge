// Copyright (c) Six. All rights reserved.

using System.Reactive.Linq;

namespace Six.Automerge.Drawing.Tests;

[Trait("Category", "Unit")]
public sealed class DrawingDocumentTests
{
  [Fact]
  public async Task CreateAsync_ShouldCreateDocumentWithDefaultLayer()
  {
    using var repo = new AutomergeRepo(new AutomergeRepoConfig());
    using var doc = await DrawingDocument.CreateAsync(repo);

    List<Layer> layers = await doc.ReadCanvasAsync();

    layers.Should().HaveCount(1);
    layers[0].Name.Should().Be("Layer 1");
    layers[0].IsVisible.Should().BeTrue();
    layers[0].Shapes.Should().BeEmpty();
  }

  [Fact]
  public async Task CreateAsync_ShouldHaveDocumentId()
  {
    using var repo = new AutomergeRepo(new AutomergeRepoConfig());
    using var doc = await DrawingDocument.CreateAsync(repo);

    doc.DocumentId.Should().NotBeNullOrWhiteSpace();
  }

  [Fact]
  public async Task AddShapeAsync_ShouldCreateRectangle()
  {
    using var repo = new AutomergeRepo(new AutomergeRepoConfig());
    using var doc = await DrawingDocument.CreateAsync(repo);

    CanvasObject shape = await doc.AddShapeAsync(
      "Rect1", ShapeType.Rectangle, 10, 20, 100, 50, "#FF0000");

    shape.Name.Should().Be("Rect1");
    shape.Type.Should().Be(ShapeType.Rectangle);
    shape.X.Should().Be(10);
    shape.Y.Should().Be(20);
    shape.Width.Should().Be(100);
    shape.Height.Should().Be(50);
    shape.Color.Should().Be("#FF0000");
    shape.ZOrder.Should().Be(0);
  }

  [Fact]
  public async Task AddShapeAsync_Ellipse_ShouldCreateEllipse()
  {
    using var repo = new AutomergeRepo(new AutomergeRepoConfig());
    using var doc = await DrawingDocument.CreateAsync(repo);

    CanvasObject shape = await doc.AddShapeAsync(
      "Circle1", ShapeType.Ellipse, 50, 60, 80, 80, "#00FF00");

    shape.Name.Should().Be("Circle1");
    shape.Type.Should().Be(ShapeType.Ellipse);
    shape.X.Should().Be(50);
    shape.Y.Should().Be(60);
    shape.Width.Should().Be(80);
    shape.Height.Should().Be(80);
    shape.Color.Should().Be("#00FF00");
  }

  [Fact]
  public async Task AddShapeAsync_Multiple_ShouldIncrementZOrder()
  {
    using var repo = new AutomergeRepo(new AutomergeRepoConfig());
    using var doc = await DrawingDocument.CreateAsync(repo);

    CanvasObject first = await doc.AddShapeAsync(
      "Shape1", ShapeType.Rectangle, 0, 0, 10, 10, "#000000");
    CanvasObject second = await doc.AddShapeAsync(
      "Shape2", ShapeType.Rectangle, 0, 0, 10, 10, "#000000");
    CanvasObject third = await doc.AddShapeAsync(
      "Shape3", ShapeType.Ellipse, 0, 0, 10, 10, "#000000");

    first.ZOrder.Should().Be(0);
    second.ZOrder.Should().Be(1);
    third.ZOrder.Should().Be(2);
  }

  [Fact]
  public async Task MoveShapeAsync_ShouldUpdatePosition()
  {
    using var repo = new AutomergeRepo(new AutomergeRepoConfig());
    using var doc = await DrawingDocument.CreateAsync(repo);

    CanvasObject shape = await doc.AddShapeAsync(
      "Rect1", ShapeType.Rectangle, 10, 20, 100, 50, "#FF0000");

    await doc.MoveShapeAsync(shape.ObjectId, 200, 300);

    List<Layer> layers = await doc.ReadCanvasAsync();
    CanvasObject moved = layers[0].Shapes[0];
    moved.X.Should().Be(200);
    moved.Y.Should().Be(300);
  }

  [Fact]
  public async Task ResizeShapeAsync_ShouldUpdateDimensions()
  {
    using var repo = new AutomergeRepo(new AutomergeRepoConfig());
    using var doc = await DrawingDocument.CreateAsync(repo);

    CanvasObject shape = await doc.AddShapeAsync(
      "Rect1", ShapeType.Rectangle, 0, 0, 100, 50, "#FF0000");

    await doc.ResizeShapeAsync(shape.ObjectId, 250, 175);

    List<Layer> layers = await doc.ReadCanvasAsync();
    CanvasObject resized = layers[0].Shapes[0];
    resized.Width.Should().Be(250);
    resized.Height.Should().Be(175);
  }

  [Fact]
  public async Task RecolorShapeAsync_ShouldUpdateColor()
  {
    using var repo = new AutomergeRepo(new AutomergeRepoConfig());
    using var doc = await DrawingDocument.CreateAsync(repo);

    CanvasObject shape = await doc.AddShapeAsync(
      "Rect1", ShapeType.Rectangle, 0, 0, 100, 50, "#FF0000");

    await doc.RecolorShapeAsync(shape.ObjectId, "#0000FF");

    List<Layer> layers = await doc.ReadCanvasAsync();
    CanvasObject recolored = layers[0].Shapes[0];
    recolored.Color.Should().Be("#0000FF");
  }

  [Fact]
  public async Task DeleteShapeAsync_ShouldRemoveFromLayer()
  {
    using var repo = new AutomergeRepo(new AutomergeRepoConfig());
    using var doc = await DrawingDocument.CreateAsync(repo);

    await doc.AddShapeAsync("Shape1", ShapeType.Rectangle, 0, 0, 10, 10, "#FF0000");
    await doc.AddShapeAsync("Shape2", ShapeType.Ellipse, 50, 50, 20, 20, "#00FF00");

    await doc.DeleteShapeAsync(0, 0);

    List<Layer> layers = await doc.ReadCanvasAsync();
    layers[0].Shapes.Should().HaveCount(1);
    layers[0].Shapes[0].Name.Should().Be("Shape2");
  }

  [Fact]
  public async Task AddLayerAsync_ShouldCreateNamedLayer()
  {
    using var repo = new AutomergeRepo(new AutomergeRepoConfig());
    using var doc = await DrawingDocument.CreateAsync(repo);

    Layer layer = await doc.AddLayerAsync("Background");

    layer.Name.Should().Be("Background");
    layer.IsVisible.Should().BeTrue();
    layer.Shapes.Should().BeEmpty();
  }

  [Fact]
  public async Task ReadCanvasAsync_ShouldReturnAllLayers()
  {
    using var repo = new AutomergeRepo(new AutomergeRepoConfig());
    using var doc = await DrawingDocument.CreateAsync(repo);

    await doc.AddLayerAsync("Layer 2");
    await doc.AddLayerAsync("Layer 3");

    List<Layer> layers = await doc.ReadCanvasAsync();

    layers.Should().HaveCount(3);
    layers[0].Name.Should().Be("Layer 1");
    layers[1].Name.Should().Be("Layer 2");
    layers[2].Name.Should().Be("Layer 3");
  }

  [Fact]
  public async Task ReadCanvasAsync_ShouldReturnShapesInDefaultLayer()
  {
    using var repo = new AutomergeRepo(new AutomergeRepoConfig());
    using var doc = await DrawingDocument.CreateAsync(repo);

    await doc.AddShapeAsync("Rect1", ShapeType.Rectangle, 10, 20, 100, 50, "#FF0000");
    await doc.AddShapeAsync("Circle1", ShapeType.Ellipse, 50, 60, 80, 80, "#00FF00");

    List<Layer> layers = await doc.ReadCanvasAsync();

    layers[0].Shapes.Should().HaveCount(2);
    layers[0].Shapes[0].Name.Should().Be("Rect1");
    layers[0].Shapes[0].Type.Should().Be(ShapeType.Rectangle);
    layers[0].Shapes[1].Name.Should().Be("Circle1");
    layers[0].Shapes[1].Type.Should().Be(ShapeType.Ellipse);
  }

  [Fact]
  public async Task CanvasChanged_ShouldEmitForLocalChanges()
  {
    using var repo = new AutomergeRepo(new AutomergeRepoConfig());
    using var doc = await DrawingDocument.CreateAsync(repo);

    CanvasChange? received = null;
    var tcs = new TaskCompletionSource<bool>();
    doc.CanvasChanged.Subscribe(change =>
    {
      received = change;
      tcs.TrySetResult(true);
    });

    await doc.AddShapeAsync("Rect1", ShapeType.Rectangle, 0, 0, 50, 50, "#FF0000");

    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

    received.Should().NotBeNull();
    received!.Value.IsLocal.Should().BeTrue();
  }

  [Fact]
  public async Task Dispose_ShouldNotThrow()
  {
    using var repo = new AutomergeRepo(new AutomergeRepoConfig());
    DrawingDocument doc = await DrawingDocument.CreateAsync(repo);

    var act = () => doc.Dispose();

    act.Should().NotThrow();
  }
}
