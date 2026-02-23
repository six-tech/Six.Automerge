// Copyright (c) Six. All rights reserved.

namespace Six.Automerge.Drawing;

/// <summary>
/// Represents a layer on the canvas.
/// </summary>
[PublicAPI]
public sealed class Layer
{
  /// <summary>
  /// The Automerge object ID for this layer's map container.
  /// </summary>
  public required AutomergeObjectId ObjectId { get; init; }

  /// <summary>
  /// The Automerge object ID for this layer's shapes list container.
  /// </summary>
  public required AutomergeObjectId ShapesListId { get; init; }

  /// <summary>
  /// The display name of this layer.
  /// </summary>
  public required string Name { get; set; }

  /// <summary>
  /// Whether this layer is visible on the canvas.
  /// </summary>
  public bool IsVisible { get; set; } = true;

  /// <summary>
  /// The shapes within this layer.
  /// </summary>
  public List<CanvasObject> Shapes { get; } = [];
}
