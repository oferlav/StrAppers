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

        var squad = new InstituteSquad { Id = 1, InstituteId = 2, Name = "Squad A" };
        squad.Roles.Add(new InstituteSquadRole { Id = 1, SquadId = 1, Name = "PM", Type = 4, IsActive = true });
        squad.Roles.Add(new InstituteSquadRole { Id = 2, SquadId = 1, Name = "Dev", Type = 1, IsActive = true });
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
    }

    [Fact]
    public async Task ValidCoupon_ActiveTemplateWithSquad_DoesNotReturnGlobalRoles()
    {
        using var ctx = CreateContext(nameof(ValidCoupon_ActiveTemplateWithSquad_DoesNotReturnGlobalRoles));
        ctx.Institutes.Add(new Institute { Id = 2, Name = "Uni" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 2, Title = "P", IsAvailable = true, Coupon = "UNI-1" });
        ctx.Roles.Add(new Role { Id = 99, Name = "GlobalRole", Type = 0 });

        var squad = new InstituteSquad { Id = 1, InstituteId = 2, Name = "S" };
        squad.Roles.Add(new InstituteSquadRole { Id = 1, SquadId = 1, Name = "SquadRole", Type = 3, IsActive = true });
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
    }

    // ── Inactive squad role excluded ──────────────────────────────────────

    [Fact]
    public async Task InactiveSquadRole_ExcludedFromResult()
    {
        using var ctx = CreateContext(nameof(InactiveSquadRole_ExcludedFromResult));
        ctx.Institutes.Add(new Institute { Id = 2, Name = "Uni" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 2, Title = "P", IsAvailable = true, Coupon = "UNI-1" });

        var squad = new InstituteSquad { Id = 1, InstituteId = 2, Name = "S" };
        squad.Roles.Add(new InstituteSquadRole { Id = 1, SquadId = 1, Name = "ActiveRole", Type = 3, IsActive = true });
        squad.Roles.Add(new InstituteSquadRole { Id = 2, SquadId = 1, Name = "InactiveRole", Type = 0, IsActive = false });
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

        var squad1 = new InstituteSquad { Id = 1, InstituteId = 2, Name = "S1" };
        squad1.Roles.Add(new InstituteSquadRole { Id = 1, SquadId = 1, Name = "PM", Type = 4, IsActive = true });
        squad1.Roles.Add(new InstituteSquadRole { Id = 2, SquadId = 1, Name = "Dev", Type = 1, IsActive = true });

        var squad2 = new InstituteSquad { Id = 2, InstituteId = 2, Name = "S2" };
        squad2.Roles.Add(new InstituteSquadRole { Id = 3, SquadId = 2, Name = "PM", Type = 4, IsActive = true }); // duplicate
        squad2.Roles.Add(new InstituteSquadRole { Id = 4, SquadId = 2, Name = "Designer", Type = 3, IsActive = true });

        ctx.InstituteSquads.AddRange(squad1, squad2);
        ctx.InstituteTemplates.Add(new InstituteTemplate { Id = 1, InstituteId = 2, InstituteProjectId = 1, CourseName = "C1", IsActive = true, SquadId = 1, Squad = squad1 });
        ctx.InstituteTemplates.Add(new InstituteTemplate { Id = 2, InstituteId = 2, InstituteProjectId = 2, CourseName = "C2", IsActive = true, SquadId = 2, Squad = squad2 });
        await ctx.SaveChangesAsync();

        var result = await CreateController(ctx).GetRolesByCoupon("UNI-1");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        // PM appears in both squads but should be deduplicated
        var doc = JsonDocument.Parse(json);
        var roles = doc.RootElement.GetProperty("roles").EnumerateArray().ToList();
        var pmCount = roles.Count(r => r.GetProperty("name").GetString() == "PM");
        Assert.Equal(1, pmCount);
        Assert.Equal(3, roles.Count); // PM, Dev, Designer
    }

    // ── Template without SquadId falls back to default ────────────────────

    [Fact]
    public async Task ActiveTemplateWithoutSquadId_FallsBackToDefaultRoles()
    {
        using var ctx = CreateContext(nameof(ActiveTemplateWithoutSquadId_FallsBackToDefaultRoles));
        ctx.Institutes.Add(new Institute { Id = 2, Name = "Uni" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 2, Title = "P", IsAvailable = true, Coupon = "UNI-1" });
        ctx.Roles.Add(new Role { Id = 10, Name = "DefaultRole", Type = 0 });
        // Template with no SquadId
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
}
