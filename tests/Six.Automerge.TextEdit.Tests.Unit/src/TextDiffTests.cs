// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge.TextEdit.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class TextDiffTests
{
  [Fact]
  public void ComputeSplice_IdenticalStrings_ReturnsNoChange()
  {
    var result = TextDiff.ComputeSplice("hello", "hello");

    result.HasChanges.Should().BeFalse();
    result.DeleteCount.Should().Be(0);
    result.InsertText.Should().BeEmpty();
  }

  [Fact]
  public void ComputeSplice_BothEmpty_ReturnsNoChange()
  {
    var result = TextDiff.ComputeSplice("", "");

    result.HasChanges.Should().BeFalse();
  }

  [Fact]
  public void ComputeSplice_InsertAtEnd_ReturnsCorrectSplice()
  {
    var result = TextDiff.ComputeSplice("hello", "hello world");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(5);
    result.DeleteCount.Should().Be(0);
    result.InsertText.Should().Be(" world");
  }

  [Fact]
  public void ComputeSplice_InsertAtBeginning_ReturnsCorrectSplice()
  {
    var result = TextDiff.ComputeSplice("world", "hello world");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(0);
    result.DeleteCount.Should().Be(0);
    result.InsertText.Should().Be("hello ");
  }

  [Fact]
  public void ComputeSplice_InsertInMiddle_ReturnsCorrectSplice()
  {
    var result = TextDiff.ComputeSplice("helloworld", "hello world");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(5);
    result.DeleteCount.Should().Be(0);
    result.InsertText.Should().Be(" ");
  }

  [Fact]
  public void ComputeSplice_DeleteFromEnd_ReturnsCorrectSplice()
  {
    var result = TextDiff.ComputeSplice("hello world", "hello");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(5);
    result.DeleteCount.Should().Be(6);
    result.InsertText.Should().BeEmpty();
  }

  [Fact]
  public void ComputeSplice_DeleteFromBeginning_ReturnsCorrectSplice()
  {
    var result = TextDiff.ComputeSplice("hello world", "world");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(0);
    result.DeleteCount.Should().Be(6);
    result.InsertText.Should().BeEmpty();
  }

  [Fact]
  public void ComputeSplice_ReplaceInMiddle_ReturnsCorrectSplice()
  {
    var result = TextDiff.ComputeSplice("hello world", "hello there");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(6);
    result.DeleteCount.Should().Be(5);
    result.InsertText.Should().Be("there");
  }

  [Fact]
  public void ComputeSplice_CompleteReplacement_ReturnsCorrectSplice()
  {
    var result = TextDiff.ComputeSplice("abc", "xyz");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(0);
    result.DeleteCount.Should().Be(3);
    result.InsertText.Should().Be("xyz");
  }

  [Fact]
  public void ComputeSplice_EmptyToNonEmpty_ReturnsInsertAll()
  {
    var result = TextDiff.ComputeSplice("", "hello");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(0);
    result.DeleteCount.Should().Be(0);
    result.InsertText.Should().Be("hello");
  }

  [Fact]
  public void ComputeSplice_NonEmptyToEmpty_ReturnsDeleteAll()
  {
    var result = TextDiff.ComputeSplice("hello", "");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(0);
    result.DeleteCount.Should().Be(5);
    result.InsertText.Should().BeEmpty();
  }

  [Fact]
  public void ComputeSplice_SingleCharInsert_ReturnsCorrectSplice()
  {
    var result = TextDiff.ComputeSplice("hllo", "hello");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(1);
    result.DeleteCount.Should().Be(0);
    result.InsertText.Should().Be("e");
  }

  [Fact]
  public void ComputeSplice_SingleCharDelete_ReturnsCorrectSplice()
  {
    var result = TextDiff.ComputeSplice("hello", "hllo");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(1);
    result.DeleteCount.Should().Be(1);
    result.InsertText.Should().BeEmpty();
  }

  [Fact]
  public void ComputeSplice_ReplaceMiddleChars_KeepsCommonPrefixAndSuffix()
  {
    var result = TextDiff.ComputeSplice("abcdef", "abXYef");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(2);
    result.DeleteCount.Should().Be(2);
    result.InsertText.Should().Be("XY");
  }

  [Theory]
  [InlineData(null, "hello")]
  [InlineData("hello", null)]
  [InlineData(null, null)]
  public void ComputeSplice_NullArguments_ThrowsArgumentNullException(string? oldText, string? newText)
  {
    var act = () => TextDiff.ComputeSplice(oldText!, newText!);

    act.Should().Throw<ArgumentNullException>();
  }

  [Fact]
  public void ComputeSplice_RepeatedCharacters_HandlesCorrectly()
  {
    // "aaa" -> "aaaa" — insert one 'a' at end
    var result = TextDiff.ComputeSplice("aaa", "aaaa");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(3);
    result.DeleteCount.Should().Be(0);
    result.InsertText.Should().Be("a");
  }

  [Fact]
  public void ComputeSplice_UnicodeInsert_ReturnsCorrectSplice()
  {
    var result = TextDiff.ComputeSplice("hello", "hello 🌍");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(5);
    result.DeleteCount.Should().Be(0);
    result.InsertText.Should().Be(" 🌍");
  }

  [Fact]
  public void ComputeSplice_UnicodeReplace_ReturnsCorrectSplice()
  {
    var result = TextDiff.ComputeSplice("café", "cafè");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(3);
    result.DeleteCount.Should().Be(1);
    result.InsertText.Should().Be("è");
  }

  [Fact]
  public void ComputeSplice_UnicodeIdentical_ReturnsNoChange()
  {
    var result = TextDiff.ComputeSplice("日本語テキスト", "日本語テキスト");

    result.HasChanges.Should().BeFalse();
  }

  [Fact]
  public void ComputeSplice_UnicodeMiddleEdit_ReturnsCorrectSplice()
  {
    // Replace middle character in Japanese text
    var result = TextDiff.ComputeSplice("日本語", "日中語");

    result.HasChanges.Should().BeTrue();
    result.Offset.Should().Be(1);
    result.DeleteCount.Should().Be(1);
    result.InsertText.Should().Be("中");
  }

  [Fact]
  public void SpliceResult_ToString_DescribesChange()
  {
    var result = TextDiff.ComputeSplice("hello", "hello world");

    result.ToString().Should().Contain("5");
  }
}
