using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests the three QuestMode URL resolution code paths introduced in Phase 2:
///
///   1. GetRepositoryUrlsByRole (MentorController ~line 814)
///      QuestBoard overrides ProjectBoard URLs; role name drives FE/BE selection.
///
///   2. GetRepositoryUrlsByRoleAsync trigger conditions (MentorController ~line 846)
///      QuestBoard is only fetched when ProjectBoard.GithubBackendUrl is null
///      AND student.BoardId is set.
///
///   3. Inline URL resolution in AppendGitHubDeveloperArtifactsAsync (MetricsController.GapAnalysis ~line 1061)
///      and the Adherence method (MetricsController ~line 413).
///      Both follow the same pattern: use board URL when present; look up QuestBoard
///      when board URL is null and studentId > 0.
/// </summary>
public class QuestBoardUrlResolutionTests
{
    private static ApplicationDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(opts);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static Student MakeStudent(
        int id, string boardId, string? boardBeUrl = null, string? boardFeUrl = null,
        string? roleName = null)
    {
        var student = new Student
        {
            Id = id,
            FirstName = "Test", LastName = "Student",
            Email = $"s{id}@test.com", LinkedInUrl = "https://linkedin.com/in/test",
            GithubUser = $"ghuser{id}", MajorId = 1, YearId = 1,
            BoardId = boardId,
        };

        if (boardBeUrl != null || boardFeUrl != null)
        {
            student.ProjectBoard = new ProjectBoard
            {
                Id = boardId, ProjectId = 1,
                GithubBackendUrl = boardBeUrl,
                GithubFrontendUrl = boardFeUrl,
            };
        }

        if (roleName != null)
        {
            student.StudentRoles = new List<StudentRole>
            {
                new()
                {
                    Id = id * 100, StudentId = id, RoleId = id * 10, IsActive = true,
                    Role = new Role { Id = id * 10, Name = roleName, Type = 1 }
                }
            };
        }

        return student;
    }

    private static QuestBoard MakeQuestBoard(int studentId, string boardId,
        string? feUrl = "https://fe.quest", string? beUrl = "https://be.quest",
        string? webApiUrl = "https://api.quest", string? publishUrl = "https://pages.quest") =>
        new()
        {
            Id = studentId,
            StudentId = studentId,
            BoardId = boardId,
            GithubFrontendUrl = feUrl,
            GithubBackendUrl = beUrl,
            WebApiUrl = webApiUrl,
            PublishUrl = publishUrl,
        };

    // Replicates MentorController.GetRepositoryUrlsByRole ~lines 814-841
    private static (string? Fe, string? Be, bool IsFullstack) ResolveByRole(
        Student student, QuestBoard? questBoard = null)
    {
        if (student.ProjectBoard == null && questBoard == null)
            return (null, null, false);

        var effectiveFe = questBoard?.GithubFrontendUrl ?? student.ProjectBoard?.GithubFrontendUrl;
        var effectiveBe = questBoard?.GithubBackendUrl ?? student.ProjectBoard?.GithubBackendUrl;

        var activeRole = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive);
        var roleName = activeRole?.Role?.Name ?? "";

        if (string.IsNullOrEmpty(roleName))
            return (null, effectiveBe, false);

        var lower = roleName.ToLowerInvariant();
        if (lower.Contains("full") && lower.Contains("stack")) return (effectiveFe, effectiveBe, true);
        if (lower.Contains("frontend")) return (effectiveFe, null, false);
        if (lower.Contains("backend")) return (null, effectiveBe, false);
        return (null, effectiveBe, false);
    }

    // Replicates MentorController.GetRepositoryUrlsByRoleAsync ~lines 846-855
    private static async Task<(string? Fe, string? Be, bool IsFullstack)> ResolveByRoleAsync(
        ApplicationDbContext db, Student student)
    {
        QuestBoard? questBoard = null;
        if (student.ProjectBoard != null &&
            string.IsNullOrEmpty(student.ProjectBoard.GithubBackendUrl) &&
            !string.IsNullOrEmpty(student.BoardId))
        {
            questBoard = await db.QuestBoards.AsNoTracking()
                .FirstOrDefaultAsync(qb => qb.BoardId == student.BoardId && qb.StudentId == student.Id);
        }
        return ResolveByRole(student, questBoard);
    }

    // Replicates MetricsController.GapAnalysis.AppendGitHubDeveloperArtifactsAsync ~lines 1061-1068
    private static async Task<string?> ResolveGapAnalysisUrl(
        ApplicationDbContext db, ProjectBoard board, string boardId,
        bool isBackend, int studentId, CancellationToken ct = default)
    {
        string? repoUrl = isBackend ? board.GithubBackendUrl : board.GithubFrontendUrl;
        if (string.IsNullOrEmpty(repoUrl) && studentId > 0)
        {
            var qb = await db.QuestBoards.AsNoTracking()
                .FirstOrDefaultAsync(q => q.BoardId == boardId && q.StudentId == studentId, ct);
            if (qb != null)
                repoUrl = isBackend ? qb.GithubBackendUrl : qb.GithubFrontendUrl;
        }
        return repoUrl;
    }

    // Replicates MetricsController (Adherence) ~lines 413-420
    private static async Task<(string? BackendUrl, string? FrontendUrl)> ResolveAdherenceUrls(
        ApplicationDbContext db, ProjectBoard board, string boardId,
        int studentId, CancellationToken ct = default)
    {
        var backendUrl = board.GithubBackendUrl;
        var frontendUrl = board.GithubFrontendUrl;
        if (string.IsNullOrEmpty(backendUrl) && studentId > 0)
        {
            var qb = await db.QuestBoards.AsNoTracking()
                .FirstOrDefaultAsync(q => q.BoardId == boardId && q.StudentId == studentId, ct);
            if (qb != null) { backendUrl = qb.GithubBackendUrl; frontendUrl = qb.GithubFrontendUrl; }
        }
        return (backendUrl, frontendUrl);
    }

    // ── 1. GetRepositoryUrlsByRole — role routing + QuestBoard override ─────────

    [Fact]
    public void BackendRole_QuestBoard_ReturnsQuestBoardBeUrl()
    {
        var student = MakeStudent(1, "board1", boardBeUrl: null, roleName: "Backend Developer");
        var qb = MakeQuestBoard(1, "board1", beUrl: "https://be.quest");

        var (_, be, _) = ResolveByRole(student, qb);

        Assert.Equal("https://be.quest", be);
    }

    [Fact]
    public void FrontendRole_QuestBoard_ReturnsQuestBoardFeUrl()
    {
        var student = MakeStudent(1, "board1", boardFeUrl: null, roleName: "Frontend Developer");
        var qb = MakeQuestBoard(1, "board1", feUrl: "https://fe.quest");

        var (fe, _, _) = ResolveByRole(student, qb);

        Assert.Equal("https://fe.quest", fe);
    }

    [Fact]
    public void FullstackRole_QuestBoard_ReturnsBothUrls()
    {
        var student = MakeStudent(1, "board1", roleName: "Full Stack Developer");
        var qb = MakeQuestBoard(1, "board1", feUrl: "https://fe.quest", beUrl: "https://be.quest");

        var (fe, be, isFullstack) = ResolveByRole(student, qb);

        Assert.Equal("https://fe.quest", fe);
        Assert.Equal("https://be.quest", be);
        Assert.True(isFullstack);
    }

    [Fact]
    public void QuestBoard_OverridesProjectBoardUrls()
    {
        var student = MakeStudent(1, "board1",
            boardBeUrl: "https://old-be.example.com",
            boardFeUrl: "https://old-fe.example.com",
            roleName: "Backend Developer");
        var qb = MakeQuestBoard(1, "board1", beUrl: "https://quest-be.example.com");

        var (_, be, _) = ResolveByRole(student, qb);

        Assert.Equal("https://quest-be.example.com", be);
    }

    [Fact]
    public void NoQuestBoard_BackendRole_UsesProjectBoardUrl()
    {
        var student = MakeStudent(1, "board1",
            boardBeUrl: "https://shared-be.example.com",
            roleName: "Backend Developer");

        var (_, be, _) = ResolveByRole(student);

        Assert.Equal("https://shared-be.example.com", be);
    }

    [Fact]
    public void NoProjectBoard_NoQuestBoard_ReturnsAllNull()
    {
        var student = MakeStudent(1, "board1"); // no ProjectBoard, no QuestBoard

        var (fe, be, isFullstack) = ResolveByRole(student);

        Assert.Null(fe);
        Assert.Null(be);
        Assert.False(isFullstack);
    }

    [Fact]
    public void NoRoleName_ReturnsBeUrlOnly()
    {
        var student = MakeStudent(1, "board1",
            boardBeUrl: "https://be.example.com",
            boardFeUrl: "https://fe.example.com"); // no role

        var (fe, be, isFullstack) = ResolveByRole(student);

        Assert.Null(fe);
        Assert.Equal("https://be.example.com", be);
        Assert.False(isFullstack);
    }

    // ── 2. GetRepositoryUrlsByRoleAsync — QuestBoard DB lookup trigger ────────

    [Fact]
    public async Task NullBoardBeUrl_WithBoardId_FetchesQuestBoardFromDb()
    {
        await using var db = CreateDb();
        var student = MakeStudent(1, "board42",
            boardBeUrl: null,          // null → triggers lookup
            boardFeUrl: null,
            roleName: "Backend Developer");
        student.ProjectBoard = new ProjectBoard { Id = "board42", ProjectId = 1 }; // explicitly null BE URL
        var qb = MakeQuestBoard(1, "board42", beUrl: "https://quest-be.example.com");
        db.QuestBoards.Add(qb);
        await db.SaveChangesAsync();

        var (_, be, _) = await ResolveByRoleAsync(db, student);

        Assert.Equal("https://quest-be.example.com", be);
    }

    [Fact]
    public async Task NonNullBoardBeUrl_SkipsQuestBoardLookup()
    {
        await using var db = CreateDb();
        var student = MakeStudent(1, "board42",
            boardBeUrl: "https://shared-be.example.com",
            roleName: "Backend Developer");
        // QuestBoard exists but should NOT be fetched since board URL is non-null
        db.QuestBoards.Add(MakeQuestBoard(1, "board42", beUrl: "https://quest-be.example.com"));
        await db.SaveChangesAsync();

        var (_, be, _) = await ResolveByRoleAsync(db, student);

        Assert.Equal("https://shared-be.example.com", be);
    }

    [Fact]
    public async Task NullBoardId_SkipsQuestBoardLookup()
    {
        await using var db = CreateDb();
        var student = MakeStudent(1, null!, boardBeUrl: null, roleName: "Backend Developer");
        student.ProjectBoard = new ProjectBoard { Id = "", ProjectId = 1 };
        student.BoardId = null; // no board ID → cannot look up QuestBoard

        var (_, be, _) = await ResolveByRoleAsync(db, student);

        Assert.Null(be);
    }

    [Fact]
    public async Task NullProjectBoard_SkipsQuestBoardLookup()
    {
        await using var db = CreateDb();
        // No ProjectBoard at all → condition `student.ProjectBoard != null` is false
        var student = new Student
        {
            Id = 1, FirstName = "T", LastName = "S", Email = "t@s.com",
            LinkedInUrl = "https://li.com/t", GithubUser = "gh", MajorId = 1, YearId = 1,
            BoardId = "board42", ProjectBoard = null
        };
        db.QuestBoards.Add(MakeQuestBoard(1, "board42", beUrl: "https://quest-be.example.com"));
        await db.SaveChangesAsync();

        var (fe, be, _) = await ResolveByRoleAsync(db, student);

        Assert.Null(fe);
        Assert.Null(be);
    }

    // ── 3. GapAnalysis URL resolution ─────────────────────────────────────────

    [Fact]
    public async Task GapAnalysis_BoardHasBeUrl_UsesDirectly()
    {
        await using var db = CreateDb();
        var board = new ProjectBoard
            { Id = "b1", ProjectId = 1, GithubBackendUrl = "https://be.example.com" };

        var url = await ResolveGapAnalysisUrl(db, board, "b1", isBackend: true, studentId: 7);

        Assert.Equal("https://be.example.com", url);
    }

    [Fact]
    public async Task GapAnalysis_NullBeUrl_StudentIdZero_StaysNull()
    {
        await using var db = CreateDb();
        var board = new ProjectBoard { Id = "b1", ProjectId = 1, GithubBackendUrl = null };
        db.QuestBoards.Add(MakeQuestBoard(7, "b1", beUrl: "https://quest-be.example.com"));
        await db.SaveChangesAsync();

        var url = await ResolveGapAnalysisUrl(db, board, "b1", isBackend: true, studentId: 0);

        Assert.Null(url); // studentId 0 must not trigger lookup
    }

    [Fact]
    public async Task GapAnalysis_NullBeUrl_StudentIdSet_QuestBoardExists_ReturnsQuestUrl()
    {
        await using var db = CreateDb();
        var board = new ProjectBoard { Id = "b1", ProjectId = 1, GithubBackendUrl = null };
        db.QuestBoards.Add(MakeQuestBoard(7, "b1", beUrl: "https://quest-be.example.com"));
        await db.SaveChangesAsync();

        var url = await ResolveGapAnalysisUrl(db, board, "b1", isBackend: true, studentId: 7);

        Assert.Equal("https://quest-be.example.com", url);
    }

    [Fact]
    public async Task GapAnalysis_NullFeUrl_StudentIdSet_QuestBoardExists_ReturnsFrontendUrl()
    {
        await using var db = CreateDb();
        var board = new ProjectBoard { Id = "b1", ProjectId = 1, GithubFrontendUrl = null };
        db.QuestBoards.Add(MakeQuestBoard(7, "b1", feUrl: "https://quest-fe.example.com"));
        await db.SaveChangesAsync();

        var url = await ResolveGapAnalysisUrl(db, board, "b1", isBackend: false, studentId: 7);

        Assert.Equal("https://quest-fe.example.com", url);
    }

    [Fact]
    public async Task GapAnalysis_NullBeUrl_NoQuestBoard_StaysNull()
    {
        await using var db = CreateDb();
        var board = new ProjectBoard { Id = "b1", ProjectId = 1, GithubBackendUrl = null };

        var url = await ResolveGapAnalysisUrl(db, board, "b1", isBackend: true, studentId: 7);

        Assert.Null(url);
    }

    // ── 4. Adherence URL resolution ───────────────────────────────────────────

    [Fact]
    public async Task Adherence_BoardHasUrls_UsesDirectly()
    {
        await using var db = CreateDb();
        var board = new ProjectBoard
        {
            Id = "b2", ProjectId = 1,
            GithubBackendUrl = "https://be.example.com",
            GithubFrontendUrl = "https://fe.example.com"
        };

        var (be, fe) = await ResolveAdherenceUrls(db, board, "b2", studentId: 5);

        Assert.Equal("https://be.example.com", be);
        Assert.Equal("https://fe.example.com", fe);
    }

    [Fact]
    public async Task Adherence_NullBeUrl_StudentIdSet_QuestBoardExists_ReturnsQuestUrls()
    {
        await using var db = CreateDb();
        var board = new ProjectBoard { Id = "b2", ProjectId = 1, GithubBackendUrl = null, GithubFrontendUrl = null };
        db.QuestBoards.Add(MakeQuestBoard(5, "b2",
            feUrl: "https://quest-fe.example.com", beUrl: "https://quest-be.example.com"));
        await db.SaveChangesAsync();

        var (be, fe) = await ResolveAdherenceUrls(db, board, "b2", studentId: 5);

        Assert.Equal("https://quest-be.example.com", be);
        Assert.Equal("https://quest-fe.example.com", fe);
    }

    [Fact]
    public async Task Adherence_NullBeUrl_StudentIdZero_StaysNull()
    {
        await using var db = CreateDb();
        var board = new ProjectBoard { Id = "b2", ProjectId = 1, GithubBackendUrl = null, GithubFrontendUrl = null };
        db.QuestBoards.Add(MakeQuestBoard(5, "b2",
            feUrl: "https://quest-fe.example.com", beUrl: "https://quest-be.example.com"));
        await db.SaveChangesAsync();

        var (be, fe) = await ResolveAdherenceUrls(db, board, "b2", studentId: 0);

        Assert.Null(be);
        Assert.Null(fe);
    }

    [Fact]
    public async Task Adherence_NullBeUrl_WrongStudentId_StaysNull()
    {
        await using var db = CreateDb();
        var board = new ProjectBoard { Id = "b2", ProjectId = 1, GithubBackendUrl = null };
        db.QuestBoards.Add(MakeQuestBoard(5, "b2", beUrl: "https://quest-be.example.com")); // studentId=5
        await db.SaveChangesAsync();

        var (be, _) = await ResolveAdherenceUrls(db, board, "b2", studentId: 99); // wrong student

        Assert.Null(be);
    }

    // ── 5. QuestBoard DB sanity ───────────────────────────────────────────────

    [Fact]
    public async Task QuestBoard_SaveAndRetrieve_AllUrlsIntact()
    {
        await using var db = CreateDb();
        db.QuestBoards.Add(new QuestBoard
        {
            StudentId = 42, BoardId = "trello123",
            GithubFrontendUrl = "https://github.com/org/fe-trello123s42",
            GithubBackendUrl = "https://github.com/org/backend_trello123s42",
            WebApiUrl = "https://webapi-trello123s42.up.railway.app",
            PublishUrl = "https://org.github.io/fe-trello123s42",
            NeonProjectId = "proj-abc", NeonBranchId = "br-xyz",
        });
        await db.SaveChangesAsync();

        var saved = await db.QuestBoards.AsNoTracking()
            .FirstAsync(q => q.StudentId == 42 && q.BoardId == "trello123");

        Assert.Equal("https://github.com/org/fe-trello123s42", saved.GithubFrontendUrl);
        Assert.Equal("https://github.com/org/backend_trello123s42", saved.GithubBackendUrl);
        Assert.Equal("https://webapi-trello123s42.up.railway.app", saved.WebApiUrl);
        Assert.Equal("https://org.github.io/fe-trello123s42", saved.PublishUrl);
        Assert.Equal("proj-abc", saved.NeonProjectId);
        Assert.Equal("br-xyz", saved.NeonBranchId);
    }

    [Fact]
    public async Task QuestBoard_MultipleStudents_SameBoardId_EachReturnedByStudentId()
    {
        await using var db = CreateDb();
        db.QuestBoards.AddRange(
            MakeQuestBoard(1, "board99", beUrl: "https://be-s1.quest"),
            MakeQuestBoard(2, "board99", beUrl: "https://be-s2.quest"),
            MakeQuestBoard(3, "board99", beUrl: "https://be-s3.quest"),
            MakeQuestBoard(4, "board99", beUrl: "https://be-s4.quest")
        );
        await db.SaveChangesAsync();

        var s2 = await db.QuestBoards.AsNoTracking()
            .FirstOrDefaultAsync(q => q.BoardId == "board99" && q.StudentId == 2);
        var count = await db.QuestBoards.CountAsync(q => q.BoardId == "board99");

        Assert.Equal(4, count);
        Assert.NotNull(s2);
        Assert.Equal("https://be-s2.quest", s2!.GithubBackendUrl);
    }
}
