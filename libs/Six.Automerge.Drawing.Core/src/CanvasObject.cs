// Copyright (c) Six. All rights reserved.

namespace Six.Automerge.Drawing;

/// <summary>
/// Represents a shape on the canvas, backed by an Automerge map object.
/// </summary>
[PublicAPI]
public sealed class CanvasObject
{
  /// <summary>
  /// The Automerge object ID for this shape's map container.
  /// </summary>
  public required AutomergeObjectId ObjectId { get; init; }

  /// <summary>
  /// The display name of this shape.
  /// </summary>
  public required string Name { get; set; }

  /// <summary>
  /// The shape type (Rectangle, Ellipse).
  /// </summary>
  public required ShapeType Type { get; set; }

  /// <summary>
  /// Horizontal position on the canvas.
  /// </summary>
  public double X { get; set; }

  /// <summary>
  /// Vertical position on the canvas.
  /// </summary>
  public double Y { get; set; }

  /// <summary>
  /// Width of the shape.
  /// </summary>
  public double Width { get; set; }

  /// <summary>
  /// Height of the shape.
  /// </summary>
  public double Height { get; set; }

  /// <summary>
  /// Fill color as a hex string (e.g., "#FF5733").
  /// </summary>
  public required string Color { get; set; }

  /// <summary>
  /// Z-order for layering. Higher values render on top.
  /// </summary>
  public long ZOrder { get; set; }
}
