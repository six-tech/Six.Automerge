// Copyright (c) Six. All rights reserved.

namespace Six.Automerge.Drawing;

/// <summary>
/// Event data emitted when the canvas changes.
/// </summary>
[PublicAPI]
public readonly record struct CanvasChange(IReadOnlyList<Layer> Layers, bool IsLocal);
