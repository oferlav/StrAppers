using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Unit tests for the kickoff-meeting-dispute state machine (MeetingDispute_Kickoff_Plan.md).
/// Follows this project's convention (see BoardStatesBranchFilterTests, UserStoryBoardControllerTests)
/// of replicating the exact logic under test against EF InMemory rather than exercising the real
/// controller/worker methods, since those depend on HTTP context, raw Postgres SQL ("FOR UPDATE",
/// interval arithmetic) and external services (Trello/SMTP) that EF InMemory cannot run.
/// </summary>
public class KickoffDisputeStateTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Student Student(int id, string boardId, bool approved = false) => new()
    {
        Id = id,
        FirstName = $"Student{id}",
        LastName = "Test",
        Email = $"student{id}@test.com",
        LinkedInUrl = "https://linkedin.com/in/test",
        GithubUser = $"gh{id}",
        MajorId = 1,
        YearId = 1,
        BoardId = boardId,
        ApprovedKickoff = approved
    };

    private static ProjectBoard Board(string id, int? kickoffState, DateTime createdAt, bool isStale = false,
        DateTime? suggestedKickoffDate = null, int? lastDateStudentId = null) => new()
    {
        Id = id,
        ProjectId = 1,
        KickoffState = kickoffState,
        CreatedAt = createdAt,
        IsStale = isStale,
        SuggestedKickoffDate = suggestedKickoffDate,
        LastDateStudentId = lastDateStudentId
    };

    // ── Replicates BoardsController.SuggestKickoffDate (kickoff-suggest) ──────────────

    private static bool CanSuggest(ProjectBoard board) =>
        board.KickoffState != null && board.KickoffState < 2 && !board.IsStale;

    private static async Task ApplySuggestAsync(ApplicationDbContext db, ProjectBoard board, int proposerId, DateTime suggestedUtc)
    {
        var squad = await db.Students.Where(s => s.BoardId == board.Id).ToListAsync();
        board.SuggestedKickoffDate = suggestedUtc;
        board.LastDateStudentId = proposerId;
        board.KickoffState = 1;
        foreach (var s in squad)
            s.ApprovedKickoff = s.Id == proposerId;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Suggest_FirstProposal_SetsState1_AutoApprovesProposer_ClearsOthers()
    {
        await using var db = CreateDb();
        const string boardId = "board1";
        var board = Board(boardId, kickoffState: 0, createdAt: DateTime.UtcNow);
        db.ProjectBoards.Add(board);
        db.Students.AddRange(Student(1, boardId), Student(2, boardId), Student(3, boardId));
        await db.SaveChangesAsync();

        Assert.True(CanSuggest(board));
        var suggested = DateTime.UtcNow.AddDays(1);
        await ApplySuggestAsync(db, board, proposerId: 1, suggested);

        Assert.Equal(1, board.KickoffState);
        Assert.Equal(suggested, board.SuggestedKickoffDate);
        Assert.Equal(1, board.LastDateStudentId);

        var students = await db.Students.Where(s => s.BoardId == boardId).ToListAsync();
        Assert.True(students.Single(s => s.Id == 1).ApprovedKickoff);
        Assert.False(students.Single(s => s.Id == 2).ApprovedKickoff);
        Assert.False(students.Single(s => s.Id == 3).ApprovedKickoff);
    }

    [Fact]
    public async Task Suggest_CounterProposal_ResetsPreviousApprovals()
    {
        await using var db = CreateDb();
        const string boardId = "board2";
        var board = Board(boardId, kickoffState: 1, createdAt: DateTime.UtcNow,
            suggestedKickoffDate: DateTime.UtcNow.AddDays(1), lastDateStudentId: 1);
        db.ProjectBoards.Add(board);
        // Student 1 proposed and is auto-approved; student 2 already approved; student 3 hasn't.
        db.Students.AddRange(Student(1, boardId, approved: true), Student(2, boardId, approved: true), Student(3, boardId));
        await db.SaveChangesAsync();

        var newSuggestion = DateTime.UtcNow.AddDays(2);
        await ApplySuggestAsync(db, board, proposerId: 2, newSuggestion);

        Assert.Equal(1, board.KickoffState);
        Assert.Equal(2, board.LastDateStudentId);
        Assert.Equal(newSuggestion, board.SuggestedKickoffDate);

        var students = await db.Students.Where(s => s.BoardId == boardId).ToListAsync();
        Assert.False(students.Single(s => s.Id == 1).ApprovedKickoff); // original proposer's approval cleared
        Assert.True(students.Single(s => s.Id == 2).ApprovedKickoff);  // new proposer auto-approved
        Assert.False(students.Single(s => s.Id == 3).ApprovedKickoff);
    }

    [Fact]
    public void Suggest_Rejected_WhenBoardIsLegacy_KickoffStateNull()
    {
        var board = Board("legacy1", kickoffState: null, createdAt: DateTime.UtcNow.AddYears(-1));
        Assert.False(CanSuggest(board));
    }

    [Fact]
    public void Suggest_Rejected_WhenAlreadyResolved()
    {
        var board = Board("board3", kickoffState: 2, createdAt: DateTime.UtcNow);
        Assert.False(CanSuggest(board));
    }

    [Fact]
    public void Suggest_Rejected_WhenBoardIsStale()
    {
        var board = Board("board4", kickoffState: 1, createdAt: DateTime.UtcNow.AddDays(-4), isStale: true);
        Assert.False(CanSuggest(board));
    }

    // ── Replicates BoardsController.ApproveKickoffDate (kickoff-approve) ──────────────

    private static async Task<bool> ApplyApproveAsync(ApplicationDbContext db, ProjectBoard board, int approverId)
    {
        var squad = await db.Students.Where(s => s.BoardId == board.Id).ToListAsync();
        var approver = squad.Single(s => s.Id == approverId);
        approver.ApprovedKickoff = true;
        var allApproved = squad.All(s => s.ApprovedKickoff);

        if (allApproved && board.SuggestedKickoffDate.HasValue)
        {
            board.KickoffState = 2;
            board.NextMeetingTime = board.SuggestedKickoffDate;
        }
        await db.SaveChangesAsync();
        return allApproved;
    }

    [Fact]
    public async Task Approve_NotUnanimous_StaysAtState1()
    {
        await using var db = CreateDb();
        const string boardId = "board5";
        var suggested = DateTime.UtcNow.AddDays(1);
        var board = Board(boardId, kickoffState: 1, createdAt: DateTime.UtcNow, suggestedKickoffDate: suggested, lastDateStudentId: 1);
        db.ProjectBoards.Add(board);
        db.Students.AddRange(Student(1, boardId, approved: true), Student(2, boardId), Student(3, boardId));
        await db.SaveChangesAsync();

        var allApproved = await ApplyApproveAsync(db, board, approverId: 2);

        Assert.False(allApproved);
        Assert.Equal(1, board.KickoffState);
        Assert.Null(board.NextMeetingTime);
    }

    [Fact]
    public async Task Approve_Unanimous_TransitionsToState2_SetsNextMeetingTime()
    {
        await using var db = CreateDb();
        const string boardId = "board6";
        var suggested = DateTime.UtcNow.AddDays(1);
        var board = Board(boardId, kickoffState: 1, createdAt: DateTime.UtcNow, suggestedKickoffDate: suggested, lastDateStudentId: 1);
        db.ProjectBoards.Add(board);
        db.Students.AddRange(Student(1, boardId, approved: true), Student(2, boardId, approved: true), Student(3, boardId));
        await db.SaveChangesAsync();

        // Last remaining squad member approves.
        var allApproved = await ApplyApproveAsync(db, board, approverId: 3);

        Assert.True(allApproved);
        Assert.Equal(2, board.KickoffState);
        Assert.Equal(suggested, board.NextMeetingTime);
    }

    // ── Replicates the reset-job WHERE clause (Worker.ResetStaleKickoffBoardsAsync) ────
    // Default timeout mirrors KickoffConfig2.BoardTimeout's default (4320 min = 3 days).

    private const int DefaultBoardTimeoutMinutes = 4320;

    private static bool IsStaleKickoffCandidate(ProjectBoard board, DateTime nowUtc, int timeoutMinutes = DefaultBoardTimeoutMinutes) =>
        board.KickoffState != null
        && board.KickoffState < 2
        && !board.IsStale
        && board.CreatedAt < nowUtc.AddMinutes(-timeoutMinutes);

    [Fact]
    public void ResetCandidate_MatchesBoard_PastDeadlineStillUnresolved()
    {
        var now = DateTime.UtcNow;
        var board = Board("stale1", kickoffState: 1, createdAt: now.AddDays(-4));
        Assert.True(IsStaleKickoffCandidate(board, now));
    }

    [Fact]
    public void ResetCandidate_Excludes_BoardStillWithinWindow()
    {
        var now = DateTime.UtcNow;
        var board = Board("fresh1", kickoffState: 0, createdAt: now.AddDays(-1));
        Assert.False(IsStaleKickoffCandidate(board, now));
    }

    [Fact]
    public void ResetCandidate_Excludes_ResolvedBoard()
    {
        var now = DateTime.UtcNow;
        var board = Board("resolved1", kickoffState: 2, createdAt: now.AddDays(-10));
        Assert.False(IsStaleKickoffCandidate(board, now));
    }

    [Fact]
    public void ResetCandidate_Excludes_LegacyBoard_KickoffStateNull()
    {
        var now = DateTime.UtcNow;
        var board = Board("legacy2", kickoffState: null, createdAt: now.AddYears(-1));
        Assert.False(IsStaleKickoffCandidate(board, now));
    }

    [Fact]
    public void ResetCandidate_Excludes_AlreadyProcessedStaleBoard()
    {
        var now = DateTime.UtcNow;
        var board = Board("stale2", kickoffState: 1, createdAt: now.AddDays(-10), isStale: true);
        Assert.False(IsStaleKickoffCandidate(board, now));
    }

    [Fact]
    public void ResetCandidate_RespectsConfiguredTimeout_ShorterThanDefault()
    {
        var now = DateTime.UtcNow;
        // Configured to 60 minutes: a board created 2 hours ago is stale even though it's well within 3 days.
        var board = Board("shorttimeout1", kickoffState: 0, createdAt: now.AddHours(-2));
        Assert.True(IsStaleKickoffCandidate(board, now, timeoutMinutes: 60));
        Assert.False(IsStaleKickoffCandidate(board, now)); // still within the default 3-day window
    }

    [Fact]
    public async Task ResetJob_ClearsStudentFields_ForStaleBoardMembers()
    {
        await using var db = CreateDb();
        const string boardId = "stale3";
        var now = DateTime.UtcNow;
        var board = Board(boardId, kickoffState: 1, createdAt: now.AddDays(-4));
        db.ProjectBoards.Add(board);
        var student = Student(1, boardId, approved: true);
        student.Status = 3;
        student.ProjectId = 42;
        student.InstitutePriority1 = 7;
        student.InstitutePriority2 = 8;
        db.Students.Add(student);
        await db.SaveChangesAsync();

        Assert.True(IsStaleKickoffCandidate(board, now));

        // Replicates the Students UPDATE half of Worker.ResetStaleKickoffBoardsAsync's CTE.
        board.IsStale = true;
        student.Status = 0;
        student.ProjectId = null;
        student.BoardId = null;
        student.InstitutePriority1 = null;
        student.InstitutePriority2 = null;
        student.InstitutePriority3 = null;
        student.InstitutePriority4 = null;
        student.ApprovedKickoff = false;
        await db.SaveChangesAsync();

        Assert.True(board.IsStale);
        Assert.Equal(0, student.Status);
        Assert.Null(student.ProjectId);
        Assert.Null(student.BoardId);
        Assert.Null(student.InstitutePriority1);
        Assert.Null(student.InstitutePriority2);
        Assert.Null(student.InstitutePriority3);
        Assert.Null(student.InstitutePriority4);
        Assert.False(student.ApprovedKickoff);
    }

    // ── New model field defaults (board creation now sets KickoffState=0, NextMeetingTime=null) ──

    [Fact]
    public void NewProjectBoard_DefaultsMatchBoardCreationBehavior()
    {
        var board = new ProjectBoard { Id = "newboard", KickoffState = 0, NextMeetingTime = null };
        Assert.Equal(0, board.KickoffState);
        Assert.Null(board.NextMeetingTime);
        Assert.False(board.IsStale);
        Assert.Null(board.SuggestedKickoffDate);
        Assert.Null(board.LastDateStudentId);
    }

    [Fact]
    public void NewProjectBoard_LegacyDefault_KickoffStateIsNull()
    {
        var board = new ProjectBoard { Id = "legacyboard" };
        Assert.Null(board.KickoffState);
    }

    [Fact]
    public void NewStudent_ApprovedKickoff_DefaultsFalse()
    {
        var student = Student(99, "someboard");
        Assert.False(student.ApprovedKickoff);
    }

    // ── Replicates StudentsController.ResetBoardIfKickoffExpiredAsync's early-return guards ──
    // (the login-triggered check, added as a faster complement to Worker's periodic sweep).
    // Same eligibility predicate as IsStaleKickoffCandidate, expressed as ResetBoardIfKickoffExpiredAsync
    // actually structures it (a sequence of "nothing to do" guards) rather than one combined condition.

    private static bool ShouldResetOnLogin(ProjectBoard board, DateTime nowUtc, int timeoutMinutes)
    {
        if (timeoutMinutes <= 0) return false;
        if (board.KickoffState == null || board.KickoffState >= 2 || board.IsStale) return false;
        if (nowUtc < board.CreatedAt.AddMinutes(timeoutMinutes)) return false;
        return true;
    }

    [Fact]
    public void LoginTrigger_MatchesWorkerJobEligibility_ForAPastDeadlineBoard()
    {
        var now = DateTime.UtcNow;
        var board = Board("logintrigger1", kickoffState: 1, createdAt: now.AddDays(-4));
        Assert.True(ShouldResetOnLogin(board, now, DefaultBoardTimeoutMinutes));
        Assert.Equal(IsStaleKickoffCandidate(board, now), ShouldResetOnLogin(board, now, DefaultBoardTimeoutMinutes));
    }

    [Fact]
    public void LoginTrigger_DoesNothing_WhenStillWithinWindow()
    {
        var now = DateTime.UtcNow;
        var board = Board("logintrigger2", kickoffState: 0, createdAt: now.AddHours(-1));
        Assert.False(ShouldResetOnLogin(board, now, DefaultBoardTimeoutMinutes));
    }

    [Fact]
    public void LoginTrigger_DoesNothing_ForAlreadyResolvedOrAlreadyStaleOrLegacyBoards()
    {
        var now = DateTime.UtcNow;
        Assert.False(ShouldResetOnLogin(Board("b1", kickoffState: 2, createdAt: now.AddDays(-10)), now, DefaultBoardTimeoutMinutes));
        Assert.False(ShouldResetOnLogin(Board("b2", kickoffState: 1, createdAt: now.AddDays(-10), isStale: true), now, DefaultBoardTimeoutMinutes));
        Assert.False(ShouldResetOnLogin(Board("b3", kickoffState: null, createdAt: now.AddYears(-1)), now, DefaultBoardTimeoutMinutes));
    }

    [Fact]
    public void LoginTrigger_Disabled_WhenTimeoutIsZeroOrNegative()
    {
        var now = DateTime.UtcNow;
        var board = Board("logintrigger3", kickoffState: 0, createdAt: now.AddYears(-1));
        Assert.False(ShouldResetOnLogin(board, now, timeoutMinutes: 0));
        Assert.False(ShouldResetOnLogin(board, now, timeoutMinutes: -5));
    }
}
