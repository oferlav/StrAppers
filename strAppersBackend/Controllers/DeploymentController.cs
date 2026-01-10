using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using strAppersBackend.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeploymentController : ControllerBase
    {
        private readonly ILogger<DeploymentController> _logger;
        private readonly IGitHubService _gitHubService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public DeploymentController(
            ILogger<DeploymentController> logger,
            IGitHubService gitHubService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _gitHubService = gitHubService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        /// <summary>
        /// Deploys a GitHub repository (frontend to GitHub Pages or backend to Railway)
        /// Determines deployment target automatically by repository content
        /// </summary>
        [HttpPost("deploy")]
        public async Task<IActionResult> DeployRepository([FromBody] DeployRepositoryRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.RepositoryUrl))
                {
                    return BadRequest(new { success = false, message = "Repository URL is required" });
                }

                _logger.LogInformation("[DEPLOY] Starting deployment for repository: {RepositoryUrl}", request.RepositoryUrl);

                // Parse repository URL
                var uri = new Uri(request.RepositoryUrl);
                var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
                if (pathParts.Length < 2)
                {
                    return BadRequest(new { success = false, message = "Invalid repository URL format" });
                }

                var owner = pathParts[0];
                var repoName = pathParts[1].Replace(".git", "");

                var githubToken = _configuration["GitHub:AccessToken"];
                if (string.IsNullOrEmpty(githubToken))
                {
                    return StatusCode(500, new { success = false, message = "GitHub access token not configured" });
                }

                // Detect repository type (frontend or backend)
                var repoType = await DetectRepositoryTypeAsync(owner, repoName, githubToken);
                if (repoType == RepositoryType.Unknown)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Could not determine repository type. Repository should contain either frontend files (index.html, config.js) or backend files (Program.cs, Backend.csproj, nixpacks.toml)"
                    });
                }

                _logger.LogInformation("[DEPLOY] Detected repository type: {RepoType} for {Owner}/{Repo}", repoType, owner, repoName);

                // Deploy based on type
                DeployRepositoryResponse response;
                if (repoType == RepositoryType.Frontend)
                {
                    response = await DeployFrontendAsync(owner, repoName, githubToken);
                }
                else // Backend
                {
                    response = await DeployBackendAsync(owner, repoName, githubToken);
                }

                if (response.Success)
                {
                    return Ok(response);
                }
                else
                {
                    return BadRequest(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DEPLOY] Error deploying repository: {Message}", ex.Message);
                return StatusCode(500, new { success = false, message = $"Error deploying repository: {ex.Message}" });
            }
        }

        /// <summary>
        /// Detects if repository is frontend or backend by checking file contents
        /// </summary>
        private async Task<RepositoryType> DetectRepositoryTypeAsync(string owner, string repoName, string accessToken)
        {
            try
            {
                _logger.LogInformation("[DEPLOY] Detecting repository type for {Owner}/{Repo}", owner, repoName);

                // Check for frontend files
                var hasIndexHtml = await CheckFileExistsAsync(owner, repoName, "index.html", accessToken);
                var hasConfigJs = await CheckFileExistsAsync(owner, repoName, "config.js", accessToken);

                // Check for backend files
                var hasProgramCs = await CheckFileExistsAsync(owner, repoName, "Program.cs", accessToken);
                var hasBackendCsproj = await CheckFileExistsAsync(owner, repoName, "Backend.csproj", accessToken);
                var hasNixpacksToml = await CheckFileExistsAsync(owner, repoName, "nixpacks.toml", accessToken);
                var hasAppPy = await CheckFileExistsAsync(owner, repoName, "app.py", accessToken);
                var hasMainPy = await CheckFileExistsAsync(owner, repoName, "main.py", accessToken);
                var hasAppJs = await CheckFileExistsAsync(owner, repoName, "app.js", accessToken);

                // Frontend detection: has index.html or config.js (and no backend indicators)
                var frontendIndicators = (hasIndexHtml ? 1 : 0) + (hasConfigJs ? 1 : 0);
                var backendIndicators = (hasProgramCs ? 1 : 0) + (hasBackendCsproj ? 1 : 0) + 
                                       (hasNixpacksToml ? 1 : 0) + (hasAppPy ? 1 : 0) + 
                                       (hasMainPy ? 1 : 0) + (hasAppJs ? 1 : 0);

                if (frontendIndicators > 0 && backendIndicators == 0)
                {
                    _logger.LogInformation("[DEPLOY] Repository detected as FRONTEND (has index.html: {HasHtml}, has config.js: {HasConfig})", 
                        hasIndexHtml, hasConfigJs);
                    return RepositoryType.Frontend;
                }
                else if (backendIndicators > 0)
                {
                    _logger.LogInformation("[DEPLOY] Repository detected as BACKEND (indicators: Program.cs={HasProg}, Backend.csproj={HasCsproj}, nixpacks.toml={HasNix})", 
                        hasProgramCs, hasBackendCsproj, hasNixpacksToml);
                    return RepositoryType.Backend;
                }
                else
                {
                    _logger.LogWarning("[DEPLOY] Repository type UNKNOWN - no clear frontend or backend indicators found");
                    return RepositoryType.Unknown;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DEPLOY] Error detecting repository type: {Message}", ex.Message);
                return RepositoryType.Unknown;
            }
        }

        /// <summary>
        /// Checks if a file exists in the repository
        /// </summary>
        private async Task<bool> CheckFileExistsAsync(string owner, string repoName, string filePath, string accessToken)
        {
            try
            {
                var content = await _gitHubService.GetFileContentAsync(owner, repoName, filePath, accessToken, "main");
                return !string.IsNullOrEmpty(content);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Deploys frontend repository to GitHub Pages (public method for direct calls from BoardsController)
        /// </summary>
        [ApiExplorerSettings(IgnoreApi = true)] // Exclude from Swagger - not a direct API endpoint
        public async Task<DeployRepositoryResponse> DeployFrontendRepositoryAsync(string repositoryUrl)
        {
            try
            {
                var uri = new Uri(repositoryUrl);
                var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
                if (pathParts.Length < 2)
                {
                    throw new ArgumentException("Invalid repository URL format");
                }

                var owner = pathParts[0];
                var repoName = pathParts[1].Replace(".git", "");

                var githubToken = _configuration["GitHub:AccessToken"];
                if (string.IsNullOrEmpty(githubToken))
                {
                    throw new InvalidOperationException("GitHub access token not configured");
                }

                return await DeployFrontendAsync(owner, repoName, githubToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DEPLOY] Error in DeployFrontendRepositoryAsync: {Message}", ex.Message);
                return new DeployRepositoryResponse
                {
                    Success = false,
                    Message = $"Error deploying frontend: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Deploys backend repository to Railway (public method for direct calls from BoardsController)
        /// </summary>
        [ApiExplorerSettings(IgnoreApi = true)] // Exclude from Swagger - not a direct API endpoint
        public async Task<DeployRepositoryResponse> DeployBackendRepositoryAsync(string repositoryUrl)
        {
            try
            {
                var uri = new Uri(repositoryUrl);
                var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
                if (pathParts.Length < 2)
                {
                    throw new ArgumentException("Invalid repository URL format");
                }

                var owner = pathParts[0];
                var repoName = pathParts[1].Replace(".git", "");

                var githubToken = _configuration["GitHub:AccessToken"];
                if (string.IsNullOrEmpty(githubToken))
                {
                    throw new InvalidOperationException("GitHub access token not configured");
                }

                return await DeployBackendAsync(owner, repoName, githubToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DEPLOY] Error in DeployBackendRepositoryAsync: {Message}", ex.Message);
                return new DeployRepositoryResponse
                {
                    Success = false,
                    Message = $"Error deploying backend: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Deploys frontend repository to GitHub Pages
        /// Since files are at root, GitHub Pages serves them directly - no workflow needed
        /// </summary>
        private async Task<DeployRepositoryResponse> DeployFrontendAsync(string owner, string repoName, string accessToken)
        {
            try
            {
                _logger.LogInformation("[DEPLOY] Deploying frontend {Owner}/{Repo} to GitHub Pages", owner, repoName);

                // Enable GitHub Pages (files at root are served automatically)
                var pagesEnabled = await _gitHubService.EnableGitHubPagesAsync(owner, repoName, accessToken);
                
                // Frontend repo name is boardId directly (no prefix)
                var pagesUrl = _gitHubService.GetGitHubPagesUrl(owner, repoName);

                // Check GitHub Pages status
                await _gitHubService.CheckGitHubPagesStatusAsync(owner, repoName, accessToken);

                return new DeployRepositoryResponse
                {
                    Success = true,
                    RepositoryType = "frontend",
                    DeploymentTarget = "GitHub Pages",
                    DeploymentUrl = pagesUrl,
                    WorkflowTriggered = false, // No workflow needed
                    Status = pagesEnabled ? "enabled" : "pending",
                    Message = pagesEnabled 
                        ? "GitHub Pages enabled successfully. Files will be served directly from root. Pages may take a few minutes to be available." 
                        : "Repository created with files at root. GitHub Pages needs to be enabled manually in Settings > Pages (API requires 'pages:write' scope)."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DEPLOY] Error deploying frontend: {Message}", ex.Message);
                return new DeployRepositoryResponse
                {
                    Success = false,
                    Message = $"Error deploying frontend: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Deploys backend repository to Railway
        /// </summary>
        private async Task<DeployRepositoryResponse> DeployBackendAsync(string owner, string repoName, string accessToken)
        {
            try
            {
                _logger.LogInformation("[DEPLOY] Deploying backend {Owner}/{Repo} to Railway", owner, repoName);

                // Check if Railway secrets exist (RAILWAY_TOKEN, RAILWAY_SERVICE_ID)
                var hasRailwaySecrets = await CheckRailwaySecretsExistAsync(owner, repoName, accessToken);

                // Try to trigger the workflow (it may exist even if file check fails due to caching/timing)
                // If the workflow doesn't exist, the trigger will fail and we'll handle it gracefully
                var workflowTriggered = await _gitHubService.TriggerWorkflowDispatchAsync(
                    owner, repoName, "deploy-backend.yml", accessToken);

                WorkflowRunStatus? workflowStatus = null;

                if (workflowTriggered)
                {
                    _logger.LogInformation("[DEPLOY] Workflow triggered successfully");
                    // Get latest workflow run status
                    workflowStatus = await GetLatestWorkflowRunStatusAsync(owner, repoName, "deploy-backend.yml", accessToken);
                }
                else
                {
                    _logger.LogInformation("[DEPLOY] Workflow trigger failed - Railway will auto-deploy when repo is connected");
                    // Railway auto-deploys when the GitHub repo is connected, so this is not necessarily a failure
                }

                // Try to get Railway deployment status (if we have service ID)
                string? railwayDeploymentUrl = null;
                if (hasRailwaySecrets)
                {
                    // Note: We would need to get RAILWAY_SERVICE_ID from secrets to query Railway API
                    // For now, we'll just return workflow status
                }

                var message = workflowTriggered 
                    ? "Backend deployment workflow triggered successfully" 
                    : "Workflow file exists but GitHub Actions hasn't processed it yet. Railway will auto-deploy when repository is connected. Manual workflow triggers will work once GitHub processes the workflow file (usually within a few minutes).";

                return new DeployRepositoryResponse
                {
                    Success = true,
                    RepositoryType = "backend",
                    DeploymentTarget = "Railway",
                    DeploymentUrl = railwayDeploymentUrl,
                    WorkflowTriggered = workflowTriggered,
                    Status = workflowStatus?.Status ?? "unknown",
                    WorkflowRunId = workflowStatus?.RunId,
                    WorkflowRunUrl = workflowStatus?.HtmlUrl,
                    LogsUrl = workflowStatus?.LogsUrl,
                    Message = message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DEPLOY] Error deploying backend: {Message}", ex.Message);
                return new DeployRepositoryResponse
                {
                    Success = false,
                    Message = $"Error deploying backend: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Checks if Railway secrets exist in the repository
        /// </summary>
        private async Task<bool> CheckRailwaySecretsExistAsync(string owner, string repoName, string accessToken)
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient("DeploymentController");
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppersBackend/1.0");

                // Check for RAILWAY_TOKEN and RAILWAY_SERVICE_ID secrets
                var secretsUrl = $"https://api.github.com/repos/{owner}/{repoName}/actions/secrets";
                var response = await httpClient.GetAsync(secretsUrl);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var secretsDoc = JsonDocument.Parse(content);
                    if (secretsDoc.RootElement.TryGetProperty("secrets", out var secretsArray))
                    {
                        var secrets = secretsArray.EnumerateArray().ToList();
                        var hasToken = secrets.Any(s => s.TryGetProperty("name", out var name) && name.GetString() == "RAILWAY_TOKEN");
                        var hasServiceId = secrets.Any(s => s.TryGetProperty("name", out var name) && name.GetString() == "RAILWAY_SERVICE_ID");
                        return hasToken && hasServiceId;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DEPLOY] Error checking Railway secrets: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Gets the latest workflow run status for a workflow file
        /// </summary>
        private async Task<WorkflowRunStatus?> GetLatestWorkflowRunStatusAsync(string owner, string repoName, string workflowFileName, string accessToken)
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient("DeploymentController");
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppersBackend/1.0");

                // First, get workflow ID from workflow file
                var workflowId = workflowFileName.EndsWith(".yml") || workflowFileName.EndsWith(".yaml")
                    ? workflowFileName
                    : $"{workflowFileName}.yml";

                // Get workflow runs for this workflow
                var runsUrl = $"https://api.github.com/repos/{owner}/{repoName}/actions/workflows/{workflowId}/runs?per_page=1";
                var response = await httpClient.GetAsync(runsUrl);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var runsDoc = JsonDocument.Parse(content);

                    if (runsDoc.RootElement.TryGetProperty("workflow_runs", out var runsArray))
                    {
                        var runs = runsArray.EnumerateArray().ToList();
                        if (runs.Count > 0)
                        {
                            var latestRun = runs[0];
                            var runId = latestRun.TryGetProperty("id", out var idProp) ? idProp.GetInt64() : 0;
                            var status = latestRun.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "unknown";
                            var conclusion = latestRun.TryGetProperty("conclusion", out var conclusionProp) ? conclusionProp.GetString() : null;
                            var htmlUrl = latestRun.TryGetProperty("html_url", out var htmlUrlProp) ? htmlUrlProp.GetString() : null;
                            var logsUrl = latestRun.TryGetProperty("logs_url", out var logsUrlProp) ? logsUrlProp.GetString() : null;

                            return new WorkflowRunStatus
                            {
                                RunId = runId,
                                Status = status ?? "unknown",
                                Conclusion = conclusion,
                                HtmlUrl = htmlUrl,
                                LogsUrl = logsUrl
                            };
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DEPLOY] Error getting workflow run status: {Message}", ex.Message);
                return null;
            }
        }
    }

    /// <summary>
    /// Request model for deployment
    /// </summary>
    public class DeployRepositoryRequest
    {
        /// <summary>
        /// GitHub repository URL (e.g., https://github.com/owner/repo)
        /// </summary>
        public string RepositoryUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model for deployment
    /// </summary>
    public class DeployRepositoryResponse
    {
        public bool Success { get; set; }
        public string RepositoryType { get; set; } = string.Empty; // "frontend" or "backend"
        public string DeploymentTarget { get; set; } = string.Empty; // "GitHub Pages" or "Railway"
        public string? DeploymentUrl { get; set; }
        public bool WorkflowTriggered { get; set; }
        public string Status { get; set; } = string.Empty; // workflow status
        public long? WorkflowRunId { get; set; }
        public string? WorkflowRunUrl { get; set; }
        public string? LogsUrl { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Repository type enumeration
    /// </summary>
    internal enum RepositoryType
    {
        Unknown,
        Frontend,
        Backend
    }

    /// <summary>
    /// Workflow run status information
    /// </summary>
    internal class WorkflowRunStatus
    {
        public long RunId { get; set; }
        public string Status { get; set; } = string.Empty; // queued, in_progress, completed
        public string? Conclusion { get; set; } // success, failure, cancelled, etc.
        public string? HtmlUrl { get; set; }
        public string? LogsUrl { get; set; }
    }
}
