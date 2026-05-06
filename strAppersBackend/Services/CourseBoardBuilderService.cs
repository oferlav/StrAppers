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
    private const int DefaultSprintCount = 8;
    private const int LeadershipTypeId = 4;

    private readonly ApplicationDbContext _context;
    private readonly IAIService _aiService;
    private readonly ITrelloService _trelloService;
    private readonly ILogger<CourseBoardBuilderService> _logger;
    private readonly IWebHostEnvironment _env;

    // ─────────────────────────────────────────────────────────────────────────
    // Internal types
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Immutable configuration resolved at the start of each BuildAsync call.
    /// ModuleLengths[i] = number of sprints module i spans.
    /// </summary>
    private sealed record CourseConfig(
        int SprintCount,
        int ModuleCount,
        int SprintLengthInDays,
        int[] ModuleLengths)
    {
        public int TotalModuleSprints => ModuleLengths.Sum();

        // 1-based last sprint index occupied by a module, per track
        public int TechnicalLastModuleSprint => 2 + TotalModuleSprints;
        public int LeadershipLastModuleSprint => 1 + TotalModuleSprints;
        public int CustomerFacingLastModuleSprint =>
            1 + (ModuleCount > 1 ? ModuleLengths.Take(ModuleCount - 1).Sum() : 0);
    }

    /// <summary>Represents a single sprint's module assignment.</summary>
    private sealed record SprintSlot(IProjectModuleRow Module, int Part, int TotalParts);

    private sealed class RoleGenerationResult
    {
        public List<TrelloCard> Cards { get; set; } = new();
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
    }

    private sealed class AiSprintCard
    {
        public int SprintNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> ChecklistItems { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────────────

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
        Project project;
        InstituteTemplate instituteTemplate;

        if (request.InstituteProject)
        {
            var ip = await _context.InstituteProjects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId);
            if (ip == null)
                return Fail($"Institute project {request.ProjectId} was not found.");

            project = new Project
            {
                Id = ip.Id,
                Title = ip.Title,
                Description = ip.Description,
                ShortBrief = ip.ShortBrief,
                InstituteId = ip.InstituteId,
            };

            instituteTemplate = await _context.InstituteTemplates
                .FirstOrDefaultAsync(t =>
                    t.Id == request.TemplateId && t.InstituteProjectId == request.ProjectId);
            if (instituteTemplate == null)
                return Fail(
                    $"InstituteTemplate {request.TemplateId} was not found for institute project {request.ProjectId}.");
        }
        else
        {
            project = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId);
            if (project == null)
                return Fail($"Project {request.ProjectId} was not found.");

            instituteTemplate = await _context.InstituteTemplates
                .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.ProjectId == request.ProjectId);
            if (instituteTemplate == null)
                return Fail($"InstituteTemplate {request.TemplateId} was not found for project {request.ProjectId}.");
        }

        // ── 2. Load roles ─────────────────────────────────────────────────────
        List<InstituteRole> roles;
        if (instituteTemplate.SquadId is > 0)
        {
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

        // ── 3. Load modules & resolve course config ───────────────────────────
        List<IProjectModuleRow> allModules;
        if (request.InstituteProject)
        {
            allModules = (await _context.InstituteProjectModules
                .AsNoTracking()
                .Where(m => m.InstituteProjectId == request.ProjectId)
                .OrderBy(m => m.Sequence ?? int.MaxValue)
                .ThenBy(m => m.Id)
                .ToListAsync()).Cast<IProjectModuleRow>().ToList();
        }
        else
        {
            allModules = (await _context.ProjectModules
                .AsNoTracking()
                .Where(m => m.ProjectId == request.ProjectId)
                .OrderBy(m => m.Sequence ?? int.MaxValue)
                .ThenBy(m => m.Id)
                .ToListAsync()).Cast<IProjectModuleRow>().ToList();
        }

        var moduleCount = request.NumberOfModules.HasValue
            ? Math.Min(request.NumberOfModules.Value, allModules.Count)
            : allModules.Count > 0 ? allModules.Count : 5;

        var sprintCount = request.NumberOfSprints ?? DefaultSprintCount;

        // Resolve module lengths (Tier 2 takes precedence over Tier 1)
        int[] moduleLengths;
        if (request.ModuleLengths is { Count: > 0 })
        {
            if (request.ModuleLengths.Count != moduleCount)
                return Fail($"ModuleLengths has {request.ModuleLengths.Count} entries but module count resolved to {moduleCount}. They must match.");
            if (request.ModuleLengths.Any(l => l < 1))
                return Fail("All ModuleLengths values must be >= 1.");
            moduleLengths = request.ModuleLengths.ToArray();
        }
        else
        {
            moduleLengths = Enumerable.Repeat(request.ModuleLengthInSprints, moduleCount).ToArray();
        }

        var totalModuleSprints = moduleLengths.Sum();
        if (sprintCount < totalModuleSprints + 3)
            return Fail($"NumberOfSprints ({sprintCount}) must be at least sum(module lengths) + 3 = {totalModuleSprints + 3}. " +
                        $"Current module lengths sum to {totalModuleSprints} sprint(s) across {moduleCount} module(s).");

        var config = new CourseConfig(sprintCount, moduleCount, request.SprintLengthInDays, moduleLengths);
        var modules = allModules.Take(moduleCount).ToList();

        // ── 4. Dry run — return sprint plans without AI ───────────────────────
        if (request.DryRun)
        {
            var dryRunPlans = roles.Select(role =>
            {
                var slots = ComputeSprintModules(modules, role, config);
                var track = role.IsTechnical ? "Technical" :
                            role.Type == LeadershipTypeId ? "Leadership" : "Customer-facing";

                return new DryRunRolePlan
                {
                    RoleName = role.Name,
                    Track = track,
                    SprintPlan = Enumerable.Range(1, config.SprintCount).Select(s =>
                    {
                        var slot = slots[s - 1];
                        string label;
                        string? part = null;

                        if (slot != null)
                        {
                            part = slot.TotalParts > 1 ? $"{slot.Part} of {slot.TotalParts}" : null;
                            label = slot.TotalParts > 1
                                ? $"{slot.Module.Title} (Part {slot.Part} of {slot.TotalParts})"
                                : slot.Module.Title ?? $"Module {slot.Module.Id}";
                        }
                        else
                        {
                            label = GetSpecialSprintLabel(s, role, config);
                        }

                        return new DryRunSprintSlot
                        {
                            SprintNumber = s,
                            ModuleId = slot?.Module.Id,
                            ModuleTitle = slot?.Module.Title,
                            Part = part,
                            Label = label
                        };
                    }).ToList()
                };
            }).ToList();

            _logger.LogInformation(
                "Dry run completed for {RoleCount} role(s): {SprintCount} sprints, {ModuleCount} modules, lengths [{Lengths}]",
                roles.Count, config.SprintCount, config.ModuleCount,
                string.Join(",", config.ModuleLengths));

            return new CourseBoardBuildResponse
            {
                Success = true,
                Message = $"Dry run — sprint plans computed for {roles.Count} role(s) " +
                          $"({config.SprintCount} sprints, {config.ModuleCount} modules, " +
                          $"{config.SprintLengthInDays} days/sprint). No AI calls made.",
                DryRunPlans = dryRunPlans
            };
        }

        // ── 5. Load system prompt ─────────────────────────────────────────────
        var systemPrompt = await LoadSystemPromptAsync();
        if (string.IsNullOrWhiteSpace(systemPrompt))
            return Fail("Course builder system prompt could not be loaded.");

        // ── 6. Generate cards for all roles in parallel ───────────────────────
        _logger.LogInformation(
            "Generating course for {RoleCount} role(s) on project {ProjectId} — " +
            "{SprintCount} sprints × {SprintLength}d, {ModuleCount} modules (lengths: [{Lengths}])",
            roles.Count, project.Id, config.SprintCount, config.SprintLengthInDays,
            config.ModuleCount, string.Join(",", config.ModuleLengths));

        var generationTasks = roles.Select(role =>
            GenerateRoleCardsAsync(project, role, modules, systemPrompt, roles, config));

        var generationResults = await Task.WhenAll(generationTasks);

        // ── 7. Assemble the board ─────────────────────────────────────────────
        var board = new TrelloProjectCreationRequest
        {
            ProjectId = project.Id,
            ProjectTitle = project.Title,
            ProjectDescription = project.Description,
            ProjectLengthWeeks = config.SprintCount,
            SprintLengthWeeks = 1,
            DueDate = null,
            TeamMembers = new List<TrelloTeamMember>(),
            StudentEmails = new List<string>(),
            SprintPlan = new TrelloSprintPlan
            {
                Boards = new List<TrelloBoard>(),
                Lists = BuildLists(config.SprintCount),
                Cards = new List<TrelloCard>(),
                TotalSprints = config.SprintCount,
                EstimatedWeeks = config.SprintCount,
                TotalTasks = 0
            }
        };

        foreach (var sprintNumber in Enumerable.Range(1, config.SprintCount))
        {
            foreach (var result in generationResults)
            {
                var card = result.Cards.FirstOrDefault(c => c.ListName == $"Sprint {sprintNumber}");
                if (card != null)
                    board.SprintPlan.Cards.Add(card);
            }
        }

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

        // ── 8. Optionally create Trello board ─────────────────────────────────
        string? createdBoardUrl = null;
        if (request.GenerateTrelloBoard)
        {
            _logger.LogInformation(
                "Creating Trello board for course template {TemplateId} (ProjectId={ProjectId})",
                request.TemplateId, request.ProjectId);

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

        // ── 9. Persist ────────────────────────────────────────────────────────
        instituteTemplate.TrelloBoardJson = JsonSerializer.Serialize(board, new JsonSerializerOptions { WriteIndented = true });
        if (request.InstituteProject)
        {
            instituteTemplate.InstituteProjectId = request.ProjectId;
            instituteTemplate.ProjectId = null;
        }
        else
        {
            instituteTemplate.ProjectId = request.ProjectId;
            instituteTemplate.InstituteProjectId = null;
        }

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
            Message = $"Course board generated for [{roleNames}] on project '{project.Title}'. " +
                      $"InstituteTemplate.Id={instituteTemplate.Id}.",
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
        List<IProjectModuleRow> modules,
        string systemPrompt,
        IReadOnlyList<InstituteRole> allRoles,
        CourseConfig config)
    {
        var sprintSlots = ComputeSprintModules(modules, role, config);
        var otherRoles = allRoles.Where(r => r.Id != role.Id).ToList();
        var userPrompt = BuildUserPrompt(project, role, sprintSlots, otherRoles, config);

        _logger.LogInformation("Generating cards for role {RoleName}", role.Name);
        var aiResult = await _aiService.GenerateCourseAsync(systemPrompt, userPrompt);

        List<AiSprintCard> aiCards;
        if (!aiResult.Success || string.IsNullOrWhiteSpace(aiResult.Content))
        {
            _logger.LogWarning("AI generation failed for role {RoleName}: {Error}", role.Name, aiResult.ErrorMessage);
            aiCards = BuildFallbackCards(role, sprintSlots, config.SprintCount);
        }
        else
        {
            var parsed = ParseAiSprintCards(aiResult.Content, config.SprintCount);
            if (parsed == null)
            {
                _logger.LogWarning("AI returned unexpected card count for role {RoleName}; using fallback", role.Name);
                aiCards = BuildFallbackCards(role, sprintSlots, config.SprintCount);
            }
            else
            {
                aiCards = parsed;
            }
        }

        var trelloCards = new List<TrelloCard>();
        for (var s = 1; s <= config.SprintCount; s++)
        {
            var aiCard = aiCards.FirstOrDefault(c => c.SprintNumber == s) ?? aiCards.ElementAtOrDefault(s - 1);
            trelloCards.Add(MapToTrelloCard(aiCard, role, s, sprintSlots[s - 1]));
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

    private static SprintSlot?[] ComputeSprintModules(List<IProjectModuleRow> modules, InstituteRole role, CourseConfig config)
    {
        var result = new SprintSlot?[config.SprintCount];
        if (modules.Count == 0) return result;

        // Determine starting sprint index (0-based) and how many modules this track covers
        int startIndex;
        int trackModuleCount;

        if (role.IsTechnical)
        {
            startIndex = 2;                       // Sprint 3 (0-based: index 2)
            trackModuleCount = config.ModuleCount;
        }
        else if (role.Type == LeadershipTypeId)
        {
            startIndex = 1;                       // Sprint 2 (0-based: index 1)
            trackModuleCount = config.ModuleCount;
        }
        else
        {
            startIndex = 1;                       // Sprint 2
            trackModuleCount = config.ModuleCount - 1; // Customer-facing: last module reserved for special sprint
        }

        var cursor = startIndex;
        for (var i = 0; i < trackModuleCount && i < modules.Count; i++)
        {
            var totalParts = config.ModuleLengths[i];
            for (var part = 1; part <= totalParts; part++)
            {
                if (cursor < config.SprintCount)
                    result[cursor++] = new SprintSlot(modules[i], part, totalParts);
            }
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
        SprintSlot?[] sprintSlots,
        IReadOnlyList<InstituteRole> otherSquadMembers,
        CourseConfig config)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Course Configuration");
        sb.AppendLine($"Total sprints: {config.SprintCount}");
        sb.AppendLine($"Total modules: {config.ModuleCount}");
        sb.AppendLine($"Module lengths (sprints per module): [{string.Join(", ", config.ModuleLengths)}]");
        sb.AppendLine($"Sprint length: {config.SprintLengthInDays} day(s)");
        sb.AppendLine();

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
            sb.AppendLine("## Squad (other roles — reference them by name in cross-role tasks)");
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
        var distinctModules = sprintSlots
            .Where(s => s != null)
            .Select(s => s!.Module)
            .GroupBy(m => m.Id)
            .Select(g => g.First())
            .ToList();
        for (var i = 0; i < distinctModules.Count; i++)
        {
            var m = distinctModules[i];
            var desc = m.Description?.Length > 300 ? m.Description[..300] + "..." : m.Description ?? string.Empty;
            sb.AppendLine($"{i + 1}. [Id={m.Id}] {m.Title}: {desc}");
        }
        sb.AppendLine();

        sb.AppendLine("## Sprint Plan (authoritative — follow exactly)");
        for (var s = 1; s <= config.SprintCount; s++)
        {
            var slot = sprintSlots[s - 1];
            if (slot == null)
            {
                sb.AppendLine($"Sprint {s}: NO MODULE — {GetSpecialSprintLabel(s, role, config)}");
            }
            else if (slot.TotalParts > 1)
            {
                sb.AppendLine($"Sprint {s}: Module [Id={slot.Module.Id}] \"{slot.Module.Title}\" " +
                              $"(Part {slot.Part} of {slot.TotalParts})");
            }
            else
            {
                sb.AppendLine($"Sprint {s}: Module [Id={slot.Module.Id}] \"{slot.Module.Title}\"");
            }
        }
        sb.AppendLine();
        sb.AppendLine($"Generate exactly {config.SprintCount} sprint cards following all system instructions. Return only the JSON array.");

        return sb.ToString();
    }

    private static string GetSpecialSprintLabel(int sprint, InstituteRole role, CourseConfig config)
    {
        // Sprint 1 — setup for all tracks
        if (sprint == 1)
        {
            if (role.IsTechnical) return "Environment setup, architecture, and technical foundation";
            if (role.Type == LeadershipTypeId) return "Vision, PRD foundation, discovery, user personas";
            return "Ecosystem discovery, tool foundation, market research";
        }

        // Sprint 2 — technical track: architecture readiness
        if (sprint == 2 && role.IsTechnical) return "Data/UI architecture and technical readiness";

        // Last sprint — always stabilization
        if (sprint == config.SprintCount) return "Stabilization, bug fixing, and final QA";

        // Second-to-last sprint — GTM for non-technical roles
        if (sprint == config.SprintCount - 1 && !role.IsTechnical) return "Go-to-Market launch campaign";

        // Track C: special cross-cutting sprint immediately after their last module sprint
        if (!role.IsTechnical && role.Type != LeadershipTypeId)
        {
            var specialSprint = config.CustomerFacingLastModuleSprint + 1;
            if (sprint == specialSprint && sprint != config.SprintCount && sprint != config.SprintCount - 1)
                return "Special cross-cutting work (ROI modeling / Landing Page Design)";
        }

        // Leadership: gap sprints between last module sprint and GTM
        if (role.Type == LeadershipTypeId
            && sprint > config.LeadershipLastModuleSprint
            && sprint < config.SprintCount - 1)
            return "Risk monitoring, backlog grooming, stakeholder alignment";

        // Technical: gap sprints between last module sprint and stabilization
        if (role.IsTechnical
            && sprint > config.TechnicalLastModuleSprint
            && sprint < config.SprintCount)
            return "Technical integration and pre-stabilization hardening";

        return "No module assigned";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AI response parsing
    // ─────────────────────────────────────────────────────────────────────────

    private List<AiSprintCard>? ParseAiSprintCards(string raw, int sprintCount)
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

            return cards?.Count == sprintCount ? cards : null;
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

    private static TrelloCard MapToTrelloCard(AiSprintCard? ai, InstituteRole role, int sprintNumber, SprintSlot? slot)
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
            ModuleId = slot?.Module.Id.ToString() ?? string.Empty,
            CardId = $"{sprintNumber}-{roleTag[..Math.Min(2, roleTag.Length)]}",
            Dependencies = new List<string>(),
            Branched = false,
            ChecklistItems = ai?.ChecklistItems ?? new List<string>(),
            RequiredSkillData = requiredSkill,
            RequiredResourceData = requiredResource
        };
    }

    private static List<TrelloList> BuildLists(int sprintCount)
    {
        var lists = new List<TrelloList>();
        for (var i = 1; i <= sprintCount; i++)
        {
            lists.Add(new TrelloList
            {
                Name = $"Sprint {i}",
                BoardName = string.Empty,
                Position = i - 1,
                StartDate = null,
                EndDate = null,
                Description = i == sprintCount ? "Final stabilization sprint — QA and bug fixing." : null,
                ChecklistItems = new List<string>()
            });
        }
        lists.Add(new TrelloList { Name = "Bugs", BoardName = string.Empty, Position = sprintCount, ChecklistItems = new List<string>() });
        lists.Add(new TrelloList { Name = "User Stories", BoardName = string.Empty, Position = sprintCount + 1, ChecklistItems = new List<string>() });
        return lists;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fallback cards
    // ─────────────────────────────────────────────────────────────────────────

    private static List<AiSprintCard> BuildFallbackCards(InstituteRole role, SprintSlot?[] sprintSlots, int sprintCount)
    {
        var cards = new List<AiSprintCard>();
        for (var s = 1; s <= sprintCount; s++)
        {
            var slot = sprintSlots[s - 1];
            string name;
            if (s == 1) name = "Infrastructure Audit & Technical Setup";
            else if (s == 2) name = "Technical Architecture & Readiness";
            else if (s == sprintCount) name = "Stabilization & Final Polish";
            else if (slot != null)
                name = slot.TotalParts > 1
                    ? $"{slot.Module.Title} — Part {slot.Part} of {slot.TotalParts}"
                    : slot.Module.Title ?? $"Sprint {s}";
            else name = $"Sprint {s} — Cross-Cutting Work";

            cards.Add(new AiSprintCard
            {
                SprintNumber = s,
                Name = name,
                Description = $"Sprint {s} work for {role.Name}.",
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
}
