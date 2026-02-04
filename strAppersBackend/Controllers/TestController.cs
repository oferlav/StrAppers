using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
// using strAppersBackend.Services; // SLACK TEMPORARILY DISABLED
using System.Text.Json;
using System.Net.Http;
using Npgsql;

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TestController> _logger;
        private readonly IKickoffService _kickoffService;
        private readonly IAIService _aiService;
        private readonly ISmtpEmailService _smtpEmailService;
        private readonly IOptions<SystemDesignAIAgentConfig> _systemDesignConfig;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        // private readonly SlackService _slackService; // SLACK TEMPORARILY DISABLED

        public TestController(
            ApplicationDbContext context,
            ILogger<TestController> logger,
            IKickoffService kickoffService,
            IAIService aiService,
            IOptions<SystemDesignAIAgentConfig> systemDesignConfig,
            ISmtpEmailService smtpEmailService,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory) // SlackService slack service disabled
        {
            _context = context;
            _logger = logger;
            _kickoffService = kickoffService;
            _aiService = aiService;
            _systemDesignConfig = systemDesignConfig;
            _smtpEmailService = smtpEmailService;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            // _slackService = slackService; // SLACK TEMPORARILY DISABLED
        }

        #region Database Test Methods

        /// <summary>
        /// Test endpoint for student-generated backends - queries TestProjects table
        /// This endpoint is included in generated student backends and should work with their Neon databases.
        /// For the main application, this gracefully handles the missing TestProjects table.
        /// </summary>
        [HttpGet("")]
        public async Task<ActionResult> GetAll()
        {
            try
            {
                // Try to query TestProjects table (exists in student Neon databases, may not exist in main app database)
                using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection") 
                    ?? Environment.GetEnvironmentVariable("DATABASE_URL"));
                
                await connection.OpenAsync();
                
                var quote = Convert.ToChar(34).ToString(); // Double quote for PostgreSQL identifier quoting
                var sql = $"SELECT {quote}Id{quote}, {quote}Name{quote} FROM {quote}TestProjects{quote} ORDER BY {quote}Id{quote}";
                
                var projects = new List<object>();
                using var cmd = new NpgsqlCommand(sql, connection);
                using var reader = await cmd.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    projects.Add(new
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1)
                    });
                }
                
                return Ok(projects);
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01") // Table does not exist
            {
                // TestProjects table doesn't exist in this database (likely main app database)
                // Return empty list gracefully - this is expected for the main application
                _logger.LogInformation("TestProjects table not found in database - returning empty list (this is expected for main application database)");
                return Ok(new List<object>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying TestProjects table");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Failed to query TestProjects table"
                });
            }
        }

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
        /// Retrieve Google Workspace OAuth configuration details for validation
        /// </summary>
        [HttpGet("googleworkspace/app-info")]
        public ActionResult GetGoogleWorkspaceAppInfo()
        {
            try
            {
                var oauthSection = _configuration.GetSection("GoogleWorkspace:OAuth");
                if (!oauthSection.Exists())
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Google Workspace OAuth configuration not found"
                    });
                }

                var info = new
                {
                    success = true,
                    appName = oauthSection["AppName"] ?? "not-configured",
                    clientId = oauthSection["ClientId"] ?? "not-configured",
                    clientSecretConfigured = !string.IsNullOrEmpty(oauthSection["ClientSecret"]),
                    authorizedJavaScriptOrigins = oauthSection.GetSection("AuthorizedJavaScriptOrigins").Get<string[]>() ?? Array.Empty<string>(),
                    authorizedRedirectUris = oauthSection.GetSection("AuthorizedRedirectUris").Get<string[]>() ?? Array.Empty<string>()
                };

                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Google Workspace app info");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to retrieve Google Workspace app info",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Send a test meeting invitation using the configured SMTP settings
        /// </summary>
        [HttpPost("email/test-smtp")]
        public async Task<ActionResult> SendTestSmtpEmail([FromBody] TestSmtpEmailRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.RecipientEmail))
                {
                    return BadRequest(new { success = false, message = "RecipientEmail is required" });
                }

                var subject = string.IsNullOrWhiteSpace(request.Subject) ? "Skill-In SMTP Test Meeting" : request.Subject;
                var description = string.IsNullOrWhiteSpace(request.Description)
                    ? "This is a test meeting invite generated by the Skill-In SMTP configuration."
                    : request.Description;
                var meetingLink = string.IsNullOrWhiteSpace(request.MeetingLink)
                    ? "https://meet.google.com/lookup/skill-in-test"
                    : request.MeetingLink;

                var startTime = request.StartTime ?? DateTime.UtcNow.AddMinutes(30);
                var endTime = request.EndTime ?? startTime.AddMinutes(30);

                _logger.LogInformation("Sending SMTP test email to {Recipient} from controller", request.RecipientEmail);

                var success = await _smtpEmailService.SendMeetingEmailAsync(
                    request.RecipientEmail,
                    subject,
                    startTime,
                    endTime,
                    meetingLink,
                    description
                );

                return Ok(new
                {
                    success,
                    message = success
                        ? "SMTP test email sent successfully"
                        : "SMTP test email failed to send",
                    details = new
                    {
                        request.RecipientEmail,
                        subject,
                        startTime = startTime.ToString("o"),
                        endTime = endTime.ToString("o"),
                        meetingLink
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP test email failed: {Message}", ex.Message);
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while sending the SMTP test email",
                    error = ex.Message
                });
            }
        }

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

        #region Railway Test Methods

        /// <summary>
        /// Test method: set the deployment trigger branch for a Railway service by name.
        /// Uses Railway API (serviceConnect) to connect the service to the same repo with the new target branch.
        /// POST /api/Test/Railway-set-trigger-branch
        /// Body: { "railwayServiceName": "webapi_xxx", "targetBranchName": "1-B", "repo": "owner/repo-name" }
        /// Optional: projectId (defaults to Railway:SharedProjectId from config).
        /// </summary>
        [HttpPost("Railway-set-trigger-branch")]
        public async Task<ActionResult> RailwaySetTriggerBranch([FromBody] RailwaySetTriggerBranchRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RailwayServiceName) || string.IsNullOrWhiteSpace(request.TargetBranchName))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "RailwayServiceName and TargetBranchName are required.",
                    example = new { railwayServiceName = "webapi_697e00d2f9cb8d26a7271afa", targetBranchName = "1-B", repo = "skill-in-projects/backend_697e00d2f9cb8d26a7271afa" }
                });
            }

            var railwayApiToken = _configuration["Railway:ApiToken"];
            var railwayApiUrl = _configuration["Railway:ApiUrl"] ?? "https://backboard.railway.com/graphql/v2";
            var projectId = request.ProjectId ?? _configuration["Railway:SharedProjectId"];

            if (string.IsNullOrWhiteSpace(railwayApiToken) || railwayApiToken == "your-railway-api-token-here")
            {
                return BadRequest(new { success = false, message = "Railway:ApiToken is not configured." });
            }
            if (string.IsNullOrWhiteSpace(projectId))
            {
                return BadRequest(new { success = false, message = "ProjectId is required (provide in body or set Railway:SharedProjectId)." });
            }

            var repo = request.Repo?.Trim();
            if (string.IsNullOrWhiteSpace(repo))
            {
                return BadRequest(new { success = false, message = "Repo is required (owner/repo-name) for serviceConnect." });
            }
            if (repo.Contains("https://github.com/", StringComparison.OrdinalIgnoreCase))
            {
                repo = repo.Replace("https://github.com/", "", StringComparison.OrdinalIgnoreCase).TrimEnd('/');
            }

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", railwayApiToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            // 1. Get project with services to find service ID by name
            var projectQuery = new
            {
                query = @"query project($id: String!) {
  project(id: $id) {
    id
    name
    services {
      edges {
        node {
          id
          name
        }
      }
    }
    environments {
      edges {
        node {
          id
          name
        }
      }
    }
  }
}",
                variables = new { id = projectId }
            };
            var projectQueryBody = JsonSerializer.Serialize(projectQuery);
            var projectResponse = await httpClient.PostAsync(railwayApiUrl, new StringContent(projectQueryBody, System.Text.Encoding.UTF8, "application/json"));
            var projectResponseContent = await projectResponse.Content.ReadAsStringAsync();

            if (!projectResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Railway project query failed: {StatusCode} {Content}", projectResponse.StatusCode, projectResponseContent);
                return StatusCode((int)projectResponse.StatusCode, new { success = false, message = "Railway API project query failed", detail = projectResponseContent });
            }

            string? serviceId = null;
            string? environmentId = null;
            using (var projectDoc = JsonDocument.Parse(projectResponseContent))
            {
                var root = projectDoc.RootElement;
                if (root.TryGetProperty("data", out var data) && data.TryGetProperty("project", out var project))
                {
                    if (project.TryGetProperty("services", out var services) && services.TryGetProperty("edges", out var edges))
                    {
                        foreach (var edge in edges.EnumerateArray())
                        {
                            if (edge.TryGetProperty("node", out var node) &&
                                node.TryGetProperty("name", out var nameProp) &&
                                string.Equals(nameProp.GetString(), request.RailwayServiceName.Trim(), StringComparison.OrdinalIgnoreCase))
                            {
                                if (node.TryGetProperty("id", out var idProp))
                                {
                                    serviceId = idProp.GetString();
                                    break;
                                }
                            }
                        }
                    }
                    if (project.TryGetProperty("environments", out var envs) && envs.TryGetProperty("edges", out var envEdges) && envEdges.GetArrayLength() > 0)
                    {
                        var firstEnv = envEdges[0];
                        if (firstEnv.TryGetProperty("node", out var envNode) && envNode.TryGetProperty("id", out var envIdProp))
                        {
                            environmentId = envIdProp.GetString();
                        }
                    }
                }
                if (root.TryGetProperty("errors", out var errors))
                {
                    _logger.LogWarning("Railway API returned errors: {Errors}", errors.GetRawText());
                    return Ok(new { success = false, message = "Railway API returned errors", errors = errors.GetRawText() });
                }
            }

            if (string.IsNullOrEmpty(serviceId))
            {
                return NotFound(new
                {
                    success = false,
                    message = $"No Railway service found with name '{request.RailwayServiceName}' in project {projectId}.",
                    projectId
                });
            }

            // 2. serviceConnect(id, { repo, branch }) to set the target branch
            var connectInput = new { repo = repo, branch = request.TargetBranchName.Trim() };
            var connectMutation = new
            {
                query = @"mutation serviceConnect($id: String!, $input: ServiceConnectInput!) {
  serviceConnect(id: $id, input: $input) {
    id
  }
}",
                variables = new { id = serviceId, input = connectInput }
            };
            var connectBody = JsonSerializer.Serialize(connectMutation);
            _logger.LogInformation("Railway serviceConnect request: url={Url} serviceId={ServiceId} input={Input}", railwayApiUrl, serviceId, JsonSerializer.Serialize(connectInput));

            var connectResponse = await httpClient.PostAsync(railwayApiUrl, new StringContent(connectBody, System.Text.Encoding.UTF8, "application/json"));
            var connectContent = await connectResponse.Content.ReadAsStringAsync();

            _logger.LogInformation("Railway serviceConnect response: statusCode={StatusCode} body={Body}", (int)connectResponse.StatusCode, connectContent);

            if (!connectResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Railway serviceConnect failed: {StatusCode} {Content}", connectResponse.StatusCode, connectContent);
                return StatusCode((int)connectResponse.StatusCode, new { success = false, message = "Railway serviceConnect failed", detail = connectContent, railwayResponse = connectContent });
            }

            bool hasGraphQLErrors = false;
            string? graphqlErrors = null;
            bool hasDataConnect = false;
            using (var connectDoc = JsonDocument.Parse(connectContent))
            {
                var root = connectDoc.RootElement;
                if (root.TryGetProperty("errors", out var errs))
                {
                    hasGraphQLErrors = true;
                    graphqlErrors = errs.GetRawText();
                    _logger.LogWarning("Railway serviceConnect returned GraphQL errors: {Errors}", graphqlErrors);
                }
                if (root.TryGetProperty("data", out var data) && data.TryGetProperty("serviceConnect", out var sc))
                {
                    hasDataConnect = sc.ValueKind != JsonValueKind.Null;
                    if (!hasDataConnect)
                        _logger.LogWarning("Railway serviceConnect returned data.serviceConnect null. Full response: {Body}", connectContent);
                }
                else
                {
                    _logger.LogWarning("Railway serviceConnect response has no data.serviceConnect. Full response: {Body}", connectContent);
                }
            }

            if (hasGraphQLErrors)
            {
                return Ok(new
                {
                    success = false,
                    message = "Railway serviceConnect returned errors (check errors and railwayResponse).",
                    errors = graphqlErrors,
                    railwayResponse = connectContent
                });
            }

            if (!hasDataConnect)
            {
                return Ok(new
                {
                    success = false,
                    message = "Railway returned 200 but no data.serviceConnect (mutation may not have applied). Check railwayResponse.",
                    railwayResponse = connectContent,
                    serviceId,
                    serviceName = request.RailwayServiceName,
                    targetBranch = request.TargetBranchName.Trim(),
                    repo
                });
            }

            _logger.LogInformation("Railway service '{ServiceName}' (id={ServiceId}) trigger branch set to '{Branch}' (repo={Repo})", request.RailwayServiceName, serviceId, request.TargetBranchName, repo);
            return Ok(new
            {
                success = true,
                message = $"Trigger branch for service '{request.RailwayServiceName}' set to '{request.TargetBranchName}'.",
                serviceId,
                serviceName = request.RailwayServiceName,
                targetBranch = request.TargetBranchName.Trim(),
                repo,
                environmentId,
                railwayResponse = connectContent
            });
        }

        /// <summary>
        /// Test method: get the current deployment trigger branch (and repo) for a Railway service by name.
        /// GET /api/Test/Railway-get-trigger-branch?railwayServiceName=webapi_xxx&amp;projectId=optional
        /// Returns service id, name, and current repo/branch if exposed by Railway API.
        /// </summary>
        [HttpGet("Railway-get-trigger-branch")]
        public async Task<ActionResult> RailwayGetTriggerBranch(
            [FromQuery] string railwayServiceName,
            [FromQuery] string? projectId = null)
        {
            if (string.IsNullOrWhiteSpace(railwayServiceName))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "railwayServiceName query parameter is required.",
                    example = "/api/Test/Railway-get-trigger-branch?railwayServiceName=webapi_697e00d2f9cb8d26a7271afa"
                });
            }

            var railwayApiToken = _configuration["Railway:ApiToken"];
            var railwayApiUrl = _configuration["Railway:ApiUrl"] ?? "https://backboard.railway.com/graphql/v2";
            var resolvedProjectId = projectId ?? _configuration["Railway:SharedProjectId"];

            if (string.IsNullOrWhiteSpace(railwayApiToken) || railwayApiToken == "your-railway-api-token-here")
            {
                return BadRequest(new { success = false, message = "Railway:ApiToken is not configured." });
            }
            if (string.IsNullOrWhiteSpace(resolvedProjectId))
            {
                return BadRequest(new { success = false, message = "projectId is required (query param or Railway:SharedProjectId)." });
            }

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", railwayApiToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            // 1. Get project with services to find service ID by name
            var projectQuery = new
            {
                query = @"query project($id: String!) {
  project(id: $id) {
    id
    name
    services {
      edges {
        node {
          id
          name
        }
      }
    }
  }
}",
                variables = new { id = resolvedProjectId }
            };
            var projectQueryBody = JsonSerializer.Serialize(projectQuery);
            var projectResponse = await httpClient.PostAsync(railwayApiUrl, new StringContent(projectQueryBody, System.Text.Encoding.UTF8, "application/json"));
            var projectResponseContent = await projectResponse.Content.ReadAsStringAsync();

            if (!projectResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Railway project query failed: {StatusCode} {Content}", projectResponse.StatusCode, projectResponseContent);
                return StatusCode((int)projectResponse.StatusCode, new { success = false, message = "Railway API project query failed", detail = projectResponseContent });
            }

            string? serviceId = null;
            using (var projectDoc = JsonDocument.Parse(projectResponseContent))
            {
                var root = projectDoc.RootElement;
                if (root.TryGetProperty("errors", out var errs))
                {
                    return Ok(new { success = false, message = "Railway API returned errors", errors = errs.GetRawText() });
                }
                if (root.TryGetProperty("data", out var data) && data.TryGetProperty("project", out var project))
                {
                    if (project.TryGetProperty("services", out var services) && services.TryGetProperty("edges", out var edges))
                    {
                        foreach (var edge in edges.EnumerateArray())
                        {
                            if (edge.TryGetProperty("node", out var node) &&
                                node.TryGetProperty("name", out var nameProp) &&
                                string.Equals(nameProp.GetString(), railwayServiceName.Trim(), StringComparison.OrdinalIgnoreCase))
                            {
                                if (node.TryGetProperty("id", out var idProp))
                                {
                                    serviceId = idProp.GetString();
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(serviceId))
            {
                return NotFound(new
                {
                    success = false,
                    message = $"No Railway service found with name '{railwayServiceName}' in project {resolvedProjectId}.",
                    projectId = resolvedProjectId
                });
            }

            // 2. Query service by ID (Railway Service type does not expose source.repo/branch in the public schema)
            var serviceQuery = new
            {
                query = @"query service($id: String!) {
  service(id: $id) {
    id
    name
    projectId
  }
}",
                variables = new { id = serviceId }
            };
            var serviceQueryBody = JsonSerializer.Serialize(serviceQuery);
            var serviceResponse = await httpClient.PostAsync(railwayApiUrl, new StringContent(serviceQueryBody, System.Text.Encoding.UTF8, "application/json"));
            var serviceResponseContent = await serviceResponse.Content.ReadAsStringAsync();

            if (!serviceResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Railway service query failed: {StatusCode} {Content}", serviceResponse.StatusCode, serviceResponseContent);
                return StatusCode((int)serviceResponse.StatusCode, new { success = false, message = "Railway API service query failed", detail = serviceResponseContent });
            }

            using (var serviceDoc = JsonDocument.Parse(serviceResponseContent))
            {
                var root = serviceDoc.RootElement;
                if (root.TryGetProperty("errors", out var serviceErrs))
                {
                    return Ok(new
                    {
                        success = false,
                        serviceId,
                        serviceName = railwayServiceName.Trim(),
                        currentBranch = (string?)null,
                        currentRepo = (string?)null,
                        branchSet = false,
                        message = "Railway API returned errors.",
                        errors = serviceErrs.GetRawText()
                    });
                }
            }

            return Ok(new
            {
                success = true,
                serviceId,
                serviceName = railwayServiceName.Trim(),
                currentBranch = (string?)null,
                currentRepo = (string?)null,
                branchSet = false,
                message = "Service found. Railway API does not expose trigger branch/repo on Service type; use set-trigger-branch to set, then verify via deployments or UI."
            });
        }

        /// <summary>
        /// Test method: query the same Railway service to verify if the deployment trigger branch was actually set.
        /// GET /api/Test/Railway-branch-was-set?railwayServiceName=webapi_xxx&amp;expectedBranch=1-B&amp;projectId=optional
        /// Returns branchWasSet (true if API reports a branch), currentBranch, and optionally matchesExpected when expectedBranch is provided.
        /// </summary>
        [HttpGet("Railway-branch-was-set")]
        public async Task<ActionResult> RailwayBranchWasSet(
            [FromQuery] string railwayServiceName,
            [FromQuery] string? expectedBranch = null,
            [FromQuery] string? projectId = null)
        {
            if (string.IsNullOrWhiteSpace(railwayServiceName))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "railwayServiceName query parameter is required.",
                    example = "/api/Test/Railway-branch-was-set?railwayServiceName=webapi_xxx&expectedBranch=1-B"
                });
            }

            var railwayApiToken = _configuration["Railway:ApiToken"];
            var railwayApiUrl = _configuration["Railway:ApiUrl"] ?? "https://backboard.railway.com/graphql/v2";
            var resolvedProjectId = projectId ?? _configuration["Railway:SharedProjectId"];

            if (string.IsNullOrWhiteSpace(railwayApiToken) || railwayApiToken == "your-railway-api-token-here")
            {
                return BadRequest(new { success = false, message = "Railway:ApiToken is not configured." });
            }
            if (string.IsNullOrWhiteSpace(resolvedProjectId))
            {
                return BadRequest(new { success = false, message = "projectId is required (query param or Railway:SharedProjectId)." });
            }

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", railwayApiToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            // 1. Get project with services to find service ID by name
            var projectQuery = new
            {
                query = @"query project($id: String!) {
  project(id: $id) {
    id
    name
    services {
      edges {
        node {
          id
          name
        }
      }
    }
  }
}",
                variables = new { id = resolvedProjectId }
            };
            var projectQueryBody = JsonSerializer.Serialize(projectQuery);
            var projectResponse = await httpClient.PostAsync(railwayApiUrl, new StringContent(projectQueryBody, System.Text.Encoding.UTF8, "application/json"));
            var projectResponseContent = await projectResponse.Content.ReadAsStringAsync();

            if (!projectResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Railway project query failed: {StatusCode} {Content}", projectResponse.StatusCode, projectResponseContent);
                return StatusCode((int)projectResponse.StatusCode, new { success = false, message = "Railway API project query failed", detail = projectResponseContent });
            }

            string? serviceId = null;
            using (var projectDoc = JsonDocument.Parse(projectResponseContent))
            {
                var root = projectDoc.RootElement;
                if (root.TryGetProperty("errors", out var errs))
                {
                    return Ok(new { success = false, message = "Railway API returned errors", errors = errs.GetRawText() });
                }
                if (root.TryGetProperty("data", out var data) && data.TryGetProperty("project", out var project))
                {
                    if (project.TryGetProperty("services", out var services) && services.TryGetProperty("edges", out var edges))
                    {
                        foreach (var edge in edges.EnumerateArray())
                        {
                            if (edge.TryGetProperty("node", out var node) &&
                                node.TryGetProperty("name", out var nameProp) &&
                                string.Equals(nameProp.GetString(), railwayServiceName.Trim(), StringComparison.OrdinalIgnoreCase))
                            {
                                if (node.TryGetProperty("id", out var idProp))
                                {
                                    serviceId = idProp.GetString();
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(serviceId))
            {
                return NotFound(new
                {
                    success = false,
                    message = $"No Railway service found with name '{railwayServiceName}' in project {resolvedProjectId}.",
                    projectId = resolvedProjectId
                });
            }

            // 2. Query service by ID (Railway Service type does not expose source.repo/branch in the public schema)
            var serviceQuery = new
            {
                query = @"query service($id: String!) {
  service(id: $id) {
    id
    name
    projectId
  }
}",
                variables = new { id = serviceId }
            };
            var serviceQueryBody = JsonSerializer.Serialize(serviceQuery);
            var serviceResponse = await httpClient.PostAsync(railwayApiUrl, new StringContent(serviceQueryBody, System.Text.Encoding.UTF8, "application/json"));
            var serviceResponseContent = await serviceResponse.Content.ReadAsStringAsync();

            if (!serviceResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Railway service query failed: {StatusCode} {Content}", serviceResponse.StatusCode, serviceResponseContent);
                return StatusCode((int)serviceResponse.StatusCode, new { success = false, message = "Railway API service query failed", detail = serviceResponseContent });
            }

            using (var serviceDoc = JsonDocument.Parse(serviceResponseContent))
            {
                var root = serviceDoc.RootElement;
                if (root.TryGetProperty("errors", out var serviceErrs))
                {
                    return Ok(new
                    {
                        success = true,
                        serviceId,
                        serviceName = railwayServiceName.Trim(),
                        branchWasSet = false,
                        currentBranch = (string?)null,
                        currentRepo = (string?)null,
                        matchesExpected = (bool?)null,
                        message = "Railway API returned errors; cannot read branch.",
                        errors = serviceErrs.GetRawText()
                    });
                }
            }

            return Ok(new
            {
                success = true,
                serviceId,
                serviceName = railwayServiceName.Trim(),
                branchWasSet = false,
                currentBranch = (string?)null,
                currentRepo = (string?)null,
                matchesExpected = (bool?)null,
                message = "Service found. Railway API does not expose trigger branch on Service type; verify via Railway UI or deployments."
            });
        }

        /// <summary>
        /// Request body for Railway-set-trigger-branch test endpoint.
        /// </summary>
        public class RailwaySetTriggerBranchRequest
        {
            /// <summary>Railway service name (e.g. webapi_697e00d2f9cb8d26a7271afa).</summary>
            public string RailwayServiceName { get; set; } = string.Empty;
            /// <summary>Target branch name to set as deployment trigger (e.g. 1-B).</summary>
            public string TargetBranchName { get; set; } = string.Empty;
            /// <summary>GitHub repo in form owner/repo (required for serviceConnect).</summary>
            public string? Repo { get; set; }
            /// <summary>Optional Railway project ID; defaults to Railway:SharedProjectId.</summary>
            public string? ProjectId { get; set; }
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

        /// <summary>
        /// Test method to get the actual prompt sent to Trello planning AI
        /// </summary>
        [HttpGet("trello-planning-prompt")]
        public async Task<ActionResult> TestTrelloPlanningPrompt([FromQuery] int projectId, [FromQuery] string? roleIds = null)
        {
            try
            {
                _logger.LogInformation("Testing Trello planning prompt for Project {ProjectId}", projectId);

                // Get project with SystemDesign
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId);

                if (project == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Project with ID {projectId} not found"
                    });
                }

                // Parse role IDs if provided
                List<int> requestedRoleIds = new List<int>();
                if (!string.IsNullOrEmpty(roleIds))
                {
                    try
                    {
                        requestedRoleIds = roleIds.Split(',')
                            .Select(id => int.Parse(id.Trim()))
                            .ToList();
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Invalid role IDs format: {ex.Message}. Expected format: '1,2,3'"
                        });
                    }
                }

                // Get students for the project
                var students = await _context.Students
                    .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                    .Where(s => s.ProjectId == projectId)
                    .ToListAsync();

                // Get roles from database
                var roles = await _context.Roles.ToListAsync();
                
                List<RoleInfo> roleGroups;
                
                if (requestedRoleIds.Any())
                {
                    // Use provided role IDs and fetch role names from DB
                    roleGroups = roles
                        .Where(r => requestedRoleIds.Contains(r.Id))
                        .Select(r => new RoleInfo
                        {
                            RoleId = r.Id,
                            RoleName = r.Name,
                            StudentCount = students
                                .Where(s => s.StudentRoles != null)
                                .SelectMany(s => s.StudentRoles)
                                .Count(sr => sr.RoleId == r.Id)
                        })
                        .ToList();
                        
                    _logger.LogInformation("Using provided role IDs: {RoleIds}", string.Join(", ", requestedRoleIds));
                }
                else
                {
                    // Extract team roles from students (fallback)
                    roleGroups = students
                        .Where(s => s.StudentRoles != null)
                        .SelectMany(s => s.StudentRoles)
                        .Where(sr => sr?.Role != null)
                        .GroupBy(sr => new { sr.RoleId, sr.Role.Name })
                        .Select(g => new RoleInfo
                        {
                            RoleId = g.Key.RoleId,
                            RoleName = g.Key.Name,
                            StudentCount = g.Count()
                        })
                        .ToList();
                        
                    _logger.LogInformation("Extracted roles from students in project");
                }

                if (!roleGroups.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = requestedRoleIds.Any() 
                            ? $"No roles found for provided role IDs: {string.Join(", ", requestedRoleIds)}"
                            : $"No students or roles found for project {projectId}"
                    });
                }

                // Fetch ProjectModules for this project to pass module IDs to AI
                var projectModules = await _context.ProjectModules
                    .Where(pm => pm.ProjectId == projectId && pm.ModuleType != 3) // Exclude data model modules (ModuleType 3)
                    .OrderBy(pm => pm.Sequence)
                    .Select(pm => new ProjectModuleInfo
                    {
                        Id = pm.Id,
                        Title = pm.Title,
                        Description = pm.Description
                    })
                    .ToListAsync();
                
                // Create SprintPlanningRequest (same as in BoardsController)
                var sprintPlanRequest = new SprintPlanningRequest
                {
                    ProjectId = projectId,
                    ProjectLengthWeeks = 8, // Default project length
                    SprintLengthWeeks = 1,  // Default sprint length
                    StartDate = DateTime.UtcNow,
                    SystemDesign = project.SystemDesign,
                    TeamRoles = roleGroups,
                    ProjectModules = projectModules, // Include project modules with their database IDs
                    Students = students.Select(s => new StudentInfo
                    {
                        Id = s.Id,
                        Name = $"{s.FirstName} {s.LastName}",
                        Email = s.Email,
                        Roles = s.StudentRoles?.Select(sr => sr.Role?.Name ?? "Unknown").ToList() ?? new List<string>()
                    }).ToList()
                };

                // Get the actual prompt that would be sent to AI
                var prompt = BuildSprintPlanningPrompt(sprintPlanRequest);

                // Actually call the AI service to get the response
                _logger.LogInformation("Calling AI service for sprint planning...");
                var aiResponse = await _aiService.GenerateSprintPlanAsync(sprintPlanRequest);

                return Ok(new
                {
                    success = true,
                    projectId = projectId,
                    projectTitle = project.Title,
                    systemDesignLength = project.SystemDesign?.Length ?? 0,
                    requestedRoleIds = requestedRoleIds,
                    availableRoles = roles.Select(r => new { r.Id, r.Name }).ToList(),
                    teamRoles = roleGroups,
                    studentCount = students.Count,
                    prompt = prompt,
                    promptLength = prompt.Length,
                    aiResponse = aiResponse,
                    message = "Trello planning prompt and AI response generated successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Trello planning prompt for Project {ProjectId}", projectId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error generating Trello planning prompt or calling AI service",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Helper method to build sprint planning prompt (copied from AIService)
        /// </summary>
        private string BuildSprintPlanningPrompt(SprintPlanningRequest request)
        {
            var teamRolesText = string.Join(", ", request.TeamRoles.Select(r => $"{r.RoleName} ({r.StudentCount} students)"));
            var totalSprints = request.ProjectLengthWeeks / request.SprintLengthWeeks; // Calculate from project length and sprint length
            
            // Filter out Data Model section to save tokens and focus on system modules
            var filteredSystemDesign = FilterOutDataModelSection(request.SystemDesign);
            
            // Build role-specific task instructions
            var roleInstructions = BuildRoleSpecificInstructions(request.TeamRoles);
            
            return $@"Generate a detailed sprint plan for a {request.ProjectLengthWeeks}-week project with {request.SprintLengthWeeks}-week sprints.

TEAM: {teamRolesText}
START DATE: {request.StartDate:yyyy-MM-dd}
TOTAL SPRINTS: {totalSprints} (CONFIGURED - must fill ALL sprints even if fewer modules)

SYSTEM DESIGN:
{filteredSystemDesign ?? "No system design available"}

CRITICAL REQUIREMENTS:
- You MUST create exactly {totalSprints} sprints (configured number, not based on modules)
- Each sprint must have tasks for ALL roles: {string.Join(", ", request.TeamRoles.Select(r => r.RoleName))}
- Map system modules to sprints (distribute modules across sprints as needed)
- Create MULTIPLE specific tasks per role per sprint based on module inputs/outputs
- Tasks must be detailed and specific to the module's functionality
- ONE sprint must include database layer tasks for Backend/Full Stack developers
- Tasks must have: id, title, description, roleId, roleName, estimatedHours, priority, dependencies

ROLE-SPECIFIC TASK GENERATION:
{roleInstructions}

TASK GENERATION RULES:
- For each module, analyze the Inputs and Outputs described
- Create specific tasks that implement those inputs/outputs
- Each task should be specific and actionable (not generic)
- Multiple tasks per role per sprint are expected and encouraged
- If you run out of modules, create additional tasks like: testing, documentation, integration, optimization, deployment

SPRINT DISTRIBUTION STRATEGY:
- Distribute the {GetModuleCount(filteredSystemDesign)} system modules across {totalSprints} sprints
- One sprint should focus on database layer implementation
- Remaining sprints should cover all modules plus additional tasks
- Ensure every sprint has meaningful work for all roles
- If you have fewer modules than sprints, create additional tasks like: testing, documentation, integration, optimization, deployment

Return ONLY valid JSON with exactly {totalSprints} sprints (NO EPICS):
{{
  ""sprints"": [{{""sprintNumber"": 1, ""name"": ""Sprint 1"", ""startDate"": ""{request.StartDate:yyyy-MM-dd}"", ""endDate"": ""{request.StartDate.AddDays(request.SprintLengthWeeks * 7 - 1):yyyy-MM-dd}"", ""tasks"": [{{""id"": ""task1"", ""title"": ""Specific Task Title"", ""description"": ""Detailed task description based on module inputs/outputs"", ""roleId"": 1, ""roleName"": ""Role"", ""estimatedHours"": 8, ""priority"": 1, ""dependencies"": []}}], ""totalStoryPoints"": 10, ""roleWorkload"": {{""1"": 8}}}}],
  ""totalSprints"": {totalSprints},
  ""totalTasks"": 0,
  ""estimatedWeeks"": {request.ProjectLengthWeeks}
}}";
        }

        /// <summary>
        /// Builds role-specific task generation instructions (copied from AIService)
        /// </summary>
        private string BuildRoleSpecificInstructions(List<RoleInfo> teamRoles)
        {
            var instructions = new List<string>();
            
            foreach (var role in teamRoles)
            {
                switch (role.RoleName.ToLower())
                {
                    case "frontend developer":
                        instructions.Add($"- Frontend Developer: Focus on React/Vue/Angular components, user interfaces, client-side logic, responsive design, user experience, form validation, state management, API integration from frontend");
                        break;
                    case "backend developer":
                        instructions.Add($"- Backend Developer: Focus on API development, server-side logic, database design, authentication, authorization, data processing, business logic, microservices, database optimization");
                        break;
                    case "full stack developer":
                        instructions.Add($"- Full Stack Developer: Focus on both frontend and backend tasks, API integration, database design, full application features, end-to-end functionality, system integration");
                        break;
                    case "ui/ux designer":
                        instructions.Add($"- UI/UX Designer: Focus on user interface design, user experience research, wireframes, prototypes, visual design, accessibility, user testing, design systems, mockups");
                        break;
                    case "quality assurance":
                        instructions.Add($"- Quality Assurance: Focus on testing strategies, test case creation, automated testing, bug tracking, quality metrics, user acceptance testing, performance testing, security testing");
                        break;
                    case "project manager":
                        instructions.Add($"- Project Manager: Focus on project coordination, task management, stakeholder communication, progress tracking, risk management, resource planning, documentation, team coordination");
                        break;
                    case "marketing":
                        instructions.Add($"- Marketing: Focus on user acquisition strategies, content creation, social media integration, analytics, user engagement features, promotional materials, market research, and ALWAYS include a task for creating a video demo of the application");
                        break;
                    case "documentation specialist":
                        instructions.Add($"- Documentation Specialist: Focus on technical documentation, user guides, API documentation, code documentation, training materials, knowledge base, help systems");
                        break;
                    default:
                        instructions.Add($"- {role.RoleName}: Create appropriate tasks based on the role's typical responsibilities and the module requirements");
                        break;
                }
            }
            
            return string.Join("\n", instructions);
        }

        /// <summary>
        /// Counts the number of modules in the system design (copied from AIService)
        /// </summary>
        private int GetModuleCount(string? systemDesign)
        {
            if (string.IsNullOrEmpty(systemDesign))
                return 0;
                
            // Count occurrences of "### Module" pattern
            var moduleCount = System.Text.RegularExpressions.Regex.Matches(systemDesign, @"### Module \d+").Count;
            return moduleCount;
        }

        /// <summary>
        /// Helper method to filter out Data Model section (copied from AIService)
        /// </summary>
        private string FilterOutDataModelSection(string? systemDesign)
        {
            if (string.IsNullOrEmpty(systemDesign))
                return systemDesign ?? string.Empty;

            try
            {
                // Split by "## Data Model" section
                var dataModelIndex = systemDesign.IndexOf("## Data Model", StringComparison.OrdinalIgnoreCase);
                
                if (dataModelIndex >= 0)
                {
                    // Return only the content before the Data Model section
                    var filteredContent = systemDesign.Substring(0, dataModelIndex).Trim();
                    
                    // Also remove any trailing "---" separators
                    filteredContent = filteredContent.TrimEnd('-', ' ', '\n', '\r');
                    
                    _logger.LogInformation("Filtered out Data Model section. Original length: {OriginalLength}, Filtered length: {FilteredLength}", 
                        systemDesign.Length, filteredContent.Length);
                    
                    return filteredContent;
                }
                
                // If no Data Model section found, return original content
                return systemDesign;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering Data Model section: {Message}", ex.Message);
                return systemDesign; // Return original content if filtering fails
            }
        }

        #endregion

        #region Neon Test Methods

        /// <summary>
        /// Test Neon branch CREATION - simulates what happens during board creation
        /// POST /api/Test/Neon-create-branch
        /// Optional query param: parentBranchId (uses config default if not provided)
        /// </summary>
        [HttpPost("Neon-create-branch")]
        public async Task<ActionResult> TestNeonCreateBranch([FromQuery] string? parentBranchId = null)
        {
            try
            {
                var neonApiKey = _configuration["Neon:ApiKey"];
                var neonBaseUrl = _configuration["Neon:BaseUrl"];
                var neonProjectId = _configuration["Neon:ProjectId"];
                var neonParentBranchId = _configuration["Neon:BranchId"]; // Default parent branch from config

                if (string.IsNullOrWhiteSpace(neonApiKey) || neonApiKey == "your-neon-api-key-here" ||
                    string.IsNullOrWhiteSpace(neonBaseUrl) ||
                    string.IsNullOrWhiteSpace(neonProjectId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Neon configuration is missing or invalid",
                        configured = new
                        {
                            hasApiKey = !string.IsNullOrWhiteSpace(neonApiKey) && neonApiKey != "your-neon-api-key-here",
                            hasBaseUrl = !string.IsNullOrWhiteSpace(neonBaseUrl),
                            hasProjectId = !string.IsNullOrWhiteSpace(neonProjectId)
                        }
                    });
                }

                // Use provided parentBranchId or fall back to config
                var actualParentBranchId = parentBranchId ?? neonParentBranchId;

                var branchApiUrl = $"{neonBaseUrl}/projects/{neonProjectId}/branches";
                
                _logger.LogInformation("🔍 [NEON TEST] Creating branch via POST: {Url}", branchApiUrl);

                // Create branch request body (same as in CreateNeonBranchAsync)
                var createBranchRequest = new
                {
                    endpoints = new[]
                    {
                        new { type = "read_write" }
                    },
                    branch = actualParentBranchId != null ? new { parent_id = actualParentBranchId } : null
                };

                var requestBody = JsonSerializer.Serialize(createBranchRequest);
                var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", neonApiKey);
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var startTime = DateTime.UtcNow;
                var response = await httpClient.PostAsync(branchApiUrl, content);
                var elapsed = DateTime.UtcNow - startTime;

                var responseContent = await response.Content.ReadAsStringAsync();

                // Try to parse as JSON to see structure
                JsonDocument? jsonDoc = null;
                string? branchId = null;
                string? endpointHost = null;
                string? endpointId = null;
                
                try
                {
                    jsonDoc = JsonDocument.Parse(responseContent);
                    
                    // Extract branch ID (same logic as CreateNeonBranchAsync)
                    if (jsonDoc.RootElement.TryGetProperty("branch", out var branchObj))
                    {
                        if (branchObj.TryGetProperty("id", out var branchIdProp))
                        {
                            branchId = branchIdProp.GetString();
                        }
                    }
                    
                    // Extract endpoint information from ROOT level endpoints array
                    if (jsonDoc.RootElement.TryGetProperty("endpoints", out var endpointsProp) && endpointsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var endpoint in endpointsProp.EnumerateArray())
                        {
                            if (endpoint.TryGetProperty("branch_id", out var endpointBranchIdProp))
                            {
                                var endpointBranchId = endpointBranchIdProp.GetString();
                                if (endpointBranchId == branchId)
                                {
                                    if (endpoint.TryGetProperty("id", out var endpointIdProp))
                                    {
                                        endpointId = endpointIdProp.GetString();
                                    }
                                    if (endpoint.TryGetProperty("host", out var hostProp))
                                    {
                                        endpointHost = hostProp.GetString();
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Not JSON or parse failed
                }

                return Ok(new
                {
                    success = response.IsSuccessStatusCode,
                    request = new
                    {
                        method = "POST",
                        url = branchApiUrl,
                        parentBranchId = actualParentBranchId,
                        requestBody = createBranchRequest,
                        headers = new
                        {
                            authorization = "Bearer ***",
                            accept = "application/json"
                        }
                    },
                    response = new
                    {
                        statusCode = (int)response.StatusCode,
                        statusReason = response.ReasonPhrase,
                        isSuccess = response.IsSuccessStatusCode,
                        elapsedMs = elapsed.TotalMilliseconds,
                        headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                        rawContent = responseContent,
                        parsedJson = jsonDoc != null ? JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true }) : null,
                        extracted = new
                        {
                            branchId = branchId,
                            endpointId = endpointId,
                            endpointHost = endpointHost
                        },
                        analysis = jsonDoc != null ? AnalyzeBranchResponse(jsonDoc.RootElement) : null,
                        nextSteps = branchId != null ? new
                        {
                            message = "Branch created successfully! Use this branchId in other test endpoints:",
                            testBranchQuery = $"/api/Test/Neon-branch?branchId={Uri.EscapeDataString(branchId)}",
                            testConnectionString = $"/api/Test/Neon-connection-string?branchId={Uri.EscapeDataString(branchId)}&databaseName=AppDB_test&roleName=neondb_owner"
                        } : null
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Neon branch creation");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error calling Neon branch creation API",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Test Neon branch API - returns raw response from Neon API
        /// GET /api/Test/Neon-branch?branchId=br-empty-salad-a9jybc9u
        /// </summary>
        [HttpGet("Neon-branch")]
        public async Task<ActionResult> TestNeonBranch([FromQuery] string branchId)
        {
            try
            {
                if (string.IsNullOrEmpty(branchId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "branchId query parameter is required",
                        example = "/api/Test/Neon-branch?branchId=br-empty-salad-a9jybc9u"
                    });
                }

                var neonApiKey = _configuration["Neon:ApiKey"];
                var neonBaseUrl = _configuration["Neon:BaseUrl"];
                var neonProjectId = _configuration["Neon:ProjectId"];

                if (string.IsNullOrWhiteSpace(neonApiKey) || neonApiKey == "your-neon-api-key-here" ||
                    string.IsNullOrWhiteSpace(neonBaseUrl) ||
                    string.IsNullOrWhiteSpace(neonProjectId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Neon configuration is missing or invalid",
                        configured = new
                        {
                            hasApiKey = !string.IsNullOrWhiteSpace(neonApiKey) && neonApiKey != "your-neon-api-key-here",
                            hasBaseUrl = !string.IsNullOrWhiteSpace(neonBaseUrl),
                            hasProjectId = !string.IsNullOrWhiteSpace(neonProjectId)
                        }
                    });
                }

                var branchApiUrl = $"{neonBaseUrl}/projects/{neonProjectId}/branches/{Uri.EscapeDataString(branchId)}";
                
                _logger.LogInformation("🔍 [NEON TEST] Querying branch API: {Url}", branchApiUrl);

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", neonApiKey);
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var startTime = DateTime.UtcNow;
                var response = await httpClient.GetAsync(branchApiUrl);
                var elapsed = DateTime.UtcNow - startTime;

                var responseContent = await response.Content.ReadAsStringAsync();

                // Try to parse as JSON to see structure
                JsonDocument? jsonDoc = null;
                try
                {
                    jsonDoc = JsonDocument.Parse(responseContent);
                }
                catch
                {
                    // Not JSON, that's okay
                }

                return Ok(new
                {
                    success = response.IsSuccessStatusCode,
                    request = new
                    {
                        method = "GET",
                        url = branchApiUrl,
                        branchId = branchId,
                        headers = new
                        {
                            authorization = "Bearer ***",
                            accept = "application/json"
                        }
                    },
                    response = new
                    {
                        statusCode = (int)response.StatusCode,
                        statusReason = response.ReasonPhrase,
                        isSuccess = response.IsSuccessStatusCode,
                        elapsedMs = elapsed.TotalMilliseconds,
                        headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                        rawContent = responseContent,
                        parsedJson = jsonDoc != null ? JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true }) : null,
                        analysis = jsonDoc != null ? AnalyzeBranchResponse(jsonDoc.RootElement) : null
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Neon branch API for branch {BranchId}", branchId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error calling Neon branch API",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Test Neon connection string API - returns raw response from Neon API
        /// GET /api/Test/Neon-connection-string?branchId=br-empty-salad-a9jybc9u&databaseName=AppDB_test&roleName=neondb_owner
        /// </summary>
        [HttpGet("Neon-connection-string")]
        public async Task<ActionResult> TestNeonConnectionString(
            [FromQuery] string branchId,
            [FromQuery] string? databaseName = null,
            [FromQuery] string? roleName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(branchId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "branchId query parameter is required",
                        example = "/api/Test/Neon-connection-string?branchId=br-empty-salad-a9jybc9u&databaseName=AppDB_test&roleName=neondb_owner"
                    });
                }

                var neonApiKey = _configuration["Neon:ApiKey"];
                var neonBaseUrl = _configuration["Neon:BaseUrl"];
                var neonProjectId = _configuration["Neon:ProjectId"];
                var neonDefaultOwnerName = _configuration["Neon:DefaultOwnerName"] ?? "neondb_owner";

                if (string.IsNullOrWhiteSpace(neonApiKey) || neonApiKey == "your-neon-api-key-here" ||
                    string.IsNullOrWhiteSpace(neonBaseUrl) ||
                    string.IsNullOrWhiteSpace(neonProjectId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Neon configuration is missing or invalid",
                        configured = new
                        {
                            hasApiKey = !string.IsNullOrWhiteSpace(neonApiKey) && neonApiKey != "your-neon-api-key-here",
                            hasBaseUrl = !string.IsNullOrWhiteSpace(neonBaseUrl),
                            hasProjectId = !string.IsNullOrWhiteSpace(neonProjectId)
                        }
                    });
                }

                var dbName = databaseName ?? "AppDB_test";
                var role = roleName ?? neonDefaultOwnerName;

                var connectionUrl = $"{neonBaseUrl}/projects/{neonProjectId}/connection_uri?database_name={Uri.EscapeDataString(dbName)}&role_name={Uri.EscapeDataString(role)}&branch_id={Uri.EscapeDataString(branchId)}&pooled=false";
                
                _logger.LogInformation("🔍 [NEON TEST] Querying connection_uri API: {Url}", connectionUrl);

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", neonApiKey);
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var startTime = DateTime.UtcNow;
                var response = await httpClient.GetAsync(connectionUrl);
                var elapsed = DateTime.UtcNow - startTime;

                var responseContent = await response.Content.ReadAsStringAsync();

                // Try to parse as JSON to see structure
                JsonDocument? jsonDoc = null;
                string? connectionString = null;
                string? extractedEndpointId = null;
                try
                {
                    jsonDoc = JsonDocument.Parse(responseContent);
                    if (jsonDoc.RootElement.TryGetProperty("uri", out var uriProp))
                    {
                        connectionString = uriProp.GetString();
                        if (!string.IsNullOrEmpty(connectionString))
                        {
                            try
                            {
                                var uri = new Uri(connectionString);
                                // Extract endpoint ID from host (format: ep-xxx-yyy-ID.gwc.azure.neon.tech)
                                var endpointIdMatch = System.Text.RegularExpressions.Regex.Match(uri.Host, @"ep-[^-]+-[^-]+-([^.]+)\.(gwc\.azure|us-east-2\.aws)\.neon\.tech");
                                extractedEndpointId = endpointIdMatch.Success ? endpointIdMatch.Groups[1].Value : null;
                            }
                            catch
                            {
                                // Failed to parse URI
                            }
                        }
                    }
                }
                catch
                {
                    // Not JSON, that's okay
                }

                // Extract expected endpoint ID from branch ID
                var branchIdParts = branchId.Split('-');
                var expectedEndpointId = branchIdParts.Length >= 3 ? branchIdParts[branchIdParts.Length - 1] : null;

                return Ok(new
                {
                    success = response.IsSuccessStatusCode,
                    request = new
                    {
                        method = "GET",
                        url = connectionUrl,
                        parameters = new
                        {
                            branchId = branchId,
                            databaseName = dbName,
                            roleName = role,
                            pooled = false
                        },
                        headers = new
                        {
                            authorization = "Bearer ***",
                            accept = "application/json"
                        }
                    },
                    response = new
                    {
                        statusCode = (int)response.StatusCode,
                        statusReason = response.ReasonPhrase,
                        isSuccess = response.IsSuccessStatusCode,
                        elapsedMs = elapsed.TotalMilliseconds,
                        headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                        rawContent = responseContent,
                        parsedJson = jsonDoc != null ? JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true }) : null,
                        connectionString = connectionString,
                        analysis = new
                        {
                            hasConnectionString = !string.IsNullOrEmpty(connectionString),
                            extractedEndpointId = extractedEndpointId,
                            expectedEndpointId = expectedEndpointId,
                            endpointIdMatches = !string.IsNullOrEmpty(extractedEndpointId) && 
                                                !string.IsNullOrEmpty(expectedEndpointId) &&
                                                extractedEndpointId.Equals(expectedEndpointId, StringComparison.OrdinalIgnoreCase)
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Neon connection string API for branch {BranchId}", branchId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error calling Neon connection_uri API",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Test Neon project endpoints API - list all endpoints and filter by branch
        /// GET /api/Test/Neon-project-endpoints?branchId=br-divine-pond-a9lr3yfm
        /// </summary>
        [HttpGet("Neon-project-endpoints")]
        public async Task<ActionResult> TestNeonProjectEndpoints([FromQuery] string? branchId = null)
        {
            try
            {
                var neonApiKey = _configuration["Neon:ApiKey"];
                var neonBaseUrl = _configuration["Neon:BaseUrl"];
                var neonProjectId = _configuration["Neon:ProjectId"];

                if (string.IsNullOrWhiteSpace(neonApiKey) || neonApiKey == "your-neon-api-key-here" ||
                    string.IsNullOrWhiteSpace(neonBaseUrl) ||
                    string.IsNullOrWhiteSpace(neonProjectId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Neon configuration is missing or invalid",
                        configured = new
                        {
                            hasApiKey = !string.IsNullOrWhiteSpace(neonApiKey) && neonApiKey != "your-neon-api-key-here",
                            hasBaseUrl = !string.IsNullOrWhiteSpace(neonBaseUrl),
                            hasProjectId = !string.IsNullOrWhiteSpace(neonProjectId)
                        }
                    });
                }

                var endpointsApiUrl = $"{neonBaseUrl}/projects/{neonProjectId}/endpoints";
                
                _logger.LogInformation("🔍 [NEON TEST] Querying project endpoints API: {Url}", endpointsApiUrl);

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", neonApiKey);
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var startTime = DateTime.UtcNow;
                var response = await httpClient.GetAsync(endpointsApiUrl);
                var elapsed = DateTime.UtcNow - startTime;

                var responseContent = await response.Content.ReadAsStringAsync();

                // Try to parse as JSON
                JsonDocument? jsonDoc = null;
                var allEndpoints = new List<object>();
                var filteredEndpoints = new List<object>();
                
                try
                {
                    jsonDoc = JsonDocument.Parse(responseContent);
                    
                    // Check if endpoints is an array at root level
                    if (jsonDoc.RootElement.TryGetProperty("endpoints", out var endpointsProp) && endpointsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var endpoint in endpointsProp.EnumerateArray())
                        {
                            var endpointObj = new Dictionary<string, object?>();
                            
                            if (endpoint.TryGetProperty("id", out var idProp))
                                endpointObj["id"] = idProp.GetString();
                            if (endpoint.TryGetProperty("host", out var hostProp))
                                endpointObj["host"] = hostProp.GetString();
                            if (endpoint.TryGetProperty("branch_id", out var branchIdProp))
                                endpointObj["branch_id"] = branchIdProp.GetString();
                            if (endpoint.TryGetProperty("current_state", out var stateProp))
                                endpointObj["current_state"] = stateProp.GetString();
                            if (endpoint.TryGetProperty("pending_state", out var pendingStateProp))
                                endpointObj["pending_state"] = pendingStateProp.GetString();
                            if (endpoint.TryGetProperty("port", out var portProp))
                                endpointObj["port"] = portProp.GetInt32();
                            
                            allEndpoints.Add(endpointObj);
                            
                            // Filter by branch_id if provided
                            if (!string.IsNullOrEmpty(branchId))
                            {
                                var epBranchId = endpointObj.ContainsKey("branch_id") ? endpointObj["branch_id"]?.ToString() : null;
                                if (epBranchId == branchId)
                                {
                                    filteredEndpoints.Add(endpointObj);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Parse failed
                }

                return Ok(new
                {
                    success = response.IsSuccessStatusCode,
                    request = new
                    {
                        method = "GET",
                        url = endpointsApiUrl,
                        filterBranchId = branchId,
                        headers = new
                        {
                            authorization = "Bearer ***",
                            accept = "application/json"
                        }
                    },
                    response = new
                    {
                        statusCode = (int)response.StatusCode,
                        statusReason = response.ReasonPhrase,
                        isSuccess = response.IsSuccessStatusCode,
                        elapsedMs = elapsed.TotalMilliseconds,
                        headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                        rawContent = responseContent,
                        parsedJson = jsonDoc != null ? JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true }) : null,
                        summary = new
                        {
                            totalEndpoints = allEndpoints.Count,
                            filteredEndpoints = filteredEndpoints.Count,
                            allEndpoints = allEndpoints,
                            filteredByBranch = !string.IsNullOrEmpty(branchId) ? filteredEndpoints : null
                        },
                        analysis = !string.IsNullOrEmpty(branchId) ? (object)new
                        {
                            branchId = branchId,
                            foundEndpoints = filteredEndpoints.Count > 0,
                            endpointDetails = filteredEndpoints.Count > 0 ? (object)filteredEndpoints : (object)new[] { new { message = "No endpoints found for this branch" } }
                        } : (object)new { message = "No branchId filter provided - showing all endpoints" }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Neon project endpoints API");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error calling Neon project endpoints API",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Test Neon connection string API with detailed endpoint extraction
        /// GET /api/Test/Neon-connection-string-detailed?branchId=br-divine-pond-a9lr3yfm&databaseName=AppDB_test&roleName=neondb_owner
        /// This version extracts and analyzes the endpoint from the connection string
        /// </summary>
        [HttpGet("Neon-connection-string-detailed")]
        public async Task<ActionResult> TestNeonConnectionStringDetailed(
            [FromQuery] string branchId,
            [FromQuery] string? databaseName = null,
            [FromQuery] string? roleName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(branchId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "branchId query parameter is required",
                        example = "/api/Test/Neon-connection-string-detailed?branchId=br-divine-pond-a9lr3yfm&databaseName=AppDB_test&roleName=neondb_owner"
                    });
                }

                var neonApiKey = _configuration["Neon:ApiKey"];
                var neonBaseUrl = _configuration["Neon:BaseUrl"];
                var neonProjectId = _configuration["Neon:ProjectId"];
                var neonDefaultOwnerName = _configuration["Neon:DefaultOwnerName"] ?? "neondb_owner";

                if (string.IsNullOrWhiteSpace(neonApiKey) || neonApiKey == "your-neon-api-key-here" ||
                    string.IsNullOrWhiteSpace(neonBaseUrl) ||
                    string.IsNullOrWhiteSpace(neonProjectId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Neon configuration is missing or invalid"
                    });
                }

                var dbName = databaseName ?? "AppDB_test";
                var role = roleName ?? neonDefaultOwnerName;

                var connectionUrl = $"{neonBaseUrl}/projects/{neonProjectId}/connection_uri?database_name={Uri.EscapeDataString(dbName)}&role_name={Uri.EscapeDataString(role)}&branch_id={Uri.EscapeDataString(branchId)}&pooled=false";
                
                _logger.LogInformation("🔍 [NEON TEST] Querying connection_uri API (detailed): {Url}", connectionUrl);

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", neonApiKey);
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var startTime = DateTime.UtcNow;
                var response = await httpClient.GetAsync(connectionUrl);
                var elapsed = DateTime.UtcNow - startTime;

                var responseContent = await response.Content.ReadAsStringAsync();

                // Detailed parsing
                JsonDocument? jsonDoc = null;
                string? connectionString = null;
                string? extractedEndpointId = null;
                string? extractedHost = null;
                int? extractedPort = null;
                string? extractedUsername = null;
                string? extractedPassword = null;
                string? extractedDatabase = null;
                
                try
                {
                    jsonDoc = JsonDocument.Parse(responseContent);
                    if (jsonDoc.RootElement.TryGetProperty("uri", out var uriProp))
                    {
                        connectionString = uriProp.GetString();
                        if (!string.IsNullOrEmpty(connectionString))
                        {
                            try
                            {
                                var uri = new Uri(connectionString);
                                extractedHost = uri.Host;
                                extractedPort = uri.Port == -1 ? 5432 : uri.Port;
                                extractedDatabase = uri.AbsolutePath.TrimStart('/');
                                
                                // Extract user info
                                var userInfo = uri.UserInfo.Split(':');
                                extractedUsername = userInfo.Length > 0 ? userInfo[0] : null;
                                extractedPassword = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null;
                                
                                // Extract endpoint ID from host (format: ep-xxx-yyy-ID.gwc.azure.neon.tech)
                                var endpointIdMatch = System.Text.RegularExpressions.Regex.Match(uri.Host, @"ep-[^-]+-[^-]+-([^.]+)\.(gwc\.azure|us-east-2\.aws)\.neon\.tech");
                                extractedEndpointId = endpointIdMatch.Success ? endpointIdMatch.Groups[1].Value : null;
                            }
                            catch (Exception ex)
                            {
                                // Failed to parse URI
                            }
                        }
                    }
                }
                catch
                {
                    // Not JSON or parse failed
                }

                // Extract expected endpoint ID from branch ID
                var branchIdParts = branchId.Split('-');
                var expectedEndpointId = branchIdParts.Length >= 3 ? branchIdParts[branchIdParts.Length - 1] : null;

                return Ok(new
                {
                    success = response.IsSuccessStatusCode,
                    request = new
                    {
                        method = "GET",
                        url = connectionUrl,
                        parameters = new
                        {
                            branchId = branchId,
                            databaseName = dbName,
                            roleName = role,
                            pooled = false
                        }
                    },
                    response = new
                    {
                        statusCode = (int)response.StatusCode,
                        statusReason = response.ReasonPhrase,
                        isSuccess = response.IsSuccessStatusCode,
                        elapsedMs = elapsed.TotalMilliseconds,
                        rawContent = responseContent,
                        parsedJson = jsonDoc != null ? JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true }) : null
                    },
                    connectionStringAnalysis = new
                    {
                        fullConnectionString = connectionString,
                        extracted = new
                        {
                            host = extractedHost,
                            port = extractedPort,
                            database = extractedDatabase,
                            username = extractedUsername,
                            password = extractedPassword != null ? "***" + extractedPassword.Substring(Math.Max(0, extractedPassword.Length - 4)) : null,
                            endpointId = extractedEndpointId
                        },
                        validation = new
                        {
                            expectedEndpointId = expectedEndpointId,
                            actualEndpointId = extractedEndpointId,
                            endpointIdMatches = !string.IsNullOrEmpty(extractedEndpointId) && 
                                                !string.IsNullOrEmpty(expectedEndpointId) &&
                                                extractedEndpointId.Equals(expectedEndpointId, StringComparison.OrdinalIgnoreCase),
                            hasConnectionString = !string.IsNullOrEmpty(connectionString),
                            hasHost = !string.IsNullOrEmpty(extractedHost),
                            hasPassword = !string.IsNullOrEmpty(extractedPassword)
                        },
                        recommendation = !string.IsNullOrEmpty(extractedHost) && !string.IsNullOrEmpty(extractedPassword) 
                            ? "✅ Connection string can be used - extract host and password from connection_uri API"
                            : "❌ Connection string incomplete or invalid"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Neon connection string API (detailed)");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error calling Neon connection_uri API",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Test Neon branch creation V2 - Proper workflow using operations API and endpoint from branch creation
        /// POST /api/Test/Neon-branch-creation-v2
        /// This implements the correct Neon workflow:
        /// 1. Create branch (POST) - get endpoint info from response
        /// 2. Poll operations API until branch is ready
        /// 3. Create database in branch
        /// 4. Construct connection string from endpoint info + database + role/password
        /// </summary>
        [HttpPost("Neon-branch-creation-v2")]
        public async Task<ActionResult> TestNeonBranchCreationVersion2([FromQuery] string? parentBranchId = null)
        {
            try
            {
                var neonApiKey = _configuration["Neon:ApiKey"];
                var neonBaseUrl = _configuration["Neon:BaseUrl"];
                var neonProjectId = _configuration["Neon:ProjectId"];
                var neonParentBranchId = _configuration["Neon:BranchId"];
                var neonDefaultOwnerName = _configuration["Neon:DefaultOwnerName"] ?? "neondb_owner";

                if (string.IsNullOrWhiteSpace(neonApiKey) || neonApiKey == "your-neon-api-key-here" ||
                    string.IsNullOrWhiteSpace(neonBaseUrl) ||
                    string.IsNullOrWhiteSpace(neonProjectId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Neon configuration is missing or invalid"
                    });
                }

                var actualParentBranchId = parentBranchId ?? neonParentBranchId;
                var testDbName = $"AppDB_test_{DateTime.UtcNow:yyyyMMddHHmmss}";
                var steps = new List<object>();
                var operationIds = new List<string>(); // Declare at method scope

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", neonApiKey);
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                // ============================================================================
                // STEP 1: Create Branch
                // ============================================================================
                var step1Start = DateTime.UtcNow;
                var createBranchRequest = new
                {
                    endpoints = new[]
                    {
                        new { type = "read_write" }
                    },
                    branch = actualParentBranchId != null ? new { parent_id = actualParentBranchId } : null
                };

                var branchApiUrl = $"{neonBaseUrl}/projects/{neonProjectId}/branches";
                var branchRequestBody = JsonSerializer.Serialize(createBranchRequest);
                var branchContent = new StringContent(branchRequestBody, System.Text.Encoding.UTF8, "application/json");

                _logger.LogInformation("🔍 [NEON V2] Step 1: Creating branch via POST: {Url}", branchApiUrl);
                var branchResponse = await httpClient.PostAsync(branchApiUrl, branchContent);
                var branchResponseContent = await branchResponse.Content.ReadAsStringAsync();
                var step1Elapsed = DateTime.UtcNow - step1Start;

                string? createdBranchId = null;
                string? endpointHost = null;
                string? endpointId = null;
                string? endpointPort = null;
                string? operationId = null;

                if (branchResponse.IsSuccessStatusCode)
                {
                    var branchDoc = JsonDocument.Parse(branchResponseContent);
                    
                    // Extract branch ID
                    if (branchDoc.RootElement.TryGetProperty("branch", out var branchObj))
                    {
                        if (branchObj.TryGetProperty("id", out var branchIdProp))
                        {
                            createdBranchId = branchIdProp.GetString();
                        }
                    }

                    // Extract endpoint info from ROOT level endpoints array (from branch creation response)
                    if (branchDoc.RootElement.TryGetProperty("endpoints", out var endpointsProp) && endpointsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var endpoint in endpointsProp.EnumerateArray())
                        {
                            if (endpoint.TryGetProperty("branch_id", out var epBranchIdProp))
                            {
                                var epBranchId = epBranchIdProp.GetString();
                                if (epBranchId == createdBranchId)
                                {
                                    if (endpoint.TryGetProperty("id", out var epIdProp))
                                        endpointId = epIdProp.GetString();
                                    if (endpoint.TryGetProperty("host", out var hostProp))
                                        endpointHost = hostProp.GetString();
                                    if (endpoint.TryGetProperty("port", out var portProp) && portProp.ValueKind == JsonValueKind.Number)
                                        endpointPort = portProp.GetInt32().ToString();
                                    break;
                                }
                            }
                        }
                    }

                    // Extract ALL operation IDs from branch creation response
                    operationIds.Clear(); // Clear and populate
                    if (branchDoc.RootElement.TryGetProperty("operations", out var operationsProp) && operationsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var op in operationsProp.EnumerateArray())
                        {
                            if (op.TryGetProperty("id", out var opIdProp))
                            {
                                var opId = opIdProp.GetString();
                                if (!string.IsNullOrEmpty(opId))
                                {
                                    operationIds.Add(opId);
                                }
                            }
                        }
                    }
                    if (operationIds.Count > 0)
                    {
                        operationId = operationIds[0]; // Keep first one for backward compatibility
                    }
                    
                    // Log extracted information from Step 1
                    _logger.LogInformation("✅ [NEON V2] Step 1: Branch created successfully");
                    _logger.LogInformation("🔍 [NEON V2] Step 1: Branch ID: {BranchId}", createdBranchId);
                    _logger.LogInformation("🔍 [NEON V2] Step 1: Initial endpoint host: {EndpointHost}", endpointHost);
                    _logger.LogInformation("🔍 [NEON V2] Step 1: Initial endpoint ID: {EndpointId}", endpointId);
                    _logger.LogInformation("🔍 [NEON V2] Step 1: Endpoint port: {EndpointPort}", endpointPort ?? "5432 (default)");
                    _logger.LogInformation("🔍 [NEON V2] Step 1: Found {Count} operation IDs: {OperationIds}", 
                        operationIds.Count, string.Join(", ", operationIds));
                }

                steps.Add(new
                {
                    step = 1,
                    name = "Create Branch",
                    success = branchResponse.IsSuccessStatusCode,
                    elapsedMs = step1Elapsed.TotalMilliseconds,
                    branchId = createdBranchId,
                    endpointHost = endpointHost,
                    endpointId = endpointId,
                    endpointPort = endpointPort,
                    operationIds = operationIds,
                    operationId = operationId, // First operation for backward compatibility
                    rawResponse = branchResponseContent
                });

                if (!branchResponse.IsSuccessStatusCode || string.IsNullOrEmpty(createdBranchId))
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Failed to create branch",
                        steps = steps
                    });
                }

                // ============================================================================
                // STEP 2: Poll Operations API until SPECIFIC operations from Step 1 are ready
                // ============================================================================
                var step2Start = DateTime.UtcNow;
                var operationsApiUrl = $"{neonBaseUrl}/projects/{neonProjectId}/operations";
                var maxOperationPolls = 60; // 60 retries × 2 seconds = 2 minutes max
                var operationPollDelay = 2000; // 2 seconds
                var operationPollCount = 0;
                var branchReady = false;
                var operationStatuses = new List<string>();
                var operationDetails = new List<object>();

                _logger.LogInformation("🔍 [NEON V2] Step 2: Polling operations API for {Count} operations until branch '{BranchId}' is ready", 
                    operationIds.Count, createdBranchId);

                // Track status of each operation from Step 1
                var trackedOperations = new Dictionary<string, string>(); // operationId -> status

                while (operationPollCount < maxOperationPolls && !branchReady)
                {
                    try
                    {
                        var opsResponse = await httpClient.GetAsync(operationsApiUrl);
                        if (opsResponse.IsSuccessStatusCode)
                        {
                            var opsContent = await opsResponse.Content.ReadAsStringAsync();
                            var opsDoc = JsonDocument.Parse(opsContent);

                            // Look for the SPECIFIC operations we got from Step 1 (by ID)
                            var foundOperations = new List<object>();
                            trackedOperations.Clear();
                            
                            if (opsDoc.RootElement.TryGetProperty("operations", out var opsArray) && opsArray.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var op in opsArray.EnumerateArray())
                                {
                                    if (op.TryGetProperty("id", out var idProp))
                                    {
                                        var opId = idProp.GetString();
                                        // Only track operations we got from Step 1
                                        if (operationIds.Contains(opId))
                                        {
                                            var opAction = op.TryGetProperty("action", out var actionProp) ? actionProp.GetString() : "unknown";
                                            var opStatus = op.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "unknown";
                                            
                                            trackedOperations[opId] = opStatus;
                                            
                                            foundOperations.Add(new
                                            {
                                                id = opId,
                                                action = opAction,
                                                status = opStatus
                                            });

                                            operationStatuses.Add($"{opAction}:{opStatus}");
                                        }
                                    }
                                }
                            }

                            operationDetails = foundOperations;

                            // Check if ALL tracked operations from Step 1 are finished
                            if (operationIds.Count > 0)
                            {
                                var allTracked = operationIds.All(id => trackedOperations.ContainsKey(id));
                                var allFinished = allTracked && trackedOperations.Values.All(status => 
                                    status == "finished" || status == "completed");

                                if (allFinished && allTracked)
                                {
                                    branchReady = true;
                                    break;
                                }
                            }
                        }

                        if (!branchReady)
                        {
                            operationPollCount++;
                            if (operationPollCount < maxOperationPolls)
                            {
                                await Task.Delay(operationPollDelay);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ [NEON V2] Error polling operations (attempt {Attempt})", operationPollCount + 1);
                        operationPollCount++;
                        if (operationPollCount < maxOperationPolls)
                        {
                            await Task.Delay(operationPollDelay);
                        }
                    }
                }

                var step2Elapsed = DateTime.UtcNow - step2Start;
                steps.Add(new
                {
                    step = 2,
                    name = "Poll Operations API",
                    success = branchReady,
                    elapsedMs = step2Elapsed.TotalMilliseconds,
                    polls = operationPollCount,
                    trackedOperationIds = operationIds,
                    operationStatuses = operationStatuses,
                    operationDetails = operationDetails,
                    message = branchReady 
                        ? $"Branch is ready - all {operationIds.Count} operations finished" 
                        : $"Still waiting after {operationPollCount} polls. Found {operationDetails.Count}/{operationIds.Count} operations. Statuses: {string.Join(", ", operationStatuses)}"
                });

                // ============================================================================
                // STEP 2.5: Verify endpoint after operations finish (endpoint may have changed)
                // ============================================================================
                var step2_5Start = DateTime.UtcNow;
                string? verifiedEndpointHost = endpointHost;
                string? verifiedEndpointId = endpointId;
                
                if (branchReady && !string.IsNullOrEmpty(createdBranchId))
                {
                    _logger.LogInformation("🔍 [NEON V2] Step 2.5: Verifying endpoint after operations finished for branch '{BranchId}'", createdBranchId);
                    _logger.LogInformation("🔍 [NEON V2] Step 2.5: Original endpoint from Step 1: {EndpointHost}", endpointHost);
                    
                    try
                    {
                        // Query branch API to get the final/verified endpoint
                        var verifyBranchApiUrl = $"{neonBaseUrl}/projects/{neonProjectId}/branches/{Uri.EscapeDataString(createdBranchId)}";
                        var branchApiResponse = await httpClient.GetAsync(verifyBranchApiUrl);
                        
                        if (branchApiResponse.IsSuccessStatusCode)
                        {
                            var branchApiContent = await branchApiResponse.Content.ReadAsStringAsync();
                            _logger.LogInformation("🔍 [NEON V2] Step 2.5: Branch API response received (length: {Length})", branchApiContent.Length);
                            
                            var branchApiDoc = JsonDocument.Parse(branchApiContent);
                            
                            // Log the root element properties for debugging
                            var rootProps = new List<string>();
                            foreach (var prop in branchApiDoc.RootElement.EnumerateObject())
                            {
                                rootProps.Add($"{prop.Name} ({prop.Value.ValueKind})");
                            }
                            _logger.LogInformation("🔍 [NEON V2] Step 2.5: Root element properties: {Properties}", string.Join(", ", rootProps));
                            
                            // Try to get endpoint from branch API response
                            var endpointsArray = default(JsonElement);
                            var endpointsFound = false;
                            
                            if (branchApiDoc.RootElement.TryGetProperty("endpoints", out var rootEndpointsProp) && 
                                rootEndpointsProp.ValueKind == JsonValueKind.Array)
                            {
                                endpointsArray = rootEndpointsProp;
                                endpointsFound = true;
                                _logger.LogInformation("🔍 [NEON V2] Step 2.5: Found 'endpoints' array at root level with {Count} items", rootEndpointsProp.GetArrayLength());
                            }
                            else if (branchApiDoc.RootElement.TryGetProperty("branch", out var branchObj) &&
                                     branchObj.TryGetProperty("endpoints", out var branchEndpointsProp) && 
                                     branchEndpointsProp.ValueKind == JsonValueKind.Array)
                            {
                                endpointsArray = branchEndpointsProp;
                                endpointsFound = true;
                                _logger.LogInformation("🔍 [NEON V2] Step 2.5: Found 'endpoints' array under 'branch' with {Count} items", branchEndpointsProp.GetArrayLength());
                            }
                            else
                            {
                                // Check what's inside the branch object
                                if (branchApiDoc.RootElement.TryGetProperty("branch", out var branchObjForInspection))
                                {
                                    var branchProps = new List<string>();
                                    foreach (var prop in branchObjForInspection.EnumerateObject())
                                    {
                                        branchProps.Add($"{prop.Name} ({prop.Value.ValueKind})");
                                    }
                                    _logger.LogInformation("🔍 [NEON V2] Step 2.5: Branch object properties: {Properties}", string.Join(", ", branchProps));
                                }
                                
                                _logger.LogWarning("⚠️ [NEON V2] Step 2.5: No 'endpoints' array found in branch API response. Will try project endpoints API instead.");
                                
                                // Fallback: Query project endpoints API and filter by branch_id
                                try
                                {
                                    var projectEndpointsUrl = $"{neonBaseUrl}/projects/{neonProjectId}/endpoints";
                                    _logger.LogInformation("🔍 [NEON V2] Step 2.5: Querying project endpoints API: {Url}", projectEndpointsUrl);
                                    var endpointsResponse = await httpClient.GetAsync(projectEndpointsUrl);
                                    
                                    if (endpointsResponse.IsSuccessStatusCode)
                                    {
                                        var endpointsContent = await endpointsResponse.Content.ReadAsStringAsync();
                                        var endpointsDoc = JsonDocument.Parse(endpointsContent);
                                        
                                        if (endpointsDoc.RootElement.TryGetProperty("endpoints", out var projectEndpointsProp) && 
                                            projectEndpointsProp.ValueKind == JsonValueKind.Array)
                                        {
                                            endpointsArray = projectEndpointsProp;
                                            endpointsFound = true;
                                            _logger.LogInformation("🔍 [NEON V2] Step 2.5: Found {Count} endpoints in project endpoints API", projectEndpointsProp.GetArrayLength());
                                        }
                                        else
                                        {
                                            _logger.LogWarning("⚠️ [NEON V2] Step 2.5: Project endpoints API response does not contain 'endpoints' array");
                                        }
                                    }
                                    else
                                    {
                                        var errorContent = await endpointsResponse.Content.ReadAsStringAsync();
                                        _logger.LogWarning("⚠️ [NEON V2] Step 2.5: Project endpoints API returned {StatusCode}: {Error}", 
                                            endpointsResponse.StatusCode, errorContent);
                                    }
                                }
                                catch (Exception endpointsEx)
                                {
                                    _logger.LogWarning(endpointsEx, "⚠️ [NEON V2] Step 2.5: Exception querying project endpoints API");
                                }
                            }
                            
                            if (endpointsFound && endpointsArray.ValueKind == JsonValueKind.Array)
                            {
                                var endpointCount = 0;
                                var matchingEndpointCount = 0;
                                
                                foreach (var ep in endpointsArray.EnumerateArray())
                                {
                                    endpointCount++;
                                    
                                    // Log endpoint structure for debugging
                                    var epProps = new List<string>();
                                    foreach (var epProp in ep.EnumerateObject())
                                    {
                                        epProps.Add($"{epProp.Name}={epProp.Value}");
                                    }
                                    _logger.LogInformation("🔍 [NEON V2] Step 2.5: Endpoint {Index} properties: {Properties}", endpointCount, string.Join(", ", epProps));
                                    
                                    if (ep.TryGetProperty("branch_id", out var epBranchIdProp))
                                    {
                                        var epBranchId = epBranchIdProp.GetString();
                                        _logger.LogInformation("🔍 [NEON V2] Step 2.5: Endpoint {Index} branch_id: {BranchId} (looking for: {TargetBranchId})", 
                                            endpointCount, epBranchId, createdBranchId);
                                        
                                        if (epBranchId == createdBranchId)
                                        {
                                            matchingEndpointCount++;
                                            _logger.LogInformation("✅ [NEON V2] Step 2.5: Endpoint {Index} matches target branch ID!", endpointCount);
                                            
                                            if (ep.TryGetProperty("host", out var epHostProp))
                                            {
                                                var verifiedHost = epHostProp.GetString();
                                                if (!string.IsNullOrEmpty(verifiedHost))
                                                {
                                                    verifiedEndpointHost = verifiedHost;
                                                    _logger.LogInformation("✅ [NEON V2] Step 2.5: Verified endpoint host from branch API: {EndpointHost}", verifiedEndpointHost);
                                                    
                                                    if (ep.TryGetProperty("id", out var epIdProp))
                                                    {
                                                        verifiedEndpointId = epIdProp.GetString();
                                                        _logger.LogInformation("✅ [NEON V2] Step 2.5: Verified endpoint ID: {EndpointId}", verifiedEndpointId);
                                                    }
                                                    break;
                                                }
                                                else
                                                {
                                                    _logger.LogWarning("⚠️ [NEON V2] Step 2.5: Endpoint {Index} has empty/null host property", endpointCount);
                                                }
                                            }
                                            else
                                            {
                                                _logger.LogWarning("⚠️ [NEON V2] Step 2.5: Endpoint {Index} does not have 'host' property", endpointCount);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogWarning("⚠️ [NEON V2] Step 2.5: Endpoint {Index} does not have 'branch_id' property", endpointCount);
                                    }
                                }
                                
                                _logger.LogInformation("🔍 [NEON V2] Step 2.5: Processed {Total} endpoints, {Matching} matched branch ID", endpointCount, matchingEndpointCount);
                            }
                            
                            if (string.IsNullOrEmpty(verifiedEndpointHost))
                            {
                                _logger.LogWarning("⚠️ [NEON V2] Step 2.5: Could not find endpoint in branch API response, using original from Step 1: {OriginalEndpoint}", endpointHost);
                            }
                        }
                        else
                        {
                            var errorContent = await branchApiResponse.Content.ReadAsStringAsync();
                            _logger.LogWarning("⚠️ [NEON V2] Step 2.5: Failed to query branch API: {StatusCode} - {Error}. Using original endpoint from Step 1", 
                                branchApiResponse.StatusCode, errorContent);
                        }
                    }
                    catch (Exception verifyEx)
                    {
                        _logger.LogWarning(verifyEx, "⚠️ [NEON V2] Step 2.5: Exception verifying endpoint, using original from Step 1");
                    }
                }
                
                var step2_5Elapsed = DateTime.UtcNow - step2_5Start;
                var endpointChanged = !string.IsNullOrEmpty(endpointHost) && 
                                     !string.IsNullOrEmpty(verifiedEndpointHost) && 
                                     !endpointHost.Equals(verifiedEndpointHost, StringComparison.OrdinalIgnoreCase);
                
                steps.Add(new
                {
                    step = 2.5,
                    name = "Verify Endpoint After Operations",
                    success = !string.IsNullOrEmpty(verifiedEndpointHost),
                    elapsedMs = step2_5Elapsed.TotalMilliseconds,
                    originalEndpointFromStep1 = endpointHost,
                    verifiedEndpointFromBranchAPI = verifiedEndpointHost,
                    endpointChanged = endpointChanged,
                    message = endpointChanged 
                        ? $"⚠️ Endpoint changed! Original: {endpointHost}, Verified: {verifiedEndpointHost}"
                        : $"✅ Endpoint verified: {verifiedEndpointHost ?? endpointHost}"
                });
                
                // Use verified endpoint if available, otherwise fall back to original
                endpointHost = verifiedEndpointHost ?? endpointHost;
                endpointId = verifiedEndpointId ?? endpointId;
                
                // Continue even if operations polling didn't confirm ready (endpoint might still work)

                // ============================================================================
                // STEP 3: Create Database in Branch
                // ============================================================================
                var step3Start = DateTime.UtcNow;
                var createDbRequest = new
                {
                    database = new
                    {
                        name = testDbName,
                        owner_name = neonDefaultOwnerName
                    }
                };

                var dbApiUrl = $"{neonBaseUrl}/projects/{neonProjectId}/branches/{Uri.EscapeDataString(createdBranchId)}/databases";
                var dbRequestBody = JsonSerializer.Serialize(createDbRequest);
                var dbContent = new StringContent(dbRequestBody, System.Text.Encoding.UTF8, "application/json");

                _logger.LogInformation("🔍 [NEON V2] Step 3: Creating database '{DbName}' in branch '{BranchId}'", testDbName, createdBranchId);
                var dbResponse = await httpClient.PostAsync(dbApiUrl, dbContent);
                var dbResponseContent = await dbResponse.Content.ReadAsStringAsync();
                var step3Elapsed = DateTime.UtcNow - step3Start;

                steps.Add(new
                {
                    step = 3,
                    name = "Create Database",
                    success = dbResponse.IsSuccessStatusCode,
                    elapsedMs = step3Elapsed.TotalMilliseconds,
                    databaseName = testDbName,
                    rawResponse = dbResponseContent
                });

                if (!dbResponse.IsSuccessStatusCode)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Failed to create database",
                        steps = steps
                    });
                }

                // ============================================================================
                // STEP 4: Get Password and Construct Connection String
                // ============================================================================
                var step4Start = DateTime.UtcNow;
                string? connectionString = null;
                string? extractedPassword = null;
                string? extractedUsername = null;
                string? connectionUriEndpoint = null; // Endpoint from connection_uri API (for comparison only)

                _logger.LogInformation("🔍 [NEON V2] Step 4: Starting connection string construction");
                _logger.LogInformation("🔍 [NEON V2] Step 4: Using endpoint host: {EndpointHost} (from Step 2.5 verification)", endpointHost);
                _logger.LogInformation("🔍 [NEON V2] Step 4: Using endpoint ID: {EndpointId}", endpointId);
                _logger.LogInformation("🔍 [NEON V2] Step 4: Database name: {DbName}", testDbName);
                _logger.LogInformation("🔍 [NEON V2] Step 4: Branch ID: {BranchId}", createdBranchId);

                // Get password from connection_uri API (we only need the password, not the endpoint)
                var connectionUrl = $"{neonBaseUrl}/projects/{neonProjectId}/connection_uri?database_name={Uri.EscapeDataString(testDbName)}&role_name={Uri.EscapeDataString(neonDefaultOwnerName)}&branch_id={Uri.EscapeDataString(createdBranchId)}&pooled=false";
                
                try
                {
                    _logger.LogInformation("🔍 [NEON V2] Step 4: Calling connection_uri API to extract password");
                    var connResponse = await httpClient.GetAsync(connectionUrl);
                    if (connResponse.IsSuccessStatusCode)
                    {
                        var connContent = await connResponse.Content.ReadAsStringAsync();
                        var connDoc = JsonDocument.Parse(connContent);
                        if (connDoc.RootElement.TryGetProperty("uri", out var uriProp))
                        {
                            var tempConnString = uriProp.GetString();
                            if (!string.IsNullOrEmpty(tempConnString))
                            {
                                var uri = new Uri(tempConnString);
                                connectionUriEndpoint = uri.Host; // Extract endpoint from connection_uri for comparison
                                
                                _logger.LogInformation("🔍 [NEON V2] Step 4: connection_uri API returned endpoint: {ConnectionUriEndpoint}", connectionUriEndpoint);
                                
                                var userInfo = uri.UserInfo.Split(':');
                                extractedUsername = userInfo.Length > 0 ? userInfo[0] : neonDefaultOwnerName;
                                extractedPassword = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
                                
                                _logger.LogInformation("✅ [NEON V2] Step 4: Extracted username: {Username}, password length: {PasswordLength}", 
                                    extractedUsername, extractedPassword?.Length ?? 0);
                                
                                // Compare endpoints
                                if (!string.IsNullOrEmpty(connectionUriEndpoint) && !string.IsNullOrEmpty(endpointHost))
                                {
                                    var endpointsMatch = connectionUriEndpoint.Equals(endpointHost, StringComparison.OrdinalIgnoreCase);
                                    if (endpointsMatch)
                                    {
                                        _logger.LogInformation("✅ [NEON V2] Step 4: Endpoint from connection_uri matches verified endpoint: {Endpoint}", endpointHost);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("⚠️ [NEON V2] Step 4: Endpoint mismatch! connection_uri: {ConnectionUriEndpoint}, verified: {VerifiedEndpoint}. Using verified endpoint.", 
                                            connectionUriEndpoint, endpointHost);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        var errorContent = await connResponse.Content.ReadAsStringAsync();
                        _logger.LogWarning("⚠️ [NEON V2] Step 4: connection_uri API returned {StatusCode}: {Error}", 
                            connResponse.StatusCode, errorContent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ [NEON V2] Step 4: Failed to extract password from connection_uri API");
                }

                // Construct connection string using verified endpoint from Step 2.5 (or original from Step 1 if verification failed)
                if (!string.IsNullOrEmpty(endpointHost) && !string.IsNullOrEmpty(extractedPassword))
                {
                    var port = !string.IsNullOrEmpty(endpointPort) ? int.Parse(endpointPort) : 5432;
                    connectionString = $"postgresql://{extractedUsername}:{Uri.EscapeDataString(extractedPassword)}@{endpointHost}:{port}/{testDbName}?sslmode=require";
                    
                    _logger.LogInformation("✅ [NEON V2] Step 4: Connection string constructed successfully");
                    _logger.LogInformation("🔍 [NEON V2] Step 4: Connection string details - Host: {Host}, Port: {Port}, Database: {Db}, Username: {User}", 
                        endpointHost, port, testDbName, extractedUsername);
                    _logger.LogInformation("🔍 [NEON V2] Step 4: Full connection string (first 80 chars): {ConnString}", 
                        connectionString.Substring(0, Math.Min(80, connectionString.Length)) + "...");
                }
                else
                {
                    _logger.LogError("❌ [NEON V2] Step 4: Cannot construct connection string - missing endpointHost or password");
                    if (string.IsNullOrEmpty(endpointHost))
                    {
                        _logger.LogError("❌ [NEON V2] Step 4: endpointHost is null or empty");
                    }
                    if (string.IsNullOrEmpty(extractedPassword))
                    {
                        _logger.LogError("❌ [NEON V2] Step 4: extractedPassword is null or empty");
                    }
                }

                var step4Elapsed = DateTime.UtcNow - step4Start;
                steps.Add(new
                {
                    step = 4,
                    name = "Construct Connection String",
                    success = !string.IsNullOrEmpty(connectionString),
                    elapsedMs = step4Elapsed.TotalMilliseconds,
                    connectionString = connectionString != null ? connectionString.Substring(0, Math.Min(100, connectionString.Length)) + "..." : null,
                    endpointUsed = endpointHost,
                    endpointFromConnectionUri = connectionUriEndpoint,
                    endpointsMatch = !string.IsNullOrEmpty(connectionUriEndpoint) && !string.IsNullOrEmpty(endpointHost) 
                        ? connectionUriEndpoint.Equals(endpointHost, StringComparison.OrdinalIgnoreCase) 
                        : (bool?)null,
                    extractedUsername = extractedUsername,
                    extractedPassword = extractedPassword != null ? "***" + extractedPassword.Substring(Math.Max(0, extractedPassword.Length - 4)) : null,
                    message = !string.IsNullOrEmpty(connectionString) 
                        ? $"✅ Connection string constructed using endpoint: {endpointHost}" 
                        : "❌ Failed to construct connection string"
                });

                var totalElapsed = DateTime.UtcNow - step1Start;

                return Ok(new
                {
                    success = !string.IsNullOrEmpty(connectionString),
                    message = !string.IsNullOrEmpty(connectionString) 
                        ? "✅ Successfully created branch, database, and connection string using proper workflow"
                        : "⚠️ Branch and database created, but connection string construction failed",
                    totalElapsedMs = totalElapsed.TotalMilliseconds,
                    summary = new
                    {
                        branchId = createdBranchId,
                        databaseName = testDbName,
                        endpointHost = endpointHost,
                        endpointId = endpointId,
                        connectionStringReady = !string.IsNullOrEmpty(connectionString),
                        operationsPolled = operationPollCount,
                        branchReadyFromOperations = branchReady
                    },
                    steps = steps,
                    recommendation = !string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(endpointHost)
                        ? $"✅ This workflow works! Use verified endpoint from Step 2.5 (endpoint: {endpointHost}) + password from connection_uri API. Check logs for endpoint verification details."
                        : "❌ Need to investigate why connection string construction failed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Neon branch creation V2 test");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error in Neon branch creation V2 workflow",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Test Neon project-per-tenant creation - Complete workflow with project creation
        /// POST /api/Test/Neon-project-per-tenant
        /// This implements the project-per-tenant model:
        /// 0. Create Neon project (NEW - project-per-tenant isolation)
        /// 1. Create branch in the new project
        /// 2. Poll operations API until branch is ready
        /// 2.5. Verify endpoint after operations finish
        /// 3. Create database in branch
        /// 4. Construct connection string from endpoint info + database + role/password
        /// </summary>
        [HttpPost("Neon-project-per-tenant")]
        public async Task<ActionResult> TestNeonProjectPerTenant([FromQuery] string? projectName = null)
        {
            try
            {
                var step0Start = DateTime.UtcNow;
                var steps = new List<object>();

                var neonApiKey = _configuration["Neon:ApiKey"];
                var neonBaseUrl = _configuration["Neon:BaseUrl"];
                var neonDefaultOwnerName = _configuration["Neon:DefaultOwnerName"] ?? "neondb_owner";

                if (string.IsNullOrWhiteSpace(neonApiKey) || neonApiKey == "your-neon-api-key-here")
                {
                    return BadRequest(new { success = false, message = "Neon API key not configured" });
                }

                if (string.IsNullOrWhiteSpace(neonBaseUrl))
                {
                    return BadRequest(new { success = false, message = "Neon base URL not configured" });
                }

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", neonApiKey);
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                // Generate test project name if not provided
                if (string.IsNullOrWhiteSpace(projectName))
                {
                    projectName = $"TestProject_{DateTime.UtcNow:yyyyMMddHHmmss}";
                }

                // ============================================================================
                // STEP 0: Create Neon Project (Project-Per-Tenant Model)
                // ============================================================================
                _logger.LogInformation("🏗️ [NEON PROJECT-PER-TENANT] Step 0: Creating Neon project: {ProjectName}", projectName);

                var createProjectRequest = new
                {
                    project = new
                    {
                        name = projectName
                    }
                };

                var projectRequestBody = JsonSerializer.Serialize(createProjectRequest);
                var projectContent = new StringContent(projectRequestBody, System.Text.Encoding.UTF8, "application/json");
                var projectApiUrl = $"{neonBaseUrl}/projects";
                
                _logger.LogInformation("🏗️ [NEON PROJECT-PER-TENANT] Step 0: Calling Neon API: POST {Url}", projectApiUrl);
                var projectResponse = await httpClient.PostAsync(projectApiUrl, projectContent);
                var projectResponseContent = await projectResponse.Content.ReadAsStringAsync();
                var step0Elapsed = DateTime.UtcNow - step0Start;

                string? createdProjectId = null;
                string? createdProjectName = null;
                string? defaultBranchId = null; // Extract default branch ID from project creation response
                var projectOperationIds = new List<string>(); // Extract operation IDs from project creation

                if (projectResponse.IsSuccessStatusCode)
                {
                    var projectDoc = JsonDocument.Parse(projectResponseContent);
                    if (projectDoc.RootElement.TryGetProperty("project", out var projectObj))
                    {
                        if (projectObj.TryGetProperty("id", out var projectIdProp))
                        {
                            createdProjectId = projectIdProp.GetString();
                        }
                        if (projectObj.TryGetProperty("name", out var projectNameProp))
                        {
                            createdProjectName = projectNameProp.GetString();
                        }
                    }

                    // Extract default branch ID from project creation response (new projects automatically create a main branch)
                    if (projectDoc.RootElement.TryGetProperty("branch", out var branchObj))
                    {
                        if (branchObj.TryGetProperty("id", out var branchIdProp))
                        {
                            defaultBranchId = branchIdProp.GetString();
                            _logger.LogInformation("🔍 [NEON PROJECT-PER-TENANT] Step 0: Found default branch ID in project creation response: {BranchId}", defaultBranchId);
                        }
                    }

                    // Extract operation IDs from project creation (we need to wait for these to finish)
                    if (projectDoc.RootElement.TryGetProperty("operations", out var operationsProp) && operationsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var op in operationsProp.EnumerateArray())
                        {
                            if (op.TryGetProperty("id", out var opIdProp))
                            {
                                var opId = opIdProp.GetString();
                                if (!string.IsNullOrEmpty(opId))
                                {
                                    projectOperationIds.Add(opId);
                                    _logger.LogInformation("🔍 [NEON PROJECT-PER-TENANT] Step 0: Found project creation operation ID: {OperationId}", opId);
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(createdProjectId))
                    {
                        _logger.LogInformation("✅ [NEON PROJECT-PER-TENANT] Step 0: Created Neon project '{ProjectId}' ({ProjectName})", 
                            createdProjectId, createdProjectName ?? projectName);
                    }
                }

                steps.Add(new
                {
                    step = 0,
                    name = "Create Neon Project",
                    success = projectResponse.IsSuccessStatusCode && !string.IsNullOrEmpty(createdProjectId),
                    elapsedMs = step0Elapsed.TotalMilliseconds,
                    projectId = createdProjectId,
                    projectName = createdProjectName ?? projectName,
                    defaultBranchId = defaultBranchId,
                    operationIds = projectOperationIds,
                    rawResponse = projectResponseContent
                });

                if (!projectResponse.IsSuccessStatusCode || string.IsNullOrEmpty(createdProjectId))
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Failed to create Neon project",
                        steps = steps
                    });
                }

                // ============================================================================
                // STEP 0.5: Wait for Project Creation Operations to Finish
                // ============================================================================
                var step0_5Start = DateTime.UtcNow;
                var projectReady = false;
                var projectOperationPollCount = 0;
                var maxProjectOperationPolls = 60; // 60 retries × 2 seconds = 2 minutes max
                var projectOperationPollDelay = 2000;
                var trackedProjectOperations = new Dictionary<string, string>();

                if (projectOperationIds.Count > 0)
                {
                    _logger.LogInformation("⏳ [NEON PROJECT-PER-TENANT] Step 0.5: Waiting for {Count} project creation operations to finish before creating branch", 
                        projectOperationIds.Count);

                    var projectOperationsApiUrl = $"{neonBaseUrl}/projects/{createdProjectId}/operations";

                    while (projectOperationPollCount < maxProjectOperationPolls && !projectReady)
                    {
                        try
                        {
                            var opsResponse = await httpClient.GetAsync(projectOperationsApiUrl);
                            if (opsResponse.IsSuccessStatusCode)
                            {
                                var opsContent = await opsResponse.Content.ReadAsStringAsync();
                                var opsDoc = JsonDocument.Parse(opsContent);

                                trackedProjectOperations.Clear();

                                if (opsDoc.RootElement.TryGetProperty("operations", out var opsArray) && opsArray.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var op in opsArray.EnumerateArray())
                                    {
                                        if (op.TryGetProperty("id", out var idProp))
                                        {
                                            var opId = idProp.GetString();
                                            if (projectOperationIds.Contains(opId))
                                            {
                                                var opStatus = op.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "unknown";
                                                var opAction = op.TryGetProperty("action", out var actionProp) ? actionProp.GetString() : "unknown";
                                                trackedProjectOperations[opId] = $"{opAction}:{opStatus}";
                                            }
                                        }
                                    }
                                }

                                if (projectOperationIds.Count > 0)
                                {
                                    var allTracked = projectOperationIds.All(id => trackedProjectOperations.ContainsKey(id));
                                    var allFinished = allTracked && trackedProjectOperations.Values.All(status => 
                                        status.Contains(":finished") || status.Contains(":completed"));

                                    if (allFinished && allTracked)
                                    {
                                        projectReady = true;
                                        _logger.LogInformation("✅ [NEON PROJECT-PER-TENANT] Step 0.5 SUCCESS: All {Count} project creation operations finished after {Polls} polls", 
                                            projectOperationIds.Count, projectOperationPollCount);
                                        break;
                                    }
                                }
                            }

                            if (!projectReady)
                            {
                                projectOperationPollCount++;
                                if (projectOperationPollCount < maxProjectOperationPolls)
                                {
                                    var statuses = string.Join(", ", trackedProjectOperations.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                                    _logger.LogInformation("⏳ [NEON PROJECT-PER-TENANT] Step 0.5: Waiting for project operations (poll {Poll}/{MaxPolls}, found {Found}/{Total}). Statuses: {Statuses}", 
                                        projectOperationPollCount, maxProjectOperationPolls, trackedProjectOperations.Count, projectOperationIds.Count, statuses);
                                    await Task.Delay(projectOperationPollDelay);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ [NEON PROJECT-PER-TENANT] Error polling project operations (attempt {Attempt})", projectOperationPollCount + 1);
                            projectOperationPollCount++;
                            if (projectOperationPollCount < maxProjectOperationPolls)
                            {
                                await Task.Delay(projectOperationPollDelay);
                            }
                        }
                    }
                }
                else
                {
                    // No operations to wait for, proceed immediately
                    projectReady = true;
                    _logger.LogInformation("⏳ [NEON PROJECT-PER-TENANT] Step 0.5: No project creation operations found, proceeding immediately");
                }

                var step0_5Elapsed = DateTime.UtcNow - step0_5Start;
                steps.Add(new
                {
                    step = 0.5,
                    name = "Wait for Project Creation Operations",
                    success = projectReady,
                    elapsedMs = step0_5Elapsed.TotalMilliseconds,
                    polls = projectOperationPollCount,
                    trackedOperationIds = projectOperationIds,
                    operationStatuses = trackedProjectOperations.Select(kvp => $"{kvp.Key}:{kvp.Value}").ToList(),
                    message = projectReady 
                        ? $"Project ready - all {projectOperationIds.Count} operations finished" 
                        : $"Still waiting after {projectOperationPollCount} polls"
                });

                if (!projectReady)
                {
                    _logger.LogWarning("⚠️ [NEON PROJECT-PER-TENANT] Step 0.5: Project operations did not finish, but proceeding with branch creation anyway");
                }

                // ============================================================================
                // STEP 1: Create Branch in the New Project
                // ============================================================================
                var step1Start = DateTime.UtcNow;
                var testDbName = $"AppDB_test_{DateTime.UtcNow:yyyyMMddHHmmss}";

                _logger.LogInformation("🌿 [NEON PROJECT-PER-TENANT] Step 1: Creating branch in project '{ProjectId}'", createdProjectId);

                // For new projects, create a branch from the default/main branch that was auto-created
                // Use JsonSerializerOptions to ignore null values
                var jsonOptions = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                object createBranchRequest;
                if (!string.IsNullOrEmpty(defaultBranchId))
                {
                    // Create branch from the default branch (for isolation)
                    _logger.LogInformation("🌿 [NEON PROJECT-PER-TENANT] Step 1: Creating branch from default branch '{DefaultBranchId}'", defaultBranchId);
                    createBranchRequest = new
                    {
                        endpoints = new[]
                        {
                            new { type = "read_write" }
                        },
                        branch = new { parent_id = defaultBranchId }
                    };
                }
                else
                {
                    // Fallback: Create branch without parent (will use project's default branch)
                    _logger.LogInformation("🌿 [NEON PROJECT-PER-TENANT] Step 1: Creating branch without parent (default branch not found in response)");
                    createBranchRequest = new
                    {
                        endpoints = new[]
                        {
                            new { type = "read_write" }
                        }
                    };
                }

                var branchRequestBody = JsonSerializer.Serialize(createBranchRequest, jsonOptions);
                var branchContent = new StringContent(branchRequestBody, System.Text.Encoding.UTF8, "application/json");
                var branchApiUrl = $"{neonBaseUrl}/projects/{createdProjectId}/branches";
                
                _logger.LogInformation("🌿 [NEON PROJECT-PER-TENANT] Step 1: Calling Neon API: POST {Url}", branchApiUrl);
                var branchResponse = await httpClient.PostAsync(branchApiUrl, branchContent);
                var branchResponseContent = await branchResponse.Content.ReadAsStringAsync();
                var step1Elapsed = DateTime.UtcNow - step1Start;

                string? createdBranchId = null;
                string? endpointHost = null;
                string? endpointId = null;
                string? endpointPort = null;
                var operationIds = new List<string>();

                if (branchResponse.IsSuccessStatusCode)
                {
                    var branchDoc = JsonDocument.Parse(branchResponseContent);
                    
                    if (branchDoc.RootElement.TryGetProperty("branch", out var branchObj))
                    {
                        if (branchObj.TryGetProperty("id", out var branchIdProp))
                        {
                            createdBranchId = branchIdProp.GetString();
                        }
                    }

                    if (branchDoc.RootElement.TryGetProperty("endpoints", out var endpointsProp) && endpointsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var endpoint in endpointsProp.EnumerateArray())
                        {
                            if (endpoint.TryGetProperty("branch_id", out var epBranchIdProp) && epBranchIdProp.GetString() == createdBranchId)
                            {
                                if (endpoint.TryGetProperty("id", out var epIdProp))
                                    endpointId = epIdProp.GetString();
                                if (endpoint.TryGetProperty("host", out var hostProp))
                                    endpointHost = hostProp.GetString();
                                if (endpoint.TryGetProperty("port", out var portProp) && portProp.ValueKind == JsonValueKind.Number)
                                    endpointPort = portProp.GetInt32().ToString();
                                break;
                            }
                        }
                    }

                    if (branchDoc.RootElement.TryGetProperty("operations", out var operationsProp) && operationsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var op in operationsProp.EnumerateArray())
                        {
                            if (op.TryGetProperty("id", out var opIdProp))
                            {
                                var opId = opIdProp.GetString();
                                if (!string.IsNullOrEmpty(opId))
                                {
                                    operationIds.Add(opId);
                                }
                            }
                        }
                    }

                    _logger.LogInformation("✅ [NEON PROJECT-PER-TENANT] Step 1: Branch created successfully");
                    _logger.LogInformation("🔍 [NEON PROJECT-PER-TENANT] Step 1: Branch ID: {BranchId}", createdBranchId);
                    _logger.LogInformation("🔍 [NEON PROJECT-PER-TENANT] Step 1: Initial endpoint host: {EndpointHost}", endpointHost);
                    _logger.LogInformation("🔍 [NEON PROJECT-PER-TENANT] Step 1: Initial endpoint ID: {EndpointId}", endpointId);
                    _logger.LogInformation("🔍 [NEON PROJECT-PER-TENANT] Step 1: Found {Count} operation IDs: {OperationIds}", 
                        operationIds.Count, string.Join(", ", operationIds));
                }

                steps.Add(new
                {
                    step = 1,
                    name = "Create Branch",
                    success = branchResponse.IsSuccessStatusCode,
                    elapsedMs = step1Elapsed.TotalMilliseconds,
                    branchId = createdBranchId,
                    endpointHost = endpointHost,
                    endpointId = endpointId,
                    endpointPort = endpointPort,
                    operationIds = operationIds,
                    rawResponse = branchResponseContent
                });

                if (!branchResponse.IsSuccessStatusCode || string.IsNullOrEmpty(createdBranchId))
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Failed to create branch",
                        steps = steps
                    });
                }

                // ============================================================================
                // STEP 2: Poll Operations API until SPECIFIC operations from Step 1 are ready
                // ============================================================================
                var step2Start = DateTime.UtcNow;
                var branchReady = false;
                var operationPollCount = 0;
                var maxOperationPolls = 60;
                var operationPollDelay = 2000;
                var trackedOperations = new Dictionary<string, string>();

                if (operationIds.Count > 0)
                {
                    _logger.LogInformation("🔍 [NEON PROJECT-PER-TENANT] Step 2: Polling operations API for {Count} operations until branch '{BranchId}' is ready", 
                        operationIds.Count, createdBranchId);

                    var operationsApiUrl = $"{neonBaseUrl}/projects/{createdProjectId}/operations";

                    while (operationPollCount < maxOperationPolls && !branchReady)
                    {
                        try
                        {
                            var opsResponse = await httpClient.GetAsync(operationsApiUrl);
                            if (opsResponse.IsSuccessStatusCode)
                            {
                                var opsContent = await opsResponse.Content.ReadAsStringAsync();
                                var opsDoc = JsonDocument.Parse(opsContent);

                                trackedOperations.Clear();

                                if (opsDoc.RootElement.TryGetProperty("operations", out var opsArray) && opsArray.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var op in opsArray.EnumerateArray())
                                    {
                                        if (op.TryGetProperty("id", out var idProp))
                                        {
                                            var opId = idProp.GetString();
                                            if (operationIds.Contains(opId))
                                            {
                                                var opStatus = op.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "unknown";
                                                trackedOperations[opId] = opStatus;
                                            }
                                        }
                                    }
                                }

                                if (operationIds.Count > 0)
                                {
                                    var allTracked = operationIds.All(id => trackedOperations.ContainsKey(id));
                                    var allFinished = allTracked && trackedOperations.Values.All(status => 
                                        status == "finished" || status == "completed");

                                    if (allFinished && allTracked)
                                    {
                                        branchReady = true;
                                        _logger.LogInformation("✅ [NEON PROJECT-PER-TENANT] Step 2 SUCCESS: All {Count} operations finished for branch '{BranchId}' after {Polls} polls", 
                                            operationIds.Count, createdBranchId, operationPollCount);
                                        break;
                                    }
                                }
                            }

                            if (!branchReady)
                            {
                                operationPollCount++;
                                if (operationPollCount < maxOperationPolls)
                                {
                                    var statuses = string.Join(", ", trackedOperations.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                                    _logger.LogInformation("⏳ [NEON PROJECT-PER-TENANT] Step 2: Waiting for operations (poll {Poll}/{MaxPolls}, found {Found}/{Total}). Statuses: {Statuses}", 
                                        operationPollCount, maxOperationPolls, trackedOperations.Count, operationIds.Count, statuses);
                                    await Task.Delay(operationPollDelay);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ [NEON PROJECT-PER-TENANT] Error polling operations (attempt {Attempt})", operationPollCount + 1);
                            operationPollCount++;
                            if (operationPollCount < maxOperationPolls)
                            {
                                await Task.Delay(operationPollDelay);
                            }
                        }
                    }
                }

                var step2Elapsed = DateTime.UtcNow - step2Start;
                steps.Add(new
                {
                    step = 2,
                    name = "Poll Operations API",
                    success = branchReady,
                    elapsedMs = step2Elapsed.TotalMilliseconds,
                    polls = operationPollCount,
                    trackedOperationIds = operationIds,
                    operationStatuses = trackedOperations.Select(kvp => $"{kvp.Key}:{kvp.Value}").ToList(),
                    message = branchReady 
                        ? $"Branch is ready - all {operationIds.Count} operations finished" 
                        : $"Still waiting after {operationPollCount} polls"
                });

                // ============================================================================
                // STEP 2.5: Verify endpoint after operations finish
                // ============================================================================
                var step2_5Start = DateTime.UtcNow;
                string? verifiedEndpointHost = endpointHost;
                string? verifiedEndpointId = endpointId;

                if (branchReady && !string.IsNullOrEmpty(createdBranchId))
                {
                    _logger.LogInformation("🔍 [NEON PROJECT-PER-TENANT] Step 2.5: Verifying endpoint after operations finished for branch '{BranchId}'", createdBranchId);
                    
                    try
                    {
                        var verifyBranchApiUrl = $"{neonBaseUrl}/projects/{createdProjectId}/branches/{Uri.EscapeDataString(createdBranchId)}";
                        var branchApiResponse = await httpClient.GetAsync(verifyBranchApiUrl);

                        if (branchApiResponse.IsSuccessStatusCode)
                        {
                            var branchApiContent = await branchApiResponse.Content.ReadAsStringAsync();
                            var branchApiDoc = JsonDocument.Parse(branchApiContent);

                            var endpointsArray = default(JsonElement);
                            var endpointsFound = false;

                            if (branchApiDoc.RootElement.TryGetProperty("endpoints", out var rootEndpointsProp) && 
                                rootEndpointsProp.ValueKind == JsonValueKind.Array)
                            {
                                endpointsArray = rootEndpointsProp;
                                endpointsFound = true;
                            }
                            else if (branchApiDoc.RootElement.TryGetProperty("branch", out var branchObj) &&
                                     branchObj.TryGetProperty("endpoints", out var branchEndpointsProp) && 
                                     branchEndpointsProp.ValueKind == JsonValueKind.Array)
                            {
                                endpointsArray = branchEndpointsProp;
                                endpointsFound = true;
                            }

                            if (!endpointsFound)
                            {
                                var projectEndpointsUrl = $"{neonBaseUrl}/projects/{createdProjectId}/endpoints";
                                var endpointsResponse = await httpClient.GetAsync(projectEndpointsUrl);

                                if (endpointsResponse.IsSuccessStatusCode)
                                {
                                    var endpointsContent = await endpointsResponse.Content.ReadAsStringAsync();
                                    var endpointsDoc = JsonDocument.Parse(endpointsContent);

                                    if (endpointsDoc.RootElement.TryGetProperty("endpoints", out var projectEndpointsProp) && 
                                        projectEndpointsProp.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var ep in projectEndpointsProp.EnumerateArray())
                                        {
                                            if (ep.TryGetProperty("branch_id", out var epBranchIdProp) &&
                                                epBranchIdProp.GetString() == createdBranchId)
                                            {
                                                if (ep.TryGetProperty("host", out var epHostProp))
                                                {
                                                    var verifiedHost = epHostProp.GetString();
                                                    if (!string.IsNullOrEmpty(verifiedHost))
                                                    {
                                                        verifiedEndpointHost = verifiedHost;
                                                        _logger.LogInformation("✅ [NEON PROJECT-PER-TENANT] Step 2.5: Verified endpoint host: {EndpointHost}", verifiedEndpointHost);
                                                        
                                                        if (ep.TryGetProperty("id", out var epIdProp))
                                                        {
                                                            verifiedEndpointId = epIdProp.GetString();
                                                        }
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception verifyEx)
                    {
                        _logger.LogWarning(verifyEx, "⚠️ [NEON PROJECT-PER-TENANT] Step 2.5: Exception verifying endpoint");
                    }
                }

                endpointHost = verifiedEndpointHost ?? endpointHost;
                endpointId = verifiedEndpointId ?? endpointId;

                var step2_5Elapsed = DateTime.UtcNow - step2_5Start;
                steps.Add(new
                {
                    step = 2.5,
                    name = "Verify Endpoint After Operations",
                    success = !string.IsNullOrEmpty(verifiedEndpointHost),
                    elapsedMs = step2_5Elapsed.TotalMilliseconds,
                    originalEndpointFromStep1 = endpointHost,
                    verifiedEndpointFromBranchAPI = verifiedEndpointHost,
                    message = !string.IsNullOrEmpty(verifiedEndpointHost) 
                        ? $"✅ Endpoint verified: {verifiedEndpointHost}" 
                        : "⚠️ Using original endpoint from Step 1"
                });

                // ============================================================================
                // STEP 3: Create Database in Branch
                // ============================================================================
                var step3Start = DateTime.UtcNow;
                var createDbRequest = new
                {
                    database = new
                    {
                        name = testDbName,
                        owner_name = neonDefaultOwnerName
                    }
                };

                var dbApiUrl = $"{neonBaseUrl}/projects/{createdProjectId}/branches/{Uri.EscapeDataString(createdBranchId)}/databases";
                var dbRequestBody = JsonSerializer.Serialize(createDbRequest);
                var dbContent = new StringContent(dbRequestBody, System.Text.Encoding.UTF8, "application/json");

                _logger.LogInformation("🔍 [NEON PROJECT-PER-TENANT] Step 3: Creating database '{DbName}' in branch '{BranchId}'", testDbName, createdBranchId);
                var dbResponse = await httpClient.PostAsync(dbApiUrl, dbContent);
                var dbResponseContent = await dbResponse.Content.ReadAsStringAsync();
                var step3Elapsed = DateTime.UtcNow - step3Start;

                steps.Add(new
                {
                    step = 3,
                    name = "Create Database",
                    success = dbResponse.IsSuccessStatusCode,
                    elapsedMs = step3Elapsed.TotalMilliseconds,
                    databaseName = testDbName,
                    rawResponse = dbResponseContent
                });

                if (!dbResponse.IsSuccessStatusCode)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Failed to create database",
                        steps = steps
                    });
                }

                // ============================================================================
                // STEP 4: Get Password and Construct Connection String
                // ============================================================================
                var step4Start = DateTime.UtcNow;
                string? connectionString = null;
                string? extractedPassword = null;
                string? extractedUsername = null;
                string? connectionUriEndpoint = null;

                _logger.LogInformation("🔍 [NEON PROJECT-PER-TENANT] Step 4: Starting connection string construction");
                _logger.LogInformation("🔍 [NEON PROJECT-PER-TENANT] Step 4: Using endpoint host: {EndpointHost}", endpointHost);

                var connectionUrl = $"{neonBaseUrl}/projects/{createdProjectId}/connection_uri?database_name={Uri.EscapeDataString(testDbName)}&role_name={Uri.EscapeDataString(neonDefaultOwnerName)}&branch_id={Uri.EscapeDataString(createdBranchId)}&pooled=false";

                try
                {
                    _logger.LogInformation("🔍 [NEON PROJECT-PER-TENANT] Step 4: Calling connection_uri API to extract password");
                    var connResponse = await httpClient.GetAsync(connectionUrl);
                    if (connResponse.IsSuccessStatusCode)
                    {
                        var connContent = await connResponse.Content.ReadAsStringAsync();
                        var connDoc = JsonDocument.Parse(connContent);
                        if (connDoc.RootElement.TryGetProperty("uri", out var uriProp))
                        {
                            var tempConnString = uriProp.GetString();
                            if (!string.IsNullOrEmpty(tempConnString))
                            {
                                var uri = new Uri(tempConnString);
                                connectionUriEndpoint = uri.Host;

                                _logger.LogInformation("🔍 [NEON PROJECT-PER-TENANT] Step 4: connection_uri API returned endpoint: {ConnectionUriEndpoint}", connectionUriEndpoint);

                                var userInfo = uri.UserInfo.Split(':');
                                extractedUsername = userInfo.Length > 0 ? userInfo[0] : neonDefaultOwnerName;
                                extractedPassword = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";

                                _logger.LogInformation("✅ [NEON PROJECT-PER-TENANT] Step 4: Extracted username: {Username}, password length: {PasswordLength}", 
                                    extractedUsername, extractedPassword?.Length ?? 0);

                                if (!string.IsNullOrEmpty(connectionUriEndpoint) && !string.IsNullOrEmpty(endpointHost))
                                {
                                    var endpointsMatch = connectionUriEndpoint.Equals(endpointHost, StringComparison.OrdinalIgnoreCase);
                                    if (endpointsMatch)
                                    {
                                        _logger.LogInformation("✅ [NEON PROJECT-PER-TENANT] Step 4: Endpoint from connection_uri matches verified endpoint: {Endpoint}", endpointHost);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("⚠️ [NEON PROJECT-PER-TENANT] Step 4: Endpoint mismatch! connection_uri: {ConnectionUriEndpoint}, verified: {VerifiedEndpoint}. Using verified endpoint.", 
                                            connectionUriEndpoint, endpointHost);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ [NEON PROJECT-PER-TENANT] Step 4: Failed to extract password from connection_uri API");
                }

                if (!string.IsNullOrEmpty(endpointHost) && !string.IsNullOrEmpty(extractedPassword))
                {
                    var port = !string.IsNullOrEmpty(endpointPort) ? int.Parse(endpointPort) : 5432;
                    connectionString = $"postgresql://{extractedUsername}:{Uri.EscapeDataString(extractedPassword)}@{endpointHost}:{port}/{testDbName}?sslmode=require";

                    _logger.LogInformation("✅ [NEON PROJECT-PER-TENANT] Step 4: Connection string constructed successfully");
                    _logger.LogInformation("🔍 [NEON PROJECT-PER-TENANT] Step 4: Connection string details - Host: {Host}, Port: {Port}, Database: {Db}, Username: {User}", 
                        endpointHost, port, testDbName, extractedUsername);
                }

                var step4Elapsed = DateTime.UtcNow - step4Start;
                steps.Add(new
                {
                    step = 4,
                    name = "Construct Connection String",
                    success = !string.IsNullOrEmpty(connectionString),
                    elapsedMs = step4Elapsed.TotalMilliseconds,
                    connectionString = connectionString != null ? connectionString.Substring(0, Math.Min(100, connectionString.Length)) + "..." : null,
                    endpointUsed = endpointHost,
                    endpointFromConnectionUri = connectionUriEndpoint,
                    endpointsMatch = !string.IsNullOrEmpty(connectionUriEndpoint) && !string.IsNullOrEmpty(endpointHost) 
                        ? connectionUriEndpoint.Equals(endpointHost, StringComparison.OrdinalIgnoreCase) 
                        : (bool?)null,
                    extractedUsername = extractedUsername,
                    extractedPassword = extractedPassword != null ? "***" + extractedPassword.Substring(Math.Max(0, extractedPassword.Length - 4)) : null,
                    message = !string.IsNullOrEmpty(connectionString) 
                        ? $"✅ Connection string constructed using endpoint: {endpointHost}" 
                        : "❌ Failed to construct connection string"
                });

                var totalElapsed = DateTime.UtcNow - step0Start;

                return Ok(new
                {
                    success = !string.IsNullOrEmpty(connectionString),
                    message = !string.IsNullOrEmpty(connectionString) 
                        ? "✅ Successfully created Neon project, branch, database, and connection string using project-per-tenant model"
                        : "⚠️ Project, branch, and database created, but connection string construction failed",
                    totalElapsedMs = totalElapsed.TotalMilliseconds,
                    summary = new
                    {
                        projectId = createdProjectId,
                        projectName = createdProjectName ?? projectName,
                        branchId = createdBranchId,
                        databaseName = testDbName,
                        endpointHost = endpointHost,
                        endpointId = endpointId,
                        connectionStringReady = !string.IsNullOrEmpty(connectionString),
                        operationsPolled = operationPollCount,
                        branchReadyFromOperations = branchReady
                    },
                    steps = steps,
                    recommendation = !string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(endpointHost)
                        ? $"✅ Project-per-tenant model works! Each board now has its own isolated Neon project. Project: {createdProjectId}, Endpoint: {endpointHost}"
                        : "❌ Need to investigate why connection string construction failed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Neon project-per-tenant test");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error in Neon project-per-tenant workflow",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Helper method to analyze branch API response structure
        /// </summary>
        private object AnalyzeBranchResponse(JsonElement root)
        {
            var analysis = new Dictionary<string, object>();

            // Check for endpoints array at root level
            if (root.TryGetProperty("endpoints", out var rootEndpoints) && rootEndpoints.ValueKind == JsonValueKind.Array)
            {
                analysis["endpointsLocation"] = "root";
                analysis["endpointsCount"] = rootEndpoints.GetArrayLength();
                
                var endpointIds = new List<string>();
                var endpointHosts = new List<string>();
                foreach (var endpoint in rootEndpoints.EnumerateArray())
                {
                    if (endpoint.TryGetProperty("id", out var epId))
                        endpointIds.Add(epId.GetString() ?? "unknown");
                    if (endpoint.TryGetProperty("host", out var epHost))
                        endpointHosts.Add(epHost.GetString() ?? "unknown");
                }
                analysis["endpointIds"] = endpointIds;
                analysis["endpointHosts"] = endpointHosts;
            }
            // Check for endpoints array inside branch object
            else if (root.TryGetProperty("branch", out var branchObj) &&
                     branchObj.TryGetProperty("endpoints", out var branchEndpoints) &&
                     branchEndpoints.ValueKind == JsonValueKind.Array)
            {
                analysis["endpointsLocation"] = "branch.endpoints";
                analysis["endpointsCount"] = branchEndpoints.GetArrayLength();
                
                var endpointIds = new List<string>();
                var endpointHosts = new List<string>();
                foreach (var endpoint in branchEndpoints.EnumerateArray())
                {
                    if (endpoint.TryGetProperty("id", out var epId))
                        endpointIds.Add(epId.GetString() ?? "unknown");
                    if (endpoint.TryGetProperty("host", out var epHost))
                        endpointHosts.Add(epHost.GetString() ?? "unknown");
                }
                analysis["endpointIds"] = endpointIds;
                analysis["endpointHosts"] = endpointHosts;
            }
            else
            {
                analysis["endpointsLocation"] = "not found";
                analysis["endpointsCount"] = 0;
            }

            // Check for branch ID
            if (root.TryGetProperty("id", out var rootId))
                analysis["branchId"] = rootId.GetString();
            else if (root.TryGetProperty("branch", out var branch) && branch.TryGetProperty("id", out var branchId))
                analysis["branchId"] = branchId.GetString();

            // List all top-level properties
            var topLevelProperties = new List<string>();
            foreach (var prop in root.EnumerateObject())
            {
                topLevelProperties.Add($"{prop.Name} ({prop.Value.ValueKind})");
            }
            analysis["topLevelProperties"] = topLevelProperties;

            return analysis;
        }

        #endregion

        #region Language-Specific Database Test Methods

        /// <summary>
        /// Test C# backend database operations (simulates Railway deployment after board creation)
        /// Tests the exact operations that happen in the generated C# backend
        /// </summary>
        [HttpGet("test-csharp/{boardId}")]
        [ProducesResponseType(typeof(LanguageBackendTestResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<LanguageBackendTestResponse>> TestCSharpBackend(string boardId)
        {
            try
            {
                _logger.LogInformation("🧪 Testing C# backend database operations for board {BoardId}", boardId);

                // Get board and connection info
                var connectionInfo = await GetBoardConnectionInfo(boardId);
                if (connectionInfo == null)
                {
                    return NotFound(new { success = false, message = $"Board {boardId} not found or missing database configuration" });
                }

                var (connectionString, databaseName) = connectionInfo.Value;
                var results = new List<object>();
                var errors = new List<string>();

                // Simulate C# backend operations
                try
                {
                    // Parse connection string URI and use NpgsqlConnectionStringBuilder for proper parsing
                    var uri = new Uri(connectionString);
                    var userInfo = uri.UserInfo.Split(':');
                    var username = userInfo[0];
                    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
                    
                    var builder = new NpgsqlConnectionStringBuilder
                    {
                        Host = uri.Host,
                        Port = uri.Port > 0 ? uri.Port : 5432,
                        Database = uri.AbsolutePath.TrimStart('/'),
                        Username = username,
                        Password = password,
                        SslMode = SslMode.Require
                    };
                    
                    using var conn = new NpgsqlConnection(builder.ConnectionString);
                    await conn.OpenAsync();

                    // Set search_path to public schema (required because isolated role has restricted search_path)
                    // Note: Using string concatenation to avoid $ interpolation issues
                    using var setPathCmd = new NpgsqlCommand("SET search_path = public, \"" + "$" + "user\";", conn);
                    await setPathCmd.ExecuteNonQueryAsync();

                    var quote = Convert.ToChar(34).ToString(); // Double quote for PostgreSQL identifier quoting
                    var sql = $"SELECT {quote}Id{quote}, {quote}Name{quote} FROM {quote}TestProjects{quote} ORDER BY {quote}Id{quote}";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        results.Add(new
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }

                    _logger.LogInformation("✅ C# test successful: Retrieved {Count} rows", results.Count);
                }
                catch (PostgresException ex) when (ex.SqlState == "42P01")
                {
                    errors.Add($"Table does not exist: {ex.Message}");
                }
                catch (Exception ex)
                {
                    errors.Add($"C# test failed: {ex.Message}");
                    _logger.LogError(ex, "C# backend test error");
                }

                return Ok(new LanguageBackendTestResponse
                {
                    Success = errors.Count == 0,
                    Language = "C#",
                    BoardId = boardId,
                    Database = databaseName,
                    RowsRetrieved = results.Count,
                    Results = results,
                    Errors = errors,
                    ConnectionStringUsed = connectionString.Substring(0, Math.Min(50, connectionString.Length)) + "..."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing C# backend for board {BoardId}", boardId);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Test Python backend database operations (simulates Railway deployment after board creation)
        /// Tests the exact operations that happen in the generated Python backend
        /// </summary>
        [HttpGet("test-python/{boardId}")]
        [ProducesResponseType(typeof(LanguageBackendTestResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<LanguageBackendTestResponse>> TestPythonBackend(string boardId)
        {
            try
            {
                _logger.LogInformation("🧪 Testing Python backend database operations for board {BoardId}", boardId);

                // Get board and connection info
                var connectionInfo = await GetBoardConnectionInfo(boardId);
                if (connectionInfo == null)
                {
                    return NotFound(new { success = false, message = $"Board {boardId} not found or missing database configuration" });
                }

                var (connectionString, databaseName) = connectionInfo.Value;
                var results = new List<object>();
                var errors = new List<string>();

                // Simulate Python backend operations (using asyncpg-style approach)
                try
                {
                    // Parse connection string URI and use NpgsqlConnectionStringBuilder for proper parsing
                    var uri = new Uri(connectionString);
                    var userInfo = uri.UserInfo.Split(':');
                    var username = userInfo[0];
                    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
                    
                    var builder = new NpgsqlConnectionStringBuilder
                    {
                        Host = uri.Host,
                        Port = uri.Port > 0 ? uri.Port : 5432,
                        Database = uri.AbsolutePath.TrimStart('/'),
                        Username = username,
                        Password = password,
                        SslMode = SslMode.Require
                    };
                    
                    using var conn = new NpgsqlConnection(builder.ConnectionString);
                    await conn.OpenAsync();

                    // Set search_path to public schema (required because isolated role has restricted search_path)
                    using var setPathCmd = new NpgsqlCommand("SET search_path = public, \"$user\";", conn);
                    await setPathCmd.ExecuteNonQueryAsync();

                    // Python uses parameterized queries with %s placeholder
                    var sql = "SELECT \"Id\", \"Name\" FROM \"TestProjects\" ORDER BY \"Id\"";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        results.Add(new
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }

                    _logger.LogInformation("✅ Python test successful: Retrieved {Count} rows", results.Count);
                }
                catch (PostgresException ex) when (ex.SqlState == "42P01")
                {
                    errors.Add($"Table does not exist: {ex.Message}");
                }
                catch (Exception ex)
                {
                    errors.Add($"Python test failed: {ex.Message}");
                    _logger.LogError(ex, "Python backend test error");
                }

                return Ok(new LanguageBackendTestResponse
                {
                    Success = errors.Count == 0,
                    Language = "Python",
                    BoardId = boardId,
                    Database = databaseName,
                    RowsRetrieved = results.Count,
                    Results = results,
                    Errors = errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Python backend for board {BoardId}", boardId);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Test Node.js backend database operations (simulates Railway deployment after board creation)
        /// Tests the exact operations that happen in the generated Node.js backend
        /// </summary>
        [HttpGet("test-nodejs/{boardId}")]
        [ProducesResponseType(typeof(LanguageBackendTestResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<LanguageBackendTestResponse>> TestNodeJSBackend(string boardId)
        {
            try
            {
                _logger.LogInformation("🧪 Testing Node.js backend database operations for board {BoardId}", boardId);

                // Get board and connection info
                var connectionInfo = await GetBoardConnectionInfo(boardId);
                if (connectionInfo == null)
                {
                    return NotFound(new { success = false, message = $"Board {boardId} not found or missing database configuration" });
                }

                var (connectionString, databaseName) = connectionInfo.Value;
                var results = new List<object>();
                var errors = new List<string>();

                // Simulate Node.js backend operations (using pg pool-style approach)
                try
                {
                    // Parse connection string URI and use NpgsqlConnectionStringBuilder for proper parsing
                    var uri = new Uri(connectionString);
                    var userInfo = uri.UserInfo.Split(':');
                    var username = userInfo[0];
                    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
                    
                    var builder = new NpgsqlConnectionStringBuilder
                    {
                        Host = uri.Host,
                        Port = uri.Port > 0 ? uri.Port : 5432,
                        Database = uri.AbsolutePath.TrimStart('/'),
                        Username = username,
                        Password = password,
                        SslMode = SslMode.Require
                    };
                    
                    using var conn = new NpgsqlConnection(builder.ConnectionString);
                    await conn.OpenAsync();

                    // Set search_path to public schema (required because isolated role has restricted search_path)
                    using var setPathCmd = new NpgsqlCommand("SET search_path = public, \"$user\";", conn);
                    await setPathCmd.ExecuteNonQueryAsync();

                    // Node.js uses $1, $2, etc. for parameterized queries
                    var sql = "SELECT \"Id\", \"Name\" FROM \"TestProjects\" ORDER BY \"Id\"";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        results.Add(new
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }

                    _logger.LogInformation("✅ Node.js test successful: Retrieved {Count} rows", results.Count);
                }
                catch (PostgresException ex) when (ex.SqlState == "42P01")
                {
                    errors.Add($"Table does not exist: {ex.Message}");
                }
                catch (Exception ex)
                {
                    errors.Add($"Node.js test failed: {ex.Message}");
                    _logger.LogError(ex, "Node.js backend test error");
                }

                return Ok(new LanguageBackendTestResponse
                {
                    Success = errors.Count == 0,
                    Language = "Node.js",
                    BoardId = boardId,
                    Database = databaseName,
                    RowsRetrieved = results.Count,
                    Results = results,
                    Errors = errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Node.js backend for board {BoardId}", boardId);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Test Java backend database operations (simulates Railway deployment after board creation)
        /// Tests the exact operations that happen in the generated Java backend
        /// </summary>
        [HttpGet("test-java/{boardId}")]
        [ProducesResponseType(typeof(LanguageBackendTestResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<LanguageBackendTestResponse>> TestJavaBackend(string boardId)
        {
            try
            {
                _logger.LogInformation("🧪 Testing Java backend database operations for board {BoardId}", boardId);

                // Get board and connection info
                var connectionInfo = await GetBoardConnectionInfo(boardId);
                if (connectionInfo == null)
                {
                    return NotFound(new { success = false, message = $"Board {boardId} not found or missing database configuration" });
                }

                var (connectionString, databaseName) = connectionInfo.Value;
                var results = new List<object>();
                var errors = new List<string>();

                // Simulate Java backend operations (using JPA EntityManager-style approach)
                try
                {
                    // Parse connection string URI and use NpgsqlConnectionStringBuilder for proper parsing
                    var uri = new Uri(connectionString);
                    var userInfo = uri.UserInfo.Split(':');
                    var username = userInfo[0];
                    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
                    
                    var builder = new NpgsqlConnectionStringBuilder
                    {
                        Host = uri.Host,
                        Port = uri.Port > 0 ? uri.Port : 5432,
                        Database = uri.AbsolutePath.TrimStart('/'),
                        Username = username,
                        Password = password,
                        SslMode = SslMode.Require
                    };
                    
                    using var conn = new NpgsqlConnection(builder.ConnectionString);
                    await conn.OpenAsync();

                    // Set search_path to public schema (required because isolated role has restricted search_path)
                    using var setPathCmd = new NpgsqlCommand("SET search_path = public, \"$user\";", conn);
                    await setPathCmd.ExecuteNonQueryAsync();

                    // Java uses :paramName for named parameters in native queries
                    var sql = "SELECT \"Id\", \"Name\" FROM \"TestProjects\" ORDER BY \"Id\"";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        results.Add(new
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }

                    _logger.LogInformation("✅ Java test successful: Retrieved {Count} rows", results.Count);
                }
                catch (PostgresException ex) when (ex.SqlState == "42P01")
                {
                    errors.Add($"Table does not exist: {ex.Message}");
                }
                catch (Exception ex)
                {
                    errors.Add($"Java test failed: {ex.Message}");
                    _logger.LogError(ex, "Java backend test error");
                }

                return Ok(new LanguageBackendTestResponse
                {
                    Success = errors.Count == 0,
                    Language = "Java",
                    BoardId = boardId,
                    Database = databaseName,
                    RowsRetrieved = results.Count,
                    Results = results,
                    Errors = errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Java backend for board {BoardId}", boardId);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Helper method to get board connection information and construct connection string
        /// </summary>
        private async Task<(string ConnectionString, string DatabaseName)?> GetBoardConnectionInfo(string boardId)
        {
            try
            {
                var board = await _context.ProjectBoards
                    .FirstOrDefaultAsync(pb => pb.Id == boardId);

                if (board == null)
                {
                    _logger.LogWarning("Board {BoardId} not found", boardId);
                    return null;
                }

                var dbName = $"AppDB_{boardId}";
                var dbPassword = board.DBPassword;
                var neonProjectId = board.NeonProjectId;
                var neonBranchId = board.NeonBranchId;

                if (string.IsNullOrEmpty(dbPassword))
                {
                    _logger.LogWarning("Board {BoardId} missing DBPassword", boardId);
                    return null;
                }

                // Construct username (sanitized board ID)
                var sanitizedDbName = dbName.ToLowerInvariant().Replace("-", "_").Replace(".", "_");
                var username = $"db_{sanitizedDbName}_user".Substring(0, Math.Min(63, $"db_{sanitizedDbName}_user".Length));

                // Try to get endpoint from Neon API
                string? endpointHost = null;
                int endpointPort = 5432;

                if (!string.IsNullOrEmpty(neonProjectId) && !string.IsNullOrEmpty(neonBranchId))
                {
                    try
                    {
                        var neonApiKey = _configuration["Neon:ApiKey"];
                        var neonBaseUrl = _configuration["Neon:BaseUrl"];

                        if (!string.IsNullOrEmpty(neonApiKey) && !string.IsNullOrEmpty(neonBaseUrl))
                        {
                            // Get endpoints for the project
                            var endpointsUrl = $"{neonBaseUrl}/projects/{neonProjectId}/endpoints";
                            using var httpClient = _httpClientFactory.CreateClient();
                            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", neonApiKey);

                            var endpointsResponse = await httpClient.GetAsync(endpointsUrl);
                            if (endpointsResponse.IsSuccessStatusCode)
                            {
                                var endpointsContent = await endpointsResponse.Content.ReadAsStringAsync();
                                var endpointsDoc = JsonDocument.Parse(endpointsContent);

                                if (endpointsDoc.RootElement.TryGetProperty("endpoints", out var endpointsArray) && endpointsArray.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var endpoint in endpointsArray.EnumerateArray())
                                    {
                                        if (endpoint.TryGetProperty("branch_id", out var branchIdProp) &&
                                            branchIdProp.GetString() == neonBranchId &&
                                            endpoint.TryGetProperty("host", out var hostProp))
                                        {
                                            endpointHost = hostProp.GetString();
                                            if (endpoint.TryGetProperty("port", out var portProp) && portProp.ValueKind == JsonValueKind.Number)
                                            {
                                                endpointPort = portProp.GetInt32();
                                            }
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to retrieve endpoint from Neon API for board {BoardId}", boardId);
                    }
                }

                if (string.IsNullOrEmpty(endpointHost))
                {
                    _logger.LogWarning("Could not determine endpoint host for board {BoardId}. Using default.", boardId);
                    // Fallback: try to extract from connection string if available, or use a default
                    return null;
                }

                // Construct connection string
                var connectionString = $"postgresql://{username}:{Uri.EscapeDataString(dbPassword)}@{endpointHost}:{endpointPort}/{dbName}?sslmode=require";

                return (connectionString, dbName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting connection info for board {BoardId}", boardId);
                return null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Response model for language-specific backend tests
    /// </summary>
    public class LanguageBackendTestResponse
    {
        public bool Success { get; set; }
        public string Language { get; set; } = string.Empty;
        public string BoardId { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public int RowsRetrieved { get; set; }
        public List<object> Results { get; set; } = new List<object>();
        public List<string> Errors { get; set; } = new List<string>();
        public string? ConnectionStringUsed { get; set; }
    }
}

public class TestSmtpEmailRequest
{
    public string RecipientEmail { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? Description { get; set; }
    public string? MeetingLink { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
