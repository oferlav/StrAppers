using strAppersBackend.Controllers;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// Phase 2 (Trello round-trip integrity): full ISO due formatting, creation-time merge-seed
/// preference, and end-of-local-day normalization for chained dues.
/// </summary>
public class TrelloDueRoundTripTests
{
    private static readonly TimeSpan Gmt2 = TimeSpan.FromHours(2);

    // ── P2-1: FormatTrelloDue ─────────────────────────────────────────────────

    [Fact]
    public void FormatTrelloDue_EmitsFullIso8601Utc()
    {
        var due = new DateTime(2026, 7, 12, 21, 59, 59, DateTimeKind.Utc);

        Assert.Equal("2026-07-12T21:59:59Z", TrelloService.FormatTrelloDue(due));
    }

    [Fact]
    public void FormatTrelloDue_EndOfDayLocal_KeepsExactInstant_NoDateShift()
    {
        // Jul 12 23:59:59 local (GMT+2) = Jul 12 21:59:59Z — the formatted string must carry
        // the time so Trello's round trip cannot flatten it to midnight UTC.
        var endOfDayLocalAsUtc = new DateTime(2026, 7, 12, 23, 59, 59).Subtract(Gmt2);

        var formatted = TrelloService.FormatTrelloDue(endOfDayLocalAsUtc);

        Assert.Equal("2026-07-12T21:59:59Z", formatted);
        Assert.Contains("T", formatted); // full timestamp, never date-only
    }

    // ── P2-2: ResolveMergeSeedDueUtc ──────────────────────────────────────────

    private static readonly DateTime Kickoff = new(2026, 7, 10, 8, 0, 0, DateTimeKind.Utc); // Fri 10:00 local

    [Fact]
    public void SeedDue_DayBased_IgnoresSnapshot_UsesComputedDue()
    {
        var flattenedSnapshot = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc); // Trello-flattened

        var due = BoardsController.ResolveMergeSeedDueUtc(
            sprintLengthDays: 2, snapshotDueUtc: flattenedSnapshot, kickoffUtc: Kickoff,
            sprintNumber: 1, firstDayOfWeek: DayOfWeek.Sunday, localOffsetMinutes: 120,
            sprintLengthWeeks: 1, localOffset: Gmt2);

        var expected = TrelloBoardScheduleHelper.GetSprintDueDateUtcForDays(Kickoff, 1, 2, Gmt2);
        Assert.Equal(expected, due);
        Assert.NotEqual(flattenedSnapshot, due);
    }

    [Fact]
    public void SeedDue_Weekly_PrefersSnapshot()
    {
        var snapshot = new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc);

        var due = BoardsController.ResolveMergeSeedDueUtc(
            sprintLengthDays: null, snapshotDueUtc: snapshot, kickoffUtc: Kickoff,
            sprintNumber: 1, firstDayOfWeek: DayOfWeek.Sunday, localOffsetMinutes: 120,
            sprintLengthWeeks: 1, localOffset: Gmt2);

        Assert.Equal(snapshot, due); // legacy behavior unchanged
    }

    [Fact]
    public void SeedDue_Weekly_NoSnapshot_FallsBackToWeeklyHelper()
    {
        var due = BoardsController.ResolveMergeSeedDueUtc(
            sprintLengthDays: null, snapshotDueUtc: null, kickoffUtc: Kickoff,
            sprintNumber: 1, firstDayOfWeek: DayOfWeek.Sunday, localOffsetMinutes: 120,
            sprintLengthWeeks: 1, localOffset: Gmt2);

        // Weekly helper: kickoff local date + 6 days at 23:59:59 local.
        var kickoffLocal = Kickoff.AddMinutes(120);
        var expected = kickoffLocal.Date.AddDays(6).AddHours(23).AddMinutes(59).AddSeconds(59).AddMinutes(-120);
        Assert.Equal(expected, due);
    }

    // ── P2-3: NormalizeToEndOfLocalDay ────────────────────────────────────────

    [Fact]
    public void Normalize_CleanEndOfDayLocal_IsNoOp()
    {
        var clean = new DateTime(2026, 7, 12).AddDays(1).AddTicks(-1).Subtract(Gmt2); // Jul 12 23:59:59.9999999 local

        var normalized = TrelloBoardScheduleHelper.NormalizeToEndOfLocalDay(clean, Gmt2);

        Assert.Equal(clean, normalized);
    }

    [Fact]
    public void Normalize_FlattenedMidnightUtc_HealsToSameLocalDayEnd()
    {
        // Jul 12 00:00Z = Jul 12 02:00 local → end of Jul 12 local = Jul 12 21:59:59.9999999Z.
        var flattened = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);

        var normalized = TrelloBoardScheduleHelper.NormalizeToEndOfLocalDay(flattened, Gmt2);

        Assert.Equal(new DateTime(2026, 7, 12).AddDays(1).AddTicks(-1).Subtract(Gmt2), normalized);
    }

    [Fact]
    public void Normalize_ChainedFromClean_StaysClean()
    {
        // AddDays on a clean end-of-day value keeps the time; normalization must not move it.
        var clean = new DateTime(2026, 7, 12).AddDays(1).AddTicks(-1).Subtract(Gmt2);
        var chained = clean.AddDays(2);

        var normalized = TrelloBoardScheduleHelper.NormalizeToEndOfLocalDay(chained, Gmt2);

        Assert.Equal(chained, normalized);
    }
}
