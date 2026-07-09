using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using strAppersBackend.Controllers;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using System.Text.Json;
using Xunit;

namespace StrAppersWebApi.Tests;

/// <summary>
/// Tests for GET /api/Students/use/institute/roles-by-coupon/{coupon}
/// </summary>
public class GetRolesByCouponTests
{
    private static ApplicationDbContext CreateContext(string name)
        => new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(name).Options);

    private static StudentsController CreateController(ApplicationDbContext ctx)
        => new StudentsController(
            ctx,
            NullLogger<StudentsController>.Instance,
            new Mock<IGitHubService>().Object,
            new Mock<IKickoffService>().Object,
            new Mock<IPasswordHasherService>().Object);

    // ── Invalid coupon ────────────────────────────────────────────────────

    [Fact]
    public async Task InvalidCoupon_ReturnsBadRequest()
    {
        using var ctx = CreateContext(nameof(InvalidCoupon_ReturnsBadRequest));
        var result = await CreateController(ctx).GetRolesByCoupon("NOTEXIST-1");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── No active template → falls back to global Roles ──────────────────

    [Fact]
    public async Task ValidCoupon_NoActiveTemplate_ReturnsDefaultRoles()
    {
        using var ctx = CreateContext(nameof(ValidCoupon_NoActiveTemplate_ReturnsDefaultRoles));
        ctx.Institutes.Add(new Institute { Id = 2, Name = "Uni" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 2, Title = "P", IsAvailable = true, Coupon = "UNI-1" });
        ctx.Roles.Add(new Role { Id = 10, Name = "Developer", Type = 1 });
        ctx.Roles.Add(new Role { Id = 11, Name = "Designer", Type = 3 });
        await ctx.SaveChangesAsync();

        var result = await CreateController(ctx).GetRolesByCoupon("UNI-1");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"source\":\"default\"", json);
        Assert.Contains("Developer", json);
        Assert.Contains("Designer", json);
    }

    // ── Active template with squad → returns squad roles ─────────────────

    [Fact]
    public async Task ValidCoupon_ActiveTemplateWithSquad_ReturnsSquadRoles()
    {
        using var ctx = CreateContext(nameof(ValidCoupon_ActiveTemplateWithSquad_ReturnsSquadRoles));
        ctx.Institutes.Add(new Institute { Id = 2, Name = "Uni" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 2, Title = "P", IsAvailable = true, Coupon = "UNI-1" });
        // Global roles with matching names — endpoint maps InstituteSquadRole → Role by name
        ctx.Roles.Add(new Role { Id = 10, Name = "PM", Type = 4 });
        ctx.Roles.Add(new Role { Id = 11, Name = "Dev", Type = 1 });

        var squad = new InstituteSquad { Id = 1, InstituteId = 2, Name = "Squad A" };
        squad.Roles.Add(new Role { Id = 101, InstituteId = 2, SquadId = 1, Name = "PM", Type = 4, IsActive = true });
        squad.Roles.Add(new Role { Id = 102, InstituteId = 2, SquadId = 1, Name = "Dev", Type = 1, IsActive = true });
        ctx.InstituteSquads.Add(squad);
        ctx.InstituteTemplates.Add(new InstituteTemplate
        {
            Id = 1, InstituteId = 2, InstituteProjectId = 1,
            CourseName = "Course", IsActive = true, SquadId = 1, Squad = squad
        });
        await ctx.SaveChangesAsync();

        var result = await CreateController(ctx).GetRolesByCoupon("UNI-1");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"source\":\"squad\"", json);
        Assert.Contains("PM", json);
        Assert.Contains("Dev", json);
        // A3: squad roles ARE Roles rows (SquadId FK) — the endpoint returns their own IDs directly.
        var doc = JsonDocument.Parse(json);
        var roles = doc.RootElement.GetProperty("roles").EnumerateArray().ToList();
        Assert.Contains(roles, r => r.GetProperty("id").GetInt32() == 101); // squad PM id
        Assert.Contains(roles, r => r.GetProperty("id").GetInt32() == 102); // squad Dev id
    }

    [Fact]
    public async Task ValidCoupon_ActiveTemplateWithSquad_DoesNotReturnGlobalRoles()
    {
        using var ctx = CreateContext(nameof(ValidCoupon_ActiveTemplateWithSquad_DoesNotReturnGlobalRoles));
        ctx.Institutes.Add(new Institute { Id = 2, Name = "Uni" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 2, Title = "P", IsAvailable = true, Coupon = "UNI-1" });
        ctx.Roles.Add(new Role { Id = 99, Name = "GlobalRole", Type = 0 });
        ctx.Roles.Add(new Role { Id = 100, Name = "SquadRole", Type = 3 }); // matching global role

        var squad = new InstituteSquad { Id = 1, InstituteId = 2, Name = "S" };
        squad.Roles.Add(new Role { Id = 101, InstituteId = 2, SquadId = 1, Name = "SquadRole", Type = 3, IsActive = true });
        ctx.InstituteSquads.Add(squad);
        ctx.InstituteTemplates.Add(new InstituteTemplate
        {
            Id = 1, InstituteId = 2, InstituteProjectId = 1,
            CourseName = "C", IsActive = true, SquadId = 1, Squad = squad
        });
        await ctx.SaveChangesAsync();

        var result = await CreateController(ctx).GetRolesByCoupon("UNI-1");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"source\":\"squad\"", json);
        Assert.DoesNotContain("GlobalRole", json);
        Assert.Contains("SquadRole", json);
    }

    // ── Inactive squad role excluded ──────────────────────────────────────

    [Fact]
    public async Task InactiveSquadRole_ExcludedFromResult()
    {
        using var ctx = CreateContext(nameof(InactiveSquadRole_ExcludedFromResult));
        ctx.Institutes.Add(new Institute { Id = 2, Name = "Uni" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 2, Title = "P", IsAvailable = true, Coupon = "UNI-1" });
        ctx.Roles.Add(new Role { Id = 10, Name = "ActiveRole", Type = 3 });
        // No global role for InactiveRole — it's inactive so it won't be looked up anyway

        var squad = new InstituteSquad { Id = 1, InstituteId = 2, Name = "S" };
        squad.Roles.Add(new Role { Id = 101, InstituteId = 2, SquadId = 1, Name = "ActiveRole", Type = 3, IsActive = true });
        squad.Roles.Add(new Role { Id = 102, InstituteId = 2, SquadId = 1, Name = "InactiveRole", Type = 0, IsActive = false });
        ctx.InstituteSquads.Add(squad);
        ctx.InstituteTemplates.Add(new InstituteTemplate
        {
            Id = 1, InstituteId = 2, InstituteProjectId = 1,
            CourseName = "C", IsActive = true, SquadId = 1, Squad = squad
        });
        await ctx.SaveChangesAsync();

        var result = await CreateController(ctx).GetRolesByCoupon("UNI-1");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("ActiveRole", json);
        Assert.DoesNotContain("InactiveRole", json);
    }

    // ── Union of roles across multiple projects sharing same coupon ───────

    [Fact]
    public async Task MultipleProjectsSameCoupon_ReturnsUnionOfDistinctRoles()
    {
        using var ctx = CreateContext(nameof(MultipleProjectsSameCoupon_ReturnsUnionOfDistinctRoles));
        ctx.Institutes.Add(new Institute { Id = 2, Name = "Uni" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 2, Title = "P1", IsAvailable = true, Coupon = "UNI-1" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 2, InstituteId = 2, Title = "P2", IsAvailable = true, Coupon = "UNI-1" });
        // Global roles matching squad role names
        ctx.Roles.Add(new Role { Id = 10, Name = "PM", Type = 4 });
        ctx.Roles.Add(new Role { Id = 11, Name = "Dev", Type = 1 });
        ctx.Roles.Add(new Role { Id = 12, Name = "Designer", Type = 3 });

        // A3 refactor: squad roles live in the Roles table (SquadId FK), not InstituteSquadRole.
        var squad1 = new InstituteSquad { Id = 1, InstituteId = 2, Name = "S1" };
        squad1.Roles.Add(new Role { Id = 101, InstituteId = 2, SquadId = 1, Name = "PM", Type = 4, IsActive = true });
        squad1.Roles.Add(new Role { Id = 102, InstituteId = 2, SquadId = 1, Name = "Dev", Type = 1, IsActive = true });

        var squad2 = new InstituteSquad { Id = 2, InstituteId = 2, Name = "S2" };
        squad2.Roles.Add(new Role { Id = 103, InstituteId = 2, SquadId = 2, Name = "PM", Type = 4, IsActive = true });
        squad2.Roles.Add(new Role { Id = 104, InstituteId = 2, SquadId = 2, Name = "Designer", Type = 3, IsActive = true });

        ctx.InstituteSquads.AddRange(squad1, squad2);
        ctx.InstituteTemplates.Add(new InstituteTemplate { Id = 1, InstituteId = 2, InstituteProjectId = 1, CourseName = "C1", IsActive = true, SquadId = 1, Squad = squad1 });
        ctx.InstituteTemplates.Add(new InstituteTemplate { Id = 2, InstituteId = 2, InstituteProjectId = 2, CourseName = "C2", IsActive = true, SquadId = 2, Squad = squad2 });
        await ctx.SaveChangesAsync();

        var result = await CreateController(ctx).GetRolesByCoupon("UNI-1");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        var doc = JsonDocument.Parse(json);
        var roles = doc.RootElement.GetProperty("roles").EnumerateArray().ToList();
        // PM deduplicated (by name), 3 distinct roles total
        Assert.Equal(1, roles.Count(r => r.GetProperty("name").GetString() == "PM"));
        Assert.Equal(3, roles.Count);
        // A3: returned IDs are the squad Role rows' own IDs (SquadId FK), not global Role IDs.
        Assert.Contains(roles, r => r.GetProperty("id").GetInt32() == 101 || r.GetProperty("id").GetInt32() == 103); // PM (either squad's row)
        Assert.Contains(roles, r => r.GetProperty("id").GetInt32() == 102); // Dev
        Assert.Contains(roles, r => r.GetProperty("id").GetInt32() == 104); // Designer
    }

    // ── Template without SquadId falls back to default ────────────────────

    [Fact]
    public async Task ActiveTemplateWithoutSquadId_FallsBackToDefaultRoles()
    {
        using var ctx = CreateContext(nameof(ActiveTemplateWithoutSquadId_FallsBackToDefaultRoles));
        ctx.Institutes.Add(new Institute { Id = 2, Name = "Uni" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 2, Title = "P", IsAvailable = true, Coupon = "UNI-1" });
        ctx.Roles.Add(new Role { Id = 10, Name = "DefaultRole", Type = 0 }); // InstituteId=null → global
        // Template with no SquadId, no institute base roles
        ctx.InstituteTemplates.Add(new InstituteTemplate
        {
            Id = 1, InstituteId = 2, InstituteProjectId = 1,
            CourseName = "C", IsActive = true, SquadId = null
        });
        await ctx.SaveChangesAsync();

        var result = await CreateController(ctx).GetRolesByCoupon("UNI-1");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"source\":\"default\"", json);
        Assert.Contains("DefaultRole", json);
    }

    // ── Scenario 2: institute has customised base roles, no squad ─────────

    [Fact]
    public async Task NoSquad_InstituteHasBaseRoles_ReturnsInstituteBaseRoles()
    {
        using var ctx = CreateContext(nameof(NoSquad_InstituteHasBaseRoles_ReturnsInstituteBaseRoles));
        ctx.Institutes.Add(new Institute { Id = 3, Name = "Tech Academy" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 3, Title = "P", IsAvailable = true, Coupon = "TECH-1" });

        // Global catalog roles
        ctx.Roles.Add(new Role { Id = 1, InstituteId = null, Name = "Backend Developer", Type = 1, IsActive = true });
        ctx.Roles.Add(new Role { Id = 2, InstituteId = null, Name = "Frontend Developer", Type = 1, IsActive = true });

        // Institute-customised base roles (InstituteId=3, SquadId=null)
        ctx.Roles.Add(new Role { Id = 100, InstituteId = 3, SquadId = null, Name = "Backend Developer", Type = 1, IsActive = true, CustomerEngagement = false, IsTechnical = true });
        ctx.Roles.Add(new Role { Id = 101, InstituteId = 3, SquadId = null, Name = "Frontend Developer", Type = 1, IsActive = true, CustomerEngagement = true,  IsTechnical = true });

        // Template exists but has no squad
        ctx.InstituteTemplates.Add(new InstituteTemplate
        {
            Id = 1, InstituteId = 3, InstituteProjectId = 1,
            CourseName = "Built-in Course", IsActive = true, SquadId = null
        });
        await ctx.SaveChangesAsync();

        var result = await CreateController(ctx).GetRolesByCoupon("TECH-1");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);

        // Must use institute base roles, not global
        Assert.Contains("\"source\":\"institute\"", json);
        var doc = JsonDocument.Parse(json);
        var roles = doc.RootElement.GetProperty("roles").EnumerateArray().ToList();
        Assert.Equal(2, roles.Count);
        Assert.Contains(roles, r => r.GetProperty("id").GetInt32() == 100);
        Assert.Contains(roles, r => r.GetProperty("id").GetInt32() == 101);
        // Global IDs must not appear
        Assert.DoesNotContain(roles, r => r.GetProperty("id").GetInt32() == 1);
        Assert.DoesNotContain(roles, r => r.GetProperty("id").GetInt32() == 2);
    }

    [Fact]
    public async Task NoSquad_NoInstituteBaseRoles_FallsBackToGlobal()
    {
        using var ctx = CreateContext(nameof(NoSquad_NoInstituteBaseRoles_FallsBackToGlobal));
        ctx.Institutes.Add(new Institute { Id = 3, Name = "Tech Academy" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 3, Title = "P", IsAvailable = true, Coupon = "TECH-1" });

        // Only global roles — no institute base roles for institute 3
        ctx.Roles.Add(new Role { Id = 1, InstituteId = null, Name = "Backend Developer", Type = 1, IsActive = true });
        ctx.Roles.Add(new Role { Id = 2, InstituteId = null, Name = "Frontend Developer", Type = 1, IsActive = true });

        ctx.InstituteTemplates.Add(new InstituteTemplate
        {
            Id = 1, InstituteId = 3, InstituteProjectId = 1,
            CourseName = "Built-in Course", IsActive = true, SquadId = null
        });
        await ctx.SaveChangesAsync();

        var result = await CreateController(ctx).GetRolesByCoupon("TECH-1");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"source\":\"default\"", json);
        var doc = JsonDocument.Parse(json);
        var roles = doc.RootElement.GetProperty("roles").EnumerateArray().ToList();
        Assert.Contains(roles, r => r.GetProperty("id").GetInt32() == 1);
        Assert.Contains(roles, r => r.GetProperty("id").GetInt32() == 2);
    }

    [Fact]
    public async Task SquadRoles_TakePriorityOverInstituteBaseRoles()
    {
        using var ctx = CreateContext(nameof(SquadRoles_TakePriorityOverInstituteBaseRoles));
        ctx.Institutes.Add(new Institute { Id = 3, Name = "Tech Academy" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 3, Title = "P", IsAvailable = true, Coupon = "TECH-1" });

        // Institute base role
        ctx.Roles.Add(new Role { Id = 100, InstituteId = 3, SquadId = null, Name = "Backend Developer", Type = 1, IsActive = true });

        // Squad-scoped role (should win)
        var squad = new InstituteSquad { Id = 1, InstituteId = 3, Name = "Squad A", IsActive = true };
        ctx.InstituteSquads.Add(squad);
        ctx.Roles.Add(new Role { Id = 200, InstituteId = 3, SquadId = 1, Name = "Backend Developer", Type = 1, IsActive = true });

        ctx.InstituteTemplates.Add(new InstituteTemplate
        {
            Id = 1, InstituteId = 3, InstituteProjectId = 1,
            CourseName = "Course", IsActive = true, SquadId = 1
        });
        await ctx.SaveChangesAsync();

        var result = await CreateController(ctx).GetRolesByCoupon("TECH-1");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"source\":\"squad\"", json);
        var doc = JsonDocument.Parse(json);
        var roles = doc.RootElement.GetProperty("roles").EnumerateArray().ToList();
        Assert.Contains(roles, r => r.GetProperty("id").GetInt32() == 200); // squad role wins
        Assert.DoesNotContain(roles, r => r.GetProperty("id").GetInt32() == 100); // base role not returned
    }
}
