using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Utilities;

namespace strAppersBackend.Tests;

/// <summary>
/// SprintLengthResolver: per-board sprint length in days from template JSON
/// (InstituteProject preferred, Project fallback), else configWeeks × 7.
/// </summary>
public class SprintLengthResolverTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private const string JsonDays3 = """{"ProjectId":1,"SprintLengthWeeks":1,"SprintLengthDays":3}""";
    private const string JsonDays5 = """{"ProjectId":1,"SprintLengthWeeks":1,"SprintLengthDays":5}""";
    private const string JsonNoDays = """{"ProjectId":1,"SprintLengthWeeks":1}""";

    // ── ResolveFromJson ───────────────────────────────────────────────────────

    [Fact]
    public void Json_WithDays_ReturnsDays()
    {
        Assert.Equal(3, SprintLengthResolver.ResolveFromJson(JsonDays3, configSprintLengthWeeks: 1));
    }

    [Fact]
    public void Json_WithoutDays_FallsBackToConfigWeeks()
    {
        Assert.Equal(7, SprintLengthResolver.ResolveFromJson(JsonNoDays, configSprintLengthWeeks: 1));
        Assert.Equal(14, SprintLengthResolver.ResolveFromJson(JsonNoDays, configSprintLengthWeeks: 2));
    }

    [Fact]
    public void MalformedJson_FallsBackToConfigWeeks()
    {
        Assert.Equal(7, SprintLengthResolver.ResolveFromJson("{not valid json", configSprintLengthWeeks: 1));
    }

    [Fact]
    public void NullOrEmptyJson_FallsBackToConfigWeeks()
    {
        Assert.Equal(7, SprintLengthResolver.ResolveFromJson(null, configSprintLengthWeeks: 1));
        Assert.Equal(7, SprintLengthResolver.ResolveFromJson("", configSprintLengthWeeks: 1));
        Assert.Equal(7, SprintLengthResolver.ResolveFromJson("   ", configSprintLengthWeeks: 1));
    }

    [Fact]
    public void ZeroOrNegativeDays_ClampedToMinimumOne()
    {
        const string zeroDays = """{"ProjectId":1,"SprintLengthDays":0}""";
        const string negativeDays = """{"ProjectId":1,"SprintLengthDays":-3}""";
        Assert.Equal(1, SprintLengthResolver.ResolveFromJson(zeroDays, configSprintLengthWeeks: 1));
        Assert.Equal(1, SprintLengthResolver.ResolveFromJson(negativeDays, configSprintLengthWeeks: 1));
    }

    [Fact]
    public void ZeroConfigWeeks_ClampedToMinimumOneDay()
    {
        Assert.Equal(1, SprintLengthResolver.ResolveFromJson(null, configSprintLengthWeeks: 0));
    }

    // ── ResolveForBoardAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task Board_WithInstituteProject_PrefersInstituteProjectJson()
    {
        using var db = CreateDb();
        db.Projects.Add(new Project { Id = 1, Title = "P", TrelloBoardJson = JsonDays5 });
        db.InstituteProjects.Add(new InstituteProject { Id = 10, Title = "IP", TrelloBoardJson = JsonDays3 });
        db.ProjectBoards.Add(new ProjectBoard { Id = "b1", ProjectId = 1, InstituteProjectId = 10 });
        await db.SaveChangesAsync();

        var days = await SprintLengthResolver.ResolveForBoardAsync(db, "b1", configSprintLengthWeeks: 1);

        Assert.Equal(3, days);
    }

    [Fact]
    public async Task Board_WithoutInstituteProject_UsesProjectJson()
    {
        using var db = CreateDb();
        db.Projects.Add(new Project { Id = 1, Title = "P", TrelloBoardJson = JsonDays3 });
        db.ProjectBoards.Add(new ProjectBoard { Id = "b1", ProjectId = 1 });
        await db.SaveChangesAsync();

        var days = await SprintLengthResolver.ResolveForBoardAsync(db, "b1", configSprintLengthWeeks: 1);

        Assert.Equal(3, days);
    }

    [Fact]
    public async Task Board_InstituteProjectJsonEmpty_FallsThroughToProjectJson()
    {
        using var db = CreateDb();
        db.Projects.Add(new Project { Id = 1, Title = "P", TrelloBoardJson = JsonDays3 });
        db.InstituteProjects.Add(new InstituteProject { Id = 10, Title = "IP", TrelloBoardJson = null });
        db.ProjectBoards.Add(new ProjectBoard { Id = "b1", ProjectId = 1, InstituteProjectId = 10 });
        await db.SaveChangesAsync();

        var days = await SprintLengthResolver.ResolveForBoardAsync(db, "b1", configSprintLengthWeeks: 1);

        Assert.Equal(3, days);
    }

    [Fact]
    public async Task LegacyBoard_NoJsonAnywhere_FallsBackToConfigWeeks()
    {
        using var db = CreateDb();
        db.Projects.Add(new Project { Id = 1, Title = "P", TrelloBoardJson = null });
        db.ProjectBoards.Add(new ProjectBoard { Id = "b1", ProjectId = 1 });
        await db.SaveChangesAsync();

        var days = await SprintLengthResolver.ResolveForBoardAsync(db, "b1", configSprintLengthWeeks: 1);

        Assert.Equal(7, days);
    }

    [Fact]
    public async Task UnknownBoard_FallsBackToConfigWeeks()
    {
        using var db = CreateDb();

        var days = await SprintLengthResolver.ResolveForBoardAsync(db, "no-such-board", configSprintLengthWeeks: 1);

        Assert.Equal(7, days);
    }
}
