namespace Six.Automerge.Repository.Helpers;

/// <summary>
/// Helper methods for working with binary data.
/// </summary>
internal static class BinaryHelper
{
  /// <summary>
  /// Merges multiple byte arrays into a single array.
  /// </summary>
  /// <param name="arrays">The arrays to merge.</param>
  /// <returns>A single merged array.</returns>
  public static byte[] Merge(params byte[][] arrays)
  {
    if(arrays.Length == 0)
      return [];

    var totalLength = arrays.Sum(a => a?.Length ?? 0);
    if(totalLength == 0)
      return [];

    var result = new byte[totalLength];
    var offset = 0;

    foreach(var array in arrays)
    {
      if(array is null || array.Length == 0) continue;

      Array.Copy(array, 0, result, offset, array.Length);
      offset += array.Length;
    }

    return result;
  }

  /// <summary>
  /// Compares two byte arrays for equality.
  /// </summary>
  /// <param name="array1">First array.</param>
  /// <param name="array2">Second array.</param>
  /// <returns>True if arrays are equal, false otherwise.</returns>
  public static bool AreEqual(byte[]? array1, byte[]? array2)
  {
    if(array1 is null && array2 is null) return true;
    if(array1 is null || array2 is null) return false;
    if(array1.Length != array2.Length) return false;

    return array1.SequenceEqual(array2);
  }

  /// <summary>
  /// Creates a hex string representation of binary data.
  /// </summary>
  /// <param name="data">The binary data.</param>
  /// <returns>A lowercase hex string.</returns>
  public static string ToHexString(byte[] data)
  {
    if(data.Length == 0)
      return string.Empty;

    return BitConverter.ToString(data).Replace("-", "").ToLowerInvariant();
  }

  /// <summary>
  /// Parses a hex string into binary data.
  /// </summary>
  /// <param name="hex">The hex string (case-insensitive).</param>
  /// <returns>The decoded binary data.</returns>
  /// <exception cref="FormatException">Thrown if hex string is invalid.</exception>
  public static byte[] FromHexString(string hex)
  {
    if(string.IsNullOrWhiteSpace(hex))
      return Array.Empty<byte>();

    // Remove whitespace and convert to uppercase
    hex = hex.Replace(" ", "").Replace("-", "").ToUpperInvariant();

    if(hex.Length % 2 != 0)
      throw new FormatException("Hex string must have an even length");

    var bytes = new byte[hex.Length / 2];
    for(int i = 0; i < bytes.Length; i++)
    {
      try
      {
        bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
      }
      catch(Exception ex)
      {
        throw new FormatException($"Invalid hex string at position {i * 2}", ex);
      }
    }

    return bytes;
  }
}
