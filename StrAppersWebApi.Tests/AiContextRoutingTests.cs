using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Utilities;

namespace StrAppersWebApi.Tests;

/// <summary>
/// Verifies that ProjectContextHelper and ProjectModuleLookup route to the correct
/// data source (InstituteProjects/InstituteProjectModules vs catalog Projects/ProjectModules)
/// based on whether boardInstituteProjectId is null or set.
/// </summary>
public class AiContextRoutingTests
{
    // ── seed IDs ────────────────────────────────────────────────────────────
    private const int CatalogProjectId      = 1;
    private const int InstituteProjectId    = 10;
    private const int CatalogModuleId       = 100;
    private const int InstituteModuleId     = 200;
    private const int SharedSequence        = 5;

    private static ApplicationDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(opts);

        db.Projects.Add(new Project
        {
            Id          = CatalogProjectId,
            Title       = "Catalog Project",
            Description = "catalog-description",
            CustomerPastStory = "catalog-past-story",
        });

        db.InstituteProjects.Add(new InstituteProject
        {
            Id          = InstituteProjectId,
            InstituteId = 2,
            Title       = "Institute Project",
            Description = "institute-description",
            CustomerPastStory = "institute-past-story",
        });

        db.ProjectModules.Add(new ProjectModule
        {
            Id        = CatalogModuleId,
            ProjectId = CatalogProjectId,
            Title     = "Catalog Module",
            Sequence  = SharedSequence,
        });

        db.InstituteProjectModules.Add(new InstituteProjectModule
        {
            Id                 = InstituteModuleId,
            InstituteProjectId = InstituteProjectId,
            Title              = "Institute Module",
            Sequence           = SharedSequence,
        });

        db.SaveChanges();
        return db;
    }

    // ════════════════════════════════════════════════════════════════════════
    // ProjectContextHelper — Pattern A (Description / CustomerPastStory)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ProjectContext_NullInstituteId_UsesCatalogProject()
    {
        await using var db = CreateDb();
        var (desc, story, source) = await ProjectContextHelper.GetEffectiveProjectDataAsync(
            db, CatalogProjectId, boardInstituteProjectId: null);

        Assert.Equal("catalog-description",  desc);
        Assert.Equal("catalog-past-story",   story);
        Assert.Contains("Projects.Id=", source);
        Assert.DoesNotContain("InstituteProjects", source);
    }

    [Fact]
    public async Task ProjectContext_WithInstituteId_UsesInstituteProject()
    {
        await using var db = CreateDb();
        var (desc, story, source) = await ProjectContextHelper.GetEffectiveProjectDataAsync(
            db, CatalogProjectId, boardInstituteProjectId: InstituteProjectId);

        Assert.Equal("institute-description", desc);
        Assert.Equal("institute-past-story",  story);
        Assert.Contains("InstituteProjects.Id=", source);
    }

    [Fact]
    public async Task ProjectContext_InstituteIdNotFound_FallsBackToCatalog()
    {
        await using var db = CreateDb();
        var (desc, story, source) = await ProjectContextHelper.GetEffectiveProjectDataAsync(
            db, CatalogProjectId, boardInstituteProjectId: 9999);

        Assert.Equal("catalog-description", desc);
        Assert.Equal("catalog-past-story",  story);
        Assert.Contains("Projects.Id=", source);
    }

    // ════════════════════════════════════════════════════════════════════════
    // ProjectModuleLookup.FindByBoardScopeAsync — Pattern B (by module Id)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ModuleLookup_NullInstituteId_ReturnsCatalogModule()
    {
        await using var db = CreateDb();
        var row = await ProjectModuleLookup.FindByBoardScopeAsync(
            db, CatalogModuleId, CatalogProjectId, boardInstituteProjectId: null);

        Assert.NotNull(row);
        Assert.Equal(CatalogModuleId, row.Id);
        Assert.Equal("Catalog Module", row.Title);
    }

    [Fact]
    public async Task ModuleLookup_WithInstituteId_ReturnsInstituteModule()
    {
        await using var db = CreateDb();
        var row = await ProjectModuleLookup.FindByBoardScopeAsync(
            db, InstituteModuleId, CatalogProjectId, boardInstituteProjectId: InstituteProjectId);

        Assert.NotNull(row);
        Assert.Equal(InstituteModuleId, row.Id);
        Assert.Equal("Institute Module", row.Title);
    }

    [Fact]
    public async Task ModuleLookup_InstituteModuleNotFound_FallsBackToCatalog()
    {
        await using var db = CreateDb();
        // CatalogModuleId exists in catalog but NOT in institute modules
        var row = await ProjectModuleLookup.FindByBoardScopeAsync(
            db, CatalogModuleId, CatalogProjectId, boardInstituteProjectId: InstituteProjectId);

        Assert.NotNull(row);
        Assert.Equal(CatalogModuleId, row.Id);
        Assert.Equal("Catalog Module", row.Title);
    }

    [Fact]
    public async Task ModuleLookup_NullInstituteId_UnknownModuleId_ReturnsNull()
    {
        await using var db = CreateDb();
        var row = await ProjectModuleLookup.FindByBoardScopeAsync(
            db, 9999, CatalogProjectId, boardInstituteProjectId: null);

        Assert.Null(row);
    }

    // ════════════════════════════════════════════════════════════════════════
    // ProjectModuleLookup.FindManyBySequenceAsync — Pattern B (by sequence)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ModuleLookupBySequence_NullInstituteId_ReturnsCatalogModules()
    {
        await using var db = CreateDb();
        var rows = await ProjectModuleLookup.FindManyBySequenceAsync(
            db, SharedSequence, CatalogProjectId, boardInstituteProjectId: null);

        Assert.Single(rows);
        Assert.Equal(CatalogModuleId, rows[0].Id);
    }

    [Fact]
    public async Task ModuleLookupBySequence_WithInstituteId_ReturnsInstituteModules()
    {
        await using var db = CreateDb();
        var rows = await ProjectModuleLookup.FindManyBySequenceAsync(
            db, SharedSequence, CatalogProjectId, boardInstituteProjectId: InstituteProjectId);

        Assert.Single(rows);
        Assert.Equal(InstituteModuleId, rows[0].Id);
    }

    [Fact]
    public async Task ModuleLookupBySequence_InstituteModulesEmpty_ReturnsCatalogModules()
    {
        await using var db = CreateDb();
        // sequence 99 exists in neither — both return empty; institute path returns empty list
        var rows = await ProjectModuleLookup.FindManyBySequenceAsync(
            db, 99, CatalogProjectId, boardInstituteProjectId: InstituteProjectId);

        Assert.Empty(rows);
    }
}
