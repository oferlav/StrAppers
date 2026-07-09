using strAppersBackend.Models;
using strAppersBackend.Utilities;

namespace strAppersBackend.Tests;

/// <summary>
/// SprintPlanDateResolver after the days switch: the merge-row resolver takes DAYS directly (7a),
/// and the SprintPlan fallback accepts a days override that bypasses the weekly heuristic (7b).
/// </summary>
public class SprintPlanDateResolverDaysTests
{
    // ── 7a. Merge-row resolver ────────────────────────────────────────────────

    [Fact]
    public void Sprint2_Days3_StartIsDueMinus2Days()
    {
        var due = new DateTime(2026, 7, 12, 21, 59, 59, DateTimeKind.Utc);
        var merge = new ProjectBoardSprintMerge { SprintNumber = 2, DueDate = due };

        var ok = SprintPlanDateResolver.TryGetInclusiveUtcRangeFromSprintMerge(merge, 2, 3, out var start, out var end);

        Assert.True(ok);
        Assert.Equal(due.AddDays(-2), start);
        Assert.Equal(due, end);
    }

    [Fact]
    public void Sprint1_UsesMergedAt_IgnoresLength()
    {
        var mergedAt = new DateTime(2026, 7, 7, 8, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2026, 7, 9, 21, 59, 59, DateTimeKind.Utc);
        var merge = new ProjectBoardSprintMerge { SprintNumber = 1, MergedAt = mergedAt, DueDate = due };

        var ok = SprintPlanDateResolver.TryGetInclusiveUtcRangeFromSprintMerge(merge, 1, 3, out var start, out _);

        Assert.True(ok);
        Assert.Equal(mergedAt, start);
    }

    [Fact]
    public void Days7_MatchesOldWeeks1Behavior()
    {
        // Regression: passing 7 days must produce the exact output the old weeks=1 produced.
        var due = new DateTime(2026, 7, 11, 21, 59, 59, DateTimeKind.Utc);
        var merge = new ProjectBoardSprintMerge { SprintNumber = 2, DueDate = due };

        var ok = SprintPlanDateResolver.TryGetInclusiveUtcRangeFromSprintMerge(merge, 2, 7, out var start, out var end);

        Assert.True(ok);
        Assert.Equal(due.AddDays(-6), start); // old: weeks=1 → 7 days → due − 6
        Assert.Equal(due, end);
    }

    [Fact]
    public void NullDueDate_ReturnsFalse()
    {
        var merge = new ProjectBoardSprintMerge { SprintNumber = 2, DueDate = null };

        Assert.False(SprintPlanDateResolver.TryGetInclusiveUtcRangeFromSprintMerge(merge, 2, 3, out _, out _));
    }

    [Fact]
    public void MidnightDue_EndInclusiveIsEndOfDay()
    {
        var dueMidnight = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
        var merge = new ProjectBoardSprintMerge { SprintNumber = 2, DueDate = dueMidnight };

        var ok = SprintPlanDateResolver.TryGetInclusiveUtcRangeFromSprintMerge(merge, 2, 3, out _, out var end);

        Assert.True(ok);
        Assert.Equal(dueMidnight.Date.AddDays(1).AddTicks(-1), end);
    }

    // ── 7b. SprintPlan fallback resolver ──────────────────────────────────────

    private const string PlanNoListDates =
        """{"Lists":[{"Name":"Sprint1"},{"Name":"Sprint2"},{"Name":"Sprint3"},{"Name":"Bugs"}],"TotalSprints":3,"EstimatedWeeks":3}""";

    [Fact]
    public void Fallback_WithDaysOverride3_ProducesThreeDayWindows()
    {
        var boardStart = new DateTime(2026, 7, 7, 0, 0, 0, DateTimeKind.Utc);

        var ok = SprintPlanDateResolver.TryGetSprintInclusiveUtcRange(
            PlanNoListDates, boardStart, 2, out var start, out var end, sprintLengthDaysOverride: 3);

        Assert.True(ok);
        Assert.Equal(boardStart.AddDays(3), start);            // sprint 2 starts 3 days in
        Assert.Equal(boardStart.AddDays(6).AddTicks(-1), end); // and ends 6 days in
    }

    [Fact]
    public void Fallback_WithoutOverride_KeepsWeeklyHeuristic()
    {
        var boardStart = new DateTime(2026, 7, 7, 0, 0, 0, DateTimeKind.Utc);

        var ok = SprintPlanDateResolver.TryGetSprintInclusiveUtcRange(
            PlanNoListDates, boardStart, 2, out var start, out var end);

        Assert.True(ok);
        Assert.Equal(boardStart.AddDays(7), start);             // legacy: 7×3/3 = 7 days per sprint
        Assert.Equal(boardStart.AddDays(14).AddTicks(-1), end);
    }

    [Fact]
    public void Fallback_ListDatesPresent_OverrideIgnored()
    {
        const string planWithDates =
            """{"Lists":[{"Name":"Sprint1","StartDate":"2026-07-07T00:00:00Z","EndDate":"2026-07-13T21:59:59Z"}],"TotalSprints":1}""";

        var ok = SprintPlanDateResolver.TryGetSprintInclusiveUtcRange(
            planWithDates, new DateTime(2026, 7, 7), 1, out var start, out var end, sprintLengthDaysOverride: 3);

        Assert.True(ok);
        Assert.Equal(new DateTime(2026, 7, 7, 0, 0, 0), start); // explicit list dates win
        Assert.Equal(new DateTime(2026, 7, 13, 21, 59, 59), end);
    }
}
