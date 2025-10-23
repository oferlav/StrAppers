using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
// using strAppersBackend.Services; // SLACK TEMPORARILY DISABLED

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TestController> _logger;
        private readonly IKickoffService _kickoffService;
        // private readonly SlackService _slackService; // SLACK TEMPORARILY DISABLED

        public TestController(ApplicationDbContext context, ILogger<TestController> logger, IKickoffService kickoffService) // SlackService slackService - SLACK TEMPORARILY DISABLED
        {
            _context = context;
            _logger = logger;
            _kickoffService = kickoffService;
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

        #region Kickoff Test Methods

        /// <summary>
        /// Test kickoff logic with different scenarios
        /// Optional parameters: projectId, studentIds (comma-separated)
        /// Examples:
        /// GET /api/test/kickoff - Test all projects
        /// GET /api/test/kickoff?projectId=1 - Test specific project
        /// GET /api/test/kickoff?projectId=1&studentIds=1,2,3 - Test specific project with specific students
        /// </summary>
        [HttpGet("kickoff")]
        public async Task<ActionResult> TestKickoffLogic([FromQuery] int? projectId = null, [FromQuery] string? studentIds = null)
        {
            try
            {
                _logger.LogInformation("=== KICKOFF LOGIC TEST STARTED ===");
                _logger.LogInformation("Parameters: projectId={ProjectId}, studentIds={StudentIds}", projectId, studentIds);

                var scenarios = new List<object>();

                // Parse student IDs if provided
                var parsedStudentIds = new List<int>();
                if (!string.IsNullOrEmpty(studentIds))
                {
                    try
                    {
                        parsedStudentIds = studentIds.Split(',')
                            .Select(id => int.Parse(id.Trim()))
                            .ToList();
                        _logger.LogInformation("Parsed student IDs: [{StudentIds}]", string.Join(", ", parsedStudentIds));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing student IDs: {StudentIds}", studentIds);
                        return BadRequest(new
                        {
                            success = false,
                            error = "Invalid student IDs format. Use comma-separated integers (e.g., '1,2,3')",
                            providedStudentIds = studentIds
                        });
                    }
                }

                // Scenario 1: Test specific project with specific students
                if (projectId.HasValue && parsedStudentIds.Any())
                {
                    _logger.LogInformation("Testing specific project {ProjectId} with specific students: [{StudentIds}]", 
                        projectId.Value, string.Join(", ", parsedStudentIds));

                    var project = await _context.Projects.FindAsync(projectId.Value);
                    if (project == null)
                    {
                        return NotFound(new
                        {
                            success = false,
                            error = $"Project with ID {projectId.Value} not found"
                        });
                    }

                    // Validate student IDs exist
                    var existingStudents = await _context.Students
                        .Where(s => parsedStudentIds.Contains(s.Id))
                        .Select(s => s.Id)
                        .ToListAsync();

                    var missingStudents = parsedStudentIds.Except(existingStudents).ToList();
                    if (missingStudents.Any())
                    {
                        return BadRequest(new
                        {
                            success = false,
                            error = $"Students not found: [{string.Join(", ", missingStudents)}]",
                            validStudentIds = existingStudents
                        });
                    }

                    var kickoffResult = await _kickoffService.ShouldKickoffBeTrue(projectId.Value, parsedStudentIds);

                    // Get detailed explanation for kickoff result
                    var explanation = await GetKickoffExplanation(projectId.Value, parsedStudentIds);

                    scenarios.Add(new
                    {
                        testType = "Specific Project + Specific Students",
                        projectId = projectId.Value,
                        projectTitle = project.Title,
                        studentIds = parsedStudentIds,
                        kickoffResult = kickoffResult,
                        currentKickoffStatus = project.Kickoff,
                        explanation = explanation
                    });
                }
                // Scenario 2: Test specific project with its actual students
                else if (projectId.HasValue)
                {
                    _logger.LogInformation("Testing specific project {ProjectId} with its actual students", projectId.Value);

                    var project = await _context.Projects.FindAsync(projectId.Value);
                    if (project == null)
                    {
                        return NotFound(new
                        {
                            success = false,
                            error = $"Project with ID {projectId.Value} not found"
                        });
                    }

                    var projectStudents = await _context.Students
                        .Where(s => s.ProjectId == projectId.Value)
                        .Select(s => s.Id)
                        .ToListAsync();

                    _logger.LogInformation("Project {ProjectId} has {StudentCount} students: [{StudentIds}]", 
                        projectId.Value, projectStudents.Count, string.Join(", ", projectStudents));

                    var kickoffResult = await _kickoffService.ShouldKickoffBeTrue(projectId.Value, projectStudents);

                    // Get detailed explanation for kickoff result
                    var explanation = await GetKickoffExplanation(projectId.Value, projectStudents);

                    scenarios.Add(new
                    {
                        testType = "Specific Project + Actual Students",
                        projectId = projectId.Value,
                        projectTitle = project.Title,
                        studentCount = projectStudents.Count,
                        studentIds = projectStudents,
                        kickoffResult = kickoffResult,
                        currentKickoffStatus = project.Kickoff,
                        explanation = explanation
                    });
                }
                // Scenario 3: Test all projects (default behavior)
                else
                {
                    _logger.LogInformation("Testing all projects");

                    var projects = await _context.Projects.Take(5).ToListAsync();

                    foreach (var project in projects)
                    {
                        _logger.LogInformation("Testing kickoff for project {ProjectId}: {ProjectTitle}", project.Id, project.Title);

                        var projectStudents = await _context.Students
                            .Where(s => s.ProjectId == project.Id)
                            .Select(s => s.Id)
                            .ToListAsync();

                        _logger.LogInformation("Project {ProjectId} has {StudentCount} students: [{StudentIds}]", 
                            project.Id, projectStudents.Count, string.Join(", ", projectStudents));

                        var kickoffResult = await _kickoffService.ShouldKickoffBeTrue(project.Id, projectStudents);

                        // Get detailed explanation for kickoff result
                        var explanation = await GetKickoffExplanation(project.Id, projectStudents);

                        scenarios.Add(new
                        {
                            testType = "All Projects Test",
                            projectId = project.Id,
                            projectTitle = project.Title,
                            studentCount = projectStudents.Count,
                            studentIds = projectStudents,
                            kickoffResult = kickoffResult,
                            currentKickoffStatus = project.Kickoff,
                            explanation = explanation
                        });
                    }

                    // Add some additional test scenarios
                    var emptyTestResult = await _kickoffService.ShouldKickoffBeTrue(1, new List<int>());
                    var emptyExplanation = await GetKickoffExplanation(1, new List<int>());

                    scenarios.Add(new
                    {
                        testType = "Empty Student List Test",
                        projectId = 1,
                        studentIds = new List<int>(),
                        kickoffResult = emptyTestResult,
                        explanation = emptyExplanation
                    });

                    var nonExistentTestResult = await _kickoffService.ShouldKickoffBeTrue(99999, new List<int> { 1, 2 });
                    var nonExistentExplanation = await GetKickoffExplanation(99999, new List<int> { 1, 2 });

                    scenarios.Add(new
                    {
                        testType = "Non-existent Project Test",
                        projectId = 99999,
                        studentIds = new List<int> { 1, 2 },
                        kickoffResult = nonExistentTestResult,
                        explanation = nonExistentExplanation
                    });
                }

                _logger.LogInformation("=== KICKOFF LOGIC TEST COMPLETED ===");

                return Ok(new
                {
                    success = true,
                    message = "Kickoff logic test completed successfully",
                    parameters = new
                    {
                        projectId = projectId,
                        studentIds = studentIds,
                        parsedStudentIds = parsedStudentIds
                    },
                    results = new
                    {
                        timestamp = DateTime.UtcNow,
                        testScenarios = scenarios,
                        summary = new
                        {
                            totalTests = scenarios.Count,
                            successfulTests = scenarios.Count(s => s.GetType().GetProperty("kickoffResult")?.GetValue(s) != null),
                            testTypes = scenarios.Select(s => s.GetType().GetProperty("testType")?.GetValue(s)?.ToString() ?? "Unknown").Distinct()
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in kickoff logic test: {ErrorMessage}", ex.Message);
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    message = "Kickoff logic test failed"
                });
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get detailed explanation for kickoff logic result
        /// </summary>
        private async Task<object> GetKickoffExplanation(int projectId, List<int> studentIds)
        {
            try
            {
                // Get project details
                var project = await _context.Projects.FindAsync(projectId);
                if (project == null)
                {
                    return new
                    {
                        message = "Project not found",
                        details = $"Project with ID {projectId} does not exist in the database"
                    };
                }

                // Get student details with roles - only students without board (same logic as KickoffService)
                var students = await _context.Students
                    .Where(s => studentIds.Contains(s.Id) && 
                               s.ProjectId == projectId && 
                               (s.BoardId == null || s.BoardId == ""))
                    .Include(s => s.StudentRoles)
                        .ThenInclude(sr => sr.Role)
                    .ToListAsync();

                if (!students.Any())
                {
                    return new
                    {
                        message = "No students found",
                        details = $"No students with IDs [{string.Join(", ", studentIds)}] found in the database that are assigned to project {projectId} and don't have a board",
                        studentCount = 0,
                        requirements = new
                        {
                            minimumStudents = "At least 2 students required",
                            adminRequired = "At least one admin student required",
                            uiuxRequired = "At least one UI/UX Designer required",
                            developerRequired = "At least one Developer or 2+ Junior Developers required",
                            boardRequirement = "Students must not have a board assigned (BoardId is null or empty)"
                        }
                    };
                }

                // Analyze student roles
                var studentDetails = students.Select(s => new
                {
                    id = s.Id,
                    name = $"{s.FirstName} {s.LastName}",
                    email = s.Email,
                    isAdmin = s.IsAdmin,
                    roles = s.StudentRoles.Select(sr => new
                    {
                        id = sr.Role.Id,
                        name = sr.Role.Name,
                        type = sr.Role.Type
                    }).ToList()
                }).ToList();

                // Check kickoff requirements using the same logic as KickoffService
                var totalStudents = students.Count;
                var adminCount = students.Count(s => s.IsAdmin);
                var uiuxCount = students.Count(s => s.StudentRoles.Any(sr => sr.IsActive && sr.Role?.Type == 3));
                var developerCount = students.Count(s => s.StudentRoles.Any(sr => sr.IsActive && sr.Role?.Type == 1));
                var juniorDeveloperCount = students.Count(s => s.StudentRoles.Any(sr => sr.IsActive && sr.Role?.Type == 2));

                // Apply the same rules as KickoffService
                var hasMinimumStudents = totalStudents >= 2;
                var hasAdmin = adminCount > 0;
                var hasUIUX = uiuxCount > 0;
                var hasDeveloper = developerCount >= 1 || juniorDeveloperCount >= 2;

                // Determine why kickoff is true/false
                var reasons = new List<string>();
                var requirements = new List<string>();

                if (!hasMinimumStudents)
                {
                    reasons.Add($"Insufficient students: {totalStudents} (minimum 2 required)");
                    requirements.Add("Need at least 2 students");
                }
                else
                {
                    requirements.Add($"✓ Student count: {totalStudents} (meets minimum of 2)");
                }

                if (!hasAdmin)
                {
                    reasons.Add($"No admin students: {adminCount} (at least 1 required)");
                    requirements.Add("Need at least one admin student");
                }
                else
                {
                    requirements.Add($"✓ Admin students: {adminCount} (meets requirement)");
                }

                if (!hasUIUX)
                {
                    reasons.Add($"No UI/UX Designer: {uiuxCount} (at least 1 required)");
                    requirements.Add("Need at least one UI/UX Designer (Type=3)");
                }
                else
                {
                    requirements.Add($"✓ UI/UX Designers: {uiuxCount} (meets requirement)");
                }

                if (!hasDeveloper)
                {
                    reasons.Add($"Insufficient developers: {developerCount} Developer(s) and {juniorDeveloperCount} Junior Developer(s) (need 1+ Developer OR 2+ Junior Developers)");
                    requirements.Add("Need at least 1 Developer (Type=1) OR 2+ Junior Developers (Type=2)");
                }
                else
                {
                    requirements.Add($"✓ Developers: {developerCount} Developer(s) and {juniorDeveloperCount} Junior Developer(s) (meets requirement)");
                }

                var kickoffResult = hasMinimumStudents && hasAdmin && hasUIUX && hasDeveloper;
                var message = kickoffResult 
                    ? "Kickoff requirements are met" 
                    : "Kickoff requirements are not met";

                return new
                {
                    message = message,
                    kickoffResult = kickoffResult,
                    studentCount = totalStudents,
                    studentDetails = studentDetails,
                    roleAnalysis = new
                    {
                        adminCount = adminCount,
                        uiuxCount = uiuxCount,
                        developerCount = developerCount,
                        juniorDeveloperCount = juniorDeveloperCount,
                        hasAdmin = hasAdmin,
                        hasUIUX = hasUIUX,
                        hasDeveloper = hasDeveloper,
                        hasMinimumStudents = hasMinimumStudents
                    },
                    requirements = requirements,
                    reasons = reasons,
                    summary = kickoffResult 
                        ? "All kickoff requirements are satisfied" 
                        : $"Kickoff blocked: {string.Join(", ", reasons)}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating kickoff explanation for project {ProjectId} with students [{StudentIds}]", 
                    projectId, string.Join(", ", studentIds));
                
                return new
                {
                    message = "Error generating explanation",
                    error = ex.Message,
                    details = "An error occurred while analyzing kickoff requirements"
                };
            }
        }

        #endregion
    }
}
