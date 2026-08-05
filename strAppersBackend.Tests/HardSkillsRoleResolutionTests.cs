using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using strAppersBackend.Controllers;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// Which Role row supplies the Professional Skills rubric.
///
/// A student's StudentRole usually points at the GLOBAL role (InstituteId = null), while "Roles &amp;
/// Professional Skills definition" saves onto a separate institute-scoped row. The resolver used to
/// return the assigned row as soon as it carried any Competencies text, so a stale global definition
/// silently outranked what staff had just saved — and the report scored criteria that appear nowhere
/// in the definition screen.
/// </summary>
public class HardSkillsRoleResolutionTests
{
    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static MetricsController BuildController(ApplicationDbContext db) =>
        new(db, Mock.Of<ITrelloService>(), Mock.Of<IGitHubService>(),
            new ConfigurationBuilder().Build(), NullLogger<MetricsController>.Instance,
            Mock.Of<IChatCompletionService>(), Mock.Of<IHttpClientFactory>(),
            Options.Create(new PromptConfig()), Mock.Of<IMicrosoftGraphService>(),
            Mock.Of<ISmtpEmailService>(), Mock.Of<IAzureBlobStorageService>());

    private static Role RoleRow(int id, string name, int? instituteId, string? competencies, int? squadId = null) =>
        new()
        {
            Id = id, Name = name, InstituteId = instituteId, SquadId = squadId,
            IsActive = true, Competencies = competencies,
        };

    /// <summary>The reported bug, exactly.</summary>
    [Fact]
    public async Task InstituteDefinition_WinsOverStaleGlobalText()
    {
        var db = CreateDb();
        var global = RoleRow(1, "Product Manager", null, "OLD global criteria");
        db.Roles.Add(global);
        db.Roles.Add(RoleRow(2, "Product Manager", 7, "Category: Stakeholder alignment"));
        await db.SaveChangesAsync();

        var resolved = await BuildController(db).ResolveHardSkillsRoleAsync(global, 7, CancellationToken.None);

        Assert.Equal(2, resolved.Id);
        Assert.Equal("Category: Stakeholder alignment", resolved.Competencies);
    }

    [Fact]
    public async Task AssignedRow_WinsWhenItIsItselfTheInstitutesRow()
    {
        // A student on a squad-scoped role must not be graded against the institute-level text.
        var db = CreateDb();
        var squadRole = RoleRow(3, "Product Manager", 7, "Squad-specific criteria", squadId: 42);
        db.Roles.Add(RoleRow(2, "Product Manager", 7, "Institute-level criteria"));
        db.Roles.Add(squadRole);
        await db.SaveChangesAsync();

        var resolved = await BuildController(db).ResolveHardSkillsRoleAsync(squadRole, 7, CancellationToken.None);

        Assert.Equal(3, resolved.Id);
        Assert.Equal("Squad-specific criteria", resolved.Competencies);
    }

    [Fact]
    public async Task InstituteLevelRow_IsPreferredOverSquadScopedOnes()
    {
        var db = CreateDb();
        var global = RoleRow(1, "Product Manager", null, null);
        db.Roles.Add(global);
        db.Roles.Add(RoleRow(9, "Product Manager", 7, "Squad text", squadId: 42));
        db.Roles.Add(RoleRow(5, "Product Manager", 7, "Institute text"));
        await db.SaveChangesAsync();

        var resolved = await BuildController(db).ResolveHardSkillsRoleAsync(global, 7, CancellationToken.None);

        Assert.Equal(5, resolved.Id);
    }

    [Fact]
    public async Task NoInstituteRowWithCompetencies_KeepsTheAssignedRow()
    {
        var db = CreateDb();
        var global = RoleRow(1, "Product Manager", null, "Global criteria");
        db.Roles.Add(global);
        db.Roles.Add(RoleRow(2, "Product Manager", 7, ""));   // exists but empty
        await db.SaveChangesAsync();

        var resolved = await BuildController(db).ResolveHardSkillsRoleAsync(global, 7, CancellationToken.None);

        Assert.Equal(1, resolved.Id);
        Assert.Equal("Global criteria", resolved.Competencies);
    }

    [Fact]
    public async Task InactiveInstituteRows_AreIgnored()
    {
        var db = CreateDb();
        var global = RoleRow(1, "Product Manager", null, "Global criteria");
        var retired = RoleRow(2, "Product Manager", 7, "Retired criteria");
        retired.IsActive = false;
        db.Roles.Add(global);
        db.Roles.Add(retired);
        await db.SaveChangesAsync();

        var resolved = await BuildController(db).ResolveHardSkillsRoleAsync(global, 7, CancellationToken.None);

        Assert.Equal(1, resolved.Id);
    }

    [Fact]
    public async Task AnotherInstitutesRow_IsNeverUsed()
    {
        var db = CreateDb();
        var global = RoleRow(1, "Product Manager", null, "Global criteria");
        db.Roles.Add(global);
        db.Roles.Add(RoleRow(2, "Product Manager", 99, "Other institute criteria"));
        await db.SaveChangesAsync();

        var resolved = await BuildController(db).ResolveHardSkillsRoleAsync(global, 7, CancellationToken.None);

        Assert.Equal(1, resolved.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task WithoutAnInstitute_TheAssignedRowIsUsed(int? instituteId)
    {
        var db = CreateDb();
        var global = RoleRow(1, "Product Manager", null, "Global criteria");
        db.Roles.Add(global);
        await db.SaveChangesAsync();

        var resolved = await BuildController(db).ResolveHardSkillsRoleAsync(global, instituteId, CancellationToken.None);

        Assert.Equal(1, resolved.Id);
    }

    [Fact]
    public async Task RoleNamesMatchCaseInsensitively()
    {
        var db = CreateDb();
        var global = RoleRow(1, "product manager", null, "Global criteria");
        db.Roles.Add(global);
        db.Roles.Add(RoleRow(2, "Product Manager", 7, "Institute criteria"));
        await db.SaveChangesAsync();

        var resolved = await BuildController(db).ResolveHardSkillsRoleAsync(global, 7, CancellationToken.None);

        Assert.Equal(2, resolved.Id);
    }
}
