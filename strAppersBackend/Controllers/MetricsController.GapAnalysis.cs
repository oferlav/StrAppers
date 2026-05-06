using System.ComponentModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;
using strAppersBackend.Services;
using strAppersBackend.Utilities;

namespace strAppersBackend.Controllers;

public partial class MetricsController
{
    private const int GapAnalysisMetricId = 2;

    /// <summary>Labels used to find the Product Manager sprint card for ModuleId (user story link).</summary>
    private static readonly string[] GapAnalysisPmSprintCardLabels = { "Product Manager", "PM" };

    public class GapAnalysisRequest
    {
        public string BoardId { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public int SprintNumber { get; set; }
        /// <summary>When true, returns only the generated system and user prompts (no LLM call, no CacheMetrics update). Omit or false for normal gap analysis.</summary>
        [DefaultValue(false)]
        public bool Test { get; set; } = false;
    }

    /// <summary>
    /// Sprint gap analysis (MetricId 2): compares sprint requirements to delivered artifacts; stores <see cref="CacheMetrics.ReviewContent"/> and base64 PNG bar chart(s) in <see cref="CacheMetrics.Graph"/> and optional <see cref="CacheMetrics.Graph2"/>.
    /// Full Stack runs two separate analyses (backend repo vs frontend repo); the model is not told “full stack”—only “backend” or “frontend” expert. Backend result is saved first; frontend narrative and chart are appended (<see cref="CacheMetrics.Graph2"/>).
    /// Trello cards are matched by the green label: <see cref="Role.Description"/> if set, otherwise <see cref="Role.Name"/> (same board convention as “Role Description” labels).
    /// Set <see cref="GapAnalysisRequest.Test"/> to true to return only generated system and user prompts (no LLM, no DB write).
    /// </summary>
    [HttpPost("use/GapAnalysis")]
    public async Task<ActionResult<object>> GapAnalysis([FromBody] GapAnalysisRequest? request, CancellationToken cancellationToken)
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
        var roleDesc = string.IsNullOrWhiteSpace(activeRole?.Role?.Description)
            ? roleName
            : $"{roleName} — {activeRole!.Role!.Description!.Trim()}";

        try
        {
            if (request.Test)
            {
                if (IsFullStackRole(roleName))
                {
                    var bePrompts = await BuildGapAnalysisPromptsForTrackAsync(
                        boardId, board, student.Id, request.SprintNumber, "Backend Developer", $"{roleDesc} (backend repository)", roleName, isBackend: true, cancellationToken);
                    var fePrompts = await BuildGapAnalysisPromptsForTrackAsync(
                        boardId, board, student.Id, request.SprintNumber, "Frontend Developer", $"{roleDesc} (frontend repository)", roleName, isBackend: false, cancellationToken);
                    return Ok(new
                    {
                        success = true,
                        test = true,
                        message = "Test mode: LLM not called; CacheMetrics not updated.",
                        backend = new { systemPrompt = bePrompts.SystemPrompt, userPrompt = bePrompts.UserPrompt },
                        frontend = new { systemPrompt = fePrompts.SystemPrompt, userPrompt = fePrompts.UserPrompt },
                    });
                }

                var trelloLabelTest = ResolveTrelloSprintCardLabel(activeRole?.Role, fullStackTrackLabel: null);
                var backendFirstTest = IsBackendDeveloperRole(roleName) ||
                    (ContainsDeveloper(roleName) && !IsFrontendDeveloperRole(roleName));
                var singlePrompts = await BuildGapAnalysisPromptsForTrackAsync(
                    boardId, board, student.Id, request.SprintNumber, trelloLabelTest, roleDesc, roleName,
                    isBackend: backendFirstTest,
                    cancellationToken);
                return Ok(new
                {
                    success = true,
                    test = true,
                    message = "Test mode: LLM not called; CacheMetrics not updated.",
                    systemPrompt = singlePrompts.SystemPrompt,
                    userPrompt = singlePrompts.UserPrompt,
                });
            }

            if (IsFullStackRole(roleName))
            {
                var be = await RunGapAnalysisForTrackAsync(
                    boardId, board, student.Id, request.SprintNumber, "Backend Developer", $"{roleDesc} (backend repository)", roleName, isBackend: true, cancellationToken);

                if (!be.ParsedOk)
                {
                    return UnprocessableEntity(new
                    {
                        success = false,
                        message = "Gap analysis did not return valid JSON for the backend track. Nothing was saved to CacheMetrics.",
                        backendParsed = false,
                        backendPreview = Truncate(be.Narrative, 2000),
                    });
                }

                var beGraphB64 = GapAnalysisBarChartRenderer.ToBase64Png(GapAnalysisBarChartRenderer.RenderSingleChart(be.ChartRows, "Backend"));
                var beSection = "## Backend (repository)\n\n" + be.Narrative;
                await UpsertCacheMetricsAsync(boardId, student.Id, request.SprintNumber, GapAnalysisMetricId, beSection, beGraphB64, cancellationToken);

                var fe = await RunGapAnalysisForTrackAsync(
                    boardId, board, student.Id, request.SprintNumber, "Frontend Developer", $"{roleDesc} (frontend repository)", roleName, isBackend: false, cancellationToken);

                if (!fe.ParsedOk)
                {
                    return UnprocessableEntity(new
                    {
                        success = false,
                        message = "Gap analysis did not return valid JSON for the frontend track. Backend result was saved to CacheMetrics; frontend chart was not appended.",
                        backendParsed = true,
                        frontendParsed = false,
                        backendPreview = Truncate(be.Narrative, 2000),
                        frontendPreview = Truncate(fe.Narrative, 2000),
                    });
                }

                var feGraphB64 = GapAnalysisBarChartRenderer.ToBase64Png(GapAnalysisBarChartRenderer.RenderSingleChart(fe.ChartRows, "Frontend"));
                var feSection = "## Frontend (repository)\n\n" + fe.Narrative;
                await UpsertCacheMetricsAsync(
                    boardId, student.Id, request.SprintNumber, GapAnalysisMetricId, feSection, null, cancellationToken,
                    graph2Base64: feGraphB64, appendReviewContent: true);

                var combinedNarrative = beSection + "\n\n" + feSection;
                var stackedB64 = GapAnalysisBarChartRenderer.ToBase64Png(
                    GapAnalysisBarChartRenderer.RenderStackedCharts(be.ChartRows, fe.ChartRows));

                return Ok(new
                {
                    success = true,
                    metricId = GapAnalysisMetricId,
                    reviewContent = combinedNarrative,
                    graphBase64 = beGraphB64,
                    graph2Base64 = feGraphB64,
                    graphStackedBase64 = stackedB64,
                    tracks = new { backend = be.RawModel, frontend = fe.RawModel }
                });
            }

            var trelloLabel = ResolveTrelloSprintCardLabel(activeRole?.Role, fullStackTrackLabel: null);
            var backendFirst = IsBackendDeveloperRole(roleName) ||
                (ContainsDeveloper(roleName) && !IsFrontendDeveloperRole(roleName));
            var single = await RunGapAnalysisForTrackAsync(
                boardId, board, student.Id, request.SprintNumber, trelloLabel, roleDesc, roleName,
                isBackend: backendFirst,
                cancellationToken);

            if (!single.ParsedOk)
            {
                return UnprocessableEntity(new
                {
                    success = false,
                    message = "Gap analysis did not return valid JSON. Nothing was saved to CacheMetrics.",
                    preview = Truncate(single.Narrative, 4000),
                });
            }

            // Backend/Frontend headlines on charts are only for full-stack (two repos); single-track roles get no section title.
            var graphSingle = GapAnalysisBarChartRenderer.ToBase64Png(GapAnalysisBarChartRenderer.RenderSingleChart(single.ChartRows));
            await UpsertCacheMetricsAsync(boardId, student.Id, request.SprintNumber, GapAnalysisMetricId, single.Narrative, graphSingle, cancellationToken);

            return Ok(new
            {
                success = true,
                metricId = GapAnalysisMetricId,
                reviewContent = single.Narrative,
                graphBase64 = graphSingle,
                model = single.RawModel
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GapAnalysis failed for board {BoardId}", boardId);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <param name="ParsedOk">False when the model output was not valid JSON — caller must not persist.</param>
    private sealed record GapTrackResult(string Narrative, List<(string Label, int Score)> ChartRows, object? RawModel, bool ParsedOk);

    /// <summary>Board sprint cards use a label that matches the role: prefer database <see cref="Role.Description"/>, then <see cref="Role.Name"/>. Full-stack tracks use fixed developer labels.</summary>
    private static string ResolveTrelloSprintCardLabel(Role? role, string? fullStackTrackLabel)
    {
        if (!string.IsNullOrWhiteSpace(fullStackTrackLabel))
            return fullStackTrackLabel.Trim();
        if (role == null)
            return "Team Member";
        if (!string.IsNullOrWhiteSpace(role.Description))
            return role.Description.Trim();
        return role.Name.Trim();
    }

    private sealed record GapAnalysisPrompts(string SystemPrompt, string UserPrompt);

    private async Task<GapAnalysisPrompts> BuildGapAnalysisPromptsForTrackAsync(
        string boardId,
        ProjectBoard board,
        int studentId,
        int sprintNumber,
        string trelloRoleLabel,
        string expertRoleDescription,
        string originalRoleName,
        bool isBackend,
        CancellationToken cancellationToken)
    {
        var contextMd = await BuildGapAnalysisContextAsync(boardId, board, studentId, sprintNumber, trelloRoleLabel, originalRoleName, cancellationToken);
        var artifactsMd = await BuildGapAnalysisArtifactsAsync(boardId, board, studentId, sprintNumber, trelloRoleLabel, originalRoleName, isBackend, cancellationToken);

        var systemTemplate = LoadGapAnalysisSystemPrompt();
        var systemPrompt = systemTemplate.Replace("{{ROLE_DESCRIPTION}}", expertRoleDescription, StringComparison.Ordinal);

        var userPrompt = new StringBuilder()
            .AppendLine("## CONTEXT (requirements / sprint intent)")
            .AppendLine(contextMd)
            .AppendLine()
            .AppendLine("## STUDENT ARTIFACTS (skill work and resource links)")
            .AppendLine(artifactsMd)
            .AppendLine()
            .AppendLine(
                "**Category rule:** Score using evidence from **STUDENT ARTIFACTS** above (code/repo, design assets, CRM rows, PM story text, resources). " +
                "When **CONTEXT** includes **Customer background** and/or **Customer chat history** with substantive content, include a **separate** scored category for **customer alignment** (broad name—distinct from PM user story **Requirements coverage**), per your system instructions. " +
                "Do not add scored categories for checklist-only items with no matching artifact block (e.g. meetings you cannot verify from this data). Mention those in `narrative` if needed.")
            .AppendLine()
            .AppendLine("Respond with JSON only as specified in your instructions.")
            .ToString();

        return new GapAnalysisPrompts(systemPrompt, userPrompt);
    }

    private async Task<GapTrackResult> RunGapAnalysisForTrackAsync(
        string boardId,
        ProjectBoard board,
        int studentId,
        int sprintNumber,
        string trelloRoleLabel,
        string expertRoleDescription,
        string originalRoleName,
        bool isBackend,
        CancellationToken cancellationToken)
    {
        var prompts = await BuildGapAnalysisPromptsForTrackAsync(
            boardId, board, studentId, sprintNumber, trelloRoleLabel, expertRoleDescription, originalRoleName, isBackend, cancellationToken);
        var systemPrompt = prompts.SystemPrompt;
        var userPrompt = prompts.UserPrompt;

        var cheapName = _configuration["OpenAI:CheapModel"] ?? "gpt-4o-mini";
        var aiModel = new AIModel
        {
            Name = cheapName,
            Provider = "OpenAI",
            BaseUrl = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1",
            MaxTokens = 16384,
            DefaultTemperature = 0.2
        };

        var (llmText, _, _) = await _chatCompletionService.GetChatCompletionAsync(aiModel, systemPrompt, userPrompt, null);
        var parsed = TryParseGapAnalysisJson(llmText, out var dto);
        if (!parsed || dto == null)
            return new GapTrackResult(llmText.Trim(), new List<(string, int)>(), new { parseError = true, raw = llmText }, ParsedOk: false);

        var rows = dto.Categories
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .Select(c => (c.Name.Trim(), Math.Clamp(c.Score, 0, 100)))
            .ToList();
        if (rows.Count == 0)
            rows.Add(("Overall", 0));

        return new GapTrackResult(dto.Narrative.Trim(), rows, dto, ParsedOk: true);
    }

    private static string LoadGapAnalysisSystemPrompt()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", "Metrics", "GapAnalysisSystem.txt");
        if (System.IO.File.Exists(path))
        {
            var t = System.IO.File.ReadAllText(path).Trim();
            if (!string.IsNullOrEmpty(t))
                return t;
        }

        return "You are an expert reviewer. Output JSON with categories (name, score 0-100, rationale) and a narrative field.";
    }

    private static bool TryParseGapAnalysisJson(string llmText, out GapAnalysisLlmResult? dto)
    {
        dto = null;
        var json = ExtractJsonObject(llmText);
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            dto = JsonSerializer.Deserialize<GapAnalysisLlmResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return dto != null;
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractJsonObject(string text)
    {
        var i = text.IndexOf('{');
        var j = text.LastIndexOf('}');
        if (i < 0 || j <= i)
            return null;
        return text[i..(j + 1)];
    }

    private async Task AppendGapAnalysisProjectModuleSectionFromModuleIdAsync(
        StringBuilder sb,
        ProjectBoard board,
        int moduleId,
        string sectionHeading,
        CancellationToken cancellationToken)
    {
        var pm = await strAppersBackend.Utilities.ProjectModuleLookup.FindByBoardScopeAsync(
            _context,
            moduleId,
            board.ProjectId,
            cancellationToken);
        if (pm == null)
            return;
        sb.AppendLine(sectionHeading);
        sb.AppendLine($"- Module Id: {pm.Id}, Title: {pm.Title ?? "(none)"}");
        sb.AppendLine("- Description:");
        sb.AppendLine(string.IsNullOrWhiteSpace(pm.Description) ? "(none)" : pm.Description!.Trim());
        sb.AppendLine();
    }

    private async Task<string> BuildGapAnalysisContextAsync(
        string boardId,
        ProjectBoard board,
        int studentId,
        int sprintNumber,
        string trelloRoleLabel,
        string originalRoleName,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        var snap = await _trelloService.GetSprintRoleCardSnapshotAsync(boardId, sprintNumber, trelloRoleLabel);
        if (snap != null)
        {
            sb.AppendLine("### Sprint role card (Trello)");
            sb.AppendLine($"- Card: {snap.CardName}");
            sb.AppendLine("- Description:");
            sb.AppendLine(string.IsNullOrWhiteSpace(snap.Description) ? "(none)" : snap.Description.Trim());
            if (!string.IsNullOrWhiteSpace(snap.ChecklistsText))
            {
                sb.AppendLine("- Tasks / checklists:");
                sb.AppendLine(snap.ChecklistsText);
            }
            sb.AppendLine();
        }

        if (ContainsProduct(originalRoleName))
        {
            string? pmModuleId = null;
            foreach (var lbl in GapAnalysisPmSprintCardLabels)
            {
                pmModuleId = await _trelloService.GetModuleIdFromSprintCardAsync(boardId, sprintNumber, lbl);
                if (!string.IsNullOrWhiteSpace(pmModuleId))
                    break;
            }

            if (!string.IsNullOrWhiteSpace(pmModuleId) && int.TryParse(pmModuleId.Trim(), out var pmModInt))
            {
                await AppendGapAnalysisProjectModuleSectionFromModuleIdAsync(
                    sb, board, pmModInt, "### Project module (from Trello ModuleId on the Product Manager sprint card)", cancellationToken);
            }
            // Do not include the user story in context for Product/PM — it is the skill artifact, not background.
        }
        else
        {
            var roleModuleId = await _trelloService.GetModuleIdFromSprintCardAsync(boardId, sprintNumber, trelloRoleLabel);
            string? pmModuleId = null;
            foreach (var lbl in GapAnalysisPmSprintCardLabels)
            {
                pmModuleId = await _trelloService.GetModuleIdFromSprintCardAsync(boardId, sprintNumber, lbl);
                if (!string.IsNullOrWhiteSpace(pmModuleId))
                    break;
            }

            if (!string.IsNullOrWhiteSpace(roleModuleId) && int.TryParse(roleModuleId.Trim(), out var roleModInt))
            {
                await AppendGapAnalysisProjectModuleSectionFromModuleIdAsync(
                    sb, board, roleModInt, "### Project module (from Trello ModuleId on your sprint card)", cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(pmModuleId) && int.TryParse(pmModuleId.Trim(), out var pmOnlyModInt))
            {
                await AppendGapAnalysisProjectModuleSectionFromModuleIdAsync(
                    sb, board, pmOnlyModInt, "### Project module (from Product Manager sprint card ModuleId)", cancellationToken);
            }

            // Match user-story cards by the same ModuleId as this role's sprint card (e.g. 889 for developers).
            // The PM sprint card can still carry a different ModuleId; using it here pulled the wrong story.
            var moduleIdForUserStory = !string.IsNullOrWhiteSpace(roleModuleId)
                ? roleModuleId.Trim()
                : pmModuleId?.Trim();
            if (!string.IsNullOrWhiteSpace(moduleIdForUserStory))
            {
                if (!string.IsNullOrWhiteSpace(pmModuleId) && !string.IsNullOrWhiteSpace(roleModuleId) &&
                    !string.Equals(roleModuleId.Trim(), pmModuleId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation(
                        "GapAnalysis: user story lookup uses role sprint ModuleId {RoleModule}, not PM sprint ModuleId {PmModule}",
                        roleModuleId!.Trim(), pmModuleId!.Trim());
                }

                var usResult = await _trelloService.GetUserStoryCardByModuleIdAsync(boardId, moduleIdForUserStory);
                var card = GetUserStoryCardFromResult(usResult);
                var storyText = ConcatenateUserStoryText(card);
                if (CountWords(storyText) > 10)
                {
                    sb.AppendLine("### PM user story (requirements reference — compare your work to this)");
                    sb.AppendLine(storyText.Trim());
                    sb.AppendLine();
                }
            }
        }

        if (!ContainsDeveloper(originalRoleName))
        {
            var project = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == board.ProjectId, cancellationToken);
            if (project != null && !string.IsNullOrWhiteSpace(project.CustomerPastStory))
            {
                sb.AppendLine("### Customer background (project)");
                sb.AppendLine(project.CustomerPastStory.Trim());
                sb.AppendLine();
            }

            await AppendGapAnalysisCustomerChatHistoryAsync(sb, studentId, sprintNumber, cancellationToken);
        }

        return sb.Length == 0 ? "(No context blocks could be assembled for this sprint/role.)" : sb.ToString();
    }

    /// <summary>
    /// Last N raw chat rows (user + assistant) included in gap analysis prompts. Uses long math so
    /// <c>pairLimit * 2</c> cannot overflow to a negative <see cref="Queryable.Take"/> (which yields zero rows while <see cref="Queryable.Any"/> on the full set can still be true).
    /// </summary>
    private int GetCustomerGapAnalysisChatMaxRowCount()
    {
        var pairLimit = _promptConfig.Customer.ChatHistoryLength;
        if (pairLimit < 1)
            pairLimit = 5;
        var doubled = (long)pairLimit * 2L;
        if (doubled < 1)
            return 10;
        if (doubled > int.MaxValue)
            return int.MaxValue;
        return (int)doubled;
    }

    /// <summary>
    /// Same row window as <see cref="AppendGapAnalysisCustomerChatHistoryAsync"/> (must match for "has chat" vs. prompt text).
    /// </summary>
    private async Task<bool> HasCustomerGapAnalysisChatRowsAsync(
        int studentId,
        int sprintNumber,
        CancellationToken cancellationToken)
    {
        var maxRows = GetCustomerGapAnalysisChatMaxRowCount();
        return await _context.CustomerChatHistory.AsNoTracking()
            .Where(h => h.StudentId == studentId && h.SprintId == sprintNumber)
            .OrderByDescending(h => h.CreatedAt)
            .Take(maxRows)
            .AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Customer ↔ AI Customer chat for this student / sprint (same rows as mentor resource review uses).
    /// Omitted for Developer roles (see <see cref="BuildGapAnalysisContextAsync"/> gate).
    /// </summary>
    private async Task AppendGapAnalysisCustomerChatHistoryAsync(
        StringBuilder sb,
        int studentId,
        int sprintNumber,
        CancellationToken cancellationToken)
    {
        var maxRows = GetCustomerGapAnalysisChatMaxRowCount();

        var chatRows = await _context.CustomerChatHistory.AsNoTracking()
            .Where(h => h.StudentId == studentId && h.SprintId == sprintNumber)
            .OrderByDescending(h => h.CreatedAt)
            .Take(maxRows)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

        sb.AppendLine("### Customer chat history (AI Customer; filtered by StudentId, SprintNumber)");
        if (chatRows.Count == 0)
        {
            sb.AppendLine("(No messages in `CustomerChatHistory` for this student and sprint.)");
            sb.AppendLine();
            return;
        }

        foreach (var row in chatRows)
        {
            var role = row.Role?.Trim().ToLowerInvariant() == "assistant" ? "Assistant" : "User";
            var msg = row.Message?.Trim() ?? "";
            if (msg.Length > 4000)
                msg = msg[..4000] + "…";
            sb.AppendLine($"- **[{role}]** ({row.CreatedAt:u}): {msg}");
        }

        sb.AppendLine();
    }

    private async Task<string> BuildGapAnalysisArtifactsAsync(
        string boardId,
        ProjectBoard board,
        int studentId,
        int sprintNumber,
        string trelloRoleLabel,
        string originalRoleName,
        bool isBackend,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var ghToken = _configuration["GitHub:AccessToken"];

        if (ContainsDesigner(originalRoleName))
            await AppendDesignerSkillArtifactsAsync(sb, boardId, studentId, sprintNumber, cancellationToken);
        else if (ContainsProduct(originalRoleName))
            await AppendProductManagerSkillArtifactAsync(sb, boardId, board.ProjectId, sprintNumber, cancellationToken);
        else if (IsMarketingOrBizDev(originalRoleName))
            sb.AppendLine(await BuildStakeholdersArtifactSectionAsync(boardId, sprintNumber, cancellationToken));
        else if (ContainsDeveloper(originalRoleName))
        {
            if (string.IsNullOrEmpty(ghToken))
            {
                sb.AppendLine("### Skill — GitHub");
                sb.AppendLine("(GitHub token not configured — cannot load diffs or pull requests.)");
            }
            else
                await AppendGitHubDeveloperArtifactsAsync(sb, boardId, board, sprintNumber, trelloRoleLabel, isBackend, ghToken, cancellationToken);
        }

        sb.AppendLine("### Resource links (non-Figma; sprint-scoped when sprint number ≥ 1)");
        List<Resource> resources;
        if (sprintNumber > 0)
        {
            resources = await _context.Resources.AsNoTracking()
                .Where(r => r.BoardId == boardId && r.StudentId == studentId && !r.IsFigma && r.SprintNumber == sprintNumber)
                .ToListAsync(cancellationToken);
        }
        else
        {
            resources = await _context.Resources.AsNoTracking()
                .Where(r => r.BoardId == boardId && r.StudentId == studentId && !r.IsFigma &&
                            (r.SprintNumber == sprintNumber || r.SprintNumber == null))
                .ToListAsync(cancellationToken);
        }
        foreach (var r in resources)
            sb.AppendLine($"- {r.Name}: {r.Url} (Azure Blob or HTTPS — not sent through the Figma API)");
        if (resources.Count == 0)
            sb.AppendLine("(none)");

        return sb.ToString();
    }

    private async Task AppendProductManagerSkillArtifactAsync(
        StringBuilder sb,
        string boardId,
        int projectId,
        int sprintNumber,
        CancellationToken cancellationToken)
    {
        sb.AppendLine("### Skill — Product Manager user story (primary PM deliverable)");
        string? moduleIdStr = null;
        foreach (var lbl in GapAnalysisPmSprintCardLabels)
        {
            moduleIdStr = await _trelloService.GetModuleIdFromSprintCardAsync(boardId, sprintNumber, lbl);
            if (!string.IsNullOrWhiteSpace(moduleIdStr))
                break;
        }

        if (string.IsNullOrWhiteSpace(moduleIdStr))
        {
            sb.AppendLine("No ModuleId on the Product Manager sprint card — a linked user story is not expected for this sprint.");
            return;
        }

        var usResult = await _trelloService.GetUserStoryCardByModuleIdAsync(boardId, moduleIdStr.Trim());
        var card = GetUserStoryCardFromResult(usResult);
        if (card == null)
        {
            sb.AppendLine($"ModuleId **{moduleIdStr.Trim()}** is set, but no matching card was found in the User Stories list.");
            return;
        }

        sb.AppendLine(ConcatenateUserStoryText(card).Trim());
    }

    private async Task AppendDesignerSkillArtifactsAsync(
        StringBuilder sb,
        string boardId,
        int studentId,
        int sprintNumber,
        CancellationToken cancellationToken)
    {
        var figmaRows = await _context.Resources.AsNoTracking()
            .Where(r => r.BoardId == boardId && r.StudentId == studentId && r.IsFigma &&
                        (r.SprintNumber == sprintNumber || r.SprintNumber == null))
            .ToListAsync(cancellationToken);

        var depth = _configuration.GetValue<int?>("FigmaMetadataLlm:DefaultDepth", 8);

        sb.AppendLine("### Skill — Figma (IsFigma = true → file metadata via platform Figma API)");
        if (figmaRows.Count == 0)
            sb.AppendLine("(No Figma URLs in Resources for this scope.)");

        foreach (var r in figmaRows)
        {
            sb.AppendLine($"#### {r.Name}");
            sb.AppendLine($"- URL: {r.Url}");
            var (ok, semantic, err) = await TryFetchFigmaSemanticJsonAsync(boardId, r.Url.Trim(), depth, cancellationToken);
            if (ok && !string.IsNullOrEmpty(semantic))
            {
                sb.AppendLine("- Semantic file tree (for gap analysis):");
                sb.AppendLine(semantic);
            }
            else
            {
                sb.AppendLine($"- Figma API / download-metadata failed: {err ?? "unknown"}");
                sb.AppendLine("- Fallback: use image resource URLs below (non-Figma) if present.");
            }
            sb.AppendLine();
        }

        var imgRows = await _context.Resources.AsNoTracking()
            .Where(r => r.BoardId == boardId && r.StudentId == studentId && !r.IsFigma &&
                        (r.SprintNumber == sprintNumber || sprintNumber <= 0 && r.SprintNumber == null))
            .ToListAsync(cancellationToken);
        var images = imgRows.Where(r => LooksLikeImageUrl(r.Url)).ToList();
        sb.AppendLine("### Skill — Image uploads (IsFigma = false; use when Figma API fails or for supplementary visuals)");
        foreach (var r in images)
            sb.AppendLine($"- {r.Name}: {r.Url}");
        if (images.Count == 0)
            sb.AppendLine("(none)");
    }

    private async Task<(bool Ok, string? SemanticJson, string? Error)> TryFetchFigmaSemanticJsonAsync(
        string boardId,
        string figmaFileUrl,
        int? depth,
        CancellationToken cancellationToken)
    {
        var baseUrl = (_configuration["ApiBaseUrl"] ?? "").TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
            return (false, null, "ApiBaseUrl is not configured; cannot call POST /api/Figma/use/download-metadata.");

        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["boardId"] = boardId,
                ["figmaFileUrl"] = figmaFileUrl,
                ["depth"] = depth
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(10);
            using var resp = await client.PostAsync(
                $"{baseUrl}/api/Figma/use/download-metadata",
                new StringContent(json, Encoding.UTF8, "application/json"),
                cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
                return (false, null, $"HTTP {(int)resp.StatusCode}: {Truncate(body, 800)}");

            var pruned = FigmaMetadataPruner.PruneHeavyKeys(body);
            var semantic = FigmaSemanticJsonTransformer.TryTransform(pruned);
            var final = string.IsNullOrEmpty(semantic) ? pruned : semantic;
            var max = _configuration.GetValue("GapAnalysis:MaxFigmaSemanticChars", 80_000);
            if (final.Length > max)
                final = final[..max] + "\n…(truncated for gap analysis prompt)";
            return (true, final, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    private static bool LooksLikeImageUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var u = url.Trim().ToLowerInvariant();
        return u.Contains(".png", StringComparison.Ordinal) || u.Contains(".jpg", StringComparison.Ordinal) ||
               u.Contains(".jpeg", StringComparison.Ordinal) || u.Contains(".webp", StringComparison.Ordinal) || u.Contains(".gif", StringComparison.Ordinal);
    }

    /// <summary>
    /// Raw <c>BranchContext</c> from Trello (e.g. <c>Bugs</c>). If it already ends with <c>-B</c> or <c>-F</c>, use as the full branch name; otherwise append <c>-B</c>/<c>-F</c> for this repository.
    /// </summary>
    private static string ResolveGapAnalysisHeadFromBranchContext(string branchContext, bool isBackend)
    {
        if (branchContext.EndsWith("-B", StringComparison.OrdinalIgnoreCase) ||
            branchContext.EndsWith("-F", StringComparison.OrdinalIgnoreCase))
            return branchContext;
        return $"{branchContext}-{(isBackend ? "B" : "F")}";
    }

    /// <summary>
    /// Compares the sprint branch to the integration branch (usually <c>main</c>). Set <c>GitHub:GapAnalysisCompareBaseBranch</c> or legacy <c>GitHub:GapAnalysisBaseBranch</c>.
    /// When the sprint role card has a <c>BranchContext</c> custom field (e.g. <c>Bugs</c>), that overrides the default <c>{sprint}-B/F</c> or Bugs branch naming for this track.
    /// </summary>
    private async Task AppendGitHubDeveloperArtifactsAsync(
        StringBuilder sb,
        string boardId,
        ProjectBoard board,
        int sprintNumber,
        string trelloRoleLabel,
        bool isBackend,
        string token,
        CancellationToken cancellationToken)
    {
        var repoUrl = isBackend ? board.GithubBackendUrl : board.GithubFrontendUrl;
        sb.AppendLine($"### Skill — GitHub ({(isBackend ? "Backend" : "Frontend")} repository)");
        if (string.IsNullOrWhiteSpace(repoUrl) || !TryParseOwnerRepo(repoUrl, out var owner, out var repo))
        {
            sb.AppendLine("(Missing or invalid GitHub repository URL on the project board.)");
            return;
        }

        var defaultHead = sprintNumber == 0
            ? (isBackend ? "Bugs-B" : "Bugs-F")
            : $"{sprintNumber}-{(isBackend ? "B" : "F")}";

        string head;
        var branchContext = await _trelloService.GetSprintCardBranchContextAsync(boardId, sprintNumber, trelloRoleLabel);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(branchContext))
        {
            head = ResolveGapAnalysisHeadFromBranchContext(branchContext.Trim(), isBackend);
            sb.AppendLine(
                $"Branch for diff/PR: **{head}** (from Trello **BranchContext** = `{branchContext.Trim()}`; default without this field would be **{defaultHead}**).");
        }
        else
            head = defaultHead;

        var configuredBase = _configuration["GitHub:GapAnalysisCompareBaseBranch"]
            ?? _configuration["GitHub:GapAnalysisBaseBranch"]
            ?? "main";
        var bases = new[] { configuredBase, "main", "master", "develop" };

        var (diff, usedBase, pr) = await FetchGitHubGapAnalysisEvidenceAsync(owner, repo, head, bases, token);
        AppendGitHubCompareArtifactLines(sb, diff, usedBase, head, configuredBase, owner, repo);

        AppendGitHubPrArtifactLine(sb, pr, owner, repo, head, logIfMissing: true);

        // Trello BranchContext often says Bugs-F while Git uses sprint-numbered branches (e.g. 8-F). Retry default sprint branch when names differ and primary missed PR or compare.
        if (!string.Equals(head, defaultHead, StringComparison.Ordinal) &&
            (pr == null || diff == null))
        {
            var (diffAlt, usedAlt, prAlt) = await FetchGitHubGapAnalysisEvidenceAsync(owner, repo, defaultHead, bases, token);
            if (prAlt != null || diffAlt != null)
            {
                sb.AppendLine();
                sb.AppendLine(
                    $"**Sprint default branch `{defaultHead}`:** Trello **BranchContext** targets **`{head}`**, but work is often pushed to the numbered sprint branch **`{defaultHead}`** (e.g. mentor tooling). Additional GitHub evidence for **`{defaultHead}`**:");
                AppendGitHubCompareArtifactLines(sb, diffAlt, usedAlt, defaultHead, configuredBase, owner, repo);
                AppendGitHubPrArtifactLine(sb, prAlt, owner, repo, defaultHead, logIfMissing: false);
                _logger.LogInformation(
                    "GapAnalysis: appended fallback branch evidence {DefaultHead} (primary was {Head}, pr={HasPr}, diff={HasDiff})",
                    defaultHead, head, pr != null, diff != null);
            }
        }

        sb.AppendLine(
            "**Scoring rule for this track:** Use **0%** implementation completeness only when the GitHub evidence shows **no commits** on the relevant branch relative to the integration branch (nothing landed). If there **are** commits (see compare line) but **no** merged PR yet, **do not** use 0% — reduce the score and explain the missing review/merge. If **two** branch sections appear above, score from the one that shows commits/PR matching the student’s delivery.");
    }

    private async Task<(GitHubCommitDiff? Diff, string? UsedBase, GitHubPullRequest? Pr)> FetchGitHubGapAnalysisEvidenceAsync(
        string owner, string repo, string headBranch, string[] bases, string token)
    {
        GitHubCommitDiff? diff = null;
        string? usedBase = null;
        foreach (var b in bases.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var attempt = await _githubService.GetCompareDiffAsync(owner, repo, b, headBranch, token);
            if (attempt != null)
            {
                diff = attempt;
                usedBase = b;
                break;
            }
        }

        var pr = await _githubService.GetPullRequestForGapAnalysisAsync(owner, repo, headBranch, token);
        return (diff, usedBase, pr);
    }

    private void AppendGitHubCompareArtifactLines(
        StringBuilder sb,
        GitHubCommitDiff? diff,
        string? usedBase,
        string headLabel,
        string configuredBase,
        string owner,
        string repo)
    {
        if (diff != null && usedBase != null)
        {
            sb.AppendLine(
                $"GitHub compare **{usedBase}**...**{headLabel}**: status={diff.CompareStatus}, commits in range={diff.CommitsCount}, ahead_by={diff.AheadBy}, behind_by={diff.BehindBy}, files changed={diff.TotalFilesChanged}, +{diff.TotalAdditions}/-{diff.TotalDeletions}.");
            if (diff.TotalFilesChanged > 0)
            {
                var patch = string.Join("\n", diff.FileChanges.Take(25).Select(f => f.Patch ?? ""));
                sb.AppendLine(Truncate(patch, 12000));
            }
            else if (diff.BehindBy > 0 && diff.AheadBy == 0)
            {
                sb.AppendLine(
                    $"**{headLabel}** is **behind** **{usedBase}** by {diff.BehindBy} commit(s) and has **no commits ahead** of **{usedBase}** in this compare (status={diff.CompareStatus}). " +
                    "That usually means the branch tip is stale relative to main (or work was merged and the branch was reset), not that this prompt failed. " +
                    "Use the PR line below; do **not** treat this as “identical to main” with merged work unless the PR says merged.");
            }
            else if (string.Equals(diff.CompareStatus, "identical", StringComparison.OrdinalIgnoreCase)
                     || (diff.AheadBy == 0 && diff.BehindBy == 0 && diff.CommitsCount == 0))
            {
                sb.AppendLine(
                    $"No diff vs **{usedBase}** (same tip or no commits in range). If you merged **{headLabel}** into **{usedBase}**, an empty compare is expected; score using PR/merge evidence below.");
            }
            else
            {
                sb.AppendLine(
                    $"No file-level diff in this compare (status={diff.CompareStatus}, ahead_by={diff.AheadBy}, behind_by={diff.BehindBy}, commits in range={diff.CommitsCount}). See PR line below.");
            }
        }
        else
        {
            sb.AppendLine(
                $"Compare API did not return data for **{headLabel}** vs **{configuredBase}** / fallback bases (wrong repo URL, missing branch, auth, or network). Treat Git evidence as incomplete.");
            _logger.LogWarning(
                "GapAnalysis: GetCompareDiffAsync returned null for all bases for repo {Owner}/{Repo} head {Head}",
                owner, repo, headLabel);
        }
    }

    private void AppendGitHubPrArtifactLine(StringBuilder sb, GitHubPullRequest? pr, string owner, string repo, string headLabel, bool logIfMissing)
    {
        if (pr == null)
        {
            sb.AppendLine(
                $"**Pull request lookup:** No PR found for head `{owner}:{headLabel}` (searched open and merged via GitHub API). If a PR was merged, check branch name spelling, repo owner in `head`, or token access.");
            if (logIfMissing)
            {
                _logger.LogWarning(
                    "GapAnalysis: no PR (open or merged) for {Owner}/{Repo} head branch {Head}",
                    owner, repo, headLabel);
            }
        }
        else
        {
            var mergedNote = pr.Merged ? "merged=yes" : "merged=no";
            sb.AppendLine(
                $"Pull request #{pr.Number}: state={pr.State}, {mergedNote}, title={pr.Title}");
        }
    }

    /// <summary>Gap analysis for Marketing/BizDev does not use sprint 0; callers should not rely on this path for sprint 0.</summary>
    private async Task<string> BuildStakeholdersArtifactSectionAsync(string boardId, int sprintNumber, CancellationToken cancellationToken)
    {
        if (sprintNumber == 0)
        {
            return "### Skill — Stakeholders / CRM\n(Sprint 0 gap analysis is not used for this role.)";
        }

        var board = await _context.ProjectBoards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boardId, cancellationToken);
        if (board == null)
            return "### Skill — Stakeholders\n(board not found)";

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

        var q = _context.Stakeholders.AsNoTracking().Where(s => s.BoardId == boardId);
        if (haveWindow)
        {
            q = q.Where(s =>
                (s.CreatedAt != null && s.CreatedAt >= windowStartUtc && s.CreatedAt <= windowEndInclusiveUtc) ||
                (s.UpdatedAt != null && s.UpdatedAt >= windowStartUtc && s.UpdatedAt <= windowEndInclusiveUtc));
        }

        var rows = await q.Take(80).ToListAsync(cancellationToken);
        var sb2 = new StringBuilder();
        sb2.AppendLine("### Skill — Stakeholders / CRM (rows created or updated in this sprint window)");
        foreach (var s in rows)
            sb2.AppendLine($"- {s.Name}: {Truncate(s.Delta ?? "", 500)}");
        if (rows.Count == 0)
            sb2.AppendLine("(none in window)");
        return sb2.ToString();
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s[..max] + "\n…(truncated)";
    }
}
