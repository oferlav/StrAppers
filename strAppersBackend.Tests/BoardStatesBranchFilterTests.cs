using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests the branch filter logic from MentorController ~line 5824.
/// Logic: IsSingleRole == true AND RoleIndex > 0 → keep only records where
/// GithubBranch is empty/null OR ends with -{RoleIndex}.
/// </summary>
public class BoardStatesBranchFilterTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    // Replicates the LINQ filter from MentorController ~line 5824-5829.
    private static IQueryable<BoardState> ApplyBranchFilter(
        IQueryable<BoardState> query, bool isSingleRole, int roleIndex)
    {
        if (isSingleRole && roleIndex > 0)
        {
            var suffix = $"-{roleIndex}";
            query = query.Where(bs =>
                string.IsNullOrEmpty(bs.GithubBranch) ||
                bs.GithubBranch.EndsWith(suffix));
        }
        return query;
    }

    private static BoardState State(int id, string boardId, string? branch) =>
        new()
        {
            Id = id,
            BoardId = boardId,
            Source = string.IsNullOrEmpty(branch) ? "Railway" : "GitHub",
            GithubBranch = branch,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    [Fact]
    public async Task SingleRole_RoleIndex2_IncludesOwnBranchesAndSharedRailway()
    {
        await using var db = CreateDb();
        const string board = "board1";

        db.BoardStates.AddRange(
            State(1, board, "2-B-2"),    // own backend branch → include
            State(2, board, "2-F-2"),    // own frontend branch → include
            State(3, board, "2-B-1"),    // other dev's backend → exclude
            State(4, board, "2-F-1"),    // other dev's frontend → exclude
            State(5, board, null),       // Railway (null branch) → include
            State(6, board, "")          // Railway (empty branch) → include
        );
        await db.SaveChangesAsync();

        var query = db.BoardStates.Where(bs => bs.BoardId == board);
        var result = await ApplyBranchFilter(query, isSingleRole: true, roleIndex: 2).ToListAsync();

        Assert.Equal(4, result.Count);
        Assert.Contains(result, bs => bs.GithubBranch == "2-B-2");
        Assert.Contains(result, bs => bs.GithubBranch == "2-F-2");
        Assert.Contains(result, bs => bs.GithubBranch == null);
        Assert.Contains(result, bs => bs.GithubBranch == "");
        Assert.DoesNotContain(result, bs => bs.GithubBranch == "2-B-1");
        Assert.DoesNotContain(result, bs => bs.GithubBranch == "2-F-1");
    }

    [Fact]
    public async Task SquadCourse_IsSingleRoleFalse_ReturnsAllBoardStates()
    {
        await using var db = CreateDb();
        const string board = "board2";

        db.BoardStates.AddRange(
            State(10, board, "1-B"),
            State(11, board, "1-F"),
            State(12, board, null)
        );
        await db.SaveChangesAsync();

        var query = db.BoardStates.Where(bs => bs.BoardId == board);
        var result = await ApplyBranchFilter(query, isSingleRole: false, roleIndex: 1).ToListAsync();

        // No filter applied — all 3 returned
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task SingleRole_RoleIndex0_ReturnsAllBoardStates()
    {
        await using var db = CreateDb();
        const string board = "board3";

        db.BoardStates.AddRange(
            State(20, board, "1-B-1"),
            State(21, board, "1-F-2"),
            State(22, board, null)
        );
        await db.SaveChangesAsync();

        var query = db.BoardStates.Where(bs => bs.BoardId == board);
        var result = await ApplyBranchFilter(query, isSingleRole: true, roleIndex: 0).ToListAsync();

        // RoleIndex == 0 → filter skipped → all 3 returned
        Assert.Equal(3, result.Count);
    }
}
