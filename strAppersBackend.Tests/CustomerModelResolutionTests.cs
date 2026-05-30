using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests the model resolution block from CustomerController.Respond (~line 99).
/// Same pattern as MentorModelResolutionTests but reads Customer:AiModel.
/// </summary>
public class CustomerModelResolutionTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Mock<IConfiguration> Config(string? customerAiModel)
    {
        var mock = new Mock<IConfiguration>();
        mock.Setup(c => c["Customer:AiModel"]).Returns(customerAiModel);
        return mock;
    }

    // Replicates CustomerController ~lines 99-108 exactly.
    private static async Task<AIModel?> Resolve(
        ApplicationDbContext db, IConfiguration config, string aiModelName)
    {
        var resolvedModelName = aiModelName;
        if (string.IsNullOrWhiteSpace(resolvedModelName) ||
            resolvedModelName.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            resolvedModelName = config["Customer:AiModel"] ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(resolvedModelName)
            ? await db.AIModels.FirstOrDefaultAsync(m => m.IsActive)
            : await db.AIModels.FirstOrDefaultAsync(m => m.Name == resolvedModelName && m.IsActive);
    }

    [Fact]
    public async Task Default_ConfigHasModel_DbHasModel_ResolvesToConfigModel()
    {
        await using var db = CreateDb();
        db.AIModels.Add(new AIModel { Id = 1, Name = "claude-sonnet-4-5-20250929", IsActive = true });
        await db.SaveChangesAsync();

        var result = await Resolve(db, Config("claude-sonnet-4-5-20250929").Object, "default");

        Assert.NotNull(result);
        Assert.Equal("claude-sonnet-4-5-20250929", result.Name);
    }

    [Fact]
    public async Task Default_ConfigHasModel_DbLacksModel_ReturnsNull()
    {
        await using var db = CreateDb();
        db.AIModels.Add(new AIModel { Id = 2, Name = "gpt-4o-mini", IsActive = true });
        await db.SaveChangesAsync();

        // Config points at a model absent from DB
        var result = await Resolve(db, Config("claude-sonnet-4-5-20250929").Object, "default");

        Assert.Null(result);
    }

    [Fact]
    public async Task Default_ConfigKeyMissing_ResolvesToFirstActiveDbModel()
    {
        await using var db = CreateDb();
        db.AIModels.Add(new AIModel { Id = 3, Name = "gpt-4o-mini", IsActive = true });
        await db.SaveChangesAsync();

        var result = await Resolve(db, Config(null).Object, "default");

        Assert.NotNull(result);
        Assert.Equal("gpt-4o-mini", result.Name);
    }

    [Fact]
    public async Task ExplicitModelName_IgnoresConfig_ResolvesDirectlyFromDb()
    {
        await using var db = CreateDb();
        db.AIModels.AddRange(
            new AIModel { Id = 4, Name = "claude-sonnet-4-5-20250929", IsActive = true },
            new AIModel { Id = 5, Name = "gpt-4o-mini", IsActive = true }
        );
        await db.SaveChangesAsync();

        var result = await Resolve(db, Config("claude-sonnet-4-5-20250929").Object, "gpt-4o-mini");

        Assert.NotNull(result);
        Assert.Equal("gpt-4o-mini", result.Name);
    }
}
