namespace Six.Automerge;

[PublicAPI]
public enum AutomergeScalarValueKind
{
  Bytes,
  String,
  Int,
  Uint,
  Double,
  Counter,
  Timestamp,
  Boolean,
  Null,
  Unknown
}

[PublicAPI]
public sealed class AutomergeScalarValue
{
  public AutomergeScalarValueKind Kind { get; }
  public string? StringValue { get; }
  public bool? BoolValue { get; }
  public long? IntValue { get; }
  public ulong? UIntValue { get; }
  public double? DoubleValue { get; }
  public byte[]? BytesValue { get; }
  public long? CounterValue { get; }
  public long? TimestampValue { get; }
  public byte? UnknownTypeCode { get; }
  public byte[]? UnknownBytes { get; }

  internal AutomergeScalarValue(
    AutomergeScalarValueKind kind,
    string? stringValue = null,
    bool? boolValue = null,
    long? intValue = null,
    ulong? uintValue = null,
    double? doubleValue = null,
    byte[]? bytesValue = null,
    long? counterValue = null,
    long? timestampValue = null,
    byte? unknownTypeCode = null,
    byte[]? unknownBytes = null)
  {
    Kind = kind;
    StringValue = stringValue;
    BoolValue = boolValue;
    IntValue = intValue;
    UIntValue = uintValue;
    DoubleValue = doubleValue;
    BytesValue = bytesValue;
    CounterValue = counterValue;
    TimestampValue = timestampValue;
    UnknownTypeCode = unknownTypeCode;
    UnknownBytes = unknownBytes;
  }
}

[PublicAPI]
public enum AutomergeValueType
{
  Scalar,
  Object
}

[PublicAPI]
public sealed class AutomergeValue
{
  public AutomergeValueType Type { get; }
  public AutomergeScalarValue? Scalar { get; }
  public AutomergeContainerType? ObjectType { get; }

  internal AutomergeValue(AutomergeValueType type, AutomergeScalarValue? scalar, AutomergeContainerType? objectType)
  {
    Type = type;
    Scalar = scalar;
    ObjectType = objectType;
  }
}

[PublicAPI]
public enum AutomergePropKind
{
  Map,
  Seq
}

[PublicAPI]
public sealed class AutomergeProp
{
  public AutomergePropKind Kind { get; }
  public string? Key { get; }
  public ulong Index { get; }

  internal AutomergeProp(AutomergePropKind kind, string? key, ulong index)
  {
    Kind = kind;
    Key = key;
    Index = index;
  }
}

[PublicAPI]
public sealed class AutomergeInsertEntry
{
  public AutomergeValue Value { get; }
  public AutomergeObjectId? ObjectId { get; }
  public bool Conflict { get; }

  internal AutomergeInsertEntry(AutomergeValue value, AutomergeObjectId? objectId, bool conflict)
  {
    Value = value;
    ObjectId = objectId;
    Conflict = conflict;
  }
}

[PublicAPI]
public sealed class AutomergeMarkData
{
  public string Name { get; }
  public AutomergeScalarValue Value { get; }

  internal AutomergeMarkData(string name, AutomergeScalarValue value)
  {
    Name = name;
    Value = value;
  }
}

[PublicAPI]
public sealed class AutomergeMarkSpan
{
  public ulong Start { get; }
  public ulong End { get; }
  public string Name { get; }
  public AutomergeScalarValue Value { get; }

  internal AutomergeMarkSpan(ulong start, ulong end, string name, AutomergeScalarValue value)
  {
    Start = start;
    End = end;
    Name = name;
    Value = value;
  }
}

[PublicAPI]
public enum AutomergePatchActionKind
{
  PutMap,
  PutSeq,
  Insert,
  SpliceText,
  Increment,
  Conflict,
  DeleteMap,
  DeleteSeq,
  Mark
}

[PublicAPI]
public sealed class AutomergePatchAction
{
  public AutomergePatchActionKind Kind { get; }
  public string? Key { get; }
  public ulong Index { get; }
  public AutomergeValue? Value { get; }
  public AutomergeObjectId? ValueObjectId { get; }
  public bool Conflict { get; }
  public IReadOnlyList<AutomergeInsertEntry> Inserted { get; }
  public string? Text { get; }
  public IReadOnlyList<AutomergeMarkData> ActiveMarks { get; }
  public IReadOnlyList<AutomergeMarkSpan> Marks { get; }
  public AutomergeProp? Prop { get; }
  public ulong Length { get; }
  public long IncrementValue { get; }

  internal AutomergePatchAction(
    AutomergePatchActionKind kind,
    string? key,
    ulong index,
    AutomergeValue? value,
    AutomergeObjectId? valueObjectId,
    bool conflict,
    IReadOnlyList<AutomergeInsertEntry> inserted,
    string? text,
    IReadOnlyList<AutomergeMarkData> activeMarks,
    IReadOnlyList<AutomergeMarkSpan> marks,
    AutomergeProp? prop,
    ulong length,
    long incrementValue)
  {
    Kind = kind;
    Key = key;
    Index = index;
    Value = value;
    ValueObjectId = valueObjectId;
    Conflict = conflict;
    Inserted = inserted;
    Text = text;
    ActiveMarks = activeMarks;
    Marks = marks;
    Prop = prop;
    Length = length;
    IncrementValue = incrementValue;
  }
}

[PublicAPI]
public sealed class AutomergePatchPathElement
{
  public AutomergeObjectId ObjectId { get; }
  public AutomergeProp Prop { get; }

  internal AutomergePatchPathElement(AutomergeObjectId objectId, AutomergeProp prop)
  {
    ObjectId = objectId;
    Prop = prop;
  }
}

[PublicAPI]
public sealed class AutomergePatch
{
  public AutomergeObjectId ObjectId { get; }
  public IReadOnlyList<AutomergePatchPathElement> Path { get; }
  public AutomergePatchAction Action { get; }

  internal AutomergePatch(
    AutomergeObjectId objectId,
    IReadOnlyList<AutomergePatchPathElement> path,
    AutomergePatchAction action)
  {
    ObjectId = objectId;
    Path = path;
    Action = action;
  }
}
