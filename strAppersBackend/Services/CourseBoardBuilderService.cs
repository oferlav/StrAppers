using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Services;

public interface ICourseBoardBuilderService
{
    Task<CourseBoardBuildResponse> BuildAsync(CourseBoardBuildRequest request);
}

public class CourseBoardBuilderService : ICourseBoardBuilderService
{
    private const int SprintCount = 8;
    private const int ModuleCount = 5;

    // Leadership role type id
    private const int LeadershipTypeId = 4;

    private readonly ApplicationDbContext _context;
    private readonly IAIService _aiService;
    private readonly ILogger<CourseBoardBuilderService> _logger;
    private readonly IWebHostEnvironment _env;

    public CourseBoardBuilderService(
        ApplicationDbContext context,
        IAIService aiService,
        ILogger<CourseBoardBuilderService> logger,
        IWebHostEnvironment env)
    {
        _context = context;
        _aiService = aiService;
        _logger = logger;
        _env = env;
    }

    public async Task<CourseBoardBuildResponse> BuildAsync(CourseBoardBuildRequest request)
    {
        // ── 1. Load entities ──────────────────────────────────────────────────
        var project = await _context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId);
        if (project == null)
            return Fail($"Project {request.ProjectId} was not found.");

        var role = await _context.InstituteRoles
            .AsNoTracking()
            .Include(r => r.Skill)
            .FirstOrDefaultAsync(r => r.Id == request.InstituteRoleId && r.TemplateId == request.TemplateId);
        if (role == null)
            return Fail($"InstituteRole {request.InstituteRoleId} for template {request.TemplateId} was not found.");

        if (project.InstituteId.HasValue && role.InstituteId != project.InstituteId.Value)
            return Fail($"InstituteRole {request.InstituteRoleId} does not belong to institute {project.InstituteId.Value}.");

        var modules = await _context.ProjectModules
            .AsNoTracking()
            .Where(m => m.ProjectId == request.ProjectId)
            .OrderBy(m => m.Sequence ?? int.MaxValue)
            .ThenBy(m => m.Id)
            .Take(ModuleCount)
            .ToListAsync();

        var instituteTemplate = await _context.InstituteTemplates
            .FirstOrDefaultAsync(t =>
                t.Id == request.TemplateId &&
                t.ProjectId == request.ProjectId &&
                t.InstituteId == role.InstituteId);
        if (instituteTemplate == null)
            return Fail($"InstituteTemplate {request.TemplateId} was not found for project {request.ProjectId}.");

        // ── 2. Compute sprint→module assignments ──────────────────────────────
        var sprintModules = ComputeSprintModules(modules, role);

        // ── 3. Load system prompt from file ───────────────────────────────────
        var systemPrompt = await LoadSystemPromptAsync();
        if (string.IsNullOrWhiteSpace(systemPrompt))
            return Fail("Course builder system prompt could not be loaded.");

        // ── 4. Build user prompt ───────────────────────────────────────────────
        var userPrompt = BuildUserPrompt(project, role, sprintModules);

        // ── 5. Single AI call ─────────────────────────────────────────────────
        _logger.LogInformation("Generating course board for role {RoleName}, project {ProjectId}", role.Name, project.Id);
        var aiResult = await _aiService.GenerateCourseAsync(systemPrompt, userPrompt);
        if (!aiResult.Success)
            return Fail($"AI generation failed: {aiResult.ErrorMessage}");

        // ── 6. Parse AI response into sprint cards ────────────────────────────
        var aiCards = ParseAiSprintCards(aiResult.Content ?? string.Empty);
        if (aiCards == null || aiCards.Count != SprintCount)
        {
            _logger.LogWarning("AI returned {Count} cards instead of {Expected}; falling back to defaults", aiCards?.Count ?? 0, SprintCount);
            aiCards = BuildFallbackCards(role, sprintModules);
        }

        // ── 7. Assemble the full board ─────────────────────────────────────────
        var board = new TrelloProjectCreationRequest
        {
            ProjectId = project.Id,
            ProjectTitle = project.Title,
            ProjectDescription = project.Description,
            ProjectLengthWeeks = SprintCount,
            SprintLengthWeeks = 1,
            DueDate = null,
            TeamMembers = new List<TrelloTeamMember>(),
            StudentEmails = new List<string>(),
            SprintPlan = new TrelloSprintPlan
            {
                Boards = new List<TrelloBoard>(),
                Lists = BuildLists(),
                Cards = new List<TrelloCard>(),
                TotalSprints = SprintCount,
                EstimatedWeeks = SprintCount,
                TotalTasks = 0
            }
        };

        for (var s = 1; s <= SprintCount; s++)
        {
            var aiCard = aiCards.FirstOrDefault(c => c.SprintNumber == s)
                         ?? aiCards.ElementAtOrDefault(s - 1);
            var module = sprintModules[s - 1];
            board.SprintPlan.Cards.Add(MapToTrelloCard(aiCard, role, s, module));
        }

        // ── 8. Add User Story cards (one per module, empty) ───────────────────
        foreach (var module in modules)
        {
            board.SprintPlan.Cards.Add(new TrelloCard
            {
                Name = module.Title ?? $"Module {module.Id}",
                Description = "Add user story details and acceptance criteria.",
                ListName = "User Stories",
                AssignedToEmail = string.Empty,
                AssignedToName = string.Empty,
                Labels = new List<string>(),
                DueDate = null,
                Priority = 1,
                EstimatedHours = 2,
                RoleName = string.Empty,
                Status = "To Do",
                Risk = "Low",
                ModuleId = module.Id.ToString(),
                CardId = $"US-{module.Id}",
                Dependencies = new List<string>(),
                Branched = false,
                ChecklistItems = new List<string> { "Add Acceptance Criteria here" },
                RequiredSkillData = false,
                RequiredResourceData = false
            });
        }

        board.SprintPlan.TotalTasks = board.SprintPlan.Cards.Count;

        // ── 9. Persist to InstituteTemplate ───────────────────────────────────
        instituteTemplate.TrelloBoardJson = JsonSerializer.Serialize(board, new JsonSerializerOptions { WriteIndented = true });
        instituteTemplate.ProjectId = request.ProjectId;
        instituteTemplate.InstituteId = role.InstituteId;
        await _context.SaveChangesAsync();

        return new CourseBoardBuildResponse
        {
            Success = true,
            Message = $"Course board generated for role '{role.Name}', project '{project.Title}'. InstituteTemplate.Id={instituteTemplate.Id}.",
            BoardTemplate = board,
            TokenUsage = request.IncludeTokenUsage
                ? new CourseBuildTokenUsage
                {
                    PromptTokens = aiResult.PromptTokens,
                    CompletionTokens = aiResult.CompletionTokens
                }
                : null
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sprint→module assignment
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns an array of 8 entries (index 0 = Sprint 1), each entry is the module assigned to that sprint or null.
    /// </summary>
    private static ProjectModule?[] ComputeSprintModules(List<ProjectModule> modules, InstituteRole role)
    {
        var result = new ProjectModule?[SprintCount];

        if (modules.Count == 0)
            return result;

        if (role.IsTechnical)
        {
            // Sprints 3-7 → Modules 0-4
            for (var i = 0; i < ModuleCount && i < modules.Count; i++)
                result[2 + i] = modules[i]; // sprint index 2 = sprint 3
        }
        else if (role.Type == LeadershipTypeId)
        {
            // Sprints 2-6 → Modules 0-4
            for (var i = 0; i < ModuleCount && i < modules.Count; i++)
                result[1 + i] = modules[i]; // sprint index 1 = sprint 2
        }
        else
        {
            // Customer-facing non-technical: Sprints 2-5 → Modules 0-3 only (sprint 6 = special, no module 5)
            for (var i = 0; i < ModuleCount - 1 && i < modules.Count; i++)
                result[1 + i] = modules[i];
        }

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Prompt building
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<string> LoadSystemPromptAsync()
    {
        var path = Path.Combine(_env.ContentRootPath, "Prompts", "Courses", "CoursBuilder.txt");
        if (!File.Exists(path))
        {
            _logger.LogError("System prompt file not found at {Path}", path);
            return string.Empty;
        }
        return await File.ReadAllTextAsync(path);
    }

    private static string BuildUserPrompt(Project project, InstituteRole role, ProjectModule?[] sprintModules)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Project");
        sb.AppendLine($"Title: {project.Title}");
        sb.AppendLine($"Description: {project.Description}");
        if (!string.IsNullOrWhiteSpace(project.ShortBrief))
            sb.AppendLine($"ShortBrief: {project.ShortBrief}");
        sb.AppendLine();

        sb.AppendLine("## Role");
        sb.AppendLine($"Name: {role.Name}");
        sb.AppendLine($"IsTechnical: {role.IsTechnical}");
        sb.AppendLine($"IsLeadership: {role.Type == LeadershipTypeId}");
        sb.AppendLine($"CustomerEngagement: {role.CustomerEngagement}");
        sb.AppendLine($"PrimaryTool: {role.Skill?.Name ?? "N/A"} (SkillId={role.SkillId?.ToString() ?? "none"})");
        if (!string.IsNullOrWhiteSpace(role.Competencies))
            sb.AppendLine($"Competencies: {role.Competencies}");
        sb.AppendLine();

        sb.AppendLine("## Project Modules (ordered by sequence)");
        // Collect distinct modules in assignment order
        var distinctModules = sprintModules
            .Where(m => m != null)
            .GroupBy(m => m!.Id)
            .Select(g => g.First())
            .ToList();
        for (var i = 0; i < distinctModules.Count; i++)
        {
            var m = distinctModules[i]!;
            var desc = m.Description?.Length > 300 ? m.Description[..300] + "..." : m.Description ?? string.Empty;
            sb.AppendLine($"{i + 1}. [Id={m.Id}] {m.Title}: {desc}");
        }
        sb.AppendLine();

        sb.AppendLine("## Sprint Plan (pre-computed module assignments)");
        for (var s = 1; s <= SprintCount; s++)
        {
            var module = sprintModules[s - 1];
            if (module == null)
            {
                var specialLabel = GetSpecialSprintLabel(s, role);
                sb.AppendLine($"Sprint {s}: NO MODULE — {specialLabel}");
            }
            else
            {
                sb.AppendLine($"Sprint {s}: Module [Id={module.Id}] \"{module.Title}\"");
            }
        }
        sb.AppendLine();
        sb.AppendLine("Generate the 8 sprint cards following all system instructions. Return only the JSON array.");

        return sb.ToString();
    }

    private static string GetSpecialSprintLabel(int sprint, InstituteRole role)
    {
        if (sprint == 1)
        {
            if (role.IsTechnical) return "Environment setup, architecture, and technical foundation";
            if (role.Type == LeadershipTypeId) return "Vision, PRD foundation, discovery, user personas";
            return "Ecosystem discovery, tool foundation, market research";
        }
        if (sprint == 2 && role.IsTechnical) return "Data/UI architecture and technical readiness";
        if (sprint == 6 && !role.IsTechnical && role.Type != LeadershipTypeId) return "Special cross-cutting work (ROI modeling / Landing Page Design)";
        if (sprint == 7 && !role.IsTechnical) return "Go-to-Market launch campaign";
        if (sprint == 8) return "Stabilization, bug fixing, and final QA";
        return "No module assigned";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AI response parsing
    // ─────────────────────────────────────────────────────────────────────────

    private List<AiSprintCard>? ParseAiSprintCards(string raw)
    {
        try
        {
            var trimmed = raw.Trim();
            // Strip markdown code fences if present
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed
                    .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("```", string.Empty)
                    .Trim();
            }

            var start = trimmed.IndexOf('[');
            var end = trimmed.LastIndexOf(']');
            if (start < 0 || end <= start)
                return null;

            var json = trimmed[start..(end + 1)];
            var cards = JsonSerializer.Deserialize<List<AiSprintCard>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return cards is { Count: SprintCount } ? cards : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI sprint cards");
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Card assembly
    // ─────────────────────────────────────────────────────────────────────────

    private static TrelloCard MapToTrelloCard(AiSprintCard? ai, InstituteRole role, int sprintNumber, ProjectModule? module)
    {
        var (requiredSkill, requiredResource) = TrelloRequiredDataFieldRules.ValuesForListName($"Sprint {sprintNumber}");

        return new TrelloCard
        {
            Name = ai?.Name ?? $"Sprint {sprintNumber}",
            Description = ai?.Description ?? $"Sprint {sprintNumber} work plan for {role.Name}.",
            ListName = $"Sprint {sprintNumber}",
            AssignedToEmail = string.Empty,
            AssignedToName = string.Empty,
            Labels = new List<string> { role.Name },
            DueDate = null,
            Priority = 1,
            EstimatedHours = 8,
            RoleName = role.Name,
            Status = "To Do",
            Risk = sprintNumber >= 7 ? "High" : "Medium",
            ModuleId = module?.Id.ToString() ?? string.Empty,
            CardId = $"{sprintNumber}-{role.Name.Replace(" ", string.Empty)[..Math.Min(2, role.Name.Replace(" ", string.Empty).Length)]}",
            Dependencies = new List<string>(),
            Branched = false,
            ChecklistItems = ai?.ChecklistItems ?? new List<string>(),
            RequiredSkillData = requiredSkill,
            RequiredResourceData = requiredResource
        };
    }

    private static List<TrelloList> BuildLists()
    {
        var lists = new List<TrelloList>();
        for (var i = 1; i <= SprintCount; i++)
        {
            lists.Add(new TrelloList
            {
                Name = $"Sprint {i}",
                BoardName = string.Empty,
                Position = i - 1,
                StartDate = null,
                EndDate = null,
                Description = i == SprintCount ? "Final stabilization sprint — QA and bug fixing." : null,
                ChecklistItems = new List<string>()
            });
        }
        lists.Add(new TrelloList { Name = "Bugs", BoardName = string.Empty, Position = SprintCount, ChecklistItems = new List<string>() });
        lists.Add(new TrelloList { Name = "User Stories", BoardName = string.Empty, Position = SprintCount + 1, ChecklistItems = new List<string>() });
        return lists;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fallback cards (used when AI fails to return a valid response)
    // ─────────────────────────────────────────────────────────────────────────

    private static List<AiSprintCard> BuildFallbackCards(InstituteRole role, ProjectModule?[] sprintModules)
    {
        var cards = new List<AiSprintCard>();
        for (var s = 1; s <= SprintCount; s++)
        {
            var module = sprintModules[s - 1];
            cards.Add(new AiSprintCard
            {
                SprintNumber = s,
                Name = s switch
                {
                    1 => "Infrastructure Audit & Technical Setup",
                    2 => "Technical Architecture & Readiness",
                    SprintCount => "Stabilization & Final Polish",
                    _ => module != null ? module.Title ?? $"Sprint {s} Feature" : $"Sprint {s} — Cross-Cutting Work"
                },
                Description = $"Sprint {s} work for {role.Name}. Focus on {(module != null ? module.Title : "cross-cutting deliverables")}.",
                ChecklistItems = new List<string>
                {
                    "[ ] Review sprint goals with the team.",
                    "[ ] Create sprint branch via Mentor Panel.",
                    "[ ] Complete core sprint deliverables.",
                    "[ ] Run peer review with AI Mentor.",
                    "[ ] Submit PR and merge to main."
                }
            });
        }
        return cards;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static CourseBoardBuildResponse Fail(string message) =>
        new() { Success = false, Message = message };

    // ─────────────────────────────────────────────────────────────────────────
    // Internal DTOs for AI response
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class AiSprintCard
    {
        public int SprintNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> ChecklistItems { get; set; } = new();
    }
}
