using Six.Automerge.FFI;

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for error handling and edge cases.
/// </summary>
[Trait("Category", "ErrorHandling")]
public sealed class AutomergeErrorHandlingTests
{
    [Fact]
    public void OperationsOnDisposedDocument_ShouldThrow()
    {
        var doc = AutomergeDocument.Create();
        var root = doc.RootId;
        doc.Dispose();

        Assert.Throws<ObjectDisposedException>(() => doc.PutString(root, "key", "value"));
    }

    [Fact]
    public void EmptyKey_ShouldWork()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        doc.PutString(root, "", "empty key");
        doc.Commit("empty");

        var keys = doc.GetKeys(root);
        Assert.Contains("", keys);
    }

    [Fact]
    public void SpecialCharacterKeys_ShouldWork()
    {
        using var doc = AutomergeDocument.Create();
        var root = doc.RootId;

        doc.PutString(root, "key with spaces", "value1");
        doc.PutString(root, "key.with.dots", "value2");
        doc.PutString(root, "key/with/slashes", "value3");
        doc.PutString(root, "key-with-unicode-→", "value4");
        doc.Commit("special");

        var keys = doc.GetKeys(root);
        Assert.Equal(4, keys.Count);
        Assert.Contains("key with spaces", keys);
        Assert.Contains("key.with.dots", keys);
        Assert.Contains("key/with/slashes", keys);
        Assert.Contains("key-with-unicode-→", keys);
    }

    [Fact]
    public void LoadInvalidData_ShouldThrow()
    {
        var invalidBytes = new byte[] { 0xFF, 0xFF, 0xFF };
        var ex = Assert.Throws<AutomergeException>(() => AutomergeDocument.Load(invalidBytes));
        Assert.NotNull(ex.Message);
    }

    [Fact]
    public void LoadEmptyData_ShouldThrow()
    {
        var emptyBytes = Array.Empty<byte>();
        var ex = Assert.Throws<ArgumentException>(() => AutomergeDocument.Load(emptyBytes));
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
