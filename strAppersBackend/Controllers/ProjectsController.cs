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
        IAzureBlobStorageService azureBlobStorage)
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
        var hasCustomTemplate = await _context.InstituteTemplates
            .AsNoTracking()
            .AnyAsync(
                t => t.InstituteId == instituteId && t.ProjectId == projectId,
                cancellationToken);
        if (hasCustomTemplate)
        {
            return true;
        }

        // "System" option for copied projects: backed by Project.TrelloBoardJson.
        return await _context.Projects
            .AsNoTracking()
            .AnyAsync(
                p => p.Id == projectId
                     && p.InstituteId == instituteId
                     && p.TrelloBoardJson != null
                     && p.TrelloBoardJson.Trim() != "",
                cancellationToken);
    }

    public sealed class ProjectTemplateDto
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? ProjectBrief { get; set; }
        public string? Name { get; set; }
        public string? TrelloBoardJson { get; set; }
    }

    public sealed class InstituteTemplateListItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
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

    /// <summary>
    /// Create a new institute project design from scratch (minimal defaults).
    /// Route: POST /api/Projects/use/by-institute/create-empty
    /// </summary>
    [HttpPost("use/by-institute/create-empty")]
    public async Task<ActionResult<Project>> CreateEmptyProjectDesignForInstitute()
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
                "CreateEmptyProjectDesign: saving new project InstituteId={InstituteId}, OrganizationId={OrganizationId}",
                instituteId.Value,
                organizationId);

            var project = new Project
            {
                Title = "New Project Design",
                OrganizationId = organizationId,
                InstituteId = instituteId.Value,
                IsAvailable = false,
                Priority = "Medium",
                CreatedAt = DateTime.UtcNow
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            var hasModuleType2 = await _context.ModuleTypes
                .AsNoTracking()
                .AnyAsync(mt => mt.Id == 2);
            if (hasModuleType2)
            {
                _context.ProjectModules.Add(new ProjectModule
                {
                    ProjectId = project.Id,
                    ModuleType = 2,
                    Title = "Module 1",
                    Description = string.Empty,
                    Sequence = 1,
                });
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "CreateEmptyProjectDesign: created default module for ProjectId={ProjectId}",
                    project.Id);
            }
            else
            {
                _logger.LogWarning(
                    "CreateEmptyProjectDesign: ModuleType=2 not found; skipped default ProjectModule row for ProjectId={ProjectId}",
                    project.Id);
            }

            _logger.LogInformation(
                "CreateEmptyProjectDesign: saved ProjectId={ProjectId}, Title={Title}",
                project.Id,
                project.Title);

            return Ok(new Project
            {
                Id = project.Id,
                Title = project.Title,
                Mission = project.Mission,
                OneLiner = project.OneLiner,
                Description = project.Description,
                ExtendedDescription = project.ExtendedDescription,
                ShortBrief = project.ShortBrief,
                Priority = project.Priority,
                OrganizationId = project.OrganizationId,
                InstituteId = project.InstituteId,
                IsAvailable = project.IsAvailable,
                Kickoff = project.Kickoff,
                CriteriaIds = project.CriteriaIds,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
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
            return Ok(new { success = true, id });
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
    /// Get all available projects for the given institute.
    /// Includes global projects (InstituteId is null) and institute-owned projects.
    /// Route: GET /api/Projects/use/by-institute
    /// </summary>
    [HttpGet("use/by-institute")]
    public async Task<ActionResult<IEnumerable<Project>>> GetAvailableProjectsForInstitute()
    {
        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            var projects = await _context.Projects
                .AsNoTracking()
                .Where(p =>
                    p.InstituteId == instituteId.Value ||
                    (p.InstituteId == null && p.IsAvailable))
                .OrderBy(p => p.Title)
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
                    InstituteId = p.InstituteId,
                    IsAvailable = p.IsAvailable,
                    InUse = p.InUse,
                    Kickoff = p.Kickoff,
                    CriteriaIds = p.CriteriaIds,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();

            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving institute-scoped projects from auth context");
            return StatusCode(500, "An error occurred while retrieving projects.");
        }
    }

    /// <summary>
    /// Set project InUse flag for the current institute context.
    /// Route: POST /api/Projects/use/by-institute/set-use/{id}
    /// </summary>
    [HttpPost("use/by-institute/set-use/{id:int}")]
    public async Task<ActionResult> SetProjectInUseByInstitute(int id, [FromBody] SetProjectInUseRequest? request)
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
    public async Task<ActionResult<ProjectReadyValidationDto>> GetProjectReadyValidation([FromQuery] int projectId)
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
                return Ok(new ProjectReadyValidationDto
                {
                    IsReady = true,
                    MissingRequirements = new List<string>()
                });
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
    public async Task<ActionResult<ProjectHeaderDto>> GetProjectDesignHeader(int id)
    {
        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            var project = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value))
                .Select(p => new ProjectHeaderDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Logo = p.InstituteId == null
                        ? (p.Organization != null ? p.Organization.Logo : null)
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
        [FromBody] UpdateProjectHeaderRequest request)
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

            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value));

            if (project == null)
            {
                return NotFound($"Project with ID {id} not found for institute {instituteId.Value}.");
            }

            if (project.InstituteId == null)
            {
                return StatusCode(StatusCodes.Status403Forbidden, "System projects are read-only.");
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

            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value), cancellationToken);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found for this institute context.");
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
                    "UPLOAD_DESIGN_MARKER_v1 parse-failed: ProjectId={ProjectId}, FileName={FileName}, Ext={Ext}",
                    id,
                    file.FileName,
                    ext);
                return BadRequest("The file uploaded successfully, but we could not extract readable text. Please upload a text-based PDF, DOCX, or PPTX.");
            }

            project.SystemDesign = extractedText.Trim();
            project.SystemDesignDoc = bytes;
            project.SystemDesignFormatted = $"{UploadedDesignMarker}\nBlobUrl: {blobUrl}\nFileName: {file.FileName}\nUploadedAtUtc: {DateTime.UtcNow:O}";
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            if (blobUri != null)
            {
                await _azureBlobStorage.DeleteBlobIfExistsAsync(blobUri, cancellationToken);
            }

            _logger.LogInformation(
                "UPLOAD_DESIGN_MARKER_v1 success: ProjectId={ProjectId}, FileName={FileName}, TextChars={Chars}, BlobUrl={BlobUrl}",
                id,
                file.FileName,
                project.SystemDesign.Length,
                blobUrl);

            return Ok(new
            {
                success = true,
                message = "Design document uploaded, parsed, and saved successfully.\nUse the AI assistant to build your modules—tell it how many modules you want, and it will generate professional titles and module content based on this design.",
                blobUrl,
                parsedChars = project.SystemDesign.Length,
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

            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value), cancellationToken);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found for this institute context.");
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
                    "UPLOAD_CUSTOMER_STORY_MARKER_v1 parse-failed: ProjectId={ProjectId}, FileName={FileName}, Ext={Ext}",
                    id,
                    file.FileName,
                    ext);
                return BadRequest("The file uploaded successfully, but we could not extract readable text. Please upload a text-based PDF, DOCX, or PPTX.");
            }

            project.CustomerPastStory = extractedText.Trim();
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            if (blobUri != null)
            {
                await _azureBlobStorage.DeleteBlobIfExistsAsync(blobUri, cancellationToken);
            }

            _logger.LogInformation(
                "UPLOAD_CUSTOMER_STORY_MARKER_v1 success: ProjectId={ProjectId}, FileName={FileName}, TextChars={Chars}, BlobUrl={BlobUrl}",
                id,
                file.FileName,
                project.CustomerPastStory.Length,
                blobUrl);

            return Ok(new
            {
                success = true,
                message = "Customer context document uploaded and parsed successfully.",
                parsedChars = project.CustomerPastStory.Length,
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

    /// <summary>
    /// Get template options for the selected project in Project Designs header.
    /// Includes the built-in "System" option plus institute templates for this project.
    /// Route: GET /api/Projects/use/by-institute/templates/{id}
    /// </summary>
    [HttpGet("use/by-institute/templates/{id:int}")]
    public async Task<ActionResult<ProjectHeaderTemplatesResponseDto>> GetProjectDesignTemplates(int id)
    {
        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            var project = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value))
                .Select(p => new { p.Id, p.InstituteId, p.TrelloBoardJson })
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound($"Project with ID {id} not found for institute {instituteId.Value}.");
            }

            var instituteTemplates = await _context.InstituteTemplates
                .AsNoTracking()
                .Where(t => t.InstituteId == instituteId.Value && t.ProjectId == id)
                .OrderBy(t => t.Id)
                .Select(t => new TemplateOptionDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    IsActive = t.IsActive,
                })
                .ToListAsync();

            var isSystemProject = project.InstituteId == null;
            var hasSystemTemplate = !string.IsNullOrWhiteSpace(project.TrelloBoardJson);
            if (isSystemProject)
            {
                var firstActiveSystem = instituteTemplates.FirstOrDefault(t => t.IsActive);
                var templates = new List<TemplateOptionDto>
                {
                    new()
                    {
                        Id = 0,
                        Name = "System",
                        IsActive = firstActiveSystem == null,
                    },
                };
                templates.AddRange(instituteTemplates);

                return Ok(new ProjectHeaderTemplatesResponseDto
                {
                    Templates = templates,
                    ActiveTemplateId = firstActiveSystem?.Id ?? 0,
                    ActiveTemplateName = firstActiveSystem?.Name ?? "System",
                });
            }

            var firstActive = instituteTemplates.FirstOrDefault(t => t.IsActive) ?? instituteTemplates.FirstOrDefault();
            var templatesForProject = instituteTemplates;
            if (hasSystemTemplate)
            {
                templatesForProject = new List<TemplateOptionDto>
                {
                    new()
                    {
                        Id = 0,
                        Name = "System",
                        IsActive = firstActive == null
                    }
                };
                templatesForProject.AddRange(instituteTemplates);
            }

            return Ok(new ProjectHeaderTemplatesResponseDto
            {
                Templates = templatesForProject,
                ActiveTemplateId = firstActive?.Id ?? (hasSystemTemplate ? 0 : 0),
                ActiveTemplateName = firstActive?.Name ?? (hasSystemTemplate ? "System" : string.Empty),
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
    public async Task<ActionResult<object>> SetProjectDesignActiveTemplate(int id, int templateId)
    {
        try
        {
            var instituteId = await ResolveInstituteIdFromAuthContextAsync();
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return Unauthorized("Institute authentication context is missing or invalid.");
            }

            var project = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == id && (p.InstituteId == null || p.InstituteId == instituteId.Value))
                .Select(p => new { p.Id, p.InstituteId, p.TrelloBoardJson })
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound($"Project with ID {id} not found for institute {instituteId.Value}.");
            }

            var isSystemProject = project.InstituteId == null;
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
                ActiveTemplateName = selectedTemplate?.Name ?? "System",
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
    [HttpGet("use/templates/list")]
    public async Task<ActionResult<IEnumerable<InstituteTemplateListItemDto>>> GetInstituteTemplatesListForProject(
        [FromQuery] int projectId,
        [FromQuery] int instituteId)
    {
        if (projectId <= 0 || instituteId <= 0)
        {
            return BadRequest("projectId and instituteId must be positive integers.");
        }

        try
        {
            var items = await _context.InstituteTemplates
                .AsNoTracking()
                .Where(t => t.ProjectId == projectId && t.InstituteId == instituteId)
                .OrderByDescending(t => t.Id)
                .Select(t => new InstituteTemplateListItemDto { Id = t.Id, Name = t.Name })
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
    /// Get the saved Trello template JSON for a project.
    /// Uses <see cref="Project.TrelloBoardJson"/> unless <paramref name="instituteTemplateId"/> targets a row in
    /// <c>InstituteTemplates</c>, or unless <paramref name="instituteId"/> is set without <paramref name="instituteTemplateId"/> — then the latest institute row for that project + institute is returned.
    /// </summary>
    [HttpGet("use/templates")]
    public async Task<ActionResult<ProjectTemplateDto>> GetProjectTemplateByProjectId(
        [FromQuery] int projectId,
        [FromQuery] int? instituteId = null,
        [FromQuery] int? instituteTemplateId = null)
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
            var project = await _context.Projects
                .Where(p => p.Id == projectId)
                .Select(p => new ProjectTemplateDto
                {
                    ProjectId = p.Id,
                    ProjectName = p.Title,
                    ProjectBrief = p.Description,
                    TrelloBoardJson = p.TrelloBoardJson
                })
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found.");
            }

            if (instituteTemplateId is > 0)
            {
                var byId = await _context.InstituteTemplates
                    .AsNoTracking()
                    .Where(t =>
                        t.Id == instituteTemplateId.Value &&
                        t.ProjectId == projectId &&
                        t.InstituteId == instituteId!.Value)
                    .Select(t => new { t.TrelloBoardJson, t.Name })
                    .FirstOrDefaultAsync();

                if (byId == null)
                {
                    return NotFound(
                        $"Institute template {instituteTemplateId.Value} not found for project {projectId} and institute {instituteId}.");
                }

                project.TrelloBoardJson = byId.TrelloBoardJson;
                project.Name = byId.Name;
            }
            else if (instituteId is > 0)
            {
                var instituteRow = await _context.InstituteTemplates
                    .AsNoTracking()
                    .Where(t => t.ProjectId == projectId && t.InstituteId == instituteId.Value)
                    .OrderByDescending(t => t.Id)
                    .Select(t => new { t.TrelloBoardJson, t.Name })
                    .FirstOrDefaultAsync();

                if (instituteRow != null)
                {
                    project.TrelloBoardJson = instituteRow.TrelloBoardJson;
                    project.Name = instituteRow.Name;
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
    /// Insert: omit <see cref="AddInstituteTemplateRequest.InstituteTemplateId"/> and send a unique <see cref="AddInstituteTemplateRequest.Name"/> (per institute + project, case-insensitive).
    /// Update: set <see cref="AddInstituteTemplateRequest.InstituteTemplateId"/> to an existing row id for this institute + project; optional <see cref="AddInstituteTemplateRequest.Name"/> renames if unique.
    /// </summary>
    [HttpPost("use/add-template")]
    public async Task<ActionResult<object>> AddInstituteTemplate(
        [FromQuery] int projectId,
        [FromQuery] int instituteId,
        [FromBody] AddInstituteTemplateRequest request)
    {
        if (projectId <= 0 || instituteId <= 0)
        {
            return BadRequest("projectId and instituteId must be positive integers.");
        }

        if (request == null || string.IsNullOrWhiteSpace(request.TrelloBoardJson))
        {
            return BadRequest("Request body must include a non-empty trelloBoardJson string.");
        }

        try
        {
            var instituteExists = await _context.Institutes.AnyAsync(i => i.Id == instituteId);
            if (!instituteExists)
            {
                return NotFound($"Institute with ID {instituteId} not found.");
            }

            var project = await _context.Projects.AsNoTracking()
                .Where(p => p.Id == projectId)
                .Select(p => new { p.Id, p.Title })
                .FirstOrDefaultAsync();
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found.");
            }

            var json = request.TrelloBoardJson.Trim();

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
                        t.ProjectId == projectId);

                if (row == null)
                {
                    return NotFound(
                        $"Institute template {request.InstituteTemplateId.Value} not found for this institute and project.");
                }

                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    var newName = ClampName(request.Name);
                    var taken = await _context.InstituteTemplates.AsNoTracking()
                        .AnyAsync(t =>
                            t.InstituteId == instituteId &&
                            t.ProjectId == projectId &&
                            t.Id != row.Id &&
                            t.Name.ToLower() == newName.ToLower());
                    if (taken)
                    {
                        return Conflict($"A template named \"{newName}\" already exists for this project.");
                    }

                    row.Name = newName;
                }

                row.TrelloBoardJson = json;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Updated InstituteTemplates row {Id} for InstituteId={InstituteId}, ProjectId={ProjectId}",
                    row.Id, instituteId, projectId);

                return Ok(new
                {
                    Success = true,
                    Id = row.Id,
                    InstituteId = instituteId,
                    ProjectId = projectId,
                    Name = row.Name,
                    Updated = true,
                });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Name is required when creating a new institute template.");
            }

            var insertName = ClampName(request.Name!);
            var insertNameTaken = await _context.InstituteTemplates.AsNoTracking()
                .AnyAsync(t =>
                    t.InstituteId == instituteId &&
                    t.ProjectId == projectId &&
                    t.Name.ToLower() == insertName.ToLower());
            if (insertNameTaken)
            {
                return Conflict($"A template named \"{insertName}\" already exists for this project.");
            }

            var newRow = new InstituteTemplate
            {
                InstituteId = instituteId,
                ProjectId = projectId,
                Name = insertName,
                TrelloBoardJson = json
            };
            _context.InstituteTemplates.Add(newRow);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created InstituteTemplates row {Id} for InstituteId={InstituteId}, ProjectId={ProjectId}",
                newRow.Id, instituteId, projectId);

            return Ok(new
            {
                Success = true,
                Id = newRow.Id,
                InstituteId = instituteId,
                ProjectId = projectId,
                Name = newRow.Name,
                Updated = false,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving Trello template for InstituteId={InstituteId}, ProjectId={ProjectId}", instituteId, projectId);
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

            // Validate project exists
            var projectExists = await _context.Projects.AnyAsync(p => p.Id == id);
            if (!projectExists)
            {
                _logger.LogWarning("Project {ProjectId} not found", id);
                return NotFound($"Project with ID {id} not found.");
            }

            // Get students where any ProjectPriority field equals projectId AND Status < 2
            var students = await _context.Students
                .Include(s => s.Major)
                .Include(s => s.Year)
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .Where(s => s.Status.HasValue && s.Status < 2 && (
                    s.ProjectPriority1 == id ||
                    s.ProjectPriority2 == id ||
                    s.ProjectPriority3 == id ||
                    s.ProjectPriority4 == id
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
    public async Task<ActionResult<Project>> GetProjectById(int id)
    {
        try
        {
            _logger.LogInformation("Getting project with ID {ProjectId}", id);

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
                    Kickoff = p.Kickoff,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                    // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                })
                .FirstOrDefaultAsync();

            if (project == null)
            {
                _logger.LogWarning("Project with ID {ProjectId} not found", id);
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
    /// Duplicate an existing project for an institute.
    /// New project fields:
    /// - Title = "{source.Title}-Copy" (single-token copy suffix for Project Designs one-word naming)
    /// - OrganizationId = 100 if that organization exists, otherwise null (same as create-empty; avoids FK errors)
    /// - InstituteId = from auth context
    /// Route: POST /api/Projects/use/duplicate/{id}
    /// </summary>
    [HttpPost("use/duplicate/{id:int}")]
    public async Task<ActionResult<Project>> DuplicateProject(int id, [FromBody] DuplicateProjectRequest request)
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
                    "DuplicateProject: Organizations.Id={PreferredOrgId} not found — inserting with OrganizationId=null (avoid FK violation). SourceProjectId={SourceId}, InstituteId={InstituteId}",
                    preferredOrganizationId,
                    id,
                    instituteId);
            }

            var source = await _context.Projects
                .AsNoTracking()
                .Include(p => p.Organization)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (source == null)
            {
                return NotFound($"Project with ID {id} not found.");
            }

            var sourceModules = await _context.ProjectModules
                .AsNoTracking()
                .Where(pm => pm.ProjectId == source.Id)
                .OrderBy(pm => pm.Sequence ?? int.MaxValue)
                .ThenBy(pm => pm.Id)
                .ToListAsync();
            _logger.LogInformation(
                "DuplicateProject: SourceProjectId={SourceProjectId}, SourceModulesCount={SourceModulesCount}",
                source.Id,
                sourceModules.Count);

            const int titleLimit = 200;
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
            var existingTitles = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Title != null)
                .Select(p => p.Title!)
                .ToListAsync();

            static string BuildCopyTitle(string baseTitle, int suffixNumber, int limit)
            {
                var suffix = suffixNumber <= 1 ? "-Copy" : $"-Copy{suffixNumber}";
                var maxBaseLen = Math.Max(1, limit - suffix.Length);
                var trimmedBase = baseTitle.Length > maxBaseLen ? baseTitle[..maxBaseLen] : baseTitle;
                return $"{trimmedBase}{suffix}";
            }

            var existingSet = new HashSet<string>(existingTitles, StringComparer.OrdinalIgnoreCase);
            var suffixNumber = 1;
            var copyTitle = BuildCopyTitle(sourceTitle, suffixNumber, titleLimit);
            while (existingSet.Contains(copyTitle))
            {
                suffixNumber++;
                copyTitle = BuildCopyTitle(sourceTitle, suffixNumber, titleLimit);
            }

            var duplicate = new Project
            {
                Title = copyTitle,
                Mission = source.Mission,
                OneLiner = source.OneLiner,
                Description = source.Description,
                ExtendedDescription = source.ExtendedDescription,
                Logo = source.InstituteId == null
                    ? (source.Organization != null ? source.Organization.Logo : source.Logo)
                    : source.Logo,
                SystemDesign = source.SystemDesign,
                DataSchema = source.DataSchema,
                SystemDesignDoc = source.SystemDesignDoc,
                SystemDesignFormatted = source.SystemDesignFormatted,
                Priority = source.Priority,
                InstituteId = instituteId,
                IsAvailable = false,
                Kickoff = source.Kickoff,
                TrelloBoardJson = source.TrelloBoardJson,
                CustomerPastStory = source.CustomerPastStory,
                ShortBrief = source.ShortBrief,
                DeploymentManifest = source.DeploymentManifest,
                IdeGenerationStatus = source.IdeGenerationStatus,
                TotalChunks = source.TotalChunks,
                CompletedChunks = source.CompletedChunks,
                MockRecordsCount = source.MockRecordsCount,
                CriteriaIds = source.CriteriaIds,
                OrganizationId = organizationId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };

            _context.Projects.Add(duplicate);
            await _context.SaveChangesAsync();

            var oldToNewModuleIdMap = new Dictionary<int, int>();
            if (sourceModules.Count > 0)
            {
                var duplicatedModules = sourceModules.Select(pm => new ProjectModule
                {
                    ProjectId = duplicate.Id,
                    ModuleType = pm.ModuleType,
                    Title = pm.Title,
                    Description = pm.Description,
                    Sequence = pm.Sequence,
                    OriginalModuleId = pm.Id,
                }).ToList();
                _context.ProjectModules.AddRange(duplicatedModules);
                await _context.SaveChangesAsync();

                // Build old -> new from OriginalModuleId (reliable; not dependent on sort order / index alignment).
                var newModuleRows = await _context.ProjectModules
                    .AsNoTracking()
                    .Where(pm => pm.ProjectId == duplicate.Id && pm.OriginalModuleId != null)
                    .ToListAsync();
                if (newModuleRows.Count != sourceModules.Count)
                {
                    _logger.LogWarning(
                        "DuplicateProject: new module row count with OriginalModuleId {NewCount} != source {SourceCount} for NewProjectId={NewProjectId}; Trello module-id remap may be incomplete.",
                        newModuleRows.Count,
                        sourceModules.Count,
                        duplicate.Id);
                }
                foreach (var row in newModuleRows)
                {
                    var fromId = row.OriginalModuleId!.Value;
                    if (fromId > 0 && row.Id > 0 && fromId != row.Id)
                    {
                        oldToNewModuleIdMap[fromId] = row.Id;
                    }
                }

                if (oldToNewModuleIdMap.Count == 0)
                {
                    _logger.LogWarning(
                        "DuplicateProject: module id map is empty after copy (source modules={SourceCount}, new rows={NewCount}) for NewProjectId={NewProjectId}; TrelloBoardJson will keep source module ids.",
                        sourceModules.Count,
                        newModuleRows.Count,
                        duplicate.Id);
                }

                _logger.LogInformation(
                    "DuplicateProject: module id map for Trello (NewProjectId={NewProjectId}): {MapDetails}",
                    duplicate.Id,
                    FormatModuleIdMapForLog(oldToNewModuleIdMap));
            }
            else
            {
                var hasModuleType2 = await _context.ModuleTypes
                    .AsNoTracking()
                    .AnyAsync(mt => mt.Id == 2);
                if (hasModuleType2)
                {
                    _context.ProjectModules.Add(new ProjectModule
                    {
                        ProjectId = duplicate.Id,
                        ModuleType = 2,
                        Title = "Module 1",
                        Description = string.Empty,
                        Sequence = 1,
                    });
                    await _context.SaveChangesAsync();
                    _logger.LogInformation(
                        "DuplicateProject: source had no modules; created default module for NewProjectId={NewProjectId}",
                        duplicate.Id);
                }
                else
                {
                    _logger.LogWarning(
                        "DuplicateProject: source had no modules and ModuleType=2 not found; skipped default ProjectModule row for ProjectId={ProjectId}",
                        duplicate.Id);
                }
            }

            if (!string.IsNullOrWhiteSpace(duplicate.TrelloBoardJson))
            {
                var jsonIn = duplicate.TrelloBoardJson!;
                _logger.LogInformation(
                    "DuplicateProject: TrelloBoardJson remap START NewProjectId={NewProjectId} sourceProjectId={SourceProjectId} inputLen={Len} mapSize={MapSize} map=[{Map}]",
                    duplicate.Id,
                    id,
                    jsonIn.Length,
                    oldToNewModuleIdMap.Count,
                    FormatModuleIdMapForLog(oldToNewModuleIdMap));
                var updatedJson = TryRemapTrelloJsonAfterProjectDuplicate(
                    jsonIn,
                    oldToNewModuleIdMap,
                    sourceProjectId: id,
                    newProjectId: duplicate.Id);
                if (string.IsNullOrWhiteSpace(updatedJson))
                {
                    _logger.LogWarning(
                        "DuplicateProject: TrelloBoardJson remapper returned empty; DB json NOT updated. NewProjectId={NewProjectId}",
                        duplicate.Id);
                }
                else if (string.Equals(updatedJson, jsonIn, StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "DuplicateProject: TrelloBoardJson remapper made NO string change — DB still has source ids. NewProjectId={NewProjectId} sourceProjectId={Source} mapSize={MapSize} map=[{Map}] (see TryRemapTrelloJson RESIDUAL logs if any)",
                        duplicate.Id,
                        id,
                        oldToNewModuleIdMap.Count,
                        FormatModuleIdMapForLog(oldToNewModuleIdMap));
                }
                else
                {
                    duplicate.TrelloBoardJson = updatedJson;
                    duplicate.UpdatedAt = DateTime.UtcNow;
                    _context.Entry(duplicate).Property(p => p.TrelloBoardJson).IsModified = true;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation(
                        "DuplicateProject: TrelloBoardJson SAVED after remap. NewProjectId={NewProjectId} oldLen={OldLen} newLen={NewLen}",
                        duplicate.Id,
                        jsonIn.Length,
                        updatedJson.Length);
                }
            }
            else
            {
                _logger.LogInformation("DuplicateProject: TrelloBoardJson is empty; no remap. NewProjectId={NewProjectId}", duplicate.Id);
            }

            return Ok(new Project
            {
                Id = duplicate.Id,
                Title = duplicate.Title,
                Mission = duplicate.Mission,
                OneLiner = duplicate.OneLiner,
                Description = duplicate.Description,
                ExtendedDescription = duplicate.ExtendedDescription,
                Logo = duplicate.Logo,
                ShortBrief = duplicate.ShortBrief,
                Priority = duplicate.Priority,
                OrganizationId = duplicate.OrganizationId,
                InstituteId = duplicate.InstituteId,
                IsAvailable = duplicate.IsAvailable,
                InUse = duplicate.InUse,
                Kickoff = duplicate.Kickoff,
                CriteriaIds = duplicate.CriteriaIds,
                CreatedAt = duplicate.CreatedAt,
                UpdatedAt = duplicate.UpdatedAt
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
    /// Activate a project (set IsAvailable to true)
    /// </summary>
    [HttpPost("use/activate/{id}")]
    public async Task<ActionResult> ActivateProject(int id)
    {
        try
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found.");
            }

            if (project.InstituteId is int instActivate)
            {
                var auth = await ResolveInstituteIdFromAuthContextAsync();
                if (!auth.HasValue || auth.Value != instActivate)
                {
                    return Unauthorized("Institute authentication is required to activate this project.");
                }

                var check = await BuildProjectReadyValidationForInstituteProjectAsync(id, instActivate);
                if (!check.IsReady)
                {
                    return BadRequest(new
                    {
                        error = "Project is not ready to be made available.",
                        missingRequirements = check.MissingRequirements
                    });
                }

                if (!await InstituteProjectHasSyllabusTemplateAsync(id, instActivate))
                {
                    return BadRequest(new
                    {
                        title = "Syllabus template required",
                        detail =
                            "A syllabus template must be saved for this project before it can be made available. " +
                            "In Project Designs, open the General tab and select a template under Syllabus Template. " +
                            "If none are listed, go to the Syllabus templates tab, create a template for this project, " +
                            "return to the General tab to select it, and try again."
                    });
                }
            }

            project.IsAvailable = true;
            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Project {ProjectId} activated successfully", id);
            return Ok(new { Success = true, Message = "Project activated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating project {ProjectId}", id);
            return StatusCode(500, "An error occurred while activating the project");
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

