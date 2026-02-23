// Copyright (c) Six. Licensed under the MIT License.

using Six.Automerge;

namespace Six.Automerge.TextEdit.Tests.Unit;

/// <summary>
/// Creates <see cref="AutomergeMarkInfo"/> instances for tests by using a real
/// AutomergeDocument, since the constructor is internal.
/// </summary>
internal static class AutomergeMarkInfoFactory
{
  public static AutomergeMarkInfo CreateBool(string name, bool value, ulong start, ulong end)
  {
    using var doc = AutomergeDocument.Create();
    var textId = doc.PutObject(doc.RootId, "t", AutomergeContainerType.Text);
    int len = (int)Math.Max(end, 1);
    doc.SpliceText(textId, 0, 0, new string('x', len));
    doc.Mark(textId, start, end, name, value, ExpandMode.Both);
    var marks = doc.GetMarks(textId);
    return marks.First(m => m.Name == name);
  }

  public static AutomergeMarkInfo CreateString(string name, string value, ulong start, ulong end)
  {
    using var doc = AutomergeDocument.Create();
    var textId = doc.PutObject(doc.RootId, "t", AutomergeContainerType.Text);
    int len = (int)Math.Max(end, 1);
    doc.SpliceText(textId, 0, 0, new string('x', len));
    doc.Mark(textId, start, end, name, value, ExpandMode.Both);
    var marks = doc.GetMarks(textId);
    return marks.First(m => m.Name == name);
  }

  public static AutomergeMarkInfo CreateInt(string name, long value, ulong start, ulong end)
  {
    using var doc = AutomergeDocument.Create();
    var textId = doc.PutObject(doc.RootId, "t", AutomergeContainerType.Text);
    int len = (int)Math.Max(end, 1);
    doc.SpliceText(textId, 0, 0, new string('x', len));
    doc.Mark(textId, start, end, name, value, ExpandMode.Both);
    var marks = doc.GetMarks(textId);
    return marks.First(m => m.Name == name);
  }
}
