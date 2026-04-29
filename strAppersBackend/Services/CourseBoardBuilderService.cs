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
    private const int LeadershipTypeId = 4;

    private readonly ApplicationDbContext _context;
    private readonly IAIService _aiService;
    private readonly ITrelloService _trelloService;
    private readonly ILogger<CourseBoardBuilderService> _logger;
    private readonly IWebHostEnvironment _env;

    public CourseBoardBuilderService(
        ApplicationDbContext context,
        IAIService aiService,
        ITrelloService trelloService,
        ILogger<CourseBoardBuilderService> logger,
        IWebHostEnvironment env)
    {
        _context = context;
        _aiService = aiService;
        _trelloService = trelloService;
        _logger = logger;
        _env = env;
    }

    public async Task<CourseBoardBuildResponse> BuildAsync(CourseBoardBuildRequest request)
    {
        // ── 1. Load project & template ────────────────────────────────────────
        var project = await _context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId);
        if (project == null)
            return Fail($"Project {request.ProjectId} was not found.");

        var instituteTemplate = await _context.InstituteTemplates
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.ProjectId == request.ProjectId);
        if (instituteTemplate == null)
            return Fail($"InstituteTemplate {request.TemplateId} was not found for project {request.ProjectId}.");

        // ── 2. Load all requested roles ───────────────────────────────────────
        List<InstituteRole> roles;
        if (instituteTemplate.SquadId is > 0)
        {
            // Squad mode: InstituteRoleId is ignored — use InstituteRoleIds to filter, or load all active roles
            var filterIds = request.InstituteRoleIds is { Count: > 0 } ? request.InstituteRoleIds : null;

            var squadQuery = _context.InstituteSquadRoles
                .AsNoTracking()
                .Include(r => r.Skill)
                .Include(r => r.BaseInstituteRole)
                .Where(r => r.SquadId == instituteTemplate.SquadId.Value && r.IsActive);

            if (filterIds != null)
                squadQuery = squadQuery.Where(r => filterIds.Contains(r.Id));

            var squadRoles = await squadQuery.ToListAsync();

            if (squadRoles.Count == 0)
                return Fail($"No active roles found in squad {instituteTemplate.SquadId}.");

            if (filterIds != null)
            {
                var missingIds = filterIds.Except(squadRoles.Select(r => r.Id)).ToList();
                if (missingIds.Count > 0)
                    return Fail($"Squad role(s) {string.Join(", ", missingIds)} were not found in squad {instituteTemplate.SquadId}.");
            }

            roles = squadRoles.Select(sr => new InstituteRole
            {
                Id = sr.Id,
                InstituteId = instituteTemplate.InstituteId,
                Name = sr.Name,
                Description = sr.Description,
                // Fallback to base institute role competencies when squad role has none
                Competencies = !string.IsNullOrWhiteSpace(sr.Competencies)
                    ? sr.Competencies
                    : sr.BaseInstituteRole?.Competencies,
                Category = sr.Category,
                Type = sr.Type,
                SkillId = sr.SkillId,
                Skill = sr.Skill,
                CustomerEngagement = sr.CustomerEngagement,
                IsTechnical = sr.IsTechnical,
                IsActive = sr.IsActive,
                CreatedAt = sr.CreatedAt,
                UpdatedAt = sr.UpdatedAt,
            }).ToList();
        }
        else
        {
            // Legacy mode (no squad): role IDs must be explicitly provided
            var effectiveRoleIds = request.EffectiveRoleIds;
            if (effectiveRoleIds.Count == 0)
                return Fail("This template has no squad linked. Provide InstituteRoleId or InstituteRoleIds.");

            roles = await _context.InstituteRoles
                .AsNoTracking()
                .Include(r => r.Skill)
                .Where(r => effectiveRoleIds.Contains(r.Id) && r.TemplateId == request.TemplateId)
                .ToListAsync();

            var missingIds = effectiveRoleIds.Except(roles.Select(r => r.Id)).ToList();
            if (missingIds.Count > 0)
                return Fail($"InstituteRole(s) {string.Join(", ", missingIds)} were not found for template {request.TemplateId}.");
        }

        if (project.InstituteId.HasValue && roles.Any(r => r.InstituteId != project.InstituteId.Value))
            return Fail("One or more roles do not belong to the project's institute.");

        // ── 3. Load modules & system prompt ───────────────────────────────────
        var modules = await _context.ProjectModules
            .AsNoTracking()
            .Where(m => m.ProjectId == request.ProjectId)
            .OrderBy(m => m.Sequence ?? int.MaxValue)
            .ThenBy(m => m.Id)
            .Take(ModuleCount)
            .ToListAsync();

        var systemPrompt = await LoadSystemPromptAsync();
        if (string.IsNullOrWhiteSpace(systemPrompt))
            return Fail("Course builder system prompt could not be loaded.");

        // ── 4. Generate cards for all roles in parallel ───────────────────────
        _logger.LogInformation("Generating course for {RoleCount} role(s) on project {ProjectId}", roles.Count, project.Id);

        var generationTasks = roles.Select(role =>
            GenerateRoleCardsAsync(project, role, modules, systemPrompt, roles));

        var generationResults = await Task.WhenAll(generationTasks);

        // ── 5. Assemble the board ─────────────────────────────────────────────
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

        // Add sprint cards — grouped by sprint number so all Sprint-1 cards come first, then Sprint-2, etc.
        foreach (var sprintNumber in Enumerable.Range(1, SprintCount))
        {
            foreach (var result in generationResults)
            {
                var card = result.Cards.FirstOrDefault(c => c.ListName == $"Sprint {sprintNumber}");
                if (card != null)
                    board.SprintPlan.Cards.Add(card);
            }
        }

        // User Story cards — one per module, added once regardless of role count
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

        string? createdBoardUrl = null;
        if (request.GenerateTrelloBoard)
        {
            _logger.LogInformation(
                "Creating Trello board for course template {TemplateId} (ProjectId={ProjectId})",
                request.TemplateId,
                request.ProjectId);
            var trelloResult = await _trelloService.CreateProjectWithSprintsAsync(board, project.Title);
            if (!trelloResult.Success || string.IsNullOrWhiteSpace(trelloResult.BoardUrl))
            {
                var errors = trelloResult.Errors is { Count: > 0 }
                    ? string.Join("; ", trelloResult.Errors)
                    : "Unknown Trello creation error.";
                return Fail($"Course template generated, but Trello board creation failed: {errors}");
            }

            createdBoardUrl = trelloResult.BoardUrl;
        }

        // ── 6. Persist ────────────────────────────────────────────────────────
        instituteTemplate.TrelloBoardJson = JsonSerializer.Serialize(board, new JsonSerializerOptions { WriteIndented = true });
        instituteTemplate.ProjectId = request.ProjectId;
        instituteTemplate.InstituteId = roles.First().InstituteId;
        if (request.GenerateTrelloBoard)
            instituteTemplate.BoardUrl = createdBoardUrl;
        await _context.SaveChangesAsync();

        var roleNames = string.Join(", ", roles.Select(r => r.Name));
        var totalPromptTokens = generationResults.Sum(r => r.PromptTokens);
        var totalCompletionTokens = generationResults.Sum(r => r.CompletionTokens);

        return new CourseBoardBuildResponse
        {
            Success = true,
            Message = $"Course board generated for [{roleNames}] on project '{project.Title}'. InstituteTemplate.Id={instituteTemplate.Id}.",
            BoardTemplate = board,
            BoardUrl = createdBoardUrl,
            TokenUsage = request.IncludeTokenUsage
                ? new CourseBuildTokenUsage
                {
                    PromptTokens = totalPromptTokens,
                    CompletionTokens = totalCompletionTokens
                }
                : null
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Per-role generation
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<RoleGenerationResult> GenerateRoleCardsAsync(
        Project project,
        InstituteRole role,
        List<ProjectModule> modules,
        string systemPrompt,
        IReadOnlyList<InstituteRole> allRoles)
    {
        var sprintModules = ComputeSprintModules(modules, role);
        var otherRoles = allRoles.Where(r => r.Id != role.Id).ToList();
        var userPrompt = BuildUserPrompt(project, role, sprintModules, otherRoles);

        _logger.LogInformation("Generating cards for role {RoleName}", role.Name);
        var aiResult = await _aiService.GenerateCourseAsync(systemPrompt, userPrompt);

        List<AiSprintCard> aiCards;
        if (!aiResult.Success || string.IsNullOrWhiteSpace(aiResult.Content))
        {
            _logger.LogWarning("AI generation failed for role {RoleName}: {Error}", role.Name, aiResult.ErrorMessage);
            aiCards = BuildFallbackCards(role, sprintModules);
        }
        else
        {
            var parsed = ParseAiSprintCards(aiResult.Content);
            if (parsed == null || parsed.Count != SprintCount)
            {
                _logger.LogWarning("AI returned {Count} cards for role {RoleName}; using fallback", parsed?.Count ?? 0, role.Name);
                aiCards = BuildFallbackCards(role, sprintModules);
            }
            else
            {
                aiCards = parsed;
            }
        }

        var trelloCards = new List<TrelloCard>();
        for (var s = 1; s <= SprintCount; s++)
        {
            var aiCard = aiCards.FirstOrDefault(c => c.SprintNumber == s) ?? aiCards.ElementAtOrDefault(s - 1);
            trelloCards.Add(MapToTrelloCard(aiCard, role, s, sprintModules[s - 1]));
        }

        return new RoleGenerationResult
        {
            Cards = trelloCards,
            PromptTokens = aiResult.PromptTokens,
            CompletionTokens = aiResult.CompletionTokens
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sprint→module assignment
    // ─────────────────────────────────────────────────────────────────────────

    private static ProjectModule?[] ComputeSprintModules(List<ProjectModule> modules, InstituteRole role)
    {
        var result = new ProjectModule?[SprintCount];
        if (modules.Count == 0) return result;

        if (role.IsTechnical)
        {
            // Sprints 3-7 → Modules 0-4
            for (var i = 0; i < ModuleCount && i < modules.Count; i++)
                result[2 + i] = modules[i];
        }
        else if (role.Type == LeadershipTypeId)
        {
            // Sprints 2-6 → Modules 0-4
            for (var i = 0; i < ModuleCount && i < modules.Count; i++)
                result[1 + i] = modules[i];
        }
        else
        {
            // Customer-facing non-technical: Sprints 2-5 → Modules 0-3 (sprint 6 = special)
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

    private static string BuildUserPrompt(
        Project project,
        InstituteRole role,
        ProjectModule?[] sprintModules,
        IReadOnlyList<InstituteRole> otherSquadMembers)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Project");
        sb.AppendLine($"Title: {project.Title}");
        sb.AppendLine($"Description: {project.Description}");
        if (!string.IsNullOrWhiteSpace(project.ShortBrief))
            sb.AppendLine($"ShortBrief: {project.ShortBrief}");
        sb.AppendLine();

        sb.AppendLine("## Role (generate cards for THIS role only)");
        sb.AppendLine($"Name: {role.Name}");
        sb.AppendLine($"IsTechnical: {role.IsTechnical}");
        sb.AppendLine($"IsLeadership: {role.Type == LeadershipTypeId}");
        sb.AppendLine($"CustomerEngagement: {role.CustomerEngagement}");
        sb.AppendLine($"PrimaryTool: {role.Skill?.Name ?? "N/A"} (SkillId={role.SkillId?.ToString() ?? "none"})");
        if (!string.IsNullOrWhiteSpace(role.Competencies))
            sb.AppendLine($"Competencies: {role.Competencies}");
        sb.AppendLine();

        if (otherSquadMembers.Count > 0)
        {
            sb.AppendLine("## Squad (other roles in this course — reference them by name in cross-role tasks)");
            foreach (var m in otherSquadMembers)
            {
                var tags = new List<string>();
                if (m.IsTechnical) tags.Add("Technical");
                if (m.Type == LeadershipTypeId) tags.Add("Leadership");
                if (m.CustomerEngagement) tags.Add("CustomerEngagement");
                sb.AppendLine($"- {m.Name}" + (tags.Count > 0 ? $" ({string.Join(", ", tags)})" : string.Empty));
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Project Modules (ordered by sequence)");
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
                sb.AppendLine($"Sprint {s}: NO MODULE — {GetSpecialSprintLabel(s, role)}");
            else
                sb.AppendLine($"Sprint {s}: Module [Id={module.Id}] \"{module.Title}\"");
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
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed
                    .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("```", string.Empty)
                    .Trim();
            }

            var start = trimmed.IndexOf('[');
            var end = trimmed.LastIndexOf(']');
            if (start < 0 || end <= start) return null;

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
        var roleTag = role.Name.Replace(" ", string.Empty);

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
            CardId = $"{sprintNumber}-{roleTag[..Math.Min(2, roleTag.Length)]}",
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
    // Fallback cards
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
                    _ => module?.Title ?? $"Sprint {s} — Cross-Cutting Work"
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

    private sealed class AiSprintCard
    {
        public int SprintNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> ChecklistItems { get; set; } = new();
    }

    private sealed class RoleGenerationResult
    {
        public List<TrelloCard> Cards { get; set; } = new();
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
    }
}
