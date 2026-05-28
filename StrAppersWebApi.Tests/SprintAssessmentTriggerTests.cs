using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace StrAppersWebApi.Tests;

/// <summary>
/// Verifies that sprint-assessment auto-run is triggered only when a new Trello list was
/// actually created (ListCreated = true) and that the completed sprint number is N-1.
/// Also verifies SprintAssessmentService early-exit behaviour for empty data.
/// </summary>
public class SprintAssessmentTriggerTests
{
    // ── fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeMergeService : ITrelloSprintMergeService
    {
        public bool ListCreated { get; set; } = true;
        public Task<(bool Success, string? Error, int CardsCount, bool ListCreated)> ExecuteMergeSprintAsync(
            int projectId, string boardId, int sprintNumber, bool merge) =>
            Task.FromResult((true, (string?)null, 3, ListCreated));
    }

    private sealed class TrackingAssessmentService : ISprintAssessmentService
    {
        public List<(string BoardId, int Sprint)> Calls { get; } = new();
        public Task RunForBoardSprintAsync(string boardId, int completedSprintNumber, CancellationToken cancellationToken = default)
        {
            Calls.Add((boardId, completedSprintNumber));
            return Task.CompletedTask;
        }
    }

    private static ApplicationDbContext CreateDb(string? dbName = null)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(opts);
    }

    /// <summary>Seeds one board with one active student (status=3) and one due sprint merge row.</summary>
    private static async Task<(string BoardId, int SprintNumber)> SeedBoardWithDueSprintAsync(
        ApplicationDbContext db,
        int sprintNumber = 2)
    {
        var boardId = "board-test-001";
        db.ProjectBoards.Add(new ProjectBoard { Id = boardId, ProjectId = 1 });
        db.Students.Add(new Student
        {
            Id = 1,
            FirstName = "Test",
            LastName = "Student",
            Email = "test@example.com",
            BoardId = boardId,
            Status = 3,
        });
        // Sprint N-1 is due (its DueDate is in the past), sprint N has MergedAt=null
        db.ProjectBoardSprintMerges.Add(new ProjectBoardSprintMerge
        {
            ProjectBoardId = boardId,
            SprintNumber = sprintNumber - 1,
            DueDate = DateTime.UtcNow.AddDays(-1),
            MergedAt = DateTime.UtcNow.AddDays(-1)
        });
        db.ProjectBoardSprintMerges.Add(new ProjectBoardSprintMerge
        {
            ProjectBoardId = boardId,
            SprintNumber = sprintNumber,
            DueDate = null,
            MergedAt = null
        });
        await db.SaveChangesAsync();
        return (boardId, sprintNumber);
    }

    private static StudentTeamBuilderService BuildService(
        ApplicationDbContext db,
        ITrelloSprintMergeService mergeService,
        ISprintAssessmentService assessmentService) =>
        new(db, mergeService, assessmentService,
            null!, null!, NullLogger<StudentTeamBuilderService>.Instance);

    // ════════════════════════════════════════════════════════════════════════
    // Trigger logic
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunDueSprintMerges_TriggersAssessment_WhenListCreated()
    {
        await using var db = CreateDb();
        var (boardId, sprintNumber) = await SeedBoardWithDueSprintAsync(db, sprintNumber: 2);

        var mergeService = new FakeMergeService { ListCreated = true };
        var assessmentService = new TrackingAssessmentService();
        var svc = BuildService(db, mergeService, assessmentService);

        await svc.RunDueSprintMergesAsync();

        // Wait briefly for the fire-and-forget background task
        await Task.Delay(100);

        Assert.Single(assessmentService.Calls);
        Assert.Equal(boardId, assessmentService.Calls[0].BoardId);
        Assert.Equal(sprintNumber - 1, assessmentService.Calls[0].Sprint);
    }

    [Fact]
    public async Task RunDueSprintMerges_DoesNotTriggerAssessment_WhenListNotCreated()
    {
        await using var db = CreateDb();
        await SeedBoardWithDueSprintAsync(db, sprintNumber: 2);

        var mergeService = new FakeMergeService { ListCreated = false };
        var assessmentService = new TrackingAssessmentService();
        var svc = BuildService(db, mergeService, assessmentService);

        await svc.RunDueSprintMergesAsync();
        await Task.Delay(100);

        Assert.Empty(assessmentService.Calls);
    }

    [Fact]
    public async Task RunDueSprintMerges_CompletedSprintIsNMinus1()
    {
        await using var db = CreateDb();
        var (boardId, sprintNumber) = await SeedBoardWithDueSprintAsync(db, sprintNumber: 4);

        var mergeService = new FakeMergeService { ListCreated = true };
        var assessmentService = new TrackingAssessmentService();
        var svc = BuildService(db, mergeService, assessmentService);

        await svc.RunDueSprintMergesAsync();
        await Task.Delay(100);

        Assert.Single(assessmentService.Calls);
        Assert.Equal(3, assessmentService.Calls[0].Sprint); // sprint 4-1 = 3
    }

    // ════════════════════════════════════════════════════════════════════════
    // SprintAssessmentService early-exit cases (no Trello/LLM calls needed)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SprintAssessmentService_NoStudents_CompletesWithoutError()
    {
        await using var db = CreateDb();
        // Board exists but no students
        db.ProjectBoards.Add(new ProjectBoard { Id = "empty-board", ProjectId = 1 });
        db.Metrics.Add(new Metric { Id = 2, Name = "Gap analysis", Endpoint = "metrics/use/GapAnalysis" });
        await db.SaveChangesAsync();

        var svc = new SprintAssessmentService(new FakeScopeFactory(db), NullLogger<SprintAssessmentService>.Instance);
        // Should not throw
        await svc.RunForBoardSprintAsync("empty-board", 1);
    }

    [Fact]
    public async Task SprintAssessmentService_NoMetricsWithEndpoints_CompletesWithoutError()
    {
        await using var db = CreateDb();
        db.ProjectBoards.Add(new ProjectBoard { Id = "board-x", ProjectId = 1 });
        db.Students.Add(new Student { Id = 10, FirstName = "A", LastName = "B", Email = "a@b.com", BoardId = "board-x", Status = 3 });
        // Metric exists but has no endpoint
        db.Metrics.Add(new Metric { Id = 99, Name = "No endpoint metric", Endpoint = null });
        await db.SaveChangesAsync();

        var svc = new SprintAssessmentService(new FakeScopeFactory(db), NullLogger<SprintAssessmentService>.Instance);
        await svc.RunForBoardSprintAsync("board-x", 1);
    }

    // ── minimal IServiceScopeFactory fake for SprintAssessmentService tests ─

    private sealed class FakeScopeFactory : IServiceScopeFactory
    {
        private readonly ApplicationDbContext _db;
        public FakeScopeFactory(ApplicationDbContext db) => _db = db;

        public IServiceScope CreateScope() => new FakeScope(_db);

        private sealed class FakeScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; }
            public FakeScope(ApplicationDbContext db) => ServiceProvider = new FakeProvider(db);
            public void Dispose() { }
        }

        private sealed class FakeProvider : IServiceProvider
        {
            private readonly ApplicationDbContext _db;
            public FakeProvider(ApplicationDbContext db) => _db = db;
            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(ApplicationDbContext)) return _db;
                return null;
            }
        }
    }
}
