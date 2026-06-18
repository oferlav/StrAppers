using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using strAppersBackend.Utilities;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public partial class ProjectsController : ControllerBase
{
    private const string UploadedDesignMarker = "[UploadedDesignParsed]";
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProjectsController> _logger;
    private readonly IDesignDocumentService _designDocumentService;
    private readonly BusinessLogicConfig _businessLogicConfig;
    private readonly KickoffConfig _kickoffConfig;
    private readonly EngagementRulesConfig _engagementRulesConfig;
    private readonly IConfiguration _configuration;
    private readonly IKickoffService _kickoffService;
    private readonly IAIService _aiService;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly IWebHostEnvironment _environment;
    private readonly IOptions<ProjectsInstituteMaxLengthFieldsOptions> _headerFieldWordOptions;
    private readonly IAzureBlobStorageService _azureBlobStorage;
    private readonly ISmtpEmailService _smtpEmailService;

    /// <summary>Project name max words; from <c>ProjectsInstitute:MaxLengthFields:ProjectNameWords</c> (min 1).</summary>
    private int EffectiveHeaderProjectNameWords => Math.Max(1, _headerFieldWordOptions.Value.ProjectNameWords);

    private int EffectiveHeaderMissionWords => Math.Max(1, _headerFieldWordOptions.Value.MissionWords);
    private int EffectiveHeaderOneLinerWords => Math.Max(1, _headerFieldWordOptions.Value.OneLinerWords);
    private int EffectiveHeaderShortBriefWords => Math.Max(1, _headerFieldWordOptions.Value.ShortBriefWords);

    public ProjectsController(
        ApplicationDbContext context,
        ILogger<ProjectsController> logger,
        IDesignDocumentService designDocumentService,
        IOptions<BusinessLogicConfig> businessLogicConfig,
        IOptions<KickoffConfig> kickoffConfig,
        IOptions<EngagementRulesConfig> engagementRulesConfig,
        IConfiguration configuration,
        IKickoffService kickoffService,
        IAIService aiService,
        IChatCompletionService chatCompletionService,
        IWebHostEnvironment environment,
        IOptions<ProjectsInstituteMaxLengthFieldsOptions> headerFieldWordOptions,
        IAzureBlobStorageService azureBlobStorage,
        ISmtpEmailService smtpEmailService)
    {
        _context = context;
        _logger = logger;
        _designDocumentService = designDocumentService;
        _businessLogicConfig = businessLogicConfig.Value;
        _kickoffConfig = kickoffConfig.Value;
        _engagementRulesConfig = engagementRulesConfig.Value;
        _configuration = configuration;
        _kickoffService = kickoffService;
        _aiService = aiService;
        _chatCompletionService = chatCompletionService;
        _environment = environment;
        _headerFieldWordOptions = headerFieldWordOptions;
        _azureBlobStorage = azureBlobStorage;
        _smtpEmailService = smtpEmailService;
    }

    private async Task<int?> ResolveInstituteIdFromAuthContextAsync()
    {
        var userType = Request.Headers["X-User-Type"].FirstOrDefault()?.Trim();
        var userEmail = Request.Headers["X-User-Email"].FirstOrDefault()?.Trim();

        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(userType) &&
            !string.Equals(userType, "institute", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var teacher = await _context.Teachers
            .AsNoTracking()
            .Include(t => t.Institute)
            .FirstOrDefaultAsync(t => t.Email.ToLower() == userEmail.ToLower());

        if (teacher?.Institute == null || !teacher.Institute.IsActive)
        {
            return null;
        }

        return teacher.InstituteId;
    }

    private static bool HasMeaningfulText(string? s) => !string.IsNullOrWhiteSpace(s);

    private async Task<ProjectReadyValidationDto> BuildProjectReadyValidationForInstituteProjectAsync(
        int projectId,
        int expectedInstituteId,
        CancellationToken cancellationToken = default)
    {
        var missing = new List<string>();
        var project = await _context.Projects
            .AsNoTracking()
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project == null)
        {
            return new ProjectReadyValidationDto
            {
                IsReady = false,
                MissingRequirements = new List<string> { "Project not found" }
            };
        }

        if (project.InstituteId != expectedInstituteId)
        {
            return new ProjectReadyValidationDto
            {
                IsReady = false,
                MissingRequirements = new List<string> { "Project is not in your organization" }
            };
        }

        if (!HasMeaningfulText(project.Title)) missing.Add("Title");
        if (!HasMeaningfulText(project.Description)) missing.Add("Description");
        if (!HasMeaningfulText(project.CustomerPastStory)) missing.Add("CustomerPastStory");
        if (!HasMeaningfulText(project.ShortBrief)) missing.Add("ShortBrief");
        if (!HasMeaningfulText(project.Mission)) missing.Add("Mission");
        if (!HasMeaningfulText(project.OneLiner)) missing.Add("OneLiner");
        if (!HasMeaningfulText(project.Logo)) missing.Add("Logo");

        var hasType2Module = await _context.ProjectModules
            .AsNoTracking()
            .AnyAsync(pm => pm.ProjectId == projectId && pm.ModuleType == 2, cancellationToken);
        if (!hasType2Module) missing.Add("At least one ProjectModule with ModuleType 2");

        return new ProjectReadyValidationDto
        {
            IsReady = missing.Count == 0,
            MissingRequirements = missing
        };
    }

    private async Task<bool> InstituteProjectHasSyllabusTemplateAsync(
        int projectId,
        int instituteId,
        CancellationToken cancellationToken = default)
    {
        // Only an explicit InstituteTemplates selection counts as a course assignment.
        // TrelloBoardJson on the project row is NOT sufficient — modules may have diverged after copy.
        return await _context.InstituteTemplates
            .AsNoTracking()
            .AnyAsync(
                t => t.InstituteId == instituteId && t.ProjectId == projectId,
                cancellationToken);
    }

    public sealed class ProjectTemplateDto
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? ProjectBrief { get; set; }
        public string? Name { get; set; }
        public string? TrelloBoardJson { get; set; }
        /// <summary>Trello board URL from <c>InstituteTemplates.BoardURL</c> when this payload comes from an institute template row.</summary>
        public string? BoardUrl { get; set; }
        /// <summary>"Squad" or "Role". Null/empty when the row pre-dates this field.</summary>
        public string? CourseType { get; set; }
        /// <summary>Number of parallel students for Role-type courses (1–5).</summary>
        public int? RoleCount { get; set; }
    }

    public sealed class InstituteTemplateListItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        /// <summary>Trello board URL when generated for this institute template (same as <c>InstituteTemplates.BoardURL</c>).</summary>
        public string? BoardUrl { get; set; }

        /// <summary><see cref="InstituteTemplates.IsActive"/> — assigned course for Project Designs.</summary>
        public bool IsActive { get; set; }
        /// <summary>"Squad" or "Role".</summary>
        public string? CourseType { get; set; }
        /// <summary>Number of parallel students for Role-type courses (1–5).</summary>
        public int? RoleCount { get; set; }
    }

    public sealed class CourseCatalogItemDto
    {
        public string Kind { get; set; } = "institute"; // "builtIn" | "institute"
        public int ProjectId { get; set; }
        public int? InstituteTemplateId { get; set; }
        public string Name { get; set; } = string.Empty;
        /// <summary>Underlying project design title (for pickers that should show project name, e.g. Create Course).</summary>
        public string? ProjectTitle { get; set; }
        /// <summary>Set for <see cref="Kind"/> institute rows when a board URL was saved.</summary>
        public string? BoardUrl { get; set; }

        /// <summary>For <see cref="Kind"/> institute: true when <see cref="ProjectId"/> is an <see cref="InstituteProject"/> id.</summary>
        public bool InstituteProjectDesign { get; set; }

        /// <summary>When <see cref="InstituteProjectDesign"/> is true: <see cref="InstituteProject.IsBuiltIn"/> (activated catalog copy).</summary>
        public bool InstituteProjectIsBuiltIn { get; set; }

        /// <summary>Whether the underlying project is marked in use (Create Course / catalog filtering).</summary>
        public bool InUse { get; set; } = true;

        /// <summary>
        /// Same readiness as <c>GET .../project-ready-validation</c> (not pending): required header fields + at least one ModuleType 2 module.
        /// Catalog built-ins use catalog validation (typically ready).
        /// </summary>
        public bool IsReady { get; set; } = true;

        /// <summary>Institute rows: <see cref="InstituteTemplates.IsActive"/> (active course in Project Designs). Built-in rows: false.</summary>
        public bool IsActive { get; set; }
        /// <summary>"Squad" or "Role". Only set for institute rows.</summary>
        public string? CourseType { get; set; }
        /// <summary>Number of parallel students for Role-type courses (1–5). Only set for institute rows.</summary>
        public int? RoleCount { get; set; }
        /// <summary>Logo/image URL of the linked project or institute project design.</summary>
        public string? ProjectLogo { get; set; }
    }

    /// <summary>Project row for Courses &quot;Create Course&quot; project combo (institute-owned and optional global built-ins).</summary>
    public sealed class CourseCreateProjectOptionDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int? InstituteId { get; set; }
        public bool IsBuiltIn { get; set; }
        /// <summary>True when <see cref="Id"/> refers to <c>InstituteProjects</c> (custom or activated built-in copy).</summary>
        public bool IsInstituteProject { get; set; }
    }

    /// <summary>Unified list item for Project Designs and institute project pickers (JSON matches <see cref="Project"/> fields used by the SPA plus flags).</summary>
    public sealed class ProjectDesignsListItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        /// <summary>Built-in catalog: student-facing course label (from <c>Projects.CourseName</c>).</summary>
        public string? CourseName { get; set; }
        /// <summary><see cref="InstituteProject"/> only: mirrored <c>Projects.CourseName</c> from catalog activate/copy.</summary>
        public string? BuiltInCourseName { get; set; }
        public string? Mission { get; set; }
        public string? OneLiner { get; set; }
        public string? Description { get; set; }
        public string? ExtendedDescription { get; set; }
        public string? ShortBrief { get; set; }
        public string Priority { get; set; } = "Medium";
        public int? OrganizationId { get; set; }
        public int? InstituteId { get; set; }
        public bool IsAvailable { get; set; }
        public bool InUse { get; set; }
        public bool? Kickoff { get; set; }
        public string? CriteriaIds { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        /// <summary>True when the row is stored in <c>InstituteProjects</c>.</summary>
        public bool InstituteProject { get; set; }
        public int? BaseProjectId { get; set; }
        /// <summary>Coupon code for student registration. Set on activation.</summary>
        public string? Coupon { get; set; }
    }

    public sealed class DuplicateProjectRequest
    {
        public int? InstituteId { get; set; }
    }

    public sealed class ProjectHeaderDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Logo { get; set; }
        public string? Mission { get; set; }
        public string? ShortBrief { get; set; }
        public string? OneLiner { get; set; }
        public string? Description { get; set; }
        public string? CustomerPastStory { get; set; }
    }

    public sealed class UpdateProjectHeaderRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Logo { get; set; }
        public string? Mission { get; set; }
        public string? ShortBrief { get; set; }
        public string? OneLiner { get; set; }
        public string? Description { get; set; }
        public string? CustomerPastStory { get; set; }
    }

    public sealed class ProjectReadyValidationDto
    {
        public bool IsReady { get; set; }
        public List<string> MissingRequirements { get; set; } = new();
    }

    /// <summary>Result of the project header AI assistant (POST ai-assistant/General/...).</summary>
    public sealed class ProjectHeaderAiAssistantResponse
    {
        public string AssistantMessage { get; set; } = string.Empty;
        public string? SuggestedTitle { get; set; }
        public string? SuggestedMission { get; set; }
        public string? SuggestedOneLiner { get; set; }
        public string? SuggestedShortBrief { get; set; }
    }

    /// <summary>Body for <c>POST /api/Projects/use/by-institute/ai-assistant/General/{id}</c>. Use current form values; server falls back to DB when a field is omitted.</summary>
    public sealed class ProjectInstituteAssistantRequest
    {
        /// <summary>Validated in <see cref="ProjectsController.PostInstituteProjectAssistant"/> (not via model binder, so Templates bodies are not rejected).</summary>
        public string UserRequest { get; set; } = string.Empty;

        public string? CurrentTitle { get; set; }
        public string? CurrentMission { get; set; }
        public string? CurrentOneLiner { get; set; }
        public string? CurrentShortBrief { get; set; }
    }

    /// <summary>Internal shape for deserializing the model JSON; keys align with the prompt.</summary>
    private sealed class ProjectHeaderAiAssistantJson
    {
        public string? AssistantMessage { get; set; }
        public string? SuggestedTitle { get; set; }
        public string? SuggestedMission { get; set; }
        public string? SuggestedOneLiner { get; set; }
        public string? SuggestedShortBrief { get; set; }
    }

    public sealed class ProjectInstituteBriefAssistantRequest
    {
        public string UserRequest { get; set; } = string.Empty;
        public string? CurrentDescription { get; set; }
        public string? OriginalDescription { get; set; }
    }

    public sealed class ProjectBriefAiAssistantResponse
    {
        public string AssistantMessage { get; set; } = string.Empty;
        public string? SuggestedDescription { get; set; }
    }

    public sealed class ProjectInstituteCustomerAssistantRequest
    {
        public string UserRequest { get; set; } = string.Empty;
        public string? CurrentCustomerPastStory { get; set; }
        public string? OriginalCustomerPastStory { get; set; }
        public List<ProjectModuleAssistantItem>? CurrentModules { get; set; }
        public List<ProjectModuleAssistantItem>? OriginalModules { get; set; }
        public string? CurrentMission { get; set; }
        public string? CurrentShortBrief { get; set; }
        public string? CurrentDescription { get; set; }
    }

    public sealed class ProjectCustomerAiAssistantResponse
    {
        public string AssistantMessage { get; set; } = string.Empty;
        public string? SuggestedCustomerPastStory { get; set; }
    }

    public sealed class ProjectInstituteModulesAssistantRequest
    {
        public string UserRequest { get; set; } = string.Empty;
        public List<ProjectModuleAssistantItem>? CurrentModules { get; set; }
        public List<ProjectModuleAssistantItem>? OriginalModules { get; set; }
        public string? CurrentMission { get; set; }
        public string? CurrentShortBrief { get; set; }
        public string? CurrentDescription { get; set; }
    }

    public sealed class ProjectModuleAssistantItem
    {
        /// <summary>ProjectModules.Id — include in suggestedModules when editing/reordering an existing row.</summary>
        public int? ModuleId { get; set; }

        public int? Sequence { get; set; }
        public string? Title { get; set; }
        public string? Body { get; set; }
    }

    public sealed class ProjectModulesAiAssistantResponse
    {
        public string AssistantMessage { get; set; } = string.Empty;
        public List<ProjectModuleAssistantItem>? SuggestedModules { get; set; }
        public bool DesignContextUsed { get; set; }
        public int DesignContextChars { get; set; }
        public string DesignContextSource { get; set; } = string.Empty;
    }

    private sealed class ProjectBriefAiAssistantJson
    {
        public string? AssistantMessage { get; set; }
        public string? SuggestedDescription { get; set; }
    }

    private sealed class ProjectModulesAiAssistantJson
    {
        public string? AssistantMessage { get; set; }
        public List<ProjectModuleAssistantItem>? SuggestedModules { get; set; }
    }

    private sealed class ProjectCustomerAiAssistantJson
    {
        public string? AssistantMessage { get; set; }
        public string? SuggestedCustomerPastStory { get; set; }
    }

    /// <summary>Configured header field word limits (from appsettings, for SPA).</summary>
    public sealed class HeaderFieldWordLimitsResponseDto
    {
        public int ProjectNameWords { get; set; }
        public int MissionWords { get; set; }
        public int OneLinerWords { get; set; }
        public int ShortBriefWords { get; set; }
    }

    public sealed class ProjectHeaderTemplatesResponseDto
    {
        public List<TemplateOptionDto> Templates { get; set; } = new();
        public int ActiveTemplateId { get; set; }
        public string ActiveTemplateName { get; set; } = "System";
    }

    public sealed class TemplateOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    private sealed class CourseSourceForAssignment
    {
        public int TemplateId { get; set; } // 0 = built-in
        public string Name { get; set; } = string.Empty;
        public bool IsBuiltIn { get; set; }
        public string TrelloBoardJson { get; set; } = string.Empty;
        public List<(int Id, string Title, int Sequence)> SourceModules { get; set; } = new();
        public InstituteTemplate? TemplateRow { get; set; }
    }

    private static string NormalizeProjectTitleKey(string? title)
    {
        return string.Join(
            " ",
            (title ?? string.Empty)
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
    }

    private static string BuildCopyLikeTitle(string baseTitle, int suffixNumber, int limit)
    {
        var suffix = suffixNumber <= 1 ? "-Copy" : $"-Copy{suffixNumber}";
        var maxBaseLen = Math.Max(1, limit - suffix.Length);
        var trimmedBase = baseTitle.Length > maxBaseLen ? baseTitle[..maxBaseLen] : baseTitle;
        return $"{trimmedBase}{suffix}";
    }

    /// <summary>
    /// Create a new institute project design from scratch (minimal defaults).
    /// Route: POST /api/Projects/use/by-institute/create-empty
    /// </summary>
    [HttpPost("use/by-institute/create-empty")]
    public async Task<ActionResult<ProjectDesignsListItemDto>> CreateEmptyProjectDesignForInstitute()
    {
        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            var instituteExists = await _context.Institutes.AsNoTracking().AnyAsync(i => i.Id == instituteId.Value && i.IsActive);
            if (!instituteExists)
            {
                return NotFound($"Institute with ID {instituteId.Value} not found or inactive.");
            }

            const int preferredOrganizationId = 100;
            var organizationId =
                await _context.Organizations.AsNoTracking().AnyAsync(o => o.Id == preferredOrganizationId)
                    ? preferredOrganizationId
                    : (int?)null;
            if (organizationId == null)
            {
                _logger.LogWarning(
                    "CreateEmptyProjectDesign: Organizations.Id={PreferredOrgId} not found — inserting project with OrganizationId=null (avoid FK violation). InstituteId={InstituteId}",
                    preferredOrganizationId,
                    instituteId.Value);
            }

            _logger.LogInformation(
                "CreateEmptyProjectDesign: saving new InstituteProject InstituteId={InstituteId}, OrganizationId={OrganizationId}",
                instituteId.Value,
                organizationId);

            const int titleLimit = 200;
            const string defaultBaseTitle = "New Project Design";
            var builtInTitles = await _context.Projects.AsNoTracking()
                .Where(p => p.InstituteId == null && p.IsAvailable && p.Title != null)
                .Select(p => p.Title!)
                .ToListAsync();
            var instituteTitles = await _context.InstituteProjects.AsNoTracking()
                .Where(ip => ip.InstituteId == instituteId.Value && ip.Title != null)
                .Select(ip => ip.Title!)
                .ToListAsync();
            var builtInTitleKeys = new HashSet<string>(
                builtInTitles.Select(NormalizeProjectTitleKey),
                StringComparer.Ordinal);
            var instituteTitleKeys = new HashSet<string>(
                instituteTitles.Select(NormalizeProjectTitleKey),
                StringComparer.Ordinal);
            var mergedTakenKeys = new HashSet<string>(instituteTitleKeys, StringComparer.Ordinal);
            foreach (var k in builtInTitleKeys) mergedTakenKeys.Add(k);

            var resolvedTitle = defaultBaseTitle;
            var n = 1;
            while (mergedTakenKeys.Contains(NormalizeProjectTitleKey(resolvedTitle)))
            {
                n++;
                resolvedTitle = BuildCopyLikeTitle(defaultBaseTitle, n, titleLimit);
            }

            var hasModuleType2 = await _context.ModuleTypes
                .AsNoTracking()
                .AnyAsync(mt => mt.Id == 2);

            using var transaction = await _context.Database.BeginTransactionAsync();

            // Step 1: create a placeholder Projects row so InstituteProject.BaseProjectId is always set.
            // IsAvailable=false / InUse=false keeps it out of all B2C project listings.
            var placeholder = new Project
            {
                Title = resolvedTitle,
                InstituteId = instituteId.Value,
                IsAvailable = false,
                InUse = false,
                CreatedAt = DateTime.UtcNow,
            };
            _context.Projects.Add(placeholder);
            await _context.SaveChangesAsync(); // resolves placeholder.Id

            // Step 2: create InstituteProject with BaseProjectId already set — no separate UPDATE needed.
            var project = new InstituteProject
            {
                Title = resolvedTitle,
                OrganizationId = organizationId,
                InstituteId = instituteId.Value,
                IsAvailable = false,
                InUse = true,
                Priority = "Medium",
                BaseProjectId = placeholder.Id,
                CreatedAt = DateTime.UtcNow,
            };
            _context.InstituteProjects.Add(project);
            await _context.SaveChangesAsync(); // resolves project.Id

            // Step 3: create default module.
            if (hasModuleType2)
            {
                _context.InstituteProjectModules.Add(new InstituteProjectModule
                {
                    InstituteProjectId = project.Id,
                    ModuleType = 2,
                    Title = "Module 1",
                    Description = string.Empty,
                    Sequence = 1,
                });
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "CreateEmptyProjectDesign: created default module for InstituteProjectId={InstituteProjectId}",
                    project.Id);
            }
            else
            {
                _logger.LogWarning(
                    "CreateEmptyProjectDesign: ModuleType=2 not found; skipped default ProjectModule row for InstituteProjectId={InstituteProjectId}",
                    project.Id);
            }

            await transaction.CommitAsync();
            _logger.LogInformation(
                "CreateEmptyProjectDesign: committed. PlaceholderProjectId={PlaceholderId}, InstituteProjectId={IpId}, Title={Title}",
                placeholder.Id, project.Id, project.Title);

            return Ok(new ProjectDesignsListItemDto
            {
                Id = project.Id,
                Title = project.Title,
                CourseName = project.Title,
                BuiltInCourseName = project.BuiltInCourseName,
                Mission = project.Mission,
                OneLiner = project.OneLiner,
                Description = project.Description,
                ExtendedDescription = project.ExtendedDescription,
                ShortBrief = project.ShortBrief,
                Priority = project.Priority,
                OrganizationId = project.OrganizationId,
                InstituteId = project.InstituteId,
                IsAvailable = project.IsAvailable,
                InUse = project.InUse,
                Kickoff = project.Kickoff,
                CriteriaIds = project.CriteriaIds,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt,
                InstituteProject = true,
                BaseProjectId = project.BaseProjectId,
            });
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx,
                "CreateEmptyProjectDesign DB error (inner={Inner}). SqlState hints: inspect InnerException.",
                dbEx.InnerException?.Message ?? "(none)");
            if (_environment.IsDevelopment())
            {
                return StatusCode(500, new
                {
                    error = "CreateEmptyProjectDesignDbError",
                    message = dbEx.Message,
                    inner = dbEx.InnerException?.Message,
                    sqlState = dbEx.InnerException?.InnerException?.Message,
                });
            }

            return StatusCode(500, new { error = "CreateEmptyProjectDesignDbError", message = "Database save failed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error creating empty project design for institute: {Message}. Inner={Inner}",
                ex.Message,
                ex.InnerException?.Message ?? "(none)");
            if (_environment.IsDevelopment())
            {
                return StatusCode(500, new
                {
                    error = "CreateEmptyProjectDesignFailed",
                    message = ex.Message,
                    inner = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace,
                });
            }

            return StatusCode(500, new { error = "CreateEmptyProjectDesignFailed", message = ex.Message });
        }
    }

    /// <summary>
    /// Permanently delete a project that belongs to the current institute. System (global) projects cannot be deleted.
    /// Routes: POST /api/Projects/use/by-institute/{id}/delete, POST /api/Projects/use/by-institute/delete/{id}
    /// </summary>
    [HttpPost("use/by-institute/{id:int}/delete")]
    [HttpPost("use/by-institute/delete/{id:int}")]
    public async Task<IActionResult> DeleteInstituteProjectForInstituteContext(int id)
    {
        try
        {
            var authInstituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!authInstituteId.HasValue || authInstituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            var ip = await _context.InstituteProjects.FirstOrDefaultAsync(p => p.Id == id);
            if (ip != null)
            {
                if (ip.InstituteId != authInstituteId.Value)
                {
                    return Forbid();
                }

                // Block delete if any student in this institute still has this InstituteProject in their selection slots.
                if (await _context.Students.AsNoTracking().AnyAsync(s =>
                        s.InstituteId == authInstituteId.Value
                        && (s.InstitutePriority1 == id
                            || s.InstitutePriority2 == id
                            || s.InstitutePriority3 == id
                            || s.InstitutePriority4 == id)))
                {
                    return Conflict("This project is still used in student project selections and cannot be deleted yet.");
                }

                _context.InstituteProjects.Remove(ip);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Institute {InstituteId} deleted InstituteProject {ProjectId}", authInstituteId.Value, id);
                return Ok(new { success = true, id, instituteProject = true });
            }

            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found.");
            }

            if (project.InstituteId == null)
            {
                return BadRequest("System projects cannot be deleted.");
            }

            if (project.InstituteId != authInstituteId.Value)
            {
                return Forbid();
            }

            if (await _context.ProjectInstances.AsNoTracking().AnyAsync(pi => pi.ProjectId == id))
            {
                return Conflict("This project has project instances and cannot be deleted yet.");
            }

            if (await _context.Students.AsNoTracking().AnyAsync(s =>
                    s.ProjectId == id
                    || s.ProjectPriority1 == id
                    || s.ProjectPriority2 == id
                    || s.ProjectPriority3 == id
                    || s.ProjectPriority4 == id))
            {
                return Conflict("This project is still used in student project selections and cannot be deleted yet.");
            }

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Institute {InstituteId} deleted project {ProjectId}", authInstituteId.Value, id);
            return Ok(new { success = true, id, instituteProject = false });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "DB error deleting project {ProjectId} for institute", id);
            return Conflict("This project could not be deleted. It may still be referenced by boards, templates, or other data.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {ProjectId} for institute", id);
            return StatusCode(500, "An error occurred while deleting the project.");
        }
    }

    /// <summary>
    /// Get all projects
    /// </summary>
    [HttpGet]
    [Obsolete("This method is disabled. Use /use/ routing instead.")]
    public async Task<ActionResult<IEnumerable<Project>>> GetProjects()
    {
        try
        {
            var projects = await _context.Projects
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Mission = p.Mission,
                    OneLiner = p.OneLiner,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    ShortBrief = p.ShortBrief,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    IsAvailable = p.IsAvailable,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                    // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                })
                .ToListAsync();

            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects");
            return StatusCode(500, "An error occurred while retrieving projects");
        }
    }

    /// <summary>
    /// Get a specific project by ID
    /// </summary>
    [HttpGet("{id}")]
    [Obsolete("This method is disabled. Use /use/ routing instead.")]
    public async Task<ActionResult<Project>> GetProject(int id)
    {
        try
        {
            var project = await _context.Projects
                .Where(p => p.Id == id)
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Mission = p.Mission,
                    OneLiner = p.OneLiner,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    ShortBrief = p.ShortBrief,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    IsAvailable = p.IsAvailable,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                    // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                })
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound($"Project with ID {id} not found");
            }

            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project with ID {ProjectId}", id);
            return StatusCode(500, "An error occurred while retrieving the project");
        }
    }


    /// <summary>
    /// Get projects by organization
    /// </summary>
    [HttpGet("use/by-organization/{organizationId}")]
    public async Task<ActionResult<IEnumerable<Project>>> GetProjectsByOrganization(int organizationId)
    {
        try
        {
            _logger.LogInformation("Getting projects for organization {OrganizationId}", organizationId);
            
            // Test database connection first
            var totalProjects = await _context.Projects.CountAsync();
            _logger.LogInformation("Database connection successful. Total projects in database: {Count}", totalProjects);
            
            // First check if organization exists
            var organizationExists = await _context.Organizations.AnyAsync(o => o.Id == organizationId);
            _logger.LogInformation("Organization {OrganizationId} exists: {Exists}", organizationId, organizationExists);
            
            if (!organizationExists)
            {
                _logger.LogWarning("Organization {OrganizationId} not found", organizationId);
                return NotFound($"Organization with ID {organizationId} not found");
            }
            
            // Get all projects first to see what we have
            var allProjects = await _context.Projects.ToListAsync();
            _logger.LogInformation("All projects in database: {Projects}", 
                string.Join(", ", allProjects.Select(p => $"ID:{p.Id}, OrgId:{p.OrganizationId}")));
            
            var projects = await _context.Projects
                .Where(p => p.OrganizationId == organizationId)
                .OrderBy(p => p.CreatedAt)
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Mission = p.Mission,
                    OneLiner = p.OneLiner,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    ShortBrief = p.ShortBrief,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    IsAvailable = p.IsAvailable,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                    // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                })
                .ToListAsync();

            _logger.LogInformation("Found {Count} projects for organization {OrganizationId}", projects.Count, organizationId);
            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects for organization {OrganizationId}: {Message}", organizationId, ex.Message);
            return StatusCode(500, $"An error occurred while retrieving projects for the organization: {ex.Message}");
        }
    }

    /// <summary>
    /// Create a new project
    /// </summary>
    [HttpPost("use/create/{organizationId}")]
    public async Task<ActionResult<Project>> CreateProject(int organizationId, CreateProjectRequest request)
    {
        try
        {
            // Validate the request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate ExtendedDescription minimum word count
            var minimumWords = _configuration.GetValue<int>("ProjectsOrg:ExtendedDescriptionMinimumWords", 50);
            if (!string.IsNullOrWhiteSpace(request.ExtendedDescription))
            {
                var wordCount = request.ExtendedDescription.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount < minimumWords)
                {
                    var errorMessage = $"ExtendedDescription must contain at least {minimumWords} words (currently {wordCount} words). A rich and detailed project description is essential for creating a comprehensive system design that guides the development team effectively. Please provide more details about the project's goals, requirements, features, and technical considerations.";
                    _logger.LogWarning("ExtendedDescription validation failed: {WordCount} words (minimum: {MinimumWords})", wordCount, minimumWords);
                    return BadRequest(new { Success = false, Message = errorMessage, WordCount = wordCount, MinimumWords = minimumWords });
                }
            }

            // Validate organization exists
            var organization = await _context.Organizations
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            if (organization == null)
            {
                return BadRequest($"Organization with ID {organizationId} not found");
            }

            if (!organization.IsActive)
            {
                return BadRequest($"Organization '{organization.Name}' is not active");
            }


            // Create new project
            var project = new Project
            {
                Title = request.Title,
                Mission = request.Mission,
                OneLiner = request.OneLiner,
                Description = request.Description,
                ExtendedDescription = request.ExtendedDescription,
                ShortBrief = request.ShortBrief,
                Priority = request.Priority,
                OrganizationId = organizationId,
                IsAvailable = request.IsAvailable,
                CriteriaIds = request.CriteriaIds,
                CreatedAt = DateTime.UtcNow
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            // Load the project with related data for response (excluding problematic fields)
            var createdProject = await _context.Projects
                .Where(p => p.Id == project.Id)
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Mission = p.Mission,
                    OneLiner = p.OneLiner,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    ShortBrief = p.ShortBrief,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    IsAvailable = p.IsAvailable,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                    // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                })
                .FirstOrDefaultAsync();

            _logger.LogInformation("Project created successfully with ID {ProjectId} and title {Title}", 
                project.Id, project.Title);

            // Classify project criteria using AI
            try
            {
                _logger.LogInformation("Classifying project criteria for project {ProjectId}", project.Id);
                
                // Get available project criteria (only active; used for classification)
                var availableCriteria = await _context.ProjectCriterias
                    .Where(c => c.Active)
                    .OrderBy(c => c.Id)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} available criteria for classification", availableCriteria.Count);

                // Call AI service to classify the project
                var criteriaClassification = await _aiService.ClassifyProjectCriteriaAsync(
                    project.Title,
                    project.Description ?? "",
                    project.ExtendedDescription,
                    availableCriteria
                );

                if (criteriaClassification != null && criteriaClassification.Success)
                {
                    if (!string.IsNullOrWhiteSpace(criteriaClassification.CriteriaIds))
                    {
                        // Merge with existing CriteriaIds if any
                        var existingCriteriaIds = new HashSet<int>();
                        if (!string.IsNullOrEmpty(project.CriteriaIds))
                        {
                            var existingIds = project.CriteriaIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            foreach (var idStr in existingIds)
                            {
                                if (int.TryParse(idStr, out int id))
                                {
                                    existingCriteriaIds.Add(id);
                                }
                            }
                        }

                        // Add AI-classified criteria
                        var aiClassifiedIds = criteriaClassification.CriteriaIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var idStr in aiClassifiedIds)
                        {
                            if (int.TryParse(idStr, out int id))
                            {
                                existingCriteriaIds.Add(id);
                            }
                        }

                        // Convert to comma-separated string (sorted)
                        var mergedCriteriaIds = existingCriteriaIds.Count > 0
                            ? string.Join(",", existingCriteriaIds.OrderBy(id => id))
                            : null;

                        project.CriteriaIds = mergedCriteriaIds;
                        await _context.SaveChangesAsync();
                        
                        _logger.LogInformation("✅ AI successfully classified project {ProjectId} with CriteriaIds: '{CriteriaIds}'", 
                            project.Id, mergedCriteriaIds ?? "empty");
                    }
                    else
                    {
                        _logger.LogInformation("AI classification returned no criteria for project {ProjectId}", project.Id);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to classify project criteria for project {ProjectId}: {Message}", 
                        project.Id, criteriaClassification?.Message ?? "Unknown error");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error classifying project criteria for project {ProjectId}: {Message}", 
                    project.Id, ex.Message);
                // Don't fail the project creation if criteria classification fails
            }

            // Generate system design for the project
            try
            {
                _logger.LogInformation("Generating system design for project {ProjectId}", project.Id);
                
                var systemDesignRequest = new SystemDesignRequest
                {
                    ProjectId = project.Id,
                    ExtendedDescription = project.ExtendedDescription ?? project.Description ?? "",
                    CreatedBy = null,
                    TeamRoles = new List<RoleInfo>() // Empty for now since students aren't allocated yet
                };

                var systemDesignResponse = await _designDocumentService.CreateSystemDesignAsync(systemDesignRequest);
                
                if (systemDesignResponse != null && systemDesignResponse.Success)
                {
                    // Update the project with the generated system design
                    project.SystemDesign = systemDesignResponse.DesignDocument;
                    project.SystemDesignDoc = systemDesignResponse.DesignDocumentPdf;
                    project.UpdatedAt = DateTime.UtcNow;
                    
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("System design generated and saved for project {ProjectId}", project.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to generate system design for project {ProjectId}: {Message}", 
                        project.Id, systemDesignResponse?.Message ?? "Unknown error");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating system design for project {ProjectId}: {Message}", 
                    project.Id, ex.Message);
                // Don't fail the project creation if system design generation fails
            }

            return Ok(createdProject);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while creating project");
            return StatusCode(500, "An error occurred while saving the project to the database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating project");
            return StatusCode(500, "An unexpected error occurred while creating the project");
        }
    }

    /// <summary>
    /// Create a project without generating system design
    /// </summary>
    [HttpPost("use/create")]
    public async Task<ActionResult<Project>> CreateProjectSimple(CreateProjectSimpleRequest request)
    {
        try
        {
            // Validate the request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate ExtendedDescription minimum word count
            var minimumWords = _configuration.GetValue<int>("ProjectsOrg:ExtendedDescriptionMinimumWords", 50);
            if (!string.IsNullOrWhiteSpace(request.ExtendedDescription))
            {
                var wordCount = request.ExtendedDescription.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount < minimumWords)
                {
                    var errorMessage = $"ExtendedDescription must contain at least {minimumWords} words (currently {wordCount} words). A rich and detailed project description is essential for creating a comprehensive system design that guides the development team effectively. Please provide more details about the project's goals, requirements, features, and technical considerations.";
                    _logger.LogWarning("ExtendedDescription validation failed: {WordCount} words (minimum: {MinimumWords})", wordCount, minimumWords);
                    return BadRequest(new { Success = false, Message = errorMessage, WordCount = wordCount, MinimumWords = minimumWords });
                }
            }

            // Validate organization exists
            var organization = await _context.Organizations
                .FirstOrDefaultAsync(o => o.Id == request.OrganizationId);

            if (organization == null)
            {
                return BadRequest($"Organization with ID {request.OrganizationId} not found");
            }

            if (!organization.IsActive)
            {
                return BadRequest($"Organization '{organization.Name}' is not active");
            }

            // Create new project with default values
            var project = new Project
            {
                Title = request.Title,
                Mission = request.Mission,
                OneLiner = request.OneLiner,
                Description = request.Description,
                ExtendedDescription = request.ExtendedDescription,
                ShortBrief = request.ShortBrief,
                Priority = "high", // Default priority
                OrganizationId = request.OrganizationId,
                IsAvailable = true, // Default isAvailable
                CriteriaIds = request.CriteriaIds,
                CreatedAt = DateTime.UtcNow
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            // Load the project with related data for response (excluding problematic fields)
            var createdProject = await _context.Projects
                .Where(p => p.Id == project.Id)
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Mission = p.Mission,
                    OneLiner = p.OneLiner,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    ShortBrief = p.ShortBrief,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    IsAvailable = p.IsAvailable,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                    // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                })
                .FirstOrDefaultAsync();

            _logger.LogInformation("Project created successfully (simple) with ID {ProjectId} and title {Title}", 
                project.Id, project.Title);

            return Ok(createdProject);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while creating project (simple)");
            return StatusCode(500, "An error occurred while saving the project to the database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating project (simple)");
            return StatusCode(500, "An unexpected error occurred while creating the project");
        }
    }

    /// <summary>
    /// Delete a project
    /// </summary>
    [HttpDelete("{id}")]
    [Obsolete("This method is disabled. Use /use/ routing instead.")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        try
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found");
            }

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Project with ID {ProjectId} deleted successfully", id);

            return NoContent();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while deleting project with ID {ProjectId}", id);
            return StatusCode(500, "An error occurred while deleting the project from the database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while deleting project with ID {ProjectId}", id);
            return StatusCode(500, "An unexpected error occurred while deleting the project");
        }
    }

    /// <summary>
    /// Get projects marked as in use (Task Builder project combo).
    /// </summary>
    [HttpGet("use/available")]
    public async Task<ActionResult<IEnumerable<Project>>> GetAvailableProjects()
    {
        try
        {
            var projects = await _context.Projects
                .Where(p => p.InUse)
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Mission = p.Mission,
                    OneLiner = p.OneLiner,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    ShortBrief = p.ShortBrief,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    IsAvailable = p.IsAvailable,
                    InUse = p.InUse,
                    Kickoff = p.Kickoff,
                    CriteriaIds = p.CriteriaIds,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    ApplicantsCount = _context.Students
                        .Count(s => s.Status == 1 && (
                            s.ProjectId == p.Id ||
                            s.ProjectPriority1 == p.Id ||
                            s.ProjectPriority2 == p.Id ||
                            s.ProjectPriority3 == p.Id ||
                            s.ProjectPriority4 == p.Id
                        ))
                    // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                })
                .ToListAsync();

            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available projects");
            return StatusCode(500, "An error occurred while retrieving available projects");
        }
    }

    /// <summary>
    /// Returns available InstituteProjects for the student's institute.
    /// Only for students with InstituteId > 1. Existing behaviour for InstituteId=0/1 is unchanged.
    /// Route: GET /api/Projects/use/institute/available/{studentId}
    /// </summary>
    [HttpGet("use/institute/available/{studentId}")]
    public async Task<ActionResult> GetAvailableInstituteProjectsForStudent(int studentId)
    {
        try
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
                return NotFound($"Student with ID {studentId} not found.");

            if (student.InstituteId == null || student.InstituteId <= 0)
                return BadRequest("Student does not have a valid institute assignment (InstituteId must be >= 1).");

            // Filter by coupon when present (student registered with a specific coupon)
            var projects = await _context.InstituteProjects
                .Where(p => p.InstituteId == student.InstituteId && p.IsAvailable &&
                            (student.Coupon == null || p.Coupon == student.Coupon))
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Mission,
                    p.OneLiner,
                    p.Description,
                    p.ExtendedDescription,
                    p.ShortBrief,
                    p.Priority,
                    p.IsAvailable,
                    p.InUse,
                    p.Logo,
                    p.BuiltInCourseName,
                    p.CreatedAt,
                    p.UpdatedAt,
                    ApplicantsCount = _context.Students.Count(s => s.Status.HasValue && s.Status <= 1 && (
                        s.InstitutePriority1 == p.Id ||
                        s.InstitutePriority2 == p.Id ||
                        s.InstitutePriority3 == p.Id ||
                        s.InstitutePriority4 == p.Id
                    ))
                })
                .ToListAsync();

            // Load squad roles + template metadata for all returned projects
            var projectIds = projects.Select(p => p.Id).ToList();

            var squadRolesFlat = await _context.InstituteTemplates
                .Where(t => t.InstituteProjectId != null &&
                            projectIds.Contains(t.InstituteProjectId.Value) &&
                            t.IsActive &&
                            t.SquadId != null)
                .SelectMany(t => _context.Roles
                    .Where(r => r.SquadId == t.SquadId && r.IsActive)
                    .Select(r => new { ProjectId = t.InstituteProjectId!.Value, r.Name, r.Type }))
                .ToListAsync();

            var templateMetadata = await _context.InstituteTemplates
                .Where(t => t.InstituteProjectId != null &&
                            projectIds.Contains(t.InstituteProjectId.Value) &&
                            t.IsActive)
                .Select(t => new
                {
                    ProjectId = t.InstituteProjectId!.Value,
                    RequireDeveloperRule = t.Squad != null && t.Squad.RequireDeveloperRule,
                    t.CourseType,
                    t.RoleCount,
                })
                .ToListAsync();

            var squadRolesByProject = squadRolesFlat
                .GroupBy(r => r.ProjectId)
                .ToDictionary(
                    g => g.Key,
                    g => (IEnumerable<object>)g
                        .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(ng => (object)new { ng.First().Name, ng.First().Type })
                        .ToList()
                );

            var metaByProject = templateMetadata
                .GroupBy(m => m.ProjectId)
                .ToDictionary(g => g.Key, g => g.First());

            return Ok(projects.Select(p =>
            {
                var meta = metaByProject.GetValueOrDefault(p.Id);
                return new
                {
                    p.Id,
                    p.Title,
                    p.Mission,
                    p.OneLiner,
                    p.Description,
                    p.ExtendedDescription,
                    p.ShortBrief,
                    p.Priority,
                    p.IsAvailable,
                    p.InUse,
                    p.Logo,
                    p.BuiltInCourseName,
                    p.CreatedAt,
                    p.UpdatedAt,
                    p.ApplicantsCount,
                    SquadRoles = squadRolesByProject.GetValueOrDefault(p.Id, Enumerable.Empty<object>()),
                    RequireDeveloperRule = meta?.RequireDeveloperRule ?? false,
                    CourseType = meta?.CourseType,
                    RoleCount = meta?.RoleCount,
                };
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving institute projects for student {StudentId}", studentId);
            return StatusCode(500, "An error occurred while retrieving institute projects.");
        }
    }

    /// <summary>
    /// Returns available InstituteProjects for a given institute without requiring a student.
    /// Intended for public/anonymous views (e.g. landing page). No coupon filtering is applied.
    /// Route: GET /api/Projects/use/institute/available/by-institute/{instituteId}
    /// </summary>
    [HttpGet("use/institute/available/by-institute/{instituteId:int}")]
    public async Task<ActionResult> GetAvailableInstituteProjectsByInstituteId(int instituteId)
    {
        try
        {
            var projects = await _context.InstituteProjects
                .Where(p => p.InstituteId == instituteId && p.IsAvailable)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Mission,
                    p.OneLiner,
                    p.Description,
                    p.ExtendedDescription,
                    p.ShortBrief,
                    p.Priority,
                    p.IsAvailable,
                    p.InUse,
                    p.Logo,
                    p.BuiltInCourseName,
                    p.CreatedAt,
                    p.UpdatedAt,
                    ApplicantsCount = _context.Students.Count(s => s.Status.HasValue && s.Status <= 1 && (
                        s.InstitutePriority1 == p.Id ||
                        s.InstitutePriority2 == p.Id ||
                        s.InstitutePriority3 == p.Id ||
                        s.InstitutePriority4 == p.Id
                    ))
                })
                .ToListAsync();

            var projectIds = projects.Select(p => p.Id).ToList();

            var squadRolesFlat = await _context.InstituteTemplates
                .Where(t => t.InstituteProjectId != null &&
                            projectIds.Contains(t.InstituteProjectId.Value) &&
                            t.IsActive &&
                            t.SquadId != null)
                .SelectMany(t => _context.Roles
                    .Where(r => r.SquadId == t.SquadId && r.IsActive)
                    .Select(r => new { ProjectId = t.InstituteProjectId!.Value, r.Name, r.Type }))
                .ToListAsync();

            var templateMetadata = await _context.InstituteTemplates
                .Where(t => t.InstituteProjectId != null &&
                            projectIds.Contains(t.InstituteProjectId.Value) &&
                            t.IsActive)
                .Select(t => new
                {
                    ProjectId = t.InstituteProjectId!.Value,
                    RequireDeveloperRule = t.Squad != null && t.Squad.RequireDeveloperRule,
                    t.CourseType,
                    t.RoleCount,
                })
                .ToListAsync();

            var squadRolesByProject = squadRolesFlat
                .GroupBy(r => r.ProjectId)
                .ToDictionary(
                    g => g.Key,
                    g => (IEnumerable<object>)g
                        .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(ng => (object)new { ng.First().Name, ng.First().Type })
                        .ToList()
                );

            var metaByProject = templateMetadata
                .GroupBy(m => m.ProjectId)
                .ToDictionary(g => g.Key, g => g.First());

            return Ok(projects.Select(p =>
            {
                var meta = metaByProject.GetValueOrDefault(p.Id);
                return new
                {
                    p.Id,
                    p.Title,
                    p.Mission,
                    p.OneLiner,
                    p.Description,
                    p.ExtendedDescription,
                    p.ShortBrief,
                    p.Priority,
                    p.IsAvailable,
                    p.InUse,
                    p.Logo,
                    p.BuiltInCourseName,
                    p.CreatedAt,
                    p.UpdatedAt,
                    p.ApplicantsCount,
                    SquadRoles = squadRolesByProject.GetValueOrDefault(p.Id, Enumerable.Empty<object>()),
                    RequireDeveloperRule = meta?.RequireDeveloperRule ?? false,
                    CourseType = meta?.CourseType,
                    RoleCount = meta?.RoleCount,
                };
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving institute projects for institute {InstituteId}", instituteId);
            return StatusCode(500, "An error occurred while retrieving institute projects.");
        }
    }

    /// <summary>
    /// Get all available projects for the given institute.
    /// Built-in catalog rows are always included (even when activated/copied into <c>InstituteProjects</c>).
    /// Route: GET /api/Projects/use/by-institute
    /// </summary>
    [HttpGet("use/by-institute")]
    public async Task<ActionResult<IEnumerable<ProjectDesignsListItemDto>>> GetAvailableProjectsForInstitute()
    {
        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            var iid = instituteId.Value;

            var builtIns = await _context.Projects
                .AsNoTracking()
                .Where(p =>
                    p.InstituteId == null &&
                    p.IsAvailable)
                .OrderBy(p => p.CourseName ?? p.Title)
                .Select(p => new ProjectDesignsListItemDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    CourseName = p.CourseName ?? p.Title,
                    Mission = p.Mission,
                    OneLiner = p.OneLiner,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    ShortBrief = p.ShortBrief,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    InstituteId = p.InstituteId,
                    IsAvailable = p.IsAvailable,
                    InUse = p.InUse,
                    Kickoff = p.Kickoff,
                    CriteriaIds = p.CriteriaIds,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    InstituteProject = false,
                    BaseProjectId = null,
                })
                .ToListAsync();

            var legacyInstitute = await _context.Projects
                .AsNoTracking()
                .Where(p => p.InstituteId == iid)
                .OrderBy(p => p.Title)
                .Select(p => new ProjectDesignsListItemDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Mission = p.Mission,
                    OneLiner = p.OneLiner,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    ShortBrief = p.ShortBrief,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    InstituteId = p.InstituteId,
                    IsAvailable = p.IsAvailable,
                    InUse = p.InUse,
                    Kickoff = p.Kickoff,
                    CriteriaIds = p.CriteriaIds,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    InstituteProject = false,
                    BaseProjectId = null,
                })
                .ToListAsync();

            var fromInstituteTable = await _context.InstituteProjects
                .AsNoTracking()
                .Where(ip => ip.InstituteId == iid)
                .OrderBy(p => p.Title)
                .Select(p => new ProjectDesignsListItemDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    BuiltInCourseName = p.BuiltInCourseName,
                    CourseName = p.BuiltInCourseName ?? p.Title,
                    Mission = p.Mission,
                    OneLiner = p.OneLiner,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    ShortBrief = p.ShortBrief,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    InstituteId = p.InstituteId,
                    IsAvailable = p.IsAvailable,
                    InUse = p.InUse,
                    Kickoff = p.Kickoff,
                    CriteriaIds = p.CriteriaIds,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    InstituteProject = true,
                    BaseProjectId = p.BaseProjectId,
                    Coupon = p.Coupon,
                })
                .ToListAsync();

            var merged = new List<ProjectDesignsListItemDto>(builtIns.Count + legacyInstitute.Count + fromInstituteTable.Count);
            merged.AddRange(builtIns);
            merged.AddRange(legacyInstitute);
            merged.AddRange(fromInstituteTable);
            merged.Sort((a, b) =>
                string.Compare(
                    a.CourseName ?? a.Title,
                    b.CourseName ?? b.Title,
                    StringComparison.OrdinalIgnoreCase));

            return Ok(merged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving institute-scoped projects from auth context");
            return StatusCode(500, "An error occurred while retrieving projects.");
        }
    }

    /// <summary>
    /// Set project <c>InUse</c> flag for the current institute context. Does not delete <see cref="InstituteProjects"/> rows;
    /// when <c>InUse</c> becomes false, institute projects also get <see cref="InstituteProject.IsAvailable"/> set to false (same as institute-owned <see cref="Project"/> rows).
    /// Route: POST /api/Projects/use/by-institute/set-use/{id}
    /// </summary>
    [HttpPost("use/by-institute/set-use/{id:int}")]
    public async Task<ActionResult> SetProjectInUseByInstitute(
        int id,
        [FromBody] SetProjectInUseRequest? request,
        [FromQuery] bool instituteProject = false)
    {
        if (id <= 0)
        {
            return BadRequest("Project id must be a positive integer.");
        }

        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            if (instituteProject)
            {
                var ip = await _context.InstituteProjects.FirstOrDefaultAsync(p => p.Id == id);
                if (ip == null)
                {
                    return NotFound($"Institute project with ID {id} not found.");
                }

                if (ip.InstituteId != instituteId.Value)
                {
                    return Forbid();
                }

                var nextInUseIp = request?.InUse ?? true;
                ip.InUse = nextInUseIp;
                if (!ip.InUse)
                {
                    ip.IsAvailable = false;
                }

                ip.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    id = ip.Id,
                    inUse = ip.InUse,
                    isAvailable = ip.IsAvailable,
                    instituteProject = true,
                });
            }

            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found.");
            }

            if (project.InstituteId.HasValue && project.InstituteId.Value != instituteId.Value)
            {
                return Forbid();
            }

            project.InUse = request?.InUse ?? true;
            // Auto-unpublish only institute-owned projects when they are marked not in use.
            if (!project.InUse && project.InstituteId.HasValue)
            {
                project.IsAvailable = false;
            }
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                id = project.Id,
                inUse = project.InUse,
                isAvailable = project.IsAvailable,
                instituteProject = false,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating InUse for project {ProjectId}", id);
            return StatusCode(500, "An error occurred while updating project in-use flag.");
        }
    }

    /// <summary>
    /// Whether an institute project meets requirements before it can be set Available (IsAvailable=true).
    /// Route: GET /api/Projects/use/by-institute/project-ready-validation?projectId=
    /// </summary>
    [HttpGet("use/by-institute/project-ready-validation")]
    public async Task<ActionResult<ProjectReadyValidationDto>> GetProjectReadyValidation(
        [FromQuery] int projectId,
        [FromQuery] bool instituteProject = false)
    {
        if (projectId <= 0)
        {
            return BadRequest("projectId must be a positive integer.");
        }

        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            if (instituteProject)
            {
                var ipOk = await _context.InstituteProjects.AsNoTracking()
                    .AnyAsync(ip => ip.Id == projectId && ip.InstituteId == instituteId.Value);
                if (!ipOk)
                {
                    return NotFound($"Institute project with ID {projectId} was not found for this institute.");
                }

                return Ok(await BuildProjectReadyValidationForInstituteProjectRowAsync(projectId, instituteId.Value));
            }

            var scoped = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == projectId && (p.InstituteId == null || p.InstituteId == instituteId.Value))
                .Select(p => new { p.Id, p.InstituteId })
                .FirstOrDefaultAsync();

            if (scoped == null)
            {
                return NotFound($"Project with ID {projectId} not found for this institute context.");
            }

            if (scoped.InstituteId == null)
            {
                return Ok(await BuildProjectReadyValidationForCatalogProjectAsync(projectId));
            }

            if (scoped.InstituteId != instituteId.Value)
            {
                return Forbid();
            }

            return Ok(await BuildProjectReadyValidationForInstituteProjectAsync(projectId, instituteId.Value));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in project ready validation for project {ProjectId}", projectId);
            return StatusCode(500, "An error occurred while validating project readiness.");
        }
    }

    /// <summary>
    /// Get Project Designs Header data for the selected project and institute context.
    /// Route: GET /api/Projects/use/by-institute/header/{id}
    /// </summary>
    [HttpGet("use/by-institute/header/{id:int}")]
    public async Task<ActionResult<ProjectHeaderDto>> GetProjectDesignHeader(int id, [FromQuery] bool instituteProject = false)
    {
        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            if (instituteProject)
            {
                var headerIp = await _context.InstituteProjects
                    .AsNoTracking()
                    .Where(p => p.Id == id && p.InstituteId == instituteId.Value)
                    .Select(p => new ProjectHeaderDto
                    {
                        Id = p.Id,
                        Title = p.Title,
                        Logo = p.Logo,
                        Mission = p.Mission,
                        ShortBrief = p.ShortBrief,
                        OneLiner = p.OneLiner,
                        Description = p.Description,
                        CustomerPastStory = p.CustomerPastStory,
                    })
                    .FirstOrDefaultAsync();

                if (headerIp == null)
                {
                    return NotFound($"Institute project with ID {id} not found for institute {instituteId.Value}.");
                }

                return Ok(headerIp);
            }

            var project = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value))
                .Select(p => new ProjectHeaderDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Logo = p.InstituteId == null
                        ? (_context.InstituteProjects
                            .Where(ip => ip.BaseProjectId == p.Id && ip.InstituteId == instituteId.Value && ip.Logo != null && ip.Logo != "")
                            .Select(ip => ip.Logo)
                            .FirstOrDefault()
                           ?? (!string.IsNullOrEmpty(p.Logo) ? p.Logo : (p.Organization != null ? p.Organization.Logo : null)))
                        : p.Logo,
                    Mission = p.Mission,
                    ShortBrief = p.ShortBrief,
                    OneLiner = p.OneLiner,
                    Description = p.Description,
                    CustomerPastStory = p.CustomerPastStory,
                })
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound($"Project with ID {id} not found for institute {instituteId.Value}.");
            }

            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project header for project {ProjectId}", id);
            return StatusCode(500, "An error occurred while retrieving project header.");
        }
    }

    /// <summary>
    /// Word limits for the Institute project design "General" header fields (configurable; see <c>ProjectsInstitute:MaxLengthFields</c>).
    /// Route: GET /api/Projects/use/by-institute/header-field-word-limits
    /// </summary>
    [HttpGet("use/by-institute/header-field-word-limits")]
    public ActionResult<HeaderFieldWordLimitsResponseDto> GetHeaderFieldWordLimits()
    {
        return Ok(new HeaderFieldWordLimitsResponseDto
        {
            ProjectNameWords = EffectiveHeaderProjectNameWords,
            MissionWords = EffectiveHeaderMissionWords,
            OneLinerWords = EffectiveHeaderOneLinerWords,
            ShortBriefWords = EffectiveHeaderShortBriefWords,
        });
    }

    private string BuildProjectHeaderAiSystemInstructions()
    {
        var pn = EffectiveHeaderProjectNameWords;
        var w = "word" + (pn == 1 ? "" : "s");
        return $"""
You are an assistant for teaching staff who are editing a "project design" in an admin tool. Your job is to help with the current step only, using clear, professional language. Match the language of the current fields and the staff member’s request; do not require or prefer English.

**Scope of this request (Header tab)**
- You may work with exactly these items: project name, mission, one-line pitch, and short brief.
- Suggest new text for one or more of these, answer questions, or explain trade-offs, according to the staff member's request and the current values below.
- The design workflow also has other steps that are *not* part of this request: a fuller project description, feature modules, customer story, and so on. Those will have their own assistant features when the user moves to those tabs. Do not ask the user to fill those areas here unless they are directly relevant; if appropriate, you may briefly note in your reply that a longer brief, module breakdown, or customer context can be developed in the next tabs.

**Hard limits (must follow when you propose new text)**
- Project name: at most **{pn}** {w} (whitespace-separated; if the limit is 1, use a single token with no internal spaces).
- Mission: at most **{EffectiveHeaderMissionWords}** words.
- One-line: at most **{EffectiveHeaderOneLinerWords}** words (a single short line; treat as a tagline for browsable listings).
- Short brief: at most **{EffectiveHeaderShortBriefWords}** words.
- Rename rule for copied titles: if the current project name contains "copy" (case-insensitive) and the staff asks for a new name, suggest a genuinely different name (do not return the same base/original name and do not just reword "copy").

**What students see**
- The header content (especially the name, one-line, and short brief) is what students typically see when browsing and selecting a project during registration. Wording should be clear, fair, and appropriate for a student audience.

**Response format (mandatory)**
- Reply with a single JSON object. Do not add markdown, code fences, or any text before or after the JSON.
- Use exactly these keys (strings; use null for "no change"):
  - "assistantMessage" (required, non-empty): a concise reply to the staff member.
  - "suggestedTitle" — new project name, or null if unchanged
  - "suggestedMission" — or null
  - "suggestedOneLiner" — or null
  - "suggestedShortBrief" — or null
- **The product UI copies "suggested*" into the form fields.** If you invent, rewrite, or change any of the name, mission, one-line, or short brief, you **must** put the exact new strings in the corresponding "suggested*" fields. The staff will not get updated fields from "assistantMessage" alone.
- If you only need to give advice with no new text, set all "suggested*" fields to null and put everything in "assistantMessage".
- Proposed "suggested*" text must **already** respect the hard limits above (do not ask the app to trim for you).
- Keep "assistantMessage" short (1-3 sentences).
- When any "suggested*" field is non-null, do not repeat full suggested text inside "assistantMessage"; provide only a brief summary of what changed and why.
""";
    }

    private string BuildProjectHeaderAiUserBlock(
        string? projectName,
        string? mission,
        string? oneLiner,
        string? shortBrief,
        string userRequest)
    {
        string F(string? v) => string.IsNullOrWhiteSpace(v) ? "(empty)" : v.Trim();
        return $"""
**Current project name**
{F(projectName)}

**Current mission**
{F(mission)}

**Current one-line pitch**
{F(oneLiner)}

**Current short brief**
{F(shortBrief)}

**Constraints the platform will enforce (when saving or when applying suggestions)**
- Project name: {EffectiveHeaderProjectNameWords} word(s) max · Mission: {EffectiveHeaderMissionWords} words max · One-line: {EffectiveHeaderOneLinerWords} words max · Short brief: {EffectiveHeaderShortBriefWords} words max

**Staff request**
{userRequest}
""";
    }

    private string BuildProjectBriefAiSystemInstructions()
    {
        return """
You are an assistant for teaching staff editing the Project Designs "Brief" tab.

Scope:
- You may edit only the brief description field for this request.
- The brief is structured as JSON with this shape:
  {"content":[{"type":"heading","text":"..."},{"type":"paragraph","text":"..."}, ...]}
- The UI shows editable heading/body blocks; on save, heading/body pairs are serialized into the structure above.

Behavior:
- Help with direct edits such as: "change paragraph 3", "rewrite heading 2", "shorten paragraph 1", etc.
- If a numbered paragraph/heading is requested, apply the change to that index in the current structured brief.
- The prompt includes both:
  - Original brief description payload (server baseline before local edits)
  - Current brief description payload (latest edited state)
- If the user asks what the original brief was, answer using the original payload from context. Do not say you cannot recall unless the original payload is actually empty/missing.
- Keep assistantMessage concise (1-3 sentences).
- When suggestedDescription is non-null, do not repeat the full rewritten brief in assistantMessage.

Output format (mandatory):
- Return one JSON object only with exactly these keys:
  - "assistantMessage" (string, required)
  - "suggestedDescription" (string or null)
- suggestedDescription must be either:
  - full valid JSON string in the brief structure above, or
  - null when no content change is requested.
""";
    }

    private string BuildProjectBriefAiUserBlock(
        string? originalDescription,
        string? currentDescription,
        string userRequest)
    {
        string F(string? v) => string.IsNullOrWhiteSpace(v) ? "(empty)" : v.Trim();
        return $"""
Original brief description payload (server baseline before local edits):
{F(originalDescription)}

Current brief description payload (JSON string or text):
{F(currentDescription)}

Staff request:
{userRequest}
""";
    }

    private string BuildProjectModulesAiSystemInstructions()
    {
        return """
You are an assistant for teaching staff editing the Project Designs "Modules" tab.

Scope:
- Modules are persisted rows ordered by the integer "sequence" column (1 = first in the list).
- Each module in the snapshots includes:
  - moduleId: database id for that ProjectModules row (omit only for brand-new modules you invent before save).
  - sequence: display order (1-based).
  - title and body (module body text).

Behavior:
- Support targeted edits like "change module 3 title to ...", "rewrite module 2 body ...".
- To reorder (e.g. swap module 1 and 2): return suggestedModules with the same moduleId values but updated sequence values — do not duplicate rows or swap text between unrelated slots unless the user asked for a content swap.
- To remove one or more modules: return suggestedModules as the **complete list of modules that should remain**, each with its **moduleId** from the snapshot. **Omit** removed modules entirely — do not claim you removed a module unless it is absent from suggestedModules and you returned every survivor row.
- For a single-module edit when multiple modules exist, still return **every** surviving module in suggestedModules (same count as before unless removing). Returning only the edited row may be ignored by the client.
- Always include moduleId for every existing module you return in suggestedModules so the client can apply updates without confusing array positions.
- Use uploaded design document context only when it is explicitly provided in the prompt. If it is marked empty/missing, do not assume any design-document content.
- If uploaded design document context is provided and non-empty, you must treat it as available context. Do not claim that it is missing.
- If the request is advisory only, keep suggestedModules null.
- Keep assistantMessage concise (1-3 sentences).
- When suggestedModules is non-null, do not paste full module text into assistantMessage.

Output format (mandatory):
- Return one JSON object only with exactly these keys:
  - "assistantMessage" (string, required)
  - "suggestedModules" (array or null)
- If suggestedModules is an array, each item must include:
  - "moduleId" (int, required for existing modules from the snapshot)
  - "sequence" (int, 1-based display order after this change)
  - "title" (string)
  - "body" (string)
""";
    }

    private string BuildProjectModulesAiUserBlock(
        string? mission,
        string? shortBrief,
        string? description,
        string? uploadedDesignContext,
        List<ProjectModuleAssistantItem>? originalModules,
        List<ProjectModuleAssistantItem>? currentModules,
        string userRequest)
    {
        string F(string? v) => string.IsNullOrWhiteSpace(v) ? "(empty)" : v.Trim();
        var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
        var originalJson = (originalModules != null && originalModules.Count > 0)
            ? JsonSerializer.Serialize(originalModules, jsonOpts)
            : "(empty)";
        var currentJson = (currentModules != null && currentModules.Count > 0)
            ? JsonSerializer.Serialize(currentModules, jsonOpts)
            : "(empty)";

        return $"""
Current project mission:
{F(mission)}

Current short brief:
{F(shortBrief)}

Current brief description:
{F(description)}

Uploaded design document context (present only when upload+parse succeeded):
{F(uploadedDesignContext)}

Original modules snapshot:
{originalJson}

Current modules snapshot:
{currentJson}

Staff request:
{userRequest}
""";
    }

    private string BuildProjectCustomerAiSystemInstructions()
    {
        return """
You are an assistant for teaching staff editing the Project Designs "Customer" tab.

Goal:
- Write a realistic customer backstory that describes life before the proposed solution.
- Voice must be customer-like and non-technical.

Source text (in "Current customer past story" below) may be messy: pasted notes, meeting bullets, or raw text from file extraction—including mixed languages (e.g. Hebrew and English), out-of-order fragments, or duplicates. It is **source material only**, not a draft to return unchanged.

Synthesis rules (non-negotiable):
- **Never** copy, paste, or lightly reorder the "Current customer past story" as your output. **Never** return the same text verbatim or as a string that only fixes spacing.
- **Always** read that field, then **write a new** coherent narrative: flowing paragraphs, clear sequence, one primary language for the backstory. If the source mixes languages, **unify** into a single clear student-facing story (use the same language as the project mission/short brief when that is unambiguous; otherwise use English for the backstory, and do not interleave two languages in the final text unless a single short quote is needed).
- Omit irrelevant boilerplate (headers, page numbers, OCR noise). Merge duplicate points. You may name facts from the source, but the **form** of the backstory must be a polished story, not a transcript.
- If the only appropriate action is a short note (e.g. the source is empty, unreadable, or the user asked a yes/no question), you may return a brief assistantMessage and set suggestedCustomerPastStory to null.

Required content in suggestedCustomerPastStory (when the user wants a full story, rewrite, or expansion):
- A clear "before" narrative (manual steps / current workaround without the solution).
- Practical examples that reference the project modules as needs/problems the customer still struggles with.
- Pain points, delays, risks, errors, frustrations, and business impact.
- Why the customer needs help now (urgency and context), still in plain language.

Length:
- If the user asks for a specific length (e.g. "1000 words", "about 500 words"), the draft in suggestedCustomerPastStory MUST be approximately that length (within about ±10%). Do not reply with a much shorter story when a length is requested.
- If the user asks to expand, lengthen, "write more", or "continue" a customer story, put the complete resulting story in suggestedCustomerPastStory, not in assistantMessage.

Field split (critical for the app UI):
- suggestedCustomerPastStory: the full customer past story text whenever you are writing or updating that story. This is the only field the form reads for the long draft.
- assistantMessage: 1-3 short sentences of acknowledgment, guidance, or clarification only. Never place the long narrative in assistantMessage when you are also providing a story; put the long narrative in suggestedCustomerPastStory.
- If user asks for advice only (no draft), suggestedCustomerPastStory may be null.

Output format (mandatory):
- Return one JSON object only with exactly these keys (no other top-level keys):
  - "assistantMessage" (string, required)
  - "suggestedCustomerPastStory" (string or null)
""";
    }

    private string BuildProjectCustomerAiUserBlock(
        string? mission,
        string? shortBrief,
        string? description,
        string? originalCustomerPastStory,
        string? currentCustomerPastStory,
        List<ProjectModuleAssistantItem>? originalModules,
        List<ProjectModuleAssistantItem>? currentModules,
        string userRequest)
    {
        string F(string? v) => string.IsNullOrWhiteSpace(v) ? "(empty)" : v.Trim();
        var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
        var originalJson = (originalModules != null && originalModules.Count > 0)
            ? JsonSerializer.Serialize(originalModules, jsonOpts)
            : "(empty)";
        var currentJson = (currentModules != null && currentModules.Count > 0)
            ? JsonSerializer.Serialize(currentModules, jsonOpts)
            : "(empty)";

        return $"""
Current project mission:
{F(mission)}

Current short brief:
{F(shortBrief)}

Current brief description:
{F(description)}

Original customer past story (server baseline before local edits):
{F(originalCustomerPastStory)}

Current customer past story (may be raw file extraction, mixed languages, or unstructured—treat as notes; output a new synthesized backstory, do not echo verbatim):
{F(currentCustomerPastStory)}

Original modules snapshot:
{originalJson}

Current modules snapshot:
{currentJson}

Staff request:
{userRequest}
""";
    }

    private static string? ClampStringToMaxLength(string? value, int maxChars)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
        {
            return value;
        }
        return value[..maxChars];
    }

    /// <summary>Trims a string to a maximum number of whitespace-separated words.</summary>
    private static string? ClampToMaxWordsString(string? value, int maxWords)
    {
        if (string.IsNullOrWhiteSpace(value) || maxWords <= 0)
        {
            return null;
        }

        var parts = value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }
        if (parts.Length <= maxWords)
        {
            return string.Join(" ", parts);
        }
        return string.Join(" ", parts.Take(maxWords));
    }

    private void ApplyHeaderFieldWordLimitsToDto(
        ProjectHeaderAiAssistantResponse r,
        bool enforceTitleOneWord,
        bool enforceTextLimits)
    {
        if (r == null) return;
        if (enforceTitleOneWord)
        {
            r.SuggestedTitle = string.IsNullOrWhiteSpace(r.SuggestedTitle)
                ? r.SuggestedTitle
                : ClampStringToMaxLength(ClampToMaxWordsString(r.SuggestedTitle, EffectiveHeaderProjectNameWords), 200);
        }
        if (enforceTextLimits)
        {
            r.SuggestedMission = string.IsNullOrWhiteSpace(r.SuggestedMission)
                ? r.SuggestedMission
                : ClampStringToMaxLength(ClampToMaxWordsString(r.SuggestedMission, EffectiveHeaderMissionWords), 2000);
            r.SuggestedOneLiner = string.IsNullOrWhiteSpace(r.SuggestedOneLiner)
                ? r.SuggestedOneLiner
                : ClampStringToMaxLength(ClampToMaxWordsString(r.SuggestedOneLiner, EffectiveHeaderOneLinerWords), 250);
            r.SuggestedShortBrief = string.IsNullOrWhiteSpace(r.SuggestedShortBrief)
                ? r.SuggestedShortBrief
                : ClampStringToMaxLength(ClampToMaxWordsString(r.SuggestedShortBrief, EffectiveHeaderShortBriefWords), 2000);
        }
    }

    private static bool TryParseProjectHeaderAiJsonResponse(
        string raw,
        out string assistantMessage,
        out (string? Title, string? Mission, string? OneLiner, string? ShortBrief) suggestions)
    {
        assistantMessage = string.Empty;
        suggestions = (null, null, null, null);

        var jsonText = TryExtractJsonObjectString(raw);
        if (string.IsNullOrEmpty(jsonText))
        {
            return false;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            var dto = JsonSerializer.Deserialize<ProjectHeaderAiAssistantJson>(jsonText, options);
            if (dto == null || string.IsNullOrWhiteSpace(dto.AssistantMessage))
            {
                return false;
            }

            assistantMessage = dto.AssistantMessage.Trim();
            suggestions = (dto.SuggestedTitle, dto.SuggestedMission, dto.SuggestedOneLiner, dto.SuggestedShortBrief);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseProjectBriefAiJsonResponse(
        string raw,
        out string assistantMessage,
        out string? suggestedDescription)
    {
        assistantMessage = string.Empty;
        suggestedDescription = null;

        var jsonText = TryExtractJsonObjectString(raw);
        if (string.IsNullOrEmpty(jsonText))
        {
            return false;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            var dto = JsonSerializer.Deserialize<ProjectBriefAiAssistantJson>(jsonText, options);
            if (dto == null || string.IsNullOrWhiteSpace(dto.AssistantMessage))
            {
                return false;
            }

            assistantMessage = dto.AssistantMessage.Trim();
            suggestedDescription = string.IsNullOrWhiteSpace(dto.SuggestedDescription)
                ? null
                : dto.SuggestedDescription.Trim();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseProjectModulesAiJsonResponse(
        string raw,
        out string assistantMessage,
        out List<ProjectModuleAssistantItem>? suggestedModules)
    {
        assistantMessage = string.Empty;
        suggestedModules = null;

        var jsonText = TryExtractJsonObjectString(raw);
        if (string.IsNullOrEmpty(jsonText))
        {
            return false;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            var dto = JsonSerializer.Deserialize<ProjectModulesAiAssistantJson>(jsonText, options);
            if (dto == null || string.IsNullOrWhiteSpace(dto.AssistantMessage))
            {
                return false;
            }

            assistantMessage = dto.AssistantMessage.Trim();
            suggestedModules = dto.SuggestedModules?
                .Where(x => (x?.Sequence ?? 0) > 0)
                .Select(x => new ProjectModuleAssistantItem
                {
                    ModuleId = x!.ModuleId,
                    Sequence = x.Sequence,
                    Title = x.Title?.Trim() ?? string.Empty,
                    Body = x.Body?.Trim() ?? string.Empty,
                })
                .ToList();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseProjectCustomerAiJsonResponse(
        string raw,
        out string assistantMessage,
        out string? suggestedCustomerPastStory)
    {
        assistantMessage = string.Empty;
        suggestedCustomerPastStory = null;

        var jsonText = TryExtractJsonObjectString(raw);
        if (string.IsNullOrEmpty(jsonText))
        {
            return false;
        }

        if (TryDeserializeProjectCustomerAiFromDto(jsonText, out assistantMessage, out suggestedCustomerPastStory))
        {
            return true;
        }

        return TryDeserializeProjectCustomerAiFromJsonObject(jsonText, out assistantMessage, out suggestedCustomerPastStory);
    }

    private static bool TryDeserializeProjectCustomerAiFromDto(
        string jsonText,
        out string assistantMessage,
        out string? suggestedCustomerPastStory)
    {
        assistantMessage = string.Empty;
        suggestedCustomerPastStory = null;

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            var dto = JsonSerializer.Deserialize<ProjectCustomerAiAssistantJson>(jsonText, options);
            if (dto == null || string.IsNullOrWhiteSpace(dto.AssistantMessage))
            {
                return false;
            }

            assistantMessage = dto.AssistantMessage.Trim();
            suggestedCustomerPastStory = string.IsNullOrWhiteSpace(dto.SuggestedCustomerPastStory)
                ? null
                : dto.SuggestedCustomerPastStory.Trim();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Reads customer-assistant keys when the model uses alternate property names or DTO parse fails.</summary>
    private static bool TryDeserializeProjectCustomerAiFromJsonObject(
        string jsonText,
        out string assistantMessage,
        out string? suggestedCustomerPastStory)
    {
        assistantMessage = string.Empty;
        suggestedCustomerPastStory = null;

        try
        {
            if (JsonNode.Parse(jsonText) is not JsonObject root)
            {
                return false;
            }

            var a = GetJsonStringFromObject(
                root,
                "assistantMessage", "assistant_message", "message", "reply", "acknowledgment", "acknowledgement");
            var s = GetJsonStringFromObject(
                root,
                "suggestedCustomerPastStory", "suggested_customer_past_story", "customerPastStory", "customer_past_story",
                "suggestedText", "suggested_text", "story", "narrative", "content", "draft", "backstory", "text");

            if (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(s))
            {
                a = "I've added a draft to the customer past story field below. You can edit it before saving.";
            }

            if (string.IsNullOrWhiteSpace(a))
            {
                return false;
            }

            assistantMessage = a.Trim();
            suggestedCustomerPastStory = string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string? GetJsonStringFromObject(JsonObject root, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (!root.TryGetPropertyValue(name, out var node) || node is null)
            {
                continue;
            }

            try
            {
                switch (node)
                {
                    case JsonValue v:
                        return v.GetValueKind() == JsonValueKind.String
                            ? v.GetValue<string>()
                            : v.ToString();
                    default:
                        // Avoid dumping huge objects into the story field
                        if (node is JsonObject or JsonArray)
                        {
                            return null;
                        }

                        return node.ToString();
                }
            }
            catch (InvalidOperationException)
            {
                // wrong node type; try next key
            }
        }

        return null;
    }

    /// <summary>
    /// If the model put the long story only in assistantMessage, move it to suggestedCustomerPastStory so the form updates.
    /// </summary>
    private static void ApplyProjectCustomerAssistantFieldNormalization(ref string message, ref string? suggested)
    {
        if (!string.IsNullOrWhiteSpace(suggested))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var wordCount = message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount < 50 && message.Length < 500)
        {
            return;
        }

        suggested = message.Trim();
        message = "I've added the full draft to the customer past story field. You can review and edit it there before saving.";
    }

    /// <summary>Finds the outermost JSON object in the model output (strips code fences and prose).</summary>
    private static string? TryExtractJsonObjectString(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        var r = response.Trim();
        // Strip common ```json ... ``` wrapping
        if (r.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = r.IndexOf('\n');
            if (firstLineEnd > 0)
            {
                r = r[(firstLineEnd + 1)..].Trim();
            }
            var fence = r.IndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
            {
                r = r[..fence].Trim();
            }
        }

        var start = r.IndexOf('{');
        var end = r.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return r.Substring(start, end - start + 1);
    }

    /// <summary>
    /// Update Project Designs Header values.
    /// Route: POST /api/Projects/use/by-institute/header/update/{id}
    /// </summary>
    [HttpPost("use/by-institute/header/update/{id:int}")]
    public async Task<ActionResult<ProjectHeaderDto>> UpdateProjectDesignHeader(
        int id,
        [FromBody] UpdateProjectHeaderRequest request,
        [FromQuery] bool instituteProject = false)
    {
        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (instituteProject)
            {
                var ip = await _context.InstituteProjects
                    .FirstOrDefaultAsync(p => p.Id == id && p.InstituteId == instituteId.Value);

                if (ip == null)
                {
                    return NotFound($"Institute project with ID {id} not found for institute {instituteId.Value}.");
                }

                const int titleMaxIp = 200;
                var titleIp = ClampStringToMaxLength(ClampToMaxWordsString(request.Title, EffectiveHeaderProjectNameWords) ?? string.Empty, titleMaxIp);
                if (string.IsNullOrWhiteSpace(titleIp))
                {
                    return BadRequest("Title is required.");
                }

                if (ip.IsAvailable)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Activated projects are read-only.");
                }

                var normalizedTitleIp = NormalizeProjectTitleKey(titleIp);
                var builtInTitlesForValidation = await _context.Projects
                    .AsNoTracking()
                    .Where(p => p.InstituteId == null && p.IsAvailable && p.Title != null)
                    .Select(p => new { p.Id, p.Title })
                    .ToListAsync();
                var hasBuiltInTitleConflict = builtInTitlesForValidation.Any(p =>
                    NormalizeProjectTitleKey(p.Title) == normalizedTitleIp &&
                    !(ip.BaseProjectId.HasValue && p.Id == ip.BaseProjectId.Value));
                if (hasBuiltInTitleConflict)
                {
                    return Conflict("This title is reserved by a built-in catalog project. Please choose a different project name.");
                }

                ip.Title = titleIp;
                ip.Logo = string.IsNullOrWhiteSpace(request.Logo) ? null : request.Logo.Trim();
                ip.Mission = string.IsNullOrWhiteSpace(request.Mission) ? null : ClampStringToMaxLength(ClampToMaxWordsString(request.Mission, EffectiveHeaderMissionWords), 2000);
                ip.ShortBrief = string.IsNullOrWhiteSpace(request.ShortBrief) ? null : ClampStringToMaxLength(ClampToMaxWordsString(request.ShortBrief, EffectiveHeaderShortBriefWords), 2000);
                ip.OneLiner = string.IsNullOrWhiteSpace(request.OneLiner) ? null : ClampStringToMaxLength(ClampToMaxWordsString(request.OneLiner, EffectiveHeaderOneLinerWords), 250);
                ip.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
                ip.CustomerPastStory = string.IsNullOrWhiteSpace(request.CustomerPastStory)
                    ? null
                    : request.CustomerPastStory.Trim();
                ip.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new ProjectHeaderDto
                {
                    Id = ip.Id,
                    Title = ip.Title,
                    Logo = ip.Logo,
                    Mission = ip.Mission,
                    ShortBrief = ip.ShortBrief,
                    OneLiner = ip.OneLiner,
                    Description = ip.Description,
                    CustomerPastStory = ip.CustomerPastStory,
                });
            }

            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value));

            if (project == null)
            {
                return NotFound($"Project with ID {id} not found for institute {instituteId.Value}.");
            }

            if (project.InstituteId == null)
            {
                // Catalog projects: only the logo field is editable
                project.Logo = string.IsNullOrWhiteSpace(request.Logo) ? null : request.Logo.Trim();
                project.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return Ok(new ProjectHeaderDto
                {
                    Id = project.Id,
                    Title = project.Title ?? string.Empty,
                    Logo = project.Logo,
                    Mission = project.Mission,
                    ShortBrief = project.ShortBrief,
                    OneLiner = project.OneLiner,
                    Description = project.Description,
                    CustomerPastStory = project.CustomerPastStory,
                });
            }

            const int titleMax = 200;
            var title = ClampStringToMaxLength(ClampToMaxWordsString(request.Title, EffectiveHeaderProjectNameWords) ?? string.Empty, titleMax);
            if (string.IsNullOrWhiteSpace(title))
            {
                return BadRequest("Title is required.");
            }

            project.Title = title;
            project.Logo = string.IsNullOrWhiteSpace(request.Logo) ? null : request.Logo.Trim();
            project.Mission = string.IsNullOrWhiteSpace(request.Mission) ? null : ClampStringToMaxLength(ClampToMaxWordsString(request.Mission, EffectiveHeaderMissionWords), 2000);
            project.ShortBrief = string.IsNullOrWhiteSpace(request.ShortBrief) ? null : ClampStringToMaxLength(ClampToMaxWordsString(request.ShortBrief, EffectiveHeaderShortBriefWords), 2000);
            project.OneLiner = string.IsNullOrWhiteSpace(request.OneLiner) ? null : ClampStringToMaxLength(ClampToMaxWordsString(request.OneLiner, EffectiveHeaderOneLinerWords), 250);
            project.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            project.CustomerPastStory = string.IsNullOrWhiteSpace(request.CustomerPastStory)
                ? null
                : request.CustomerPastStory.Trim();
            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new ProjectHeaderDto
            {
                Id = project.Id,
                Title = project.Title,
                Logo = project.Logo,
                Mission = project.Mission,
                ShortBrief = project.ShortBrief,
                OneLiner = project.OneLiner,
                Description = project.Description,
                CustomerPastStory = project.CustomerPastStory,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project header for project {ProjectId}", id);
            return StatusCode(500, "An error occurred while updating project header.");
        }
    }

    /// <summary>
    /// Upload a design document for Project Designs (institute scope), extract text, and persist into Project.SystemDesign.
    /// Route: POST /api/Projects/use/by-institute/upload-design/{id}
    /// Accepts: .pdf, .doc, .docx, .ppt, .pptx
    /// </summary>
    [HttpPost("use/by-institute/upload-design/{id:int}")]
    [RequestSizeLimit(52_428_800)]
    public async Task<ActionResult> UploadDesignDocumentByInstitute(
        int id,
        [FromForm] IFormFile? file,
        [FromQuery] bool instituteProject = false,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return BadRequest("Project id must be a positive integer.");
        }
        if (file == null || file.Length <= 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        var ext = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
        if (!IsAllowedDesignUploadExtension(ext))
        {
            return BadRequest("Unsupported file type. Allowed: .pdf, .doc, .docx, .ppt, .pptx");
        }

        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            InstituteProject? ip = null;
            Project? project = null;
            if (instituteProject)
            {
                ip = await _context.InstituteProjects
                    .FirstOrDefaultAsync(p => p.Id == id && p.InstituteId == instituteId.Value, cancellationToken);
                if (ip == null)
                {
                    return NotFound($"Institute project with ID {id} not found for this institute context.");
                }
            }
            else
            {
                project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value), cancellationToken);
                if (project == null)
                {
                    return NotFound($"Project with ID {id} not found for this institute context.");
                }
            }

            await using var memory = new MemoryStream();
            await file.CopyToAsync(memory, cancellationToken);
            var bytes = memory.ToArray();
            if (bytes.Length == 0)
            {
                return BadRequest("Uploaded file is empty.");
            }

            if (!_azureBlobStorage.IsConfigured)
            {
                return StatusCode(500, "Azure Blob Storage is not configured.");
            }

            await using var uploadStream = new MemoryStream(bytes, writable: false);
            var blobUrl = await _azureBlobStorage.UploadResourceBlobAsync(
                uploadStream,
                file.ContentType ?? "application/octet-stream",
                boardId: "project-design-docs",
                studentId: id,
                safeFileName: Path.GetFileName(file.FileName),
                cancellationToken: cancellationToken);
            var blobUri = Uri.TryCreate(blobUrl, UriKind.Absolute, out var tmpBlobUri) ? tmpBlobUri : null;

            var extractedText = await ExtractDesignUploadTextAsync(bytes, file.ContentType, file.FileName, ext, cancellationToken);
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                if (blobUri != null)
                {
                    await _azureBlobStorage.DeleteBlobIfExistsAsync(blobUri, cancellationToken);
                }
                _logger.LogWarning(
                    "UPLOAD_DESIGN_MARKER_v1 parse-failed: ProjectId={ProjectId}, InstituteProject={Ip}, FileName={FileName}, Ext={Ext}",
                    id,
                    instituteProject,
                    file.FileName,
                    ext);
                return BadRequest("The file uploaded successfully, but we could not extract readable text. Please upload a text-based PDF, DOCX, or PPTX.");
            }

            var textOut = extractedText.Trim();
            if (instituteProject && ip != null)
            {
                ip.SystemDesign = textOut;
                ip.SystemDesignDoc = bytes;
                ip.SystemDesignFormatted = $"{UploadedDesignMarker}\nBlobUrl: {blobUrl}\nFileName: {file.FileName}\nUploadedAtUtc: {DateTime.UtcNow:O}";
                ip.UpdatedAt = DateTime.UtcNow;
            }
            else if (project != null)
            {
                project.SystemDesign = textOut;
                project.SystemDesignDoc = bytes;
                project.SystemDesignFormatted = $"{UploadedDesignMarker}\nBlobUrl: {blobUrl}\nFileName: {file.FileName}\nUploadedAtUtc: {DateTime.UtcNow:O}";
                project.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (blobUri != null)
            {
                await _azureBlobStorage.DeleteBlobIfExistsAsync(blobUri, cancellationToken);
            }

            _logger.LogInformation(
                "UPLOAD_DESIGN_MARKER_v1 success: ProjectId={ProjectId}, InstituteProject={Ip}, FileName={FileName}, TextChars={Chars}, BlobUrl={BlobUrl}",
                id,
                instituteProject,
                file.FileName,
                textOut.Length,
                blobUrl);

            return Ok(new
            {
                success = true,
                message = "Design document uploaded, parsed, and saved successfully.\nUse the AI assistant to build your modules—tell it how many modules you want, and it will generate professional titles and module content based on this design.",
                blobUrl,
                parsedChars = textOut.Length,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UPLOAD_DESIGN_MARKER_v1 failed: ProjectId={ProjectId}", id);
            return StatusCode(500, "An error occurred while uploading and processing the design document.");
        }
    }

    /// <summary>
    /// Upload a customer-context document for Project Designs (institute scope), extract text, and persist into Project.CustomerPastStory.
    /// Route: POST /api/Projects/use/by-institute/upload-customer-story/{id}
    /// Accepts: .pdf, .doc, .docx, .ppt, .pptx
    /// </summary>
    [HttpPost("use/by-institute/upload-customer-story/{id:int}")]
    [RequestSizeLimit(52_428_800)]
    public async Task<ActionResult> UploadCustomerStoryDocumentByInstitute(
        int id,
        [FromForm] IFormFile? file,
        [FromQuery] bool instituteProject = false,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return BadRequest("Project id must be a positive integer.");
        }
        if (file == null || file.Length <= 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        var ext = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
        if (!IsAllowedDesignUploadExtension(ext))
        {
            return BadRequest("Unsupported file type. Allowed: .pdf, .doc, .docx, .ppt, .pptx");
        }

        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            InstituteProject? ip = null;
            Project? project = null;
            if (instituteProject)
            {
                ip = await _context.InstituteProjects
                    .FirstOrDefaultAsync(p => p.Id == id && p.InstituteId == instituteId.Value, cancellationToken);
                if (ip == null)
                {
                    return NotFound($"Institute project with ID {id} not found for this institute context.");
                }
            }
            else
            {
                project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value), cancellationToken);
                if (project == null)
                {
                    return NotFound($"Project with ID {id} not found for this institute context.");
                }
            }

            await using var memory = new MemoryStream();
            await file.CopyToAsync(memory, cancellationToken);
            var bytes = memory.ToArray();
            if (bytes.Length == 0)
            {
                return BadRequest("Uploaded file is empty.");
            }

            if (!_azureBlobStorage.IsConfigured)
            {
                return StatusCode(500, "Azure Blob Storage is not configured.");
            }

            await using var uploadStream = new MemoryStream(bytes, writable: false);
            var blobUrl = await _azureBlobStorage.UploadResourceBlobAsync(
                uploadStream,
                file.ContentType ?? "application/octet-stream",
                boardId: "project-customer-story-docs",
                studentId: id,
                safeFileName: Path.GetFileName(file.FileName),
                cancellationToken: cancellationToken);
            var blobUri = Uri.TryCreate(blobUrl, UriKind.Absolute, out var tmpBlobUri) ? tmpBlobUri : null;

            var extractedText = await ExtractDesignUploadTextAsync(bytes, file.ContentType, file.FileName, ext, cancellationToken);
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                if (blobUri != null)
                {
                    await _azureBlobStorage.DeleteBlobIfExistsAsync(blobUri, cancellationToken);
                }
                _logger.LogWarning(
                    "UPLOAD_CUSTOMER_STORY_MARKER_v1 parse-failed: ProjectId={ProjectId}, InstituteProject={Ip}, FileName={FileName}, Ext={Ext}",
                    id,
                    instituteProject,
                    file.FileName,
                    ext);
                return BadRequest("The file uploaded successfully, but we could not extract readable text. Please upload a text-based PDF, DOCX, or PPTX.");
            }

            var storyText = extractedText.Trim();
            if (instituteProject && ip != null)
            {
                ip.CustomerPastStory = storyText;
                ip.UpdatedAt = DateTime.UtcNow;
            }
            else if (project != null)
            {
                project.CustomerPastStory = storyText;
                project.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (blobUri != null)
            {
                await _azureBlobStorage.DeleteBlobIfExistsAsync(blobUri, cancellationToken);
            }

            _logger.LogInformation(
                "UPLOAD_CUSTOMER_STORY_MARKER_v1 success: ProjectId={ProjectId}, InstituteProject={Ip}, FileName={FileName}, TextChars={Chars}, BlobUrl={BlobUrl}",
                id,
                instituteProject,
                file.FileName,
                storyText.Length,
                blobUrl);

            return Ok(new
            {
                success = true,
                message = "Customer context document uploaded and parsed successfully.",
                parsedChars = storyText.Length,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UPLOAD_CUSTOMER_STORY_MARKER_v1 failed: ProjectId={ProjectId}", id);
            return StatusCode(500, "An error occurred while uploading and processing the customer context document.");
        }
    }

    private static bool IsAllowedDesignUploadExtension(string ext) =>
        ext is ".pdf" or ".doc" or ".docx" or ".ppt" or ".pptx";

    private static async Task<string?> ExtractDesignUploadTextAsync(
        byte[] bytes,
        string? contentType,
        string? fileName,
        string ext,
        CancellationToken cancellationToken)
    {
        if (ext == ".pdf")
        {
            return PdfTextExtractor.TryExtractText(bytes, ResourceDocumentContentExtractor.MaxTextCharsInPrompt);
        }

        if (ext == ".docx" || ext == ".pptx")
        {
            await using var ms = new MemoryStream(bytes, writable: false);
            var payload = await ResourceDocumentContentExtractor.BuildAsync(
                ms,
                contentType ?? "application/octet-stream",
                fileName ?? "upload",
                cancellationToken);
            if (string.Equals(payload.Mode, "text", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(payload.TextBody))
            {
                return payload.TextBody;
            }
            return null;
        }

        // Legacy binary formats are allowed, but text extraction is not reliable without conversion.
        return null;
    }

    private static string NormalizeModuleTitle(string? value)
    {
        return string.Join(
            " ",
            (value ?? string.Empty)
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
    }

    private static List<int> ExtractModuleIdsFromTrelloJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<int>();
        }

        var ids = new List<int>();
        var seen = new HashSet<int>();
        foreach (Match m in Regex.Matches(
                     json,
                     @"""(?:Trello)?ModuleId""\s*:\s*(""(?<id>\d+)""|(?<id>\d+))",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (!m.Success)
            {
                continue;
            }

            if (int.TryParse(m.Groups["id"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) &&
                id > 0 &&
                seen.Add(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private async Task<List<InstituteTemplate>> BuildCandidateTemplateRowsForInstituteProjectAsync(
        int instituteId,
        int instituteProjectId,
        int? baseProjectId)
    {
        var candidates = await _context.InstituteTemplates
            .Where(t => t.InstituteId == instituteId &&
                        (
                            t.InstituteProjectId == instituteProjectId ||
                            t.ProjectId == instituteProjectId ||
                            (baseProjectId.HasValue && t.ProjectId == baseProjectId.Value)
                        ))
            .OrderByDescending(t => t.Id)
            .ToListAsync();

        return candidates
            .GroupBy(t => t.Id)
            .Select(g => g.First())
            .ToList();
    }

    private async Task<List<(int Id, string Title, int Sequence)>> ResolveTemplateSourceModulesAsync(InstituteTemplate template)
    {
        if (template.InstituteProjectId is > 0)
        {
            var rows = await _context.InstituteProjectModules
                .AsNoTracking()
                .Where(m => m.InstituteProjectId == template.InstituteProjectId.Value && m.ModuleType != 1)
                .OrderBy(m => m.Sequence ?? int.MaxValue)
                .ThenBy(m => m.Id)
                .ToListAsync();
            return rows
                .Select(m => (m.Id, m.Title ?? string.Empty, m.Sequence ?? int.MaxValue))
                .ToList();
        }

        if (template.ProjectId is > 0)
        {
            var fromProjectRows = await _context.ProjectModules
                .AsNoTracking()
                .Where(m => m.ProjectId == template.ProjectId.Value && m.ModuleType != 1)
                .OrderBy(m => m.Sequence ?? int.MaxValue)
                .ThenBy(m => m.Id)
                .ToListAsync();
            var fromProject = fromProjectRows
                .Select(m => (m.Id, m.Title ?? string.Empty, m.Sequence ?? int.MaxValue))
                .ToList();
            if (fromProject.Count > 0)
            {
                return fromProject;
            }

            // Backward-compatibility: some legacy rows stored InstituteProject id in ProjectId.
            var fallbackRows = await _context.InstituteProjectModules
                .AsNoTracking()
                .Where(m => m.InstituteProjectId == template.ProjectId.Value && m.ModuleType != 1)
                .OrderBy(m => m.Sequence ?? int.MaxValue)
                .ThenBy(m => m.Id)
                .ToListAsync();
            return fallbackRows
                .Select(m => (m.Id, m.Title ?? string.Empty, m.Sequence ?? int.MaxValue))
                .ToList();
        }

        return new List<(int Id, string Title, int Sequence)>();
    }

    /// <summary>
    /// Get template options for the selected project in Project Designs header.
    /// Includes the built-in "System" option plus institute templates for this project.
    /// Route: GET /api/Projects/use/by-institute/templates/{id}
    /// </summary>
    [HttpGet("use/by-institute/templates/{id:int}")]
    public async Task<ActionResult<ProjectHeaderTemplatesResponseDto>> GetProjectDesignTemplates(
        int id,
        [FromQuery] bool instituteProject = false)
    {
        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            if (instituteProject)
            {
                var ipRow = await _context.InstituteProjects
                    .AsNoTracking()
                    .Where(p => p.Id == id && p.InstituteId == instituteId.Value)
                    .Select(p => new { p.Id, p.BaseProjectId, p.BuiltInCourseName, p.TrelloBoardJson })
                    .FirstOrDefaultAsync();

                if (ipRow == null)
                {
                    return NotFound($"Institute project with ID {id} not found for institute {instituteId.Value}.");
                }

                var candidateRows = await BuildCandidateTemplateRowsForInstituteProjectAsync(
                    instituteId.Value,
                    id,
                    ipRow.BaseProjectId);

                var templatesForIp = candidateRows
                    .OrderByDescending(t => t.Id)
                    .Select(t => new TemplateOptionDto
                    {
                        Id = t.Id,
                        Name = t.CourseName,
                        IsActive = t.IsActive,
                    })
                    .ToList();

                var builtInName = string.IsNullOrWhiteSpace(ipRow.BuiltInCourseName)
                    ? "System"
                    : ipRow.BuiltInCourseName.Trim();
                if (!templatesForIp.Any(t => t.Id == 0))
                {
                    templatesForIp.Insert(0, new TemplateOptionDto
                    {
                        Id = 0,
                        Name = builtInName,
                        IsActive = false,
                    });
                }

                var firstExplicitActiveIp = templatesForIp.FirstOrDefault(t => t.Id > 0 && t.IsActive);
                var hasAssignedCourseJson = !string.IsNullOrWhiteSpace(ipRow.TrelloBoardJson);
                int activeIdIp;
                string activeNameIp;
                if (hasAssignedCourseJson && firstExplicitActiveIp != null)
                {
                    activeIdIp = firstExplicitActiveIp.Id;
                    activeNameIp = firstExplicitActiveIp.Name ?? builtInName;
                }
                else if (hasAssignedCourseJson)
                {
                    activeIdIp = 0;
                    activeNameIp = builtInName;
                }
                else
                {
                    activeIdIp = 0;
                    activeNameIp = "No course selected";
                }

                for (var i = 0; i < templatesForIp.Count; i++)
                {
                    templatesForIp[i].IsActive = templatesForIp[i].Id > 0
                        ? (hasAssignedCourseJson && templatesForIp[i].Id == activeIdIp)
                        : (activeIdIp == 0 && hasAssignedCourseJson);
                }

                return Ok(new ProjectHeaderTemplatesResponseDto
                {
                    Templates = templatesForIp,
                    ActiveTemplateId = activeIdIp,
                    ActiveTemplateName = activeNameIp,
                });
            }

            var project = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value))
                .Select(p => new { p.Id, p.InstituteId, p.TrelloBoardJson, p.Title, p.CourseName, p.OrganizationId })
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound($"Project with ID {id} not found for institute {instituteId.Value}.");
            }

            var isSystemProject = project.InstituteId == null;

            List<TemplateOptionDto> instituteTemplates;
            if (isSystemProject)
            {
                var instituteProjectIdsForBase = await _context.InstituteProjects
                    .AsNoTracking()
                    .Where(ip => ip.InstituteId == instituteId.Value && ip.BaseProjectId == id)
                    .Select(ip => ip.Id)
                    .ToListAsync();

                var templateEntities = await _context.InstituteTemplates
                    .AsNoTracking()
                    .Where(t =>
                        t.InstituteId == instituteId.Value
                        && (
                            t.ProjectId == id
                            || (t.InstituteProjectId != null && instituteProjectIdsForBase.Contains(t.InstituteProjectId.Value))))
                    .OrderBy(t => t.Id)
                    .ToListAsync();

                instituteTemplates = templateEntities
                    .GroupBy(t => t.Id)
                    .Select(g => g.First())
                    .OrderBy(t => t.Id)
                    .Select(t => new TemplateOptionDto
                    {
                        Id = t.Id,
                        Name = t.CourseName,
                        IsActive = t.IsActive,
                    })
                    .ToList();
            }
            else
            {
                instituteTemplates = await _context.InstituteTemplates
                    .AsNoTracking()
                    .Where(t => t.InstituteId == instituteId.Value && t.ProjectId == id)
                    .OrderBy(t => t.Id)
                    .Select(t => new TemplateOptionDto
                    {
                        Id = t.Id,
                        Name = t.CourseName,
                        IsActive = t.IsActive,
                    })
                    .ToListAsync();
            }

            var hasSystemTemplate = !string.IsNullOrWhiteSpace(project.TrelloBoardJson);
            if (isSystemProject)
            {
                var firstActiveSystem = instituteTemplates.FirstOrDefault(t => t.IsActive);
                var courseNameSystem = string.IsNullOrWhiteSpace(project.CourseName)
                    ? "System"
                    : project.CourseName.Trim();
                var templates = new List<TemplateOptionDto>
                {
                    new()
                    {
                        Id = 0,
                        Name = courseNameSystem,
                        IsActive = firstActiveSystem == null,
                    },
                };
                templates.AddRange(instituteTemplates);

                return Ok(new ProjectHeaderTemplatesResponseDto
                {
                    Templates = templates,
                    ActiveTemplateId = firstActiveSystem?.Id ?? 0,
                    ActiveTemplateName = firstActiveSystem?.Name ?? courseNameSystem,
                });
            }

            // Prefer an explicitly active row; if none, use the built-in board (id 0) when the project has Trello JSON;
            // otherwise the first custom row when the built-in is unavailable.
            var firstExplicitActive = instituteTemplates.FirstOrDefault(t => t.IsActive);
            int activeId;
            string activeName;
            if (firstExplicitActive != null)
            {
                activeId = firstExplicitActive.Id;
                activeName = firstExplicitActive.Name ?? "System";
            }
            else if (hasSystemTemplate)
            {
                activeId = 0;
                activeName = "System";
            }
            else if (instituteTemplates.Count > 0)
            {
                var firstT = instituteTemplates[0];
                activeId = firstT.Id;
                activeName = firstT.Name;
            }
            else
            {
                activeId = 0;
                activeName = string.Empty;
            }

            List<TemplateOptionDto> templatesForProject;
            if (hasSystemTemplate)
            {
                templatesForProject = new List<TemplateOptionDto>
                {
                    new() { Id = 0, Name = "System", IsActive = activeId == 0 }
                };
                foreach (var t in instituteTemplates)
                {
                    templatesForProject.Add(new TemplateOptionDto
                    {
                        Id = t.Id,
                        Name = t.Name,
                        IsActive = t.Id == activeId
                    });
                }
            }
            else
            {
                templatesForProject = instituteTemplates
                    .Select(t => new TemplateOptionDto
                    {
                        Id = t.Id,
                        Name = t.Name,
                        IsActive = t.Id == activeId
                    })
                    .ToList();
            }

            return Ok(new ProjectHeaderTemplatesResponseDto
            {
                Templates = templatesForProject,
                ActiveTemplateId = activeId,
                ActiveTemplateName = activeName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving templates for project {ProjectId}", id);
            return StatusCode(500, "An error occurred while retrieving templates.");
        }
    }

    /// <summary>
    /// Set active template for Project Designs header template combo.
    /// Route: POST /api/Projects/use/by-institute/set-active/{id}/{templateId}
    /// </summary>
    [HttpPost("use/by-institute/set-active/{id:int}/{templateId:int}")]
    public async Task<ActionResult<object>> SetProjectDesignActiveTemplate(
        int id,
        int templateId,
        [FromQuery] bool instituteProject = false)
    {
        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            if (instituteProject)
            {
                var ipMeta = await _context.InstituteProjects
                    .AsTracking()
                    .Where(p => p.Id == id && p.InstituteId == instituteId.Value)
                    .FirstOrDefaultAsync();

                if (ipMeta == null)
                {
                    return NotFound($"Institute project with ID {id} not found for institute {instituteId.Value}.");
                }

                var targetModules = await _context.InstituteProjectModules
                    .AsNoTracking()
                    .Where(m => m.InstituteProjectId == id && m.ModuleType != 1)
                    .OrderBy(m => m.Sequence ?? int.MaxValue)
                    .ThenBy(m => m.Id)
                    .Select(m => new { m.Id, m.Title, m.OriginalModuleId })
                    .ToListAsync();

                var candidateRows = await BuildCandidateTemplateRowsForInstituteProjectAsync(
                    instituteId.Value,
                    id,
                    ipMeta.BaseProjectId);
                var selectedTemplateIp = templateId == 0
                    ? null
                    : candidateRows.FirstOrDefault(t => t.Id == templateId);
                async Task<ActionResult<object>> UnassignCourseWithValidationResult(
                    string message,
                    string? diagnosticDetail = null)
                {
                    _logger.LogWarning(
                        "SetActiveTemplate: course assignment NOT applied (returning CourseAssignmentRemoved). InstituteProjectId={InstituteProjectId}, TemplateId={TemplateId}, BaseProjectId={BaseProjectId}, InstituteId={InstituteId}. UserMessage={UserMessage}. Diagnostic={Diagnostic}",
                        ipMeta.Id,
                        templateId,
                        ipMeta.BaseProjectId,
                        instituteId.Value,
                        message,
                        diagnosticDetail ?? string.Empty);

                    ipMeta.TrelloBoardJson = null;
                    ipMeta.UpdatedAt = DateTime.UtcNow;
                    foreach (var template in candidateRows)
                    {
                        template.IsActive = false;
                    }

                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        Success = false,
                        CourseAssignmentRemoved = true,
                        ValidationMessage = message,
                        ActiveTemplateId = 0,
                        ActiveTemplateName = "No course selected",
                    });
                }
                if (templateId < 0)
                {
                    ipMeta.TrelloBoardJson = null;
                    ipMeta.UpdatedAt = DateTime.UtcNow;
                    foreach (var template in candidateRows)
                    {
                        template.IsActive = false;
                    }

                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        Success = true,
                        ActiveTemplateId = 0,
                        ActiveTemplateName = "No course selected",
                    });
                }

                if (templateId != 0 && selectedTemplateIp == null)
                {
                    _logger.LogWarning(
                        "SetActiveTemplate: template id not found in candidate rows. InstituteProjectId={InstituteProjectId}, TemplateId={TemplateId}, CandidateCount={CandidateCount}",
                        ipMeta.Id,
                        templateId,
                        candidateRows.Count);
                    return NotFound($"Template with ID {templateId} not found for this project and institute.");
                }

                string sourceJson;
                List<(int Id, string Title, int Sequence)> sourceModules;
                string activeCourseName;
                if (templateId == 0)
                {
                    if (!ipMeta.BaseProjectId.HasValue || ipMeta.BaseProjectId.Value <= 0)
                    {
                        _logger.LogWarning(
                            "SetActiveTemplate: institute row has no BaseProjectId for built-in course source. InstituteProjectId={InstituteProjectId}",
                            ipMeta.Id);
                        return BadRequest("This project does not have a built-in course source.");
                    }

                    var baseProject = await _context.Projects
                        .AsNoTracking()
                        .Where(p => p.Id == ipMeta.BaseProjectId.Value)
                        .Select(p => new { p.TrelloBoardJson, p.CourseName })
                        .FirstOrDefaultAsync();
                    if (baseProject == null || string.IsNullOrWhiteSpace(baseProject.TrelloBoardJson))
                    {
                        _logger.LogWarning(
                            "SetActiveTemplate: catalog project has no TrelloBoardJson for built-in source. InstituteProjectId={InstituteProjectId}, BaseProjectId={BaseProjectId}",
                            ipMeta.Id,
                            ipMeta.BaseProjectId.Value);
                        return BadRequest("Built-in course is not available for this project.");
                    }

                    sourceJson = baseProject.TrelloBoardJson!;
                    activeCourseName = string.IsNullOrWhiteSpace(ipMeta.BuiltInCourseName)
                        ? (string.IsNullOrWhiteSpace(baseProject.CourseName) ? "System" : baseProject.CourseName!.Trim())
                        : ipMeta.BuiltInCourseName.Trim();
                    var sourceModuleRows = await _context.ProjectModules
                        .AsNoTracking()
                        .Where(m => m.ProjectId == ipMeta.BaseProjectId.Value && m.ModuleType != 1)
                        .OrderBy(m => m.Sequence ?? int.MaxValue)
                        .ThenBy(m => m.Id)
                        .ToListAsync();
                    sourceModules = sourceModuleRows
                        .Select(m => (m.Id, m.Title ?? string.Empty, m.Sequence ?? int.MaxValue))
                        .ToList();
                }
                else
                {
                    sourceJson = selectedTemplateIp!.TrelloBoardJson ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(sourceJson))
                    {
                        _logger.LogWarning(
                            "SetActiveTemplate: InstituteTemplates row has empty TrelloBoardJson. InstituteProjectId={InstituteProjectId}, InstituteTemplateId={InstituteTemplateId}",
                            ipMeta.Id,
                            selectedTemplateIp.Id);
                        return BadRequest("Selected course does not contain a valid board definition.");
                    }

                    activeCourseName = selectedTemplateIp.CourseName;
                    sourceModules = await ResolveTemplateSourceModulesAsync(selectedTemplateIp);
                }

                var sourceModuleIds = ExtractModuleIdsFromTrelloJson(sourceJson);
                _logger.LogInformation(
                    "SetActiveTemplate validation start: InstituteProjectId={InstituteProjectId}, TemplateId={TemplateId}, BaseProjectId={BaseProjectId}, SourceModuleIdsCount={SourceCount}, SourceModuleIds=[{SourceIds}]",
                    ipMeta.Id,
                    templateId,
                    ipMeta.BaseProjectId,
                    sourceModuleIds.Count,
                    string.Join(", ", sourceModuleIds));
                if (sourceModuleIds.Count == 0)
                {
                    var jsonLen = sourceJson?.Length ?? 0;
                    return await UnassignCourseWithValidationResult(
                        "The selected course could not be applied because its modules do not match this project.",
                        $"No module ids extracted from course Trello JSON (jsonLength={jsonLen}, courseSource={(templateId == 0 ? "built-in Project.TrelloBoardJson" : $"InstituteTemplates.Id={templateId}")}).");
                }

                var sourceById = sourceModules.ToDictionary(m => m.Id, m => m);
                var sourceStructure = new List<(int Id, string NormTitle)>();
                foreach (var moduleId in sourceModuleIds)
                {
                    if (!sourceById.TryGetValue(moduleId, out var sourceModule))
                    {
                        // Module id is in the Trello JSON but not in the filtered source list.
                        // This is expected for ModuleType=1 modules (e.g. kickoff/intro) which are
                        // intentionally excluded from matching — skip them silently.
                        _logger.LogInformation(
                            "SetActiveTemplate: skipping module id {ModuleId} — not in filtered source list (likely ModuleType=1). courseSource={CourseSource}",
                            moduleId,
                            templateId == 0 ? $"ProjectModules for BaseProjectId={ipMeta.BaseProjectId}" : $"InstituteTemplates.Id={templateId}");
                        continue;
                    }

                    sourceStructure.Add((moduleId, NormalizeModuleTitle(sourceModule.Title)));
                }

                // Validation rule: module-name set compatibility only (ignore order and duplicates).
                var selectedProjectNameSet = targetModules
                    .Select(m => NormalizeModuleTitle(m.Title))
                    .ToHashSet(StringComparer.Ordinal);
                var selectedCourseNameSet = sourceStructure
                    .Select(s => s.NormTitle)
                    .ToHashSet(StringComparer.Ordinal);

                var nameMismatch = !selectedProjectNameSet.SetEquals(selectedCourseNameSet);
                _logger.LogInformation(
                    "SetActiveTemplate validation compare: InstituteProjectId={InstituteProjectId}, TemplateId={TemplateId}, NameMismatch={NameMismatch}, ProjectNames=[{ProjectNames}], CourseNames=[{CourseNames}], ProjectModules=[{ProjectModules}], CourseModules=[{CourseModules}]",
                    ipMeta.Id,
                    templateId,
                    nameMismatch,
                    string.Join(", ", selectedProjectNameSet.OrderBy(x => x)),
                    string.Join(", ", selectedCourseNameSet.OrderBy(x => x)),
                    string.Join(" | ", targetModules.Select(m => $"{m.Id}:{NormalizeModuleTitle(m.Title)}")),
                    string.Join(" | ", sourceStructure.Select(s => $"{s.Id}:{s.NormTitle}")));
                if (nameMismatch)
                {
                    var onlyInProject = selectedProjectNameSet.Except(selectedCourseNameSet, StringComparer.Ordinal).OrderBy(x => x).ToList();
                    var onlyInCourse = selectedCourseNameSet.Except(selectedProjectNameSet, StringComparer.Ordinal).OrderBy(x => x).ToList();
                    var projectRawTitles = string.Join(" | ", targetModules.Select(m => $"{m.Id}:\"{m.Title ?? ""}\""));
                    var courseRawTitles = string.Join(
                        " | ",
                        sourceStructure.Select(s =>
                        {
                            var title = sourceById.TryGetValue(s.Id, out var sm) ? (sm.Title ?? string.Empty) : "?";
                            return $"{s.Id}:\"{title}\"->{s.NormTitle}";
                        }));
                    return await UnassignCourseWithValidationResult(
                        "The selected course could not be applied because its modules do not match this project.",
                        $"Module title set mismatch. Project modules use ModuleType!=1; course module list comes from ResolveTemplateSourceModulesAsync (may include all types). Normalized onlyInProject=[{string.Join(", ", onlyInProject)}], onlyInCourse=[{string.Join(", ", onlyInCourse)}]. Raw project modules: {projectRawTitles}. Raw course modules (id->title->norm): {courseRawTitles}. courseSource={(templateId == 0 ? $"built-in ProjectModules BaseProjectId={ipMeta.BaseProjectId}" : $"InstituteTemplates.Id={templateId}")}.");
                }

                // Build remap by matched normalized names (counts already validated above).
                var targetIdsByName = targetModules
                    .GroupBy(m => NormalizeModuleTitle(m.Title), StringComparer.Ordinal)
                    .ToDictionary(
                        g => g.Key,
                        g => new Queue<int>(g.Select(x => x.Id)),
                        StringComparer.Ordinal);
                var moduleMap = new Dictionary<int, int>();
                foreach (var source in sourceStructure)
                {
                    if (!targetIdsByName.TryGetValue(source.NormTitle, out var q) || q.Count == 0)
                    {
                        return await UnassignCourseWithValidationResult(
                            "The selected course could not be applied because its modules do not match this project.",
                            $"Remap dequeue failed: no InstituteProjectModule available for normalized title \"{source.NormTitle}\" (source module id {source.Id}).");
                    }
                    moduleMap[source.Id] = q.Dequeue();
                }
                _logger.LogInformation(
                    "SetActiveTemplate module remap ready: InstituteProjectId={InstituteProjectId}, TemplateId={TemplateId}, ModuleMap={ModuleMap}",
                    ipMeta.Id,
                    templateId,
                    FormatModuleIdMapForLog(moduleMap));

                var remappedJson = TryRemapTrelloJsonAfterProjectDuplicate(
                    sourceJson,
                    moduleMap,
                    sourceProjectId: templateId == 0 ? ipMeta.BaseProjectId!.Value : id,
                    newProjectId: ipMeta.Id);
                if (string.IsNullOrWhiteSpace(remappedJson))
                {
                    return await UnassignCourseWithValidationResult(
                        "The selected course could not be applied because its board definition is invalid.",
                        $"TryRemapTrelloJsonAfterProjectDuplicate returned empty. sourceProjectId={(templateId == 0 ? ipMeta.BaseProjectId!.Value : id)}, newProjectId={ipMeta.Id}, moduleMap={FormatModuleIdMapForLog(moduleMap)}.");
                }

                ipMeta.TrelloBoardJson = remappedJson;
                ipMeta.UpdatedAt = DateTime.UtcNow;
                foreach (var template in candidateRows)
                {
                    template.IsActive = selectedTemplateIp != null && template.Id == selectedTemplateIp.Id;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Success = true,
                    ActiveTemplateId = selectedTemplateIp?.Id ?? 0,
                    ActiveTemplateName = string.IsNullOrWhiteSpace(activeCourseName) ? "System" : activeCourseName,
                });
            }

            var project = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value))
                .Select(p => new { p.Id, p.InstituteId, p.TrelloBoardJson, p.Title })
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound($"Project with ID {id} not found for institute {instituteId.Value}.");
            }

            var isSystemProject = project.InstituteId == null;
            if (isSystemProject)
            {
                if (templateId < 0)
                {
                    return BadRequest("Built-in projects must keep a course assigned.");
                }

                var builtInTitleKey = NormalizeProjectTitleKey(project.Title);
                var existingBuiltInCandidates = await _context.InstituteProjects
                    .AsTracking()
                    .Where(ip =>
                        ip.InstituteId == instituteId.Value &&
                        ip.BaseProjectId == id &&
                        ip.IsBuiltIn)
                    .ToListAsync();
                var existingIp = existingBuiltInCandidates
                    .FirstOrDefault(ip => NormalizeProjectTitleKey(ip.Title) == builtInTitleKey);

                if (existingIp == null)
                {
                    var sourceProject = await _context.Projects
                        .AsNoTracking()
                        .Include(p => p.Organization)
                        .FirstOrDefaultAsync(p => p.Id == id && p.InstituteId == null && p.IsAvailable);
                    if (sourceProject == null)
                    {
                        return NotFound($"Catalog project with ID {id} was not found or is not available.");
                    }

                    existingIp = InstituteProjectMapper.CopyFromProject(sourceProject, instituteId.Value, id);
                    existingIp.IsAvailable = false;
                    existingIp.InUse = true;
                    existingIp.IsBuiltIn = true;
                    _context.InstituteProjects.Add(existingIp);
                    await _context.SaveChangesAsync();

                    var sourceModules = await _context.ProjectModules
                        .AsNoTracking()
                        .Where(pm => pm.ProjectId == id)
                        .OrderBy(pm => pm.Sequence ?? int.MaxValue)
                        .ThenBy(pm => pm.Id)
                        .ToListAsync();
                    if (sourceModules.Count > 0)
                    {
                        _context.InstituteProjectModules.AddRange(sourceModules.Select(pm => new InstituteProjectModule
                        {
                            InstituteProjectId = existingIp.Id,
                            ModuleType = pm.ModuleType,
                            Title = pm.Title,
                            Description = pm.Description,
                            Sequence = pm.Sequence,
                            OriginalModuleId = pm.Id,
                        }));
                        await _context.SaveChangesAsync();
                    }
                }

                _logger.LogInformation(
                    "SetActiveTemplate catalog built-in: validating via institute mirror row CatalogProjectId={CatalogProjectId}, InstituteProjectId={InstituteProjectId}, TemplateId={TemplateId}",
                    id,
                    existingIp.Id,
                    templateId);
                return await SetProjectDesignActiveTemplate(existingIp.Id, templateId, true);
            }

            var hasSystemTemplate = !string.IsNullOrWhiteSpace(project.TrelloBoardJson);
            if (templateId == 0 && !isSystemProject && !hasSystemTemplate)
            {
                return BadRequest("System template is not available for this project.");
            }

            var templates = await _context.InstituteTemplates
                .Where(t => t.InstituteId == instituteId.Value && t.ProjectId == id)
                .ToListAsync();

            InstituteTemplate? selectedTemplate = null;
            if (templateId != 0)
            {
                selectedTemplate = templates.FirstOrDefault(t => t.Id == templateId);
                if (selectedTemplate == null)
                {
                    return NotFound($"Template with ID {templateId} not found for this project and institute.");
                }
            }

            foreach (var template in templates)
            {
                template.IsActive = selectedTemplate != null && template.Id == selectedTemplate.Id;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                ActiveTemplateId = selectedTemplate?.Id ?? 0,
                ActiveTemplateName = selectedTemplate?.CourseName ?? "System",
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting active template for project {ProjectId}, template {TemplateId}", id, templateId);
            return StatusCode(500, "An error occurred while setting active template.");
        }
    }

    /// <summary>
    /// List saved institute templates for a project (names + ids for Task Builder picker).
    /// </summary>
    [HttpGet("use/by-institute/templates/list")]
    public async Task<ActionResult<IEnumerable<InstituteTemplateListItemDto>>> GetInstituteTemplatesListForProject(
        [FromQuery] int projectId,
        [FromQuery] int instituteId,
        [FromQuery] bool instituteProject = false)
    {
        if (projectId <= 0 || instituteId <= 0)
        {
            return BadRequest("projectId and instituteId must be positive integers.");
        }

        try
        {
            var items = await _context.InstituteTemplates
                .AsNoTracking()
                .Where(t =>
                    t.InstituteId == instituteId &&
                    (instituteProject ? t.InstituteProjectId == projectId : t.ProjectId == projectId))
                .OrderByDescending(t => t.Id)
                .Select(t => new InstituteTemplateListItemDto
                {
                    Id = t.Id,
                    Name = t.CourseName,
                    BoardUrl = t.BoardUrl,
                    IsActive = t.IsActive,
                    CourseType = t.CourseType,
                    RoleCount = t.RoleCount,
                })
                .ToListAsync();

            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing institute templates for project {ProjectId}", projectId);
            return StatusCode(500, "An error occurred while listing institute templates.");
        }
    }

    /// <summary>
    /// Catalog rows for Courses screen.
    /// Returns institute templates (same institute), and optionally built-in project templates (Projects table).
    /// </summary>
    [HttpGet("use/by-institute/templates/catalog")]
    public async Task<ActionResult<IEnumerable<CourseCatalogItemDto>>> GetCoursesCatalog(
        [FromQuery] int instituteId,
        [FromQuery] bool includeBuiltIn = false)
    {
        if (instituteId <= 0)
            return BadRequest("instituteId must be a positive integer.");

        try
        {
            var instituteExists = await _context.Institutes.AsNoTracking().AnyAsync(i => i.Id == instituteId);
            if (!instituteExists)
                return NotFound($"Institute with ID {instituteId} not found.");

            var instituteRows = await _context.InstituteTemplates
                .AsNoTracking()
                .Where(t =>
                    t.InstituteId == instituteId
                    && (t.ProjectId != null || t.InstituteProjectId != null))
                .Select(t => new CourseCatalogItemDto
                {
                    Kind = "institute",
                    ProjectId = t.ProjectId ?? t.InstituteProjectId ?? 0,
                    InstituteTemplateId = t.Id,
                    Name = string.IsNullOrWhiteSpace(t.CourseName) ? $"Course {t.Id}" : t.CourseName,
                    ProjectTitle = t.InstituteProjectId != null
                        ? t.InstituteProject!.Title
                        : t.Project!.Title,
                    BoardUrl = t.BoardUrl,
                    InstituteProjectDesign = t.InstituteProjectId != null,
                    InstituteProjectIsBuiltIn = t.InstituteProjectId != null && t.InstituteProject!.IsBuiltIn,
                    InUse = t.InstituteProjectId != null ? t.InstituteProject!.InUse : t.Project!.InUse,
                    IsActive = t.IsActive,
                    CourseType = t.CourseType,
                    RoleCount = t.RoleCount,
                    ProjectLogo = t.InstituteProjectId != null
                        ? (t.InstituteProject!.Logo ?? t.InstituteProject!.BaseProject!.Logo)
                        : t.Project!.Logo,
                })
                .OrderByDescending(x => x.InstituteTemplateId)
                .ToListAsync();

            foreach (var row in instituteRows)
            {
                var v = row.InstituteProjectDesign
                    ? await BuildProjectReadyValidationForInstituteProjectRowAsync(row.ProjectId, instituteId)
                    : await BuildProjectReadyValidationForInstituteProjectAsync(row.ProjectId, instituteId);
                row.IsReady = v.IsReady;
            }

            if (!includeBuiltIn)
                return Ok(instituteRows);

            var builtInRows = await _context.Projects
                .AsNoTracking()
                .Where(p =>
                    p.IsAvailable &&
                    !string.IsNullOrWhiteSpace(p.TrelloBoardJson) &&
                    (p.InstituteId == null || p.InstituteId == instituteId))
                .OrderBy(p => p.CourseName ?? p.Title)
                .Select(p => new CourseCatalogItemDto
                {
                    Kind = "builtIn",
                    ProjectId = p.Id,
                    InstituteTemplateId = null,
                    Name = p.CourseName ?? p.Title,
                    ProjectTitle = p.Title,
                    InUse = p.InUse,
                    ProjectLogo = p.Logo,
                })
                .ToListAsync();

            foreach (var row in builtInRows)
            {
                var v = await BuildProjectReadyValidationForCatalogProjectAsync(row.ProjectId);
                row.IsReady = v.IsReady;
            }

            return Ok(builtInRows.Concat(instituteRows).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building courses catalog for institute {InstituteId}", instituteId);
            return StatusCode(500, "An error occurred while loading courses catalog.");
        }
    }

    /// <summary>
    /// Project picker for Courses &quot;Create Course&quot;: institute-owned projects with <see cref="Project.InUse"/> /
    /// <see cref="InstituteProject.InUse"/> that pass project-ready validation (not in &quot;Pending&quot;).
    /// Rows where <see cref="InstituteProject.IsBuiltIn"/> is true (activated catalog copies in <c>InstituteProjects</c>)
    /// are never returned — use global catalog rows when <paramref name="includeBuiltIn"/> is true. With
    /// <paramref name="includeBuiltIn"/> false, the list is institute non-built-in mirrors and legacy institute projects only.
    /// When <paramref name="includeBuiltIn"/> is true, also includes global catalog projects
    /// (<c>InstituteId</c> is null) that are <see cref="Project.InUse"/>, <see cref="Project.IsAvailable"/>, have a saved
    /// Trello template JSON, and pass catalog readiness (same notion as catalog built-ins).
    /// </summary>
    /// <remarks>
    /// Exposed twice so proxies or older clients can call either route:
    /// <c>GET /api/Projects/use/for-course-create</c> or
    /// <c>GET /api/Projects/use/by-institute/for-course-create</c> (same query params).
    /// </remarks>
    [HttpGet("use/for-course-create")]
    public Task<ActionResult<IEnumerable<CourseCreateProjectOptionDto>>> GetProjectsForCourseCreateLegacy(
        [FromQuery] int instituteId,
        [FromQuery] bool includeBuiltIn = false)
        => GetProjectsForCourseCreateImplAsync(instituteId, includeBuiltIn);

    /// <inheritdoc cref="GetProjectsForCourseCreateLegacy"/>
    [HttpGet("use/by-institute/for-course-create")]
    public Task<ActionResult<IEnumerable<CourseCreateProjectOptionDto>>> GetProjectsForCourseCreateByInstitute(
        [FromQuery] int instituteId,
        [FromQuery] bool includeBuiltIn = false)
        => GetProjectsForCourseCreateImplAsync(instituteId, includeBuiltIn);

    private async Task<ActionResult<IEnumerable<CourseCreateProjectOptionDto>>> GetProjectsForCourseCreateImplAsync(
        int instituteId,
        bool includeBuiltIn)
    {
        if (instituteId <= 0)
            return BadRequest("instituteId must be a positive integer.");

        try
        {
            var instituteExists = await _context.Institutes.AsNoTracking().AnyAsync(i => i.Id == instituteId);
            if (!instituteExists)
                return NotFound($"Institute with ID {instituteId} was not found.");

            var fromInstituteProjectsTable = await _context.InstituteProjects
                .AsNoTracking()
                .Where(ip => ip.InstituteId == instituteId && ip.InUse)
                .OrderBy(ip => ip.Title)
                .Select(ip => new CourseCreateProjectOptionDto
                {
                    Id = ip.Id,
                    Title = ip.Title ?? string.Empty,
                    InstituteId = instituteId,
                    IsBuiltIn = ip.IsBuiltIn,
                    IsInstituteProject = true,
                })
                .ToListAsync();

            var legacyInstituteProjects = await _context.Projects
                .AsNoTracking()
                .Where(p => p.InstituteId == instituteId && p.InUse)
                .OrderBy(p => p.Title)
                .Select(p => new CourseCreateProjectOptionDto
                {
                    Id = p.Id,
                    Title = p.Title ?? string.Empty,
                    InstituteId = p.InstituteId,
                    IsBuiltIn = false,
                    IsInstituteProject = false,
                })
                .ToListAsync();

            var readyInstituteProjectsTable = new List<CourseCreateProjectOptionDto>(fromInstituteProjectsTable.Count);
            foreach (var opt in fromInstituteProjectsTable)
            {
                var v = await BuildProjectReadyValidationForInstituteProjectRowAsync(opt.Id, instituteId);
                if (v.IsReady)
                    readyInstituteProjectsTable.Add(opt);
            }

            var readyLegacyInstitute = new List<CourseCreateProjectOptionDto>(legacyInstituteProjects.Count);
            foreach (var opt in legacyInstituteProjects)
            {
                var v = await BuildProjectReadyValidationForInstituteProjectAsync(opt.Id, instituteId);
                if (v.IsReady)
                    readyLegacyInstitute.Add(opt);
            }

            var instituteOwned = new List<CourseCreateProjectOptionDto>(
                readyInstituteProjectsTable.Count + readyLegacyInstitute.Count);
            instituteOwned.AddRange(readyInstituteProjectsTable);
            instituteOwned.AddRange(readyLegacyInstitute);
            instituteOwned.Sort((a, b) =>
                string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));

            // Never list activated catalog mirrors (InstituteProjects.IsBuiltIn); "include built-in" adds global Projects rows instead.
            instituteOwned = instituteOwned
                .Where(o => !(o.IsInstituteProject && o.IsBuiltIn))
                .ToList();

            if (!includeBuiltIn)
                return Ok(instituteOwned);

            var builtInGlobals = await _context.Projects
                .AsNoTracking()
                .Where(p =>
                    p.InstituteId == null &&
                    p.InUse &&
                    p.IsAvailable &&
                    !string.IsNullOrWhiteSpace(p.TrelloBoardJson))
                .OrderBy(p => p.Title)
                .Select(p => new CourseCreateProjectOptionDto
                {
                    Id = p.Id,
                    Title = p.Title ?? string.Empty,
                    InstituteId = p.InstituteId,
                    IsBuiltIn = true,
                    IsInstituteProject = false,
                })
                .ToListAsync();

            var readyBuiltIns = new List<CourseCreateProjectOptionDto>(builtInGlobals.Count);
            foreach (var opt in builtInGlobals)
            {
                var v = await BuildProjectReadyValidationForCatalogProjectAsync(opt.Id);
                if (v.IsReady)
                    readyBuiltIns.Add(opt);
            }

            var merged = new List<CourseCreateProjectOptionDto>(instituteOwned.Count + readyBuiltIns.Count);
            merged.AddRange(instituteOwned);
            merged.AddRange(readyBuiltIns);
            return Ok(merged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading projects for course create (InstituteId={InstituteId})", instituteId);
            return StatusCode(500, "An error occurred while loading projects.");
        }
    }

    /// <summary>
    /// Delete a saved institute template for a specific institute + project.
    /// Route: DELETE /api/Projects/use/by-institute/templates/{instituteTemplateId}?projectId=&instituteId=
    /// </summary>
    [HttpDelete("use/by-institute/templates/{instituteTemplateId:int}")]
    public async Task<ActionResult<object>> DeleteInstituteTemplate(
        int instituteTemplateId,
        [FromQuery] int projectId,
        [FromQuery] int instituteId,
        [FromQuery] bool instituteProject = false)
    {
        if (instituteTemplateId <= 0 || projectId <= 0 || instituteId <= 0)
            return BadRequest("instituteTemplateId, projectId and instituteId must be positive integers.");

        try
        {
            var row = await _context.InstituteTemplates
                .FirstOrDefaultAsync(t =>
                    t.Id == instituteTemplateId &&
                    t.InstituteId == instituteId &&
                    (instituteProject ? t.InstituteProjectId == projectId : t.ProjectId == projectId));
            if (row == null)
                return NotFound($"Institute template {instituteTemplateId} was not found for this institute and project.");

            if (row.IsActive)
            {
                return Conflict(
                    "This course is currently assigned as the active syllabus for its project in Project Designs. "
                    + "Choose another course there or clear the assignment before deleting this template.");
            }

            // Orphan prevention: removing the squad when no other template references it cascades to InstituteSquadRoles.
            var squadId = row.SquadId;
            var squadSharedElsewhere = squadId is > 0 &&
                await _context.InstituteTemplates.AsNoTracking()
                    .AnyAsync(t => t.SquadId == squadId && t.Id != instituteTemplateId);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.InstituteTemplates.Remove(row);
                await _context.SaveChangesAsync();

                if (squadId is > 0 && !squadSharedElsewhere)
                {
                    var squad = await _context.InstituteSquads
                        .FirstOrDefaultAsync(s => s.Id == squadId && s.InstituteId == instituteId);
                    if (squad != null)
                    {
                        _context.InstituteSquads.Remove(squad);
                        await _context.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return Ok(new
            {
                Success = true,
                DeletedId = instituteTemplateId,
                ProjectId = projectId,
                InstituteId = instituteId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting institute template {TemplateId} for institute {InstituteId}, project {ProjectId}", instituteTemplateId, instituteId, projectId);
            return StatusCode(500, "An error occurred while deleting the template.");
        }
    }

    /// <summary>
    /// Delete a saved institute template using institute-scoped POST route convention.
    /// Route: POST /api/Projects/use/by-institute/delete-template
    /// </summary>
    [HttpPost("use/by-institute/delete-template")]
    public Task<ActionResult<object>> DeleteInstituteTemplateByInstitute([FromBody] DeleteInstituteTemplateRequest request)
    {
        if (request == null)
            return Task.FromResult<ActionResult<object>>(BadRequest("Request body is required."));

        return DeleteInstituteTemplate(
            request.InstituteTemplateId,
            request.ProjectId,
            request.InstituteId,
            request.InstituteProject);
    }

    /// <summary>
    /// Get the saved Trello template JSON for a project.
    /// Uses <see cref="Project.TrelloBoardJson"/> unless <paramref name="instituteTemplateId"/> targets a row in
    /// <c>InstituteTemplates</c>, or unless <paramref name="instituteId"/> is set without <paramref name="instituteTemplateId"/> — then the latest institute row for that project + institute is returned.
    /// </summary>
    [HttpGet("use/by-institute/templates")]
    public async Task<ActionResult<ProjectTemplateDto>> GetProjectTemplateByProjectId(
        [FromQuery] int projectId,
        [FromQuery] int? instituteId = null,
        [FromQuery] int? instituteTemplateId = null,
        [FromQuery] bool instituteProject = false)
    {
        if (projectId <= 0)
        {
            return BadRequest("projectId must be a positive number.");
        }

        if (instituteTemplateId is > 0 && instituteId is not > 0)
        {
            return BadRequest("instituteTemplateId requires a positive instituteId.");
        }

        try
        {
            ProjectTemplateDto? project;
            if (instituteProject)
            {
                var ipQuery = _context.InstituteProjects.AsNoTracking().Where(ip => ip.Id == projectId);
                if (instituteId is > 0)
                {
                    ipQuery = ipQuery.Where(ip => ip.InstituteId == instituteId);
                }

                project = await ipQuery
                    .Select(ip => new ProjectTemplateDto
                    {
                        ProjectId = ip.Id,
                        ProjectName = ip.Title,
                        ProjectBrief = ip.Description,
                        TrelloBoardJson = ip.TrelloBoardJson,
                    })
                    .FirstOrDefaultAsync();

                if (project == null)
                {
                    return NotFound($"Institute project with ID {projectId} was not found.");
                }
            }
            else
            {
                project = await _context.Projects
                    .Where(p => p.Id == projectId)
                    .Select(p => new ProjectTemplateDto
                    {
                        ProjectId = p.Id,
                        ProjectName = p.Title,
                        ProjectBrief = p.Description,
                        TrelloBoardJson = p.TrelloBoardJson,
                    })
                    .FirstOrDefaultAsync();

                if (project == null)
                {
                    return NotFound($"Project with ID {projectId} not found.");
                }
            }

            if (string.IsNullOrEmpty(project.TrelloBoardJson))
            {
                project.TrelloBoardJson = string.Empty;
            }

            if (instituteTemplateId is > 0)
            {
                var byId = await _context.InstituteTemplates
                    .AsNoTracking()
                    .Where(t =>
                        t.Id == instituteTemplateId.Value &&
                        t.InstituteId == instituteId!.Value &&
                        (instituteProject ? t.InstituteProjectId == projectId : t.ProjectId == projectId))
                    .Select(t => new { t.TrelloBoardJson, t.CourseName, t.BoardUrl, t.CourseType, t.RoleCount })
                    .FirstOrDefaultAsync();

                if (byId == null)
                {
                    return NotFound(
                        $"Institute template {instituteTemplateId.Value} not found for project {projectId} and institute {instituteId}.");
                }

                project.TrelloBoardJson = byId.TrelloBoardJson;
                project.Name = byId.CourseName;
                project.BoardUrl = byId.BoardUrl;
                project.CourseType = byId.CourseType;
                project.RoleCount = byId.RoleCount;
            }
            else if (instituteId is > 0)
            {
                var instituteRow = await _context.InstituteTemplates
                    .AsNoTracking()
                    .Where(t =>
                        t.InstituteId == instituteId.Value &&
                        (instituteProject ? t.InstituteProjectId == projectId : t.ProjectId == projectId))
                    .OrderByDescending(t => t.Id)
                    .Select(t => new { t.TrelloBoardJson, t.CourseName, t.BoardUrl, t.CourseType, t.RoleCount })
                    .FirstOrDefaultAsync();

                if (instituteRow != null)
                {
                    project.TrelloBoardJson = instituteRow.TrelloBoardJson;
                    project.Name = instituteRow.CourseName;
                    project.BoardUrl = instituteRow.BoardUrl;
                    project.CourseType = instituteRow.CourseType;
                    project.RoleCount = instituteRow.RoleCount;
                }
            }

            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Trello template for project {ProjectId}", projectId);
            return StatusCode(500, "An error occurred while retrieving the project template.");
        }
    }

    /// <summary>
    /// Get projects that already have a saved Trello template JSON and <see cref="Project.IsAvailable"/> is true.
    /// </summary>
    [HttpGet("use/available-with-template")]
    public async Task<ActionResult<IEnumerable<ProjectTemplateDto>>> GetProjectsAvailableWithTemplate()
    {
        try
        {
            var projects = await _context.Projects
                .Where(p => p.IsAvailable && p.TrelloBoardJson != null && p.TrelloBoardJson.Trim() != "")
                .OrderBy(p => p.Title)
                .Select(p => new ProjectTemplateDto
                {
                    ProjectId = p.Id,
                    ProjectName = p.Title,
                    ProjectBrief = p.Description,
                    TrelloBoardJson = p.TrelloBoardJson
                })
                .ToListAsync();

            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects with Trello templates");
            return StatusCode(500, "An error occurred while retrieving projects with templates.");
        }
    }

    /// <summary>
    /// Save Task Builder template JSON for an institute and project.
    /// Query: <c>?projectId=</c> and <c>?instituteId=</c>.
    /// Insert: omit <see cref="AddInstituteTemplateRequest.InstituteTemplateId"/> and send a unique <see cref="AddInstituteTemplateRequest.CourseName"/> (per institute + project, case-insensitive). Legacy clients may send <c>name</c> instead.
    /// Update: set <see cref="AddInstituteTemplateRequest.InstituteTemplateId"/> to an existing row id for this institute + project; optional <see cref="AddInstituteTemplateRequest.CourseName"/> renames if unique.
    /// </summary>
    [HttpPost("use/by-institute/add-template")]
    public async Task<ActionResult<object>> AddInstituteTemplate(
        [FromQuery] int projectId,
        [FromQuery] int instituteId,
        [FromBody] AddInstituteTemplateRequest request,
        [FromQuery] bool instituteProject = false)
    {
        if (projectId <= 0 || instituteId <= 0)
        {
            return BadRequest("projectId and instituteId must be positive integers.");
        }

        if (request == null)
        {
            return BadRequest("Request body is required.");
        }

        try
        {
            var instituteExists = await _context.Institutes.AnyAsync(i => i.Id == instituteId);
            if (!instituteExists)
            {
                return NotFound($"Institute with ID {instituteId} not found.");
            }

            if (instituteProject)
            {
                var ipRow = await _context.InstituteProjects.AsNoTracking()
                    .Where(ip => ip.Id == projectId && ip.InstituteId == instituteId)
                    .Select(ip => new { ip.Id, ip.Title })
                    .FirstOrDefaultAsync();
                if (ipRow == null)
                {
                    return NotFound($"Institute project with ID {projectId} was not found for this institute.");
                }
            }
            else
            {
                var project = await _context.Projects.AsNoTracking()
                    .Where(p => p.Id == projectId)
                    .Select(p => new { p.Id, p.Title })
                    .FirstOrDefaultAsync();
                if (project == null)
                {
                    return NotFound($"Project with ID {projectId} not found.");
                }
            }

            var json = request.TrelloBoardJson?.Trim() ?? string.Empty;
            var effectiveCourseName = !string.IsNullOrWhiteSpace(request.CourseName)
                ? request.CourseName
                : request.Name;

            static string ClampName(string s)
            {
                var t = s.Trim();
                return t.Length <= 100 ? t : t[..100];
            }

            if (request.InstituteTemplateId is > 0)
            {
                var row = await _context.InstituteTemplates
                    .FirstOrDefaultAsync(t =>
                        t.Id == request.InstituteTemplateId.Value &&
                        t.InstituteId == instituteId &&
                        (instituteProject
                            ? t.InstituteProjectId == projectId && t.ProjectId == null
                            : t.ProjectId == projectId && t.InstituteProjectId == null));

                if (row == null)
                {
                    return NotFound(
                        $"Institute template {request.InstituteTemplateId.Value} not found for this institute and project.");
                }

                if (row.IsActive)
                {
                    return Conflict(
                        "This course is currently assigned as the active syllabus in Project Designs. The existing template cannot be overwritten. Save your changes as a new course instead.");
                }

                if (!string.IsNullOrWhiteSpace(effectiveCourseName))
                {
                    var newName = ClampName(effectiveCourseName!);
                    var taken = await _context.InstituteTemplates.AsNoTracking()
                        .AnyAsync(t =>
                            t.InstituteId == instituteId &&
                            t.Id != row.Id &&
                            t.CourseName.ToLower() == newName.ToLower());
                    if (taken)
                    {
                        return Conflict($"A template named \"{newName}\" already exists for this institute.");
                    }

                    row.CourseName = newName;
                }

                row.TrelloBoardJson = json;
                if (!string.IsNullOrWhiteSpace(request.CourseType))
                    row.CourseType = request.CourseType.Trim();
                if (request.RoleCount is >= 1 and <= 5)
                    row.RoleCount = request.RoleCount;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Updated InstituteTemplates row {Id} for InstituteId={InstituteId}, ProjectId={ProjectId}, InstituteProject={InstituteProject}",
                    row.Id, instituteId, projectId, instituteProject);

                return Ok(new
                {
                    Success = true,
                    Id = row.Id,
                    InstituteId = instituteId,
                    ProjectId = projectId,
                    Name = row.CourseName,
                    CourseName = row.CourseName,
                    Updated = true,
                });
            }

            if (string.IsNullOrWhiteSpace(effectiveCourseName))
            {
                return BadRequest("CourseName is required when creating a new institute template.");
            }

            var insertName = ClampName(effectiveCourseName!);
            // Check against the DB unique constraint: (InstituteId, CourseName) is unique across all projects.
            var insertNameTaken = await _context.InstituteTemplates.AsNoTracking()
                .AnyAsync(t =>
                    t.InstituteId == instituteId &&
                    t.CourseName.ToLower() == insertName.ToLower());
            if (insertNameTaken)
            {
                return Conflict($"A template named \"{insertName}\" already exists for this institute.");
            }

            var newRow = new InstituteTemplate
            {
                InstituteId = instituteId,
                ProjectId = instituteProject ? null : projectId,
                InstituteProjectId = instituteProject ? projectId : null,
                CourseName = insertName,
                TrelloBoardJson = json,
                // Do not auto-select newly created templates in Project Designs until the user saves an assignment.
                IsActive = false,
                CourseType = !string.IsNullOrWhiteSpace(request.CourseType) ? request.CourseType.Trim() : "Squad",
                RoleCount = request.RoleCount is >= 1 and <= 5 ? request.RoleCount : null,
            };
            _context.InstituteTemplates.Add(newRow);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created InstituteTemplates row {Id} for InstituteId={InstituteId}, ProjectId={ProjectId}, InstituteProject={InstituteProject}",
                newRow.Id, instituteId, projectId, instituteProject);

            return Ok(new
            {
                Success = true,
                Id = newRow.Id,
                InstituteId = instituteId,
                ProjectId = projectId,
                Name = newRow.CourseName,
                CourseName = newRow.CourseName,
                Updated = false,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving Trello template for InstituteId={InstituteId}, ProjectId={ProjectId}", instituteId, projectId);
            // DEBUG — remove after diagnosis
            try
            {
                var body = $"AddInstituteTemplate 500 DEBUG\n" +
                           $"projectId={projectId} instituteId={instituteId} instituteProject={instituteProject}\n" +
                           $"CourseName={request?.CourseName} InstituteTemplateId={request?.InstituteTemplateId}\n" +
                           $"Exception: {ex.Message}\n" +
                           $"Inner: {ex.InnerException?.Message ?? "none"}\n" +
                           $"Stack:\n{ex.StackTrace}";
                await _smtpEmailService.SendPlainEmailAsync("ofer@skill-in.com", "[CourseBuilder Debug] AddInstituteTemplate 500", body);
            }
            catch { /* ignore debug errors */ }
            // END DEBUG
            return StatusCode(500, "An error occurred while saving the template.");
        }
    }

    /// <summary>
    /// Get all project criteria
    /// </summary>
    [HttpGet("use/ProjectCriteria")]
    public async Task<ActionResult<IEnumerable<ProjectCriteria>>> GetProjectCriteria()
    {
        try
        {
            var criteria = await _context.ProjectCriterias
                .Where(c => c.Active)
                .OrderBy(c => c.Id)
                .ToListAsync();

            return Ok(criteria);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project criteria");
            return StatusCode(500, "An error occurred while retrieving project criteria");
        }
    }

    /// <summary>
    /// Get all students who have this project in their priority fields (ProjectPriority1-4) and Status < 2
    /// </summary>
    /// <param name="id">The project ID</param>
    /// <param name="candidateRoleId">When MultiRolesPerProject is false, students with this active role are excluded, developer/full-stack pairing rules apply (see <see cref="ShouldExcludePeerByDeveloperRule"/>), then the list is de-duplicated to one student per role.</param>
    /// <param name="currentStudentId">Optional logged-in student id that should always be returned when present in this project's candidate list (used to keep the applicant visible after Apply).</param>
    [HttpGet("use/get-students/{id}")]
    public async Task<ActionResult<IEnumerable<object>>> GetStudentsForProject(
        int id,
        [FromQuery(Name = "CandidateRoleId")] int? candidateRoleId = null,
        [FromQuery(Name = "CurrentStudentId")] int? currentStudentId = null)
    {
        try
        {
            _logger.LogInformation(
                "Getting students for project {ProjectId} with Status < 2 (MultiRolesPerProject={MultiRoles}, CandidateRoleId={CandidateRoleId}, CurrentStudentId={CurrentStudentId})",
                id, _kickoffConfig.MultiRolesPerProject, candidateRoleId, currentStudentId);

            // Accept both B2C Projects and InstituteProjects ids
            var projectExists = await _context.Projects.AnyAsync(p => p.Id == id)
                             || await _context.InstituteProjects.AnyAsync(p => p.Id == id);
            if (!projectExists)
            {
                _logger.LogWarning("Project {ProjectId} not found", id);
                return NotFound($"Project with ID {id} not found.");
            }

            // Get students where any ProjectPriority or InstitutePriority field equals projectId AND Status < 2
            var students = await _context.Students
                .Include(s => s.Major)
                .Include(s => s.Year)
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .Where(s => s.Status.HasValue && s.Status < 2 && (
                    s.ProjectPriority1 == id ||
                    s.ProjectPriority2 == id ||
                    s.ProjectPriority3 == id ||
                    s.ProjectPriority4 == id ||
                    s.InstitutePriority1 == id ||
                    s.InstitutePriority2 == id ||
                    s.InstitutePriority3 == id ||
                    s.InstitutePriority4 == id
                ))
                .Select(s => new
                {
                    Id = s.Id,
                    FirstName = s.FirstName,
                    LastName = s.LastName,
                    Email = s.Email,
                    StudentId = s.StudentId,
                    MajorId = s.MajorId,
                    MajorName = s.Major != null ? s.Major.Name : null,
                    YearId = s.YearId,
                    YearName = s.Year != null ? s.Year.Name : null,
                    LinkedInUrl = s.LinkedInUrl,
                    GithubUser = s.GithubUser,
                    Photo = s.Photo,
                    ProjectId = s.ProjectId,
                    ProjectPriority1 = s.ProjectPriority1,
                    ProjectPriority2 = s.ProjectPriority2,
                    ProjectPriority3 = s.ProjectPriority3,
                    ProjectPriority4 = s.ProjectPriority4,
                    IsAdmin = s.IsAdmin,
                    BoardId = s.BoardId,
                    IsAvailable = s.IsAvailable,
                    Status = s.Status,
                    StartPendingAt = s.StartPendingAt,
                    RoleId = s.StudentRoles.FirstOrDefault(sr => sr.IsActive) != null ? s.StudentRoles.FirstOrDefault(sr => sr.IsActive).RoleId : (int?)null,
                    RoleName = s.StudentRoles.FirstOrDefault(sr => sr.IsActive) != null ? s.StudentRoles.FirstOrDefault(sr => sr.IsActive).Role.Name : null,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                })
                .ToListAsync();

            if (!_kickoffConfig.MultiRolesPerProject)
            {
                string? candidateRoleName = null;
                if (candidateRoleId.HasValue)
                {
                    candidateRoleName = await _context.Roles
                        .Where(r => r.Id == candidateRoleId.Value)
                        .Select(r => r.Name)
                        .FirstOrDefaultAsync();
                    if (string.IsNullOrWhiteSpace(candidateRoleName))
                    {
                        _logger.LogWarning(
                            "get-students project {ProjectId}: CandidateRoleId {CandidateRoleId} has no matching Role row; " +
                            "developer/full-stack pairing is skipped (same-role exclusion still applies). Client should pass a valid role id.",
                            id, candidateRoleId.Value);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "get-students project {ProjectId}: CandidateRoleId query param missing while MultiRolesPerProject=false; " +
                        "same-role exclusion and developer/full-stack pairing are skipped (per-role dedupe still runs). " +
                        "Student UI should pass the logged-in active role id.",
                        id);
                }

                var filtered = students
                    .Where(s => !candidateRoleId.HasValue || s.RoleId != candidateRoleId.Value || (currentStudentId.HasValue && s.Id == currentStudentId.Value))
                    .Where(s => (currentStudentId.HasValue && s.Id == currentStudentId.Value) || !ShouldExcludePeerByDeveloperRule(candidateRoleName, s.RoleName))
                    .ToList();

                students = filtered
                    .GroupBy(s => s.RoleId)
                    .Select(g => g
                        .OrderByDescending(s => currentStudentId.HasValue && s.Id == currentStudentId.Value)
                        .ThenBy(s => s.UpdatedAt ?? DateTime.MaxValue)
                        .ThenBy(s => s.Id)
                        .First())
                    .ToList();
            }

            _logger.LogInformation("Found {Count} students for project {ProjectId} with Status < 2", students.Count, id);
            return Ok(students);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving students for project {ProjectId}: {Message}", id, ex.Message);
            return StatusCode(500, $"An error occurred while retrieving students for the project: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all projects for an organization by email address
    /// </summary>
    [HttpGet("use/by-email/{email}")]
    public async Task<ActionResult<IEnumerable<Project>>> GetProjectsByEmail(string email)
    {
        try
        {
            _logger.LogInformation("Getting projects for organization with email {Email}", email);

            // First, find the organization by email
            var organization = await _context.Organizations
                .FirstOrDefaultAsync(o => o.ContactEmail == email);

            if (organization == null)
            {
                _logger.LogWarning("Organization not found for email {Email}", email);
                return NotFound($"Organization with email {email} not found.");
            }

            _logger.LogInformation("Found organization {OrganizationId} for email {Email}", organization.Id, email);

            // Get all projects for this organization
            var projects = await _context.Projects
                .Where(p => p.OrganizationId == organization.Id)
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Mission = p.Mission,
                    OneLiner = p.OneLiner,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    ShortBrief = p.ShortBrief,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    IsAvailable = p.IsAvailable,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                    // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                })
                .ToListAsync();

            _logger.LogInformation("Found {ProjectCount} projects for organization {OrganizationId}", projects.Count, organization.Id);

            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects for email {Email}", email);
            return StatusCode(500, "An error occurred while retrieving projects for the organization");
        }
    }

    /// <summary>
    /// Get Project Engagement Rules - informative rules for project participation
    /// </summary>
    [HttpGet("use/engagement-rules")]
    public ActionResult<object> GetProjectEngagementRules()
    {
        try
        {
            _logger.LogInformation("Retrieving Project Engagement Rules");

            // Build sections from configuration
            var sections = new List<object>();

            // Project Structure section with dynamic values
            if (_engagementRulesConfig.Sections.ContainsKey("ProjectStructure"))
            {
                var projectStructure = _engagementRulesConfig.Sections["ProjectStructure"];
                var projectStructureRules = new List<string>(projectStructure.Rules);
                projectStructureRules.Insert(0, $"Project Duration: {_businessLogicConfig.ProjectLengthInWeeks} weeks");
                projectStructureRules.Insert(1, $"Sprint Length: {_businessLogicConfig.SprintLengthInWeeks} week per sprint");
                projectStructureRules.Insert(2, $"Total Sprints: {_businessLogicConfig.ProjectLengthInWeeks / _businessLogicConfig.SprintLengthInWeeks} sprints per project");

                sections.Add(new
                {
                    title = projectStructure.Title,
                    description = projectStructure.Description,
                    rules = projectStructureRules
                });
            }

            // Kickoff Requirements section with dynamic values
            if (_engagementRulesConfig.Sections.ContainsKey("KickoffRequirements"))
            {
                var kickoffRequirements = _engagementRulesConfig.Sections["KickoffRequirements"];
                var kickoffRules = new List<string>(kickoffRequirements.Rules);
                kickoffRules.Insert(0, $"Minimum Juniors: {_kickoffConfig.MinimumStudents} juniors must be allocated");
                kickoffRules.Insert(1, _kickoffConfig.RequireAdmin ? "Admin Required: At least one junior must be an admin" : "Admin Required: Not required");
                kickoffRules.Insert(2, _kickoffConfig.RequireUIUXDesigner ? "UI/UX Designer Required: Exactly one junior must have UI/UX Designer role (Type=3)" : "UI/UX Designer Required: Not required");
                kickoffRules.Insert(3, _kickoffConfig.RequireProductManager ? "Product Manager Required: Exactly one junior must have Product Manager role (Type=4)" : "Product Manager Required: Not required");
                kickoffRules.Insert(4, _kickoffConfig.RequireDeveloperRule ? "Developer Rule: At least 1 Full-stack Developer OR at least one Backend and one Frontend developers" : "Developer Rule: Not required");

                sections.Add(new
                {
                    title = kickoffRequirements.Title,
                    description = kickoffRequirements.Description,
                    rules = kickoffRules
                });
            }

            // Add other sections from configuration
            foreach (var sectionKey in new[] { "RoleTypes", "ProjectStatusFlow", "TechnicalRequirements", "JuniorAllocationProcess" })
            {
                if (_engagementRulesConfig.Sections.ContainsKey(sectionKey))
                {
                    var section = _engagementRulesConfig.Sections[sectionKey];
                    sections.Add(new
                    {
                        title = section.Title,
                        description = section.Description,
                        rules = section.Rules
                    });
                }
            }

            var rules = new
            {
                title = _engagementRulesConfig.Title,
                description = _engagementRulesConfig.Description,
                lastUpdated = DateTime.UtcNow,
                sections = sections,
                configuration = new
                {
                    businessLogic = new
                    {
                        projectLengthInWeeks = _businessLogicConfig.ProjectLengthInWeeks,
                        sprintLengthInWeeks = _businessLogicConfig.SprintLengthInWeeks,
                        maxProjectsSelection = _businessLogicConfig.MaxProjectsSelection
                    },
                    kickoff = new
                    {
                        minimumStudents = _kickoffConfig.MinimumStudents,
                        requireAdmin = _kickoffConfig.RequireAdmin,
                        requireUIUXDesigner = _kickoffConfig.RequireUIUXDesigner,
                        requireProductManager = _kickoffConfig.RequireProductManager,
                        requireDeveloperRule = _kickoffConfig.RequireDeveloperRule
                    }
                },
                summary = _engagementRulesConfig.Summary
            };

            _logger.LogInformation("Project Engagement Rules retrieved successfully");
            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Project Engagement Rules");
            return StatusCode(500, new
            {
                error = "An error occurred while retrieving the Project Engagement Rules",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Get a single project by ID
    /// </summary>
    [HttpGet("use/{id:int}")]
    public async Task<ActionResult<Project>> GetProjectById(
        int id,
        [FromQuery] bool instituteProject = false)
    {
        try
        {
            _logger.LogInformation("Getting project with ID {ProjectId} instituteProject={InstituteProject}", id, instituteProject);

            Project? project;
            if (instituteProject)
            {
                project = await _context.InstituteProjects
                    .Where(ip => ip.Id == id)
                    .Select(ip => new Project
                    {
                        Id = ip.Id,
                        Title = ip.Title,
                        Mission = ip.Mission,
                        OneLiner = ip.OneLiner,
                        Description = ip.Description,
                        ExtendedDescription = ip.ExtendedDescription,
                        ShortBrief = ip.ShortBrief,
                        Priority = ip.Priority,
                        OrganizationId = ip.OrganizationId,
                        InstituteId = ip.InstituteId,
                        IsAvailable = ip.IsAvailable,
                        Kickoff = ip.Kickoff,
                        CreatedAt = ip.CreatedAt,
                        UpdatedAt = ip.UpdatedAt,
                    })
                    .FirstOrDefaultAsync();
            }
            else
            {
                project = await _context.Projects
                    .Where(p => p.Id == id)
                    .Select(p => new Project
                    {
                        Id = p.Id,
                        Title = p.Title,
                        Mission = p.Mission,
                        OneLiner = p.OneLiner,
                        Description = p.Description,
                        ExtendedDescription = p.ExtendedDescription,
                        ShortBrief = p.ShortBrief,
                        Priority = p.Priority,
                        OrganizationId = p.OrganizationId,
                        IsAvailable = p.IsAvailable,
                        Kickoff = p.Kickoff,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                        // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                    })
                    .FirstOrDefaultAsync();
            }

            if (project == null)
            {
                _logger.LogWarning("Project with ID {ProjectId} not found (instituteProject={InstituteProject})", id, instituteProject);
                return NotFound($"Project with ID {id} not found.");
            }

            _logger.LogInformation("Found project {ProjectTitle} with ID {ProjectId}", project.Title, project.Id);
            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project with ID {ProjectId}", id);
            return StatusCode(500, "An error occurred while retrieving the project");
        }
    }

    /// <summary>
    /// Duplicate into <see cref="InstituteProjects"/>: from a built-in/legacy <see cref="Project"/> or an existing
    /// <see cref="InstituteProject"/> row when <c>?sourceInstituteProject=true</c>.
    /// Title uses the same &quot;-Copy&quot; suffix rules; uniqueness is enforced across <c>InstituteProjects</c> and
    /// legacy institute <c>Projects</c> rows for the institute.
    /// The copy does not include a course: <c>TrelloBoardJson</c> is null and no <c>InstituteTemplates</c> rows are created;
    /// modules and other header fields are copied as before.
    /// Route: POST /api/Projects/use/duplicate/{id}?sourceInstituteProject=true
    /// </summary>
    [HttpPost("use/duplicate/{id:int}")]
    public async Task<ActionResult<ProjectDesignsListItemDto>> DuplicateProject(
        int id,
        [FromBody] DuplicateProjectRequest request,
        [FromQuery] bool sourceInstituteProject = false)
    {
        try
        {
            var authInstituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!authInstituteId.HasValue || authInstituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }
            var instituteId = authInstituteId.Value;

            var instituteExists = await _context.Institutes.AsNoTracking()
                .AnyAsync(i => i.Id == instituteId && i.IsActive);
            if (!instituteExists)
            {
                return NotFound($"Institute with ID {instituteId} not found or inactive.");
            }

            const int preferredOrganizationId = 100;
            var organizationId =
                await _context.Organizations.AsNoTracking().AnyAsync(o => o.Id == preferredOrganizationId)
                    ? preferredOrganizationId
                    : (int?)null;
            if (organizationId == null)
            {
                _logger.LogWarning(
                    "DuplicateProject: Organizations.Id={PreferredOrgId} not found — inserting with OrganizationId=null (avoid FK violation). SourceId={SourceId}, InstituteId={InstituteId}",
                    preferredOrganizationId,
                    id,
                    instituteId);
            }

            const int titleLimit = 200;

            static string BuildCopyTitle(string baseTitle, int suffixNumber, int limit)
            {
                var suffix = suffixNumber <= 1 ? "-Copy" : $"-Copy{suffixNumber}";
                var maxBaseLen = Math.Max(1, limit - suffix.Length);
                var trimmedBase = baseTitle.Length > maxBaseLen ? baseTitle[..maxBaseLen] : baseTitle;
                return $"{trimmedBase}{suffix}";
            }

            var instituteIpTitles = await _context.InstituteProjects
                .AsNoTracking()
                .Where(ip => ip.InstituteId == instituteId && ip.Title != null)
                .Select(ip => ip.Title!)
                .ToListAsync();
            var legacyInstituteProjectTitles = await _context.Projects
                .AsNoTracking()
                .Where(p => p.InstituteId == instituteId && p.Title != null)
                .Select(p => p.Title!)
                .ToListAsync();
            var existingSet = new HashSet<string>(
                instituteIpTitles.Concat(legacyInstituteProjectTitles),
                StringComparer.OrdinalIgnoreCase);

            InstituteProject newIp;
            List<ProjectModule> sourceCatalogModules = new();
            List<InstituteProjectModule> sourceInstituteModules = new();

            if (sourceInstituteProject)
            {
                var sourceIp = await _context.InstituteProjects
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ip => ip.Id == id && ip.InstituteId == instituteId);
                if (sourceIp == null)
                {
                    return NotFound($"Institute project with ID {id} was not found.");
                }

                var rawSourceTitle = string.IsNullOrWhiteSpace(sourceIp.Title) ? "Project" : sourceIp.Title.Trim();
                var sourceTitle = Regex.Replace(
                    rawSourceTitle,
                    @"-copy\d*$",
                    string.Empty,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
                if (string.IsNullOrWhiteSpace(sourceTitle))
                {
                    sourceTitle = "Project";
                }

                var suffixNumber = 1;
                var copyTitle = BuildCopyTitle(sourceTitle, suffixNumber, titleLimit);
                while (existingSet.Contains(copyTitle))
                {
                    suffixNumber++;
                    copyTitle = BuildCopyTitle(sourceTitle, suffixNumber, titleLimit);
                }

                newIp = InstituteProjectMapper.ForDuplicateFromInstituteProject(sourceIp, instituteId, copyTitle, organizationId);
                _context.InstituteProjects.Add(newIp);
                await _context.SaveChangesAsync();

                sourceInstituteModules = await _context.InstituteProjectModules
                    .AsNoTracking()
                    .Where(pm => pm.InstituteProjectId == id)
                    .OrderBy(pm => pm.Sequence ?? int.MaxValue)
                    .ThenBy(pm => pm.Id)
                    .ToListAsync();

                _logger.LogInformation(
                    "DuplicateProject: source=InstituteProject SourceId={SourceId}, NewInstituteProjectId={NewId}, SourceModulesCount={Count}",
                    id,
                    newIp.Id,
                    sourceInstituteModules.Count);
            }
            else
            {
                var source = await _context.Projects
                    .AsNoTracking()
                    .Include(p => p.Organization)
                    .FirstOrDefaultAsync(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId));
                if (source == null)
                {
                    return NotFound($"Project with ID {id} not found.");
                }

                var rawSourceTitle = string.IsNullOrWhiteSpace(source.Title) ? "Project" : source.Title.Trim();
                var sourceTitle = Regex.Replace(
                    rawSourceTitle,
                    @"-copy\d*$",
                    string.Empty,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
                if (string.IsNullOrWhiteSpace(sourceTitle))
                {
                    sourceTitle = "Project";
                }

                var suffixNumber = 1;
                var copyTitle = BuildCopyTitle(sourceTitle, suffixNumber, titleLimit);
                while (existingSet.Contains(copyTitle))
                {
                    suffixNumber++;
                    copyTitle = BuildCopyTitle(sourceTitle, suffixNumber, titleLimit);
                }

                var basePid = source.InstituteId == null ? source.Id : (int?)null;
                newIp = InstituteProjectMapper.ForDuplicateFromProject(source, instituteId, basePid, copyTitle, organizationId);

                _context.InstituteProjects.Add(newIp);
                await _context.SaveChangesAsync();

                sourceCatalogModules = await _context.ProjectModules
                    .AsNoTracking()
                    .Where(pm => pm.ProjectId == source.Id)
                    .OrderBy(pm => pm.Sequence ?? int.MaxValue)
                    .ThenBy(pm => pm.Id)
                    .ToListAsync();

                _logger.LogInformation(
                    "DuplicateProject: source=Project SourceProjectId={SourceId}, NewInstituteProjectId={NewId}, SourceModulesCount={Count}",
                    source.Id,
                    newIp.Id,
                    sourceCatalogModules.Count);
            }

            var sourceModulesCount = sourceInstituteProject ? sourceInstituteModules.Count : sourceCatalogModules.Count;
            if (sourceModulesCount > 0)
            {
                if (sourceInstituteProject)
                {
                    var duplicatedModules = sourceInstituteModules.Select(pm => new InstituteProjectModule
                    {
                        InstituteProjectId = newIp.Id,
                        ModuleType = pm.ModuleType,
                        Title = pm.Title,
                        Description = pm.Description,
                        Sequence = pm.Sequence,
                        OriginalModuleId = pm.OriginalModuleId ?? pm.Id,
                    }).ToList();
                    _context.InstituteProjectModules.AddRange(duplicatedModules);
                }
                else
                {
                    var duplicatedModules = sourceCatalogModules.Select(pm => new InstituteProjectModule
                    {
                        InstituteProjectId = newIp.Id,
                        ModuleType = pm.ModuleType,
                        Title = pm.Title,
                        Description = pm.Description,
                        Sequence = pm.Sequence,
                        OriginalModuleId = pm.Id,
                    }).ToList();
                    _context.InstituteProjectModules.AddRange(duplicatedModules);
                }

                await _context.SaveChangesAsync();

                var newModuleRows = await _context.InstituteProjectModules
                    .AsNoTracking()
                    .Where(pm => pm.InstituteProjectId == newIp.Id && pm.OriginalModuleId != null)
                    .ToListAsync();
                if (newModuleRows.Count != sourceModulesCount)
                {
                    _logger.LogWarning(
                        "DuplicateProject: new module row count with OriginalModuleId {NewCount} != source {SourceCount} for NewInstituteProjectId={NewProjectId}.",
                        newModuleRows.Count,
                        sourceModulesCount,
                        newIp.Id);
                }
            }
            else
            {
                var hasModuleType2 = await _context.ModuleTypes
                    .AsNoTracking()
                    .AnyAsync(mt => mt.Id == 2);
                if (hasModuleType2)
                {
                    _context.InstituteProjectModules.Add(new InstituteProjectModule
                    {
                        InstituteProjectId = newIp.Id,
                        ModuleType = 2,
                        Title = "Module 1",
                        Description = string.Empty,
                        Sequence = 1,
                    });
                    await _context.SaveChangesAsync();
                    _logger.LogInformation(
                        "DuplicateProject: source had no modules; created default module for NewInstituteProjectId={NewProjectId}",
                        newIp.Id);
                }
                else
                {
                    _logger.LogWarning(
                        "DuplicateProject: source had no modules and ModuleType=2 not found; skipped default ProjectModule row for InstituteProjectId={ProjectId}",
                        newIp.Id);
                }
            }

            _logger.LogInformation(
                "DuplicateProject: no course/board copied — TrelloBoardJson cleared, no InstituteTemplates rows. NewInstituteProjectId={NewId}",
                newIp.Id);

            return Ok(new ProjectDesignsListItemDto
            {
                Id = newIp.Id,
                Title = newIp.Title,
                BuiltInCourseName = newIp.BuiltInCourseName,
                CourseName = newIp.BuiltInCourseName ?? newIp.Title,
                Mission = newIp.Mission,
                OneLiner = newIp.OneLiner,
                Description = newIp.Description,
                ExtendedDescription = newIp.ExtendedDescription,
                ShortBrief = newIp.ShortBrief,
                Priority = newIp.Priority,
                OrganizationId = newIp.OrganizationId,
                InstituteId = newIp.InstituteId,
                IsAvailable = newIp.IsAvailable,
                InUse = newIp.InUse,
                Kickoff = newIp.Kickoff,
                CriteriaIds = newIp.CriteriaIds,
                CreatedAt = newIp.CreatedAt,
                UpdatedAt = newIp.UpdatedAt,
                InstituteProject = true,
                BaseProjectId = newIp.BaseProjectId,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error duplicating project {ProjectId}", id);
            return StatusCode(500, "An error occurred while duplicating the project.");
        }
    }

    private static string FormatModuleIdMapForLog(IReadOnlyDictionary<int, int> map, int maxPairs = 40)
    {
        if (map is null or { Count: 0 })
        {
            return "(empty)";
        }

        var parts = map.Take(maxPairs).Select(kv => $"{kv.Key}→{kv.Value}");
        var s = string.Join(", ", parts);
        if (map.Count > maxPairs)
        {
            s += $" …(+{map.Count - maxPairs})";
        }

        return s;
    }

    /// <summary>
    /// After duplicating a project, rewrites <c>*ModuleId</c> (JSON number or string of digits) and
    /// <c>ProjectId</c> (number or string) via regex passes, then logs warnings if any old map id or source project id
    /// still appears in those fields. No <see cref="JsonNode"/> walk. On failure returns <paramref name="rawJson"/>.
    /// </summary>
    private string? TryRemapTrelloJsonAfterProjectDuplicate(
        string rawJson,
        IReadOnlyDictionary<int, int> sourceToTargetModuleIds,
        int sourceProjectId,
        int newProjectId)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return rawJson;
        }

        var moduleMap = sourceToTargetModuleIds ?? new Dictionary<int, int>();
        if (sourceProjectId == newProjectId)
        {
            _logger.LogWarning(
                "TryRemapTrelloJsonAfterProjectDuplicate: sourceProjectId == newProjectId ({Id}); skipping remap",
                sourceProjectId);
            return rawJson;
        }

        _logger.LogInformation(
            "TryRemapTrelloJsonAfterProjectDuplicate: enter SourceProjectId={Src} NewProjectId={New} mapSize={N} map=[{Map}] jsonLen={Len}",
            sourceProjectId,
            newProjectId,
            moduleMap.Count,
            FormatModuleIdMapForLog(moduleMap),
            rawJson.Length);

        var modRxTrelloNum = 0;
        var modRxTrelloStr = 0;
        var modRxNum = 0;
        var modRxStr = 0;
        var projRxNum = 0;
        var projRxStr = 0;

        try
        {
            // Regex-only: JsonNode graph walks are not safe (shared nodes → "The node already has a parent.").
            var s = rawJson;
            if (moduleMap.Count > 0)
            {
                // TrelloProjectCreationRequest / TrelloTask JSON uses JsonProperty "moduleId" (string) for DB module row id
                // — that exact shape is handled first; generic *ModuleId pass follows.
                s = ApplyTrelloModelModuleIdNumeric(s, moduleMap, out modRxTrelloNum);
                s = ApplyTrelloModelModuleIdStringValue(s, moduleMap, out modRxTrelloStr);
                s = ApplyModuleIdMapNumericRegex(s, moduleMap, out modRxNum);
                s = ApplyModuleIdMapStringValueRegex(s, moduleMap, out modRxStr);
            }

            s = ApplyProjectIdNumericRegex(s, sourceProjectId, newProjectId, out projRxNum);
            s = ApplyProjectIdStringValueRegex(s, sourceProjectId, newProjectId, out projRxStr);

            var moduleTotal = modRxTrelloNum + modRxTrelloStr + modRxNum + modRxStr;
            var projectTotal = projRxNum + projRxStr;
            var outChanged = !string.Equals(s, rawJson, StringComparison.Ordinal);
            _logger.LogInformation(
                "TryRemapTrelloJsonAfterProjectDuplicate: done (regex) NewProjectId={New} outChanged={Changed} trlNum={TrelloN} trlStr={TrelloS} modNum={ModN} modStr={ModS} modTotal={ModT} projNum={PrN} projStr={PrS} projTotal={PrT} mapSize={MapSize} inLen={InLen} outLen={OutLen}",
                newProjectId,
                outChanged,
                modRxTrelloNum,
                modRxTrelloStr,
                modRxNum,
                modRxStr,
                moduleTotal,
                projRxNum,
                projRxStr,
                projectTotal,
                moduleMap.Count,
                rawJson.Length,
                s.Length);

            if (moduleMap.Count > 0 && moduleTotal == 0)
            {
                var hasModuleIdKey = rawJson.Contains("oduleId", StringComparison.OrdinalIgnoreCase);
                _logger.LogWarning(
                    "TryRemapTrelloJsonAfterProjectDuplicate: 0 module id replacements. JSON contains oduleId-like text={HasKey} SourceProjectId={SourceId} NewProjectId={NewId} map=[{Map}]",
                    hasModuleIdKey,
                    sourceProjectId,
                    newProjectId,
                    FormatModuleIdMapForLog(moduleMap));
            }

            LogTrelloRemapResiduals(
                s,
                moduleMap,
                sourceProjectId,
                newProjectId);

            if (!outChanged)
            {
                _logger.LogWarning(
                    "TryRemapTrelloJsonAfterProjectDuplicate: output equals input (no net change). NewProjectId={NewId} SourceProjectId={Src} modT={ModT} projT={PrT}",
                    newProjectId,
                    sourceProjectId,
                    moduleTotal,
                    projectTotal);
            }

            return s;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "TryRemapTrelloJsonAfterProjectDuplicate: failed during regex remap; leaving Trello JSON unchanged (length={Length}) NewProjectId={New} SourceProjectId={Src}",
                rawJson.Length,
                newProjectId,
                sourceProjectId);
            return rawJson;
        }
    }

    /// <summary>Post-remap: warn if any <c>*ModuleId</c> field still holds a value that is a key in the old→new map, or <c>ProjectId</c> still equals the source id.</summary>
    private void LogTrelloRemapResiduals(
        string json,
        IReadOnlyDictionary<int, int> moduleMap,
        int sourceProjectId,
        int newProjectId)
    {
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        var valuePat = new Regex(
            @"""([^""]*)""\s*:\s*(?:([0-9]+)|""\s*([0-9]+)\s*"")",
            RegexOptions.CultureInvariant);
        var stale = new List<int>();
        foreach (Match m in valuePat.Matches(json))
        {
            var key = m.Groups[1].Value;
            if (!key.EndsWith("ModuleId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var vs = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
            if (!int.TryParse(vs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            {
                continue;
            }

            if (moduleMap.Count > 0 && moduleMap.ContainsKey(n))
            {
                stale.Add(n);
            }
        }

        if (stale.Count > 0)
        {
            var sample = string.Join(
                ", ",
                stale.Distinct().Order().Take(24).Select(x => x.ToString(CultureInfo.InvariantCulture)));
            if (stale.Distinct().Count() > 24)
            {
                sample += " …";
            }

            _logger.LogWarning(
                "TryRemapTrelloJsonAfterProjectDuplicate: RESIDUAL old module id value(s) still in *ModuleId fields: [{Stale}] (should have been remapped). NewProjectId={New} SourceProjectId={Src}",
                sample,
                newProjectId,
                sourceProjectId);
        }

        if (sourceProjectId == newProjectId)
        {
            return;
        }

        var prx = new Regex(
            @"(?i)""(projectid)""\s*:\s*(?:([0-9]+)|""\s*([0-9]+)\s*"")",
            RegexOptions.CultureInvariant);
        foreach (Match m in prx.Matches(json))
        {
            var vs = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
            if (int.TryParse(vs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) && p == sourceProjectId)
            {
                _logger.LogWarning(
                    "TryRemapTrelloJsonAfterProjectDuplicate: RESIDUAL source ProjectId={Src} still in JSON. NewProjectId={New}",
                    sourceProjectId,
                    newProjectId);
                return;
            }
        }
    }

    /// <summary>
    /// Trello sprint / card JSON: the property is exactly <c>moduleId</c> (case-insensitive), value a JSON <b>number</b>.
    /// This matches the common serialized shape before generic <c>*ModuleId</c> sweeps.
    /// </summary>
    private static string ApplyTrelloModelModuleIdNumeric(
        string json,
        IReadOnlyDictionary<int, int> moduleMap,
        out int replacements)
    {
        replacements = 0;
        if (string.IsNullOrEmpty(json) || moduleMap.Count == 0)
        {
            return json;
        }

        var replBox = new int[1];
        var rx = new Regex(
            @"(?i)""(moduleid)""\s*:\s*([0-9]+)(?![0-9.""])",
            RegexOptions.CultureInvariant);
        var result = rx.Replace(
            json,
            m =>
            {
                if (!int.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                    || !moduleMap.TryGetValue(n, out var toId))
                {
                    return m.Value;
                }

                replBox[0]++;
                return $@"""{m.Groups[1].Value}"" : {toId.ToString(CultureInfo.InvariantCulture)}";
            });
        replacements = replBox[0];
        return result;
    }

    /// <summary>Same as <see cref="ApplyTrelloModelModuleIdNumeric"/> but value is a JSON string of digits (with optional internal spaces).</summary>
    private static string ApplyTrelloModelModuleIdStringValue(
        string json,
        IReadOnlyDictionary<int, int> moduleMap,
        out int replacements)
    {
        replacements = 0;
        if (string.IsNullOrEmpty(json) || moduleMap.Count == 0)
        {
            return json;
        }

        var replBox = new int[1];
        var rx = new Regex(
            @"(?i)""(moduleid)""\s*:\s*""\s*([0-9][0-9 \t]*[0-9]|[0-9]+)\s*""",
            RegexOptions.CultureInvariant);
        var result = rx.Replace(
            json,
            m =>
            {
                var raw = m.Groups[2].Value;
                if (!int.TryParse(
                        raw,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var n)
                    || !moduleMap.TryGetValue(n, out var toId))
                {
                    return m.Value;
                }

                replBox[0]++;
                return $@"""{m.Groups[1].Value}"" : ""{toId.ToString(CultureInfo.InvariantCulture)}""";
            });
        replacements = replBox[0];
        return result;
    }

    /// <summary>
    /// <c>&quot;…ModuleId&quot;: 123</c> (JSON number) when 123 is in the old→new map.
    /// </summary>
    private static string ApplyModuleIdMapNumericRegex(
        string json,
        IReadOnlyDictionary<int, int> moduleMap,
        out int replacements)
    {
        replacements = 0;
        if (string.IsNullOrEmpty(json) || moduleMap.Count == 0)
        {
            return json;
        }

        var rx = new Regex(
            @"""([^""]*)""\s*:\s*([0-9]+)(?![0-9.""])",
            RegexOptions.CultureInvariant);
        var replBox = new int[1];
        var result = rx.Replace(
            json,
            m =>
            {
                var key = m.Groups[1].Value;
                if (!key.EndsWith("ModuleId", StringComparison.OrdinalIgnoreCase))
                {
                    return m.Value;
                }

                if (!int.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                    || !moduleMap.TryGetValue(n, out var toId))
                {
                    return m.Value;
                }

                replBox[0]++;
                return $@"""{key}"" : {toId.ToString(CultureInfo.InvariantCulture)}";
            });
        replacements = replBox[0];
        return result;
    }

    /// <summary>
    /// <c>&quot;…ModuleId&quot;: "123"</c> (JSON string of digits, optional internal spaces) when 123 is in the old→new map.
    /// </summary>
    private static string ApplyModuleIdMapStringValueRegex(
        string json,
        IReadOnlyDictionary<int, int> moduleMap,
        out int replacements)
    {
        replacements = 0;
        if (string.IsNullOrEmpty(json) || moduleMap.Count == 0)
        {
            return json;
        }

        var rx = new Regex(
            @"""([^""]*)""\s*:\s*""\s*([0-9][0-9 \t]*[0-9]|[0-9]+)\s*""",
            RegexOptions.CultureInvariant);
        var replBox = new int[1];
        var result = rx.Replace(
            json,
            m =>
            {
                var key = m.Groups[1].Value;
                if (!key.EndsWith("ModuleId", StringComparison.OrdinalIgnoreCase))
                {
                    return m.Value;
                }

                if (!int.TryParse(
                        m.Groups[2].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var n)
                    || !moduleMap.TryGetValue(n, out var toId))
                {
                    return m.Value;
                }

                replBox[0]++;
                return $@"""{key}"" : ""{toId.ToString(CultureInfo.InvariantCulture)}""";
            });
        replacements = replBox[0];
        return result;
    }

    /// <summary><c>ProjectId</c> as JSON number.</summary>
    private static string ApplyProjectIdNumericRegex(
        string json,
        int sourceProjectId,
        int newProjectId,
        out int replacements)
    {
        replacements = 0;
        if (string.IsNullOrEmpty(json) || sourceProjectId == newProjectId)
        {
            return json;
        }

        var replBox = new int[1];
        var rx = new Regex(
            @"(?i)""(projectid)""\s*:\s*([0-9]+)(?![0-9""])",
            RegexOptions.CultureInvariant);
        var result = rx.Replace(
            json,
            m =>
            {
                if (!int.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                    || n != sourceProjectId)
                {
                    return m.Value;
                }

                replBox[0]++;
                return $@"""{m.Groups[1].Value}"" : {newProjectId.ToString(CultureInfo.InvariantCulture)}";
            });
        replacements = replBox[0];
        return result;
    }

    /// <summary><c>ProjectId</c> as a JSON string of digits.</summary>
    private static string ApplyProjectIdStringValueRegex(
        string json,
        int sourceProjectId,
        int newProjectId,
        out int replacements)
    {
        replacements = 0;
        if (string.IsNullOrEmpty(json) || sourceProjectId == newProjectId)
        {
            return json;
        }

        var newStr = newProjectId.ToString(CultureInfo.InvariantCulture);
        var replBox = new int[1];
        var rx = new Regex(
            @"(?i)""(projectid)""\s*:\s*""\s*([0-9]+)\s*""",
            RegexOptions.CultureInvariant);
        var result = rx.Replace(
            json,
            m =>
            {
                if (!int.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                    || n != sourceProjectId)
                {
                    return m.Value;
                }

                replBox[0]++;
                return $@"""{m.Groups[1].Value}"" : ""{newStr}""";
            });
        replacements = replBox[0];
        return result;
    }

    /// <summary>
    /// Temporary deploy sanity-check endpoint. Remove after deployment verification.
    /// Route: GET /api/Projects/debug/deploy-check
    /// </summary>
    [HttpGet("debug/deploy-check")]
    public ActionResult<object> DebugDeployCheck()
    {
        return Ok(new
        {
            ok = true,
            marker = "projects-controller-deploy-check-v1",
            utc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Suspend a project (set IsAvailable to false)
    /// </summary>
    [HttpPost("use/suspend/{id}")]
    public async Task<ActionResult> SuspendProject(int id)
    {
        try
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found.");
            }

            if (project.InstituteId is int instSuspend)
            {
                var auth = await ResolveInstituteIdFromAuthContextAsync();
                if (!auth.HasValue || auth.Value != instSuspend)
                {
                    return Unauthorized("Institute authentication is required to change availability for this project.");
                }
            }

            project.IsAvailable = false;
            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Project {ProjectId} suspended successfully", id);
            return Ok(new { Success = true, Message = "Project suspended successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending project {ProjectId}", id);
            return StatusCode(500, "An error occurred while suspending the project");
        }
    }

    /// <summary>
    /// Activate a project (set IsAvailable to true). Built-in catalog rows (<c>Projects</c> with no institute) are copied into <c>InstituteProjects</c>.
    /// Query <paramref name="instituteProject"/> when <paramref name="id"/> refers to <see cref="InstituteProject"/> (custom / activated copy).
    /// </summary>
    [HttpPost("use/activate/{id}")]
    public async Task<ActionResult<object>> ActivateProject(int id, [FromQuery] bool instituteProject = false)
    {
        try
        {
            var auth = await ResolveInstituteIdFromAuthContextAsync();
            if (!auth.HasValue || auth.Value <= 0)
            {
                return Unauthorized("Institute authentication is required to activate this project.");
            }

            var instActivate = auth.Value;

            if (instituteProject)
            {
                var ip = await _context.InstituteProjects.FirstOrDefaultAsync(x => x.Id == id && x.InstituteId == instActivate);
                if (ip == null)
                {
                    return NotFound($"Institute project with ID {id} was not found.");
                }

                var checkIp = await BuildProjectReadyValidationForInstituteProjectRowAsync(id, instActivate);
                if (!checkIp.IsReady)
                {
                    return BadRequest(new
                    {
                        error = "Project is not ready to be made available.",
                        missingRequirements = checkIp.MissingRequirements,
                    });
                }

                if (!await InstituteProjectHasSyllabusForInstituteProjectRowAsync(id, instActivate))
                {
                    return Ok(new
                    {
                        success = false,
                        title = "Course assignment required",
                        detail =
                            "This project cannot be activated because no course is assigned yet. " +
                            "In Project Designs, open the General tab and assign a course in the Course field, then try activating again.",
                    });
                }

                ip.IsAvailable = true;
                ip.UpdatedAt = DateTime.UtcNow;

                // Always recompute coupon on activation so it reflects the current active course:
                // InstituteName-SquadId when a template squad exists, otherwise InstituteId.
                {
                    var activeTemplateSquadId = await _context.InstituteTemplates
                        .AsNoTracking()
                        .Where(t => t.InstituteId == instActivate
                                    && (t.InstituteProjectId == ip.Id
                                        || (ip.BaseProjectId.HasValue && t.ProjectId == ip.BaseProjectId.Value))
                                    && t.IsActive
                                    && t.SquadId != null)
                        .Select(t => t.SquadId)
                        .FirstOrDefaultAsync();

                    if (activeTemplateSquadId.HasValue)
                    {
                        var instituteName = await _context.Institutes
                            .AsNoTracking()
                            .Where(i => i.Id == instActivate)
                            .Select(i => i.Name)
                            .FirstOrDefaultAsync() ?? instActivate.ToString();
                        ip.Coupon = instituteName.Replace(" ", "") + "-" + activeTemplateSquadId.Value;
                    }
                    else
                    {
                        ip.Coupon = instActivate.ToString();
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("InstituteProject {InstituteProjectId} activated successfully", id);
                return Ok(new { Success = true, Message = "Project activated successfully.", Id = id, InstituteProject = true, Coupon = ip.Coupon });
            }

            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found.");
            }

            if (project.InstituteId == null)
            {
                return await ActivateCatalogProjectIntoInstituteTableAsync(id, instActivate);
            }

            if (project.InstituteId != instActivate)
            {
                return Unauthorized("Institute authentication is required to activate this project.");
            }

            var check = await BuildProjectReadyValidationForInstituteProjectAsync(id, instActivate);
            if (!check.IsReady)
            {
                return BadRequest(new
                {
                    error = "Project is not ready to be made available.",
                    missingRequirements = check.MissingRequirements,
                });
            }

            if (!await InstituteProjectHasSyllabusTemplateAsync(id, instActivate))
            {
                return Ok(new
                {
                    success = false,
                    title = "Course assignment required",
                    detail =
                        "This project cannot be activated because no course is assigned yet. " +
                        "In Project Designs, open the General tab and assign a course in the Course field, then try activating again.",
                });
            }

            project.IsAvailable = true;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Project {ProjectId} activated successfully", id);
            return Ok(new { Success = true, Message = "Project activated successfully.", Id = id, InstituteProject = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating project {ProjectId}", id);
            return StatusCode(500, "An error occurred while activating the project");
        }
    }

    /// <summary>
    /// Deactivate a project.
    /// - For activated built-in copies (<see cref="InstituteProject.BaseProjectId"/> != null), deactivation removes the institute copy and its modules.
    /// - For custom institute projects, deactivation only sets <see cref="InstituteProject.IsAvailable"/> to false.
    /// Route: POST /api/Projects/use/deactivate/{id}?instituteProject=true
    /// </summary>
    [HttpPost("use/deactivate/{id:int}")]
    public async Task<ActionResult<object>> DeactivateProject(int id, [FromQuery] bool instituteProject = false)
    {
        try
        {
            var auth = await ResolveInstituteIdFromAuthContextAsync();
            if (!auth.HasValue || auth.Value <= 0)
            {
                return Unauthorized("Institute authentication is required to deactivate this project.");
            }

            var instituteId = auth.Value;

            if (instituteProject)
            {
                var ip = await _context.InstituteProjects
                    .FirstOrDefaultAsync(x => x.Id == id && x.InstituteId == instituteId);
                if (ip == null)
                {
                    return NotFound($"Institute project with ID {id} was not found.");
                }

                var shouldDeleteAsBuiltInMirror = ip.IsBuiltIn && ip.BaseProjectId.HasValue;

                if (shouldDeleteAsBuiltInMirror)
                {
                    var hasTemplates = await _context.InstituteTemplates
                        .AsNoTracking()
                        .AnyAsync(t => t.InstituteId == instituteId && t.InstituteProjectId == ip.Id);
                    if (hasTemplates)
                    {
                        ip.IsAvailable = false;
                        ip.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        return Ok(new { Success = true, Deleted = false, Id = id, InstituteProject = true });
                    }

                    var modules = await _context.InstituteProjectModules
                        .Where(m => m.InstituteProjectId == ip.Id)
                        .ToListAsync();
                    if (modules.Count > 0)
                    {
                        _context.InstituteProjectModules.RemoveRange(modules);
                    }
                    _context.InstituteProjects.Remove(ip);
                    await _context.SaveChangesAsync();
                    return Ok(new { Success = true, Deleted = true, Id = id, InstituteProject = true });
                }

                ip.IsAvailable = false;
                ip.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return Ok(new { Success = true, Deleted = false, Id = id, InstituteProject = true });
            }

            var baseProject = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.InstituteId == null && p.IsAvailable);
            if (baseProject == null)
            {
                return NotFound($"Catalog project with ID {id} was not found or is not available.");
            }

            var baseTitleNorm = NormalizeProjectTitleKey(baseProject.Title);
            var activatedCopies = await _context.InstituteProjects
                .Where(ip => ip.InstituteId == instituteId && ip.BaseProjectId == id && ip.IsAvailable)
                .OrderByDescending(ip => ip.UpdatedAt ?? ip.CreatedAt)
                .ThenByDescending(ip => ip.Id)
                .ToListAsync();
            var target = activatedCopies.FirstOrDefault(ip => NormalizeProjectTitleKey(ip.Title) == baseTitleNorm)
                ?? activatedCopies.FirstOrDefault();
            if (target == null)
            {
                return NotFound("This built-in project is not currently activated for your institute.");
            }

            var targetModules = await _context.InstituteProjectModules
                .Where(m => m.InstituteProjectId == target.Id)
                .ToListAsync();
            var hasTemplatesForTarget = await _context.InstituteTemplates
                .AsNoTracking()
                .AnyAsync(t => t.InstituteId == instituteId && t.InstituteProjectId == target.Id);
            if (hasTemplatesForTarget)
            {
                target.IsAvailable = false;
                target.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return Ok(new { Success = true, Deleted = false, Id = target.Id, InstituteProject = true, BaseProjectId = id });
            }
            if (targetModules.Count > 0)
            {
                _context.InstituteProjectModules.RemoveRange(targetModules);
            }
            _context.InstituteProjects.Remove(target);
            await _context.SaveChangesAsync();

            return Ok(new { Success = true, Deleted = true, Id = target.Id, InstituteProject = true, BaseProjectId = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating project {ProjectId}", id);
            return StatusCode(500, "An error occurred while deactivating the project");
        }
    }

    /// <summary>
    /// Update a project
    /// </summary>
    [HttpPost("use/update/{id}")]
    public async Task<ActionResult> UpdateProject(int id, [FromBody] UpdateProjectRequest request)
    {
        try
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found.");
            }

            // Update fields if provided
            if (!string.IsNullOrEmpty(request.Title))
                project.Title = request.Title;

            if (request.Description != null)
                project.Description = request.Description;

            if (request.ExtendedDescription != null)
                project.ExtendedDescription = request.ExtendedDescription;

            if (request.ShortBrief != null)
                project.ShortBrief = request.ShortBrief;

            if (request.Mission != null)
                project.Mission = request.Mission;

            if (request.OneLiner != null)
                project.OneLiner = request.OneLiner;

            if (!string.IsNullOrEmpty(request.Priority))
                project.Priority = request.Priority;

            if (request.IsAvailable.HasValue)
                project.IsAvailable = request.IsAvailable.Value;

            if (request.CriteriaIds != null)
                project.CriteriaIds = request.CriteriaIds;

            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Project {ProjectId} updated successfully", id);

            // Generate system design for the updated project
            try
            {
                _logger.LogInformation("Generating system design for updated project {ProjectId}", id);
                
                var systemDesignRequest = new SystemDesignRequest
                {
                    ProjectId = id,
                    ExtendedDescription = project.ExtendedDescription ?? project.Description ?? "",
                    CreatedBy = null,
                    TeamRoles = new List<RoleInfo>() // Empty for now since students aren't allocated yet
                };

                var systemDesignResponse = await _designDocumentService.CreateSystemDesignAsync(systemDesignRequest);
                
                if (systemDesignResponse != null && systemDesignResponse.Success)
                {
                    // Update the project with the generated system design
                    project.SystemDesign = systemDesignResponse.DesignDocument;
                    project.SystemDesignDoc = systemDesignResponse.DesignDocumentPdf;
                    project.UpdatedAt = DateTime.UtcNow;
                    
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("System design generated and saved for updated project {ProjectId}", id);
                }
                else
                {
                    _logger.LogWarning("Failed to generate system design for updated project {ProjectId}: {Message}", 
                        id, systemDesignResponse?.Message ?? "Unknown error");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating system design for updated project {ProjectId}: {Message}", 
                    id, ex.Message);
                // Don't fail the project update if system design generation fails
            }

            return Ok(new { Success = true, Message = "Project updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project {ProjectId}", id);
            return StatusCode(500, "An error occurred while updating the project");
        }
    }

    /// <summary>
    /// Get system design JSON content for a specific project
    /// </summary>
    [HttpGet("use/DesignContent/{id}")]
    public async Task<ActionResult<string>> GetProjectDesignContent(int id)
    {
        try
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found.");
            }

            if (string.IsNullOrEmpty(project.SystemDesign))
            {
                return NotFound($"No system design content found for project {id}.");
            }

            _logger.LogInformation("Retrieved system design content for project {ProjectId}", id);
            return Ok(project.SystemDesign);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system design content for project {ProjectId}", id);
            return StatusCode(500, "An error occurred while retrieving the system design content");
        }
    }

    /// <summary>
    /// Get system design document (PDF) for a specific project
    /// </summary>
    [HttpGet("use/DesignDocument/{id}")]
    public async Task<ActionResult> GetProjectDesignDocument(int id)
    {
        try
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found.");
            }

            if (project.SystemDesignDoc == null || project.SystemDesignDoc.Length == 0)
            {
                return NotFound($"No system design document found for project {id}.");
            }

            _logger.LogInformation("Retrieved system design document for project {ProjectId}", id);
            
            // Return the PDF document
            return File(project.SystemDesignDoc, "application/pdf", $"project_{id}_system_design.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system design document for project {ProjectId}", id);
            return StatusCode(500, "An error occurred while retrieving the system design document");
        }
    }

    /// <summary>
    /// Get "Hot Projects" for a student - projects that would cause immediate kickoff if the student selects them
    /// Returns a comma-separated string of project IDs
    /// </summary>
    /// <param name="studentId">The student ID to check hot projects for</param>
    /// <returns>Comma-separated string of project IDs that would cause kickoff if selected</returns>
    [HttpGet("use/hot-projects/{studentId}")]
    public async Task<ActionResult<string>> GetHotProjects(int studentId)
    {
        try
        {
            _logger.LogInformation("Getting hot projects for student {StudentId}", studentId);

            // Get the student
            var student = await _context.Students
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
            {
                _logger.LogWarning("Student {StudentId} not found", studentId);
                return NotFound($"Student with ID {studentId} not found");
            }

            // Get all available projects (or projects that don't already have kickoff)
            var availableProjects = await _context.Projects
                .Where(p => p.IsAvailable && (p.Kickoff == null || p.Kickoff == false)) // Only projects that are available and not already kicked off
                .Select(p => p.Id)
                .ToListAsync();

            _logger.LogInformation("Found {Count} available projects to check for student {StudentId}", availableProjects.Count, studentId);

            var hotProjectIds = new List<int>();

            foreach (var projectId in availableProjects)
            {
                // Get all students with Status < 2 who have this project in ProjectPriorityX (excluding our student)
                var studentsWithProjectPriority = await _context.Students
                    .Include(s => s.StudentRoles)
                        .ThenInclude(sr => sr.Role)
                    .Where(s => s.Id != studentId &&
                               s.Status.HasValue &&
                               s.Status < 2 &&
                               (s.ProjectPriority1 == projectId ||
                                s.ProjectPriority2 == projectId ||
                                s.ProjectPriority3 == projectId ||
                                s.ProjectPriority4 == projectId))
                    .Select(s => s.Id)
                    .ToListAsync();

                // Simulate adding the student to this project
                var simulatedStudentIds = studentsWithProjectPriority.ToList();
                if (!simulatedStudentIds.Contains(studentId))
                {
                    simulatedStudentIds.Add(studentId);
                }

                // Check if kickoff would be met with this student added
                // Note: We simulate that these students would be allocated (ProjectId would be set)
                // by using KickoffService which checks students with ProjectId == projectId
                // So we need to simulate the scenario where these students have ProjectId set
                // But KickoffService expects students already allocated with ProjectId
                
                // Actually, we need to check: if these students (with ProjectPriorityX) were to be allocated,
                // would kickoff be met? The KickoffService checks students with ProjectId set.
                // So we simulate by temporarily checking if kickoff rules would be met.
                
                // For simulation, we'll check the kickoff rules manually similar to KickoffService
                // but adapted for the ProjectPriorityX scenario
                
                var allSimulatedStudents = await _context.Students
                    .Include(s => s.StudentRoles)
                        .ThenInclude(sr => sr.Role)
                    .Where(s => simulatedStudentIds.Contains(s.Id))
                    .ToListAsync();

                // Check kickoff rules (similar to KickoffService logic)
                bool wouldKickoff = true;

                // Rule 1: Minimum number of students
                if (allSimulatedStudents.Count < _kickoffConfig.MinimumStudents)
                {
                    wouldKickoff = false;
                    _logger.LogDebug("Project {ProjectId}: Would NOT kickoff - only {Count} students (need {Min})", 
                        projectId, allSimulatedStudents.Count, _kickoffConfig.MinimumStudents);
                    continue;
                }

                // Rule 2: Admin requirement
                if (_kickoffConfig.RequireAdmin)
                {
                    bool hasAdmin = allSimulatedStudents.Any(s => s.IsAdmin);
                    if (!hasAdmin)
                    {
                        wouldKickoff = false;
                        _logger.LogDebug("Project {ProjectId}: Would NOT kickoff - no admin", projectId);
                        continue;
                    }
                }

                // Rule 3: UI/UX Designer requirement (exactly 1)
                if (_kickoffConfig.RequireUIUXDesigner)
                {
                    var uiuxCount = allSimulatedStudents.Count(s =>
                        s.StudentRoles.Any(sr => sr.IsActive && sr.Role?.Type == 3));
                    
                    if (uiuxCount != 1)
                    {
                        wouldKickoff = false;
                        _logger.LogDebug("Project {ProjectId}: Would NOT kickoff - UI/UX count is {Count} (need exactly 1)", 
                            projectId, uiuxCount);
                        continue;
                    }
                }

                // Rule 4: Product Manager requirement (exactly 1)
                if (_kickoffConfig.RequireProductManager)
                {
                    var pmCount = allSimulatedStudents.Count(s =>
                        s.StudentRoles.Any(sr => sr.IsActive && sr.Role?.Type == 4));
                    
                    if (pmCount != 1)
                    {
                        wouldKickoff = false;
                        _logger.LogDebug("Project {ProjectId}: Would NOT kickoff - Product Manager count is {Count} (need exactly 1)", 
                            projectId, pmCount);
                        continue;
                    }
                }

                // Rule 5: Developer rule
                if (_kickoffConfig.RequireDeveloperRule)
                {
                    var developers = allSimulatedStudents.Count(s =>
                        s.StudentRoles.Any(sr => sr.IsActive && sr.Role?.Type == 1));
                    var juniorDevelopers = allSimulatedStudents.Count(s =>
                        s.StudentRoles.Any(sr => sr.IsActive && sr.Role?.Type == 2));

                    bool developerRuleMet = developers >= 1 || juniorDevelopers >= 2;

                    if (!developerRuleMet)
                    {
                        wouldKickoff = false;
                        _logger.LogDebug("Project {ProjectId}: Would NOT kickoff - Developer rule not met (Devs: {Devs}, JuniorDevs: {JuniorDevs})", 
                            projectId, developers, juniorDevelopers);
                        continue;
                    }
                }

                if (wouldKickoff)
                {
                    hotProjectIds.Add(projectId);
                    _logger.LogInformation("Project {ProjectId} is HOT for student {StudentId} - would cause kickoff", projectId, studentId);
                }
            }

            var result = hotProjectIds.Count > 0 
                ? string.Join(",", hotProjectIds.OrderBy(id => id))
                : "";

            _logger.LogInformation("Found {Count} hot projects for student {StudentId}: [{Projects}]", 
                hotProjectIds.Count, studentId, result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving hot projects for student {StudentId}", studentId);
            return StatusCode(500, "An error occurred while retrieving hot projects");
        }
    }

    /// <summary>
    /// When filtering peers for a candidate role: full-stack labels exclude every role whose name contains &quot;Developer&quot;;
    /// any developer-role label excludes full-stack-style labels (Full Stack / Fullstack / FullStack + Developer).
    /// </summary>
    private static bool ShouldExcludePeerByDeveloperRule(string? candidateRoleName, string? peerRoleName)
    {
        if (string.IsNullOrWhiteSpace(candidateRoleName))
            return false;

        bool candidateIsFullStack = RoleNameIsFullStackDeveloperLabel(candidateRoleName);
        bool candidateIsDeveloper = RoleNameContainsDeveloperWord(candidateRoleName);

        if (candidateIsFullStack && RoleNameContainsDeveloperWord(peerRoleName))
            return true;

        if (candidateIsDeveloper && RoleNameIsFullStackDeveloperLabel(peerRoleName))
            return true;

        return false;
    }

    private static bool RoleNameContainsDeveloperWord(string? roleName)
    {
        return !string.IsNullOrEmpty(roleName)
               && roleName.Contains("developer", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Matches &quot;Full Stack Developer&quot;, &quot;Fullstack Developer&quot;, &quot;FullStack Developer&quot;, and similar (case/spacing insensitive).</summary>
    private static bool RoleNameIsFullStackDeveloperLabel(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName) || !RoleNameContainsDeveloperWord(roleName))
            return false;

        if (roleName.Contains("full stack", StringComparison.OrdinalIgnoreCase))
            return true;

        var compact = string.Concat(roleName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Contains("fullstack", StringComparison.OrdinalIgnoreCase);
    }

}

