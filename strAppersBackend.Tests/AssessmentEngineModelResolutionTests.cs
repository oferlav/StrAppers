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
/// Tests for <see cref="MetricsController.ResolveAssessmentEngineModelAsync"/>: institutes can select
/// which AIModels row the generic Data Assessment Engine (use/assess) uses via
/// Institute.AssessmentEngineAIModelId. No selection (or a selection pointing at a missing/inactive row)
/// falls back to the same OpenAI:CheapModel config default the engine always used. MaxTokens and
/// DefaultTemperature always stay at the engine's own fixed tuning (16384 / 0.2) regardless of what the
/// selected AIModels row itself stores.
/// </summary>
public class AssessmentEngineModelResolutionTests
{
    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MetricsController BuildController(ApplicationDbContext db, IConfiguration? config = null)
    {
        return new MetricsController(
            db,
            Mock.Of<ITrelloService>(),
            Mock.Of<IGitHubService>(),
            config ?? new ConfigurationBuilder().Build(),
            NullLogger<MetricsController>.Instance,
            Mock.Of<IChatCompletionService>(),
            Mock.Of<IHttpClientFactory>(),
            Options.Create(new PromptConfig()),
            Mock.Of<IMicrosoftGraphService>(),
            Mock.Of<ISmtpEmailService>(),
            Mock.Of<IAzureBlobStorageService>());
    }

    [Fact]
    public async Task NoInstituteId_UsesConfigDefault()
    {
        var db = CreateDb();
        var controller = BuildController(db);

        var result = await controller.ResolveAssessmentEngineModelAsync(null, CancellationToken.None);

        Assert.Equal("gpt-4o-mini", result.Name);
        Assert.Equal("OpenAI", result.Provider);
        Assert.Equal("https://api.openai.com/v1", result.BaseUrl);
        Assert.Equal(16384, result.MaxTokens);
        Assert.Equal(0.2, result.DefaultTemperature);
    }

    [Fact]
    public async Task InstituteWithNoSelection_UsesConfigDefault()
    {
        var db = CreateDb();
        db.Institutes.Add(new Institute { Id = 1, Name = "Test Institute", AssessmentEngineAIModelId = null });
        await db.SaveChangesAsync();
        var controller = BuildController(db);

        var result = await controller.ResolveAssessmentEngineModelAsync(1, CancellationToken.None);

        Assert.Equal("gpt-4o-mini", result.Name);
        Assert.Equal("OpenAI", result.Provider);
    }

    [Fact]
    public async Task NonExistentInstitute_UsesConfigDefault()
    {
        var db = CreateDb();
        var controller = BuildController(db);

        var result = await controller.ResolveAssessmentEngineModelAsync(999, CancellationToken.None);

        Assert.Equal("gpt-4o-mini", result.Name);
    }

    [Fact]
    public async Task InstituteWithActiveSelection_UsesSelectedModel()
    {
        var db = CreateDb();
        db.AIModels.Add(new AIModel
        {
            Id = 10, Name = "claude-sonnet-4-6", Provider = "Anthropic",
            BaseUrl = "https://api.anthropic.com/v1", ApiVersion = "2023-06-01",
            MaxTokens = 200000, DefaultTemperature = 1.0, IsActive = true,
        });
        db.Institutes.Add(new Institute { Id = 1, Name = "Test Institute", AssessmentEngineAIModelId = 10 });
        await db.SaveChangesAsync();
        var controller = BuildController(db);

        var result = await controller.ResolveAssessmentEngineModelAsync(1, CancellationToken.None);

        Assert.Equal("claude-sonnet-4-6", result.Name);
        Assert.Equal("Anthropic", result.Provider);
        Assert.Equal("https://api.anthropic.com/v1", result.BaseUrl);
        Assert.Equal("2023-06-01", result.ApiVersion);
    }

    [Fact]
    public async Task InstituteWithActiveSelection_StillUsesEngineFixedTuning_NotTheRowsOwnValues()
    {
        var db = CreateDb();
        db.AIModels.Add(new AIModel
        {
            Id = 10, Name = "claude-sonnet-4-6", Provider = "Anthropic",
            MaxTokens = 200000, DefaultTemperature = 1.0, IsActive = true,
        });
        db.Institutes.Add(new Institute { Id = 1, Name = "Test Institute", AssessmentEngineAIModelId = 10 });
        await db.SaveChangesAsync();
        var controller = BuildController(db);

        var result = await controller.ResolveAssessmentEngineModelAsync(1, CancellationToken.None);

        // The row's own MaxTokens=200000 / DefaultTemperature=1.0 must NOT leak through —
        // the engine always enforces its own JSON-output-safe tuning.
        Assert.Equal(16384, result.MaxTokens);
        Assert.Equal(0.2, result.DefaultTemperature);
    }

    [Fact]
    public async Task InstituteWithInactiveSelection_FallsBackToConfigDefault()
    {
        var db = CreateDb();
        db.AIModels.Add(new AIModel { Id = 10, Name = "retired-model", Provider = "OpenAI", IsActive = false });
        db.Institutes.Add(new Institute { Id = 1, Name = "Test Institute", AssessmentEngineAIModelId = 10 });
        await db.SaveChangesAsync();
        var controller = BuildController(db);

        var result = await controller.ResolveAssessmentEngineModelAsync(1, CancellationToken.None);

        Assert.Equal("gpt-4o-mini", result.Name);
        Assert.NotEqual("retired-model", result.Name);
    }

    [Fact]
    public async Task InstituteWithSelectionPointingToDeletedModel_FallsBackToConfigDefault()
    {
        var db = CreateDb();
        // AssessmentEngineAIModelId points at a row that no longer exists (e.g. deleted from AIModels).
        db.Institutes.Add(new Institute { Id = 1, Name = "Test Institute", AssessmentEngineAIModelId = 999 });
        await db.SaveChangesAsync();
        var controller = BuildController(db);

        var result = await controller.ResolveAssessmentEngineModelAsync(1, CancellationToken.None);

        Assert.Equal("gpt-4o-mini", result.Name);
    }

    [Fact]
    public async Task NoSelection_RespectsConfigOverride_ForNameAndBaseUrl()
    {
        var db = CreateDb();
        db.Institutes.Add(new Institute { Id = 1, Name = "Test Institute", AssessmentEngineAIModelId = null });
        await db.SaveChangesAsync();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:CheapModel"] = "gpt-4.1-mini",
                ["OpenAI:BaseUrl"] = "https://custom.openai.example.com/v1",
            })
            .Build();
        var controller = BuildController(db, config);

        var result = await controller.ResolveAssessmentEngineModelAsync(1, CancellationToken.None);

        Assert.Equal("gpt-4.1-mini", result.Name);
        Assert.Equal("https://custom.openai.example.com/v1", result.BaseUrl);
    }

    [Fact]
    public async Task ActiveSelection_TakesPrecedenceOverConfigOverride()
    {
        var db = CreateDb();
        db.AIModels.Add(new AIModel { Id = 10, Name = "claude-sonnet-4-6", Provider = "Anthropic", IsActive = true });
        db.Institutes.Add(new Institute { Id = 1, Name = "Test Institute", AssessmentEngineAIModelId = 10 });
        await db.SaveChangesAsync();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:CheapModel"] = "gpt-4.1-mini" })
            .Build();
        var controller = BuildController(db, config);

        var result = await controller.ResolveAssessmentEngineModelAsync(1, CancellationToken.None);

        Assert.Equal("claude-sonnet-4-6", result.Name);
        Assert.Equal("Anthropic", result.Provider);
    }
}
