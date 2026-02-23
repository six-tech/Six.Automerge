// Copyright (c) Six. All rights reserved.

namespace DemoApp.Shared.Controls;

/// <summary>
/// The action chosen by the user in the document session dialog.
/// </summary>
[PublicAPI]
public enum SessionAction
{
  /// <summary>Create a new document.</summary>
  Create,

  /// <summary>Join an existing document by ID.</summary>
  Join,

  /// <summary>Resume a previously stored document.</summary>
  Resume
}
