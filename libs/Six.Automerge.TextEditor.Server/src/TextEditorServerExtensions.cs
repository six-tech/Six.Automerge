// Copyright (c) Six. All rights reserved.

namespace Six.Automerge.TextEditor.Server;

/// <summary>
/// Constants and helpers for the text editor sync server.
/// </summary>
[PublicAPI]
public static class TextEditorServerExtensions
{
  /// <summary>
  /// The SignalR hub path for the text editor.
  /// </summary>
  public const string HubPath = "/hubs/editor";
}
