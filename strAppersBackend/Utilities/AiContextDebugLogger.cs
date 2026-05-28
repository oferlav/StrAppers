using strAppersBackend.Services;

namespace strAppersBackend.Utilities;

/// <summary>
/// Sends a debug email when <c>Debug:AiContext = true</c> so each AI call logs which DB tables were
/// used for context. Follows the same silent-fail pattern as <c>FlushDebugLog</c> in BoardsController —
/// failures never interrupt the AI flow.
/// </summary>
public static class AiContextDebugLogger
{
    public static async Task LogAndEmailAsync(
        ISmtpEmailService smtpEmail,
        string endpoint,
        string boardId,
        int studentId,
        int sprint,
        int? boardInstituteProjectId,
        string? projectSource,
        string? descriptionSnippet,
        string? customerPastStorySnippet,
        string? moduleSource = null,
        int? moduleId = null,
        string? moduleTitle = null)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== AI Context Debug ===");
            sb.AppendLine($"Timestamp:  {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
            sb.AppendLine($"Endpoint:   {endpoint}");
            sb.AppendLine($"BoardId:    {boardId}");
            sb.AppendLine($"StudentId:  {studentId}");
            sb.AppendLine($"Sprint:     {sprint}");
            sb.AppendLine($"BoardInstituteProjectId: {boardInstituteProjectId?.ToString() ?? "(null — catalog/B2C board)"}");
            sb.AppendLine();
            sb.AppendLine("--- Project Context (Pattern A) ---");
            sb.AppendLine($"Source:     {projectSource ?? "(not resolved)"}");
            if (!string.IsNullOrWhiteSpace(descriptionSnippet))
                sb.AppendLine($"Description[0..200]:       {Snippet(descriptionSnippet, 200)}");
            if (!string.IsNullOrWhiteSpace(customerPastStorySnippet))
                sb.AppendLine($"CustomerPastStory[0..200]: {Snippet(customerPastStorySnippet, 200)}");
            sb.AppendLine();
            if (moduleSource != null || moduleId.HasValue)
            {
                sb.AppendLine("--- Module Context (Pattern B) ---");
                if (moduleSource != null)
                    sb.AppendLine($"Source:     {moduleSource}");
                if (moduleId.HasValue)
                    sb.AppendLine($"ModuleId:   {moduleId.Value}");
                if (!string.IsNullOrWhiteSpace(moduleTitle))
                    sb.AppendLine($"ModuleTitle: {moduleTitle}");
            }

            var body = sb.ToString();
            await smtpEmail.SendPlainEmailAsync(
                "ofer@skill-in.com",
                $"[AiContext Debug] {endpoint} BoardId={boardId}",
                body);
        }
        catch { /* never interrupt AI flow */ }
    }

    private static string Snippet(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "(empty)" : s.Length <= max ? s.Trim() : s[..max].Trim() + "…";
}
