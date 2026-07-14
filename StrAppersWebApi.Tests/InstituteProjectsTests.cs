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
/// Unit tests for institute-scoped endpoints using in-memory EF + Moq stubs.
/// Tests controller methods directly — no HTTP server required.
/// </summary>
public class InstituteProjectsTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static ApplicationDbContext CreateContext(string dbName)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static StudentsController CreateStudentsController(ApplicationDbContext ctx)
        => new StudentsController(
            ctx,
            NullLogger<StudentsController>.Instance,
            new Mock<IGitHubService>().Object,
            new Mock<IKickoffService>().Object,
            new Mock<IPasswordHasherService>().Object);

    private static Student MakeStudent(int id, string email = "a@test.com") =>
        new Student { Id = id, Email = email, FirstName = "A", LastName = "B", StudentId = email, LinkedInUrl = "https://linkedin.com/in/a" };

    private static InstituteProject MakeInstituteProject(int id, int instituteId = 2, bool isAvailable = true) =>
        new InstituteProject { Id = id, InstituteId = instituteId, Title = $"Project{id}", IsAvailable = isAvailable };

    // ── GET /api/Students/use/institute/is-allocatable/{projectId}/{studentId} ──

    [Fact]
    public async Task IsInstituteAllocatable_StudentNotFound_ReturnsNotFound()
    {
        using var ctx = CreateContext(nameof(IsInstituteAllocatable_StudentNotFound_ReturnsNotFound));
        var result = await CreateStudentsController(ctx).IsStudentInstituteAllocatable(1, 999);
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task IsInstituteAllocatable_ProjectNotFound_ReturnsNotFound()
    {
        using var ctx = CreateContext(nameof(IsInstituteAllocatable_ProjectNotFound_ReturnsNotFound));
        ctx.Students.Add(MakeStudent(1));
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).IsStudentInstituteAllocatable(999, 1);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task IsInstituteAllocatable_AvailableProject_ReturnsTrue()
    {
        using var ctx = CreateContext(nameof(IsInstituteAllocatable_AvailableProject_ReturnsTrue));
        ctx.Students.Add(MakeStudent(1));
        ctx.InstituteProjects.Add(MakeInstituteProject(1, isAvailable: true));
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).IsStudentInstituteAllocatable(1, 1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"isAllocatable\":true", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IsInstituteAllocatable_UnavailableProject_ReturnsFalse()
    {
        using var ctx = CreateContext(nameof(IsInstituteAllocatable_UnavailableProject_ReturnsFalse));
        ctx.Students.Add(MakeStudent(1));
        ctx.InstituteProjects.Add(MakeInstituteProject(1, isAvailable: false));
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).IsStudentInstituteAllocatable(1, 1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"isAllocatable\":false", json, StringComparison.OrdinalIgnoreCase);
    }

    // ── POST /api/Students/use/institute/allocate/{projectId}/{studentId} ──

    [Fact]
    public async Task AllocateInstituteProject_StudentNotFound_ReturnsNotFound()
    {
        using var ctx = CreateContext(nameof(AllocateInstituteProject_StudentNotFound_ReturnsNotFound));
        var result = await CreateStudentsController(ctx).AllocateStudentToInstituteProject(1, 999, new AllocateStudentRequest());
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task AllocateInstituteProject_ProjectNotFound_ReturnsNotFound()
    {
        using var ctx = CreateContext(nameof(AllocateInstituteProject_ProjectNotFound_ReturnsNotFound));
        ctx.Students.Add(MakeStudent(1));
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).AllocateStudentToInstituteProject(999, 1, new AllocateStudentRequest());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task AllocateInstituteProject_FirstSlot_SetsInstitutePriority1()
    {
        using var ctx = CreateContext(nameof(AllocateInstituteProject_FirstSlot_SetsInstitutePriority1));
        ctx.Students.Add(MakeStudent(1));
        ctx.InstituteProjects.Add(MakeInstituteProject(5));
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).AllocateStudentToInstituteProject(5, 1, new AllocateStudentRequest());

        Assert.IsType<OkObjectResult>(result);
        var student = await ctx.Students.FindAsync(1);
        Assert.Equal(5, student!.InstitutePriority1);
        Assert.Null(student.InstitutePriority2);
        Assert.Null(student.InstitutePriority3);
        Assert.Null(student.InstitutePriority4);
    }

    [Fact]
    public async Task AllocateInstituteProject_SecondSlot_SetsInstitutePriority2()
    {
        using var ctx = CreateContext(nameof(AllocateInstituteProject_SecondSlot_SetsInstitutePriority2));
        ctx.Students.Add(new Student { Id = 1, Email = "a@test.com", FirstName = "A", LastName = "B", StudentId = "a@test.com", LinkedInUrl = "https://linkedin.com/in/a", InstitutePriority1 = 1 });
        ctx.InstituteProjects.Add(MakeInstituteProject(5));
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).AllocateStudentToInstituteProject(5, 1, new AllocateStudentRequest());

        Assert.IsType<OkObjectResult>(result);
        var student = await ctx.Students.FindAsync(1);
        Assert.Equal(1, student!.InstitutePriority1);
        Assert.Equal(5, student.InstitutePriority2);
    }

    [Fact]
    public async Task AllocateInstituteProject_AllSlotsFull_ReturnsBadRequest()
    {
        using var ctx = CreateContext(nameof(AllocateInstituteProject_AllSlotsFull_ReturnsBadRequest));
        ctx.Students.Add(new Student { Id = 1, Email = "a@test.com", FirstName = "A", LastName = "B", StudentId = "a@test.com", LinkedInUrl = "https://linkedin.com/in/a", InstitutePriority1 = 1, InstitutePriority2 = 2, InstitutePriority3 = 3, InstitutePriority4 = 4 });
        ctx.InstituteProjects.Add(MakeInstituteProject(5));
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).AllocateStudentToInstituteProject(5, 1, new AllocateStudentRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AllocateInstituteProject_KeepsStatusZero_UntilCheckout()
    {
        // Allocation only sets priorities; Status stays 0 until ConfirmInstituteCheckout sets 1.
        using var ctx = CreateContext(nameof(AllocateInstituteProject_KeepsStatusZero_UntilCheckout));
        ctx.Students.Add(MakeStudent(1));
        ctx.InstituteProjects.Add(MakeInstituteProject(5));
        await ctx.SaveChangesAsync();

        await CreateStudentsController(ctx).AllocateStudentToInstituteProject(5, 1, new AllocateStudentRequest());

        var student = await ctx.Students.FindAsync(1);
        Assert.Equal(0, student!.Status);
        Assert.Equal(5, student.InstitutePriority1);
    }

    // ── POST /api/Students/use/institute/deallocate/{projectId}/{studentId} ─

    [Fact]
    public async Task DeallocateInstituteProject_StudentNotFound_ReturnsNotFound()
    {
        using var ctx = CreateContext(nameof(DeallocateInstituteProject_StudentNotFound_ReturnsNotFound));
        var result = await CreateStudentsController(ctx).DeallocateStudentFromInstituteProject(1, 999, new DeallocateStudentRequest());
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeallocateInstituteProject_NotAllocated_ReturnsBadRequest()
    {
        using var ctx = CreateContext(nameof(DeallocateInstituteProject_NotAllocated_ReturnsBadRequest));
        ctx.Students.Add(MakeStudent(1));
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).DeallocateStudentFromInstituteProject(99, 1, new DeallocateStudentRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeallocateInstituteProject_ClearsPriority1()
    {
        using var ctx = CreateContext(nameof(DeallocateInstituteProject_ClearsPriority1));
        ctx.Students.Add(new Student { Id = 1, Email = "a@test.com", FirstName = "A", LastName = "B", StudentId = "a@test.com", LinkedInUrl = "https://linkedin.com/in/a", InstitutePriority1 = 5 });
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).DeallocateStudentFromInstituteProject(5, 1, new DeallocateStudentRequest());

        Assert.IsType<OkObjectResult>(result);
        var student = await ctx.Students.FindAsync(1);
        Assert.Null(student!.InstitutePriority1);
    }

    [Fact]
    public async Task DeallocateInstituteProject_ClearsPriority3_LeavesPriority1And2()
    {
        using var ctx = CreateContext(nameof(DeallocateInstituteProject_ClearsPriority3_LeavesPriority1And2));
        ctx.Students.Add(new Student { Id = 1, Email = "a@test.com", FirstName = "A", LastName = "B", StudentId = "a@test.com", LinkedInUrl = "https://linkedin.com/in/a", InstitutePriority1 = 1, InstitutePriority2 = 2, InstitutePriority3 = 5 });
        await ctx.SaveChangesAsync();

        await CreateStudentsController(ctx).DeallocateStudentFromInstituteProject(5, 1, new DeallocateStudentRequest());

        var student = await ctx.Students.FindAsync(1);
        Assert.Equal(1, student!.InstitutePriority1);
        Assert.Equal(2, student.InstitutePriority2);
        Assert.Null(student.InstitutePriority3);
    }

    // ── GET /api/Students/use/institute/allocated-projects/{email} ──────────

    [Fact]
    public async Task GetAllocatedInstituteProjects_UnknownEmail_ReturnsNotFound()
    {
        using var ctx = CreateContext(nameof(GetAllocatedInstituteProjects_UnknownEmail_ReturnsNotFound));
        var result = await CreateStudentsController(ctx).GetAllocatedInstituteProjects("nobody@test.com");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetAllocatedInstituteProjects_NoPriorities_ReturnsEmptyArray()
    {
        using var ctx = CreateContext(nameof(GetAllocatedInstituteProjects_NoPriorities_ReturnsEmptyArray));
        ctx.Students.Add(MakeStudent(1));
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).GetAllocatedInstituteProjects("a@test.com");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Equal("[]", json);
    }

    [Fact]
    public async Task GetAllocatedInstituteProjects_WithPriority1_ReturnsProject()
    {
        using var ctx = CreateContext(nameof(GetAllocatedInstituteProjects_WithPriority1_ReturnsProject));
        ctx.Students.Add(new Student { Id = 1, Email = "a@test.com", FirstName = "A", LastName = "B", StudentId = "a@test.com", LinkedInUrl = "https://linkedin.com/in/a", InstitutePriority1 = 10 });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 10, InstituteId = 2, Title = "UniProject", IsAvailable = true });
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).GetAllocatedInstituteProjects("a@test.com");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("UniProject", json);
    }

    [Fact]
    public async Task GetAllocatedInstituteProjects_DeduplicatesIds()
    {
        using var ctx = CreateContext(nameof(GetAllocatedInstituteProjects_DeduplicatesIds));
        ctx.Students.Add(new Student { Id = 1, Email = "a@test.com", FirstName = "A", LastName = "B", StudentId = "a@test.com", LinkedInUrl = "https://linkedin.com/in/a", InstitutePriority1 = 7, InstitutePriority2 = 7 });
        ctx.InstituteProjects.Add(new InstituteProject { Id = 7, InstituteId = 2, Title = "P", IsAvailable = true });
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).GetAllocatedInstituteProjects("a@test.com");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        // Should only appear once despite being in two priority slots
        Assert.Single(System.Text.Json.JsonDocument.Parse(json).RootElement.EnumerateArray());
    }
}

/// <summary>
/// Checkout closes selection: Status >= 1 must block further project allocation
/// (the reported go-live bug: a checked-out student could keep applying).
/// </summary>
public class SelectionLockAfterCheckoutTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static StudentsController CreateStudentsController(ApplicationDbContext ctx)
        => new StudentsController(
            ctx,
            NullLogger<StudentsController>.Instance,
            new Mock<IGitHubService>().Object,
            new Mock<IKickoffService>().Object,
            new Mock<IPasswordHasherService>().Object);

    private static Student MakeStudent(int id, int? status) =>
        new Student { Id = id, Email = $"s{id}@t.com", FirstName = "A", LastName = "B", StudentId = $"s{id}@t.com", LinkedInUrl = "https://linkedin.com/in/a", Status = status };

    private static InstituteProject MakeProject(int id) =>
        new InstituteProject { Id = id, InstituteId = 2, Title = $"P{id}", IsAvailable = true };

    // ── Allocate endpoint (authoritative guard) ──────────────────────────────

    [Fact]
    public async Task Allocate_AfterCheckout_Status1_ReturnsBadRequest_AndWritesNothing()
    {
        using var ctx = CreateContext(nameof(Allocate_AfterCheckout_Status1_ReturnsBadRequest_AndWritesNothing));
        ctx.Students.Add(MakeStudent(1, status: 1));
        ctx.InstituteProjects.Add(MakeProject(5));
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).AllocateStudentToInstituteProject(5, 1, new AllocateStudentRequest());

        Assert.IsType<BadRequestObjectResult>(result);
        var student = await ctx.Students.FindAsync(1);
        Assert.Null(student!.InstitutePriority1); // no slot was written
    }

    [Fact]
    public async Task Allocate_StudentOnBoard_Status3_ReturnsBadRequest()
    {
        using var ctx = CreateContext(nameof(Allocate_StudentOnBoard_Status3_ReturnsBadRequest));
        ctx.Students.Add(MakeStudent(1, status: 3));
        ctx.InstituteProjects.Add(MakeProject(5));
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).AllocateStudentToInstituteProject(5, 1, new AllocateStudentRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(null)]
    public async Task Allocate_BeforeCheckout_Succeeds(int? status)
    {
        using var ctx = CreateContext($"{nameof(Allocate_BeforeCheckout_Succeeds)}_{status?.ToString() ?? "null"}");
        ctx.Students.Add(MakeStudent(1, status));
        ctx.InstituteProjects.Add(MakeProject(5));
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).AllocateStudentToInstituteProject(5, 1, new AllocateStudentRequest());

        Assert.IsType<OkObjectResult>(result);
        var student = await ctx.Students.FindAsync(1);
        Assert.Equal(5, student!.InstitutePriority1);
    }

    // ── Pre-check endpoint (FE-facing message) ───────────────────────────────

    [Fact]
    public async Task IsAllocatable_AfterCheckout_ReturnsFalse_WithClosedMessage()
    {
        using var ctx = CreateContext(nameof(IsAllocatable_AfterCheckout_ReturnsFalse_WithClosedMessage));
        ctx.Students.Add(MakeStudent(1, status: 1));
        ctx.InstituteProjects.Add(MakeProject(5));
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).IsStudentInstituteAllocatable(5, 1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"isAllocatable\":false", json);
        Assert.Contains("checkout", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IsAllocatable_BeforeCheckout_ReturnsTrue()
    {
        using var ctx = CreateContext(nameof(IsAllocatable_BeforeCheckout_ReturnsTrue));
        ctx.Students.Add(MakeStudent(1, status: 0));
        ctx.InstituteProjects.Add(MakeProject(5));
        await ctx.SaveChangesAsync();

        var result = await CreateStudentsController(ctx).IsStudentInstituteAllocatable(5, 1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"isAllocatable\":true", json);
    }
}
