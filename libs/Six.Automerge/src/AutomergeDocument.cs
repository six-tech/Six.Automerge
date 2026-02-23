using System.Buffers.Binary;
using System.Text;
using Six.Automerge.FFI;
using Native = Six.Automerge.FFI.Native;

namespace Six.Automerge;

[PublicAPI]
public sealed class AutomergeDocument : IDisposable
{
  readonly Native.AutomergeDoc _doc;
  bool _disposed;
  static readonly UTF8Encoding ActorIdEncoding = new(false, true);

  AutomergeDocument(Native.AutomergeDoc doc)
  {
    _doc = doc;
  }

  void ThrowIfDisposed()
  {
    if(_disposed)
    {
      throw new ObjectDisposedException(nameof(AutomergeDocument));
    }
  }

  public static AutomergeDocument Create(string? actorId = null)
  {
    if(string.IsNullOrWhiteSpace(actorId))
    {
      using var none = Native.OptionUtf8String.None;
      return CreateDocument(none, false, null);
    }

    if(IsHexActorId(actorId))
    {
      using var actorOption = CreateActorHexOption(actorId);
      return CreateDocument(actorOption, true, actorId);
    }

    using var defaultOption = Native.OptionUtf8String.None;
    var document = CreateDocument(defaultOption, false, null);
    document.ActorId = actorId;
    return document;
  }

  static AutomergeDocument CreateDocument(
    Native.OptionUtf8String actorOption,
    bool actorProvided,
    string? requestedActorId)
  {
    try
    {
      var native = Native.AutomergeDoc.New(actorOption);
      return new AutomergeDocument(native);
    }
    catch(Native.InteropException ex)
    {
      var code = actorProvided
        ? Native.AutomergeFfiErrorCode.InvalidActorId
        : Native.AutomergeFfiErrorCode.AutomergeError;

      var message = actorProvided
        ? $"Invalid actor id '{requestedActorId}'. Provide a valid hex identifier or use a non-hex string to let the library derive one."
        : "Failed to create Automerge document.";

      throw new AutomergeException(code, message, ex);
    }
  }

  static Native.OptionUtf8String CreateActorHexOption(string actorId)
  {
    var utf8 = Native.Utf8String.From(actorId);
    return Native.OptionUtf8String.Some(utf8);
  }

  static bool IsHexActorId(string value)
  {
    if(value.Length == 0 || (value.Length % 2) != 0)
    {
      return false;
    }

    foreach(var ch in value)
    {
      if(!Uri.IsHexDigit(ch))
      {
        return false;
      }
    }

    return true;
  }

  static bool TryDecodeActorId(string hex, out string decoded)
  {
    decoded = string.Empty;
    if(!IsHexActorId(hex))
    {
      return false;
    }

    try
    {
      var bytes = Convert.FromHexString(hex);
      if(bytes.Length == 0)
      {
        return false;
      }

      var candidate = ActorIdEncoding.GetString(bytes);
      if(!IsPrintableActorId(candidate))
      {
        return false;
      }

      var roundtrip = Convert.ToHexString(ActorIdEncoding.GetBytes(candidate));
      if(!roundtrip.Equals(hex, StringComparison.OrdinalIgnoreCase))
      {
        return false;
      }

      decoded = candidate;
      return true;
    }
    catch(FormatException)
    {
      return false;
    }
    catch(DecoderFallbackException)
    {
      return false;
    }
    catch(EncoderFallbackException)
    {
      return false;
    }
  }

  static bool IsPrintableActorId(string value)
  {
    foreach(var ch in value)
    {
      if(char.IsControl(ch))
      {
        return false;
      }
    }

    return true;
  }

  public static AutomergeDocument Load(byte[] data)
  {
    if(data is null)
    {
      throw new ArgumentNullException(nameof(data));
    }

    if(data.Length == 0)
    {
      throw new ArgumentException("Cannot load empty data.", nameof(data));
    }

    try
    {
      using var slice = Native.SliceU8.From(data);
      var doc = Native.AutomergeDoc.Load(slice);
      return new AutomergeDocument(doc);
    }
    catch(Exception ex) when(ex.GetType().Name.StartsWith("EnumException") || ex.GetType().Name == "InteropException")
    {
      // Convert EnumException/InteropException from AsOk() to proper AutomergeException
      throw new AutomergeException(
        Native.AutomergeFfiErrorCode.AutomergeError,
        "Failed to load Automerge document. The data may be invalid or corrupted.",
        ex);
    }
  }

  public void Dispose()
  {
    if(_disposed)
    {
      return;
    }

    _disposed = true;

    // CRITICAL: Ensure all pending operations are committed before disposing the native document.
    // The Rust FFI library (automerge-0.7.0) panics during cleanup if there are uncommitted changes
    // because remove_actor() fails with "cant rewrite - actor is present" error.
    // This can occur if a document has pending operations that haven't been committed.
    // By calling Rollback(), we discard any uncommitted changes and ensure clean disposal.
    try
    {
      // Rollback any uncommitted transactions to ensure a clean state
      // This prevents the Rust layer from panicking during cleanup
      if (PendingOperations > 0)
      {
        Rollback();
      }
    }
    catch
    {
      // Ignore errors during rollback - we still need to dispose the document
    }

    _doc.Dispose();
  }

  public ulong PendingOperations
  {
    get
    {
      ThrowIfDisposed();
      return _doc.PendingOps();
    }
  }

  public AutomergeObjectId RootId
  {
    get
    {
      ThrowIfDisposed();
      var native = _doc.Root();
      return AutomergeObjectId.FromNative(native);
    }
  }

  public string ActorId
  {
    get
    {
      ThrowIfDisposed();
      using var native = _doc.GetActorId();
      var hex = native.AsManagedString();
      return TryDecodeActorId(hex, out var decoded)
        ? decoded
        : hex;
    }
    set
    {
      ThrowIfDisposed();
      if(string.IsNullOrWhiteSpace(value))
      {
        throw new ArgumentException("Actor id cannot be null or whitespace.", nameof(value));
      }

      if(IsHexActorId(value))
      {
        SetActorIdFromHex(value);
      }
      else
      {
        SetActorIdFromUtf8(value);
      }
    }
  }

  void SetActorIdFromHex(string hexActorId)
  {
    using var actor = Native.Utf8String.From(hexActorId);
    try
    {
      _doc.SetActorId(actor);
    }
    catch(Native.InteropException ex)
    {
      throw new AutomergeException(
        Native.AutomergeFfiErrorCode.InvalidActorId,
        $"Invalid actor id '{hexActorId}'.",
        ex);
    }
  }

  void SetActorIdFromUtf8(string actorId)
  {
    var bytes = ActorIdEncoding.GetBytes(actorId);
    using var slice = Native.SliceU8.From(bytes);
    try
    {
      _doc.ActorIdFromBytes(slice);
    }
    catch(Native.InteropException ex)
    {
      throw new AutomergeException(
        Native.AutomergeFfiErrorCode.AutomergeError,
        $"Failed to set actor id from value '{actorId}'.",
        ex);
    }
  }

  public string? Commit(string? message = null, long? timestampMillis = null)
  {
    ThrowIfDisposed();
    using var options = new Native.AutomergeCommitOptions
    {
      message = message is null
        ? Native.OptionUtf8String.None
        : Native.OptionUtf8String.Some(Native.Utf8String.From(message)),
      timestamp_millis = timestampMillis.HasValue
        ? Native.OptionI64.Some(timestampMillis.Value)
        : Native.OptionI64.None
    };

    var result = _doc.Commit(options);
    if(result.IsNone)
      return null;

    var hash = result.AsSome();
    return Convert.ToHexString(hash.AsManagedBytes()).ToLowerInvariant();
  }

  public ulong Rollback()
  {
    ThrowIfDisposed();
    return _doc.Rollback();
  }

  public byte[] Save()
  {
    ThrowIfDisposed();
    using var bytes = _doc.Save();
    return bytes.AsManagedBytes();
  }

  public byte[] SaveIncremental()
  {
    ThrowIfDisposed();
    using var bytes = _doc.SaveIncremental();
    return bytes.AsManagedBytes();
  }

  /// <summary>
  /// Saves all changes made after the specified heads.
  /// </summary>
  /// <remarks>
  /// <para>
  /// This method is used by the Storage subsystem to save incremental changes
  /// relative to known document heads. Unlike <see cref="SaveIncremental"/>,
  /// which uses an internal cursor, this method provides explicit control over
  /// what changes to save.
  /// </para>
  /// <para>
  /// Typical usage:
  /// </para>
  /// <list type="number">
  /// <item>Load document and record heads: <c>var baseHeads = doc.GetHeads()</c></item>
  /// <item>Make changes and commit</item>
  /// <item>Save only new changes: <c>var incremental = doc.SaveAfter(baseHeads)</c></item>
  /// <item>Load later with: <c>doc.LoadIncremental(incremental)</c></item>
  /// </list>
  /// <para>
  /// This corresponds to the TypeScript <c>A.saveSince(doc, heads)</c> API and is
  /// critical for efficient storage subsystem operations.
  /// </para>
  /// </remarks>
  /// <param name="heads">The heads representing the baseline state. Pass empty collection to save all changes.</param>
  /// <returns>Binary data containing changes made after the specified heads. Returns empty array if no new changes.</returns>
  /// <exception cref="ArgumentNullException">Thrown when heads is null.</exception>
  /// <exception cref="ArgumentException">Thrown when heads contains invalid hash strings.</exception>
  /// <exception cref="ObjectDisposedException">Thrown when the document has been disposed.</exception>
  [PublicAPI]
  public byte[] SaveAfter(IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();

    var headArray = heads.ToArray();

    // Empty heads is valid - it means "save all changes from the beginning"
    // (equivalent to save_after(&[]) in Rust)
    var hashes = new Native.ChangeHashBytes[headArray.Length];
    for(var i = 0; i < headArray.Length; i++)
    {
      try
      {
        hashes[i] = ParseHash(headArray[i]);
      }
      catch(ArgumentException ex)
      {
        throw new ArgumentException($"Invalid hash at index {i}: {ex.Message}", nameof(heads), ex);
      }
    }

    using var slice = Native.SliceChangeHashBytes.From(hashes);
    using var bytes = _doc.SaveAfter(slice);
    return bytes.AsManagedBytes();
  }

  [PublicAPI]
  public byte[] SaveNoCompress()
  {
    ThrowIfDisposed();
    using var bytes = _doc.SaveNocompress();
    return bytes.AsManagedBytes();
  }

  [PublicAPI]
  public byte[] SaveWithOptions(SaveOptions options)
  {
    if(options is null)
    {
      throw new ArgumentNullException(nameof(options));
    }

    ThrowIfDisposed();
    using var bytes = _doc.SaveWithOptions(options.ToNative());
    return bytes.AsManagedBytes();
  }

  [PublicAPI]
  public static AutomergeDocument LoadWithOptions(byte[] data, LoadOptions options)
  {
    if(data is null)
    {
      throw new ArgumentNullException(nameof(data));
    }

    if(data.Length == 0)
    {
      throw new ArgumentException("Cannot load empty data.", nameof(data));
    }

    if(options is null)
    {
      throw new ArgumentNullException(nameof(options));
    }

    try
    {
      using var slice = Native.SliceU8.From(data);
      var doc = Native.AutomergeDoc.LoadWithOptions(slice, options.ToNative());
      return new AutomergeDocument(doc);
    }
    catch(Exception ex) when(ex.GetType().Name.StartsWith("EnumException") || ex.GetType().Name == "InteropException")
    {
      throw new AutomergeException(
        Native.AutomergeFfiErrorCode.AutomergeError,
        "Failed to load Automerge document. The data may be invalid or corrupted.",
        ex);
    }
  }

  public ulong LoadIncremental(byte[] data)
  {
    ThrowIfDisposed();
    if(data is null)
    {
      throw new ArgumentNullException(nameof(data));
    }

    using var slice = Native.SliceU8.From(data);
    return _doc.LoadIncremental(slice);
  }

  public ulong LoadIncremental(ReadOnlySpan<byte> data)
  {
    if(data.Length == 0)
    {
      return LoadIncremental(Array.Empty<byte>());
    }

    return LoadIncremental(data.ToArray());
  }

  public AutomergeContainerType GetObjectType(AutomergeObjectId objectId)
  {
    ThrowIfDisposed();
    var native = ToNativeObjectId(objectId);
    try
    {
      var value = _doc.ObjectType(ref native);
      return value.FromNative();
    }
    finally
    {
      native.Dispose();
    }
  }

  public void PutString(AutomergeObjectId objectId, string key, string value)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      using var valueUtf8 = ToUtf8(value, nameof(value));
      _doc.MapPutString(ref target, keyUtf8, valueUtf8);
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Gets a string value from the specified object at the given key.
  /// </summary>
  /// <param name="objectId">The target object.</param>
  /// <param name="key">The property key.</param>
  /// <returns>
  /// The string value if found and of correct type; otherwise null.
  /// Returns null if the key doesn't exist, the value is not a string, or the value is explicitly null.
  /// </returns>
  /// <example>
  /// <code>
  /// using var doc = AutomergeDocument.Create();
  /// var root = doc.RootId;
  /// doc.PutString(root, "name", "Alice");
  /// doc.Commit();
  ///
  /// var name = doc.GetString(root, "name"); // Returns "Alice"
  /// var missing = doc.GetString(root, "nonexistent"); // Returns null
  /// </code>
  /// </example>
  [PublicAPI]
  public string? GetString(AutomergeObjectId objectId, string key)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      var result = _doc.MapGet(ref target, keyUtf8);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsString)
            {
              if(scalar.string_value.IsSome)
              {
                var str = scalar.string_value.AsSome();
                return str.AsManagedString();
              }
            }
          }
        }
      }
      return null;
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Stores an immutable string value at the specified key.
  /// </summary>
  /// <param name="objectId">The target object.</param>
  /// <param name="key">The property key.</param>
  /// <param name="value">The immutable string value to store.</param>
  /// <remarks>
  /// <para>
  /// Immutable strings are stored as regular string scalars but signal intent
  /// that the value should be replaced atomically, not merged character-by-character.
  /// </para>
  /// <para>
  /// Use <see cref="ImmutableString"/> for values like URLs, IDs, enum-like constants,
  /// or any string that must be replaced as a complete unit during concurrent updates.
  /// </para>
  /// </remarks>
  /// <example>
  /// <code>
  /// using var doc = AutomergeDocument.Create();
  /// var root = doc.RootId;
  /// doc.PutImmutableString(root, "websiteUrl", new ImmutableString("https://example.com"));
  /// doc.Commit("Add website URL");
  /// </code>
  /// </example>
  [PublicAPI]
  public void PutImmutableString(AutomergeObjectId objectId, string key, ImmutableString value)
  {
    // ImmutableString has implicit conversion to string
    // Store as regular string scalar - the type distinction is semantic only
    PutString(objectId, key, value);
  }

  /// <summary>
  /// Gets an immutable string value from the specified object at the given key.
  /// </summary>
  /// <param name="objectId">The target object.</param>
  /// <param name="key">The property key.</param>
  /// <returns>
  /// An ImmutableString wrapping the value if found and is a string; otherwise null.
  /// Returns null if the key doesn't exist, the value is not a string, or the value is explicitly null.
  /// </returns>
  /// <remarks>
  /// This method retrieves the same underlying string value stored by either PutString or PutImmutableString,
  /// wrapping it in an ImmutableString. The distinction between string and ImmutableString is semantic only
  /// and exists at the presentation layer, not in storage.
  /// </remarks>
  /// <example>
  /// <code>
  /// using var doc = AutomergeDocument.Create();
  /// var root = doc.RootId;
  /// doc.PutImmutableString(root, "url", new ImmutableString("https://example.com"));
  /// doc.Commit();
  ///
  /// var url = doc.GetImmutableString(root, "url"); // Returns ImmutableString("https://example.com")
  /// var missing = doc.GetImmutableString(root, "nonexistent"); // Returns null
  /// </code>
  /// </example>
  [PublicAPI]
  public ImmutableString? GetImmutableString(AutomergeObjectId objectId, string key)
  {
    var str = GetString(objectId, key);
    return str is null ? null : new ImmutableString(str);
  }

  public void PutBoolean(AutomergeObjectId objectId, string key, bool value)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      _doc.MapPutBoolean(ref target, keyUtf8, value);
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Gets a boolean value from the specified object at the given key.
  /// </summary>
  /// <param name="objectId">The target object.</param>
  /// <param name="key">The property key.</param>
  /// <returns>
  /// The boolean value if found and of correct type; otherwise null.
  /// </returns>
  [PublicAPI]
  public bool? GetBoolean(AutomergeObjectId objectId, string key)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      var result = _doc.MapGet(ref target, keyUtf8);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsBoolean)
            {
              return scalar.bool_value;
            }
          }
        }
      }
      return null;
    }
    finally
    {
      target.Dispose();
    }
  }

  public void PutInt64(AutomergeObjectId objectId, string key, long value)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      _doc.MapPutInt(ref target, keyUtf8, value);
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Gets a 64-bit signed integer value from the specified object at the given key.
  /// </summary>
  /// <param name="objectId">The target object.</param>
  /// <param name="key">The property key.</param>
  /// <returns>
  /// The int64 value if found and of correct type; otherwise null.
  /// </returns>
  [PublicAPI]
  public long? GetInt64(AutomergeObjectId objectId, string key)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      var result = _doc.MapGet(ref target, keyUtf8);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsInt)
            {
              return scalar.int_value;
            }
          }
        }
      }
      return null;
    }
    finally
    {
      target.Dispose();
    }
  }

  public void PutUInt64(AutomergeObjectId objectId, string key, ulong value)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      _doc.MapPutUint(ref target, keyUtf8, value);
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Gets a 64-bit unsigned integer value from the specified object at the given key.
  /// </summary>
  /// <param name="objectId">The target object.</param>
  /// <param name="key">The property key.</param>
  /// <returns>
  /// The uint64 value if found and of correct type; otherwise null.
  /// </returns>
  [PublicAPI]
  public ulong? GetUInt64(AutomergeObjectId objectId, string key)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      var result = _doc.MapGet(ref target, keyUtf8);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsUint)
            {
              return scalar.uint_value;
            }
          }
        }
      }
      return null;
    }
    finally
    {
      target.Dispose();
    }
  }

  public void PutDouble(AutomergeObjectId objectId, string key, double value)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      _doc.MapPutF64(ref target, keyUtf8, value);
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Gets a double-precision floating-point value from the specified object at the given key.
  /// </summary>
  /// <param name="objectId">The target object.</param>
  /// <param name="key">The property key.</param>
  /// <returns>
  /// The double value if found and of correct type; otherwise null.
  /// </returns>
  [PublicAPI]
  public double? GetDouble(AutomergeObjectId objectId, string key)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      var result = _doc.MapGet(ref target, keyUtf8);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsF64)
            {
              return scalar.f64_value;
            }
          }
        }
      }
      return null;
    }
    finally
    {
      target.Dispose();
    }
  }

  public void PutBytes(AutomergeObjectId objectId, string key, ReadOnlySpan<byte> value)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      using var slice = CreateSlice(value);
      _doc.MapPutBytes(ref target, keyUtf8, slice);
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Gets a byte array value from the specified object at the given key.
  /// </summary>
  /// <param name="objectId">The target object.</param>
  /// <param name="key">The property key.</param>
  /// <returns>
  /// The byte array if found and of correct type; otherwise null.
  /// </returns>
  [PublicAPI]
  public byte[]? GetBytes(AutomergeObjectId objectId, string key)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      var result = _doc.MapGet(ref target, keyUtf8);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsBytes)
            {
              return CopyVec(scalar.bytes_value);
            }
          }
        }
      }
      return null;
    }
    finally
    {
      target.Dispose();
    }
  }

  public void PutNull(AutomergeObjectId objectId, string key)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      _doc.MapPutNull(ref target, keyUtf8);
    }
    finally
    {
      target.Dispose();
    }
  }

  public void PutCounter(AutomergeObjectId objectId, string key, long value)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      _doc.MapPutCounter(ref target, keyUtf8, value);
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Gets a counter value from the specified object at the given key.
  /// </summary>
  /// <param name="objectId">The target object.</param>
  /// <param name="key">The property key.</param>
  /// <returns>
  /// The counter value if found and of correct type; otherwise null.
  /// </returns>
  [PublicAPI]
  public long? GetCounter(AutomergeObjectId objectId, string key)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      var result = _doc.MapGet(ref target, keyUtf8);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsCounter)
            {
              return scalar.counter_value;
            }
          }
        }
      }
      return null;
    }
    finally
    {
      target.Dispose();
    }
  }

  public void PutTimestamp(AutomergeObjectId objectId, string key, long unixMilliseconds)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      _doc.MapPutTimestamp(ref target, keyUtf8, unixMilliseconds);
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Gets a timestamp value from the specified object at the given key.
  /// </summary>
  /// <param name="objectId">The target object.</param>
  /// <param name="key">The property key.</param>
  /// <returns>
  /// The timestamp value as Unix milliseconds if found and of correct type; otherwise null.
  /// </returns>
  [PublicAPI]
  public long? GetTimestamp(AutomergeObjectId objectId, string key)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      var result = _doc.MapGet(ref target, keyUtf8);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsTimestamp)
            {
              return scalar.timestamp_value;
            }
          }
        }
      }
      return null;
    }
    finally
    {
      target.Dispose();
    }
  }

  public AutomergeObjectId PutObject(AutomergeObjectId objectId, string key, AutomergeContainerType type)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      var created = _doc.MapPutObject(ref target, keyUtf8, type.ToNative());
      return AutomergeObjectId.FromNative(created);
    }
    finally
    {
      target.Dispose();
    }
  }

  public void Delete(AutomergeObjectId objectId, string key)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      _doc.MapDelete(ref target, keyUtf8);
    }
    finally
    {
      target.Dispose();
    }
  }

  public void ListInsertString(AutomergeObjectId listId, ulong index, string value)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      using var valueUtf8 = ToUtf8(value, nameof(value));
      _doc.ListInsertString(ref list, index, valueUtf8);
    }
    finally
    {
      list.Dispose();
    }
  }

  public void ListInsertBoolean(AutomergeObjectId listId, ulong index, bool value)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      _doc.ListInsertBool(ref list, index, value);
    }
    finally
    {
      list.Dispose();
    }
  }

  public void ListInsertInt64(AutomergeObjectId listId, ulong index, long value)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      _doc.ListInsertInt(ref list, index, value);
    }
    finally
    {
      list.Dispose();
    }
  }

  public void ListInsertUInt64(AutomergeObjectId listId, ulong index, ulong value)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      _doc.ListInsertUint(ref list, index, value);
    }
    finally
    {
      list.Dispose();
    }
  }

  public void ListInsertDouble(AutomergeObjectId listId, ulong index, double value)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      _doc.ListInsertF64(ref list, index, value);
    }
    finally
    {
      list.Dispose();
    }
  }

  public void ListInsertBytes(AutomergeObjectId listId, ulong index, ReadOnlySpan<byte> value)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      using var slice = CreateSlice(value);
      _doc.ListInsertBytes(ref list, index, slice);
    }
    finally
    {
      list.Dispose();
    }
  }

  public void ListInsertNull(AutomergeObjectId listId, ulong index)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      _doc.ListInsertNull(ref list, index);
    }
    finally
    {
      list.Dispose();
    }
  }

  public AutomergeObjectId ListInsertObject(AutomergeObjectId listId, ulong index, AutomergeContainerType type)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      var created = _doc.ListInsertObject(ref list, index, type.ToNative());
      return AutomergeObjectId.FromNative(created);
    }
    finally
    {
      list.Dispose();
    }
  }

  public void ListRemove(AutomergeObjectId listId, ulong index)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      _doc.ListRemove(ref list, index);
    }
    finally
    {
      list.Dispose();
    }
  }

  public void ListPutString(AutomergeObjectId listId, ulong index, string value)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      using var valueUtf8 = ToUtf8(value, nameof(value));
      _doc.ListPutString(ref list, index, valueUtf8);
    }
    finally
    {
      list.Dispose();
    }
  }

  public void ListPutBoolean(AutomergeObjectId listId, ulong index, bool value)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      _doc.ListPutBool(ref list, index, value);
    }
    finally
    {
      list.Dispose();
    }
  }

  public void ListPutInt64(AutomergeObjectId listId, ulong index, long value)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      _doc.ListPutInt(ref list, index, value);
    }
    finally
    {
      list.Dispose();
    }
  }

  public void ListPutUInt64(AutomergeObjectId listId, ulong index, ulong value)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      _doc.ListPutUint(ref list, index, value);
    }
    finally
    {
      list.Dispose();
    }
  }

  public void ListPutDouble(AutomergeObjectId listId, ulong index, double value)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      _doc.ListPutF64(ref list, index, value);
    }
    finally
    {
      list.Dispose();
    }
  }

  public void ListPutBytes(AutomergeObjectId listId, ulong index, ReadOnlySpan<byte> value)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      using var slice = CreateSlice(value);
      _doc.ListPutBytes(ref list, index, slice);
    }
    finally
    {
      list.Dispose();
    }
  }

  public void ListPutNull(AutomergeObjectId listId, ulong index)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      _doc.ListPutNull(ref list, index);
    }
    finally
    {
      list.Dispose();
    }
  }

  public AutomergeObjectId ListPutObject(AutomergeObjectId listId, ulong index, AutomergeContainerType type)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      var created = _doc.ListPutObject(ref list, index, type.ToNative());
      return AutomergeObjectId.FromNative(created);
    }
    finally
    {
      list.Dispose();
    }
  }

  public void ListSplice(AutomergeObjectId listId, ulong index, long delete, IReadOnlyList<AutomergeScalarValue> values)
  {
    if(values is null)
    {
      throw new ArgumentNullException(nameof(values));
    }

    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      var nativeValues = new Native.AutomergeScalarValue[values.Count];
      for(int i = 0; i < values.Count; i++)
      {
        nativeValues[i] = CreateNativeScalar(values[i]);
      }

      using var slice = Native.SliceAutomergeScalarValue.From(nativeValues);
      _doc.ListSplice(ref list, index, delete, slice);

      for(int i = 0; i < nativeValues.Length; i++)
      {
        nativeValues[i].Dispose();
      }
    }
    finally
    {
      list.Dispose();
    }
  }

  public AutomergeCursor GetCursor(AutomergeObjectId objectId, ulong index)
  {
    ThrowIfDisposed();
    var obj = ToNativeObjectId(objectId);
    try
    {
      using var result = _doc.GetCursor(ref obj, index);
      var bytes = new byte[result.data.Count];
      for(int i = 0; i < bytes.Length; i++)
      {
        bytes[i] = result.data[i];
      }

      return new AutomergeCursor(bytes);
    }
    finally
    {
      obj.Dispose();
    }
  }

  public ulong GetCursorPosition(AutomergeObjectId objectId, AutomergeCursor cursor)
  {
    if(cursor is null)
    {
      throw new ArgumentNullException(nameof(cursor));
    }

    ThrowIfDisposed();
    var obj = ToNativeObjectId(objectId);
    try
    {
      var cursorBytes = new Native.ByteArray { data = CreateVec(cursor.AsSpan()) };
      return _doc.GetCursorPosition(ref obj, cursorBytes);
    }
    finally
    {
      obj.Dispose();
    }
  }

  public void Increment(AutomergeObjectId objectId, string key, long delta)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      _doc.MapIncrement(ref target, keyUtf8, delta);
    }
    finally
    {
      target.Dispose();
    }
  }

  public void Increment(AutomergeObjectId listId, ulong index, long delta)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      _doc.ListIncrement(ref list, index, delta);
    }
    finally
    {
      list.Dispose();
    }
  }

  public string GetText(AutomergeObjectId textId)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(textId);
    try
    {
      using var text = _doc.Text(ref target);
      return text.AsManagedString();
    }
    finally
    {
      target.Dispose();
    }
  }

  public string GetTextAtHeads(AutomergeObjectId textId, IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();

    var target = ToNativeObjectId(textId);
    try
    {
      var hashes = heads.Select(ParseHash).ToArray();
      using var slice = Native.SliceChangeHashBytes.From(hashes);
      using var text = _doc.TextAt(ref target, slice);
      return text.AsManagedString();
    }
    finally
    {
      target.Dispose();
    }
  }

  [PublicAPI]
  public string? ListGetString(AutomergeObjectId listId, ulong index)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      var result = _doc.ListGet(ref list, index);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsString && scalar.string_value.IsSome)
            {
              return scalar.string_value.AsSome().AsManagedString();
            }
          }
        }
      }
      return null;
    }
    finally
    {
      list.Dispose();
    }
  }

  [PublicAPI]
  public long? ListGetInt64(AutomergeObjectId listId, ulong index)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      var result = _doc.ListGet(ref list, index);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsInt)
            {
              return scalar.int_value;
            }
          }
        }
      }
      return null;
    }
    finally
    {
      list.Dispose();
    }
  }

  [PublicAPI]
  public bool? ListGetBoolean(AutomergeObjectId listId, ulong index)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      var result = _doc.ListGet(ref list, index);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsBoolean)
            {
              return scalar.bool_value;
            }
          }
        }
      }
      return null;
    }
    finally
    {
      list.Dispose();
    }
  }

  [PublicAPI]
  public double? ListGetDouble(AutomergeObjectId listId, ulong index)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      var result = _doc.ListGet(ref list, index);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsF64)
            {
              return scalar.f64_value;
            }
          }
        }
      }
      return null;
    }
    finally
    {
      list.Dispose();
    }
  }

  [PublicAPI]
  public ulong? ListGetUInt64(AutomergeObjectId listId, ulong index)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      var result = _doc.ListGet(ref list, index);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsUint)
            {
              return scalar.uint_value;
            }
          }
        }
      }
      return null;
    }
    finally
    {
      list.Dispose();
    }
  }

  [PublicAPI]
  public byte[]? ListGetBytes(AutomergeObjectId listId, ulong index)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      var result = _doc.ListGet(ref list, index);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsBytes)
            {
              return CopyVec(scalar.bytes_value);
            }
          }
        }
      }
      return null;
    }
    finally
    {
      list.Dispose();
    }
  }

  [PublicAPI]
  public AutomergeObjectId? ListGetObject(AutomergeObjectId listId, ulong index)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      var result = _doc.ListGetObject(ref list, index);
      using(result)
      {
        if(result.IsSome)
        {
          var objId = result.AsSome();
          return ConvertObjectId(objId);
        }
      }
      return null;
    }
    finally
    {
      list.Dispose();
    }
  }

  [PublicAPI]
  public AutomergeObjectId? GetObject(AutomergeObjectId objectId, string key)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      var result = _doc.MapGetObject(ref target, keyUtf8);
      using(result)
      {
        if(result.IsSome)
        {
          var objId = result.AsSome();
          return ConvertObjectId(objId);
        }
      }
      return null;
    }
    finally
    {
      target.Dispose();
    }
  }

  [PublicAPI]
  public string? GetStringAtHeads(AutomergeObjectId objectId, string key, IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      var hashes = heads.Select(ParseHash).ToArray();
      using var slice = Native.SliceChangeHashBytes.From(hashes);
      var result = _doc.MapGetAt(ref target, keyUtf8, slice);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsString && scalar.string_value.IsSome)
            {
              return scalar.string_value.AsSome().AsManagedString();
            }
          }
        }
      }
      return null;
    }
    finally
    {
      target.Dispose();
    }
  }

  [PublicAPI]
  public string? ListGetStringAtHeads(AutomergeObjectId listId, ulong index, IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      var hashes = heads.Select(ParseHash).ToArray();
      using var slice = Native.SliceChangeHashBytes.From(hashes);
      var result = _doc.ListGetAt(ref list, index, slice);
      using(result)
      {
        if(result.IsSome)
        {
          var scalar = result.AsSome();
          using(scalar)
          {
            if(scalar.value_type.IsString && scalar.string_value.IsSome)
            {
              return scalar.string_value.AsSome().AsManagedString();
            }
          }
        }
      }
      return null;
    }
    finally
    {
      list.Dispose();
    }
  }

  [PublicAPI]
  public IReadOnlyList<AutomergeConflictValue> GetAll(AutomergeObjectId objectId, string key)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      using var vec = _doc.MapGetAll(ref target, keyUtf8);
      return ConvertConflictValues(vec);
    }
    finally
    {
      target.Dispose();
    }
  }

  [PublicAPI]
  public IReadOnlyList<AutomergeConflictValue> GetAll(AutomergeObjectId listId, ulong index)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      using var vec = _doc.ListGetAll(ref list, index);
      return ConvertConflictValues(vec);
    }
    finally
    {
      list.Dispose();
    }
  }

  [PublicAPI]
  public IReadOnlyList<AutomergeConflictValue> GetAllAtHeads(
    AutomergeObjectId objectId,
    string key,
    IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();
    var target = ToNativeObjectId(objectId);
    try
    {
      using var keyUtf8 = ToUtf8(key, nameof(key));
      var hashes = heads.Select(ParseHash).ToArray();
      using var slice = Native.SliceChangeHashBytes.From(hashes);
      using var vec = _doc.MapGetAllAt(ref target, keyUtf8, slice);
      return ConvertConflictValues(vec);
    }
    finally
    {
      target.Dispose();
    }
  }

  [PublicAPI]
  public IReadOnlyList<AutomergeConflictValue> GetAllAtHeads(
    AutomergeObjectId listId,
    ulong index,
    IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      var hashes = heads.Select(ParseHash).ToArray();
      using var slice = Native.SliceChangeHashBytes.From(hashes);
      using var vec = _doc.ListGetAllAt(ref list, index, slice);
      return ConvertConflictValues(vec);
    }
    finally
    {
      list.Dispose();
    }
  }

  public AutomergeDocument Fork()
  {
    ThrowIfDisposed();
    var forked = Native.AutomergeDoc.Fork(_doc.Context);
    return new AutomergeDocument(forked);
  }

  public string EmptyChange(string? message = null, long? timestampMillis = null)
  {
    ThrowIfDisposed();
    using var options = new Native.AutomergeCommitOptions
    {
      message = message is null
        ? Native.OptionUtf8String.None
        : Native.OptionUtf8String.Some(Native.Utf8String.From(message)),
      timestamp_millis = timestampMillis.HasValue
        ? Native.OptionI64.Some(timestampMillis.Value)
        : Native.OptionI64.None
    };

    var hash = _doc.EmptyChange(options);
    return Convert.ToHexString(hash.AsManagedBytes()).ToLowerInvariant();
  }

  public IReadOnlyList<string> BaseHeads()
  {
    ThrowIfDisposed();
    using var heads = _doc.BaseHeads();
    var hashes = heads.AsHashList();
    return hashes
      .Select(hash => Convert.ToHexString(hash.AsManagedBytes()).ToLowerInvariant())
      .ToArray();
  }

  public void SpliceText(AutomergeObjectId textId, ulong index, long deleteCount, string text)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(textId);
    try
    {
      using var valueUtf8 = ToUtf8(text, nameof(text));
      _doc.ListSpliceText(ref target, index, deleteCount, valueUtf8);
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Applies a string mark to a range of text.
  /// </summary>
  [PublicAPI]
  public void Mark(AutomergeObjectId textId, ulong start, ulong end, string name, string markValue, ExpandMode expand = ExpandMode.After)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(textId);
    try
    {
      using var nameUtf8 = ToUtf8(name, nameof(name));
      using var scalar = CreateStringScalar(markValue);
      _doc.Mark(ref target, start, end, nameUtf8, scalar, expand.ToNative());
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Applies a boolean mark to a range of text.
  /// </summary>
  [PublicAPI]
  public void Mark(AutomergeObjectId textId, ulong start, ulong end, string name, bool markValue, ExpandMode expand = ExpandMode.After)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(textId);
    try
    {
      using var nameUtf8 = ToUtf8(name, nameof(name));
      using var scalar = CreateBoolScalar(markValue);
      _doc.Mark(ref target, start, end, nameUtf8, scalar, expand.ToNative());
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Applies an integer mark to a range of text.
  /// </summary>
  [PublicAPI]
  public void Mark(AutomergeObjectId textId, ulong start, ulong end, string name, long markValue, ExpandMode expand = ExpandMode.After)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(textId);
    try
    {
      using var nameUtf8 = ToUtf8(name, nameof(name));
      using var scalar = CreateIntScalar(markValue);
      _doc.Mark(ref target, start, end, nameUtf8, scalar, expand.ToNative());
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Removes a mark from a range of text.
  /// </summary>
  [PublicAPI]
  public void Unmark(AutomergeObjectId textId, string name, ulong start, ulong end, ExpandMode expand = ExpandMode.After)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(textId);
    try
    {
      using var nameUtf8 = ToUtf8(name, nameof(name));
      _doc.Unmark(ref target, nameUtf8, start, end, expand.ToNative());
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Gets all marks currently applied to a text object.
  /// </summary>
  [PublicAPI]
  public IReadOnlyList<AutomergeMarkInfo> GetMarks(AutomergeObjectId textId)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(textId);
    try
    {
      using var vec = _doc.Marks(ref target);
      return ConvertMarkInfos(vec);
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Gets all marks applied to a text object at a historical point in time.
  /// </summary>
  [PublicAPI]
  public IReadOnlyList<AutomergeMarkInfo> GetMarksAtHeads(AutomergeObjectId textId, IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();
    var target = ToNativeObjectId(textId);
    try
    {
      var hashes = heads.Select(ParseHash).ToArray();
      using var slice = Native.SliceChangeHashBytes.From(hashes);
      using var vec = _doc.MarksAt(ref target, slice);
      return ConvertMarkInfos(vec);
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Gets the sequence of text spans and block markers in a text object.
  /// Each span is either a <see cref="AutomergeTextSpan"/> with text content and active marks,
  /// or an <see cref="AutomergeBlockSpan"/> representing a block-level marker.
  /// </summary>
  [PublicAPI]
  public IReadOnlyList<AutomergeSpan> GetSpans(AutomergeObjectId textId)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(textId);
    try
    {
      using var vec = _doc.Spans(ref target);
      return ConvertSpans(vec);
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Gets the sequence of text spans and block markers at a historical point in time.
  /// </summary>
  [PublicAPI]
  public IReadOnlyList<AutomergeSpan> GetSpansAtHeads(AutomergeObjectId textId, IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();
    var target = ToNativeObjectId(textId);
    try
    {
      var hashes = heads.Select(ParseHash).ToArray();
      using var slice = Native.SliceChangeHashBytes.From(hashes);
      using var vec = _doc.SpansAt(ref target, slice);
      return ConvertSpans(vec);
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Inserts a block marker into a text object at the given index, splitting the text
  /// into two sections. Returns the <see cref="AutomergeObjectId"/> of the new block marker,
  /// which can be used to set properties like "type" and "parents" via PutString/PutObject.
  /// </summary>
  [PublicAPI]
  public AutomergeObjectId SplitBlock(AutomergeObjectId textId, ulong index)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(textId);
    try
    {
      var blockId = _doc.SplitBlock(ref target, index);
      return AutomergeObjectId.FromNative(blockId);
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Removes a block marker at the given index from a text object, joining the text
  /// on either side of the marker.
  /// </summary>
  [PublicAPI]
  public void JoinBlock(AutomergeObjectId textId, ulong index)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(textId);
    try
    {
      _doc.JoinBlock(ref target, index);
    }
    finally
    {
      target.Dispose();
    }
  }

  /// <summary>
  /// Updates the blocks and text in a text object by computing a minimal diff against
  /// the current state and applying only the necessary changes.
  /// </summary>
  [PublicAPI]
  public void UpdateSpans(AutomergeObjectId textId, IReadOnlyList<AutomergeSpan> spans)
  {
    ThrowIfDisposed();
    if(spans is null)
    {
      throw new ArgumentNullException(nameof(spans));
    }

    var target = ToNativeObjectId(textId);
    try
    {
      var nativeSpans = new Native.AutomergeSpan[spans.Count];
      for(var i = 0; i < spans.Count; i++)
      {
        nativeSpans[i] = ConvertToNativeSpan(spans[i]);
      }

      using var slice = Native.SliceAutomergeSpan.From(nativeSpans);
      _doc.UpdateSpans(ref target, slice);
    }
    finally
    {
      target.Dispose();
    }
  }

  static Native.AutomergeSpan ConvertToNativeSpan(AutomergeSpan span)
  {
    if(span is AutomergeTextSpan textSpan)
    {
      var nativeMarks = new Native.AutomergeMarkData[textSpan.Marks.Count];
      for(var i = 0; i < textSpan.Marks.Count; i++)
      {
        var mark = textSpan.Marks[i];
        nativeMarks[i] = new Native.AutomergeMarkData
        {
          name = Native.Utf8String.From(mark.Name),
          value = CreateScalarFromMarkInfo(mark)
        };
      }

      return new Native.AutomergeSpan
      {
        kind = Native.AutomergeSpanKind.Text,
        text = Native.Utf8String.From(textSpan.Text),
        marks = nativeMarks.Length > 0
          ? Native.VecAutomergeMarkData.From(nativeMarks)
          : Native.VecAutomergeMarkData.Empty(),
        block_obj_id = new Native.AutomergeObjId { bytes = Native.VecU8.Empty() },
        block_type = Native.Utf8String.From(string.Empty),
        block_parents = Native.VecUtf8String.Empty(),
        block_attrs_json = Native.Utf8String.From(string.Empty)
      };
    }

    if(span is AutomergeBlockSpan blockSpan)
    {
      var parentStrings = new Native.Utf8String[blockSpan.Parents.Count];
      for(var i = 0; i < blockSpan.Parents.Count; i++)
      {
        parentStrings[i] = Native.Utf8String.From(blockSpan.Parents[i]);
      }

      return new Native.AutomergeSpan
      {
        kind = Native.AutomergeSpanKind.Block,
        text = Native.Utf8String.From(string.Empty),
        marks = Native.VecAutomergeMarkData.Empty(),
        block_obj_id = new Native.AutomergeObjId { bytes = Native.VecU8.Empty() },
        block_type = Native.Utf8String.From(blockSpan.BlockType),
        block_parents = parentStrings.Length > 0
          ? Native.VecUtf8String.From(parentStrings)
          : Native.VecUtf8String.Empty(),
        block_attrs_json = Native.Utf8String.From(blockSpan.AttributesJson ?? "{}")
      };
    }

    throw new ArgumentException($"Unknown span type: {span.GetType().Name}", nameof(span));
  }

  static Native.AutomergeScalarValue CreateScalarFromMarkInfo(AutomergeMarkInfo mark)
  {
    return mark.Value.Kind switch
    {
      AutomergeScalarValueKind.String => CreateStringScalar(mark.Value.StringValue ?? string.Empty),
      AutomergeScalarValueKind.Boolean => CreateBoolScalar(mark.Value.BoolValue ?? false),
      AutomergeScalarValueKind.Int => CreateIntScalar(mark.Value.IntValue ?? 0),
      AutomergeScalarValueKind.Null => CreateNullScalar(),
      _ => CreateNullScalar()
    };
  }

  static Native.AutomergeScalarValue CreateNullScalar()
  {
    return new Native.AutomergeScalarValue
    {
      value_type = Native.AutomergeScalarType.Null,
      string_value = Native.OptionUtf8String.None,
      bool_value = false,
      int_value = 0,
      uint_value = 0,
      f64_value = 0,
      bytes_value = Native.VecU8.Empty(),
      counter_value = 0,
      timestamp_value = 0,
      unknown_type_code = 0,
      unknown_bytes = Native.VecU8.Empty()
    };
  }

  public ulong ListLength(AutomergeObjectId listId)
  {
    ThrowIfDisposed();
    var list = ToNativeObjectId(listId);
    try
    {
      return _doc.ListLength(ref list);
    }
    finally
    {
      list.Dispose();
    }
  }

  public IReadOnlyList<string> GetKeys(AutomergeObjectId mapId)
  {
    ThrowIfDisposed();
    var target = ToNativeObjectId(mapId);
    try
    {
      var keys = _doc.MapKeys(ref target);
      return keys.AsStringList();
    }
    finally
    {
      target.Dispose();
    }
  }

  public IReadOnlyList<string> GetKeysAtHeads(AutomergeObjectId mapId, IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();

    var target = ToNativeObjectId(mapId);
    try
    {
      var hashes = heads.Select(ParseHash).ToArray();
      using var slice = Native.SliceChangeHashBytes.From(hashes);
      var keys = _doc.MapKeysAt(ref target, slice);
      return keys.AsStringList();
    }
    finally
    {
      target.Dispose();
    }
  }

  public ulong LengthAtHeads(AutomergeObjectId objectId, IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();

    var target = ToNativeObjectId(objectId);
    try
    {
      var hashes = heads.Select(ParseHash).ToArray();
      using var slice = Native.SliceChangeHashBytes.From(hashes);
      return _doc.LengthAt(ref target, slice);
    }
    finally
    {
      target.Dispose();
    }
  }

  public IReadOnlyList<AutomergeConflictValue> Values(AutomergeObjectId objectId)
  {
    ThrowIfDisposed();

    var target = ToNativeObjectId(objectId);
    try
    {
      var vec = _doc.ObjValues(ref target);
      return ConvertConflictValues(vec);
    }
    finally
    {
      target.Dispose();
    }
  }

  public IReadOnlyList<AutomergeConflictValue> ValuesAtHeads(AutomergeObjectId objectId, IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();

    var target = ToNativeObjectId(objectId);
    try
    {
      var hashes = heads.Select(ParseHash).ToArray();
      using var slice = Native.SliceChangeHashBytes.From(hashes);
      var vec = _doc.ObjValuesAt(ref target, slice);
      return ConvertConflictValues(vec);
    }
    finally
    {
      target.Dispose();
    }
  }

  public IReadOnlyList<AutomergeConflictValue> MapRange(AutomergeObjectId mapId, string startKey, string endKey)
  {
    ThrowIfDisposed();

    var target = ToNativeObjectId(mapId);
    try
    {
      using var start = ToUtf8(startKey, nameof(startKey));
      using var end = ToUtf8(endKey, nameof(endKey));
      var vec = _doc.ObjMapRange(ref target, start, end);
      return ConvertConflictValues(vec);
    }
    finally
    {
      target.Dispose();
    }
  }

  public IReadOnlyList<AutomergeConflictValue> MapRangeFull(AutomergeObjectId mapId)
  {
    ThrowIfDisposed();

    var target = ToNativeObjectId(mapId);
    try
    {
      var vec = _doc.ObjMapRangeFull(ref target);
      return ConvertConflictValues(vec);
    }
    finally
    {
      target.Dispose();
    }
  }

  public IReadOnlyList<AutomergeConflictValue> ListRange(AutomergeObjectId listId, ulong startIndex, ulong endIndex)
  {
    ThrowIfDisposed();

    var target = ToNativeObjectId(listId);
    try
    {
      var vec = _doc.ObjListRange(ref target, startIndex, endIndex);
      return ConvertConflictValues(vec);
    }
    finally
    {
      target.Dispose();
    }
  }

  public IReadOnlyList<AutomergeConflictValue> ListRangeFull(AutomergeObjectId listId)
  {
    ThrowIfDisposed();

    var target = ToNativeObjectId(listId);
    try
    {
      var vec = _doc.ObjListRangeFull(ref target);
      return ConvertConflictValues(vec);
    }
    finally
    {
      target.Dispose();
    }
  }

  public IReadOnlyList<AutomergePathElement> Parents(AutomergeObjectId objectId)
  {
    ThrowIfDisposed();

    var target = ToNativeObjectId(objectId);
    try
    {
      var vec = _doc.ObjParents(ref target);
      return ConvertPathElements(vec);
    }
    finally
    {
      target.Dispose();
    }
  }

  public IReadOnlyList<AutomergePathElement> PathToObject(AutomergeObjectId objectId)
  {
    ThrowIfDisposed();

    var target = ToNativeObjectId(objectId);
    try
    {
      var vec = _doc.ObjPathToObject(ref target);
      return ConvertPathElements(vec);
    }
    finally
    {
      target.Dispose();
    }
  }

  public string HydrateJson()
  {
    ThrowIfDisposed();
    var json = _doc.HydrateJson();
    return json.AsManagedString();
  }

  public string HydrateJsonAtHeads(IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();

    var hashes = heads.Select(ParseHash).ToArray();
    using var slice = Native.SliceChangeHashBytes.From(hashes);
    var json = _doc.HydrateJsonAtHeads(slice);
    return json.AsManagedString();
  }

  /// <summary>
  /// Gets detailed metadata about a specific change by its hash.
  /// </summary>
  /// <param name="hash">The change hash as a hex string.</param>
  /// <returns>
  /// A <see cref="DecodedChange"/> containing the change metadata, or null if the change hash doesn't exist.
  /// </returns>
  /// <exception cref="ArgumentException">Thrown when the hash format is invalid.</exception>
  /// <exception cref="ObjectDisposedException">Thrown when the document has been disposed.</exception>
  public DecodedChange? GetChangeMetadata(string hash)
  {
    if(string.IsNullOrWhiteSpace(hash))
    {
      throw new ArgumentException("Change hash cannot be null or whitespace.", nameof(hash));
    }

    ThrowIfDisposed();

    var changeHash = ParseHash(hash);
    var result = _doc.GetChangeMetadata(changeHash);

    if(result.IsNone)
    {
      return null;
    }

    var native = result.AsSome();
    try
    {
      return ConvertDecodedChange(native);
    }
    finally
    {
      native.Dispose();
    }
  }

  static DecodedChange ConvertDecodedChange(Native.DecodedChange native)
  {
    var hash = ConvertHashToHex(native.hash);
    var actor = ConvertString(native.actor, disposeUtf8: false);
    var sequence = native.seq;
    var timestamp = native.timestamp;
    var message = ConvertOptionalString(native.message, disposeOption: false);
    var deps = ConvertDependencies(native.deps);

    return new DecodedChange(hash, actor, sequence, timestamp, message, deps);
  }

  static IReadOnlyList<string> ConvertDependencies(Native.VecChangeHashBytes deps)
  {
    var count = deps.Count;
    if(count == 0)
    {
      return Array.Empty<string>();
    }

    var list = new List<string>(count);
    for(var i = 0; i < count; i++)
    {
      var hash = deps[i];
      list.Add(ConvertHashToHex(hash));
    }

    return list;
  }

  public byte[]? GetLastLocalChange()
  {
    ThrowIfDisposed();
    var change = _doc.GetLastLocalChange();
    return change.AsManagedBytes();
  }

  public IReadOnlyList<string> GetHeads()
  {
    ThrowIfDisposed();
    using var heads = _doc.GetHeads();
    var hashes = heads.AsHashList();
    return hashes
      .Select(hash => Convert.ToHexString(hash.AsManagedBytes()).ToLowerInvariant())
      .ToArray();
  }

  public IReadOnlyList<byte[]> GetChanges(IEnumerable<string> knownHeads)
  {
    ThrowIfDisposed();
    var hashes = knownHeads?.Select(ParseHash).ToArray() ?? Array.Empty<Native.ChangeHashBytes>();
    using var slice = Native.SliceChangeHashBytes.From(hashes);
    using var vec = _doc.GetChanges(slice);
    return vec.AsByteArrayList();
  }

  public IReadOnlyList<AutomergePatch> Diff(IEnumerable<string> beforeHeads, IEnumerable<string> afterHeads)
  {
    if(beforeHeads is null)
    {
      throw new ArgumentNullException(nameof(beforeHeads));
    }

    if(afterHeads is null)
    {
      throw new ArgumentNullException(nameof(afterHeads));
    }

    ThrowIfDisposed();

    var before = beforeHeads.Select(ParseHash).ToArray();
    var after = afterHeads.Select(ParseHash).ToArray();

    using var beforeSlice = Native.SliceChangeHashBytes.From(before);
    using var afterSlice = Native.SliceChangeHashBytes.From(after);
    using var nativePatches = _doc.Diff(beforeSlice, afterSlice);

    var patches = new List<AutomergePatch>(nativePatches.Count);
    for(var i = 0; i < nativePatches.Count; i++)
    {
      var nativePatch = nativePatches[i];
      patches.Add(ConvertPatch(nativePatch));
    }

    return patches;
  }

  public IReadOnlyList<string> TopoHistoryTraversal()
  {
    ThrowIfDisposed();

    using var vec = _doc.TopoHistoryTraversal();
    var results = new List<string>(vec.Count);
    for(var i = 0; i < vec.Count; i++)
    {
      var hash = vec[i];
      results.Add(ConvertHashToHex(hash));
    }

    return results;
  }

  public void Isolate(IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();
    var array = heads.Select(ParseHash).ToArray();
    using var slice = Native.SliceChangeHashBytes.From(array);
    _doc.Isolate(slice);
  }

  public void Integrate()
  {
    ThrowIfDisposed();
    _doc.Integrate();
  }

  public AutomergeDocument Clone()
  {
    ThrowIfDisposed();
    var data = Save();
    return Load(data);
  }

  public AutomergeDocument ViewAtHeads(IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();

    // Create a clone for the view. The view will read at specific heads.
    // Note: We still need to clone because we can't create a "view-only" document
    // that shares the underlying data. The key difference from before is that
    // we use HydrateJsonAtHeads() instead of Isolate() for reading.
    var clone = Clone();
    // Store the heads in the clone so it can use them for reading
    // For now, since we don't have a native "view" type, we use isolate
    // but callers should use HydrateJsonAtHeads directly
    clone.Isolate(heads);
    return clone;
  }

  /// <summary>
  /// Creates a fork of this document at the specified heads.
  /// </summary>
  /// <param name="heads">The heads to fork at.</param>
  /// <returns>A new <see cref="AutomergeDocument"/> forked at the specified heads.</returns>
  /// <remarks>
  /// <para>
  /// This method creates an independent copy of the document as it existed at the specified heads.
  /// Changes made to the forked document can later be merged back into the original document to
  /// create concurrent branches.
  /// </para>
  /// <para>
  /// This is useful for implementing the <c>changeAt</c> operation where you want to make changes
  /// as if the document were at a specific point in history, creating a concurrent branch.
  /// </para>
  /// <example>
  /// <code>
  /// var heads1 = doc.GetHeads();
  /// doc.Change(d => d.PutString(d.RootId, "key", "value1"));
  ///
  /// // Fork at heads1 and make a concurrent change
  /// using var fork = doc.ForkAtHeads(heads1);
  /// fork.Change(d => d.PutString(d.RootId, "key", "value2"));
  /// fork.Commit("Concurrent change");
  ///
  /// // Merge the fork back to create concurrent branches
  /// doc.Merge(fork);
  /// </code>
  /// </example>
  /// </remarks>
  /// <exception cref="ArgumentNullException">Thrown when heads is null.</exception>
  /// <exception cref="ArgumentException">Thrown when heads is empty or contains invalid hashes.</exception>
  /// <exception cref="ObjectDisposedException">Thrown when the document has been disposed.</exception>
  public AutomergeDocument ForkAtHeads(IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();

    var headArray = heads.ToArray();
    if(headArray.Length == 0)
    {
      throw new ArgumentException("Heads cannot be empty.", nameof(heads));
    }

    // NOTE: The native fork_at_heads FFI method exists, but the generated C# wrapper
    // makes it difficult to call with the private _context field. As a workaround, we use
    // Clone + mutations to achieve the fork-at-heads behavior.
    // The key insight: we clone the document, then use the Rust `fork_at` indirectly
    // by saving incremental changes since the target heads.
    //
    // For now, we'll use a simpler but less efficient approach: Clone and then manually
    // create a new document that represents the state at those heads.
    // TODO: Improve FFI wrapper generation or add a public Handle property to make fork_at_heads accessible

    var forked = Clone();
    // Isolate to the target heads - this makes the fork show state at those heads
    forked.Isolate(heads);
    return forked;
  }

  static string ConvertHashToHex(Native.ChangeHashBytes hash) =>
    Convert.ToHexString(hash.AsManagedBytes()).ToLowerInvariant();

  static AutomergePatch ConvertPatch(Native.AutomergePatch native)
  {
    var managedObjectId = ConvertObjectId(native.object_id, dispose: false);

    var managedPath = ConvertPath(native.path);

    var managedAction = ConvertAction(native.action);

    return new AutomergePatch(managedObjectId, managedPath, managedAction);
  }

  static IReadOnlyList<AutomergePatchPathElement> ConvertPath(Native.VecAutomergePatchPathElement native)
  {
    var count = native.Count;
    if(count == 0)
    {
      return Array.Empty<AutomergePatchPathElement>();
    }

    var elements = new List<AutomergePatchPathElement>(count);
    for(var i = 0; i < count; i++)
    {
      var element = native[i];
      elements.Add(ConvertPathElement(element));
    }

    return elements;
  }

  static AutomergePatchPathElement ConvertPathElement(Native.AutomergePatchPathElement native)
  {
    var managedObjectId = ConvertObjectId(native.object_id, dispose: false);

    var managedProp = ConvertProp(native.prop, dispose: false);

    return new AutomergePatchPathElement(managedObjectId, managedProp);
  }

  static AutomergePatchAction ConvertAction(Native.AutomergePatchAction native)
  {
    var kind = ConvertActionKind(native.kind);
    var key = ConvertOptionalString(native.key, disposeOption: false);
    var value = ConvertOptionalValue(native.value, disposeOption: false);
    var valueObjectId = ConvertOptionalObjectId(native.value_object_id, disposeOption: false);
    var conflict = native.conflict;
    var inserted = ConvertInsertEntries(native.inserted);
    var text = ConvertOptionalString(native.text, disposeOption: false);
    var activeMarks = ConvertMarkData(native.active_marks);
    var marks = ConvertMarkSpans(native.marks);
    var prop = ConvertOptionalProp(native.prop, disposeOption: false);
    var length = native.length;
    var incrementValue = native.increment_value;

    return new AutomergePatchAction(
      kind,
      key,
      native.index,
      value,
      valueObjectId,
      conflict,
      inserted,
      text,
      activeMarks,
      marks,
      prop,
      length,
      incrementValue);
  }

  static AutomergePatchActionKind ConvertActionKind(Native.AutomergePatchActionKind kind)
  {
    if(kind.IsPutMap)
      return AutomergePatchActionKind.PutMap;
    if(kind.IsPutSeq)
      return AutomergePatchActionKind.PutSeq;
    if(kind.IsInsert)
      return AutomergePatchActionKind.Insert;
    if(kind.IsSpliceText)
      return AutomergePatchActionKind.SpliceText;
    if(kind.IsIncrement)
      return AutomergePatchActionKind.Increment;
    if(kind.IsConflict)
      return AutomergePatchActionKind.Conflict;
    if(kind.IsDeleteMap)
      return AutomergePatchActionKind.DeleteMap;
    if(kind.IsDeleteSeq)
      return AutomergePatchActionKind.DeleteSeq;
    if(kind.IsMark)
      return AutomergePatchActionKind.Mark;

    throw new ArgumentOutOfRangeException(nameof(kind), kind.ToString(), "Unknown patch action kind.");
  }

  static IReadOnlyList<AutomergeInsertEntry> ConvertInsertEntries(Native.VecAutomergeInsertEntry native)
  {
    var count = native.Count;
    if(count == 0)
    {
      return Array.Empty<AutomergeInsertEntry>();
    }

    var list = new List<AutomergeInsertEntry>(count);
    for(var i = 0; i < count; i++)
    {
      var entry = native[i];
      list.Add(ConvertInsertEntry(entry));
    }

    return list;
  }

  static AutomergeInsertEntry ConvertInsertEntry(Native.AutomergeInsertEntry native)
  {
    var managedValue = ConvertValue(native.value, dispose: false);

    var managedObjectId = ConvertObjectId(native.object_id, dispose: false);

    return new AutomergeInsertEntry(managedValue, managedObjectId, native.conflict);
  }

  static IReadOnlyList<AutomergeMarkData> ConvertMarkData(Native.VecAutomergeMarkData native)
  {
    var count = native.Count;
    if(count == 0)
    {
      return Array.Empty<AutomergeMarkData>();
    }

    var list = new List<AutomergeMarkData>(count);
    for(var i = 0; i < count; i++)
    {
      var mark = native[i];
      list.Add(ConvertMarkData(mark));
    }

    return list;
  }

  static AutomergeMarkData ConvertMarkData(Native.AutomergeMarkData native)
  {
    var name = ConvertString(native.name, disposeUtf8: false);
    var scalar = ConvertScalar(native.value, dispose: false);
    return new AutomergeMarkData(name, scalar);
  }

  static IReadOnlyList<AutomergeMarkSpan> ConvertMarkSpans(Native.VecAutomergeMarkSpan native)
  {
    var count = native.Count;
    if(count == 0)
    {
      return Array.Empty<AutomergeMarkSpan>();
    }

    var list = new List<AutomergeMarkSpan>(count);
    for(var i = 0; i < count; i++)
    {
      var mark = native[i];
      list.Add(ConvertMarkSpan(mark));
    }

    return list;
  }

  static AutomergeMarkSpan ConvertMarkSpan(Native.AutomergeMarkSpan native)
  {
    var name = ConvertString(native.name, disposeUtf8: false);
    var scalar = ConvertScalar(native.value, dispose: false);
    return new AutomergeMarkSpan(native.start, native.end, name, scalar);
  }

  static IReadOnlyList<AutomergeMarkInfo> ConvertMarkInfos(Native.VecAutomergeMarkSpan vec)
  {
    var count = vec.Count;
    if(count == 0)
    {
      return Array.Empty<AutomergeMarkInfo>();
    }

    var list = new List<AutomergeMarkInfo>(count);
    for(var i = 0; i < count; i++)
    {
      var native = vec[i];
      var name = ConvertString(native.name, disposeUtf8: false);
      var scalar = ConvertScalar(native.value, dispose: false);
      list.Add(new AutomergeMarkInfo(name, scalar, native.start, native.end));
    }

    return list;
  }

  static IReadOnlyList<AutomergeSpan> ConvertSpans(Native.VecAutomergeSpan vec)
  {
    var count = vec.Count;
    if(count == 0)
    {
      return Array.Empty<AutomergeSpan>();
    }

    var list = new List<AutomergeSpan>(count);
    for(var i = 0; i < count; i++)
    {
      var native = vec[i];
      if(native.kind.IsText)
      {
        var text = ConvertString(native.text, disposeUtf8: false);
        var marks = ConvertSpanMarks(native.marks);
        list.Add(new AutomergeTextSpan(text, marks));
      }
      else
      {
        var blockType = ConvertString(native.block_type, disposeUtf8: false);
        var parents = ConvertStringVec(native.block_parents);
        var attrsJson = ConvertString(native.block_attrs_json, disposeUtf8: false);
        list.Add(new AutomergeBlockSpan(blockType, parents, attrsJson));
      }
    }

    return list;
  }

  static IReadOnlyList<AutomergeMarkInfo> ConvertSpanMarks(Native.VecAutomergeMarkData marks)
  {
    var count = marks.Count;
    if(count == 0)
    {
      return Array.Empty<AutomergeMarkInfo>();
    }

    var list = new List<AutomergeMarkInfo>(count);
    for(var i = 0; i < count; i++)
    {
      var mark = marks[i];
      var name = ConvertString(mark.name, disposeUtf8: false);
      var scalar = ConvertScalar(mark.value, dispose: false);
      list.Add(new AutomergeMarkInfo(name, scalar, 0, 0));
    }

    return list;
  }

  static IReadOnlyList<AutomergePathElement> ConvertPathElements(Native.VecAutomergePathElement vec)
  {
    var count = vec.Count;
    if(count == 0)
    {
      return Array.Empty<AutomergePathElement>();
    }

    var list = new List<AutomergePathElement>(count);
    for(var i = 0; i < count; i++)
    {
      var native = vec[i];
      var objId = ConvertObjectId(native.obj_id, dispose: false);
      var prop = ConvertProp(native.prop, dispose: false);
      AutomergePathElement element = prop.Kind == AutomergePropKind.Map
        ? new AutomergeMapPathElement(objId, prop.Key!)
        : new AutomergeListPathElement(objId, prop.Index);
      list.Add(element);
    }

    return list;
  }

  static IReadOnlyList<string> ConvertStringVec(Native.VecUtf8String vec)
  {
    var count = vec.Count;
    if(count == 0)
    {
      return Array.Empty<string>();
    }

    var list = new List<string>(count);
    for(var i = 0; i < count; i++)
    {
      var utf8 = vec[i];
      list.Add(utf8.String);
    }

    return list;
  }

  static AutomergeProp? ConvertOptionalProp(Native.OptionAutomergeProp option, bool disposeOption = true)
  {
    if(!option.IsSome)
    {
      if(disposeOption)
      {
        option.Dispose();
      }
      return null;
    }

    var prop = option.AsSome();
    var converted = ConvertProp(prop, dispose: disposeOption);
    if(disposeOption)
    {
      option.Dispose();
    }

    return converted;
  }

  static AutomergeProp ConvertProp(Native.AutomergeProp native, bool dispose = true)
  {
    var kind = ConvertPropKind(native.kind);
    var key = ConvertOptionalString(native.key, disposeOption: dispose);
    if(dispose)
    {
      native.key.Dispose();
    }

    return new AutomergeProp(kind, key, native.index);
  }

  static AutomergePropKind ConvertPropKind(Native.AutomergePropKind kind)
  {
    if(kind.IsMap)
      return AutomergePropKind.Map;
    if(kind.IsSeq)
      return AutomergePropKind.Seq;

    throw new ArgumentOutOfRangeException(nameof(kind), kind.ToString(), "Unknown property kind.");
  }

  static IReadOnlyList<AutomergeConflictValue> ConvertConflictValues(Native.VecAutomergeConflictValue vec)
  {
    var count = vec.Count;
    if(count == 0)
    {
      return Array.Empty<AutomergeConflictValue>();
    }

    var list = new List<AutomergeConflictValue>(count);
    for(var i = 0; i < count; i++)
    {
      var native = vec[i];
      list.Add(ConvertConflictValue(native));
    }

    return list;
  }

  static AutomergeConflictValue ConvertConflictValue(Native.AutomergeConflictValue native)
  {
    var isObject = native.is_object;
    AutomergeScalarValue? scalar = null;
    AutomergeObjectId? objectId = null;
    AutomergeContainerType? objectType = null;

    if(isObject)
    {
      objectId = ConvertObjectId(native.obj_id, dispose: false);
      objectType = ConvertObjectType(native.obj_type);
    }
    else
    {
      scalar = ConvertScalar(native.scalar, dispose: false);
    }

    var opId = ConvertString(native.op_id, disposeUtf8: false);

    return new AutomergeConflictValue(isObject, scalar, objectId, objectType, opId);
  }

  static AutomergeObjectId? ConvertOptionalObjectId(Native.OptionAutomergeObjId option, bool disposeOption = true)
  {
    if(!option.IsSome)
    {
      if(disposeOption)
      {
        option.Dispose();
      }
      return null;
    }

    var objectId = option.AsSome();
    var converted = ConvertObjectId(objectId, dispose: disposeOption);
    if(disposeOption)
    {
      option.Dispose();
    }

    return converted;
  }

  static AutomergeObjectId ConvertObjectId(Native.AutomergeObjId native, bool dispose = true)
  {
    var bytes = CopyVec(native.bytes, dispose);
    if(dispose)
    {
      native.Dispose();
    }

    return AutomergeObjectId.FromBytes(bytes);
  }

  static AutomergeValue? ConvertOptionalValue(Native.OptionAutomergeValue option, bool disposeOption = true)
  {
    if(!option.IsSome)
    {
      if(disposeOption)
      {
        option.Dispose();
      }
      return null;
    }

    var value = option.AsSome();
    var converted = ConvertValue(value, dispose: disposeOption);
    if(disposeOption)
    {
      option.Dispose();
    }

    return converted;
  }

  static AutomergeValue ConvertValue(Native.AutomergeValue native, bool dispose = true)
  {
    var type = ConvertValueType(native.value_type);
    AutomergeScalarValue? scalar = null;
    if(type == AutomergeValueType.Scalar && native.scalar.IsSome)
    {
      var scalarValue = native.scalar.AsSome();
      scalar = ConvertScalar(scalarValue, dispose);
      if(dispose)
      {
        scalarValue.Dispose();
      }
    }

    AutomergeContainerType? objectType = null;
    if(type == AutomergeValueType.Object && native.object_type.IsSome)
    {
      var objectTypeValue = native.object_type.AsSome();
      objectType = ConvertObjectType(objectTypeValue);
    }

    if(dispose)
    {
      native.scalar.Dispose();
    }

    return new AutomergeValue(type, scalar, objectType);
  }

  static AutomergeValueType ConvertValueType(Native.AutomergeValueType type)
  {
    if(type.IsScalar)
      return AutomergeValueType.Scalar;
    if(type.IsObject)
      return AutomergeValueType.Object;

    throw new ArgumentOutOfRangeException(nameof(type), type.ToString(), "Unknown Automerge value type.");
  }

  static AutomergeContainerType ConvertObjectType(Native.AutomergeObjectType type)
  {
    if(type.IsMap)
      return AutomergeContainerType.Map;
    if(type.IsList)
      return AutomergeContainerType.List;
    if(type.IsText)
      return AutomergeContainerType.Text;

    throw new ArgumentOutOfRangeException(nameof(type), type.ToString(), "Unknown Automerge object type.");
  }

  static AutomergeScalarValue ConvertScalar(Native.AutomergeScalarValue native, bool dispose = true)
  {
    var kind = ConvertScalarKind(native.value_type);
    string? stringValue = null;
    bool? boolValue = null;
    long? intValue = null;
    ulong? uintValue = null;
    double? doubleValue = null;
    byte[]? bytesValue = null;
    long? counterValue = null;
    long? timestampValue = null;
    byte? unknownTypeCode = null;
    byte[]? unknownBytes = null;

    switch(kind)
    {
      case AutomergeScalarValueKind.String:
        stringValue = ConvertOptionalString(native.string_value, dispose);
        break;
      case AutomergeScalarValueKind.Boolean:
        boolValue = native.bool_value;
        break;
      case AutomergeScalarValueKind.Int:
        intValue = native.int_value;
        break;
      case AutomergeScalarValueKind.Uint:
        uintValue = native.uint_value;
        break;
      case AutomergeScalarValueKind.Double:
        doubleValue = native.f64_value;
        break;
      case AutomergeScalarValueKind.Bytes:
        bytesValue = CopyVec(native.bytes_value, dispose);
        break;
      case AutomergeScalarValueKind.Counter:
        counterValue = native.counter_value;
        break;
      case AutomergeScalarValueKind.Timestamp:
        timestampValue = native.timestamp_value;
        break;
      case AutomergeScalarValueKind.Unknown:
        unknownTypeCode = native.unknown_type_code;
        unknownBytes = CopyVec(native.unknown_bytes, dispose);
        break;
    }

    var managed = new AutomergeScalarValue(
      kind,
      stringValue,
      boolValue,
      intValue,
      uintValue,
      doubleValue,
      bytesValue,
      counterValue,
      timestampValue,
      unknownTypeCode,
      unknownBytes);

    if(dispose)
    {
      native.Dispose();
    }

    return managed;
  }

  static AutomergeScalarValueKind ConvertScalarKind(Native.AutomergeScalarType kind)
  {
    if(kind.IsBytes)
      return AutomergeScalarValueKind.Bytes;
    if(kind.IsString)
      return AutomergeScalarValueKind.String;
    if(kind.IsInt)
      return AutomergeScalarValueKind.Int;
    if(kind.IsUint)
      return AutomergeScalarValueKind.Uint;
    if(kind.IsF64)
      return AutomergeScalarValueKind.Double;
    if(kind.IsCounter)
      return AutomergeScalarValueKind.Counter;
    if(kind.IsTimestamp)
      return AutomergeScalarValueKind.Timestamp;
    if(kind.IsBoolean)
      return AutomergeScalarValueKind.Boolean;
    if(kind.IsNull)
      return AutomergeScalarValueKind.Null;
    if(kind.IsUnknown)
      return AutomergeScalarValueKind.Unknown;

    throw new ArgumentOutOfRangeException(nameof(kind), kind.ToString(), "Unknown scalar value kind.");
  }

  static string? ConvertOptionalString(Native.OptionUtf8String option, bool disposeOption = true)
  {
    if(!option.IsSome)
    {
      if(disposeOption)
      {
        option.Dispose();
      }
      return null;
    }

    var utf8 = option.AsSome();
    var value = utf8.String;
    if(disposeOption)
    {
      utf8.Dispose();
      option.Dispose();
    }

    return value;
  }

  static string ConvertString(Native.Utf8String utf8, bool disposeUtf8 = true)
  {
    var value = utf8.String;
    if(disposeUtf8)
    {
      utf8.Dispose();
    }

    return value;
  }

  static byte[] CopyVec(Native.VecU8 vec, bool disposeVec = true)
  {
    var count = vec.Count;
    if(count == 0)
    {
      if(disposeVec)
      {
        vec.Dispose();
      }

      return Array.Empty<byte>();
    }

    var buffer = new byte[count];
    for(var i = 0; i < count; i++)
    {
      buffer[i] = vec[i];
    }

    if(disposeVec)
    {
      vec.Dispose();
    }

    return buffer;
  }

  public void ApplyChange(ReadOnlySpan<byte> change)
  {
    ThrowIfDisposed();
    using var native = CreateChangeBytes(change);
    _doc.ApplyChange(native);
  }

  public void ApplyChanges(IEnumerable<byte[]> changes)
  {
    ThrowIfDisposed();
    var changeArray = changes?.Select(change => CreateChangeBytes(change)).ToArray() ??
                      Array.Empty<Native.ChangeBytes>();

    try
    {
      using var slice = Native.SliceChangeBytes.From(changeArray);
      _doc.ApplyChanges(slice);
    }
    finally
    {
      foreach(var change in changeArray)
      {
        change.Dispose();
      }
    }
  }

  public IReadOnlyList<string> Merge(AutomergeDocument other)
  {
    if(other is null)
      throw new ArgumentNullException(nameof(other));

    ThrowIfDisposed();
    using var merged = _doc.Merge(other._doc.Context);
    var hashes = merged.AsHashList();
    
    return hashes
      .Select(hash => Convert.ToHexString(hash.AsManagedBytes()).ToLowerInvariant())
      .ToArray();
  }

  public AutomergeSyncState CreateSyncState() => AutomergeSyncState.Create();

  public byte[]? GenerateSyncMessage(AutomergeSyncState state)
  {
    if(state is null)
    {
      throw new ArgumentNullException(nameof(state));
    }

    ThrowIfDisposed();
    var option = _doc.SyncGenerateMessage(state.Context);
    return option.AsManagedBytes();
  }

  public void ReceiveSyncMessage(AutomergeSyncState state, ReadOnlySpan<byte> message)
  {
    if(state is null)
    {
      throw new ArgumentNullException(nameof(state));
    }

    ThrowIfDisposed();
    using var bytes = CreateByteArray(message);
    _doc.SyncReceiveMessage(state.Context, bytes);
  }

  [PublicAPI]
  public IReadOnlyList<byte[]> GetChangesAdded(AutomergeDocument other)
  {
    if(other is null)
    {
      throw new ArgumentNullException(nameof(other));
    }

    ThrowIfDisposed();
    using var vec = _doc.GetChangesAdded(other._doc.Context);
    return vec.AsByteArrayList();
  }

  [PublicAPI]
  public byte[] Bundle(IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();
    var hashes = heads.Select(ParseHash).ToArray();
    using var slice = Native.SliceChangeHashBytes.From(hashes);
    using var bytes = _doc.Bundle(slice);
    return bytes.AsManagedBytes();
  }

  [PublicAPI]
  public IReadOnlyList<AutomergePatch> DiffIncremental()
  {
    ThrowIfDisposed();
    using var nativePatches = _doc.DiffIncremental();

    var patches = new List<AutomergePatch>(nativePatches.Count);
    for(var i = 0; i < nativePatches.Count; i++)
    {
      var nativePatch = nativePatches[i];
      patches.Add(ConvertPatch(nativePatch));
    }

    return patches;
  }

  [PublicAPI]
  public IReadOnlyList<string> GetMissingDeps(IEnumerable<string> heads)
  {
    if(heads is null)
    {
      throw new ArgumentNullException(nameof(heads));
    }

    ThrowIfDisposed();
    var hashes = heads.Select(ParseHash).ToArray();
    using var slice = Native.SliceChangeHashBytes.From(hashes);
    using var vec = _doc.GetMissingDeps(slice);

    var results = new List<string>(vec.Count);
    for(var i = 0; i < vec.Count; i++)
    {
      var hash = vec[i];
      results.Add(ConvertHashToHex(hash));
    }

    return results;
  }

  [PublicAPI]
  public bool HasOurChanges(AutomergeSyncState state)
  {
    if(state is null)
    {
      throw new ArgumentNullException(nameof(state));
    }

    ThrowIfDisposed();
    return _doc.HasOurChanges(state.Context);
  }

  static Native.AutomergeObjId ToNativeObjectId(AutomergeObjectId objectId)
  {
    if(objectId is null)
    {
      throw new ArgumentNullException(nameof(objectId));
    }

    return objectId.ToNative();
  }

  static Native.Utf8String ToUtf8(string value, string parameterName)
  {
    if(value is null)
    {
      throw new ArgumentNullException(parameterName);
    }

    return Native.Utf8String.From(value);
  }

  static Native.SliceU8 CreateSlice(ReadOnlySpan<byte> data)
  {
    var buffer = data.ToArray();
    return Native.SliceU8.From(buffer);
  }

  static Native.ChangeBytes CreateChangeBytes(ReadOnlySpan<byte> data)
  {
    var change = new Native.ChangeBytes { bytes = CreateVec(data) };

    return change;
  }

  static Native.ByteArray CreateByteArray(ReadOnlySpan<byte> data)
  {
    var byteArray = new Native.ByteArray { data = CreateVec(data) };

    return byteArray;
  }

  static Native.VecU8 CreateVec(ReadOnlySpan<byte> data)
  {
    if(data.IsEmpty)
    {
      return Native.VecU8.Empty();
    }

    byte[] buffer = data.ToArray();
    return Native.VecU8.From(buffer.AsSpan());
  }

  static Native.AutomergeScalarValue CreateStringScalar(string text)
  {
    var scalar = new Native.AutomergeScalarValue
    {
      value_type = Native.AutomergeScalarType.String,
      string_value = Native.OptionUtf8String.Some(Native.Utf8String.From(text)),
      bool_value = false,
      int_value = 0,
      uint_value = 0,
      f64_value = 0,
      bytes_value = Native.VecU8.Empty(),
      counter_value = 0,
      timestamp_value = 0,
      unknown_type_code = 0,
      unknown_bytes = Native.VecU8.Empty()
    };
    return scalar;
  }

  static Native.AutomergeScalarValue CreateBoolScalar(bool flag)
  {
    var scalar = new Native.AutomergeScalarValue
    {
      value_type = Native.AutomergeScalarType.Boolean,
      string_value = Native.OptionUtf8String.None,
      bool_value = flag,
      int_value = 0,
      uint_value = 0,
      f64_value = 0,
      bytes_value = Native.VecU8.Empty(),
      counter_value = 0,
      timestamp_value = 0,
      unknown_type_code = 0,
      unknown_bytes = Native.VecU8.Empty()
    };
    return scalar;
  }

  static Native.AutomergeScalarValue CreateIntScalar(long number)
  {
    var scalar = new Native.AutomergeScalarValue
    {
      value_type = Native.AutomergeScalarType.Int,
      string_value = Native.OptionUtf8String.None,
      bool_value = false,
      int_value = number,
      uint_value = 0,
      f64_value = 0,
      bytes_value = Native.VecU8.Empty(),
      counter_value = 0,
      timestamp_value = 0,
      unknown_type_code = 0,
      unknown_bytes = Native.VecU8.Empty()
    };
    return scalar;
  }

  static Native.AutomergeScalarValue CreateNativeScalar(AutomergeScalarValue managed)
  {
    return managed.Kind switch
    {
      AutomergeScalarValueKind.String => CreateStringScalar(managed.StringValue ?? string.Empty),
      AutomergeScalarValueKind.Boolean => CreateBoolScalar(managed.BoolValue ?? false),
      AutomergeScalarValueKind.Int => CreateIntScalar(managed.IntValue ?? 0),
      AutomergeScalarValueKind.Uint => new Native.AutomergeScalarValue
      {
        value_type = Native.AutomergeScalarType.Uint,
        string_value = Native.OptionUtf8String.None,
        bool_value = false,
        int_value = 0,
        uint_value = managed.UIntValue ?? 0,
        f64_value = 0,
        bytes_value = Native.VecU8.Empty(),
        counter_value = 0,
        timestamp_value = 0,
        unknown_type_code = 0,
        unknown_bytes = Native.VecU8.Empty()
      },
      AutomergeScalarValueKind.Double => new Native.AutomergeScalarValue
      {
        value_type = Native.AutomergeScalarType.F64,
        string_value = Native.OptionUtf8String.None,
        bool_value = false,
        int_value = 0,
        uint_value = 0,
        f64_value = managed.DoubleValue ?? 0,
        bytes_value = Native.VecU8.Empty(),
        counter_value = 0,
        timestamp_value = 0,
        unknown_type_code = 0,
        unknown_bytes = Native.VecU8.Empty()
      },
      AutomergeScalarValueKind.Bytes => new Native.AutomergeScalarValue
      {
        value_type = Native.AutomergeScalarType.Bytes,
        string_value = Native.OptionUtf8String.None,
        bool_value = false,
        int_value = 0,
        uint_value = 0,
        f64_value = 0,
        bytes_value = managed.BytesValue is { Length: > 0 }
          ? Native.VecU8.From(managed.BytesValue.AsSpan())
          : Native.VecU8.Empty(),
        counter_value = 0,
        timestamp_value = 0,
        unknown_type_code = 0,
        unknown_bytes = Native.VecU8.Empty()
      },
      AutomergeScalarValueKind.Null => CreateNullScalar(),
      _ => CreateNullScalar()
    };
  }

  static Native.ChangeHashBytes ParseHash(string hex)
  {
    if(string.IsNullOrWhiteSpace(hex))
    {
      throw new ArgumentException("Hash cannot be null or whitespace.", nameof(hex));
    }

    byte[] bytes;
    try
    {
      bytes = Convert.FromHexString(hex);
    }
    catch(FormatException ex)
    {
      throw new ArgumentException("Hash must contain exactly 32 bytes encoded as 64 hexadecimal characters.",
        nameof(hex), ex);
    }

    if(bytes.Length != 32)
    {
      throw new ArgumentException("Hash must contain exactly 32 bytes encoded as 64 hexadecimal characters.",
        nameof(hex));
    }

    return BuildChangeHash(bytes);
  }

  static Native.ChangeHashBytes BuildChangeHash(ReadOnlySpan<byte> bytes)
  {
    if(bytes.Length != 32)
    {
      throw new ArgumentException("Change hash must contain exactly 32 bytes.", nameof(bytes));
    }

    return new Native.ChangeHashBytes
    {
      part1 = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(0, 8)),
      part2 = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(8, 8)),
      part3 = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(16, 8)),
      part4 = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(24, 8))
    };
  }
}
