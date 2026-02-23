// Copyright (c) Six. All rights reserved.

namespace DemoApp.Shared.Controls;

/// <summary>
/// The result of the document session dialog.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed record SessionResult
{
  /// <summary>
  /// Gets the action chosen by the user.
  /// </summary>
  public required SessionAction Action { get; init; }

  /// <summary>
  /// Gets the document ID for Join/Resume actions. Null for Create.
  /// </summary>
  public string? DocumentId { get; init; }
}
