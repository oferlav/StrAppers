using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// Regression tests for the institute team-builder bug where a project's applicants who ranked
/// it at DIFFERENT InstitutePriority slots (e.g. Product Manager at P1, UI/UX Designer at P3)
/// were never evaluated together. RunInstituteTeamBuildingAsync used to process one priority
/// rank at a time (grouping by project only WITHIN that rank), so each single-rank pass saw an
/// incomplete roster and rejected it — even though the full applicant pool for the project,
/// pooled across all 4 ranks, satisfied every mandatory role.
///
/// Real-world case: students 103 (Product Manager), 157 (Marketing/BizDev), 161 (Full Stack
/// Developer) and 163 (UI/UX Designer) all ranked InstituteProject 60 — at ranks P1, P2, P1 and
/// P3 respectively — and no board was ever created for them.
/// </summary>
public class InstitutePriorityPoolingLogicTests
{
    private record Candidate(int StudentId, int? P1, int? P2, int? P3, int? P4);

    /// <summary>Mirrors the pooling shape in StudentTeamBuilderService.RunInstituteTeamBuildingAsync:
    /// expand each student's 4 priority slots and group by project, regardless of rank.</summary>
    private static List<(int ProjectId, List<int> StudentIds)> PoolByProject(IEnumerable<Candidate> students)
    {
        var expanded = students
            .SelectMany(s => new[]
            {
                (StudentId: s.StudentId, ProjectId: s.P1, Rank: 1),
                (StudentId: s.StudentId, ProjectId: s.P2, Rank: 2),
                (StudentId: s.StudentId, ProjectId: s.P3, Rank: 3),
                (StudentId: s.StudentId, ProjectId: s.P4, Rank: 4),
            })
            .Where(x => x.ProjectId != null)
            .ToList();

        return expanded
            .GroupBy(x => x.ProjectId!.Value)
            .Select(g => (g.Key, g.Select(x => x.StudentId).Distinct().OrderBy(id => id).ToList()))
            .ToList();
    }

    [Fact]
    public void StudentsRankingSameProjectAtDifferentRanks_ArePooledTogether()
    {
        var students = new[]
        {
            new Candidate(103, P1: 60, P2: null, P3: null, P4: null),
            new Candidate(157, P1: null, P2: 60, P3: null, P4: null),
            new Candidate(161, P1: 60, P2: null, P3: null, P4: null),
            new Candidate(163, P1: null, P2: null, P3: 60, P4: null),
        };

        var pooled = PoolByProject(students);

        var project60 = Assert.Single(pooled, p => p.ProjectId == 60);
        Assert.Equal(new List<int> { 103, 157, 161, 163 }, project60.StudentIds);
    }

    [Fact]
    public void OldPerRankGrouping_WouldHaveFragmentedTheSameSquad()
    {
        // Documents exactly what the buggy code computed: a separate, incomplete group per rank.
        var students = new[]
        {
            new Candidate(103, P1: 60, P2: null, P3: null, P4: null),
            new Candidate(157, P1: null, P2: 60, P3: null, P4: null),
            new Candidate(161, P1: 60, P2: null, P3: null, P4: null),
            new Candidate(163, P1: null, P2: null, P3: 60, P4: null),
        };

        List<int> AtRank(Func<Candidate, int?> getRank) =>
            students.Where(s => getRank(s) == 60).Select(s => s.StudentId).OrderBy(id => id).ToList();

        Assert.Equal(new List<int> { 103, 161 }, AtRank(s => s.P1)); // missing UI/UX + Marketing
        Assert.Equal(new List<int> { 157 }, AtRank(s => s.P2));     // alone
        Assert.Equal(new List<int> { 163 }, AtRank(s => s.P3));     // alone
    }
}

/// <summary>
/// End-to-end regression coverage for the same bug, exercising the real
/// RunInstituteTeamBuildingAsync method against an in-memory database.
/// </summary>
public class RunInstituteTeamBuildingAsyncPoolingTests
{
    private static string NewDbName() => Guid.NewGuid().ToString();

    private static ApplicationDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    private static StudentTeamBuilderService BuildService(ApplicationDbContext db, MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var kickoffConfig = Options.Create(new KickoffConfig { MinimumStudents = 2, RequireDeveloperRule = true });

        return new StudentTeamBuilderService(
            db,
            Mock.Of<ITrelloSprintMergeService>(),
            factory.Object,
            new ConfigurationBuilder().Build(),
            NullLogger<StudentTeamBuilderService>.Instance,
            kickoffConfig,
            Mock.Of<ISmtpEmailService>());
    }

    private static (Role Pm, Role UiUx, Role FullStack, Role Marketing) SeedGlobalBaseRoles(ApplicationDbContext db, int idOffset)
    {
        var pm = new Role { Id = idOffset + 1, Name = "Product Manager", Type = 4, InstituteId = null, SquadId = null, IsActive = true };
        var uiux = new Role { Id = idOffset + 2, Name = "UI/UX Designer", Type = 3, InstituteId = null, SquadId = null, IsActive = true };
        var fullStack = new Role { Id = idOffset + 3, Name = "Full Stack Developer", Type = 1, InstituteId = null, SquadId = null, IsActive = true };
        var marketing = new Role { Id = idOffset + 4, Name = "Marketing/BizDev", Type = 0, InstituteId = null, SquadId = null, IsActive = true };
        db.Roles.AddRange(pm, uiux, fullStack, marketing);
        return (pm, uiux, fullStack, marketing);
    }

    private static Student MakeStudent(
        int id, int instituteId, Role role,
        int? p1 = null, int? p2 = null, int? p3 = null, int? p4 = null,
        DateTime? startPendingAt = null)
    {
        var student = new Student
        {
            Id = id,
            FirstName = $"Student{id}",
            LastName = "Test",
            Email = $"student{id}@test.com",
            InstituteId = instituteId,
            Status = 1,
            InstitutePriority1 = p1,
            InstitutePriority2 = p2,
            InstitutePriority3 = p3,
            InstitutePriority4 = p4,
            StartPendingAt = startPendingAt ?? DateTime.UtcNow,
            Coupon = "1",
        };
        student.StudentRoles.Add(new StudentRole { StudentId = id, RoleId = role.Id, Role = role, IsActive = true });
        return student;
    }

    [Fact]
    public async Task PooledAcrossRanks_CreatesBoard_ForSquadSplitAcrossDifferentPriorityRanks()
    {
        var dbName = NewDbName();
        const int instituteId = 1;
        const int ipId = 60;

        using (var seed = CreateDb(dbName))
        {
            seed.Institutes.Add(new Institute { Id = instituteId, Name = "Test Institute", QuestMode = false });
            seed.InstituteProjects.Add(new InstituteProject
            {
                Id = ipId,
                InstituteId = instituteId,
                BaseProjectId = 999,
                Title = "MatchAble",
                IsAvailable = true,
            });
            var (pm, uiux, fullStack, marketing) = SeedGlobalBaseRoles(seed, idOffset: 0);

            // Mirrors production exactly: same project (60), different InstitutePriority ranks.
            seed.Students.Add(MakeStudent(103, instituteId, pm, p1: ipId));
            seed.Students.Add(MakeStudent(157, instituteId, marketing, p2: ipId));
            seed.Students.Add(MakeStudent(161, instituteId, fullStack, p1: ipId));
            seed.Students.Add(MakeStudent(163, instituteId, uiux, p3: ipId));

            await seed.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var handler = MockHttpMessageHandler.ReturnOk("""{"success":true}""");
        var service = BuildService(db, handler);

        var (created, _, messages) = await service.RunInstituteTeamBuildingAsync();

        Assert.Equal(1, created);
        Assert.Equal(1, handler.CallCount);
        Assert.Contains(messages, m => m.Contains("board created for 4 student"));
    }

    [Fact]
    public async Task AlreadyProcessedStudent_IsNotDoubleBooked_ViaALowerPriorityProject()
    {
        var dbName = NewDbName();
        const int instituteId = 1;
        const int projectA = 60;
        const int projectB = 61;
        var earlier = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var later = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        using (var seed = CreateDb(dbName))
        {
            seed.Institutes.Add(new Institute { Id = instituteId, Name = "Test Institute", QuestMode = false });
            seed.InstituteProjects.Add(new InstituteProject { Id = projectA, InstituteId = instituteId, BaseProjectId = 900, Title = "Project A", IsAvailable = true });
            seed.InstituteProjects.Add(new InstituteProject { Id = projectB, InstituteId = instituteId, BaseProjectId = 901, Title = "Project B", IsAvailable = true });
            var (pm, uiux, fullStack, _) = SeedGlobalBaseRoles(seed, idOffset: 0);

            // Shared PM ranks Project A first (P1) and Project B second (P2). Project A's other
            // two seats are P1-only for A; Project B's other two seats are P1-only for B, but
            // its StartPendingAt is later so Project A is evaluated first.
            seed.Students.Add(MakeStudent(1, instituteId, pm, p1: projectA, p2: projectB, startPendingAt: earlier));
            seed.Students.Add(MakeStudent(2, instituteId, uiux, p1: projectA, startPendingAt: earlier));
            seed.Students.Add(MakeStudent(3, instituteId, fullStack, p1: projectA, startPendingAt: earlier));
            seed.Students.Add(MakeStudent(4, instituteId, uiux, p1: projectB, startPendingAt: later));
            seed.Students.Add(MakeStudent(5, instituteId, fullStack, p1: projectB, startPendingAt: later));

            await seed.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var handler = MockHttpMessageHandler.ReturnOk("""{"success":true}""");
        var service = BuildService(db, handler);

        var (created, skipped, messages) = await service.RunInstituteTeamBuildingAsync();

        // Only Project A gets a board (using the shared PM); Project B is left one seat short
        // because the PM was already committed to Project A and must not be reused.
        Assert.Equal(1, created);
        Assert.True(skipped >= 1);
        Assert.Equal(1, handler.CallCount);
        Assert.Contains(messages, m => m.Contains($"IpId {projectA}") && m.Contains("board created"));
        Assert.DoesNotContain(messages, m => m.Contains($"IpId {projectB}") && m.Contains("board created"));
    }
}
