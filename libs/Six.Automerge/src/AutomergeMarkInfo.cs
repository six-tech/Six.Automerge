// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge;

/// <summary>
/// Represents a mark applied to a range of text with its name, value, and position.
/// </summary>
[PublicAPI]
public sealed class AutomergeMarkInfo
{
  public string Name { get; }
  public AutomergeScalarValue Value { get; }
  public ulong Start { get; }
  public ulong End { get; }

  internal AutomergeMarkInfo(string name, AutomergeScalarValue value, ulong start, ulong end)
  {
    Name = name;
    Value = value;
    Start = start;
    End = end;
  }
}
