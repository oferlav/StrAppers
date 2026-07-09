using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using strAppersBackend.Controllers;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using Xunit;

namespace StrAppersWebApi.Tests;

/// <summary>
/// Tests for coupon filtering in GET /api/Projects/use/institute/available/{studentId}.
/// Verifies that students with a Coupon only see projects matching that coupon.
/// </summary>
public class InstituteCouponFilterTests
{
    private static ApplicationDbContext CreateContext(string name)
        => new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(name).Options);

    private static ProjectsController CreateProjectsController(ApplicationDbContext ctx)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Trello:UseDBProjectBoard"] = "false" })
            .Build();

        return new ProjectsController(
            ctx,
            NullLogger<ProjectsController>.Instance,
            new Mock<IDesignDocumentService>().Object,
            Options.Create(new BusinessLogicConfig()),
            Options.Create(new KickoffConfig()),
            Options.Create(new EngagementRulesConfig()),
            config,
            new Mock<IKickoffService>().Object,
            new Mock<IAIService>().Object,
            new Mock<IChatCompletionService>().Object,
            new Mock<IWebHostEnvironment>().Object,
            Options.Create(new ProjectsInstituteMaxLengthFieldsOptions()),
            new Mock<IAzureBlobStorageService>().Object,
            new Mock<ISmtpEmailService>().Object);
    }

    private static Student MakeInstituteStudent(int id, int instituteId, string? coupon = null) =>
        new Student
        {
            Id = id, InstituteId = instituteId, Coupon = coupon,
            Email = $"s{id}@test.com", FirstName = "F", LastName = "L",
            StudentId = $"s{id}@test.com", LinkedInUrl = "https://linkedin.com/in/x"
        };

    // ── InstituteId=1 → BadRequest ────────────────────────────────────────

    [Fact]
    public async Task InstituteId1_ReturnsBadRequest()
    {
        using var ctx = CreateContext(nameof(InstituteId1_ReturnsBadRequest));
        ctx.Students.Add(MakeInstituteStudent(1, instituteId: 1));
        await ctx.SaveChangesAsync();

        var result = await CreateProjectsController(ctx).GetAvailableInstituteProjectsForStudent(1);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── Student with no coupon → all available institute projects ─────────

    [Fact]
    public async Task NoCoupon_ReturnsAllAvailableInstituteProjects()
    {
        using var ctx = CreateContext(nameof(NoCoupon_ReturnsAllAvailableInstituteProjects));
        ctx.Students.Add(MakeInstituteStudent(1, instituteId: 2, coupon: null));
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 2, Title = "P1", IsAvailable = true, Coupon = "UNI-1" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 2, InstituteId = 2, Title = "P2", IsAvailable = true, Coupon = "UNI-2" });
        await ctx.SaveChangesAsync();

        var result = await CreateProjectsController(ctx).GetAvailableInstituteProjectsForStudent(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("P1", json);
        Assert.Contains("P2", json);
    }

    // ── Student with coupon → only matching projects ──────────────────────

    [Fact]
    public async Task WithCoupon_ReturnsOnlyMatchingProjects()
    {
        using var ctx = CreateContext(nameof(WithCoupon_ReturnsOnlyMatchingProjects));
        ctx.Students.Add(MakeInstituteStudent(1, instituteId: 2, coupon: "UNI-1"));
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 2, Title = "ProjectA", IsAvailable = true, Coupon = "UNI-1" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 2, InstituteId = 2, Title = "ProjectB", IsAvailable = true, Coupon = "UNI-2" });
        await ctx.SaveChangesAsync();

        var result = await CreateProjectsController(ctx).GetAvailableInstituteProjectsForStudent(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("ProjectA", json);
        Assert.DoesNotContain("ProjectB", json);
    }

    [Fact]
    public async Task WithCoupon_MultipleProjsSameCoupon_ReturnsAll()
    {
        using var ctx = CreateContext(nameof(WithCoupon_MultipleProjsSameCoupon_ReturnsAll));
        ctx.Students.Add(MakeInstituteStudent(1, instituteId: 2, coupon: "UNI-1"));
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 2, Title = "Alpha", IsAvailable = true, Coupon = "UNI-1" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 2, InstituteId = 2, Title = "Beta", IsAvailable = true, Coupon = "UNI-1" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 3, InstituteId = 2, Title = "Gamma", IsAvailable = true, Coupon = "UNI-2" });
        await ctx.SaveChangesAsync();

        var result = await CreateProjectsController(ctx).GetAvailableInstituteProjectsForStudent(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("Alpha", json);
        Assert.Contains("Beta", json);
        Assert.DoesNotContain("Gamma", json);
    }

    // ── IsAvailable=false excluded regardless of coupon ───────────────────

    [Fact]
    public async Task UnavailableProject_ExcludedEvenIfCouponMatches()
    {
        using var ctx = CreateContext(nameof(UnavailableProject_ExcludedEvenIfCouponMatches));
        ctx.Students.Add(MakeInstituteStudent(1, instituteId: 2, coupon: "UNI-1"));
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 2, Title = "Available", IsAvailable = true, Coupon = "UNI-1" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 2, InstituteId = 2, Title = "Hidden", IsAvailable = false, Coupon = "UNI-1" });
        await ctx.SaveChangesAsync();

        var result = await CreateProjectsController(ctx).GetAvailableInstituteProjectsForStudent(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("Available", json);
        Assert.DoesNotContain("Hidden", json);
    }

    // ── Projects from different institute never shown ─────────────────────

    [Fact]
    public async Task OtherInstituteProjects_NeverShown()
    {
        using var ctx = CreateContext(nameof(OtherInstituteProjects_NeverShown));
        ctx.Students.Add(MakeInstituteStudent(1, instituteId: 2, coupon: "UNI-1"));
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 2, Title = "Mine", IsAvailable = true, Coupon = "UNI-1" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 2, InstituteId = 3, Title = "OtherUni", IsAvailable = true, Coupon = "UNI-1" });
        await ctx.SaveChangesAsync();

        var result = await CreateProjectsController(ctx).GetAvailableInstituteProjectsForStudent(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("Mine", json);
        Assert.DoesNotContain("OtherUni", json);
    }
}
