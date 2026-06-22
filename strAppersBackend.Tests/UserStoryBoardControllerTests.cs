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
}
