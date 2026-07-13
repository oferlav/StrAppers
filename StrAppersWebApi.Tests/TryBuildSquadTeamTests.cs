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
            NullLogger<StudentTeamBuilderService>.Instance,
            Microsoft.Extensions.Options.Options.Create(new KickoffConfig()),
            new Mock<ISmtpEmailService>().Object);
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
            Roles = new List<Role>
            {
                new Role { Id = 1, Name = "UI/UX Designer", Type = 3, IsActive = true },
                new Role { Id = 2, Name = "PM", Type = 4, IsActive = true },
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
            Roles = new List<Role>
            {
                new Role { Id = 1, Name = "UI/UX Designer", Type = 3, IsActive = true },
                new Role { Id = 2, Name = "PM", Type = 4, IsActive = true },
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
            Roles = new List<Role>
            {
                new Role { Id = 1, Name = "UI/UX Designer", Type = 3, IsActive = true },
                new Role { Id = 2, Name = "PM", Type = 4, IsActive = true },
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
            Roles = new List<Role>
            {
                new Role { Id = 1, Name = "PM", Type = 4, IsActive = true },
                new Role { Id = 2, Name = "Analyst", Type = 0, IsActive = true }, // optional, no candidate
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
            Roles = new List<Role>
            {
                new Role { Id = 1, Name = "Full Stack", Type = 1, IsActive = true },
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
            Roles = new List<Role>
            {
                new Role { Id = 1, Name = "Frontend Dev", Type = 2, IsActive = true },
                new Role { Id = 2, Name = "Backend Dev",  Type = 2, IsActive = true },
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
            Roles = new List<Role>
            {
                new Role { Id = 1, Name = "Frontend Dev", Type = 2, IsActive = true },
                new Role { Id = 2, Name = "Backend Dev",  Type = 2, IsActive = true },
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
            Roles = new List<Role>
            {
                new Role { Id = 1, Name = "PM", Type = 4, IsActive = true },
                new Role { Id = 2, Name = "Full Stack", Type = 1, IsActive = true },
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
            Roles = new List<Role>()
        };

        var svc = CreateService(ctx);
        var result = svc.TestTryBuildSquadTeam(new List<Student>(), MakeTemplate(squad), 2, 1);

        Assert.Null(result);
    }
}

/// <summary>
/// Developer exclusivity rule (ported from Worker.SelectGroup): a team gets EITHER one full-stack
/// OR the FE+BE pair — never both. Covers the squad path, the built-in path, and the wait-time
/// resolution when both sides have candidates.
/// </summary>
public class DeveloperExclusivityRuleTests
{
    private static ApplicationDbContext CreateContext(string name) =>
        new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name).Options);

    private static StudentTeamBuilderService CreateService(ApplicationDbContext ctx)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ApiBaseUrl"] = "http://localhost:5000" })
            .Build();
        return new StudentTeamBuilderService(
            ctx,
            new Mock<ITrelloSprintMergeService>().Object,
            new Mock<ISprintAssessmentService>().Object,
            new Mock<IHttpClientFactory>().Object,
            config,
            NullLogger<StudentTeamBuilderService>.Instance,
            Microsoft.Extensions.Options.Options.Create(new KickoffConfig { RequireDeveloperRule = true, MinimumStudents = 1 }),
            new Mock<ISmtpEmailService>().Object);
    }

    private static Student MakeStudent(int id, string roleName, int roleType, DateTime? startPendingAt = null) => new()
    {
        Id = id,
        Email = $"s{id}@test.com",
        FirstName = "F",
        LastName = "L",
        StudentId = $"s{id}@test.com",
        LinkedInUrl = "https://linkedin.com/in/x",
        StartPendingAt = startPendingAt,
        StudentRoles = new List<StudentRole>
        {
            new StudentRole { RoleId = id + 100, IsActive = true,
                Role = new Role { Id = id + 100, Name = roleName, Type = roleType } }
        }
    };

    private static List<Role> DevRoles() => new()
    {
        new Role { Id = 1, Name = "Full Stack Developer", Type = 1, IsActive = true },
        new Role { Id = 2, Name = "Frontend Developer",   Type = 2, IsActive = true },
        new Role { Id = 3, Name = "Backend Developer",    Type = 2, IsActive = true },
    };

    private static InstituteTemplate SquadTemplate(bool requireRule) => new()
    {
        Id = 1, InstituteId = 2, CourseType = "Squad",
        Squad = new InstituteSquad { Id = 1, InstituteId = 2, Name = "S", RequireDeveloperRule = requireRule, Roles = DevRoles() }
    };

    // ── The bug: FS + FE + BE triple must never all be seated ────────────────

    [Fact]
    public void Squad_Triple_NeverSeatsAllThree()
    {
        using var ctx = CreateContext(nameof(Squad_Triple_NeverSeatsAllThree));
        var fs = MakeStudent(1, "Full Stack Developer", 1);
        var fe = MakeStudent(2, "Frontend Developer", 2);
        var be = MakeStudent(3, "Backend Developer", 2);

        var team = CreateService(ctx).TestTryBuildSquadTeam(new List<Student> { fs, fe, be }, SquadTemplate(true), 2, 1);

        Assert.NotNull(team);
        // Either [fs] alone or [fe, be] — never all three.
        Assert.True(team!.Count == 1 || team.Count == 2);
        var hasFs = team.Any(s => s.Id == 1);
        var hasPair = team.Any(s => s.Id == 2) || team.Any(s => s.Id == 3);
        Assert.False(hasFs && hasPair, "full-stack and FE/BE must never be seated together");
    }

    [Fact]
    public void Squad_Triple_Tie_KeepsFullStack()
    {
        using var ctx = CreateContext(nameof(Squad_Triple_Tie_KeepsFullStack));
        var fs = MakeStudent(1, "Full Stack Developer", 1);
        var fe = MakeStudent(2, "Frontend Developer", 2);
        var be = MakeStudent(3, "Backend Developer", 2);

        var team = CreateService(ctx).TestTryBuildSquadTeam(new List<Student> { fs, fe, be }, SquadTemplate(true), 2, 1);

        Assert.NotNull(team);
        Assert.Single(team!);
        Assert.Equal(1, team![0].Id); // worker default: ties keep the full-stack
    }

    [Fact]
    public void Squad_Triple_PairWaitedLonger_KeepsPair()
    {
        using var ctx = CreateContext(nameof(Squad_Triple_PairWaitedLonger_KeepsPair));
        var now = DateTime.UtcNow;
        var fs = MakeStudent(1, "Full Stack Developer", 1, startPendingAt: now.AddHours(-1));
        var fe = MakeStudent(2, "Frontend Developer", 2, startPendingAt: now.AddHours(-48));
        var be = MakeStudent(3, "Backend Developer", 2, startPendingAt: now.AddHours(-2));

        var team = CreateService(ctx).TestTryBuildSquadTeam(new List<Student> { fs, fe, be }, SquadTemplate(true), 2, 1);

        Assert.NotNull(team);
        Assert.Equal(2, team!.Count);
        Assert.DoesNotContain(team, s => s.Id == 1);
    }

    [Fact]
    public void Squad_FullStackWithPartialPair_KeepsFullStackOnly()
    {
        using var ctx = CreateContext(nameof(Squad_FullStackWithPartialPair_KeepsFullStackOnly));
        var fs = MakeStudent(1, "Full Stack Developer", 1);
        var fe = MakeStudent(2, "Frontend Developer", 2); // no backend candidate

        var team = CreateService(ctx).TestTryBuildSquadTeam(new List<Student> { fs, fe }, SquadTemplate(true), 2, 1);

        Assert.NotNull(team);
        Assert.Single(team!);
        Assert.Equal(1, team![0].Id);
    }

    [Fact]
    public void Squad_OnlyOnePairMember_NoFullStack_Rejected()
    {
        using var ctx = CreateContext(nameof(Squad_OnlyOnePairMember_NoFullStack_Rejected));
        var fe = MakeStudent(2, "Frontend Developer", 2);

        var team = CreateService(ctx).TestTryBuildSquadTeam(new List<Student> { fe }, SquadTemplate(true), 2, 1);

        Assert.Null(team);
    }

    [Fact]
    public void Squad_RuleOff_SeatsAllMatched()
    {
        using var ctx = CreateContext(nameof(Squad_RuleOff_SeatsAllMatched));
        var fs = MakeStudent(1, "Full Stack Developer", 1);
        var fe = MakeStudent(2, "Frontend Developer", 2);
        var be = MakeStudent(3, "Backend Developer", 2);

        var team = CreateService(ctx).TestTryBuildSquadTeam(new List<Student> { fs, fe, be }, SquadTemplate(false), 2, 1);

        Assert.NotNull(team);
        Assert.Equal(3, team!.Count); // rule disabled: legacy optional behavior preserved
    }

    // ── Built-in path (KickoffConfig gate) ────────────────────────────────────

    [Fact]
    public void BuiltIn_Triple_NeverSeatsAllThree()
    {
        using var ctx = CreateContext(nameof(BuiltIn_Triple_NeverSeatsAllThree));
        var fs = MakeStudent(1, "Full Stack Developer", 1);
        var fe = MakeStudent(2, "Frontend Developer", 2);
        var be = MakeStudent(3, "Backend Developer", 2);

        var team = CreateService(ctx).TestTryBuildBuiltInTeam(new List<Student> { fs, fe, be }, DevRoles(), 2, 1);

        Assert.NotNull(team);
        var hasFs = team!.Any(s => s.Id == 1);
        var hasPair = team.Any(s => s.Id == 2) || team.Any(s => s.Id == 3);
        Assert.False(hasFs && hasPair, "full-stack and FE/BE must never be seated together");
    }

    [Fact]
    public void BuiltIn_PairOnly_SeatsBoth()
    {
        using var ctx = CreateContext(nameof(BuiltIn_PairOnly_SeatsBoth));
        var fe = MakeStudent(2, "Frontend Developer", 2);
        var be = MakeStudent(3, "Backend Developer", 2);

        var team = CreateService(ctx).TestTryBuildBuiltInTeam(new List<Student> { fe, be }, DevRoles(), 2, 1);

        Assert.NotNull(team);
        Assert.Equal(2, team!.Count);
    }

    [Fact]
    public void BuiltIn_FullStackOnly_Seated()
    {
        using var ctx = CreateContext(nameof(BuiltIn_FullStackOnly_Seated));
        var fs = MakeStudent(1, "Full Stack Developer", 1);

        var team = CreateService(ctx).TestTryBuildBuiltInTeam(new List<Student> { fs }, DevRoles(), 2, 1);

        Assert.NotNull(team);
        Assert.Single(team!);
    }
}
