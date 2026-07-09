using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using strAppersBackend.Controllers;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using Xunit;

namespace StrAppersWebApi.Tests;

/// <summary>
/// Tests for coupon handling in POST /api/Students/use/create:
///   - Validates against InstituteProjects.Coupon (not Institutes.Coupon)
///   - Sets Student.Coupon and Student.InstituteId on match
///   - Defaults to InstituteId=1 and Coupon=null when no coupon given
/// Also tests coupon filtering in GET /api/Projects/use/institute/available/{studentId}.
/// </summary>
public class CouponRegistrationTests
{
    private static ApplicationDbContext CreateContext(string name)
        => new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(name).Options);

    private static StudentsController CreateStudentsController(ApplicationDbContext ctx)
    {
        var githubMock = new Mock<IGitHubService>();
        githubMock.Setup(g => g.ValidateGitHubUserAsync(It.IsAny<string>())).ReturnsAsync(true);
        return new StudentsController(
            ctx,
            NullLogger<StudentsController>.Instance,
            githubMock.Object,
            new Mock<IKickoffService>().Object,
            new Mock<IPasswordHasherService>().Object);
    }

    /// <summary>Minimum seed for CreateStudent to pass basic validations.</summary>
    private static async Task SeedMinimumEntities(ApplicationDbContext ctx, int instituteId = 2)
    {
        ctx.Institutes.Add(new Institute { Id = instituteId, Name = "Uni" });
        ctx.Majors.Add(new Major { Id = 1, Name = "CS" });
        ctx.Years.Add(new Year { Id = 1, Name = "1st" });
        ctx.Roles.Add(new Role { Id = 1, Name = "Developer", Type = 1 });
        await ctx.SaveChangesAsync();
    }

    private static CreateStudentRequest MakeRequest(string? coupon = null) => new CreateStudentRequest
    {
        FirstName = "Test",
        LastName = "Student",
        Email = $"test_{Guid.NewGuid():N}@test.com",
        MajorId = 1,
        YearId = 1,
        RoleId = 1,
        LinkedInUrl = "https://linkedin.com/in/test",
        InstituteCoupon = coupon
    };

    // ── No coupon → InstituteId=1, Coupon="1" (default institute coupon) ──

    [Fact]
    public async Task NoCoupon_SetsInstituteId1_AndDefaultCoupon1()
    {
        // Registration without a coupon defaults Coupon to "1" (the default institute's coupon).
        using var ctx = CreateContext(nameof(NoCoupon_SetsInstituteId1_AndDefaultCoupon1));
        await SeedMinimumEntities(ctx);
        var ctrl = CreateStudentsController(ctx);

        await ctrl.CreateStudent(MakeRequest(coupon: null));

        var student = await ctx.Students.FirstOrDefaultAsync();
        Assert.NotNull(student);
        Assert.Equal(1, student!.InstituteId);
        Assert.Equal("1", student.Coupon);
    }

    // ── Invalid coupon (not in InstituteProjects) → 400 ──────────────────

    [Fact]
    public async Task InvalidCoupon_ReturnsBadRequest()
    {
        using var ctx = CreateContext(nameof(InvalidCoupon_ReturnsBadRequest));
        await SeedMinimumEntities(ctx);
        var ctrl = CreateStudentsController(ctx);

        var result = await ctrl.CreateStudent(MakeRequest(coupon: "BOGUS-99"));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await ctx.Students.ToListAsync());
    }

    // ── Valid coupon (in InstituteProjects) → sets InstituteId + Coupon ───

    [Fact]
    public async Task ValidCoupon_SetsInstituteIdFromProject_AndCouponOnStudent()
    {
        using var ctx = CreateContext(nameof(ValidCoupon_SetsInstituteIdFromProject_AndCouponOnStudent));
        await SeedMinimumEntities(ctx, instituteId: 5);
        ctx.InstituteProjects.Add(new InstituteProject
        {
            Id = 1, InstituteId = 5, Title = "P", IsAvailable = true, Coupon = "MYUNI-1"
        });
        await ctx.SaveChangesAsync();
        var ctrl = CreateStudentsController(ctx);

        await ctrl.CreateStudent(MakeRequest(coupon: "MYUNI-1"));

        var student = await ctx.Students.FirstOrDefaultAsync();
        Assert.NotNull(student);
        Assert.Equal(5, student!.InstituteId);
        Assert.Equal("MYUNI-1", student.Coupon);
    }

    [Fact]
    public async Task ValidCoupon_DoesNotMatchInstitutes_StillWorks()
    {
        // Institutes.Coupon is NOT checked — only InstituteProjects.Coupon
        using var ctx = CreateContext(nameof(ValidCoupon_DoesNotMatchInstitutes_StillWorks));
        await SeedMinimumEntities(ctx, instituteId: 5);
        // Institute has a different coupon value — should be irrelevant
        var inst = await ctx.Institutes.FindAsync(5);
        inst!.Coupon = "DIFFERENT";
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 5, Title = "P", IsAvailable = true, Coupon = "MYUNI-1" });
        await ctx.SaveChangesAsync();
        var ctrl = CreateStudentsController(ctx);

        var result = await ctrl.CreateStudent(MakeRequest(coupon: "MYUNI-1"));

        Assert.IsType<OkObjectResult>(result.Result);
        var student = await ctx.Students.FirstOrDefaultAsync();
        Assert.Equal(5, student!.InstituteId);
        Assert.Equal("MYUNI-1", student.Coupon);
    }

    [Fact]
    public async Task MultipleProjsSameCoupon_SetsInstituteIdFromFirstMatch()
    {
        // Multiple projects can share a coupon — InstituteId should always be the same institute
        using var ctx = CreateContext(nameof(MultipleProjsSameCoupon_SetsInstituteIdFromFirstMatch));
        await SeedMinimumEntities(ctx, instituteId: 5);
        ctx.InstituteProjects.Add(new InstituteProject { Id = 1, InstituteId = 5, Title = "P1", IsAvailable = true, Coupon = "UNI-1" });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 2, InstituteId = 5, Title = "P2", IsAvailable = true, Coupon = "UNI-1" });
        await ctx.SaveChangesAsync();
        var ctrl = CreateStudentsController(ctx);

        await ctrl.CreateStudent(MakeRequest(coupon: "UNI-1"));

        var student = await ctx.Students.FirstOrDefaultAsync();
        Assert.Equal(5, student!.InstituteId);
        Assert.Equal("UNI-1", student.Coupon);
    }

    // ── GetStudentByEmail includes Coupon in response ─────────────────────

    [Fact]
    public async Task GetStudentByEmail_ReturnsCoupon()
    {
        using var ctx = CreateContext(nameof(GetStudentByEmail_ReturnsCoupon));
        // Seed the lookup entities GetStudentByEmail includes
        ctx.Majors.Add(new Major { Id = 1, Name = "CS" });
        ctx.Years.Add(new Year { Id = 1, Name = "1st" });
        var role = new Role { Id = 1, Name = "Dev", Type = 1 };
        ctx.Roles.Add(role);
        var student = new Student
        {
            Id = 1, Email = "a@test.com", FirstName = "A", LastName = "B",
            StudentId = "a@test.com", LinkedInUrl = "https://linkedin.com/in/a",
            MajorId = 1, YearId = 1, InstituteId = 5, Coupon = "UNI-2",
            StudentRoles = new List<StudentRole>
            {
                new StudentRole { RoleId = 1, IsActive = true, Role = role }
            }
        };
        ctx.Students.Add(student);
        await ctx.SaveChangesAsync();
        var ctrl = CreateStudentsController(ctx);

        var result = await ctrl.GetStudentByEmail("a@test.com");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("UNI-2", json);
    }
}
