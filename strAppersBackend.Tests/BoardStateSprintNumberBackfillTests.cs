using strAppersBackend.Controllers;

namespace strAppersBackend.Tests;

/// <summary>
/// Guards the branch-name parsing used by the BoardStates.SprintNumber backfill migration.
///
/// The migration parses in SQL (split_part on '-', a digit test, and a 'bugs' test) while the
/// capture-time writers parse in C# via <see cref="MentorController.ParseSprintNumber"/>. If the two
/// ever disagree, backfilled rows land in a different sprint than freshly written ones and the
/// assessment engine's exact SprintNumber match silently drops them. This test replicates the SQL
/// rules and asserts the two agree on every branch shape the platform produces.
/// </summary>
public class BoardStateSprintNumberBackfillTests
{
    /// <summary>
    /// C# equivalent of the migration's SQL:
    ///   split_part(branch,'-',1) ~ '^[0-9]{1,6}$'  -> that number
    ///   lower(split_part(branch,'-',1)) = 'bugs'   -> 0
    ///   otherwise                                  -> not updated (null)
    /// </summary>
    private static int? BackfillSql(string? branch)
    {
        if (string.IsNullOrEmpty(branch)) return null;

        var first = branch.Split('-')[0];

        var isDigits = first.Length is > 0 and <= 6 && first.All(char.IsAsciiDigit);
        if (isDigits) return int.Parse(first);

        if (string.Equals(first, "bugs", StringComparison.OrdinalIgnoreCase)) return 0;

        return null;
    }

    [Theory]
    [InlineData("1-B")]
    [InlineData("1-F")]
    [InlineData("12-B")]
    [InlineData("7-B-3")]
    [InlineData("2-F-1")]
    [InlineData("Bugs-B")]
    [InlineData("Bugs-F")]
    [InlineData("bugs-b")]
    [InlineData("BUGS-F")]
    [InlineData("main")]
    [InlineData("gh-pages")]
    [InlineData("feature/reports")]
    [InlineData("")]
    public void BackfillSqlAgreesWithCaptureTimeParser(string branch)
    {
        Assert.Equal(MentorController.ParseSprintNumber(branch), BackfillSql(branch));
    }

    /// <summary>
    /// Rows that legitimately have no sprint — deployment, runtime-error and frontend-log rows carry
    /// no branch by design, and Pages rows carry the Pages source branch. The backfill must leave
    /// them null rather than inventing a sprint.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("main")]
    [InlineData("master")]
    public void BranchlessAndNonSprintRows_StayNull(string? branch)
    {
        Assert.Null(BackfillSql(branch));
    }

    /// <summary>
    /// The SQL caps the digit run at six characters so a malformed branch cannot overflow int when
    /// cast. Anything longer must be skipped, not truncated.
    /// </summary>
    [Fact]
    public void OverlongDigitRun_IsSkippedRatherThanOverflowing()
    {
        Assert.Null(BackfillSql("99999999999999-B"));
    }

    [Fact]
    public void SixDigitSprint_IsStillAccepted()
    {
        Assert.Equal(999999, BackfillSql("999999-B"));
    }
}
