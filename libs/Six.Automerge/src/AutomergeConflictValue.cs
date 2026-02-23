// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge;

[PublicAPI]
public sealed class AutomergeConflictValue
{
  public bool IsObject { get; }
  public AutomergeScalarValue? Scalar { get; }
  public AutomergeObjectId? ObjectId { get; }
  public AutomergeContainerType? ObjectType { get; }
  public string OperationId { get; }

  internal AutomergeConflictValue(
    bool isObject,
    AutomergeScalarValue? scalar,
    AutomergeObjectId? objectId,
    AutomergeContainerType? objectType,
    string operationId)
  {
    IsObject = isObject;
    Scalar = scalar;
    ObjectId = objectId;
    ObjectType = objectType;
    OperationId = operationId;
  }
}
