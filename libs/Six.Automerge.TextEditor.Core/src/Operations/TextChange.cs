// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge.TextEdit;

/// <summary>
/// Payload for <see cref="TextDocument.TextChanged"/> observable notifications.
/// </summary>
[PublicAPI]
public readonly record struct TextChange(string Text, bool IsLocal);
