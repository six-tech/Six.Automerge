using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Six.Automerge.Repository.Helpers;

/// <summary>
/// Base58Check encoding/decoding for document identifiers.
/// Compatible with Bitcoin's Base58Check format matching the TypeScript implementation.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
internal static class Base58Check
{
  const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
  static readonly int[] _decodeMap = CreateDecodeMap();

  /// <summary>
  /// Encodes binary data to Base58Check format.
  /// </summary>
  public static string Encode(byte[] data)
  {
    if(data is null)
      throw new ArgumentNullException(nameof(data));

    if(data.Length == 0)
      throw new ArgumentException("Data cannot be empty.", nameof(data));

    var checksum = ComputeChecksum(data);
    var dataWithChecksum = new byte[data.Length + 4];
    Array.Copy(data, 0, dataWithChecksum, 0, data.Length);
    Array.Copy(checksum, 0, dataWithChecksum, data.Length, 4);

    return EncodeBase58(dataWithChecksum);
  }

  /// <summary>
  /// Decodes Base58Check string to binary data.
  /// </summary>
  public static byte[] Decode(string encoded)
  {
    if(string.IsNullOrWhiteSpace(encoded))
      throw new ArgumentException("Encoded string cannot be null or whitespace.", nameof(encoded));

    var dataWithChecksum = DecodeBase58(encoded);
    if(dataWithChecksum.Length < 4)
      throw new FormatException("Invalid Base58Check format.");

    var data = new byte[dataWithChecksum.Length - 4];
    var checksum = new byte[4];
    Array.Copy(dataWithChecksum, 0, data, 0, data.Length);
    Array.Copy(dataWithChecksum, data.Length, checksum, 0, 4);

    var expectedChecksum = ComputeChecksum(data);
    if(!checksum.SequenceEqual(expectedChecksum))
      throw new FormatException("Invalid Base58Check checksum.");

    return data;
  }

  static byte[] ComputeChecksum(byte[] data)
  {
    using var sha256 = SHA256.Create();
    var hash1 = sha256.ComputeHash(data);
    var hash2 = sha256.ComputeHash(hash1);
    return hash2.Take(4).ToArray();
  }

  static string EncodeBase58(byte[] data)
  {
    var intData = new BigInteger(data.Reverse().Concat(new byte[] { 0 }).ToArray());
    var result = new StringBuilder();

    while(intData > 0)
    {
      intData = BigInteger.DivRem(intData, 58, out var remainder);
      result.Insert(0, Alphabet[(int)remainder]);
    }

    foreach(var b in data)
    {
      if(b != 0)
        break;
      result.Insert(0, Alphabet[0]);
    }

    return result.ToString();
  }

  static byte[] DecodeBase58(string encoded)
  {
    var intData = BigInteger.Zero;

    foreach(var c in encoded)
    {
      if(c >= _decodeMap.Length)
        throw new FormatException($"Invalid Base58 character: {c}");

      var digit = _decodeMap[c];
      if(digit == -1)
        throw new FormatException($"Invalid Base58 character: {c}");

      intData = intData * 58 + digit;
    }

    var bytes = intData.ToByteArray().Reverse().ToList();
    if(bytes.Count > 0 && bytes[0] == 0)
      bytes.RemoveAt(0);

    var leadingZeros = encoded.TakeWhile(c => c == Alphabet[0]).Count();
    bytes.InsertRange(0, Enumerable.Repeat((byte)0, leadingZeros));

    return bytes.ToArray();
  }

  static int[] CreateDecodeMap()
  {
    var map = new int[128];
    for(var i = 0; i < map.Length; i++)
      map[i] = -1;

    for(var i = 0; i < Alphabet.Length; i++)
      map[Alphabet[i]] = i;

    return map;
  }
}
