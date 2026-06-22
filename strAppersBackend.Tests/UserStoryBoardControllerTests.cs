using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// T13-T18 — Unit tests for User Story board separation feature (controller / DTO logic).
/// Tests effectiveBoardId routing and model/DTO field expectations using EF InMemory.
/// </summary>
public class UserStoryBoardControllerTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    // Replicates the effectiveBoardId resolution from BoardsController.GetUserStories
    private static string ResolveEffectiveBoardId(ProjectBoard? board, string incomingBoardId) =>
        board?.UserStoryBoardId ?? incomingBoardId;

    // ── T13: ProjectBoard model exposes UserStoryBoardId and UserStoryBoardUrl ──

    [Fact]
    public void T13_ProjectBoard_HasUserStoryBoardFields_NullableByDefault()
    {
        var board = new ProjectBoard { Id = "board1" };
        Assert.Null(board.UserStoryBoardId);
        Assert.Null(board.UserStoryBoardUrl);
    }

    // ── T14: effectiveBoardId = UserStoryBoardId when set ────────────────────

    [Fact]
    public async Task T14_GetUserStories_UsesUserStoryBoardId_WhenFieldIsSet()
    {
        await using var db = CreateDb();
        db.ProjectBoards.Add(new ProjectBoard
        {
            Id               = "mainboard1",
            UserStoryBoardId = "usboard1",
            UserStoryBoardUrl = "https://trello.com/b/usboard1/stories",
        });
        await db.SaveChangesAsync();

        var board = await db.ProjectBoards.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == "mainboard1");

        var effectiveBoardId = ResolveEffectiveBoardId(board, "mainboard1");

        Assert.Equal("usboard1", effectiveBoardId);
    }

    // ── T15: effectiveBoardId falls back to main boardId when UserStoryBoardId is null ──

    [Fact]
    public async Task T15_GetUserStories_FallsBackToMainBoardId_WhenUserStoryBoardIdIsNull()
    {
        await using var db = CreateDb();
        db.ProjectBoards.Add(new ProjectBoard
        {
            Id               = "legacyboard",
            UserStoryBoardId = null,
        });
        await db.SaveChangesAsync();

        var board = await db.ProjectBoards.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == "legacyboard");

        var effectiveBoardId = ResolveEffectiveBoardId(board, "legacyboard");

        Assert.Equal("legacyboard", effectiveBoardId);
    }

    // ── T16: effectiveBoardId = incoming boardId when board not found in DB ──

    [Fact]
    public async Task T16_GetUserStories_UsesIncomingBoardId_WhenProjectBoardNotFound()
    {
        await using var db = CreateDb();
        // nothing added to DB

        var board = await db.ProjectBoards.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == "unknownboard");

        var effectiveBoardId = ResolveEffectiveBoardId(board, "unknownboard");

        Assert.Null(board);
        Assert.Equal("unknownboard", effectiveBoardId);
    }

    // ── T17: TrelloProjectCreationResponse carries UserStoryBoardId and UserStoryBoardUrl ──

    [Fact]
    public void T17_TrelloProjectCreationResponse_HasUserStoryBoardFields()
    {
        var response = new TrelloProjectCreationResponse
        {
            Success           = true,
            UserStoryBoardId  = "usboard42",
            UserStoryBoardUrl = "https://trello.com/b/usboard42/stories",
        };

        Assert.Equal("usboard42", response.UserStoryBoardId);
        Assert.Equal("https://trello.com/b/usboard42/stories", response.UserStoryBoardUrl);
    }

    // ── T18: ProjectBoard UserStoryBoardUrl is null for legacy boards ──────

    [Fact]
    public async Task T18_ProjectBoard_UserStoryBoardUrlIsNull_ForLegacyBoards()
    {
        await using var db = CreateDb();
        db.ProjectBoards.Add(new ProjectBoard { Id = "legacy1" });
        await db.SaveChangesAsync();

        var board = await db.ProjectBoards.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == "legacy1");

        Assert.NotNull(board);
        Assert.Null(board!.UserStoryBoardUrl);
    }

    // ── T19: AI context sources resolve effectiveBoardId from DB when UserStoryBoardId is set ──

    [Fact]
    public async Task T19_AiContext_ResolvesEffectiveBoardId_WhenUserStoryBoardIdSet()
    {
        await using var db = CreateDb();
        db.ProjectBoards.Add(new ProjectBoard
        {
            Id               = "main-ai",
            UserStoryBoardId = "us-ai",
        });
        await db.SaveChangesAsync();

        // Mirrors how CrmReview / ResourceReview / StoryReview / MentorController resolve effectiveBoardId
        var board = await db.ProjectBoards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == "main-ai");
        var effectiveBoardId = board?.UserStoryBoardId ?? "main-ai";

        Assert.Equal("us-ai", effectiveBoardId);
        Assert.NotEqual("main-ai", effectiveBoardId);
    }

    // ── T20: AI context sources fall back to boardId when UserStoryBoardId is null ──

    [Fact]
    public async Task T20_AiContext_FallsBackToBoardId_WhenUserStoryBoardIdIsNull()
    {
        await using var db = CreateDb();
        db.ProjectBoards.Add(new ProjectBoard
        {
            Id               = "main-legacy",
            UserStoryBoardId = null,
        });
        await db.SaveChangesAsync();

        var board = await db.ProjectBoards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == "main-legacy");
        var effectiveBoardId = board?.UserStoryBoardId ?? "main-legacy";

        Assert.Equal("main-legacy", effectiveBoardId);
    }

    // ── T21: GetUserStoriesByModuleId endpoint resolves effectiveBoardId ──

    [Fact]
    public async Task T21_GetUserStoriesByModuleId_UsesUserStoryBoardId_WhenSet()
    {
        await using var db = CreateDb();
        db.ProjectBoards.Add(new ProjectBoard
        {
            Id               = "trimmed-main",
            UserStoryBoardId = "trimmed-us",
        });
        await db.SaveChangesAsync();

        var trimmedBoardId = "trimmed-main";
        var board = await db.ProjectBoards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == trimmedBoardId);
        var effectiveBoardId = board?.UserStoryBoardId ?? trimmedBoardId;

        Assert.Equal("trimmed-us", effectiveBoardId);
    }

    // ── T22: ModuleType filter excludes both type 1 (setup) and type 3 (data model) ──

    [Fact]
    public async Task T22_ModuleTypeFilter_ExcludesType1AndType3()
    {
        await using var db = CreateDb();
        db.ProjectModules.AddRange(
            new ProjectModule { Id = 1, ProjectId = 10, ModuleType = 1, Title = "Setup"      }, // excluded
            new ProjectModule { Id = 2, ProjectId = 10, ModuleType = 2, Title = "Feature A"  }, // included
            new ProjectModule { Id = 3, ProjectId = 10, ModuleType = 3, Title = "Data Model" }, // excluded
            new ProjectModule { Id = 4, ProjectId = 10, ModuleType = 2, Title = "Feature B"  }  // included
        );
        await db.SaveChangesAsync();

        var modules = await db.ProjectModules
            .Where(pm => pm.ProjectId == 10 && pm.ModuleType != 3 && pm.ModuleType != 1)
            .OrderBy(pm => pm.Id)
            .ToListAsync();

        Assert.Equal(2, modules.Count);
        Assert.All(modules, m => Assert.Equal(2, m.ModuleType));
    }

    // ── T23: ModuleType filter includes type 2, 4, null (anything not 1 or 3) ──

    [Fact]
    public async Task T23_ModuleTypeFilter_IncludesOtherTypes()
    {
        await using var db = CreateDb();
        db.ProjectModules.AddRange(
            new ProjectModule { Id = 10, ProjectId = 20, ModuleType = 2,    Title = "Module 2"    },
            new ProjectModule { Id = 11, ProjectId = 20, ModuleType = 4,    Title = "Module 4"    },
            new ProjectModule { Id = 12, ProjectId = 20, ModuleType = null,  Title = "No type"    }
        );
        await db.SaveChangesAsync();

        var modules = await db.ProjectModules
            .Where(pm => pm.ProjectId == 20 && pm.ModuleType != 3 && pm.ModuleType != 1)
            .ToListAsync();

        Assert.Equal(3, modules.Count);
    }

    // ── T24: effectiveBoardId differs per board — two boards in same DB resolve independently ──

    [Fact]
    public async Task T24_AiContext_TwoBoards_EachResolveIndependently()
    {
        await using var db = CreateDb();
        db.ProjectBoards.AddRange(
            new ProjectBoard { Id = "boardA", UserStoryBoardId = "usA" },
            new ProjectBoard { Id = "boardB", UserStoryBoardId = null  }
        );
        await db.SaveChangesAsync();

        var boardA = await db.ProjectBoards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == "boardA");
        var boardB = await db.ProjectBoards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == "boardB");

        var effectiveA = boardA?.UserStoryBoardId ?? "boardA";
        var effectiveB = boardB?.UserStoryBoardId ?? "boardB";

        Assert.Equal("usA",   effectiveA);
        Assert.Equal("boardB", effectiveB);
    }
}
