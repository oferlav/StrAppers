using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Services;

public static class InstituteAssistantChatHelper
{
    public const string SourceGeneral = "General";
    public const string SourceTemplates = "Templates";
    public const string SourceBrief = "Brief";
    public const string SourceModules = "Modules";
    public const string SourceCustomer = "Customer";

    public static bool IsValidSource(string? source) =>
        source != null &&
        AllowedSources.Contains(source.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns canonical source string (e.g. <c>General</c>) or null if not allowed.</summary>
    public static string? NormalizeToCanonicalSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var t = source.Trim();
        return AllowedSources.FirstOrDefault(a => string.Equals(a, t, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly HashSet<string> AllowedSources = new(StringComparer.OrdinalIgnoreCase)
    {
        SourceGeneral,
        SourceTemplates,
        SourceBrief,
        SourceModules,
        SourceCustomer,
    };

    /// <summary>Recent chat (user + assistant) for the last hour, for prompt context.</summary>
    /// <remarks>Pass either <paramref name="projectId"/> (legacy <c>Projects</c> row) or <paramref name="instituteProjectId"/> (not both).</remarks>
    public static async Task<string> BuildRecentContextBlockAsync(
        ApplicationDbContext db,
        int instituteId,
        int teacherId,
        int? projectId,
        int? instituteProjectId,
        string source,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        if (projectId.HasValue == instituteProjectId.HasValue)
        {
            return string.Empty;
        }

        var since = DateTime.UtcNow.AddHours(-1);
        var rows = await db.InstituteAssistantChatHistory
            .AsNoTracking()
            .Where(h =>
                h.InstituteId == instituteId &&
                h.TeacherId == teacherId &&
                h.Source == source &&
                h.CreatedAt >= since &&
                (instituteProjectId.HasValue
                    ? h.InstituteProjectId == instituteProjectId
                    : h.ProjectId == projectId && h.InstituteProjectId == null))
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("## Recent chat (last hour, same screen)");
        foreach (var h in rows)
        {
            var who = h.IsAssistant ? "Assistant" : "User";
            sb.AppendLine($"- {who}: {h.Message}");
        }
        sb.AppendLine();
        return sb.ToString();
    }

    public static async Task SaveTurnAsync(
        ApplicationDbContext db,
        int instituteId,
        int teacherId,
        int? projectId,
        int? instituteProjectId,
        string source,
        string? userMessage,
        string? assistantMessage,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        if (projectId.HasValue == instituteProjectId.HasValue)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(userMessage) && string.IsNullOrWhiteSpace(assistantMessage))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(userMessage))
        {
            db.InstituteAssistantChatHistory.Add(new InstituteAssistantChatHistory
            {
                InstituteId = instituteId,
                TeacherId = teacherId,
                ProjectId = projectId,
                InstituteProjectId = instituteProjectId,
                Source = source,
                IsAssistant = false,
                Message = userMessage.Trim(),
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (!string.IsNullOrWhiteSpace(assistantMessage))
        {
            db.InstituteAssistantChatHistory.Add(new InstituteAssistantChatHistory
            {
                InstituteId = instituteId,
                TeacherId = teacherId,
                ProjectId = projectId,
                InstituteProjectId = instituteProjectId,
                Source = source,
                IsAssistant = true,
                Message = assistantMessage.Trim(),
                CreatedAt = DateTime.UtcNow,
            });
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Institute assistant chat history save failed (non-fatal).");
        }
    }
}
