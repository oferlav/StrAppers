using System.Text.RegularExpressions;

namespace strAppersBackend.Utilities;

/// <summary>
/// Word counting and truncation for the institute hero headlines.
///
/// Deliberately not ProjectsController.ClampToMaxWordsString: that one splits on whitespace and
/// rejoins with single spaces, which collapses newlines. These headlines are edited in multi-line
/// boxes, so line breaks are meaningful and must survive the clamp.
/// </summary>
public static class HeadlineWordLimit
{
    private static readonly Regex WordRegex = new(@"\S+", RegexOptions.Compiled);

    /// <summary>Number of whitespace-separated words. Empty/whitespace counts as 0.</summary>
    public static int CountWords(string? value)
        => string.IsNullOrWhiteSpace(value) ? 0 : WordRegex.Matches(value).Count;

    /// <summary>
    /// Trims <paramref name="value"/> to at most <paramref name="maxWords"/> words, preserving the
    /// original spacing and line breaks up to the cut. Returns null for empty input so the caller
    /// stores NULL and the frontend falls back to its default copy.
    /// </summary>
    public static string? ClampToMaxWords(string? value, int maxWords)
    {
        if (string.IsNullOrWhiteSpace(value) || maxWords <= 0)
        {
            return null;
        }

        var text = value.Trim();
        var words = WordRegex.Matches(text);
        if (words.Count <= maxWords)
        {
            return text;
        }

        var lastKept = words[maxWords - 1];
        return text[..(lastKept.Index + lastKept.Length)].TrimEnd();
    }
}
