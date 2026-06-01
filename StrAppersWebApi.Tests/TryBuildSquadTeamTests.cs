using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using Xunit;

namespace StrAppersWebApi.Tests;

/// <summary>
/// Unit tests for the new TryBuildSquadTeam role-type logic in StudentTeamBuilderService.
///
/// Type mapping (RoleTypes seed):
///   0 = Default  (optional)
///   1 = bundle    (full-stack, counts toward developer rule)
///   2 = bundle    (FE or BE, needs 2 together to satisfy developer rule)
///   3 = Required  (mandatory slot)
///   4 = leadership (mandatory slot, same formation rule as 3)
/// </summary>
public class TryBuildSquadTeamTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static ApplicationDbContext CreateContext(string name)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name).Options;
        return new ApplicationDbContext(opts);
    }

    private static StudentTeamBuilderService CreateService(ApplicationDbContext ctx)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiBaseUrl"] = "http://localhost:5000"
            })
            .Build();

        return new StudentTeamBuilderService(
            ctx,
            new Mock<ITrelloSprintMergeService>().Object,
            new Mock<ISprintAssessmentService>().Object,
            new Mock<IHttpClientFactory>().Object,
            config,
            NullLogger<StudentTeamBuilderService>.Instance);
    }

    /// <summary>Seed one Role + one Student with that Role, return the student.</summary>
    private static async Task<Student> SeedStudentWithRole(
        ApplicationDbContext ctx,
        int studentId,
        int roleId,
        string roleName,
        int roleType)
    {
        if (!ctx.Roles.Any(r => r.Id == roleId))
            ctx.Roles.Add(new Role { Id = roleId, Name = roleName, Type = roleType });

        var student = new Student
        {
            Id = studentId,
            Email = $"s{studentId}@test.com",
            FirstName = "F",
            LastName = "L",
            StudentId = $"s{studentId}@test.com",
            LinkedInUrl = "https://linkedin.com/in/x",
            StudentRoles = new List<StudentRole>
            {
                new StudentRole { RoleId = roleId, IsActive = true,
                    Role = new Role { Id = roleId + 1000, Name = roleName, Type = roleType } }
            }
        };
        ctx.Students.Add(student);
        await ctx.SaveChangesAsync();
        return student;
    }

    private static InstituteTemplate MakeTemplate(InstituteSquad squad) =>
        new InstituteTemplate { Id = 1, InstituteId = 2, CourseType = "Squad", Squad = squad };

    // ── Mandatory roles (Type=3 and Type=4) ─────────────────────────────────

    [Fact]
    public async Task MandatoryRole_Type3_Missing_ReturnsNull()
    {
        using var ctx = CreateContext(nameof(MandatoryRole_Type3_Missing_ReturnsNull));
        var pm = await SeedStudentWithRole(ctx, 1, 10, "PM", roleType: 4);
        // No UI/UX candidate

        var squad = new InstituteSquad
        {
            Id = 1, InstituteId = 2, Name = "S", RequireDeveloperRule = false,
            Roles = new List<InstituteSquadRole>
            {
                new InstituteSquadRole { Id = 1, Name = "UI/UX Designer", Type = 3, IsActive = true },
                new InstituteSquadRole { Id = 2, Name = "PM", Type = 4, IsActive = true },
            }
        };

        var svc = CreateService(ctx);
        var result = svc.TestTryBuildSquadTeam(new List<Student> { pm }, MakeTemplate(squad), 2, 1);

        Assert.Null(result);
    }

    [Fact]
    public async Task MandatoryRole_Type4_Missing_ReturnsNull()
    {
        using var ctx = CreateContext(nameof(MandatoryRole_Type4_Missing_ReturnsNull));
        var uiux = await SeedStudentWithRole(ctx, 1, 10, "UI/UX Designer", roleType: 3);

        var squad = new InstituteSquad
        {
            Id = 1, InstituteId = 2, Name = "S", RequireDeveloperRule = false,
            Roles = new List<InstituteSquadRole>
            {
                new InstituteSquadRole { Id = 1, Name = "UI/UX Designer", Type = 3, IsActive = true },
                new InstituteSquadRole { Id = 2, Name = "PM", Type = 4, IsActive = true },
            }
        };

        var svc = CreateService(ctx);
        var result = svc.TestTryBuildSquadTeam(new List<Student> { uiux }, MakeTemplate(squad), 2, 1);

        Assert.Null(result);
    }

    [Fact]
    public async Task MandatoryRoles_BothPresent_ReturnsTeam()
    {
        using var ctx = CreateContext(nameof(MandatoryRoles_BothPresent_ReturnsTeam));
        var uiux = await SeedStudentWithRole(ctx, 1, 10, "UI/UX Designer", roleType: 3);
        var pm   = await SeedStudentWithRole(ctx, 2, 11, "PM", roleType: 4);

        var squad = new InstituteSquad
        {
            Id = 1, InstituteId = 2, Name = "S", RequireDeveloperRule = false,
            Roles = new List<InstituteSquadRole>
            {
                new InstituteSquadRole { Id = 1, Name = "UI/UX Designer", Type = 3, IsActive = true },
                new InstituteSquadRole { Id = 2, Name = "PM", Type = 4, IsActive = true },
            }
        };

        var svc = CreateService(ctx);
        var result = svc.TestTryBuildSquadTeam(new List<Student> { uiux, pm }, MakeTemplate(squad), 2, 1);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
    }

    // ── Optional roles (Type=0) ──────────────────────────────────────────────

    [Fact]
    public async Task OptionalRole_Missing_TeamStillForms()
    {
        using var ctx = CreateContext(nameof(OptionalRole_Missing_TeamStillForms));
        var pm = await SeedStudentWithRole(ctx, 1, 10, "PM", roleType: 4);

        var squad = new InstituteSquad
        {
            Id = 1, InstituteId = 2, Name = "S", RequireDeveloperRule = false,
            Roles = new List<InstituteSquadRole>
            {
                new InstituteSquadRole { Id = 1, Name = "PM", Type = 4, IsActive = true },
                new InstituteSquadRole { Id = 2, Name = "Analyst", Type = 0, IsActive = true }, // optional, no candidate
            }
        };

        var svc = CreateService(ctx);
        var result = svc.TestTryBuildSquadTeam(new List<Student> { pm }, MakeTemplate(squad), 2, 1);

        Assert.NotNull(result);
        Assert.Single(result!); // only PM, Analyst slot skipped
    }

    // ── Developer bundle rule ────────────────────────────────────────────────

    [Fact]
    public async Task DeveloperRule_FullStack_Satisfies_Rule()
    {
        using var ctx = CreateContext(nameof(DeveloperRule_FullStack_Satisfies_Rule));
        var fs = await SeedStudentWithRole(ctx, 1, 10, "Full Stack", roleType: 1);

        var squad = new InstituteSquad
        {
            Id = 1, InstituteId = 2, Name = "S", RequireDeveloperRule = true,
            Roles = new List<InstituteSquadRole>
            {
                new InstituteSquadRole { Id = 1, Name = "Full Stack", Type = 1, IsActive = true },
            }
        };

        var svc = CreateService(ctx);
        var result = svc.TestTryBuildSquadTeam(new List<Student> { fs }, MakeTemplate(squad), 2, 1);

        Assert.NotNull(result);
        Assert.Single(result!);
    }

    [Fact]
    public async Task DeveloperRule_TwoBundleRoles_Satisfies_Rule()
    {
        using var ctx = CreateContext(nameof(DeveloperRule_TwoBundleRoles_Satisfies_Rule));
        var fe = await SeedStudentWithRole(ctx, 1, 10, "Frontend Dev", roleType: 2);
        var be = await SeedStudentWithRole(ctx, 2, 11, "Backend Dev", roleType: 2);

        var squad = new InstituteSquad
        {
            Id = 1, InstituteId = 2, Name = "S", RequireDeveloperRule = true,
            Roles = new List<InstituteSquadRole>
            {
                new InstituteSquadRole { Id = 1, Name = "Frontend Dev", Type = 2, IsActive = true },
                new InstituteSquadRole { Id = 2, Name = "Backend Dev",  Type = 2, IsActive = true },
            }
        };

        var svc = CreateService(ctx);
        var result = svc.TestTryBuildSquadTeam(new List<Student> { fe, be }, MakeTemplate(squad), 2, 1);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
    }

    [Fact]
    public async Task DeveloperRule_OneBundleOnly_Fails()
    {
        using var ctx = CreateContext(nameof(DeveloperRule_OneBundleOnly_Fails));
        var fe = await SeedStudentWithRole(ctx, 1, 10, "Frontend Dev", roleType: 2);

        var squad = new InstituteSquad
        {
            Id = 1, InstituteId = 2, Name = "S", RequireDeveloperRule = true,
            Roles = new List<InstituteSquadRole>
            {
                new InstituteSquadRole { Id = 1, Name = "Frontend Dev", Type = 2, IsActive = true },
                new InstituteSquadRole { Id = 2, Name = "Backend Dev",  Type = 2, IsActive = true },
            }
        };

        var svc = CreateService(ctx);
        // Only FE candidate — BE missing, Type=1 count=0, Type=2 count=1 → rule not met
        var result = svc.TestTryBuildSquadTeam(new List<Student> { fe }, MakeTemplate(squad), 2, 1);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeveloperRule_False_BundleRoleOptional()
    {
        using var ctx = CreateContext(nameof(DeveloperRule_False_BundleRoleOptional));
        var pm = await SeedStudentWithRole(ctx, 1, 10, "PM", roleType: 4);
        // No developer candidate

        var squad = new InstituteSquad
        {
            Id = 1, InstituteId = 2, Name = "S", RequireDeveloperRule = false,
            Roles = new List<InstituteSquadRole>
            {
                new InstituteSquadRole { Id = 1, Name = "PM", Type = 4, IsActive = true },
                new InstituteSquadRole { Id = 2, Name = "Full Stack", Type = 1, IsActive = true },
            }
        };

        var svc = CreateService(ctx);
        var result = svc.TestTryBuildSquadTeam(new List<Student> { pm }, MakeTemplate(squad), 2, 1);

        // RequireDeveloperRule=false → missing Full Stack doesn't block the team
        Assert.NotNull(result);
        Assert.Single(result!);
    }

    // ── Empty team guard ────────────────────────────────────────────────────

    [Fact]
    public void NoActiveRoles_ReturnsNull()
    {
        using var ctx = CreateContext(nameof(NoActiveRoles_ReturnsNull));
        var squad = new InstituteSquad
        {
            Id = 1, InstituteId = 2, Name = "S", RequireDeveloperRule = false,
            Roles = new List<InstituteSquadRole>()
        };

        var svc = CreateService(ctx);
        var result = svc.TestTryBuildSquadTeam(new List<Student>(), MakeTemplate(squad), 2, 1);

        Assert.Null(result);
    }
}
