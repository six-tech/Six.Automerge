using Native = Six.Automerge.FFI.Native;

namespace Six.Automerge;

public enum AutomergeContainerType
{
  Map,
  List,
  Text
}



internal static class AutomergeContainerTypeExtensions
{
  public static Native.AutomergeObjectType ToNative(this AutomergeContainerType type) => type switch
  {
    AutomergeContainerType.Map => Native.AutomergeObjectType.Map,
    AutomergeContainerType.List => Native.AutomergeObjectType.List,
    AutomergeContainerType.Text => Native.AutomergeObjectType.Text,
    _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported container type.")
  };

  public static AutomergeContainerType FromNative(this Native.AutomergeObjectType type)
  {
    if(type.IsMap)
    {
      return AutomergeContainerType.Map;
    }

    if(type.IsList)
    {
      return AutomergeContainerType.List;
    }

    if(type.IsText)
    {
      return AutomergeContainerType.Text;
    }

    throw new ArgumentOutOfRangeException(nameof(type), type.ToString(), "Unknown Automerge object type.");
  }
}