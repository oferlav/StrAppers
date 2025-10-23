using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using System.Text.Json;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemDesignController : ControllerBase
{
    private readonly IDesignDocumentService _designDocumentService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SystemDesignController> _logger;
    private readonly IAIService _aiService;

    public SystemDesignController(
        IDesignDocumentService designDocumentService,
        ApplicationDbContext context,
        ILogger<SystemDesignController> logger,
        IAIService aiService)
    {
        _designDocumentService = designDocumentService;
        _context = context;
        _logger = logger;
        _aiService = aiService;
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
    /// Get the latest design version for a project (for frontend use)
    /// </summary>
    [HttpGet("use/project/{projectId}/latest-design")]
    public async Task<ActionResult> GetLatestDesignVersion(int projectId)
    {
        try
        {
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            if (string.IsNullOrEmpty(project.SystemDesignFormatted))
            {
                return NotFound($"No design document found for project {projectId}");
            }

            return Ok(new
            {
                projectId = project.Id,
                designDocument = project.SystemDesignFormatted,
                createdAt = project.CreatedAt,
                updatedAt = project.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest design version for Project {ProjectId}", projectId);
            return StatusCode(500, "An error occurred while retrieving the design version");
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

            if (string.IsNullOrEmpty(project.SystemDesignFormatted))
            {
                _logger.LogWarning("No SystemDesignFormatted found for Project {ProjectId}", projectId);
                return NotFound(new
                {
                    Success = false,
                    Message = $"No SystemDesign found for project {projectId}"
                });
            }

            // Convert SystemDesignFormatted to base64
            var systemDesignBytes = System.Text.Encoding.UTF8.GetBytes(project.SystemDesignFormatted);
            var base64String = Convert.ToBase64String(systemDesignBytes);

            _logger.LogInformation("Successfully converted SystemDesignFormatted to base64 for Project {ProjectId}. Length: {Length}", 
                projectId, base64String.Length);

            return Ok(new
            {
                Success = true,
                ProjectId = projectId,
                ProjectTitle = project.Title,
                SystemDesignBase64 = base64String,
                OriginalLength = project.SystemDesignFormatted.Length,
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

            // Call AI service to generate modules
            var aiResponse = await _aiService.InitiateModulesAsync(request.ProjectId, extendedDescription);
            if (!aiResponse.Success)
            {
                return BadRequest(aiResponse);
            }

            // Save modules to database with 1-based sequence
            var savedModules = new List<ProjectModule>();
            var sequence = 1; // Start with 1-based index
            
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
    /// Update a module using AI feedback
    /// </summary>
    [HttpPost("use/update-module")]
    public async Task<ActionResult<UpdateModuleResponse>> UpdateModule([FromBody] UpdateModuleRequest request)
    {
        try
        {
            _logger.LogInformation("Updating module with sequence {Sequence} for Project {ProjectId}", request.Sequence, request.ProjectId);
            _logger.LogInformation("User Input: {UserInput}", request.UserInput);

            // Validate project exists
            var project = await _context.Projects.FindAsync(request.ProjectId);
            if (project == null)
            {
                return NotFound(new UpdateModuleResponse
                {
                    Success = false,
                    Message = $"Project with ID {request.ProjectId} not found"
                });
            }

            // Get the module by sequence and project
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

            // Get the current description from the database
            var currentDescription = module.Description ?? "";
            _logger.LogInformation("Current module description: {Description}", currentDescription);

            // Clean user input to handle newlines properly
            var cleanedUserInput = request.UserInput?.Replace("\r\n", "\n").Replace("\r", "\n") ?? "";
            _logger.LogInformation("Cleaned user input: {UserInput}", cleanedUserInput);

            // Call AI service to update module description
            var aiResponse = await _aiService.UpdateModuleAsync(module.Id, currentDescription, cleanedUserInput);
            if (!aiResponse.Success)
            {
                return BadRequest(aiResponse);
            }

            // Update module description
            module.Description = aiResponse.UpdatedDescription ?? module.Description;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully updated module with sequence {Sequence} (ID: {ModuleId}) for Project {ProjectId}", 
                request.Sequence, module.Id, request.ProjectId);

            return Ok(new UpdateModuleResponse
            {
                Success = true,
                UpdatedDescription = aiResponse.UpdatedDescription
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating module with sequence {Sequence} for Project {ProjectId}", 
                request.Sequence, request.ProjectId);
            return StatusCode(500, new UpdateModuleResponse
            {
                Success = false,
                Message = $"An error occurred while updating module: {ex.Message}"
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
        
        // Split by CREATE TABLE statements (case insensitive)
        var createTableMatches = System.Text.RegularExpressions.Regex.Matches(
            sqlScript, 
            @"CREATE\s+TABLE\s+(\w+)\s*\(", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        foreach (System.Text.RegularExpressions.Match match in createTableMatches)
        {
            var tableName = match.Groups[1].Value;
            var tableStart = match.Index;
            
            // Find the end of this CREATE TABLE statement
            var nextCreateIndex = sqlScript.IndexOf("CREATE TABLE", tableStart + 1, StringComparison.OrdinalIgnoreCase);
            var nextCreateIndex2 = sqlScript.IndexOf("CREATE ", tableStart + 1, StringComparison.OrdinalIgnoreCase);
            var nextCreateIndex3 = sqlScript.IndexOf(");", tableStart);
            
            var endIndex = sqlScript.Length;
            if (nextCreateIndex > 0 && nextCreateIndex < endIndex) endIndex = nextCreateIndex;
            if (nextCreateIndex2 > 0 && nextCreateIndex2 < endIndex) endIndex = nextCreateIndex2;
            if (nextCreateIndex3 > 0 && nextCreateIndex3 < endIndex) endIndex = nextCreateIndex3 + 2;
            
            var tableDefinition = sqlScript.Substring(tableStart, endIndex - tableStart);
            
            var columns = ParseTableColumns(tableDefinition);
            var relationships = ParseTableRelationships(tableDefinition);
            
            tables.Add(new TableInfo
            {
                Name = tableName,
                Columns = columns,
                Relationships = relationships
            });
        }
        
        return tables;
    }

    /// <summary>
    /// Parses columns from a table definition
    /// </summary>
    private List<ColumnInfo> ParseTableColumns(string tableDefinition)
    {
        var columns = new List<ColumnInfo>();
        
        // Find column definitions between parentheses
        var columnMatches = System.Text.RegularExpressions.Regex.Matches(
            tableDefinition,
            @"(\w+)\s+(\w+(?:\(\d+(?:,\d+)?\))?)\s*(?:PRIMARY\s+KEY|UNIQUE|NOT\s+NULL|NULL)?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        foreach (System.Text.RegularExpressions.Match match in columnMatches)
        {
            var columnName = match.Groups[1].Value;
            var dataType = match.Groups[2].Value;
            
            // Skip if it's a constraint keyword
            if (new[] { "PRIMARY", "FOREIGN", "KEY", "UNIQUE", "INDEX", "CONSTRAINT" }.Contains(columnName.ToUpper()))
                continue;
                
            var isPrimaryKey = tableDefinition.Contains($"PRIMARY KEY ({columnName})", StringComparison.OrdinalIgnoreCase) ||
                              tableDefinition.Contains($"{columnName} PRIMARY KEY", StringComparison.OrdinalIgnoreCase);
            
            var isForeignKey = tableDefinition.Contains($"FOREIGN KEY ({columnName})", StringComparison.OrdinalIgnoreCase) ||
                              tableDefinition.Contains($"REFERENCES", StringComparison.OrdinalIgnoreCase);
            
            columns.Add(new ColumnInfo
            {
                Name = columnName,
                DataType = dataType,
                IsPrimaryKey = isPrimaryKey,
                IsForeignKey = isForeignKey
            });
        }
        
        return columns;
    }

    /// <summary>
    /// Parses relationships from a table definition
    /// </summary>
    private List<RelationshipInfo> ParseTableRelationships(string tableDefinition)
    {
        var relationships = new List<RelationshipInfo>();
        
        // Look for FOREIGN KEY constraints
        var foreignKeyMatches = System.Text.RegularExpressions.Regex.Matches(
            tableDefinition,
            @"FOREIGN\s+KEY\s*\((\w+)\)\s*REFERENCES\s+(\w+)\s*\((\w+)\)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        foreach (System.Text.RegularExpressions.Match match in foreignKeyMatches)
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
    /// Get module description by project ID and sequence
    /// </summary>
    [HttpGet("use/ModuleDescription")]
    public async Task<ActionResult<object>> GetModuleDescription(int projectId, int sequence)
    {
        try
        {
            _logger.LogInformation("Getting module description for Project {ProjectId}, Sequence {Sequence}", projectId, sequence);

            // Validate project exists
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound(new { success = false, message = $"Project with ID {projectId} not found" });
            }

            // Get the module by sequence and project
            var module = await _context.ProjectModules
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.Sequence == sequence);

            if (module == null)
            {
                return NotFound(new { success = false, message = $"Module with sequence {sequence} not found for project {projectId}" });
            }

            _logger.LogInformation("Found module {ModuleId} for Project {ProjectId}, Sequence {Sequence}", module.Id, projectId, sequence);

            return Ok(new
            {
                success = true,
                moduleId = module.Id,
                projectId = module.ProjectId,
                sequence = module.Sequence,
                title = module.Title,
                description = module.Description,
                moduleType = module.ModuleType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting module description for Project {ProjectId}, Sequence {Sequence}", projectId, sequence);
            return StatusCode(500, new { success = false, message = $"An error occurred while getting module description: {ex.Message}" });
        }
    }

    /// <summary>
    /// Get module count for a project (type = 2)
    /// </summary>
    [HttpGet("use/ModuleCount")]
    public async Task<ActionResult<object>> GetModuleCount(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting module count for Project {ProjectId}", projectId);

            // Validate project exists
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound(new { success = false, message = $"Project with ID {projectId} not found" });
            }

            // Count modules with type = 2 (Screen type)
            var moduleCount = await _context.ProjectModules
                .CountAsync(pm => pm.ProjectId == projectId && pm.ModuleType == 2);

            _logger.LogInformation("Found {Count} modules for Project {ProjectId}", moduleCount, projectId);

            return Ok(new
            {
                success = true,
                projectId = projectId,
                moduleCount = moduleCount,
                moduleType = 2
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting module count for Project {ProjectId}", projectId);
            return StatusCode(500, new { success = false, message = $"An error occurred while getting module count: {ex.Message}" });
        }
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
                var columnDef = $"        {column.DataType} {column.Name}";
                if (column.IsPrimaryKey)
                    columnDef += " PK";
                if (column.IsForeignKey)
                    columnDef += " FK";
                    
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

// Request models for the new endpoints
public class InitiateModulesRequest
{
    public int ProjectId { get; set; }
}

public class CreateDataModelRequest
{
    public int ProjectId { get; set; }
}

public class UpdateModuleRequest
{
    public int ProjectId { get; set; }
    public int Sequence { get; set; }
    public string UserInput { get; set; } = string.Empty;
}

public class UpdateDataModelRequest
{
    public string UserInput { get; set; } = string.Empty;
}

public class UpdateDataModelResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? UpdatedSqlScript { get; set; }
}
