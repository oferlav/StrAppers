using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
// using strAppersBackend.Services; // SLACK TEMPORARILY DISABLED

namespace strAppersBackend.Controllers
{
    // DISABLED - Test controller for development only
    // [ApiController]
    // [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TestController> _logger;
        // private readonly SlackService _slackService; // SLACK TEMPORARILY DISABLED

        public TestController(ApplicationDbContext context, ILogger<TestController> logger) // SlackService slackService - SLACK TEMPORARILY DISABLED
        {
            _context = context;
            _logger = logger;
            // _slackService = slackService; // SLACK TEMPORARILY DISABLED
        }

        #region Database Test Methods

        /// <summary>
        /// Test database connection and basic queries
        /// </summary>
        [HttpGet("database")]
        public async Task<ActionResult> TestDatabase()
        {
            try
            {
                var testResults = new
                {
                    timestamp = DateTime.UtcNow,
                    databaseConnection = "Testing...",
                    entityCounts = new
                    {
                        students = await _context.Students.CountAsync(),
                        projects = await _context.Projects.CountAsync(),
                        organizations = await _context.Organizations.CountAsync(),
                        majors = await _context.Majors.CountAsync(),
                        years = await _context.Years.CountAsync(),
                        roles = await _context.Roles.CountAsync(),
                        projectStatuses = await _context.ProjectStatuses.CountAsync(),
                        joinRequests = await _context.JoinRequests.CountAsync()
                    }
                };

                return Ok(new
                {
                    success = true,
                    message = "Database test completed successfully",
                    results = testResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing database connection");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Database test failed"
                });
            }
        }

        /// <summary>
        /// Test entity relationships and includes
        /// </summary>
        [HttpGet("database/relationships")]
        public async Task<ActionResult> TestDatabaseRelationships()
        {
            try
            {
                var testResults = new
                {
                    timestamp = DateTime.UtcNow,
                    studentsWithRelations = await _context.Students
                        .Include(s => s.Major)
                        .Include(s => s.Year)
                        .Include(s => s.ProjectBoard)
                    .ThenInclude(pb => pb.Project)
                        .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                        .Take(5)
                        .Select(s => new
                        {
                            s.Id,
                            s.FirstName,
                            s.LastName,
                            s.Email,
                            MajorName = s.Major != null ? s.Major.Name : "No Major",
                            YearName = s.Year != null ? s.Year.Name : "No Year",
                            OrganizationName = "No Organization", // Organization removed from Student model
                            ProjectTitle = s.ProjectBoard != null ? s.ProjectBoard.Project.Title : "No Project",
                            RoleCount = s.StudentRoles.Count()
                        })
                        .ToListAsync(),
                    projectsWithRelations = await _context.Projects
                        .Include(p => p.Organization)
                        .Take(5)
                        .Select(p => new
                        {
                            p.Id,
                            p.Title,
                            OrganizationName = p.Organization != null ? p.Organization.Name : "No Organization",
                            StatusName = "No Status", // Status now in ProjectBoards
                            StudentCount = 0 // Students now linked via ProjectBoards
                        })
                        .ToListAsync()
                };

                return Ok(new
                {
                    success = true,
                    message = "Database relationships test completed successfully",
                    results = testResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing database relationships");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Database relationships test failed"
                });
            }
        }

        #endregion

        #region Slack Test Methods

        /// <summary>
        /// Test Slack service initialization
        /// </summary>
        [HttpGet("slack/service")]
        public async Task<ActionResult> TestSlackService()
        {
            try
            {
                var testResults = new
                {
                    timestamp = DateTime.UtcNow,
                    serviceInitialized = false, // _slackService != null, // SLACK TEMPORARILY DISABLED
                    connectionTest = "SLACK_DISABLED", // await _slackService.TestConnectionAsync(), // SLACK TEMPORARILY DISABLED
                    botInfoTest = "SLACK_DISABLED" // await _slackService.TestBotInfoAsync() // SLACK TEMPORARILY DISABLED
                };

                return Ok(new
                {
                    success = true,
                    message = "Slack service test completed successfully",
                    results = testResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Slack service");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Slack service test failed"
                });
            }
        }

        /// <summary>
        /// Test Slack API endpoints comprehensively
        /// </summary>
        [HttpGet("slack/comprehensive")]
        public async Task<ActionResult> TestSlackComprehensive()
        {
            try
            {
                var testResults = new
                {
                    timestamp = DateTime.UtcNow,
                    connectionTest = "SLACK_DISABLED", // await _slackService.TestConnectionAsync(), // SLACK TEMPORARILY DISABLED
                    botInfoTest = "SLACK_DISABLED", // await _slackService.TestBotInfoAsync(), // SLACK TEMPORARILY DISABLED
                    usersListTest = "SLACK_DISABLED", // await _slackService.TestUsersListAsync(), // SLACK TEMPORARILY DISABLED
                    channelsListTest = "SLACK_DISABLED", // await _slackService.TestChannelsListAsync(), // SLACK TEMPORARILY DISABLED
                    botPermissionsTest = "SLACK_DISABLED" // await _slackService.TestBotPermissionsAsync() // SLACK TEMPORARILY DISABLED
                };

                return Ok(new
                {
                    success = true,
                    message = "Comprehensive Slack test completed successfully",
                    results = testResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in comprehensive Slack test");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Comprehensive Slack test failed"
                });
            }
        }

        #endregion

        #region Application Test Methods

        /// <summary>
        /// Test application health and configuration
        /// </summary>
        [HttpGet("application/health")]
        public ActionResult TestApplicationHealth()
        {
            try
            {
                var healthResults = new
                {
                    timestamp = DateTime.UtcNow,
                    applicationName = "StrAppers Backend API",
                    version = "1.0.0",
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                    machineName = Environment.MachineName,
                    processorCount = Environment.ProcessorCount,
                    workingSet = Environment.WorkingSet,
                    uptime = Environment.TickCount64,
                    isHealthy = true
                };

                return Ok(new
                {
                    success = true,
                    message = "Application health check completed successfully",
                    results = healthResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in application health check");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Application health check failed"
                });
            }
        }

        /// <summary>
        /// Test all controller endpoints availability
        /// </summary>
        [HttpGet("endpoints")]
        public ActionResult TestEndpoints()
        {
            try
            {
                var endpoints = new
                {
                    timestamp = DateTime.UtcNow,
                    availableControllers = new[]
                    {
                        "StudentsController - /api/students",
                        "ProjectsController - /api/projects", 
                        "OrganizationsController - /api/organizations",
                        "MajorsController - /api/majors",
                        "YearsController - /api/years",
                        "RolesController - /api/roles",
                        "ProjectAllocationController - /api/projectallocation",
                        "SlackController - /api/slack",
                        "SlackDiagnosticController - /api/slackdiagnostic",
                        "TestController - /api/test",
                        "WeatherForecastController - /weatherforecast"
                    },
                    testEndpoints = new[]
                    {
                        "GET /api/test/database - Test database connection",
                        "GET /api/test/database/relationships - Test entity relationships",
                        "GET /api/test/slack/service - Test Slack service",
                        "GET /api/test/slack/comprehensive - Comprehensive Slack tests",
                        "GET /api/test/application/health - Application health check",
                        "GET /api/test/endpoints - This endpoint list"
                    }
                };

                return Ok(new
                {
                    success = true,
                    message = "Endpoint test completed successfully",
                    results = endpoints
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in endpoint test");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Endpoint test failed"
                });
            }
        }

        #endregion

        #region Data Validation Test Methods

        /// <summary>
        /// Test data validation and business rules
        /// </summary>
        [HttpGet("validation")]
        public async Task<ActionResult> TestDataValidation()
        {
            try
            {
                var validationResults = new
                {
                    timestamp = DateTime.UtcNow,
                    activeEntities = new
                    {
                        activeStudents = await _context.Students.CountAsync(),
                        activeProjects = await _context.Projects.CountAsync(),
                        activeOrganizations = await _context.Organizations.CountAsync(s => s.IsActive),
                        activeMajors = await _context.Majors.CountAsync(m => m.IsActive),
                        activeYears = await _context.Years.CountAsync(y => y.IsActive),
                        activeRoles = await _context.Roles.CountAsync(r => r.IsActive),
                        activeProjectStatuses = await _context.ProjectStatuses.CountAsync(ps => ps.IsActive)
                    },
                    businessRuleValidation = new
                    {
                        studentsWithProjects = await _context.Students.CountAsync(s => s.BoardId != null),
                        projectsWithAdmins = 0, // DISABLED - HasAdmin removed
                        studentsWithRoles = await _context.Students.CountAsync(s => s.StudentRoles.Any()),
                        pendingJoinRequests = await _context.JoinRequests.CountAsync(jr => !jr.Added)
                    }
                };

                return Ok(new
                {
                    success = true,
                    message = "Data validation test completed successfully",
                    results = validationResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in data validation test");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Data validation test failed"
                });
            }
        }

        #endregion

        #region Performance Test Methods

        /// <summary>
        /// Test application performance with sample queries
        /// </summary>
        [HttpGet("performance")]
        public async Task<ActionResult> TestPerformance()
        {
            try
            {
                var startTime = DateTime.UtcNow;
                
                // Test various query performance
                var students = await _context.Students
                    .Include(s => s.Major)
                    .Include(s => s.Year)
                    .Include(s => s.ProjectBoard)
                    .ThenInclude(pb => pb.Project)
                    .ToListAsync();

                var projects = await _context.Projects
                    .Include(p => p.Organization)
                    .ToListAsync();

                var endTime = DateTime.UtcNow;
                var executionTime = endTime - startTime;

                var performanceResults = new
                {
                    timestamp = DateTime.UtcNow,
                    executionTimeMs = executionTime.TotalMilliseconds,
                    studentsLoaded = students.Count(),
                    projectsLoaded = projects.Count(),
                    averageStudentLoadTime = students.Count() > 0 ? executionTime.TotalMilliseconds / students.Count() : 0,
                    averageProjectLoadTime = projects.Count() > 0 ? executionTime.TotalMilliseconds / projects.Count() : 0
                };

                return Ok(new
                {
                    success = true,
                    message = "Performance test completed successfully",
                    results = performanceResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in performance test");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Performance test failed"
                });
            }
        }

        #endregion

        #region SystemDesign Debug Tests

        /// <summary>
        /// Test SystemDesign endpoint with detailed debugging
        /// </summary>
        [HttpPost("debug/systemdesign")]
        public async Task<ActionResult> TestSystemDesign([FromBody] SystemDesignRequest request)
        {
            try
            {
                _logger.LogInformation("=== SystemDesign Debug Test Started ===");
                _logger.LogInformation("Request: ProjectId={ProjectId}, ExtendedDescription length={DescLength}, TeamRoles count={TeamRolesCount}", 
                    request.ProjectId, request.ExtendedDescription?.Length ?? 0, request.TeamRoles?.Count() ?? 0);

                // Step 1: Validate request
                _logger.LogInformation("Step 1: Validating request");
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Model validation failed: {ModelState}", ModelState);
                    return BadRequest(new { error = "Model validation failed", details = ModelState });
                }

                // Step 2: Check if project exists
                _logger.LogInformation("Step 2: Checking if project {ProjectId} exists", request.ProjectId);
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

                if (project == null)
                {
                    _logger.LogError("Project {ProjectId} not found", request.ProjectId);
                    return NotFound(new { error = $"Project with ID {request.ProjectId} not found" });
                }

                _logger.LogInformation("Project found: {ProjectTitle}", project.Title);

                // Step 3: Build team roles
                _logger.LogInformation("Step 3: Building team roles from allocated students");
                // Get students through ProjectBoards
                var students = await _context.Students
                    .Where(s => s.BoardId != null)
                    .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                    .ToListAsync();
                
                var teamRoles = students
                    .SelectMany(s => s.StudentRoles)
                    .GroupBy(sr => sr.RoleId)
                    .Select(g => new RoleInfo
                    {
                        RoleId = g.Key,
                        RoleName = g.First().Role.Name,
                        StudentCount = g.Count()
                    })
                    .ToList();

                _logger.LogInformation("Team roles built: {RoleCount} roles", teamRoles.Count());
                foreach (var role in teamRoles)
                {
                    _logger.LogInformation("Role: {RoleName} (ID: {RoleId}), Students: {StudentCount}", 
                        role.RoleName, role.RoleId, role.StudentCount);
                }

                // Step 4: Test AI Service (without actually calling OpenAI)
                _logger.LogInformation("Step 4: Testing AI Service availability");
                try
                {
                    // Check if AIService is registered
                    // var aiService = HttpContext.RequestServices.GetService<IAIService>(); // TEMPORARILY DISABLED
                    if (true) // aiService == null) // TEMPORARILY DISABLED
                    {
                        _logger.LogError("AIService is not registered in dependency injection");
                        return StatusCode(500, new { error = "AIService not available" });
                    }
                    _logger.LogInformation("AIService is available");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking AIService availability");
                    return StatusCode(500, new { error = "AIService check failed", details = ex.Message });
                }

                // Step 5: Test DesignDocumentService
                _logger.LogInformation("Step 5: Testing DesignDocumentService availability");
                try
                {
                    // var designService = HttpContext.RequestServices.GetService<IDesignDocumentService>(); // TEMPORARILY DISABLED
                    if (true) // designService == null) // TEMPORARILY DISABLED
                    {
                        _logger.LogError("DesignDocumentService is not registered in dependency injection");
                        return StatusCode(500, new { error = "DesignDocumentService not available" });
                    }
                    _logger.LogInformation("DesignDocumentService is available");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking DesignDocumentService availability");
                    return StatusCode(500, new { error = "DesignDocumentService check failed", details = ex.Message });
                }

                // Step 6: Test OpenAI configuration
                _logger.LogInformation("Step 6: Testing OpenAI configuration");
                try
                {
                    var configuration = HttpContext.RequestServices.GetService<IConfiguration>();
                    var openAiKey = configuration?["OpenAI:ApiKey"];
                    if (string.IsNullOrEmpty(openAiKey))
                    {
                        _logger.LogError("OpenAI API key is not configured");
                        return StatusCode(500, new { error = "OpenAI API key not configured" });
                    }
                    _logger.LogInformation("OpenAI API key is configured (length: {KeyLength})", openAiKey.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking OpenAI configuration");
                    return StatusCode(500, new { error = "OpenAI configuration check failed", details = ex.Message });
                }

                _logger.LogInformation("=== SystemDesign Debug Test Completed Successfully ===");

                return Ok(new
                {
                    success = true,
                    message = "All SystemDesign components are working correctly",
                    project = new
                    {
                        id = project.Id,
                        title = project.Title,
                        studentCount = await _context.Students.CountAsync(s => s.BoardId != null)
                    },
                    teamRoles = teamRoles,
                    services = new
                    {
                        aiService = "Available",
                        designDocumentService = "Available",
                        openAiConfiguration = "Available"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SystemDesign debug test failed: {ErrorMessage}", ex.Message);
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    message = "SystemDesign debug test failed"
                });
            }
        }

        /// <summary>
        /// Test SystemDesign endpoint with minimal request
        /// </summary>
        [HttpPost("debug/systemdesign-simple")]
        public async Task<ActionResult> TestSystemDesignSimple([FromBody] object request)
        {
            try
            {
                _logger.LogInformation("=== Simple SystemDesign Test Started ===");
                _logger.LogInformation("Request received: {Request}", request?.ToString() ?? "null");

                // Test with a hardcoded project ID
                var testProjectId = 1;
                _logger.LogInformation("Testing with project ID: {ProjectId}", testProjectId);

                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == testProjectId);

                if (project == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = $"Project {testProjectId} not found",
                        availableProjects = await _context.Projects.Select(p => new { p.Id, p.Title }).ToListAsync()
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Simple test completed",
                    project = new
                    {
                        id = project.Id,
                        title = project.Title,
                        studentCount = await _context.Students.CountAsync(s => s.BoardId != null),
                        hasExtendedDescription = !string.IsNullOrEmpty(project.ExtendedDescription)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Simple SystemDesign test failed: {ErrorMessage}", ex.Message);
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Simple SystemDesign test failed"
                });
            }
        }

        #endregion
    }
}
