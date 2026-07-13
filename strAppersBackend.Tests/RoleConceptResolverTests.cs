using Microsoft.EntityFrameworkCore;
using strAppersBackend.Controllers;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Utilities;

namespace strAppersBackend.Tests;

/// <summary>
/// Per-concept role resolution: the Roles catalog holds duplicate rows per conceptual role
/// (global default, institute copies, per-squad copies). "This role" always means all duplicates.
/// </summary>
public class RoleConceptResolverTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    /// <summary>Seeds the canonical duplicate landscape: global PM (1), squad copies (122, 128), unrelated FS (2).</summary>
    private static async Task SeedDuplicatePmRolesAsync(ApplicationDbContext db)
    {
        db.Roles.AddRange(
            new Role { Id = 1, Name = "Product Manager" },                                   // global default (B2C / InstituteId=1 world)
            new Role { Id = 122, Name = "Product Manager", InstituteId = 2, SquadId = 10 },  // squad copy
            new Role { Id = 128, Name = "product manager ", InstituteId = 2, SquadId = 11 }, // another squad copy (casing/space noise)
            new Role { Id = 2, Name = "Full Stack Developer" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Resolve_ReturnsAllDuplicates_ByNormalizedName()
    {
        using var db = CreateDb();
        await SeedDuplicatePmRolesAsync(db);

        var ids = await RoleConceptResolver.ResolveConceptRoleIdsAsync(db, 1);

        Assert.Equal(new[] { 1, 122, 128 }, ids.OrderBy(i => i));
    }

    [Fact]
    public async Task Resolve_FromAnyDuplicate_YieldsTheSameConceptSet()
    {
        using var db = CreateDb();
        await SeedDuplicatePmRolesAsync(db);

        var fromSquadCopy = await RoleConceptResolver.ResolveConceptRoleIdsAsync(db, 122);

        Assert.Equal(new[] { 1, 122, 128 }, fromSquadCopy.OrderBy(i => i));
    }

    [Fact]
    public async Task Resolve_UniqueRole_ReturnsJustItself()
    {
        using var db = CreateDb();
        await SeedDuplicatePmRolesAsync(db);

        var ids = await RoleConceptResolver.ResolveConceptRoleIdsAsync(db, 2);

        Assert.Equal(new[] { 2 }, ids);
    }

    [Fact]
    public async Task Resolve_UnknownRoleId_DegradesToItself()
    {
        using var db = CreateDb();

        var ids = await RoleConceptResolver.ResolveConceptRoleIdsAsync(db, 999);

        Assert.Equal(new[] { 999 }, ids);
    }
}

/// <summary>Assessment report role filter must match students holding ANY duplicate of the role.</summary>
public class AssessmentReportRoleConceptTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task RoleFilter_MatchesStudentHoldingDuplicateRow()
    {
        using var db = CreateDb();
        // Student holds the DEFAULT PM row (1); the dashboard dropdown passed the squad copy (122).
        var student = new Student
        {
            Id = 5, Email = "pm@test.com", FirstName = "P", LastName = "M",
            StudentId = "pm@test.com", LinkedInUrl = "https://linkedin.com/in/x",
            StudentRoles = new List<StudentRole> { new StudentRole { RoleId = 1, IsActive = true } }
        };
        db.Students.Add(student);
        db.CacheMetrics.Add(new CacheMetrics { Id = 1, BoardId = "b1", StudentId = 5, SprintNumber = 1, MetricId = 1, Student = student });
        await db.SaveChangesAsync();

        var conceptIds = new List<int> { 1, 122, 128 }; // resolver output for the PM concept
        var rows = await MetricsController.WhereStudentHasRoleConcept(db.CacheMetrics.AsNoTracking(), conceptIds).ToListAsync();

        Assert.Single(rows);
    }

    [Fact]
    public async Task RoleFilter_ExcludesOtherRoles_AndInactiveRoles()
    {
        using var db = CreateDb();
        var fs = new Student
        {
            Id = 6, Email = "fs@test.com", FirstName = "F", LastName = "S",
            StudentId = "fs@test.com", LinkedInUrl = "https://linkedin.com/in/x",
            StudentRoles = new List<StudentRole> { new StudentRole { RoleId = 2, IsActive = true } }
        };
        var inactivePm = new Student
        {
            Id = 7, Email = "old@test.com", FirstName = "O", LastName = "P",
            StudentId = "old@test.com", LinkedInUrl = "https://linkedin.com/in/x",
            StudentRoles = new List<StudentRole> { new StudentRole { RoleId = 1, IsActive = false } }
        };
        db.Students.AddRange(fs, inactivePm);
        db.CacheMetrics.AddRange(
            new CacheMetrics { Id = 1, BoardId = "b1", StudentId = 6, SprintNumber = 1, MetricId = 1, Student = fs },
            new CacheMetrics { Id = 2, BoardId = "b1", StudentId = 7, SprintNumber = 1, MetricId = 1, Student = inactivePm });
        await db.SaveChangesAsync();

        var rows = await MetricsController.WhereStudentHasRoleConcept(db.CacheMetrics.AsNoTracking(), new List<int> { 1, 122 }).ToListAsync();

        Assert.Empty(rows);
    }
}

/// <summary>Mentor prompt selection is per role concept: prompts attached to any duplicate row apply.</summary>
public class MentorPromptRoleConceptTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Prompts_AttachedToGlobalRow_ApplyToStudentHoldingSquadCopy()
    {
        using var db = CreateDb();
        db.Roles.AddRange(
            new Role { Id = 1, Name = "Product Manager" },
            new Role { Id = 122, Name = "Product Manager", InstituteId = 2, SquadId = 10 });
        var cat = new PromptCategory { CategoryId = 1, Name = "Guidance" };
        db.MentorPrompts.AddRange(
            new MentorPrompt { Id = 1, CategoryId = 1, Category = cat, PromptString = "generic", IsActive = true, RoleId = null },
            new MentorPrompt { Id = 2, CategoryId = 1, Category = cat, PromptString = "pm-specific", IsActive = true, RoleId = 1 });
        await db.SaveChangesAsync();

        // Student holds the squad copy (122); prompt is attached to the global row (1).
        var conceptIds = await strAppersBackend.Utilities.RoleConceptResolver.ResolveConceptRoleIdsAsync(db, 122);
        var prompts = await db.MentorPrompts
            .Where(m => m.IsActive && (m.RoleId == null || conceptIds.Contains(m.RoleId.Value)))
            .Select(m => m.PromptString)
            .ToListAsync();

        Assert.Contains("generic", prompts);
        Assert.Contains("pm-specific", prompts);
    }
}
