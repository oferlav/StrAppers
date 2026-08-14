using strAppersBackend.Utilities;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests for the institute hero headline word limits (Primary 10 words, Secondary 20 by default).
///
/// Deliberately not ProjectsController.ClampToMaxWordsString: that helper splits on whitespace and
/// rejoins with single spaces, which collapses newlines. These headlines are edited in multi-line
/// boxes, so line breaks are content and must survive the clamp.
/// </summary>
public class HeadlineWordLimitTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("one", 1)]
    [InlineData("  one   two  ", 2)]
    [InlineData("one\ntwo\nthree", 3)]
    public void CountWords_CountsWhitespaceSeparatedTokens(string? input, int expected)
        => Assert.Equal(expected, HeadlineWordLimit.CountWords(input));

    [Fact]
    public void UnderTheLimit_IsReturnedUnchanged()
        => Assert.Equal("Build real products", HeadlineWordLimit.ClampToMaxWords("Build real products", 10));

    [Fact]
    public void OverTheLimit_IsTruncatedToTheWordCount()
    {
        var clamped = HeadlineWordLimit.ClampToMaxWords("one two three four five six", 3);

        Assert.Equal("one two three", clamped);
        Assert.Equal(3, HeadlineWordLimit.CountWords(clamped));
    }

    [Fact]
    public void ExactlyTheLimit_IsKeptWhole()
        => Assert.Equal("one two three", HeadlineWordLimit.ClampToMaxWords("one two three", 3));

    [Fact]
    public void LineBreaksSurviveTheClamp()
    {
        // The whole reason this helper exists rather than reusing the projects one.
        var clamped = HeadlineWordLimit.ClampToMaxWords("first line\nsecond line", 10);

        Assert.Equal("first line\nsecond line", clamped);
        Assert.Contains('\n', clamped!);
    }

    [Fact]
    public void LineBreaksSurviveEvenWhenTruncating()
    {
        var clamped = HeadlineWordLimit.ClampToMaxWords("one two\nthree four five", 3);

        Assert.Equal("one two\nthree", clamped);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void EmptyInput_BecomesNullSoTheFrontendFallsBackToDefaultCopy(string? input)
        => Assert.Null(HeadlineWordLimit.ClampToMaxWords(input, 10));

    [Fact]
    public void SurroundingWhitespaceIsTrimmed()
        => Assert.Equal("headline", HeadlineWordLimit.ClampToMaxWords("   headline   ", 10));

    [Fact]
    public void NonPositiveLimit_ReturnsNull()
        => Assert.Null(HeadlineWordLimit.ClampToMaxWords("one two", 0));

    [Fact]
    public void DefaultsAreTenAndTwenty()
    {
        var options = new strAppersBackend.Models.InstituteHeadlineFieldsOptions();

        Assert.Equal(10, options.PrimaryWords);
        Assert.Equal(20, options.SecondaryWords);
    }
}
