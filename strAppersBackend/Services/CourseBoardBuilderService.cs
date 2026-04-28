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

    private readonly ApplicationDbContext _context;
    private readonly IAIService _aiService;
    private readonly ILogger<CourseBoardBuilderService> _logger;

    public CourseBoardBuilderService(
        ApplicationDbContext context,
        IAIService aiService,
        ILogger<CourseBoardBuilderService> logger)
    {
        _context = context;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<CourseBoardBuildResponse> BuildAsync(CourseBoardBuildRequest request)
    {
        var project = await _context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId);
        if (project == null)
        {
            return new CourseBoardBuildResponse
            {
                Success = false,
                Message = $"Project {request.ProjectId} was not found."
            };
        }

        var instituteRole = await _context.InstituteRoles
            .AsNoTracking()
            .Include(r => r.Skill)
            .FirstOrDefaultAsync(r => r.Id == request.InstituteRoleId && r.TemplateId == request.TemplateId);
        if (instituteRole == null)
        {
            return new CourseBoardBuildResponse
            {
                Success = false,
                Message = $"InstituteRole {request.InstituteRoleId} for template {request.TemplateId} was not found."
            };
        }
        if (project.InstituteId.HasValue && instituteRole.InstituteId != project.InstituteId.Value)
        {
            return new CourseBoardBuildResponse
            {
                Success = false,
                Message = $"InstituteRole {request.InstituteRoleId} for template {request.TemplateId} does not belong to project institute {project.InstituteId.Value}."
            };
        }

        var modules = await _context.ProjectModules
            .AsNoTracking()
            .Where(m => m.ProjectId == request.ProjectId)
            .OrderBy(m => m.Sequence ?? int.MaxValue)
            .ThenBy(m => m.Id)
            .ToListAsync();

        var board = new TrelloProjectCreationRequest
        {
            ProjectId = project.Id,
            ProjectTitle = project.Title,
            ProjectDescription = project.Description,
            ProjectLengthWeeks = DefaultSprintCount,
            SprintLengthWeeks = 1,
            DueDate = null,
            TeamMembers = new List<TrelloTeamMember>(),
            StudentEmails = new List<string>(),
            SprintPlan = new TrelloSprintPlan
            {
                Boards = new List<TrelloBoard>(),
                Lists = BuildLists(),
                Cards = new List<TrelloCard>(),
                TotalSprints = DefaultSprintCount,
                EstimatedWeeks = DefaultSprintCount,
                TotalTasks = 0
            }
        };

        for (var sprint = 1; sprint <= DefaultSprintCount; sprint++)
        {
            var moduleForSprint = ResolveModuleForSprint(modules, sprint);
            var generatedCard = await GenerateRoleCardAsync(project, request, instituteRole, sprint, moduleForSprint);
            board.SprintPlan.Cards.Add(generatedCard);
        }

        foreach (var module in modules)
        {
            board.SprintPlan.Cards.Add(new TrelloCard
            {
                Name = module.Title ?? $"Module {module.Id}",
                Description = "Add user story details and acceptance criteria.",
                ListName = "User Stories",
                AssignedToEmail = "",
                AssignedToName = "",
                Labels = new List<string>(),
                DueDate = null,
                Priority = 1,
                EstimatedHours = 2,
                RoleName = "",
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

        var instituteTemplate = await _context.InstituteTemplates
            .FirstOrDefaultAsync(t =>
                t.Id == request.TemplateId &&
                t.ProjectId == request.ProjectId &&
                t.InstituteId == instituteRole.InstituteId);
        if (instituteTemplate == null)
        {
            return new CourseBoardBuildResponse
            {
                Success = false,
                Message = $"InstituteTemplate {request.TemplateId} was not found for project {request.ProjectId} and institute {instituteRole.InstituteId}."
            };
        }

        board.SprintPlan.TotalTasks = board.SprintPlan.Cards.Count;

        instituteTemplate.TrelloBoardJson = JsonSerializer.Serialize(board, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        instituteTemplate.ProjectId = request.ProjectId;
        instituteTemplate.InstituteId = instituteRole.InstituteId;
        await _context.SaveChangesAsync();

        return new CourseBoardBuildResponse
        {
            Success = true,
            Message = $"Board template generated and TrelloBoardJson overridden for InstituteTemplates.Id={instituteTemplate.Id}.",
            BoardTemplate = board
        };
    }

    private static List<TrelloList> BuildLists()
    {
        var lists = new List<TrelloList>();
        for (var i = 1; i <= DefaultSprintCount; i++)
        {
            lists.Add(new TrelloList
            {
                Name = $"Sprint {i}",
                BoardName = "",
                Position = i - 1,
                StartDate = null,
                EndDate = null,
                Description = i == 8
                    ? "Final stabilization sprint focused on QA and bug fixing."
                    : null,
                ChecklistItems = new List<string>()
            });
        }

        lists.Add(new TrelloList { Name = "Bugs", BoardName = "", Position = 8, StartDate = null, EndDate = null, ChecklistItems = new List<string>() });
        lists.Add(new TrelloList { Name = "User Stories", BoardName = "", Position = 9, StartDate = null, EndDate = null, ChecklistItems = new List<string>() });
        return lists;
    }

    private static ProjectModule? ResolveModuleForSprint(List<ProjectModule> modules, int sprintNumber)
    {
        // Keep first two sprints as onboarding/foundation (no module binding).
        if (sprintNumber <= 2 || modules.Count == 0)
        {
            return null;
        }

        var moduleIndex = Math.Min(sprintNumber - 3, modules.Count - 1);
        return modules[moduleIndex];
    }

    private async Task<TrelloCard> GenerateRoleCardAsync(
        Project project,
        CourseBoardBuildRequest request,
        InstituteRole role,
        int sprintNumber,
        ProjectModule? module)
    {
        var fallback = BuildFallbackCard(role, sprintNumber, module);

        try
        {
            var prompt = BuildSprintPrompt(project, role, sprintNumber, module);
            var raw = await _aiService.GenerateTextResponseAsync(prompt);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            var parsed = TryParseSprintCard(raw);
            if (parsed == null)
            {
                return fallback;
            }

            fallback.Name = parsed.Name;
            fallback.Description = parsed.Description;
            fallback.ChecklistItems = parsed.ChecklistItems;
            return fallback;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to AI-generate card for sprint {SprintNumber}", sprintNumber);
            return fallback;
        }
    }

    private static string BuildSprintPrompt(
        Project project,
        InstituteRole role,
        int sprintNumber,
        ProjectModule? module)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You generate a SINGLE sprint card for a Trello-like project board.");
        sb.AppendLine("Return JSON only with keys: name, description, checklistItems.");
        sb.AppendLine("checklistItems must be an array of 7-12 concise technical tasks.");
        sb.AppendLine("Exclude customer interviews or business context tasks when customerEngagement is false.");
        sb.AppendLine();
        sb.AppendLine($"ProjectTitle: {project.Title}");
        sb.AppendLine($"ProjectDescription: {project.Description}");
        sb.AppendLine($"SprintNumber: {sprintNumber} of {DefaultSprintCount}");
        sb.AppendLine($"RoleName: {role.Name}");
        sb.AppendLine($"CustomerEngagement: {role.CustomerEngagement}");
        sb.AppendLine($"PrimaryTool: {role.Skill?.Name ?? "N/A"}");
        sb.AppendLine($"IsTeamLead: {IsTeamLead(role)}");
        sb.AppendLine($"RequiredSkills: {role.Competencies ?? "Not specified"}");
        sb.AppendLine($"ModuleId: {(module?.Id.ToString() ?? "none")}");
        sb.AppendLine($"ModuleTitle: {(module?.Title ?? "none")}");
        sb.AppendLine($"ModuleDescription: {(module?.Description ?? "none")}");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Sprint 1-2 should be setup, architecture, baseline quality.");
        sb.AppendLine("- Sprint 8 should be stabilization and bug-fix oriented.");
        sb.AppendLine("- Keep the role's technical focus only.");
        return sb.ToString();
    }

    private static TrelloCard BuildFallbackCard(InstituteRole role, int sprintNumber, ProjectModule? module)
    {
        var moduleId = module?.Id.ToString() ?? string.Empty;
        var defaultItems = new List<string>
        {
            "Create sprint branch and sync repository.",
            "Implement core backend logic for sprint scope.",
            "Add validation and error handling for API workflows.",
            "Write unit and integration tests for critical paths.",
            "Review query performance and optimize bottlenecks.",
            "Run peer code review and resolve feedback.",
            "Prepare technical notes and PR summary."
        };

        if (sprintNumber == 8)
        {
            defaultItems = new List<string>
            {
                "Create dedicated bugs branch for stabilization.",
                "Validate all backend endpoints for regressions.",
                "Fix API, validation, and data consistency bugs.",
                "Add regression tests for resolved defects.",
                "Verify performance and error monitoring signals.",
                "Conduct final code review before merge.",
                "Complete final PR and merge stabilization changes."
            };
        }

        return new TrelloCard
        {
            Name = sprintNumber switch
            {
                1 => "Infrastructure Audit & Technical Setup",
                2 => "Data Architecture & Technical Readiness",
                8 => "Stabilization & Final Polish",
                _ => $"Sprint {sprintNumber} Feature Execution"
            },
            Description = $"Technical work plan for Sprint {sprintNumber} focused on {role.Name}.",
            ListName = $"Sprint {sprintNumber}",
            AssignedToEmail = "",
            AssignedToName = "",
            Labels = new List<string> { role.Name },
            DueDate = null,
            Priority = 1,
            EstimatedHours = 8,
            RoleName = role.Name,
            Status = "To Do",
            Risk = sprintNumber >= 7 ? "High" : "Medium",
            ModuleId = moduleId,
            CardId = $"{sprintNumber}-B",
            Dependencies = new List<string>(),
            Branched = false,
            ChecklistItems = defaultItems,
            RequiredSkillData = true,
            RequiredResourceData = true
        };
    }

    private static bool IsTeamLead(InstituteRole role)
    {
        if (role.Type == 4)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(role.Category) &&
            role.Category.Contains("lead", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static CourseSprintCardResult? TryParseSprintCard(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed.Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                             .Replace("```", "")
                             .Trim();
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        var json = trimmed[start..(end + 1)];
        try
        {
            return JsonSerializer.Deserialize<CourseSprintCardResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private sealed class CourseSprintCardResult
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> ChecklistItems { get; set; } = new();
    }
}
