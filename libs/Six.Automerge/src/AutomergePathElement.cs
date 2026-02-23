// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge;

[PublicAPI]
public abstract class AutomergePathElement
{
  public AutomergeObjectId ObjectId { get; }

  internal AutomergePathElement(AutomergeObjectId objectId)
  {
    ObjectId = objectId;
  }
}

[PublicAPI]
public sealed class AutomergeMapPathElement : AutomergePathElement
{
  public string Key { get; }

  internal AutomergeMapPathElement(AutomergeObjectId objectId, string key)
    : base(objectId)
  {
    Key = key;
  }
}

[PublicAPI]
public sealed class AutomergeListPathElement : AutomergePathElement
{
  public ulong Index { get; }

  internal AutomergeListPathElement(AutomergeObjectId objectId, ulong index)
    : base(objectId)
  {
    Index = index;
  }
}
