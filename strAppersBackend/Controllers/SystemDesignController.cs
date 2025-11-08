using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemDesignController : ControllerBase
{
    private readonly IDesignDocumentService _designDocumentService;
    private readonly IAIService _aiService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SystemDesignController> _logger;
    private readonly IConfiguration _configuration;

    public SystemDesignController(
        IDesignDocumentService designDocumentService,
        IAIService aiService,
        ApplicationDbContext context,
        ILogger<SystemDesignController> logger,
        IConfiguration configuration)
    {
        _designDocumentService = designDocumentService;
        _aiService = aiService;
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Generate a system design document for a project (for frontend use)
    /// </summary>
    [HttpPost("use/generate-design-document")]
    public async Task<ActionResult<SystemDesignResponse>> GenerateDesignDocument([FromForm] int projectId, [FromForm] string extendedDescription)
    {
        try
        {
            _logger.LogInformation("Generating design document for Project {ProjectId}", projectId);

            // Clean the extended description
            var cleanedDescription = extendedDescription
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Replace("\t", " ")
                .Replace("  ", " ")
                .Trim();

            // Create the request object
            var request = new SystemDesignRequest
            {
                ProjectId = projectId,
                ExtendedDescription = cleanedDescription,
                CreatedBy = null,
                TeamRoles = new List<RoleInfo>() // Will be populated from database
            };

            _logger.LogInformation("Processing request for Project {ProjectId}", request.ProjectId);

            // Validate the request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate project exists and get team roles
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

            if (project == null)
            {
                return NotFound($"Project with ID {request.ProjectId} not found");
            }

            // Build team roles from allocated students
            var teamRoles = new List<RoleInfo>(); // DISABLED - Project.Students removed
            // .SelectMany(s => s.StudentRoles)
            // .GroupBy(sr => sr.RoleId)
            // .Select(g => new RoleInfo
            // {
            //     RoleId = g.Key,
            //     RoleName = g.First().Role.Name,
            //     StudentCount = g.Count()
            // })
            // .ToList();

            // Update request with actual team roles
            request.TeamRoles = teamRoles;

            // Generate system design
            var result = await _designDocumentService.CreateSystemDesignAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            _logger.LogInformation("Design document generated successfully for Project {ProjectId}, Version {VersionId}", 
                request.ProjectId, result.DesignVersionId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating design document");
            return StatusCode(500, new SystemDesignResponse
            {
                Success = false,
                Message = "An unexpected error occurred while generating the design document"
            });
        }
    }

    /// <summary>
    /// Get project's current system design from Projects.SystemDesign field
    /// </summary>
    [HttpGet("use/project/{projectId}/latest-design")]
    public async Task<ActionResult<object>> GetLatestDesignVersion(int projectId)
    {
        try
        {
            // Get project with SystemDesign field
            var project = await _context.Projects
                .Where(p => p.Id == projectId)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.SystemDesign,
                    HasSystemDesign = !string.IsNullOrEmpty(p.SystemDesign),
                    CreatedAt = DateTime.UtcNow, // Since we don't have versioning, use current time
                    UpdatedAt = DateTime.UtcNow
                })
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            if (string.IsNullOrEmpty(project.SystemDesign))
            {
                return NotFound($"No system design found for project {projectId}");
            }

            _logger.LogInformation("Retrieved system design for Project {ProjectId} from Projects.SystemDesign", projectId);

            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system design for Project {ProjectId}", projectId);
            return StatusCode(500, "An error occurred while retrieving the system design");
        }
    }

    /// <summary>
    /// Get all design versions for a project (for frontend use)
    /// </summary>
    [HttpGet("use/project/{projectId}/design-versions")]
    [Obsolete("This method is disabled.")]
    public async Task<ActionResult<List<DesignVersion>>> GetDesignVersions(int projectId)
    {
        try
        {
            var designVersions = await _designDocumentService.GetDesignVersionsAsync(projectId);
            return Ok(designVersions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving design versions for Project {ProjectId}", projectId);
            return StatusCode(500, "An error occurred while retrieving the design versions");
        }
    }

    /// <summary>
    /// Get a specific design version by ID (for frontend use)
    /// </summary>
    [HttpGet("use/design-version/{designVersionId}")]
    [Obsolete("This method is disabled.")]
    public async Task<ActionResult<DesignVersion>> GetDesignVersionById(int designVersionId)
    {
        try
        {
            var designVersion = await _designDocumentService.GetDesignVersionByIdAsync(designVersionId);

            if (designVersion == null)
            {
                return NotFound($"Design version with ID {designVersionId} not found");
            }

            return Ok(designVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving design version {DesignVersionId}", designVersionId);
            return StatusCode(500, "An error occurred while retrieving the design version");
        }
    }

    /// <summary>
    /// Get project's current system design (for frontend use)
    /// </summary>
    [HttpGet("use/project/{projectId}/current-design")]
    [Obsolete("This method is disabled.")]
    public async Task<ActionResult<object>> GetCurrentSystemDesign(int projectId)
    {
        try
        {
            var project = await _context.Projects
                .Where(p => p.Id == projectId)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.SystemDesign,
                    HasSystemDesign = !string.IsNullOrEmpty(p.SystemDesign),
                    HasSystemDesignPdf = p.SystemDesignDoc != null
                })
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current system design for Project {ProjectId}", projectId);
            return StatusCode(500, "An error occurred while retrieving the system design");
        }
    }

    /// <summary>
    /// Generate a system design document for a project (alternative endpoint that accepts form data)
    /// </summary>
    [HttpPost("use/generate-design-document-form")]
    public async Task<ActionResult<SystemDesignResponse>> GenerateDesignDocumentForm([FromForm] int projectId, [FromForm] string extendedDescription)
    {
        try
        {
            _logger.LogInformation("Generating design document with form data for Project {ProjectId}", projectId);

            // Clean the extended description manually
            var cleanedDescription = extendedDescription
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Replace("\t", " ")
                .Replace("  ", " ")
                .Trim();

            // Create the request object
            var request = new SystemDesignRequest
            {
                ProjectId = projectId,
                ExtendedDescription = cleanedDescription,
                CreatedBy = null,
                TeamRoles = new List<RoleInfo>() // Will be populated from database
            };

            _logger.LogInformation("Processing request for Project {ProjectId}", request.ProjectId);

            // Validate the request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate project exists and get team roles
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

            if (project == null)
            {
                return NotFound($"Project with ID {request.ProjectId} not found");
            }

            // Build team roles from allocated students
            var teamRoles = new List<RoleInfo>(); // DISABLED - Project.Students removed
            // .SelectMany(s => s.StudentRoles)
            // .GroupBy(sr => sr.RoleId)
            // .Select(g => new RoleInfo
            // {
            //     RoleId = g.Key,
            //     RoleName = g.First().Role.Name,
            //     StudentCount = g.Count()
            // })
            // .ToList();

            // Update request with actual team roles
            request.TeamRoles = teamRoles;

            // Generate system design
            var result = await _designDocumentService.CreateSystemDesignAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            _logger.LogInformation("Design document generated successfully for Project {ProjectId}, Version {VersionId}", 
                request.ProjectId, result.DesignVersionId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating design document with raw JSON handling");
            return StatusCode(500, new SystemDesignResponse
            {
                Success = false,
                Message = "An unexpected error occurred while generating the design document"
            });
        }
    }

    /// <summary>
    /// Get the base64 encoded SystemDesign for a project (for frontend use)
    /// </summary>
    [HttpGet("use/base64/{projectId}")]
    public async Task<ActionResult<object>> GetSystemDesignBase64(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting SystemDesign base64 for Project {ProjectId}", projectId);

            // Validate project exists
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                _logger.LogWarning("Project {ProjectId} not found", projectId);
                return NotFound(new
                {
                    Success = false,
                    Message = $"Project with ID {projectId} not found"
                });
            }

            if (string.IsNullOrEmpty(project.SystemDesign))
            {
                _logger.LogWarning("No SystemDesign found for Project {ProjectId}", projectId);
                return NotFound(new
                {
                    Success = false,
                    Message = $"No SystemDesign found for project {projectId}"
                });
            }

            // Convert SystemDesign to base64
            var systemDesignBytes = System.Text.Encoding.UTF8.GetBytes(project.SystemDesign);
            var base64String = Convert.ToBase64String(systemDesignBytes);

            _logger.LogInformation("Successfully converted SystemDesign to base64 for Project {ProjectId}. Length: {Length}", 
                projectId, base64String.Length);

            return Ok(new
            {
                Success = true,
                ProjectId = projectId,
                ProjectTitle = project.Title,
                SystemDesignBase64 = base64String,
                OriginalLength = project.SystemDesign.Length,
                Base64Length = base64String.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SystemDesign base64 for Project {ProjectId}: {Message}", projectId, ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = $"An error occurred while getting SystemDesign base64: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Initiate modules for a project using AI
    /// </summary>
    [HttpPost("use/initiate-modules")]
    public async Task<ActionResult<InitiateModulesResponse>> InitiateModules([FromBody] InitiateModulesRequest request)
    {
        try
        {
            _logger.LogInformation("Initiating modules for Project {ProjectId}", request.ProjectId);

            // Validate project exists
            var project = await _context.Projects.FindAsync(request.ProjectId);
            if (project == null)
            {
                return NotFound(new InitiateModulesResponse
                {
                    Success = false,
                    Message = $"Project with ID {request.ProjectId} not found"
                });
            }

            // Get extended description from project
            var extendedDescription = project.ExtendedDescription ?? project.Description ?? "";
            if (string.IsNullOrEmpty(extendedDescription))
            {
                return BadRequest(new InitiateModulesResponse
                {
                    Success = false,
                    Message = "Project has no extended description available"
                });
            }

            // Get configuration values
            var maxModules = _configuration.GetValue<int>("SystemDesignAIAgent:MaxModules", 10);
            var minWordsPerModule = _configuration.GetValue<int>("SystemDesignAIAgent:MinWordsPerModule", 100);

            // Call AI service to generate modules with constraints
            var aiResponse = await _aiService.InitiateModulesAsync(request.ProjectId, extendedDescription, maxModules, minWordsPerModule);
            if (!aiResponse.Success)
            {
                return BadRequest(aiResponse);
            }

            // Save modules to database with 1-based sequence
            var savedModules = new List<ProjectModule>();
            var sequence = 1; // Start with 1-based index
            
            _logger.LogInformation("Processing {Count} modules for Project {ProjectId}", 
                aiResponse.Modules?.Count ?? 0, request.ProjectId);
            
            foreach (var module in aiResponse.Modules ?? new List<ModuleInfo>())
            {
                var projectModule = new ProjectModule
                {
                    ProjectId = request.ProjectId,
                    ModuleType = 2, // Screen type
                    Title = module.Title,
                    Description = $"{module.Description}\n\nInputs: {module.Inputs}\nOutputs: {module.Outputs}",
                    Sequence = sequence
                };

                _context.ProjectModules.Add(projectModule);
                savedModules.Add(projectModule);
                sequence++; // Increment for next module
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully initiated {Count} modules for Project {ProjectId}", 
                savedModules.Count, request.ProjectId);

            return Ok(new InitiateModulesResponse
            {
                Success = true,
                Modules = aiResponse.Modules
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating modules for Project {ProjectId}", request.ProjectId);
            return StatusCode(500, new InitiateModulesResponse
            {
                Success = false,
                Message = $"An error occurred while initiating modules: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Get the count of modules for a specific project
    /// </summary>
    [HttpGet("use/ModuleCount")]
    public async Task<ActionResult<ModuleCountResponse>> GetModuleCount([FromQuery] int projectId)
    {
        try
        {
            _logger.LogInformation("Getting module count for Project {ProjectId}", projectId);

            // Validate project exists
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound(new ModuleCountResponse
                {
                    Success = false,
                    Message = $"Project with ID {projectId} not found",
                    ModuleCount = 0
                });
            }

            // Get module count for the project
            var moduleCount = await _context.ProjectModules
                .Where(pm => pm.ProjectId == projectId)
                .CountAsync();

            _logger.LogInformation("Project {ProjectId} has {ModuleCount} modules", projectId, moduleCount);

            return Ok(new ModuleCountResponse
            {
                Success = true,
                ModuleCount = moduleCount,
                ProjectId = projectId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting module count for Project {ProjectId}", projectId);
            return StatusCode(500, new ModuleCountResponse
            {
                Success = false,
                Message = $"An error occurred while getting module count: {ex.Message}",
                ModuleCount = 0
            });
        }
    }

    /// <summary>
    /// Get the description of a specific module by project ID and sequence
    /// </summary>
    [HttpGet("use/ModuleDescription")]
    public async Task<ActionResult<ModuleDescriptionResponse>> GetModuleDescription([FromQuery] int projectId, [FromQuery] int sequence)
    {
        try
        {
            _logger.LogInformation("Getting module description for Project {ProjectId}, Sequence {Sequence}", projectId, sequence);

            // Validate project exists
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound(new ModuleDescriptionResponse
                {
                    Success = false,
                    Message = $"Project with ID {projectId} not found",
                    ModuleDescription = null
                });
            }

            // Get the specific module by project ID and sequence
            var module = await _context.ProjectModules
                .Where(pm => pm.ProjectId == projectId && pm.Sequence == sequence)
                .FirstOrDefaultAsync();

            if (module == null)
            {
                return NotFound(new ModuleDescriptionResponse
                {
                    Success = false,
                    Message = $"Module with sequence {sequence} not found for project {projectId}",
                    ModuleDescription = null
                });
            }

            _logger.LogInformation("Found module for Project {ProjectId}, Sequence {Sequence}: {Title}", 
                projectId, sequence, module.Title);

            return Ok(new ModuleDescriptionResponse
            {
                Success = true,
                ModuleDescription = new ModuleDescriptionInfo
                {
                    Id = module.Id,
                    ProjectId = module.ProjectId ?? 0,
                    Sequence = module.Sequence ?? 0,
                    Title = module.Title ?? string.Empty,
                    Description = module.Description ?? string.Empty,
                    ModuleType = module.ModuleType ?? 0
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting module description for Project {ProjectId}, Sequence {Sequence}", projectId, sequence);
            return StatusCode(500, new ModuleDescriptionResponse
            {
                Success = false,
                Message = $"An error occurred while getting module description: {ex.Message}",
                ModuleDescription = null
            });
        }
    }

    /// <summary>
    /// Refine a module description using AI without persisting changes
    /// </summary>
    [HttpGet("use/refine-module")]
    public async Task<ActionResult<UpdateModuleResponse>> RefineModule([FromQuery] int projectId, [FromQuery] int sequence, [FromQuery] string? userInput)
    {
        try
        {
            _logger.LogInformation("Refining module with sequence {Sequence} for Project {ProjectId}", sequence, projectId);

            if (string.IsNullOrWhiteSpace(userInput))
            {
                return BadRequest(new UpdateModuleResponse
                {
                    Success = false,
                    Message = "User input is required."
                });
            }

            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound(new UpdateModuleResponse
                {
                    Success = false,
                    Message = $"Project with ID {projectId} not found"
                });
            }

            var module = await _context.ProjectModules
                .FirstOrDefaultAsync(pm => pm.Sequence == sequence && pm.ProjectId == projectId);

            if (module == null)
            {
                return NotFound(new UpdateModuleResponse
                {
                    Success = false,
                    Message = $"Module with sequence {sequence} not found for project {projectId}"
                });
            }

            var currentDescription = module.Description ?? string.Empty;
            _logger.LogInformation("Current module description length: {Length}", currentDescription.Length);

            var cleanedUserInput = userInput.Replace("\r\n", "\n").Replace("\r", "\n");
            _logger.LogInformation("Cleaned user input length: {Length}", cleanedUserInput.Length);

            var aiResponse = await _aiService.UpdateModuleAsync(module.Id, currentDescription, cleanedUserInput);
            if (!aiResponse.Success)
            {
                return BadRequest(aiResponse);
            }

            return Ok(new UpdateModuleResponse
            {
                Success = true,
                UpdatedDescription = aiResponse.UpdatedDescription,
                Title = module.Title ?? string.Empty
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refining module with sequence {Sequence} for Project {ProjectId}", sequence, projectId);
            return StatusCode(500, new UpdateModuleResponse
            {
                Success = false,
                Message = $"An error occurred while refining module: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Update a module description directly
    /// </summary>
    [HttpPost("use/update-module")]
    public async Task<ActionResult<UpdateModuleResponse>> UpdateModule([FromBody] UpdateModuleApplyRequest request)
    {
        try
        {
            _logger.LogInformation("Updating module description directly for Project {ProjectId}, Sequence {Sequence}", request.ProjectId, request.Sequence);

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest(new UpdateModuleResponse
                {
                    Success = false,
                    Message = "Description is required."
                });
            }

            var project = await _context.Projects.FindAsync(request.ProjectId);
            if (project == null)
            {
                return NotFound(new UpdateModuleResponse
                {
                    Success = false,
                    Message = $"Project with ID {request.ProjectId} not found"
                });
            }

            var module = await _context.ProjectModules
                .FirstOrDefaultAsync(pm => pm.Sequence == request.Sequence && pm.ProjectId == request.ProjectId);

            if (module == null)
            {
                return NotFound(new UpdateModuleResponse
                {
                    Success = false,
                    Message = $"Module with sequence {request.Sequence} not found for project {request.ProjectId}"
                });
            }

            module.Description = request.Description;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully updated module description for Project {ProjectId}, Sequence {Sequence}", request.ProjectId, request.Sequence);

            return Ok(new UpdateModuleResponse
            {
                Success = true,
                UpdatedDescription = module.Description,
                Title = module.Title ?? string.Empty
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating module description directly for Project {ProjectId}, Sequence {Sequence}", request.ProjectId, request.Sequence);
            return StatusCode(500, new UpdateModuleResponse
            {
                Success = false,
                Message = $"An error occurred while updating module: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Bind all modules for a project into a single text content and store in Projects.SystemDesign
    /// </summary>
    [HttpPost("use/bind-modules")]
    public async Task<ActionResult<BindModulesResponse>> BindModules([FromBody] BindModulesRequest request)
    {
        try
        {
            _logger.LogInformation("Binding modules for Project {ProjectId}", request.ProjectId);

            // Validate project exists
            var project = await _context.Projects.FindAsync(request.ProjectId);
            if (project == null)
            {
                return NotFound(new BindModulesResponse
                {
                    Success = false,
                    Message = $"Project with ID {request.ProjectId} not found"
                });
            }

            // Get all modules for the project, ordered by sequence
            var modules = await _context.ProjectModules
                .Where(pm => pm.ProjectId == request.ProjectId)
                .OrderBy(pm => pm.Sequence)
                .ToListAsync();

            if (!modules.Any())
            {
                return NotFound(new BindModulesResponse
                {
                    Success = false,
                    Message = $"No modules found for project {request.ProjectId}"
                });
            }

            _logger.LogInformation("Found {Count} modules for Project {ProjectId}", modules.Count, request.ProjectId);

            var projectTitle = project.Title ?? "Untitled Project";
            var projectDescription = project.Description ?? "No description available";
            var projectExtendedDescription = project.ExtendedDescription ?? "No extended description available";

            if (request.ToTranslate)
            {
                if (!string.IsNullOrWhiteSpace(projectTitle))
                {
                    var titleTranslation = await _aiService.TranslateTextToEnglishAsync(projectTitle);
                    if (titleTranslation.Success && !string.IsNullOrWhiteSpace(titleTranslation.Text))
                    {
                        projectTitle = titleTranslation.Text;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to translate project title for Project {ProjectId}: {Message}", request.ProjectId, titleTranslation.Message);
                    }
                }

                if (!string.IsNullOrWhiteSpace(projectDescription))
                {
                    var descriptionTranslation = await _aiService.TranslateTextToEnglishAsync(projectDescription);
                    if (descriptionTranslation.Success && !string.IsNullOrWhiteSpace(descriptionTranslation.Text))
                    {
                        projectDescription = descriptionTranslation.Text;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to translate project description for Project {ProjectId}: {Message}", request.ProjectId, descriptionTranslation.Message);
                    }
                }

                if (!string.IsNullOrWhiteSpace(projectExtendedDescription))
                {
                    var extendedTranslation = await _aiService.TranslateTextToEnglishAsync(projectExtendedDescription);
                    if (extendedTranslation.Success && !string.IsNullOrWhiteSpace(extendedTranslation.Text))
                    {
                        projectExtendedDescription = extendedTranslation.Text;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to translate project extended description for Project {ProjectId}: {Message}", request.ProjectId, extendedTranslation.Message);
                    }
                }
            }

            // Build the bound content
            var boundContent = new StringBuilder();
            boundContent.AppendLine($"# System Design for {projectTitle}");
            boundContent.AppendLine();
            boundContent.AppendLine($"**Project Description:** {projectDescription}");
            boundContent.AppendLine();
            boundContent.AppendLine($"**Extended Description:** {projectExtendedDescription}");
            boundContent.AppendLine();
            boundContent.AppendLine("---");
            boundContent.AppendLine();

            // Group modules by type for better organization
            var screenModules = modules.Where(m => m.ModuleType == 2).OrderBy(m => m.Sequence).ToList();
            var dataModelModules = modules.Where(m => m.ModuleType == 1).OrderBy(m => m.Sequence).ToList();

            // Add screen modules first
            if (screenModules.Any())
            {
                boundContent.AppendLine("## System Modules");
                boundContent.AppendLine();

                for (int i = 0; i < screenModules.Count; i++)
                {
                    var module = screenModules[i];

                    var moduleTitle = module.Title ?? "Untitled Module";
                    var moduleDescription = module.Description ?? "No description available";

                    if (request.ToTranslate)
                    {
                        var translationResponse = await _aiService.TranslateModuleToEnglishAsync(moduleTitle, moduleDescription);
                        if (translationResponse.Success)
                        {
                            moduleTitle = translationResponse.Title ?? moduleTitle;
                            moduleDescription = translationResponse.Description ?? moduleDescription;
                        }
                        else
                        {
                            _logger.LogWarning("Failed to translate module {ModuleSequence} for Project {ProjectId}: {Message}", module.Sequence, request.ProjectId, translationResponse.Message);
                        }
                    }

                    boundContent.AppendLine($"### Module {i + 1}: {moduleTitle}");
                    boundContent.AppendLine();
                    boundContent.AppendLine(moduleDescription);
                    boundContent.AppendLine();
                    
                    // Add inputs and outputs if they exist in the description
                    if (!string.IsNullOrEmpty(moduleDescription) && moduleDescription.Contains("Inputs:") && moduleDescription.Contains("Outputs:"))
                    {
                        // Extract inputs and outputs from the description
                        var descriptionParts = moduleDescription.Split(new[] { "\n\nInputs:", "\n\nOutputs:" }, StringSplitOptions.None);
                        if (descriptionParts.Length >= 3)
                        {
                            boundContent.AppendLine($"**Inputs:** {descriptionParts[1].Trim()}");
                            boundContent.AppendLine();
                            boundContent.AppendLine($"**Outputs:** {descriptionParts[2].Trim()}");
                            boundContent.AppendLine();
                        }
                    }
                    
                    boundContent.AppendLine("---");
                    boundContent.AppendLine();
                }
            }

            // Add data model modules last (ModuleType 1)
            if (dataModelModules.Any())
            {
                boundContent.AppendLine("## Data Model");
                boundContent.AppendLine();

                for (int i = 0; i < dataModelModules.Count; i++)
                {
                    var module = dataModelModules[i];
                    boundContent.AppendLine($"### Data Model {i + 1}: {module.Title}");
                    boundContent.AppendLine();
                    boundContent.AppendLine(module.Description ?? "No description available");
                    boundContent.AppendLine();
                    boundContent.AppendLine("---");
                    boundContent.AppendLine();
                }
            }

            // Store the bound content in Projects.SystemDesign
            project.SystemDesign = boundContent.ToString();
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully bound {Count} modules for Project {ProjectId} and stored in SystemDesign", 
                modules.Count, request.ProjectId);

            return Ok(new BindModulesResponse
            {
                Success = true,
                Message = $"Successfully bound {modules.Count} modules into system design",
                BoundContent = boundContent.ToString(),
                ModuleCount = modules.Count,
                Title = projectTitle
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error binding modules for Project {ProjectId}", request.ProjectId);
            return StatusCode(500, new BindModulesResponse
            {
                Success = false,
                Message = $"An error occurred while binding modules: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Create data model for a project using AI
    /// </summary>
    [HttpPost("use/create-data-model")]
    public async Task<ActionResult<CreateDataModelResponse>> CreateDataModel([FromBody] CreateDataModelRequest request)
    {
        try
        {
            _logger.LogInformation("Creating data model for Project {ProjectId}", request.ProjectId);

            // Validate project exists
            var project = await _context.Projects.FindAsync(request.ProjectId);
            if (project == null)
            {
                return NotFound(new CreateDataModelResponse
                {
                    Success = false,
                    Message = $"Project with ID {request.ProjectId} not found"
                });
            }

            // Get all modules except DataModel type (type != 1)
            var modules = await _context.ProjectModules
                .Where(pm => pm.ProjectId == request.ProjectId && pm.ModuleType != 1)
                .Include(pm => pm.ModuleTypeNavigation)
                .ToListAsync();

            if (!modules.Any())
            {
                return BadRequest(new CreateDataModelResponse
                {
                    Success = false,
                    Message = "No modules found for this project. Please initiate modules first."
                });
            }

            // Format modules data for AI
            var modulesData = string.Join("\n\n", modules.Select(m => 
                $"Module: {m.Title}\nType: {m.ModuleTypeNavigation?.Name ?? "Unknown"}\nDescription: {m.Description}"));

            // Call AI service to generate data model
            var aiResponse = await _aiService.CreateDataModelAsync(request.ProjectId, modulesData);
            if (!aiResponse.Success)
            {
                return BadRequest(aiResponse);
            }

            // Save data model as a module with type = 1 (DataModel)
            var dataModelModule = new ProjectModule
            {
                ProjectId = request.ProjectId,
                ModuleType = 1, // DataModel type
                Title = "Database Schema",
                Description = aiResponse.SqlScript ?? ""
            };

            _context.ProjectModules.Add(dataModelModule);
            
            // Generate HTML schema and convert to PNG base64
            var htmlSchema = GenerateVisualSchema(aiResponse.SqlScript ?? "", project.Title ?? $"Project {request.ProjectId}");
            var pngBase64 = await ConvertHtmlToPngBase64(htmlSchema);
            project.DataSchema = pngBase64; // Store base64 PNG
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully created data model for Project {ProjectId}", request.ProjectId);

            return Ok(new CreateDataModelResponse
            {
                Success = true,
                SqlScript = aiResponse.SqlScript
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating data model for Project {ProjectId}", request.ProjectId);
            return StatusCode(500, new CreateDataModelResponse
            {
                Success = false,
                Message = $"An error occurred while creating data model: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Update data model using AI feedback
    /// </summary>
    [HttpPost("use/update-data-model/{projectId}")]
    public async Task<ActionResult<UpdateDataModelResponse>> UpdateDataModel(int projectId, [FromBody] UpdateDataModelRequest request)
    {
        try
        {
            _logger.LogInformation("Updating data model for Project {ProjectId}", projectId);
            _logger.LogInformation("User Input: {UserInput}", request.UserInput);

            // Validate project exists
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound(new UpdateDataModelResponse
                {
                    Success = false,
                    Message = $"Project with ID {projectId} not found"
                });
            }

            // Get the data model module (ModuleType = 1)
            var dataModelModule = await _context.ProjectModules
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.ModuleType == 1);

            if (dataModelModule == null)
            {
                return NotFound(new UpdateDataModelResponse
                {
                    Success = false,
                    Message = $"Data model not found for project {projectId}. Please create a data model first."
                });
            }

            // Get the current SQL script from the database
            var currentSqlScript = dataModelModule.Description ?? "";
            _logger.LogInformation("Current SQL script length: {Length}", currentSqlScript.Length);

            // Clean user input to handle newlines properly
            var cleanedUserInput = request.UserInput?.Replace("\r\n", "\n").Replace("\r", "\n") ?? "";
            _logger.LogInformation("Cleaned user input: {UserInput}", cleanedUserInput);

            // Call AI service to update the SQL script
            var aiResponse = await _aiService.UpdateDataModelAsync(projectId, currentSqlScript, cleanedUserInput);
            if (!aiResponse.Success)
            {
                return BadRequest(aiResponse);
            }

            // Clean the AI response to extract only the SQL script
            var cleanedSqlScript = CleanSqlFromAiResponse(aiResponse.UpdatedSqlScript ?? "");

            // Update the data model module description with the new SQL script
            dataModelModule.Description = cleanedSqlScript;
            
            // Generate HTML schema and convert to PNG base64
            var htmlSchema = GenerateVisualSchema(cleanedSqlScript, project.Title ?? $"Project {projectId}");
            var pngBase64 = await ConvertHtmlToPngBase64(htmlSchema);
            project.DataSchema = pngBase64; // Store base64 PNG
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully updated data model for Project {ProjectId}", projectId);

            return Ok(new UpdateDataModelResponse
            {
                Success = true,
                UpdatedSqlScript = cleanedSqlScript
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating data model for Project {ProjectId}", projectId);
            return StatusCode(500, new UpdateDataModelResponse
            {
                Success = false,
                Message = $"An error occurred while updating data model: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Get visual HTML schema for data model
    /// </summary>
    [HttpGet("use/data-schema/{projectId}")]
    public async Task<ActionResult<string>> GetDataSchema(int projectId)
    {
        try
        {
            _logger.LogInformation("Generating data schema for Project {ProjectId}", projectId);

            // Validate project exists
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            // Get the data model module (ModuleType = 1)
            var dataModelModule = await _context.ProjectModules
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.ModuleType == 1);

            if (dataModelModule == null)
            {
                return NotFound($"Data model not found for project {projectId}. Please create a data model first.");
            }

            // Get the SQL script from the database
            var sqlScript = dataModelModule.Description ?? "";
            if (string.IsNullOrEmpty(sqlScript))
            {
                return NotFound($"No SQL script found for project {projectId}");
            }

            _logger.LogInformation("Generating visual schema from SQL script of length {Length}", sqlScript.Length);

            // Generate the visual HTML schema
            var htmlSchema = GenerateVisualSchema(sqlScript, project.Title ?? $"Project {projectId}");

            return Content(htmlSchema, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating data schema for Project {ProjectId}", projectId);
            return StatusCode(500, $"An error occurred while generating data schema: {ex.Message}");
        }
    }

    /// <summary>
    /// Get data schema as base64 PNG string
    /// </summary>
    [HttpGet("use/data-schema-png/{projectId}")]
    public async Task<ActionResult<DataSchemaPngResponse>> GetDataSchemaPng(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting data schema PNG for Project {ProjectId}", projectId);

            // Validate project exists
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound(new DataSchemaPngResponse
                {
                    Success = false,
                    Message = $"Project with ID {projectId} not found"
                });
            }

            // Check if DataSchema exists
            if (string.IsNullOrEmpty(project.DataSchema))
            {
                return NotFound(new DataSchemaPngResponse
                {
                    Success = false,
                    Message = $"Data schema not found for project {projectId}. Please create a data model first."
                });
            }

            // Return the stored base64 PNG string directly
            _logger.LogInformation("Successfully retrieved PNG for Project {ProjectId}", projectId);

            return Ok(new DataSchemaPngResponse
            {
                Success = true,
                PngBase64 = project.DataSchema, // Directly return stored base64 PNG
                ProjectId = projectId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PNG for Project {ProjectId}", projectId);
            return StatusCode(500, new DataSchemaPngResponse
            {
                Success = false,
                Message = $"An error occurred while retrieving PNG: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Converts HTML content to PNG base64 string using System.Drawing
    /// </summary>
    private async Task<string> ConvertHtmlToPngBase64(string htmlContent)
    {
        try
        {
            // Extract Mermaid diagram from HTML
            var mermaidDiagram = ExtractMermaidDiagram(htmlContent);
            
            if (string.IsNullOrEmpty(mermaidDiagram))
            {
                _logger.LogWarning("No Mermaid diagram found in HTML content");
                return GeneratePlaceholderImageBase64();
            }
            
            // Parse the Mermaid diagram to extract table information
            var tables = ParseMermaidDiagram(mermaidDiagram);
            
            // Generate PNG image from table data
            var pngBase64 = GenerateSchemaImage(tables);
            
            _logger.LogInformation("Successfully converted HTML to PNG using System.Drawing");
            
            return pngBase64;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting HTML to PNG: {Message}", ex.Message);
            
            // Fallback: return a simple placeholder image
            return GeneratePlaceholderImageBase64();
        }
    }

    /// <summary>
    /// Generates PNG image from table information
    /// </summary>
    private string GenerateSchemaImage(List<TableInfo> tables)
    {
        try
        {
            const int tableWidth = 200;
            const int tableHeight = 100;
            const int margin = 50;
            const int spacing = 50;
            
            var imageWidth = Math.Max(800, tables.Count * (tableWidth + spacing) + margin);
            var imageHeight = Math.Max(600, tableHeight + margin * 2);
            
            using var bitmap = new Bitmap(imageWidth, imageHeight);
            using var graphics = Graphics.FromImage(bitmap);
            
            // Fill background
            graphics.Clear(Color.White);
            
            // Draw title
            using var titleFont = new Font("Arial", 20, FontStyle.Bold);
            using var titleBrush = new SolidBrush(Color.Black);
            graphics.DrawString("Database Schema", titleFont, titleBrush, margin, margin);
            
            var startY = margin + 40;
            
            // Draw tables
            for (int i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                var x = margin + i * (tableWidth + spacing);
                var y = startY;
                
                // Draw table rectangle
                using var tablePen = new Pen(Color.Black, 2);
                graphics.DrawRectangle(tablePen, x, y, tableWidth, tableHeight);
                
                // Draw table name
                using var nameFont = new Font("Arial", 12, FontStyle.Bold);
                using var nameBrush = new SolidBrush(Color.Blue);
                var nameSize = graphics.MeasureString(table.Name, nameFont);
                var nameX = x + (tableWidth - nameSize.Width) / 2;
                graphics.DrawString(table.Name, nameFont, nameBrush, nameX, y + 5);
                
                // Draw columns
                using var columnFont = new Font("Arial", 9);
                using var columnBrush = new SolidBrush(Color.Black);
                var columnY = y + 25;
                
                foreach (var column in table.Columns.Take(5)) // Limit to 5 columns for space
                {
                    var columnText = $"{column.DataType} {column.Name}";
                    if (column.IsPrimaryKey) columnText += " PK";
                    if (column.IsForeignKey) columnText += " FK";
                    
                    graphics.DrawString(columnText, columnFont, columnBrush, x + 5, columnY);
                    columnY += 15;
                }
                
                if (table.Columns.Count > 5)
                {
                    graphics.DrawString("...", columnFont, columnBrush, x + 5, columnY);
                }
            }
            
            // Convert to base64
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            var bytes = stream.ToArray();
            
            return Convert.ToBase64String(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating schema image: {Message}", ex.Message);
            return GeneratePlaceholderImageBase64();
        }
    }

    /// <summary>
    /// Generates a placeholder PNG image as base64 when conversion fails
    /// </summary>
    private string GeneratePlaceholderImageBase64()
    {
        try
        {
            using var bitmap = new Bitmap(800, 600);
            using var graphics = Graphics.FromImage(bitmap);
            
            // Fill background
            graphics.Clear(Color.White);
            
            // Draw placeholder text
            using var font = new Font("Arial", 24, FontStyle.Bold);
            using var brush = new SolidBrush(Color.Black);
            
            var text = "Data Schema Image\n(HTML to PNG conversion failed)";
            var textSize = graphics.MeasureString(text, font);
            var x = (bitmap.Width - textSize.Width) / 2;
            var y = (bitmap.Height - textSize.Height) / 2;
            
            graphics.DrawString(text, font, brush, x, y);
            
            // Convert to base64
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            var bytes = stream.ToArray();
            
            return Convert.ToBase64String(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating placeholder image: {Message}", ex.Message);
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Error generating image"));
        }
    }

    /// <summary>
    /// Extracts Mermaid diagram from HTML content
    /// </summary>
    private string ExtractMermaidDiagram(string htmlContent)
    {
        try
        {
            var mermaidMatch = Regex.Match(htmlContent, @"<div class=""mermaid"">\s*(.*?)\s*</div>", RegexOptions.Singleline);
            return mermaidMatch.Success ? mermaidMatch.Groups[1].Value.Trim() : string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting Mermaid diagram: {Message}", ex.Message);
            return string.Empty;
        }
    }

    /// <summary>
    /// Parses Mermaid diagram to extract table information
    /// </summary>
    private List<TableInfo> ParseMermaidDiagram(string mermaidDiagram)
    {
        var tables = new List<TableInfo>();
        
        try
        {
            var lines = mermaidDiagram.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip erDiagram declaration
                if (trimmedLine.StartsWith("erDiagram"))
                    continue;
                
                // Parse table definition
                if (trimmedLine.Contains("{"))
                {
                    var tableName = trimmedLine.Split('{')[0].Trim();
                    var table = new TableInfo { Name = tableName, Columns = new List<ColumnInfo>() };
                    tables.Add(table);
                }
                // Parse column definition
                else if (trimmedLine.Contains(" ") && tables.Any())
                {
                    var parts = trimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var dataType = parts[0];
                        var columnName = parts[1];
                        var isPrimaryKey = trimmedLine.Contains("PK");
                        var isForeignKey = trimmedLine.Contains("FK");
                        
                        tables.Last().Columns.Add(new ColumnInfo
                        {
                            Name = columnName,
                            DataType = dataType,
                            IsPrimaryKey = isPrimaryKey,
                            IsForeignKey = isForeignKey
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Mermaid diagram: {Message}", ex.Message);
        }
        
        return tables;
    }




    /// <summary>
    /// Cleans AI response to extract only the SQL script, removing any explanatory text
    /// </summary>
    private string CleanSqlFromAiResponse(string aiResponse)
    {
        if (string.IsNullOrEmpty(aiResponse))
            return aiResponse;

        // Remove common AI response prefixes
        var prefixes = new[]
        {
            "Here is the updated script:",
            "Here's the updated script:",
            "Here is the updated SQL script:",
            "Here's the updated SQL script:",
            "Updated script:",
            "Updated SQL script:",
            "Here is the modified script:",
            "Here's the modified script:",
            "Modified script:",
            "Modified SQL script:"
        };

        var cleanedResponse = aiResponse.Trim();
        
        foreach (var prefix in prefixes)
        {
            if (cleanedResponse.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                cleanedResponse = cleanedResponse.Substring(prefix.Length).Trim();
                break;
            }
        }

        // Find the first CREATE statement and extract from there
        var createIndex = cleanedResponse.IndexOf("CREATE", StringComparison.OrdinalIgnoreCase);
        if (createIndex >= 0)
        {
            cleanedResponse = cleanedResponse.Substring(createIndex);
        }

        // Find the last semicolon to ensure we have complete SQL
        var lastSemicolon = cleanedResponse.LastIndexOf(';');
        if (lastSemicolon >= 0)
        {
            cleanedResponse = cleanedResponse.Substring(0, lastSemicolon + 1);
        }

        return cleanedResponse.Trim();
    }

    /// <summary>
    /// Generates a visual HTML schema from SQL script using Mermaid ER diagram
    /// </summary>
    private string GenerateVisualSchema(string sqlScript, string projectTitle)
    {
        try
        {
            // Parse SQL script to extract table information
            var tables = ParseSqlTables(sqlScript);
            
            // Generate Mermaid ER diagram
            var mermaidDiagram = GenerateMermaidDiagram(tables);
            
            // Create HTML page with Mermaid
            var html = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Database Schema - {projectTitle}</title>
    <script src=""https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js""></script>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 0;
            padding: 20px;
            background-color: #f5f5f5;
        }}
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            padding: 30px;
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
            padding-bottom: 20px;
            border-bottom: 2px solid #e0e0e0;
        }}
        .header h1 {{
            color: #333;
            margin: 0;
            font-size: 2.2em;
        }}
        .header p {{
            color: #666;
            margin: 10px 0 0 0;
            font-size: 1.1em;
        }}
        .diagram-container {{
            background: #fafafa;
            border: 1px solid #ddd;
            border-radius: 6px;
            padding: 20px;
            margin: 20px 0;
        }}
        .mermaid {{
            text-align: center;
        }}
        .table-count {{
            text-align: center;
            color: #666;
            font-style: italic;
            margin-top: 15px;
        }}
        .footer {{
            text-align: center;
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #e0e0e0;
            color: #888;
            font-size: 0.9em;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Database Schema</h1>
            <p>{projectTitle}</p>
        </div>
        
        <div class=""diagram-container"">
            <div class=""mermaid"">
{mermaidDiagram}
            </div>
            <div class=""table-count"">
                {tables.Count} table{(tables.Count != 1 ? "s" : "")} in the database
            </div>
        </div>
        
        <div class=""footer"">
            Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}
        </div>
    </div>

    <script>
        mermaid.initialize({{
            startOnLoad: true,
            theme: 'default',
            er: {{
                diagramPadding: 20,
                layoutDirection: 'TB',
                minEntityWidth: 100,
                minEntityHeight: 75,
                entityPadding: 15,
                stroke: '#333',
                fill: '#fff',
                fontSize: 12
            }}
        }});
    </script>
</body>
</html>";

            return html;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating visual schema: {Message}", ex.Message);
            return $@"
<!DOCTYPE html>
<html>
<head><title>Error</title></head>
<body>
    <h1>Error Generating Schema</h1>
    <p>An error occurred while generating the visual schema: {ex.Message}</p>
</body>
</html>";
        }
    }

    /// <summary>
    /// Parses SQL script to extract table information
    /// </summary>
    private List<TableInfo> ParseSqlTables(string sqlScript)
    {
        var tables = new List<TableInfo>();
        
        // Find CREATE TABLE statements with proper parenthesis matching
        var createTablePattern = @"CREATE\s+TABLE\s+(\w+)\s*\(";
        var createTableMatches = Regex.Matches(sqlScript, createTablePattern, RegexOptions.IgnoreCase);

        foreach (Match match in createTableMatches)
        {
            var tableName = match.Groups[1].Value;
            var startIndex = match.Index + match.Length;
            
            // Find the matching closing parenthesis
            var parenCount = 1;
            var endIndex = startIndex;
            
            while (endIndex < sqlScript.Length && parenCount > 0)
            {
                if (sqlScript[endIndex] == '(')
                    parenCount++;
                else if (sqlScript[endIndex] == ')')
                    parenCount--;
                endIndex++;
            }
            
            if (parenCount == 0)
            {
                var tableBody = sqlScript.Substring(startIndex, endIndex - startIndex - 1);
                
                var columns = ParseTableColumns(tableBody);
                var relationships = ParseTableRelationships(tableBody);
                
                tables.Add(new TableInfo
                {
                    Name = tableName,
                    Columns = columns,
                    Relationships = relationships
                });
            }
        }
        
        return tables;
    }

    /// <summary>
    /// Parses columns from a table definition
    /// </summary>
    private List<ColumnInfo> ParseTableColumns(string tableDefinition)
    {
        var columns = new List<ColumnInfo>();
        
        // Split by commas, but be careful with nested parentheses (like ENUM values)
        var columnDefinitions = SplitTableDefinition(tableDefinition);
        
        foreach (var columnDef in columnDefinitions)
        {
            var trimmedDef = columnDef.Trim();
            
            // Skip constraint definitions
            if (trimmedDef.StartsWith("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) ||
                trimmedDef.StartsWith("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) ||
                trimmedDef.StartsWith("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
                trimmedDef.StartsWith("INDEX", StringComparison.OrdinalIgnoreCase) ||
                trimmedDef.StartsWith("CONSTRAINT", StringComparison.OrdinalIgnoreCase))
                continue;
            
            // Parse column definition using regex
            var columnMatch = Regex.Match(trimmedDef, @"^(\w+)\s+(ENUM\([^)]+\)|\w+(?:\(\d+(?:,\d+)?\))?)", RegexOptions.IgnoreCase);
            
            if (columnMatch.Success)
            {
                var columnName = columnMatch.Groups[1].Value;
                var dataType = columnMatch.Groups[2].Value;
                
                // Skip if it's a constraint keyword
                if (new[] { "PRIMARY", "FOREIGN", "KEY", "UNIQUE", "INDEX", "CONSTRAINT", "CREATE", "TABLE" }.Contains(columnName.ToUpper()))
                    continue;
                
                // Clean up data type - simplify ENUM to just "ENUM"
                if (dataType.StartsWith("ENUM(", StringComparison.OrdinalIgnoreCase))
                {
                    dataType = "ENUM";
                }
                
                // Check for primary key in the definition
                var isPrimaryKey = trimmedDef.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) ||
                                  tableDefinition.Contains($"PRIMARY KEY ({columnName})", StringComparison.OrdinalIgnoreCase);
                
                // Check for foreign key in the definition
                var isForeignKey = trimmedDef.Contains("REFERENCES", StringComparison.OrdinalIgnoreCase) ||
                                  tableDefinition.Contains($"FOREIGN KEY ({columnName})", StringComparison.OrdinalIgnoreCase);
                
                columns.Add(new ColumnInfo
                {
                    Name = columnName,
                    DataType = dataType,
                    IsPrimaryKey = isPrimaryKey,
                    IsForeignKey = isForeignKey
                });
            }
        }
        
        return columns;
    }

    /// <summary>
    /// Splits table definition by commas, respecting nested parentheses
    /// </summary>
    private List<string> SplitTableDefinition(string tableDefinition)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var parenCount = 0;
        
        for (int i = 0; i < tableDefinition.Length; i++)
        {
            var c = tableDefinition[i];
            
            if (c == '(')
            {
                parenCount++;
                current.Append(c);
            }
            else if (c == ')')
            {
                parenCount--;
                current.Append(c);
            }
            else if (c == ',' && parenCount == 0)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        
        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }
        
        return result;
    }

    /// <summary>
    /// Parses relationships from a table definition
    /// </summary>
    private List<RelationshipInfo> ParseTableRelationships(string tableDefinition)
    {
        var relationships = new List<RelationshipInfo>();
        
        // Look for FOREIGN KEY constraints
        var foreignKeyMatches = Regex.Matches(
            tableDefinition,
            @"FOREIGN\s+KEY\s*\((\w+)\)\s*REFERENCES\s+(\w+)\s*\((\w+)\)",
            RegexOptions.IgnoreCase
        );

        foreach (Match match in foreignKeyMatches)
        {
            relationships.Add(new RelationshipInfo
            {
                Column = match.Groups[1].Value,
                ReferencedTable = match.Groups[2].Value,
                ReferencedColumn = match.Groups[3].Value
            });
        }
        
        return relationships;
    }

    /// <summary>
    /// Generates Mermaid ER diagram from table information
    /// </summary>
    private string GenerateMermaidDiagram(List<TableInfo> tables)
    {
        var diagram = new System.Text.StringBuilder();
        diagram.AppendLine("erDiagram");
        
        // Add table definitions
        foreach (var table in tables)
        {
            diagram.AppendLine($"    {table.Name} {{");
            
            foreach (var column in table.Columns)
            {
                // Build key indicators
                var keyIndicators = new List<string>();
                if (column.IsPrimaryKey)
                    keyIndicators.Add("PK");
                if (column.IsForeignKey)
                    keyIndicators.Add("FK");
                
                var keyIndicatorStr = keyIndicators.Any() ? $" {string.Join(", ", keyIndicators)}" : "";
                
                // Format: Type field_name [PK/FK indicators] ["optional description"]
                var columnDef = $"        {column.DataType} {column.Name}{keyIndicatorStr}";
                    
                diagram.AppendLine(columnDef);
            }
            
            diagram.AppendLine("    }");
        }
        
        // Add relationships
        foreach (var table in tables)
        {
            foreach (var relationship in table.Relationships)
            {
                diagram.AppendLine($"    {relationship.ReferencedTable} ||--o{{ {table.Name} : \"{relationship.Column}\"");
            }
        }
        
        return diagram.ToString();
    }

    // Helper classes for parsing
    private class TableInfo
    {
        public string Name { get; set; } = string.Empty;
        public List<ColumnInfo> Columns { get; set; } = new();
        public List<RelationshipInfo> Relationships { get; set; } = new();
    }

    private class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
    }

    private class RelationshipInfo
    {
        public string Column { get; set; } = string.Empty;
        public string ReferencedTable { get; set; } = string.Empty;
        public string ReferencedColumn { get; set; } = string.Empty;
    }
}

// Request model for InitiateModules (Response model already exists in Models namespace)
public class InitiateModulesRequest
{
    public int ProjectId { get; set; }
}

// Request model for direct module update
public class UpdateModuleApplyRequest
{
    public int ProjectId { get; set; }
    public int Sequence { get; set; }

    [JsonPropertyName("descriptiont")]
    public string Description { get; set; } = string.Empty;
}

// Response model for UpdateModule
public class UpdateModuleResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? UpdatedDescription { get; set; }
    public string Title { get; set; } = string.Empty;
}

// Request model for BindModules
public class BindModulesRequest
{
    public int ProjectId { get; set; }
    public bool ToTranslate { get; set; } = false;
}

// Response model for BindModules
public class BindModulesResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? BoundContent { get; set; }
    public int ModuleCount { get; set; }
    public string Title { get; set; } = string.Empty;
}

// Request model for CreateDataModel
public class CreateDataModelRequest
{
    public int ProjectId { get; set; }
}

// Response model for CreateDataModel
public class CreateDataModelResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? SqlScript { get; set; }
}

// Request model for UpdateDataModel
public class UpdateDataModelRequest
{
    public string UserInput { get; set; } = string.Empty;
}

// Response model for UpdateDataModel
public class UpdateDataModelResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? UpdatedSqlScript { get; set; }
}

// Response model for DataSchemaPng
public class DataSchemaPngResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? PngBase64 { get; set; }
    public int ProjectId { get; set; }
}

// Response model for ModuleCount
public class ModuleCountResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int ModuleCount { get; set; }
    public int ProjectId { get; set; }
}

// Response model for ModuleDescription
public class ModuleDescriptionResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public ModuleDescriptionInfo? ModuleDescription { get; set; }
}

// Module description info
public class ModuleDescriptionInfo
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int Sequence { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ModuleType { get; set; }
}
