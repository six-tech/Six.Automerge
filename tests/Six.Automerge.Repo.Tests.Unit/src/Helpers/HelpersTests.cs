using Six.Automerge.Repository.Helpers;
using Xunit;

namespace Six.Automerge.Repo.Test.Unit.Helpers;

[Trait("Category", "Helpers")]
public sealed class ThrottleTests
{
  [Fact]
  public async Task Schedule_ShouldDelayExecution()
  {
    // Arrange
    var executed = false;
    var throttle = new Throttle<string>(async key =>
    {
      await Task.CompletedTask;
      executed = true;
    }, delayMilliseconds: 100);

    // Act
    throttle.Schedule("key1");
    await Task.Delay(50); // Wait less than delay

    // Assert - Should not have executed yet
    Assert.False(executed);

    // Wait for execution
    await Task.Delay(100);
    Assert.True(executed);

    throttle.Dispose();
  }

  [Fact]
  public async Task Schedule_MultipleCalls_ShouldCancelPrevious()
  {
    // Arrange
    var executionCount = 0;
    var throttle = new Throttle<string>(async key =>
    {
      await Task.CompletedTask;
      executionCount++;
    }, delayMilliseconds: 100);

    // Act - Schedule 3 times quickly
    throttle.Schedule("key1");
    await Task.Delay(20);
    throttle.Schedule("key1");
    await Task.Delay(20);
    throttle.Schedule("key1");

    // Wait for execution
    await Task.Delay(150);

    // Assert - Should have executed only once (last call)
    Assert.Equal(1, executionCount);

    throttle.Dispose();
  }

  [Fact]
  public async Task Schedule_DifferentKeys_ShouldExecuteIndependently()
  {
    // Arrange
    var key1Count = 0;
    var key2Count = 0;
    var throttle = new Throttle<string>(async key =>
    {
      await Task.CompletedTask;
      if(key == "key1") key1Count++;
      if(key == "key2") key2Count++;
    }, delayMilliseconds: 50);

    // Act
    throttle.Schedule("key1");
    throttle.Schedule("key2");

    // Wait for execution
    await Task.Delay(100);

    // Assert
    Assert.Equal(1, key1Count);
    Assert.Equal(1, key2Count);

    throttle.Dispose();
  }

  [Fact]
  public async Task Cancel_ShouldPreventExecution()
  {
    // Arrange
    var executed = false;
    var throttle = new Throttle<string>(async key =>
    {
      await Task.CompletedTask;
      executed = true;
    }, delayMilliseconds: 100);

    // Act
    throttle.Schedule("key1");
    await Task.Delay(20);
    throttle.Cancel("key1");
    await Task.Delay(150);

    // Assert
    Assert.False(executed);

    throttle.Dispose();
  }

  [Fact]
  public async Task Dispose_ShouldCancelAllPending()
  {
    // Arrange
    var executionCount = 0;
    var throttle = new Throttle<string>(async key =>
    {
      await Task.CompletedTask;
      executionCount++;
    }, delayMilliseconds: 100);

    // Act
    throttle.Schedule("key1");
    throttle.Schedule("key2");
    throttle.Dispose();
    await Task.Delay(150);

    // Assert
    Assert.Equal(0, executionCount);
  }
}

[Trait("Category", "Helpers")]
public sealed class HeadsHelperTests
{
  [Fact]
  public void AreSame_WithIdenticalHeads_ShouldReturnTrue()
  {
    // Arrange
    var heads1 = new[] { "head1", "head2", "head3" };
    var heads2 = new[] { "head1", "head2", "head3" };

    // Act
    var areSame = HeadsHelper.AreSame(heads1, heads2);

    // Assert
    Assert.True(areSame);
  }

  [Fact]
  public void AreSame_WithReorderedHeads_ShouldReturnTrue()
  {
    // Arrange
    var heads1 = new[] { "head1", "head2", "head3" };
    var heads2 = new[] { "head3", "head1", "head2" };

    // Act
    var areSame = HeadsHelper.AreSame(heads1, heads2);

    // Assert
    Assert.True(areSame);
  }

  [Fact]
  public void AreSame_WithDifferentHeads_ShouldReturnFalse()
  {
    // Arrange
    var heads1 = new[] { "head1", "head2" };
    var heads2 = new[] { "head1", "head3" };

    // Act
    var areSame = HeadsHelper.AreSame(heads1, heads2);

    // Assert
    Assert.False(areSame);
  }

  [Fact]
  public void AreSame_WithDifferentCounts_ShouldReturnFalse()
  {
    // Arrange
    var heads1 = new[] { "head1", "head2" };
    var heads2 = new[] { "head1" };

    // Act
    var areSame = HeadsHelper.AreSame(heads1, heads2);

    // Assert
    Assert.False(areSame);
  }

  [Fact]
  public void AreSame_WithBothNull_ShouldReturnTrue()
  {
    // Act
    var areSame = HeadsHelper.AreSame(null, null);

    // Assert
    Assert.True(areSame);
  }

  [Fact]
  public void AreSame_WithOneNull_ShouldReturnFalse()
  {
    // Arrange
    var heads = new[] { "head1" };

    // Act
    var areSame1 = HeadsHelper.AreSame(heads, null);
    var areSame2 = HeadsHelper.AreSame(null, heads);

    // Assert
    Assert.False(areSame1);
    Assert.False(areSame2);
  }

  [Fact]
  public void ToHeadsKey_ShouldCreateDeterministicOutput()
  {
    // Arrange
    var heads1 = new[] { "head3", "head1", "head2" };
    var heads2 = new[] { "head1", "head2", "head3" };

    // Act
    var key1 = HeadsHelper.ToHeadsKey(heads1);
    var key2 = HeadsHelper.ToHeadsKey(heads2);

    // Assert - Should be identical (sorted)
    Assert.Equal(key1, key2);
    Assert.Equal("head1|head2|head3", key1);
  }

  [Fact]
  public void ToHeadsKey_WithEmptyList_ShouldReturnEmptyString()
  {
    // Arrange
    var heads = Array.Empty<string>();

    // Act
    var key = HeadsHelper.ToHeadsKey(heads);

    // Assert
    Assert.Equal(string.Empty, key);
  }

  [Fact]
  public void FromHeadsKey_ShouldParseHeads()
  {
    // Arrange
    var headsKey = "head1|head2|head3";

    // Act
    var heads = HeadsHelper.FromHeadsKey(headsKey);

    // Assert
    Assert.Equal(3, heads.Count);
    Assert.Equal(new[] { "head1", "head2", "head3" }, heads);
  }

  [Fact]
  public void FromHeadsKey_WithEmpty_ShouldReturnEmptyList()
  {
    // Act
    var heads = HeadsHelper.FromHeadsKey(string.Empty);

    // Assert
    Assert.Empty(heads);
  }
}

[Trait("Category", "Helpers")]
public sealed class BinaryHelperTests
{
  [Fact]
  public void Merge_ShouldCombineArrays()
  {
    // Arrange
    var array1 = new byte[] { 1, 2, 3 };
    var array2 = new byte[] { 4, 5, 6 };
    var array3 = new byte[] { 7, 8, 9 };

    // Act
    var merged = BinaryHelper.Merge(array1, array2, array3);

    // Assert
    Assert.Equal(9, merged.Length);
    Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, merged);
  }

  [Fact]
  public void Merge_WithNullArrays_ShouldSkipNulls()
  {
    // Arrange
    var array1 = new byte[] { 1, 2 };
    var array3 = new byte[] { 5, 6 };

    // Act
    var merged = BinaryHelper.Merge(array1, null!, array3);

    // Assert
    Assert.Equal(4, merged.Length);
    Assert.Equal(new byte[] { 1, 2, 5, 6 }, merged);
  }

  [Fact]
  public void Merge_WithEmptyArrays_ShouldSkipEmpty()
  {
    // Arrange
    var array1 = new byte[] { 1, 2 };
    var array2 = Array.Empty<byte>();
    var array3 = new byte[] { 3, 4 };

    // Act
    var merged = BinaryHelper.Merge(array1, array2, array3);

    // Assert
    Assert.Equal(4, merged.Length);
    Assert.Equal(new byte[] { 1, 2, 3, 4 }, merged);
  }

  [Fact]
  public void Merge_WithNoArrays_ShouldReturnEmpty()
  {
    // Act
    var merged = BinaryHelper.Merge();

    // Assert
    Assert.Empty(merged);
  }

  [Fact]
  public void AreEqual_WithIdenticalArrays_ShouldReturnTrue()
  {
    // Arrange
    var array1 = new byte[] { 1, 2, 3 };
    var array2 = new byte[] { 1, 2, 3 };

    // Act
    var areEqual = BinaryHelper.AreEqual(array1, array2);

    // Assert
    Assert.True(areEqual);
  }

  [Fact]
  public void AreEqual_WithDifferentArrays_ShouldReturnFalse()
  {
    // Arrange
    var array1 = new byte[] { 1, 2, 3 };
    var array2 = new byte[] { 1, 2, 4 };

    // Act
    var areEqual = BinaryHelper.AreEqual(array1, array2);

    // Assert
    Assert.False(areEqual);
  }

  [Fact]
  public void AreEqual_WithBothNull_ShouldReturnTrue()
  {
    // Act
    var areEqual = BinaryHelper.AreEqual(null, null);

    // Assert
    Assert.True(areEqual);
  }

  [Fact]
  public void ToHexString_ShouldFormatCorrectly()
  {
    // Arrange
    var data = new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF };

    // Act
    var hex = BinaryHelper.ToHexString(data);

    // Assert
    Assert.Equal("0123456789abcdef", hex);
  }

  [Fact]
  public void FromHexString_ShouldParseCorrectly()
  {
    // Arrange
    var hex = "0123456789ABCDEF";

    // Act
    var data = BinaryHelper.FromHexString(hex);

    // Assert
    Assert.Equal(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF }, data);
  }

  [Fact]
  public void HexRoundTrip_ShouldPreserveData()
  {
    // Arrange
    var original = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 };

    // Act
    var hex = BinaryHelper.ToHexString(original);
    var decoded = BinaryHelper.FromHexString(hex);

    // Assert
    Assert.Equal(original, decoded);
  }

  [Fact]
  public void FromHexString_WithOddLength_ShouldThrow()
  {
    // Arrange
    var hex = "123"; // Odd length

    // Act & Assert
    Assert.Throws<FormatException>(() => BinaryHelper.FromHexString(hex));
  }

  [Fact]
  public void FromHexString_WithInvalidCharacters_ShouldThrow()
  {
    // Arrange
    var hex = "12GH"; // G and H are not hex

    // Act & Assert
    Assert.Throws<FormatException>(() => BinaryHelper.FromHexString(hex));
  }
}

[Trait("Category", "Helpers")]
public sealed class TaskHelperTests
{
  [Fact]
  public async Task WithTimeout_ShouldCompleteNormally()
  {
    // Arrange
    var task = Task.Delay(50);

    // Act
    await task.WithTimeout(TimeSpan.FromMilliseconds(200));

    // Assert - Completed without exception
    Assert.True(true);
  }

  [Fact]
  public async Task WithTimeout_ShouldThrowOnTimeout()
  {
    // Arrange
    var task = Task.Delay(200);

    // Act & Assert
    await Assert.ThrowsAsync<TimeoutException>(
      () => task.WithTimeout(TimeSpan.FromMilliseconds(50)));
  }

  [Fact]
  public async Task WithTimeout_Generic_ShouldReturnResult()
  {
    // Arrange
    var task = Task.FromResult(42);

    // Act
    var result = await task.WithTimeout(TimeSpan.FromSeconds(1));

    // Assert
    Assert.Equal(42, result);
  }

  [Fact]
  public async Task FromEvent_ShouldCompleteWhenEventFires()
  {
    // Arrange
    var eventSource = new EventSource();
    var task = TaskHelper.FromEvent<TestEventArgs>(
      handler => eventSource.TestEvent += handler,
      handler => eventSource.TestEvent -= handler);

    // Act
    eventSource.RaiseEvent("test-value");
    var args = await task;

    // Assert
    Assert.Equal("test-value", args.Value);
  }

  [Fact]
  public async Task FromEvent_ShouldRespectCancellation()
  {
    // Arrange
    var eventSource = new EventSource();
    var cts = new CancellationTokenSource();
    var task = TaskHelper.FromEvent<TestEventArgs>(
      handler => eventSource.TestEvent += handler,
      handler => eventSource.TestEvent -= handler,
      cts.Token);

    // Act
    cts.Cancel();

    // Assert
    await Assert.ThrowsAsync<TaskCanceledException>(() => task);
  }

  private class EventSource
  {
    public event EventHandler<TestEventArgs>? TestEvent;

    public void RaiseEvent(string value)
    {
      TestEvent?.Invoke(this, new TestEventArgs { Value = value });
    }
  }

  private class TestEventArgs : EventArgs
  {
    public string Value { get; init; } = string.Empty;
  }
}
