using strAppersBackend.Controllers;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// Budget allocation and file accounting for the rendered diff.
///
/// A live run exposed both problems these tests pin. A 12K-char controller consumed the entire
/// per-track budget, so the three other files in the same delivery — including a whole test project —
/// were dropped with no trace; and the "Files changed" line printed the number of files SHOWN, so a
/// four-file delivery was reported as one.
/// </summary>
public class GitHubSensorDiffBudgetTests
{
    private static MetricsController.GitHubSensorOptions Options(int budget = 12000, int maxFiles = 20) =>
        new() { DiffCharBudgetPerTrack = budget, MaxFilesPerTrack = maxFiles, ExcludedPathPatterns = MetricsController.DefaultExcludedPathPatterns };

    private static GitHubFileChange File(string path, int patchChars, int additions = 10) =>
        new() { FilePath = path, Status = "added", Additions = additions, Deletions = 0, Patch = new string('x', patchChars) };

    private static MetricsController.GitHubTrackEvidence Evidence() =>
        new() { IsBackend = true, Branch = "5-B" };

    [Fact]
    public void ChangedFileCount_CountsEveryAuthoredFile_NotOnlyTheShownOnes()
    {
        var e = Evidence();
        MetricsController.ApplyDiff(e, new[]
        {
            File("Controllers/ReportsController.cs", 12000, additions: 281),
            File("Backend.csproj", 200, additions: 6),
            File("Tests/Backend.Tests.csproj", 600, additions: 16),
            File("Tests/ReportSnapshotTests.cs", 1500, additions: 39),
        }, "branch compare", Options());

        Assert.Equal(4, e.ChangedFileCount);
        Assert.Equal(342, e.TotalAdditions);
    }

    /// <summary>
    /// The exact shape of the real failure: the large file must not starve the rest of the delivery.
    /// </summary>
    [Fact]
    public void LargeFileDoesNotStarveTheRestOfTheDelivery()
    {
        var e = Evidence();
        MetricsController.ApplyDiff(e, new[]
        {
            File("Controllers/ReportsController.cs", 12000, additions: 281),
            File("Backend.csproj", 200, additions: 6),
            File("Tests/Backend.Tests.csproj", 600, additions: 16),
            File("Tests/ReportSnapshotTests.cs", 1500, additions: 39),
        }, "branch compare", Options());

        Assert.Equal(4, e.Files.Count);
        Assert.Empty(e.OmittedFileNames);

        // The test project must be visible — it is exactly what a rubric rewards.
        Assert.Contains(e.Files, f => f.FilePath == "Tests/ReportSnapshotTests.cs");
        Assert.Contains(e.Files, f => f.FilePath == "Tests/Backend.Tests.csproj");
    }

    [Fact]
    public void SmallFilesKeepTheirFullPatch_AndTheLeftoverRollsToTheLargeFile()
    {
        var e = Evidence();
        MetricsController.ApplyDiff(e, new[]
        {
            File("big.cs", 12000),
            File("small1.cs", 200),
            File("small2.cs", 600),
        }, "branch compare", Options());

        var small1 = e.Files.Single(f => f.FilePath == "small1.cs");
        var small2 = e.Files.Single(f => f.FilePath == "small2.cs");
        var big = e.Files.Single(f => f.FilePath == "big.cs");

        Assert.Equal(200, small1.Patch!.Length);
        Assert.Equal(600, small2.Patch!.Length);

        // Fair share alone would be 4000; the unused remainder of the small files rolls forward.
        Assert.True(big.Patch!.Length > 4000, $"big file got only {big.Patch.Length} chars");
    }

    [Fact]
    public void TotalBudgetIsNeverExceeded()
    {
        var e = Evidence();
        MetricsController.ApplyDiff(e, new[]
        {
            File("a.cs", 9000), File("b.cs", 9000), File("c.cs", 9000),
        }, "branch compare", Options(budget: 6000));

        Assert.True(e.Files.Sum(f => f.Patch?.Length ?? 0) <= 6000);
    }

    [Fact]
    public void FilesBeyondTheFileLimit_AreNamedNotDropped()
    {
        var e = Evidence();
        MetricsController.ApplyDiff(e, new[]
        {
            File("a.cs", 100, additions: 50),
            File("b.cs", 100, additions: 40),
            File("c.cs", 100, additions: 30),
        }, "branch compare", Options(maxFiles: 2));

        Assert.Equal(3, e.ChangedFileCount);
        Assert.Equal(2, e.Files.Count);
        Assert.Single(e.OmittedFileNames);
        Assert.Contains("c.cs", e.OmittedFileNames);
    }

    [Fact]
    public void WhenBudgetIsExhausted_RemainingFilesAreNamedNotSilentlyLost()
    {
        var e = Evidence();
        MetricsController.ApplyDiff(e, new[]
        {
            File("a.cs", 5000), File("b.cs", 5000), File("c.cs", 5000), File("d.cs", 5000),
        }, "branch compare", Options(budget: 400));

        // Everything is accounted for: shown plus named equals changed.
        Assert.Equal(4, e.ChangedFileCount);
        Assert.Equal(4, e.Files.Count + e.OmittedFileNames.Count);
    }

    [Fact]
    public void GeneratedFilesAreExcludedFromCountsAndTotals()
    {
        var e = Evidence();
        MetricsController.ApplyDiff(e, new[]
        {
            File("src/Analytics.jsx", 500, additions: 178),
            File("package-lock.json", 90000, additions: 4984),
            File("dist/index.html", 9000, additions: 436),
        }, "branch compare", Options());

        Assert.Equal(1, e.ChangedFileCount);
        Assert.Equal(178, e.TotalAdditions);
        Assert.Equal(2, e.ExcludedFileCount);
        Assert.Equal(5420, e.ExcludedAdditions);

        // Excluded files are not "omitted for budget" — they are a different category entirely.
        Assert.Empty(e.OmittedFileNames);
    }

    [Fact]
    public void ADeliveryWhosePatchesAllFallOutOfBudget_StillCountsAsDelivered()
    {
        var e = Evidence();
        MetricsController.ApplyDiff(e, new[] { File("huge.cs", 50000, additions: 900) },
            "branch compare", Options(budget: 10));

        Assert.True(e.HasAnyDelivery);
        Assert.Equal(1, e.ChangedFileCount);
    }
}
