using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using strAppersBackend.Utilities;

namespace strAppersBackend.Controllers;

/// <summary>Metric adherence and related checks (starts with Adherence).</summary>
[ApiController]
[Route("api/[controller]")]
public partial class MetricsController : ControllerBase
{
    private const int AdherenceMetricId = 1;

    private readonly ApplicationDbContext _context;
    private readonly ITrelloService _trelloService;
    private readonly IGitHubService _githubService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MetricsController> _logger;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PromptConfig _promptConfig;
    private readonly IMicrosoftGraphService _graphService;
    private readonly ISmtpEmailService _smtpEmailService;

    private bool DebugAiContext => _configuration.GetValue<bool>("Debug:AiContext", false);

    public MetricsController(
        ApplicationDbContext context,
        ITrelloService trelloService,
        IGitHubService githubService,
        IConfiguration configuration,
        ILogger<MetricsController> logger,
        IChatCompletionService chatCompletionService,
        IHttpClientFactory httpClientFactory,
        IOptions<PromptConfig> promptConfig,
        IMicrosoftGraphService graphService,
        ISmtpEmailService smtpEmailService)
    {
        _context = context;
        _trelloService = trelloService;
        _githubService = githubService;
        _configuration = configuration;
        _logger = logger;
        _chatCompletionService = chatCompletionService;
        _httpClientFactory = httpClientFactory;
        _promptConfig = promptConfig.Value;
        _graphService = graphService;
        _smtpEmailService = smtpEmailService;
    }

    public class AdherenceRequest
    {
        public string BoardId { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public int SprintNumber { get; set; }
    }

    /// <summary>
    /// Adherence metric: reads Trello sprint card checkboxes (Required Skill Data / Required Resource Data) and
    /// appends findings to ReviewContent based on role (commits, Figma, user story, CRM, resources).
    /// Full Stack / Fullstack roles use the Backend Developer and Frontend Developer sprint cards (merged checkboxes; skill checks include both GitHub tracks).
    /// </summary>
    [HttpPost("use/Adherence")]
    public async Task<ActionResult<object>> Adherence([FromBody] AdherenceRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { success = false, message = "Request body is required." });
        if (string.IsNullOrWhiteSpace(request.BoardId))
            return BadRequest(new { success = false, message = "BoardId is required." });
        if (request.StudentId <= 0)
            return BadRequest(new { success = false, message = "StudentId is required." });
        if (request.SprintNumber < 0)
            return BadRequest(new { success = false, message = "SprintNumber must be >= 0." });

        var boardId = request.BoardId.Trim();

        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentRoles)
            .ThenInclude(sr => sr.Role)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);
        if (student == null)
            return NotFound(new { success = false, message = $"Student {request.StudentId} not found." });
        if (!string.Equals(student.BoardId?.Trim(), boardId, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, message = "Student is not assigned to this BoardId." });

        var board = await _context.ProjectBoards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boardId, cancellationToken);
        if (board == null)
            return NotFound(new { success = false, message = $"Board {boardId} not found." });

        var activeRole = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive);
        var roleName = activeRole?.Role?.Name?.Trim() ?? "Team Member";
        var fullStackRole = IsFullStackRole(roleName);

        string? cardId;
        string? sprintBackendCardId = null;
        string? sprintFrontendCardId = null;
        bool requiredSkill;
        bool requiredResource;

        // For Full Stack: check if the board has a Full Stack sprint card first; if not, fall back to Backend Developer + Frontend Developer.
        if (fullStackRole)
        {
            var fsLabels = await _trelloService.ResolveSprintLabelsAsync(boardId, request.SprintNumber, roleName);
            if (fsLabels.Length == 1)
            {
                // Board has a Full Stack card — use it as the single card
                sprintBackendCardId = await _trelloService.GetSprintRoleCardIdAsync(boardId, request.SprintNumber, fsLabels[0]);
                sprintFrontendCardId = null;
            }
            else
            {
                sprintBackendCardId = await _trelloService.GetSprintRoleCardIdAsync(boardId, request.SprintNumber, "Backend Developer");
                sprintFrontendCardId = await _trelloService.GetSprintRoleCardIdAsync(boardId, request.SprintNumber, "Frontend Developer");
            }

            if (string.IsNullOrEmpty(sprintBackendCardId) && string.IsNullOrEmpty(sprintFrontendCardId))
            {
                return Ok(new
                {
                    success = true,
                    metricId = AdherenceMetricId,
                    reviewContent = "",
                    message = $"No sprint card found in Sprint {request.SprintNumber} for Full Stack role (checked for \"{roleName}\", \"Backend Developer\", and \"Frontend Developer\").",
                    requiredSkillData = false,
                    requiredResourceData = false
                });
            }

            var fieldsBackend = string.IsNullOrEmpty(sprintBackendCardId)
                ? null
                : await _trelloService.GetCardCustomFieldsAsync(boardId, sprintBackendCardId);
            var fieldsFrontend = string.IsNullOrEmpty(sprintFrontendCardId)
                ? null
                : await _trelloService.GetCardCustomFieldsAsync(boardId, sprintFrontendCardId);

            var skillName = TrelloRequiredDataFieldRules.RequiredSkillDataFieldName;
            var resName = TrelloRequiredDataFieldRules.RequiredResourceDataFieldName;
            requiredSkill = IsCheckboxChecked(fieldsBackend, skillName) || IsCheckboxChecked(fieldsFrontend, skillName);
            requiredResource = IsCheckboxChecked(fieldsBackend, resName) || IsCheckboxChecked(fieldsFrontend, resName);
            cardId = sprintBackendCardId ?? sprintFrontendCardId;
        }
        else
        {
            cardId = await _trelloService.GetSprintRoleCardIdAsync(boardId, request.SprintNumber, roleName);
            if (string.IsNullOrEmpty(cardId))
            {
                return Ok(new
                {
                    success = true,
                    metricId = AdherenceMetricId,
                    reviewContent = "",
                    message = $"No sprint card found in Sprint {request.SprintNumber} with label matching role \"{roleName}\".",
                    requiredSkillData = false,
                    requiredResourceData = false
                });
            }

            var customFields = await _trelloService.GetCardCustomFieldsAsync(boardId, cardId);
            requiredSkill = IsCheckboxChecked(customFields, TrelloRequiredDataFieldRules.RequiredSkillDataFieldName);
            requiredResource = IsCheckboxChecked(customFields, TrelloRequiredDataFieldRules.RequiredResourceDataFieldName);
        }

        var lines = new List<string>();

        if (requiredSkill)
        {
            var roleIndex = board.IsSingleRole ? student.RoleIndex : 0;
            await AppendSkillDataFindingsAsync(lines, boardId, board, request.SprintNumber, student.Id, roleName, cancellationToken, roleIndex);
        }

        bool? resourceArtifactPresent = null;
        if (requiredResource)
        {
            var hasArtifact = await HasResourceArtifactAsync(boardId, student.Id, request.SprintNumber, cancellationToken);
            resourceArtifactPresent = hasArtifact;
            if (!hasArtifact)
                lines.Add("A required resource artifact was not completed or was not shared.");
        }

        var reviewContent = string.Join(Environment.NewLine, lines.Where(l => !string.IsNullOrWhiteSpace(l)));

        // Skill checks log via GitHubService; resource uses DB only — this line is the only place to see why the resource sentence was skipped.
        _logger.LogInformation(
            "Adherence: board {BoardId} student {StudentId} sprint {SprintNumber} role {RoleName} requiredSkillData={RequiredSkillData} requiredResourceData={RequiredResourceData} resourceArtifactPresent={ResourceArtifactPresent} reviewLineCount={ReviewLineCount}",
            boardId,
            student.Id,
            request.SprintNumber,
            roleName,
            requiredSkill,
            requiredResource,
            resourceArtifactPresent,
            lines.Count);

        await UpsertCacheMetricsAsync(boardId, student.Id, request.SprintNumber, AdherenceMetricId, reviewContent, graphBase64: null, cancellationToken);

        if (fullStackRole)
        {
            return Ok(new
            {
                success = true,
                metricId = AdherenceMetricId,
                reviewContent,
                requiredSkillData = requiredSkill,
                requiredResourceData = requiredResource,
                sprintRoleCardId = cardId,
                sprintRoleBackendCardId = sprintBackendCardId,
                sprintRoleFrontendCardId = sprintFrontendCardId
            });
        }

        return Ok(new
        {
            success = true,
            metricId = AdherenceMetricId,
            reviewContent,
            requiredSkillData = requiredSkill,
            requiredResourceData = requiredResource,
            sprintRoleCardId = cardId
        });
    }

    private static bool IsCheckboxChecked(IReadOnlyDictionary<string, string>? fields, string displayName)
    {
        if (fields == null || fields.Count == 0)
            return false;
        foreach (var kv in fields)
        {
            if (!string.Equals(kv.Key.Trim(), displayName.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;
            var v = kv.Value?.Trim() ?? "";
            return v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1";
        }

        return false;
    }

    private async Task AppendSkillDataFindingsAsync(
        List<string> lines,
        string boardId,
        ProjectBoard board,
        int sprintNumber,
        int studentId,
        string roleName,
        CancellationToken cancellationToken,
        int roleIndex = 0)
    {
        var rn = roleName;

        // Designer / UI-UX (before generic Developer — e.g. UI/UX Designer matches Designer).
        // Figma links are skill work (IsFigma=true); they never count as "resource artifacts" (see HasResourceArtifactAsync).
        if (ContainsDesigner(rn))
        {
            var hasFigma = await _context.Resources.AsNoTracking()
                .AnyAsync(r => r.BoardId == boardId && r.StudentId == studentId && r.IsFigma &&
                               (r.SprintNumber == sprintNumber || r.SprintNumber == null), cancellationToken);
            if (!hasFigma)
                lines.Add("Figma work was not completed or was not shared for this sprint.");
            return;
        }

        if (ContainsProduct(rn))
        {
            string? moduleId = null;
            foreach (var pmLabel in new[] { "Product Manager", "PM" })
            {
                moduleId = await _trelloService.GetModuleIdFromSprintCardAsync(boardId, sprintNumber, pmLabel);
                if (!string.IsNullOrWhiteSpace(moduleId))
                    break;
            }

            if (string.IsNullOrWhiteSpace(moduleId))
            {
                lines.Add("No substantive user story was written for this sprint (user story text and checklists should exceed a few words).");
                return;
            }

            var usResult = await _trelloService.GetUserStoryCardByModuleIdAsync(boardId, moduleId.Trim());
            var card = GetUserStoryCardFromResult(usResult);
            var wordCount = CountWords(ConcatenateUserStoryText(card));
            if (wordCount <= 10)
                lines.Add("No substantive user story was written for this sprint (user story text and checklists should exceed a few words).");
            return;
        }

        if (IsMarketingOrBizDev(rn))
        {
            if (!await HasStakeholderDataInSprintWindowAsync(boardId, sprintNumber, cancellationToken))
                lines.Add("No CRM data was captured for this sprint.");
            return;
        }

        if (ContainsDeveloper(rn))
        {
            var token = _configuration["GitHub:AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Adherence: GitHub token missing; skipping commit checks.");
                return;
            }

            var backendUrl = board.GithubBackendUrl;
            var frontendUrl = board.GithubFrontendUrl;

            if (IsFullStackRole(rn))
            {
                var bOk = await BranchHasAnyCommitAsync(backendUrl, sprintNumber, isBackend: true, token, roleIndex);
                var fOk = await BranchHasAnyCommitAsync(frontendUrl, sprintNumber, isBackend: false, token, roleIndex);
                if (!bOk)
                    lines.Add("Backend work was not completed or was not committed for this sprint.");
                if (!fOk)
                    lines.Add("Frontend work was not completed or was not committed for this sprint.");
                return;
            }

            if (IsBackendDeveloperRole(rn))
            {
                if (!await BranchHasAnyCommitAsync(backendUrl, sprintNumber, isBackend: true, token, roleIndex))
                    lines.Add("Backend work was not completed or was not committed for this sprint.");
                return;
            }

            if (IsFrontendDeveloperRole(rn))
            {
                if (!await BranchHasAnyCommitAsync(frontendUrl, sprintNumber, isBackend: false, token, roleIndex))
                    lines.Add("Frontend work was not completed or was not committed for this sprint.");
                return;
            }

            // Generic "Developer" without Backend/Frontend/Full Stack: require a commit on either track
            var anyB = await BranchHasAnyCommitAsync(backendUrl, sprintNumber, isBackend: true, token, roleIndex);
            var anyF = await BranchHasAnyCommitAsync(frontendUrl, sprintNumber, isBackend: false, token, roleIndex);
            if (!anyB && !anyF)
                lines.Add("No committed work was found on either the backend or frontend branch for this sprint.");
        }
    }

    private static bool ContainsDesigner(string roleName) =>
        roleName.Contains("Designer", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsProduct(string roleName) =>
        roleName.Contains("Product", StringComparison.OrdinalIgnoreCase);

    private static bool IsMarketingOrBizDev(string roleName)
    {
        var r = roleName.ToLowerInvariant();
        return r.Contains("marketing", StringComparison.Ordinal) ||
               r.Contains("bizdev", StringComparison.Ordinal) ||
               r.Contains("business development", StringComparison.Ordinal);
    }

    private static bool ContainsDeveloper(string roleName) =>
        roleName.Contains("Developer", StringComparison.OrdinalIgnoreCase);

    private static bool IsFullStackRole(string roleName) =>
        roleName.Contains("Full Stack", StringComparison.OrdinalIgnoreCase) ||
        roleName.Contains("Fullstack", StringComparison.OrdinalIgnoreCase);

    private static bool IsBackendDeveloperRole(string roleName) =>
        roleName.Contains("Backend", StringComparison.OrdinalIgnoreCase);

    private static bool IsFrontendDeveloperRole(string roleName) =>
        roleName.Contains("Frontend", StringComparison.OrdinalIgnoreCase);

    private async Task<bool> BranchHasAnyCommitAsync(string? githubRepoUrl, int sprintNumber, bool isBackend, string token, int roleIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(githubRepoUrl))
            return false;
        if (!TryParseOwnerRepo(githubRepoUrl, out var owner, out var repo))
            return false;

        var idxSuffix = roleIndex > 0 ? $"-{roleIndex}" : "";
        var branch = sprintNumber == 0
            ? (isBackend ? $"Bugs-B{idxSuffix}" : $"Bugs-F{idxSuffix}")
            : $"{sprintNumber}-{(isBackend ? "B" : "F")}{idxSuffix}";

        var commits = await _githubService.GetRecentCommitsOnBranchAsync(owner, repo, branch, 1, token);
        return commits.Count > 0;
    }

    private static bool TryParseOwnerRepo(string githubUrl, out string owner, out string repo)
    {
        owner = "";
        repo = "";
        try
        {
            var uri = new Uri(githubUrl.Trim());
            var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return false;
            owner = parts[0];
            repo = parts[1].EndsWith(".git", StringComparison.Ordinal) ? parts[1][..^4] : parts[1];
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Whether the student has a non-Figma <see cref="Resource"/> that counts for this sprint's "required resource data" check.
    /// Figma entries (<see cref="Resource.IsFigma"/> true) are designer skill work, not resource artifacts.
    /// For Sprint 1+, only rows with <c>SprintNumber == sprintNumber</c> count — untagged rows (<c>null</c>)
    /// do not satisfy every sprint (otherwise a single legacy link would suppress the resource line for all sprints).
    /// Sprint 0 still allows null or 0 for board-level links.
    /// </summary>
    private async Task<bool> HasResourceArtifactAsync(string boardId, int studentId, int sprintNumber, CancellationToken cancellationToken)
    {
        if (sprintNumber <= 0)
        {
            return await _context.Resources.AsNoTracking()
                .AnyAsync(r => r.BoardId == boardId && r.StudentId == studentId &&
                               !r.IsFigma &&
                               (r.SprintNumber == sprintNumber || r.SprintNumber == null), cancellationToken);
        }

        return await _context.Resources.AsNoTracking()
            .AnyAsync(r => r.BoardId == boardId && r.StudentId == studentId && r.SprintNumber == sprintNumber && !r.IsFigma, cancellationToken);
    }

    private async Task<bool> HasStakeholderDataInSprintWindowAsync(string boardId, int sprintNumber, CancellationToken cancellationToken)
    {
        if (sprintNumber == 0)
            return await _context.Stakeholders.AsNoTracking()
                .AnyAsync(s => s.BoardId == boardId, cancellationToken);

        var board = await _context.ProjectBoards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boardId, cancellationToken);
        if (board == null)
            return false;

        var sprintLengthWeeks = _configuration.GetValue("BusinessLogicConfig:SprintLengthInWeeks", 1);
        var sprintMerge = await _context.ProjectBoardSprintMerges.AsNoTracking()
            .FirstOrDefaultAsync(m => m.ProjectBoardId == boardId && m.SprintNumber == sprintNumber, cancellationToken);

        DateTime windowStartUtc;
        DateTime windowEndInclusiveUtc;
        var haveWindow =
            SprintPlanDateResolver.TryGetInclusiveUtcRangeFromSprintMerge(
                sprintMerge, sprintNumber, sprintLengthWeeks, out windowStartUtc, out windowEndInclusiveUtc)
            || SprintPlanDateResolver.TryGetSprintInclusiveUtcRange(
                board.SprintPlan, board.StartDate, sprintNumber, out windowStartUtc, out windowEndInclusiveUtc);

        if (!haveWindow)
            return await _context.Stakeholders.AsNoTracking().AnyAsync(s => s.BoardId == boardId, cancellationToken);

        return await _context.Stakeholders.AsNoTracking()
            .AnyAsync(s => s.BoardId == boardId &&
                           ((s.CreatedAt != null && s.CreatedAt >= windowStartUtc && s.CreatedAt <= windowEndInclusiveUtc) ||
                            (s.UpdatedAt != null && s.UpdatedAt >= windowStartUtc && s.UpdatedAt <= windowEndInclusiveUtc)),
                cancellationToken);
    }

    private static object? GetUserStoryCardFromResult(object? getUserStoryResult)
    {
        if (getUserStoryResult == null) return null;
        var t = getUserStoryResult.GetType();
        var success = t.GetProperty("Success")?.GetValue(getUserStoryResult) is bool b && b;
        if (!success) return null;
        return t.GetProperty("Card")?.GetValue(getUserStoryResult);
    }

    private static string ConcatenateUserStoryText(object? card)
    {
        if (card == null) return "";
        var t = card.GetType();
        var name = t.GetProperty("Name")?.GetValue(card)?.ToString() ?? "";
        var desc = t.GetProperty("Description")?.GetValue(card)?.ToString() ?? "";
        var sb = new StringBuilder();
        sb.Append(name).Append(' ').Append(desc);
        var checklists = t.GetProperty("Checklists")?.GetValue(card) as System.Collections.IEnumerable;
        if (checklists != null)
        {
            foreach (var cl in checklists)
            {
                if (cl == null) continue;
                var clt = cl.GetType();
                var items = clt.GetProperty("CheckItems")?.GetValue(cl) as System.Collections.IEnumerable;
                if (items == null) continue;
                foreach (var ci in items)
                {
                    if (ci == null) continue;
                    var cit = ci.GetType();
                    var itemName = cit.GetProperty("Name")?.GetValue(ci)?.ToString() ?? "";
                    sb.Append(' ').Append(itemName);
                }
            }
        }

        return sb.ToString();
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return Regex.Matches(text.Trim(), @"\b\w+\b", RegexOptions.None).Count;
    }

    /// <param name="graph2Base64">Optional second chart (e.g. frontend track). Ignored when null in append mode.</param>
    /// <param name="appendReviewContent">When true, appends <paramref name="reviewContent"/> and only sets Graph/Graph2 when the corresponding argument is non-null.</param>
    private async Task UpsertCacheMetricsAsync(
        string boardId,
        int studentId,
        int sprintNumber,
        int metricId,
        string reviewContent,
        string? graphBase64,
        CancellationToken cancellationToken,
        string? graph2Base64 = null,
        bool appendReviewContent = false)
    {
        try
        {
            var existing = await _context.CacheMetrics
                .FirstOrDefaultAsync(
                    c => c.BoardId == boardId && c.StudentId == studentId && c.SprintNumber == sprintNumber && c.MetricId == metricId,
                    cancellationToken);
            if (existing != null)
            {
                if (appendReviewContent)
                {
                    existing.ReviewContent = string.IsNullOrEmpty(existing.ReviewContent)
                        ? reviewContent
                        : existing.ReviewContent + "\n\n" + reviewContent;
                    if (graphBase64 != null)
                        existing.Graph = graphBase64;
                    if (graph2Base64 != null)
                        existing.Graph2 = graph2Base64;
                }
                else
                {
                    existing.ReviewContent = reviewContent;
                    existing.Graph = graphBase64;
                    existing.Graph2 = graph2Base64;
                }
            }
            else
            {
                _context.CacheMetrics.Add(new CacheMetrics
                {
                    BoardId = boardId,
                    StudentId = studentId,
                    SprintNumber = sprintNumber,
                    MetricId = metricId,
                    ReviewContent = reviewContent,
                    Graph = graphBase64,
                    Graph2 = graph2Base64
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CacheMetrics upsert failed for board {BoardId}, metric {MetricId}", boardId, metricId);
        }
    }
}
