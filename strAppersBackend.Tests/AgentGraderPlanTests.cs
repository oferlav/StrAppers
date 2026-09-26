using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services.GoogleProxy;

namespace strAppersBackend.Tests;

/// <summary>
/// The grader resolves what to grade from the DB (board -> project -> scenario set -> exercise, world, scenarios)
/// and tags every check with the requirement it enforces.
/// </summary>
public class AgentGraderPlanTests
{
    private const string WorldJson = """{ "geocode": [], "places": [ { "id": "p1", "displayName": "Green Table", "lat": 1, "lng": 2 } ] }""";

    private static ApplicationDbContext CreateDb(string status = AgentScenarioSet.StatusPublished, string purpose = ProjectAgentScenarioSet.PurposeGraded)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.InstituteProjects.Add(new InstituteProject { Id = 84, InstituteId = 1, Title = "Dinner Scout" });
        db.ProjectBoards.AddRange(
            new ProjectBoard { Id = "b84", ProjectId = 1, InstituteProjectId = 84 },
            new ProjectBoard { Id = "bNoProject", ProjectId = 1 });
        db.AgentExercises.Add(new AgentExercise
        {
            Id = 1, Key = "dinner-scout", Name = "Dinner Scout", EndpointPath = "api/agent/dinner",
            LimitsJson = """{ "maxLatencyMs": 15000, "checkRequirements": { "grounding": "R6", "grounding.travel": "R14" } }"""
        });
        db.AgentWorlds.Add(new AgentWorld { Id = 1, Key = "midtown-dinner-1", Name = "Midtown", DataJson = WorldJson });
        db.AgentScenarioSets.Add(new AgentScenarioSet { Id = 1, ExerciseId = 1, WorldId = 1, Name = "acceptance", Version = 1, Status = status });
        db.AgentScenarios.AddRange(
            new AgentScenario
            {
                Id = 1, ScenarioSetId = 1, Key = "second", Request = "Near me", PositionLat = 40.7, PositionLng = -73.9, SortOrder = 2,
                ExpectationsJson = """{ "expectGeocodeCall": false, "requirements": { "expectGeocodeCall": "R11" } }"""
            },
            new AgentScenario
            {
                Id = 2, ScenarioSetId = 1, Key = "first", Request = "Vegetarian near Times Square", SortOrder = 1,
                ExpectationsJson = """{ "expectStatus": "ok", "minResults": 1, "mustNotInclude": ["pX"], "requirements": { "mustNotInclude": "R7, R8" } }"""
            });
        db.ProjectAgentScenarioSets.Add(new ProjectAgentScenarioSet { InstituteProjectId = 84, ScenarioSetId = 1, Purpose = purpose });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Resolves_BoardToPublishedSet_WithWorldLimitsAndOrderedScenarios()
    {
        using var db = CreateDb();

        var plan = await AgentGrader.ResolvePlanAsync(db, "b84", ProjectAgentScenarioSet.PurposeGraded, null);

        Assert.Equal(84, plan.InstituteProjectId);
        Assert.Equal("dinner-scout", plan.ExerciseKey);
        Assert.Equal("/api/agent/dinner", plan.EndpointPath);
        Assert.Equal(15000, plan.Limits.MaxLatencyMs);
        Assert.Equal(5, plan.Limits.MaxDetailsCalls); // default when LimitsJson omits it
        Assert.Equal("midtown-dinner-1", plan.World.WorldId);
        Assert.Equal("Green Table", Assert.Single(plan.World.Places).DisplayName);

        Assert.Equal(new[] { "first", "second" }, plan.Scenarios.Select(s => s.Id));
        var first = plan.Scenarios[0];
        Assert.Equal("Vegetarian near Times Square", first.Request);
        Assert.Null(first.Position);
        Assert.Equal(1, first.MinResults);
        Assert.Equal(new[] { "pX" }, first.MustNotInclude);
        Assert.Equal(40.7, plan.Scenarios[1].Position!.Lat);
        Assert.False(plan.Scenarios[1].ExpectGeocodeCall);
    }

    [Fact]
    public async Task ScenarioKeys_FilterTheSet()
    {
        using var db = CreateDb();

        var plan = await AgentGrader.ResolvePlanAsync(db, "b84", ProjectAgentScenarioSet.PurposeGraded, new[] { "second" });

        Assert.Equal("second", Assert.Single(plan.Scenarios).Id);
    }

    [Theory]
    [InlineData("unknown", "not found")]
    [InlineData("bNoProject", "no institute project")]
    public async Task BadBoard_ExplainsWhy(string boardId, string reason)
    {
        using var db = CreateDb();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => AgentGrader.ResolvePlanAsync(db, boardId, ProjectAgentScenarioSet.PurposeGraded, null));
        Assert.Contains(reason, ex.Message);
    }

    [Fact]
    public async Task DraftSet_IsNotGraded()
    {
        using var db = CreateDb(status: AgentScenarioSet.StatusDraft);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => AgentGrader.ResolvePlanAsync(db, "b84", ProjectAgentScenarioSet.PurposeGraded, null));
        Assert.Contains("only published", ex.Message);
    }

    [Fact]
    public async Task MissingPurpose_ExplainsWhy()
    {
        using var db = CreateDb(purpose: ProjectAgentScenarioSet.PurposePractice);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => AgentGrader.ResolvePlanAsync(db, "b84", ProjectAgentScenarioSet.PurposeGraded, null));
        Assert.Contains("no graded scenario set", ex.Message);
        Assert.NotNull(await AgentGrader.ResolvePlanAsync(db, "b84", ProjectAgentScenarioSet.PurposePractice, null));
    }

    [Fact]
    public async Task EmptySelection_IsRejected()
    {
        using var db = CreateDb();

        await Assert.ThrowsAsync<ArgumentException>(() => AgentGrader.ResolvePlanAsync(db, "b84", ProjectAgentScenarioSet.PurposeGraded, new[] { "nope" }));
    }

    [Fact]
    public void AllResultsIn_IsParsedFromExpectations_AndTagged()
    {
        var scenario = System.Text.Json.JsonSerializer.Deserialize<GradingScenario>(
            """{ "allResultsIn": ["nile"], "requirements": { "allResultsIn": "R3" } }""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        scenario.Requirements = new Dictionary<string, string>(scenario.Requirements, StringComparer.OrdinalIgnoreCase);
        var checks = new List<GradingCheck> { new() { Id = "ranking.allResultsFit" } };

        AgentGrader.TagRequirements(checks, scenario, new ExerciseLimits());

        Assert.Equal(new[] { "nile" }, scenario.AllResultsIn);
        Assert.Equal("R3", checks[0].Requirement);
    }

    [Fact]
    public async Task Checks_AreTaggedFromScenarioFirst_ThenLongestExercisePrefix()
    {
        using var db = CreateDb();
        var plan = await AgentGrader.ResolvePlanAsync(db, "b84", ProjectAgentScenarioSet.PurposeGraded, null);
        var checks = new List<GradingCheck>
        {
            new() { Id = "hardRule.mustNotInclude" },
            new() { Id = "grounding.found[p1]" },
            new() { Id = "grounding.travel[p1]" },
            new() { Id = "ranking.top1" }
        };

        AgentGrader.TagRequirements(checks, plan.Scenarios[0], plan.Limits);

        Assert.Equal("R7, R8", checks[0].Requirement);   // scenario tag
        Assert.Equal("R6", checks[1].Requirement);       // exercise prefix "grounding"
        Assert.Equal("R14", checks[2].Requirement);      // longer prefix "grounding.travel" wins
        Assert.Null(checks[3].Requirement);              // no tag anywhere
    }
}
