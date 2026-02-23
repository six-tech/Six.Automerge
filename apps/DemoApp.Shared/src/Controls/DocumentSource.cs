// Copyright (c) Six. All rights reserved.

namespace DemoApp.Shared.Controls;

/// <summary>
/// Indicates where a document entry originates from.
/// </summary>
[PublicAPI]
public enum DocumentSource
{
  /// <summary>Document exists only in local storage.</summary>
  Local,

  /// <summary>Document exists only on the server.</summary>
  Server,

  /// <summary>Document exists in both local storage and on the server.</summary>
  Both
}
