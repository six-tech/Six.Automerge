// Copyright (c) Six. All rights reserved.

using DemoApp.CollaborativeDrawing.ViewModels;
using FluentAssertions;
using Six.Automerge.Drawing;
using Six.Automerge.Repository;
using Xunit;

namespace DemoApp.CollaborativeDrawing.Tests;

[Trait("Category", "Unit")]
public sealed class CanvasViewModelTests : IDisposable
{
  readonly CanvasViewModel _vm;
  readonly DrawingDocument _doc;

  public CanvasViewModelTests()
  {
    var repo = new Repo(new RepoConfig());
    _doc = DrawingDocument.CreateAsync(repo).GetAwaiter().GetResult();
    _vm = new CanvasViewModel(dispatcher: action => action());
    _vm.SetDrawingDocument(_doc);
  }

  [Fact]
  public void InitialState_ShouldHaveDefaultLayer()
  {
    _vm.Layers.Should().HaveCount(1);
    _vm.Layers[0].Name.Should().Be("Layer 1");
  }

  [Fact]
  public void InitialState_ShouldHaveEmptyShapes()
  {
    _vm.Shapes.Should().BeEmpty();
  }

  [Fact]
  public void InitialState_SelectedToolShouldBeRectangle()
  {
    _vm.SelectedTool.Should().Be(ShapeType.Rectangle);
  }

  [Fact]
  public async Task AddShape_ShouldAddToShapesAndDefaultLayer()
  {
    _vm.AddShape(10, 20, 100, 50);
    await Task.Delay(100);

    _vm.Shapes.Should().HaveCount(1);
    _vm.Shapes[0].Name.Should().Contain("Rectangle");
    _vm.Shapes[0].X.Should().Be(10);
    _vm.Shapes[0].Y.Should().Be(20);
    _vm.Shapes[0].Width.Should().Be(100);
    _vm.Shapes[0].Height.Should().Be(50);
  }

  [Fact]
  public async Task AddShape_WithEllipseTool_ShouldCreateEllipse()
  {
    _vm.SelectedTool = ShapeType.Ellipse;
    _vm.AddShape(50, 60, 80, 80);
    await Task.Delay(100);

    _vm.Shapes.Should().HaveCount(1);
    _vm.Shapes[0].Name.Should().Contain("Ellipse");
    _vm.Shapes[0].Type.Should().Be(ShapeType.Ellipse);
  }

  [Fact]
  public async Task SelectShape_ShouldSetSelectedShape()
  {
    _vm.AddShape(10, 20, 100, 50);
    await Task.Delay(100);

    var shape = _vm.Shapes[0];
    _vm.SelectedShape = shape;

    _vm.SelectedShape.Should().BeSameAs(shape);
  }

  [Fact]
  public async Task MoveSelectedShape_ShouldUpdatePosition()
  {
    _vm.AddShape(10, 20, 100, 50);
    await Task.Delay(100);

    _vm.SelectedShape = _vm.Shapes[0];
    _vm.MoveSelectedShape(200, 300);

    _vm.SelectedShape!.X.Should().Be(200);
    _vm.SelectedShape.Y.Should().Be(300);
  }

  [Fact]
  public async Task ResizeSelectedShape_ShouldUpdateDimensions()
  {
    _vm.AddShape(10, 20, 100, 50);
    await Task.Delay(100);

    _vm.SelectedShape = _vm.Shapes[0];
    _vm.ResizeSelectedShape(200, 150);

    _vm.SelectedShape!.Width.Should().Be(200);
    _vm.SelectedShape.Height.Should().Be(150);
  }

  [Fact]
  public async Task DeleteSelectedShape_ShouldRemoveFromShapes()
  {
    _vm.AddShape(10, 20, 100, 50);
    await Task.Delay(100);

    _vm.SelectedShape = _vm.Shapes[0];
    _vm.DeleteSelectedShape();

    _vm.Shapes.Should().BeEmpty();
    _vm.SelectedShape.Should().BeNull();
  }

  [Fact]
  public async Task DeleteSelectedShape_WhenNothingSelected_ShouldBeNoOp()
  {
    _vm.AddShape(10, 20, 100, 50);
    await Task.Delay(100);

    _vm.DeleteSelectedShape();

    _vm.Shapes.Should().HaveCount(1);
  }

  [Fact]
  public async Task RecolorSelectedShape_ShouldUpdateColor()
  {
    _vm.AddShape(10, 20, 100, 50);
    await Task.Delay(100);

    _vm.SelectedShape = _vm.Shapes[0];
    _vm.SelectedColor = "#00FF00";
    _vm.RecolorSelectedShape();

    _vm.SelectedShape!.Color.Should().Be("#00FF00");
  }

  [Fact]
  public async Task AddLayer_ShouldAddToLayersCollection()
  {
    _vm.AddLayerCommand.Execute().Subscribe();
    await Task.Delay(100);

    _vm.Layers.Should().HaveCount(2);
    _vm.Layers[1].Name.Should().Be("Layer 2");
  }

  [Fact]
  public async Task StatusText_ShouldUpdateAfterActions()
  {
    _vm.AddShape(10, 20, 100, 50);
    await Task.Delay(100);

    _vm.StatusText.Should().Contain("Added");
  }

  [Fact]
  public void Dispose_ShouldNotThrow()
  {
    var act = () => _vm.Dispose();
    act.Should().NotThrow();
  }

  [Fact]
  public void Constructor_WithNullRepoService_ShouldWorkInOfflineMode()
  {
    using var vm = new CanvasViewModel(dispatcher: action => action());
    vm.RepoService.Should().BeNull();
    vm.StatusText.Should().Be("Ready");
  }

  [Fact]
  public async Task SetDrawingDocument_ShouldPopulateCanvasFromDocument()
  {
    using var vm = new CanvasViewModel(dispatcher: action => action());

    // Add a shape to the document before wiring it to the ViewModel
    var repo = new Repo(new RepoConfig());
    var doc = await DrawingDocument.CreateAsync(repo);
    await doc.AddShapeAsync("TestRect", ShapeType.Rectangle, 10, 20, 100, 50, "#FF0000");

    vm.SetDrawingDocument(doc);
    await Task.Delay(100);

    vm.Shapes.Should().HaveCount(1);
    vm.Shapes[0].Name.Should().Be("TestRect");
    vm.Layers.Should().HaveCount(1);
  }

  [Fact]
  public async Task RefreshFromDocument_AfterExternalChanges_ShouldUpdateShapes()
  {
    // Add a shape through the DrawingDocument directly (simulating remote change)
    await _doc.AddShapeAsync("RemoteRect", ShapeType.Rectangle, 100, 200, 150, 75, "#FF0000");

    int countBefore = _vm.Shapes.Count;
    _vm.RefreshFromDocument();
    await Task.Delay(100);

    _vm.Shapes.Should().HaveCount(countBefore + 1);
    _vm.Shapes.Should().Contain(s => s.Name == "RemoteRect");
  }

  [Fact]
  public async Task RefreshFromDocument_ShouldReplaceExistingShapes()
  {
    _vm.AddShape(10, 20, 100, 50);
    await Task.Delay(100);
    _vm.Shapes.Should().HaveCount(1);

    // Add more shapes directly on the document (simulating remote changes)
    await _doc.AddShapeAsync("Remote1", ShapeType.Ellipse, 200, 200, 60, 60, "#00FF00");
    await _doc.AddShapeAsync("Remote2", ShapeType.Rectangle, 300, 300, 80, 40, "#0000FF");

    _vm.RefreshFromDocument();
    await Task.Delay(100);

    // Should have all 3 shapes (1 original + 2 remote)
    _vm.Shapes.Should().HaveCount(3);
    _vm.Shapes.Should().Contain(s => s.Name == "Remote1");
    _vm.Shapes.Should().Contain(s => s.Name == "Remote2");
  }

  [Fact]
  public async Task RefreshFromDocument_ShouldResetSelectedShape()
  {
    _vm.AddShape(10, 20, 100, 50);
    await Task.Delay(100);

    _vm.SelectedShape = _vm.Shapes[0];
    _vm.SelectedShape.Should().NotBeNull();

    _vm.RefreshFromDocument();
    await Task.Delay(100);

    _vm.SelectedShape.Should().BeNull();
  }

  [Fact]
  public async Task RefreshFromDocument_WithMultipleLayers_ShouldFlattenAllShapes()
  {
    // Add a second layer with shapes directly through the DrawingDocument
    var layer2 = await _doc.AddLayerAsync("Layer 2");
    await _doc.AddShapeAsync("L1Shape", ShapeType.Rectangle, 10, 10, 100, 50, "#FF0000");

    _vm.RefreshFromDocument();
    await Task.Delay(100);

    _vm.Layers.Should().HaveCount(2);
    _vm.Shapes.Should().HaveCount(1);
    _vm.Shapes.Should().Contain(s => s.Name == "L1Shape");
  }

  [Fact]
  public async Task RefreshFromDocument_AfterShapeRemoval_ShouldReflectDeletion()
  {
    _vm.AddShape(10, 20, 100, 50);
    await Task.Delay(100);
    _vm.AddShape(50, 60, 80, 40);
    await Task.Delay(100);
    _vm.Shapes.Should().HaveCount(2);

    // Simulate remote deletion: remove first shape directly via the document
    await _doc.DeleteShapeAsync(0, 0);

    _vm.RefreshFromDocument();
    await Task.Delay(100);

    _vm.Shapes.Should().HaveCount(1);
  }

  [Fact]
  public async Task RefreshFromDocument_AfterMoveShape_ShouldReflectNewPosition()
  {
    _vm.AddShape(10, 20, 100, 50);
    await Task.Delay(100);

    // Read the canvas to get the shape's ObjectId
    var layers = await _doc.ReadCanvasAsync();
    var shapeId = layers[0].Shapes[0].ObjectId;
    await _doc.MoveShapeAsync(shapeId, 500, 600);

    _vm.RefreshFromDocument();
    await Task.Delay(100);

    _vm.Shapes.Should().HaveCount(1);
    _vm.Shapes[0].X.Should().Be(500);
    _vm.Shapes[0].Y.Should().Be(600);
  }

  [Fact]
  public async Task RefreshFromDocument_AfterRecolorShape_ShouldReflectNewColor()
  {
    _vm.AddShape(10, 20, 100, 50);
    await Task.Delay(100);

    // Read the canvas to get the shape's ObjectId
    var layers = await _doc.ReadCanvasAsync();
    var shapeId = layers[0].Shapes[0].ObjectId;
    await _doc.RecolorShapeAsync(shapeId, "#AABBCC");

    _vm.RefreshFromDocument();
    await Task.Delay(100);

    _vm.Shapes.Should().HaveCount(1);
    _vm.Shapes[0].Color.Should().Be("#AABBCC");
  }

  [Fact]
  public async Task DrawingDocument_ShouldBeAccessibleAfterSet()
  {
    using var vm = new CanvasViewModel(dispatcher: action => action());
    vm.DrawingDocument.Should().BeNull();

    var repo = new Repo(new RepoConfig());
    var doc = await DrawingDocument.CreateAsync(repo);
    vm.SetDrawingDocument(doc);

    vm.DrawingDocument.Should().BeSameAs(doc);
  }

  public void Dispose()
  {
    _vm.Dispose();
  }
}
