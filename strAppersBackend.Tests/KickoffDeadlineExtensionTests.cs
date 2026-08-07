using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// Unit tests for the kickoff deadline extension: a squad may propose a kickoff meeting up to 7 days
/// out, and proposing past the board's agree-by deadline (CreatedAt + KickoffConfig2:BoardTimeout)
/// pushes that deadline out to the proposed time + 12 hours grace instead of being rejected.
///
/// Exercises KickoffDeadline directly — it is the single source of truth all three enforcement
/// points (BoardsController.SuggestKickoffDate, KickoffResetService, StudentTeamBuilderService's
/// reset job) resolve the deadline through.
/// </summary>
public class KickoffDeadlineExtensionTests
{
    private const int DefaultBoardTimeoutMinutes = 4320; // KickoffConfig2.BoardTimeout default = 3 days

    private static ProjectBoard Board(DateTime createdAt, DateTime? kickoffTimeoutDateTime = null) => new()
    {
        Id = "board1",
        ProjectId = 1,
        KickoffState = 1,
        CreatedAt = createdAt,
        KickoffTimeoutDateTime = kickoffTimeoutDateTime
    };

    // ── Resolve: which deadline is actually in force ──────────────────────────────────

    [Fact]
    public void Resolve_UsesConfiguredTimeout_WhenNeverExtended()
    {
        var createdAt = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        var deadline = KickoffDeadline.Resolve(createdAt, null, DefaultBoardTimeoutMinutes);
        Assert.Equal(createdAt.AddDays(3), deadline);
    }

    [Fact]
    public void Resolve_UsesStoredDeadline_WhenExtended()
    {
        var createdAt = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        var extended = new DateTime(2026, 8, 6, 22, 0, 0, DateTimeKind.Utc);
        Assert.Equal(extended, KickoffDeadline.Resolve(createdAt, extended, DefaultBoardTimeoutMinutes));
    }

    [Fact]
    public void Resolve_HonorsAShorterConfiguredTimeout_WhenNeverExtended()
    {
        var createdAt = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        Assert.Equal(createdAt.AddMinutes(60), KickoffDeadline.Resolve(createdAt, null, 60));
    }

    // ── ExtensionFor: when a proposal moves the deadline ──────────────────────────────

    [Fact]
    public void ExtensionFor_ReturnsNull_WhenProposalIsWellInsideTheWindow()
    {
        var deadline = new DateTime(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);
        var suggested = new DateTime(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc); // 2 days before the deadline
        Assert.Null(KickoffDeadline.ExtensionFor(suggested, deadline));
    }

    [Fact]
    public void ExtensionFor_PushesDeadlineToProposalPlusGrace_WhenProposalIsPastTheWindow()
    {
        // Board created Aug 1 → default deadline Aug 4. Squad wants to meet Aug 7.
        var deadline = new DateTime(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);
        var suggested = new DateTime(2026, 8, 7, 14, 0, 0, DateTimeKind.Utc);

        var extended = KickoffDeadline.ExtensionFor(suggested, deadline);

        Assert.Equal(suggested.AddHours(KickoffDeadline.GraceHours), extended);
        Assert.Equal(new DateTime(2026, 8, 8, 2, 0, 0, DateTimeKind.Utc), extended);
    }

    [Fact]
    public void ExtensionFor_StillExtends_WhenProposalLandsWithinGraceOfTheDeadline()
    {
        // The meeting itself is inside the window, but only 2h before it closes — without the grace
        // period the board could be reset while the meeting is still running.
        var deadline = new DateTime(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);
        var suggested = new DateTime(2026, 8, 4, 7, 0, 0, DateTimeKind.Utc);

        var extended = KickoffDeadline.ExtensionFor(suggested, deadline);

        Assert.Equal(new DateTime(2026, 8, 4, 19, 0, 0, DateTimeKind.Utc), extended);
        Assert.True(extended > deadline);
    }

    [Fact]
    public void ExtensionFor_AlwaysLeavesTwelveHoursAfterTheMeeting()
    {
        var deadline = new DateTime(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);
        foreach (var offsetHours in new[] { -48, -1, 0, 1, 48, 168 })
        {
            var suggested = deadline.AddHours(offsetHours);
            var effective = KickoffDeadline.ExtensionFor(suggested, deadline) ?? deadline;
            Assert.True(effective >= suggested.AddHours(KickoffDeadline.GraceHours) || effective == deadline);
            if (effective == deadline)
                Assert.True(suggested.AddHours(KickoffDeadline.GraceHours) <= deadline);
        }
    }

    // ── Extend-only: nothing may shorten a window the squad is already relying on ─────

    [Fact]
    public void ExtensionFor_IsExtendOnly_AnEarlierProposalDoesNotPullTheDeadlineBack()
    {
        var createdAt = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        var board = Board(createdAt);

        // First proposal: Aug 7 → extends to Aug 8 02:00.
        var firstDeadline = KickoffDeadline.Resolve(board.CreatedAt, board.KickoffTimeoutDateTime, DefaultBoardTimeoutMinutes);
        board.KickoffTimeoutDateTime = KickoffDeadline.ExtensionFor(new DateTime(2026, 8, 7, 14, 0, 0, DateTimeKind.Utc), firstDeadline)
            ?? board.KickoffTimeoutDateTime;
        Assert.Equal(new DateTime(2026, 8, 8, 2, 0, 0, DateTimeKind.Utc), board.KickoffTimeoutDateTime);

        // Counter-proposal: Aug 2 — earlier than the original 3-day deadline, let alone the extension.
        var secondDeadline = KickoffDeadline.Resolve(board.CreatedAt, board.KickoffTimeoutDateTime, DefaultBoardTimeoutMinutes);
        var secondExtension = KickoffDeadline.ExtensionFor(new DateTime(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc), secondDeadline);

        Assert.Null(secondExtension);
        Assert.Equal(new DateTime(2026, 8, 8, 2, 0, 0, DateTimeKind.Utc), board.KickoffTimeoutDateTime);
    }

    [Fact]
    public void ExtensionFor_KeepsTheExtension_WhenTheSquadRejectsAndProposesAgainSooner()
    {
        // Rejection is just another suggest call — there is no path that clears KickoffTimeoutDateTime,
        // so the window the squad was told about survives a rejected proposal.
        var createdAt = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        var extended = new DateTime(2026, 8, 8, 2, 0, 0, DateTimeKind.Utc);
        var board = Board(createdAt, extended);

        var deadline = KickoffDeadline.Resolve(board.CreatedAt, board.KickoffTimeoutDateTime, DefaultBoardTimeoutMinutes);
        Assert.Null(KickoffDeadline.ExtensionFor(new DateTime(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc), deadline));
        Assert.Equal(extended, board.KickoffTimeoutDateTime);
    }

    // ── The reset job sees the extended deadline ──────────────────────────────────────

    // Replicates the reset query's WHERE clause (Worker.ResetStaleKickoffBoardsAsync) and
    // KickoffResetService's eligibility check, which now share KickoffDeadline.Resolve.
    private static bool IsStaleKickoffCandidate(ProjectBoard board, DateTime nowUtc, int timeoutMinutes = DefaultBoardTimeoutMinutes) =>
        board.KickoffState != null
        && board.KickoffState < 2
        && !board.IsStale
        && KickoffDeadline.Resolve(board.CreatedAt, board.KickoffTimeoutDateTime, timeoutMinutes) < nowUtc;

    [Fact]
    public void ResetCandidate_Excludes_ExtendedBoardPastItsOriginalThreeDayWindow()
    {
        var createdAt = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        var board = Board(createdAt, kickoffTimeoutDateTime: new DateTime(2026, 8, 8, 2, 0, 0, DateTimeKind.Utc));

        // Aug 5: four days in, well past the default 3-day deadline the board would otherwise have.
        var now = new DateTime(2026, 8, 5, 9, 0, 0, DateTimeKind.Utc);
        Assert.True(KickoffDeadline.Resolve(createdAt, null, DefaultBoardTimeoutMinutes) < now); // would have been stale
        Assert.False(IsStaleKickoffCandidate(board, now));
    }

    [Fact]
    public void ResetCandidate_Matches_ExtendedBoardOnceTheExtendedDeadlinePasses()
    {
        var createdAt = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        var board = Board(createdAt, kickoffTimeoutDateTime: new DateTime(2026, 8, 8, 2, 0, 0, DateTimeKind.Utc));

        Assert.True(IsStaleKickoffCandidate(board, new DateTime(2026, 8, 8, 3, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void ResetCandidate_Excludes_ExtendedBoardDuringItsKickoffMeeting()
    {
        // The whole point of the grace period: the meeting is at Aug 7 14:00, so the board must
        // still be alive at Aug 7 15:00 while the squad is in it.
        var createdAt = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        var board = Board(createdAt, kickoffTimeoutDateTime: new DateTime(2026, 8, 7, 14, 0, 0, DateTimeKind.Utc).AddHours(KickoffDeadline.GraceHours));

        Assert.False(IsStaleKickoffCandidate(board, new DateTime(2026, 8, 7, 15, 0, 0, DateTimeKind.Utc)));
    }

    // ── Proposal window validation (BoardsController.SuggestKickoffDate) ──────────────

    private static bool IsAcceptableProposal(DateTime suggestedUtc, DateTime nowUtc) =>
        suggestedUtc > nowUtc && suggestedUtc <= nowUtc.AddDays(KickoffDeadline.MaxProposalDays);

    [Fact]
    public void Proposal_Rejects_TimeInThePast()
    {
        var now = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        Assert.False(IsAcceptableProposal(now.AddMinutes(-1), now));
    }

    [Fact]
    public void Proposal_Accepts_SixDaysOut_PastTheOldThreeDayCap()
    {
        // This is the case the old UI blocked outright.
        var now = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        Assert.True(IsAcceptableProposal(now.AddDays(6), now));
    }

    [Fact]
    public void Proposal_Accepts_ExactlySevenDaysOut()
    {
        var now = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        Assert.True(IsAcceptableProposal(now.AddDays(KickoffDeadline.MaxProposalDays), now));
    }

    [Fact]
    public void Proposal_Rejects_BeyondSevenDays()
    {
        var now = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        Assert.False(IsAcceptableProposal(now.AddDays(KickoffDeadline.MaxProposalDays).AddMinutes(1), now));
    }

    // ── UTC normalization of the inbound proposal ─────────────────────────────────────

    [Fact]
    public void AsUtc_PassesThroughUtcUnchanged()
    {
        var value = new DateTime(2026, 8, 7, 14, 0, 0, DateTimeKind.Utc);
        Assert.Equal(value, KickoffDeadline.AsUtc(value));
        Assert.Equal(DateTimeKind.Utc, KickoffDeadline.AsUtc(value).Kind);
    }

    [Fact]
    public void AsUtc_TakesUnspecifiedAtItsWord_RatherThanShiftingByServerTimeZone()
    {
        // The field is named SuggestedDateTimeUtc; a serializer that drops the Kind must not cause
        // the value to be reinterpreted as server-local and shifted by hours.
        var value = new DateTime(2026, 8, 7, 14, 0, 0, DateTimeKind.Unspecified);
        var normalized = KickoffDeadline.AsUtc(value);
        Assert.Equal(new DateTime(2026, 8, 7, 14, 0, 0, DateTimeKind.Utc), normalized);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
    }

    [Fact]
    public void AsUtc_ConvertsLocal()
    {
        var local = new DateTime(2026, 8, 7, 14, 0, 0, DateTimeKind.Local);
        var normalized = KickoffDeadline.AsUtc(local);
        Assert.Equal(local.ToUniversalTime(), normalized);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
    }
}
