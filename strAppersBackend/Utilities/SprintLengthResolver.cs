using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Utilities;

/// <summary>
/// Resolves the effective sprint length in DAYS for a board. Day-based courses carry
/// <see cref="TrelloProjectCreationRequest.SprintLengthDays"/> inside their template JSON;
/// legacy boards fall back to the global <c>BusinessLogicConfig:SprintLengthInWeeks</c> × 7.
/// </summary>
public static class SprintLengthResolver
{
    /// <summary>Days from template JSON (<c>SprintLengthDays</c>), else <paramref name="configSprintLengthWeeks"/> × 7. Never &lt; 1.</summary>
    public static int ResolveFromJson(string? trelloBoardJson, int configSprintLengthWeeks)
    {
        var fallback = Math.Max(1, configSprintLengthWeeks * 7);
        if (string.IsNullOrWhiteSpace(trelloBoardJson))
            return fallback;
        try
        {
            var request = JsonSerializer.Deserialize<TrelloProjectCreationRequest>(trelloBoardJson);
            return request?.SprintLengthDays is int days ? Math.Max(1, days) : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    /// Loads the board's template JSON (InstituteProject.TrelloBoardJson preferred, Project.TrelloBoardJson
    /// fallback — same precedence as the sprint merge service) and resolves the sprint length in days.
    /// </summary>
    public static async Task<int> ResolveForBoardAsync(
        ApplicationDbContext ctx, string boardId, int configSprintLengthWeeks, CancellationToken ct = default)
    {
        var fallback = Math.Max(1, configSprintLengthWeeks * 7);
        if (string.IsNullOrWhiteSpace(boardId))
            return fallback;

        var board = await ctx.ProjectBoards.AsNoTracking()
            .Where(pb => pb.Id == boardId)
            .Select(pb => new { pb.InstituteProjectId, pb.ProjectId })
            .FirstOrDefaultAsync(ct);
        if (board == null)
            return fallback;

        string? json = null;
        if (board.InstituteProjectId.HasValue)
        {
            json = await ctx.InstituteProjects.AsNoTracking()
                .Where(ip => ip.Id == board.InstituteProjectId.Value)
                .Select(ip => ip.TrelloBoardJson)
                .FirstOrDefaultAsync(ct);
        }
        if (string.IsNullOrWhiteSpace(json))
        {
            json = await ctx.Projects.AsNoTracking()
                .Where(p => p.Id == board.ProjectId)
                .Select(p => p.TrelloBoardJson)
                .FirstOrDefaultAsync(ct);
        }

        return ResolveFromJson(json, configSprintLengthWeeks);
    }
}
