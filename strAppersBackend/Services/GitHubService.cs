using Google.Apis.Gmail.v1.Data;
using Humanizer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using System;
using System.Diagnostics.Metrics;
using System.Net.NetworkInformation;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;

namespace strAppersBackend.Services;

public interface IGitHubService
{
    Task<bool> ValidateGitHubUserAsync(string username);
    Task<GitHubUser?> GetGitHubUserAsync(string username);
    Task<string?> ExchangeCodeForTokenAsync(string code);
    Task<GitHubUserInfo?> GetGitHubUserInfoAsync(string accessToken);
    Task<CreateRepositoryResponse> CreateRepositoryAsync(CreateRepositoryRequest request);
    Task<bool> AddCollaboratorAsync(string owner, string repositoryName, string collaboratorUsername, string accessToken);
    Task<bool> CreateInitialCommitAsync(string owner, string repositoryName, string projectTitle, string accessToken, string? databaseConnectionString = null, string? webApiUrl = null, string? swaggerUrl = null, string? programmingLanguage = null);
    Task<bool> CreateFrontendOnlyCommitAsync(string owner, string repositoryName, string projectTitle, string accessToken, string? webApiUrl = null);
    Task<bool> CreateBackendOnlyCommitAsync(string owner, string repositoryName, string projectTitle, string accessToken, string programmingLanguage, string? databaseConnectionString = null, string? webApiUrl = null, string? swaggerUrl = null);
    Task<bool> EnableGitHubPagesAsync(string owner, string repositoryName, string accessToken);
    Task<bool> CheckGitHubPagesStatusAsync(string owner, string repositoryName, string accessToken);
    Task<bool> TriggerWorkflowDispatchAsync(string owner, string repositoryName, string workflowFileName, string accessToken);
    Task<bool> CreateOrUpdateRepositorySecretAsync(string owner, string repositoryName, string secretName, string secretValue, string accessToken);
    string GetGitHubPagesUrl(string repositoryName);
    string GetGitHubPagesUrl(string owner, string repositoryName);
    Task<DateTime?> GetLastCommitDateByUserAsync(string owner, string repo, string username, string? accessToken = null);
    Task<GitHubCommitInfo?> GetLastCommitInfoByUserAsync(string owner, string repo, string username, string? accessToken = null);
    
    // Code Review Agent methods
    Task<List<GitHubCommit>> GetRecentCommitsAsync(string owner, string repo, string username, int count = 10, string? accessToken = null);
    Task<GitHubCommitDiff?> GetCommitDiffAsync(string owner, string repo, string commitSha, string? accessToken = null);
    Task<List<GitHubFileChange>> GetFileChangesAsync(string owner, string repo, string commitSha, string? accessToken = null);
    Task<bool> HasRecentCommitsAsync(string owner, string repo, string username, int hours = 24, string? accessToken = null);
    Task<string?> GetFileContentAsync(string owner, string repo, string filePath, string? accessToken = null, string? branch = null);
    Task<bool> UpdateFileAsync(string owner, string repo, string filePath, string content, string message, string? accessToken = null, string? branch = null);
    string GenerateConfigJs(string? webApiUrl, string? mentorApiBaseUrl = null);
    Task<bool> CreateRepositoryRulesetAsync(string owner, string repo, string repoType, string accessToken);
    Task<bool> CreateBranchProtectionAsync(string owner, string repo, string branchName, string accessToken);
    Task<CreateBranchResponse> CreateBranchAsync(string owner, string repo, string branchName, string sourceBranch, string accessToken);
}

public class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubService> _logger;
    private readonly IConfiguration _configuration;
    private const string GitHubApiBaseUrl = "https://api.github.com";
    private const string GitHubTokenUrl = "https://github.com/login/oauth/access_token";

    public GitHubService(HttpClient httpClient, ILogger<GitHubService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        
        // GitHub API requires a User-Agent header
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppers-API");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    }

    /// <summary>
    /// Validates if a GitHub username exists
    /// </summary>
    public async Task<bool> ValidateGitHubUserAsync(string username)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                _logger.LogWarning("GitHub username validation failed: username is null or empty");
                return false;
            }

            _logger.LogInformation("Validating GitHub user: {Username}", username);

            var response = await _httpClient.GetAsync($"{GitHubApiBaseUrl}/users/{username}");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("GitHub user {Username} exists", username);
                return true;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("GitHub user {Username} not found", username);
                return false;
            }

            // Log other status codes
            _logger.LogError("GitHub API returned status code {StatusCode} for user {Username}", 
                response.StatusCode, username);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating GitHub user {Username}: {Message}", username, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets GitHub user information
    /// </summary>
    public async Task<GitHubUser?> GetGitHubUserAsync(string username)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            _logger.LogInformation("Getting GitHub user information for: {Username}", username);

            var response = await _httpClient.GetAsync($"{GitHubApiBaseUrl}/users/{username}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get GitHub user {Username}: {StatusCode}", 
                    username, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var githubUser = JsonSerializer.Deserialize<GitHubUser>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return githubUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting GitHub user {Username}: {Message}", username, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Exchanges GitHub OAuth authorization code for access token
    /// </summary>
    public async Task<string?> ExchangeCodeForTokenAsync(string code)
    {
        try
        {
            var clientId = _configuration["GitHub:ClientId"];
            var clientSecret = _configuration["GitHub:ClientSecret"];
            var redirectUri = _configuration["GitHub:RedirectUri"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                _logger.LogError("GitHub OAuth credentials not configured");
                return null;
            }

            _logger.LogInformation("Exchanging GitHub OAuth code for access token");

            var requestBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri ?? "")
            });

            _logger.LogInformation("Sending token exchange request to GitHub. RedirectUri: {RedirectUri}", redirectUri);
            
            // GitHub OAuth token endpoint accepts form-encoded POST
            // Add Accept header to request JSON response (optional, but helps with debugging)
            var request = new HttpRequestMessage(HttpMethod.Post, GitHubTokenUrl)
            {
                Content = requestBody
            };
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            
            var response = await _httpClient.SendAsync(request);

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("GitHub token exchange response - Status: {StatusCode}, Content: {Response}", response.StatusCode, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to exchange GitHub OAuth code. Status: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
                
                // Try to parse error from JSON response
                try
                {
                    var errorJson = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    if (errorJson.TryGetProperty("error", out var errorProp))
                    {
                        var error = errorProp.GetString();
                        var errorDesc = errorJson.TryGetProperty("error_description", out var descProp) ? descProp.GetString() : "Unknown error";
                        _logger.LogError("GitHub OAuth error details: {Error} - {Description}", error, errorDesc);
                    }
                }
                catch { }
                
                return null;
            }

            // Parse the response - GitHub returns JSON when Accept: application/json header is sent
            string? accessToken = null;
            
            // GitHub returns JSON when we send Accept: application/json header
            try
            {
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                
                if (jsonResponse.TryGetProperty("access_token", out var tokenProp))
                {
                    accessToken = tokenProp.GetString();
                    _logger.LogInformation("Successfully extracted access token from JSON response");
                }
                else if (jsonResponse.TryGetProperty("error", out var errorProp))
                {
                    var error = errorProp.GetString();
                    var errorDesc = jsonResponse.TryGetProperty("error_description", out var descProp) ? descProp.GetString() : "Unknown error";
                    _logger.LogError("GitHub OAuth error (JSON): {Error} - {Description}", error, errorDesc);
                    return null;
                }
            }
            catch (JsonException jsonEx)
            {
                // Response might be form-encoded instead
                _logger.LogWarning(jsonEx, "Failed to parse as JSON, trying form-encoded format. Response: {Response}", responseContent);
                
                try
                {
                    var parameters = responseContent.Split('&')
                        .Where(p => p.Contains('='))
                        .Select(p => 
                        {
                            var parts = p.Split(new[] { '=' }, 2);
                            return parts.Length == 2 
                                ? new { Key = parts[0], Value = Uri.UnescapeDataString(parts[1]) }
                                : null;
                        })
                        .Where(p => p != null)
                        .ToDictionary(p => p!.Key, p => p!.Value);

                    if (parameters.TryGetValue("access_token", out var token))
                    {
                        accessToken = token;
                        _logger.LogInformation("Successfully extracted access token from form-encoded response");
                    }
                    else if (parameters.TryGetValue("error", out var error))
                    {
                        var errorDescription = parameters.TryGetValue("error_description", out var desc) ? desc : "Unknown error";
                        _logger.LogError("GitHub OAuth error (form-encoded): {Error} - {Description}", error, errorDescription);
                        return null;
                    }
                }
                catch (Exception parseEx)
                {
                    _logger.LogError(parseEx, "Failed to parse GitHub token response in both JSON and form-encoded formats. Content: {Response}", responseContent);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error parsing GitHub token response. Content: {Response}", responseContent);
                return null;
            }

            if (!string.IsNullOrEmpty(accessToken))
            {
                _logger.LogInformation("Successfully exchanged GitHub OAuth code for access token");
                return accessToken;
            }

            _logger.LogError("No access token found in GitHub response. Content: {Response}", responseContent);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging GitHub OAuth code for access token: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Gets GitHub user information using access token
    /// </summary>
    public async Task<GitHubUserInfo?> GetGitHubUserInfoAsync(string accessToken)
    {
        try
        {
            _logger.LogInformation("Getting GitHub user info with access token");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{GitHubApiBaseUrl}/user");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get GitHub user info. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<GitHubUserInfo>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Successfully retrieved GitHub user info for: {Username}", userInfo?.Login);
            return userInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting GitHub user info: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Creates a new GitHub repository and adds collaborators
    /// </summary>
    public async Task<CreateRepositoryResponse> CreateRepositoryAsync(CreateRepositoryRequest request)
    {
        var response = new CreateRepositoryResponse
        {
            RepositoryName = request.Name,
            Success = false
        };

        try
        {
            _logger.LogInformation("Creating GitHub repository: {RepositoryName}", request.Name);

            // Get the access token from configuration
            var accessToken = _configuration["GitHub:AccessToken"];
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("GitHub access token is not configured");
                response.ErrorMessage = "GitHub access token is not configured";
                return response;
            }

            _logger.LogInformation("Using GitHub access token (first 10 chars): {TokenPrefix}", 
                accessToken.Length > 10 ? accessToken.Substring(0, 10) + "..." : accessToken);

            // Get organization/user account name from configuration
            // Note: If this is a user account (not an organization), we use /user/repos endpoint
            // The repository will be created under the authenticated user's account (token owner)
            var organizationName = _configuration["GitHub:Organization"];
            var useOrganization = !string.IsNullOrEmpty(organizationName);

            _logger.LogInformation("📦 [GITHUB] Repository creation config - Organization/Account: {Org}, RepoName: {RepoName}", 
                organizationName ?? "authenticated user", request.Name);

            // Create repository payload
            var repositoryPayload = new
            {
                name = request.Name,
                description = request.Description ?? "",
                @private = request.IsPrivate,
                auto_init = true
            };

            var jsonContent = JsonSerializer.Serialize(repositoryPayload);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            // Always use /user/repos endpoint - this creates repos under the authenticated user's account
            // If organizationName is set, it's just for reference/logging - the token owner determines where the repo is created
            // To create under a specific organization, use /orgs/{org}/repos (requires org membership and write:org scope)
            // To create under a specific user account, the token must belong to that account and use /user/repos
            var endpoint = $"{GitHubApiBaseUrl}/user/repos";
            
            if (useOrganization)
            {
                _logger.LogInformation("📦 [GITHUB] Organization/Account name configured: {Org}. Using /user/repos endpoint (repo will be created under token owner's account)", organizationName);
            }
            else
            {
                _logger.LogInformation("📦 [GITHUB] No organization configured. Using /user/repos endpoint (repo will be created under token owner's account)");
            }
            
            _logger.LogInformation("📦 [GITHUB] Creating repository using endpoint: {Endpoint}", endpoint);
            
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Content = content;

            var createResponse = await _httpClient.SendAsync(httpRequest);

            if (!createResponse.IsSuccessStatusCode)
            {
                var errorContent = await createResponse.Content.ReadAsStringAsync();
                _logger.LogError("❌ [GITHUB] Failed to create GitHub repository. Status: {StatusCode}, Error: {Error}, Endpoint: {Endpoint}", 
                    createResponse.StatusCode, errorContent, endpoint);
                
                // Provide more specific error messages
                if (createResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    response.ErrorMessage = "Access denied. Please check that the GitHub access token has 'repo' permissions and is valid.";
                }
                else if (createResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    response.ErrorMessage = "Unauthorized. Please check that the GitHub access token is valid and not expired.";
                }
                else if (createResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    response.ErrorMessage = "Repository or user not found. Please check that the GitHub access token is valid and belongs to the correct account.";
                }
                else
                {
                    response.ErrorMessage = $"Failed to create repository: {createResponse.StatusCode}. Error: {errorContent}";
                }
                return response;
            }

            var repositoryContent = await createResponse.Content.ReadAsStringAsync();
            _logger.LogInformation("GitHub API response: {Response}", repositoryContent);
            
            var repository = JsonSerializer.Deserialize<GitHubRepository>(repositoryContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (repository == null)
            {
                _logger.LogError("Failed to deserialize created repository");
                response.ErrorMessage = "Failed to parse created repository";
                return response;
            }

            _logger.LogInformation("Parsed repository - HtmlUrl: {HtmlUrl}, Name: {Name}, FullName: {FullName}", repository.HtmlUrl, repository.Name, repository.FullName);
            response.RepositoryUrl = repository.HtmlUrl;
            response.Success = true;

            _logger.LogInformation("Successfully created GitHub repository: {RepositoryUrl}", repository.HtmlUrl);

            // Extract owner from FullName (format: "owner/repo-name") or from authenticated user
            // Since we use /user/repos endpoint, the repo is created under the token owner's account
            string repositoryOwner;
            if (!string.IsNullOrEmpty(repository.FullName) && repository.FullName.Contains('/'))
            {
                repositoryOwner = repository.FullName.Split('/')[0];
                _logger.LogInformation("📦 [GITHUB] Extracted repository owner from FullName: {Owner}", repositoryOwner);
            }
            else
            {
                // Get from user info (the token owner - this is where the repo was actually created)
            var userInfo = await GetGitHubUserInfoAsync(accessToken);
            if (userInfo == null)
            {
                    _logger.LogError("❌ [GITHUB] Failed to get current user info and cannot determine repository owner");
                    response.ErrorMessage = "Failed to determine repository owner";
                return response;
                }
                repositoryOwner = userInfo.Login;
                _logger.LogInformation("📦 [GITHUB] Repository created under authenticated user account: {Owner}", repositoryOwner);
                
                // Log if configured organization/account name differs from actual owner
                if (useOrganization && !string.IsNullOrEmpty(organizationName) && organizationName != repositoryOwner)
                {
                    _logger.LogWarning("⚠️ [GITHUB] Configured organization/account name '{ConfigName}' differs from token owner '{ActualOwner}'. Repository created under '{ActualOwner}'.", 
                        organizationName, repositoryOwner, repositoryOwner);
                }
            }

            // Set the GitHub Pages URL using the actual owner
            // Frontend repos: use repo name directly (no prefix) - URL is https://owner.github.io/{repoName}/
            // Backend repos: extract boardId by removing "backend_" prefix
            var repoNameForUrl = request.Name;
            if (repoNameForUrl.StartsWith("backend_"))
            {
                repoNameForUrl = repoNameForUrl.Substring("backend_".Length);
            }
            // Frontend repos don't have prefix, so use name as-is
            response.GitHubPagesUrl = GetGitHubPagesUrl(repositoryOwner, repoNameForUrl);

            // Skip CreateInitialCommitAsync for backend repos (they use CreateBackendOnlyCommitAsync instead)
            // Skip CreateInitialCommitAsync for frontend repos (they use CreateFrontendOnlyCommitAsync instead)
            // Frontend repos: no prefix (just boardId) - detected by absence of ProgrammingLanguage
            // Backend repos: "backend_" prefix - detected by StartsWith("backend_")
            var isBackendRepo = request.Name.StartsWith("backend_");
            var isFrontendRepo = !isBackendRepo && string.IsNullOrEmpty(request.ProgrammingLanguage);
            
            if (!isBackendRepo && !isFrontendRepo)
            {
                // Only create initial commit for legacy repos (non-prefixed repos)
            _logger.LogInformation("Creating initial commit for repository");
            var projectTitle = request.ProjectTitle ?? request.Name;  // Use project title if provided, otherwise use repo name
                response.InitialCommitCreated = await CreateInitialCommitAsync(repositoryOwner, request.Name, projectTitle, accessToken, request.DatabaseConnectionString, request.WebApiUrl, request.SwaggerUrl, request.ProgrammingLanguage);
            
            if (!response.InitialCommitCreated)
            {
                _logger.LogWarning("Failed to create initial commit, but repository was created successfully");
                }
            }
            else
            {
                // Skip initial commit - will be created separately using CreateBackendOnlyCommitAsync or CreateFrontendOnlyCommitAsync
                _logger.LogInformation("Skipping initial commit for {RepoType} repo (will be created separately)", isBackendRepo ? "backend" : "frontend");
                response.InitialCommitCreated = false;
            }

            // Enable GitHub Pages (only for frontend repos)
            // Don't enable GitHub Pages here for frontend repos - it will be enabled AFTER the workflow file is committed
            // This ensures Pages uses workflow-based deployment instead of legacy mode
            if (isFrontendRepo)
            {
                _logger.LogInformation("[GithubPages] Skipping GitHub Pages setup during repository creation. Pages will be enabled after workflow file is committed.");
                response.GitHubPagesEnabled = false; // Will be enabled later in DeployFrontendRepositoryAsync
            }
            else if (isBackendRepo)
            {
                // Backend repos don't use GitHub Pages
                response.GitHubPagesEnabled = false;
            }

            // Add collaborators if any
            if (request.Collaborators.Any())
            {
                _logger.LogInformation("Adding {Count} collaborators to repository", request.Collaborators.Count);
                
                foreach (var collaborator in request.Collaborators)
                {
                    try
                    {
                        var collaboratorAdded = await AddCollaboratorAsync(repositoryOwner, request.Name, collaborator, accessToken);
                        if (collaboratorAdded)
                        {
                            response.AddedCollaborators.Add(collaborator);
                            _logger.LogInformation("Successfully added collaborator: {Collaborator}", collaborator);
                        }
                        else
                        {
                            response.FailedCollaborators.Add(collaborator);
                            _logger.LogWarning("Failed to add collaborator: {Collaborator}", collaborator);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error adding collaborator {Collaborator}: {Message}", collaborator, ex.Message);
                        response.FailedCollaborators.Add(collaborator);
                    }
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating GitHub repository: {Message}", ex.Message);
            response.ErrorMessage = ex.Message;
            return response;
        }
    }

    /// <summary>
    /// Adds a collaborator to a GitHub repository
    /// </summary>
    public async Task<bool> AddCollaboratorAsync(string owner, string repositoryName, string collaboratorUsername, string accessToken)
    {
        try
        {
            _logger.LogInformation("Adding collaborator {Collaborator} to repository {Owner}/{Repository}", 
                collaboratorUsername, owner, repositoryName);

            var collaboratorPayload = new
            {
                permission = "push" // Give push access to collaborators
            };

            var jsonContent = JsonSerializer.Serialize(collaboratorPayload);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Put, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/collaborators/{collaboratorUsername}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully added collaborator {Collaborator} to repository {Repository}", 
                    collaboratorUsername, repositoryName);
                return true;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("User {Collaborator} not found on GitHub", collaboratorUsername);
                return false;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to add collaborator {Collaborator}. Status: {StatusCode}, Error: {Error}", 
                collaboratorUsername, response.StatusCode, errorContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding collaborator {Collaborator}: {Message}", collaboratorUsername, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Creates initial commit with index.html file
    /// </summary>
    public async Task<bool> CreateInitialCommitAsync(string owner, string repositoryName, string projectTitle, string accessToken, string? databaseConnectionString = null, string? webApiUrl = null, string? swaggerUrl = null, string? programmingLanguage = null)
    {
        try
        {
            _logger.LogInformation("Creating initial commit for repository {Owner}/{Repository} with programming language: {Language}", 
                owner, repositoryName, programmingLanguage ?? "none");

            // Generate the GitHub Pages URL
            var pagesUrl = GetGitHubPagesUrl(repositoryName);

            // Generate the index.html content with project title
            var indexHtmlContent = GenerateDefaultIndexHtml(projectTitle, pagesUrl);

            // Get the default branch (usually 'main')
            var branchInfo = await GetDefaultBranchAsync(owner, repositoryName, accessToken);
            if (branchInfo == null)
            {
                _logger.LogError("Failed to get default branch information");
                return false;
            }

            // Build tree items - start with root files
            var treeItems = new List<object>();
            // Store file contents for fallback to Contents API
            var fileContents = new Dictionary<string, string>();

            // Create README.md in root (project overview)
            var rootReadmeContent = GenerateReadmeContent(projectTitle, databaseConnectionString, webApiUrl, swaggerUrl);
            var rootReadmeSha = await CreateBlobAsync(owner, repositoryName, rootReadmeContent, accessToken);
            if (!string.IsNullOrEmpty(rootReadmeSha))
            {
                treeItems.Add(new { path = "README.md", mode = "100644", type = "blob", sha = rootReadmeSha });
                fileContents["README.md"] = rootReadmeContent;
            }

            // Create frontend files in frontend/ folder
            // Create blob for index.html
            var indexHtmlSha = await CreateBlobAsync(owner, repositoryName, indexHtmlContent, accessToken);
            if (string.IsNullOrEmpty(indexHtmlSha))
            {
                _logger.LogError("Failed to create blob for index.html");
                return false;
            }
            treeItems.Add(new { path = "frontend/index.html", mode = "100644", type = "blob", sha = indexHtmlSha });
            fileContents["frontend/index.html"] = indexHtmlContent;

            // Create config.js
            var mentorApiBaseUrl = _configuration["ApiBaseUrl"];
            var configJsContent = GenerateConfigJs(webApiUrl, mentorApiBaseUrl);
            var configJsSha = await CreateBlobAsync(owner, repositoryName, configJsContent, accessToken);
            if (string.IsNullOrEmpty(configJsSha))
            {
                _logger.LogError("Failed to create blob for frontend/config.js");
                return false;
            }
            treeItems.Add(new { path = "frontend/config.js", mode = "100644", type = "blob", sha = configJsSha });
            fileContents["frontend/config.js"] = configJsContent;

            // Create style.css
            var styleCssContent = GenerateStyleCss();
            var styleCssSha = await CreateBlobAsync(owner, repositoryName, styleCssContent, accessToken);
            if (string.IsNullOrEmpty(styleCssSha))
            {
                _logger.LogError("Failed to create blob for frontend/style.css");
                return false;
            }
            treeItems.Add(new { path = "frontend/style.css", mode = "100644", type = "blob", sha = styleCssSha });
            fileContents["frontend/style.css"] = styleCssContent;

            // Create GitHub Actions workflow for deploying frontend to GitHub Pages
            var frontendWorkflow = GenerateGitHubActionsWorkflow();
            var frontendWorkflowSha = await CreateBlobAsync(owner, repositoryName, frontendWorkflow, accessToken);
            if (!string.IsNullOrEmpty(frontendWorkflowSha))
            {
                treeItems.Add(new { path = ".github/workflows/deploy-frontend.yml", mode = "100644", type = "blob", sha = frontendWorkflowSha });
                fileContents[".github/workflows/deploy-frontend.yml"] = frontendWorkflow;
            }

            // Create GitHub Actions workflow for deploying backend to Railway
            if (!string.IsNullOrEmpty(programmingLanguage))
            {
                var backendWorkflow = GenerateRailwayDeploymentWorkflow(programmingLanguage);
                var backendWorkflowSha = await CreateBlobAsync(owner, repositoryName, backendWorkflow, accessToken);
                if (!string.IsNullOrEmpty(backendWorkflowSha))
                {
                    treeItems.Add(new { path = ".github/workflows/deploy-backend.yml", mode = "100644", type = "blob", sha = backendWorkflowSha });
                    fileContents[".github/workflows/deploy-backend.yml"] = backendWorkflow;
                }
            }

            // Create .gitignore
            var gitignoreContent = GenerateGitIgnore(programmingLanguage);
            var gitignoreSha = await CreateBlobAsync(owner, repositoryName, gitignoreContent, accessToken);
            if (!string.IsNullOrEmpty(gitignoreSha))
            {
                treeItems.Add(new { path = ".gitignore", mode = "100644", type = "blob", sha = gitignoreSha });
                fileContents[".gitignore"] = gitignoreContent;
            }

            // Generate backend files if programming language is provided
            if (!string.IsNullOrEmpty(programmingLanguage))
            {
                _logger.LogInformation("🔧 [BACKEND] Generating backend files for programming language: {Language}", programmingLanguage);
                var backendFiles = GenerateBackendFiles(programmingLanguage, webApiUrl);
                _logger.LogInformation("🔧 [BACKEND] Generated {FileCount} backend files", backendFiles.Count);

                foreach (var file in backendFiles)
                {
                    _logger.LogDebug("🔧 [BACKEND] Creating blob for: {FilePath} ({ContentLength} chars)", file.Key, file.Value.Length);
                    var fileSha = await CreateBlobAsync(owner, repositoryName, file.Value, accessToken);
                    if (!string.IsNullOrEmpty(fileSha))
                    {
                        _logger.LogDebug("✅ [BACKEND] Created blob for {FilePath}, SHA: {Sha}", file.Key, fileSha);
                        treeItems.Add(new { path = file.Key, mode = "100644", type = "blob", sha = fileSha });
                        fileContents[file.Key] = file.Value;
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [BACKEND] Failed to create blob for {FilePath}", file.Key);
                    }
                }
                
                // Generate Railway configuration files (nixpacks.toml)
                // Railway will auto-build when repo is connected, so we need nixpacks.toml
                // GitHub Actions workflow can also deploy, but Railway's build should work too
                _logger.LogInformation("🔧 [RAILWAY] Generating Railway configuration files for language: {Language}", programmingLanguage);
                var railwayConfigFiles = GenerateRailwayConfigFiles(programmingLanguage);
                _logger.LogInformation("🔧 [RAILWAY] Generated {FileCount} Railway configuration files", railwayConfigFiles.Count);
                
                bool nixpacksFound = false;
                
                foreach (var file in railwayConfigFiles)
                {
                    _logger.LogInformation("🔧 [RAILWAY] Creating blob for Railway config: {FilePath} ({ContentLength} chars)", file.Key, file.Value.Length);
                    
                    // Log critical configuration files with more detail
                    if (file.Key == "nixpacks.toml")
                    {
                        nixpacksFound = true;
                        _logger.LogInformation("✅ [RAILWAY] CRITICAL FILE: nixpacks.toml - This configures Railway build process");
                    }
                    
                    var fileSha = await CreateBlobAsync(owner, repositoryName, file.Value, accessToken);
                    if (!string.IsNullOrEmpty(fileSha))
                    {
                        _logger.LogDebug("✅ [RAILWAY] Created blob for {FilePath}, SHA: {Sha}", file.Key, fileSha);
                        treeItems.Add(new { path = file.Key, mode = "100644", type = "blob", sha = fileSha });
                        fileContents[file.Key] = file.Value;
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [RAILWAY] Failed to create blob for {FilePath}", file.Key);
                    }
                }
                
                if (!nixpacksFound)
                {
                    _logger.LogError("❌ [RAILWAY] CRITICAL: nixpacks.toml was NOT generated! Railway build may fail!");
                }
                
                // Add a minimal root-level .csproj file so Railway's nixpacks can detect it as a .NET project
                // This allows Railway's auto-detection to succeed, then it will use the explicit phases in nixpacks.toml
                // This file is a placeholder - actual backend code is in backend/ folder
                if (programmingLanguage?.ToLowerInvariant() is "c#" or "csharp" or null)
                {
                    var rootCsprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <!-- This is a placeholder project file for Railway auto-detection -->
  <!-- Actual backend code is in backend/ folder -->
</Project>
";
                    var rootCsprojSha = await CreateBlobAsync(owner, repositoryName, rootCsprojContent, accessToken);
                    if (!string.IsNullOrEmpty(rootCsprojSha))
                    {
                        _logger.LogInformation("✅ [RAILWAY] Created root-level placeholder .csproj for Railway auto-detection");
                        treeItems.Add(new { path = "Railway.csproj", mode = "100644", type = "blob", sha = rootCsprojSha });
                        fileContents["Railway.csproj"] = rootCsprojContent;
                    }
                }
                
                _logger.LogInformation("📁 [STRUCTURE] Repository structure: backend/ and frontend/ folders at root");

                // Generate database script
                _logger.LogInformation("📄 [DB SCRIPT] Generating TestProjects.sql script for database setup");
                var dbScriptContent = GenerateTestProjectsSqlScript();
                _logger.LogInformation("📄 [DB SCRIPT] Generated SQL script: {ScriptLength} characters", dbScriptContent.Length);
                
                var dbScriptSha = await CreateBlobAsync(owner, repositoryName, dbScriptContent, accessToken);
                if (!string.IsNullOrEmpty(dbScriptSha))
                {
                    _logger.LogInformation("✅ [DB SCRIPT] Successfully created GitHub blob for backend/DatabaseScripts/TestProjects.sql, SHA: {Sha}", dbScriptSha);
                    treeItems.Add(new { path = "backend/DatabaseScripts/TestProjects.sql", mode = "100644", type = "blob", sha = dbScriptSha });
                    fileContents["backend/DatabaseScripts/TestProjects.sql"] = dbScriptContent;
                }
                else
                {
                    _logger.LogWarning("⚠️ [DB SCRIPT] Failed to create GitHub blob for database script");
                }
            }

            // Create a tree with all files
            var tree = await CreateTreeAsync(owner, repositoryName, branchInfo.TreeSha, 
                treeItems.ToArray(), 
                accessToken);

            if (string.IsNullOrEmpty(tree))
            {
                // Fallback: Use Contents API if Git Trees API fails (e.g., token permissions)
                _logger.LogWarning("Git Trees API failed, falling back to Contents API to create files individually");
                return await CreateFilesUsingContentsApiAsync(owner, repositoryName, fileContents, accessToken, programmingLanguage);
            }

            // Create a commit
            var commitMessage = !string.IsNullOrEmpty(programmingLanguage)
                ? $"Initial commit: Add project structure with {programmingLanguage} backend"
                : "Initial commit: Add project landing page";
            
            var commit = await CreateCommitAsync(owner, repositoryName, tree, branchInfo.CommitSha, 
                commitMessage, accessToken);

            if (string.IsNullOrEmpty(commit))
            {
                _logger.LogError("Failed to create commit");
                return false;
            }

            // Update the branch reference
            var updated = await UpdateReferenceAsync(owner, repositoryName, branchInfo.BranchName, commit, accessToken);

            if (updated)
            {
                _logger.LogInformation("✅ [GITHUB] Successfully created initial commit for {Owner}/{Repository} with {FileCount} files", 
                    owner, repositoryName, treeItems.Count);
                _logger.LogInformation("📋 [GITHUB] Files included in commit:");
                foreach (var item in treeItems)
                {
                    var path = item.GetType().GetProperty("path")?.GetValue(item)?.ToString() ?? "unknown";
                    _logger.LogInformation("   - {FilePath}", path);
                }
            }

            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating initial commit for {Owner}/{Repository}: {Message}", 
                owner, repositoryName, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Creates initial commit with frontend files only (at root level, no workflows)
    /// </summary>
    public async Task<bool> CreateFrontendOnlyCommitAsync(string owner, string repositoryName, string projectTitle, string accessToken, string? webApiUrl = null)
    {
        try
        {
            _logger.LogInformation("[FRONTEND] Creating frontend-only commit for repository {Owner}/{Repository}", owner, repositoryName);

            // Generate the GitHub Pages URL
            // Frontend repos use boardId directly (no prefix), so use repo name as-is
            var pagesUrl = GetGitHubPagesUrl(owner, repositoryName);

            // Generate the index.html content with project title
            var indexHtmlContent = GenerateDefaultIndexHtml(projectTitle, pagesUrl);

            // Get the default branch (usually 'main')
            var branchInfo = await GetDefaultBranchAsync(owner, repositoryName, accessToken);
            if (branchInfo == null)
            {
                _logger.LogError("[FRONTEND] Failed to get default branch information");
                return false;
            }

            // Build tree items - start with root files
            var treeItems = new List<object>();
            var fileContents = new Dictionary<string, string>();

            // Create README.md in root
            var rootReadmeContent = GenerateFrontendReadmeContent(projectTitle, pagesUrl, webApiUrl);
            var rootReadmeSha = await CreateBlobAsync(owner, repositoryName, rootReadmeContent, accessToken);
            if (!string.IsNullOrEmpty(rootReadmeSha))
            {
                treeItems.Add(new { path = "README.md", mode = "100644", type = "blob", sha = rootReadmeSha });
                fileContents["README.md"] = rootReadmeContent;
            }

            // Create frontend files at root (not in frontend/ folder)
            // index.html
            var indexHtmlSha = await CreateBlobAsync(owner, repositoryName, indexHtmlContent, accessToken);
            if (string.IsNullOrEmpty(indexHtmlSha))
            {
                _logger.LogError("[FRONTEND] Failed to create blob for index.html");
                return false;
            }
            treeItems.Add(new { path = "index.html", mode = "100644", type = "blob", sha = indexHtmlSha });
            fileContents["index.html"] = indexHtmlContent;

            // config.js
            var mentorApiBaseUrl = _configuration["ApiBaseUrl"];
            var configJsContent = GenerateConfigJs(webApiUrl, mentorApiBaseUrl);
            var configJsSha = await CreateBlobAsync(owner, repositoryName, configJsContent, accessToken);
            if (string.IsNullOrEmpty(configJsSha))
            {
                _logger.LogError("[FRONTEND] Failed to create blob for config.js");
                return false;
            }
            treeItems.Add(new { path = "config.js", mode = "100644", type = "blob", sha = configJsSha });
            fileContents["config.js"] = configJsContent;

            // style.css
            var styleCssContent = GenerateStyleCss();
            var styleCssSha = await CreateBlobAsync(owner, repositoryName, styleCssContent, accessToken);
            if (string.IsNullOrEmpty(styleCssSha))
            {
                _logger.LogError("[FRONTEND] Failed to create blob for style.css");
                return false;
            }
            treeItems.Add(new { path = "style.css", mode = "100644", type = "blob", sha = styleCssSha });
            fileContents["style.css"] = styleCssContent;

            // Create .gitignore
            var gitignoreContent = GenerateGitIgnore(null); // No programming language for frontend-only
            var gitignoreSha = await CreateBlobAsync(owner, repositoryName, gitignoreContent, accessToken);
            if (!string.IsNullOrEmpty(gitignoreSha))
            {
                treeItems.Add(new { path = ".gitignore", mode = "100644", type = "blob", sha = gitignoreSha });
                fileContents[".gitignore"] = gitignoreContent;
            }

            // Create GitHub Actions workflow for deploying frontend to GitHub Pages
            var frontendWorkflow = GenerateGitHubActionsWorkflow();
            var frontendWorkflowSha = await CreateBlobAsync(owner, repositoryName, frontendWorkflow, accessToken);
            if (!string.IsNullOrEmpty(frontendWorkflowSha))
            {
                treeItems.Add(new { path = ".github/workflows/deploy-frontend.yml", mode = "100644", type = "blob", sha = frontendWorkflowSha });
                fileContents[".github/workflows/deploy-frontend.yml"] = frontendWorkflow;
                _logger.LogInformation("[FRONTEND] ✅ Created workflow file: .github/workflows/deploy-frontend.yml");
            }
            else
            {
                _logger.LogWarning("[FRONTEND] ⚠️ Failed to create blob for workflow file");
            }

            // Create a tree with all files
            var tree = await CreateTreeAsync(owner, repositoryName, branchInfo.TreeSha, treeItems.ToArray(), accessToken);

            if (string.IsNullOrEmpty(tree))
            {
                _logger.LogWarning("[FRONTEND] Git Trees API failed, falling back to Contents API");
                return await CreateFilesUsingContentsApiAsync(owner, repositoryName, fileContents, accessToken, null);
            }

            // Create a commit
            var commitMessage = "Initial commit: Add frontend files";
            var commit = await CreateCommitAsync(owner, repositoryName, tree, branchInfo.CommitSha, commitMessage, accessToken);

            if (string.IsNullOrEmpty(commit))
            {
                _logger.LogError("[FRONTEND] Failed to create commit");
                return false;
            }

            // Update the branch reference
            var updated = await UpdateReferenceAsync(owner, repositoryName, branchInfo.BranchName, commit, accessToken);

            if (updated)
            {
                _logger.LogInformation("[FRONTEND] ✅ Successfully created frontend-only commit for {Owner}/{Repository} with {FileCount} files", 
                    owner, repositoryName, treeItems.Count);
            }

            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FRONTEND] Error creating frontend-only commit for repository {Owner}/{Repository}: {Message}", 
                owner, repositoryName, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Creates initial commit with backend files only (at root level, no workflows)
    /// </summary>
    public async Task<bool> CreateBackendOnlyCommitAsync(string owner, string repositoryName, string projectTitle, string accessToken, string programmingLanguage, string? databaseConnectionString = null, string? webApiUrl = null, string? swaggerUrl = null)
    {
        try
        {
            _logger.LogInformation("[BACKEND] Creating backend-only commit for repository {Owner}/{Repository} with language: {Language}", 
                owner, repositoryName, programmingLanguage);

            // Get the default branch (usually 'main')
            var branchInfo = await GetDefaultBranchAsync(owner, repositoryName, accessToken);
            if (branchInfo == null)
            {
                _logger.LogError("[BACKEND] Failed to get default branch information");
                return false;
            }

            // Build tree items
            var treeItems = new List<object>();
            var fileContents = new Dictionary<string, string>();

            // Create README.md in root
            var rootReadmeContent = GenerateBackendReadmeContent(projectTitle, databaseConnectionString, webApiUrl, swaggerUrl);
            var rootReadmeSha = await CreateBlobAsync(owner, repositoryName, rootReadmeContent, accessToken);
            if (!string.IsNullOrEmpty(rootReadmeSha))
            {
                treeItems.Add(new { path = "README.md", mode = "100644", type = "blob", sha = rootReadmeSha });
                fileContents["README.md"] = rootReadmeContent;
            }

            // Generate backend files at root level (no "backend/" prefix)
            _logger.LogInformation("[BACKEND] Generating backend files at root for language: {Language}", programmingLanguage);
            var backendFiles = GenerateBackendFilesAtRoot(programmingLanguage, webApiUrl);
            _logger.LogInformation("[BACKEND] Generated {FileCount} backend files at root", backendFiles.Count);

            foreach (var file in backendFiles)
            {
                _logger.LogDebug("[BACKEND] Creating blob for: {FilePath} ({ContentLength} chars)", file.Key, file.Value.Length);
                var fileSha = await CreateBlobAsync(owner, repositoryName, file.Value, accessToken);
                if (!string.IsNullOrEmpty(fileSha))
                {
                    _logger.LogDebug("[BACKEND] ✅ Created blob for {FilePath}, SHA: {Sha}", file.Key, fileSha);
                    treeItems.Add(new { path = file.Key, mode = "100644", type = "blob", sha = fileSha });
                    fileContents[file.Key] = file.Value;
                }
                else
                {
                    _logger.LogWarning("[BACKEND] ⚠️ Failed to create blob for {FilePath}", file.Key);
                }
            }

            // Create GitHub Actions workflow for deploying backend to Railway (root-level files)
            var backendWorkflow = GenerateRailwayDeploymentWorkflowAtRoot(programmingLanguage);
            var backendWorkflowSha = await CreateBlobAsync(owner, repositoryName, backendWorkflow, accessToken);
            if (!string.IsNullOrEmpty(backendWorkflowSha))
            {
                treeItems.Add(new { path = ".github/workflows/deploy-backend.yml", mode = "100644", type = "blob", sha = backendWorkflowSha });
                fileContents[".github/workflows/deploy-backend.yml"] = backendWorkflow;
                _logger.LogInformation("[BACKEND] ✅ Created workflow file: .github/workflows/deploy-backend.yml");
            }

            // Create .gitignore
            var gitignoreContent = GenerateGitIgnore(programmingLanguage);
            var gitignoreSha = await CreateBlobAsync(owner, repositoryName, gitignoreContent, accessToken);
            if (!string.IsNullOrEmpty(gitignoreSha))
            {
                treeItems.Add(new { path = ".gitignore", mode = "100644", type = "blob", sha = gitignoreSha });
                fileContents[".gitignore"] = gitignoreContent;
            }

            // Create a tree with all files
            var tree = await CreateTreeAsync(owner, repositoryName, branchInfo.TreeSha, treeItems.ToArray(), accessToken);

            if (string.IsNullOrEmpty(tree))
            {
                _logger.LogWarning("[BACKEND] Git Trees API failed, falling back to Contents API");
                return await CreateFilesUsingContentsApiAsync(owner, repositoryName, fileContents, accessToken, programmingLanguage);
            }

            // Create a commit
            var commitMessage = $"Initial commit: Add {programmingLanguage} backend files";
            var commit = await CreateCommitAsync(owner, repositoryName, tree, branchInfo.CommitSha, commitMessage, accessToken);

            if (string.IsNullOrEmpty(commit))
            {
                _logger.LogError("[BACKEND] Failed to create commit");
                return false;
            }

            // Update the branch reference
            var updated = await UpdateReferenceAsync(owner, repositoryName, branchInfo.BranchName, commit, accessToken);

            if (updated)
            {
                _logger.LogInformation("[BACKEND] ✅ Successfully created backend-only commit for {Owner}/{Repository} with {FileCount} files", 
                    owner, repositoryName, treeItems.Count);
            }

            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BACKEND] Error creating backend-only commit for repository {Owner}/{Repository}: {Message}", 
                owner, repositoryName, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Enables GitHub Pages for a repository
    /// Note: We check if the workflow file exists first. If it exists, we try to enable Pages
    /// without source (workflow-based). If workflow doesn't exist, we enable with source (legacy mode).
    /// </summary>
    public async Task<bool> EnableGitHubPagesAsync(string owner, string repositoryName, string accessToken)
    {
        try
        {
            _logger.LogInformation("Enabling GitHub Pages for repository {Owner}/{Repository}", owner, repositoryName);

            // Check if workflow file exists - if it does, we can try to enable Pages for workflow-based deployment
            bool workflowExists = false;
            try
            {
                var workflowCheckRequest = new HttpRequestMessage(HttpMethod.Get, 
                    $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/contents/.github/workflows/deploy-frontend.yml");
                workflowCheckRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                workflowCheckRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
                
                var workflowCheckResponse = await _httpClient.SendAsync(workflowCheckRequest);
                workflowExists = workflowCheckResponse.IsSuccessStatusCode;
                
                if (workflowExists)
                {
                    _logger.LogInformation("Workflow file exists for {Owner}/{Repository}. Attempting to enable Pages for workflow-based deployment.", 
                        owner, repositoryName);
                }
                else
                {
                    _logger.LogInformation("Workflow file does not exist yet for {Owner}/{Repository}. Pages will be enabled in legacy mode.", 
                        owner, repositoryName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not check for workflow file existence for {Owner}/{Repository}. Proceeding with legacy mode.", 
                    owner, repositoryName);
            }

            // GitHub API requires a valid payload with source branch and path for initial setup
            // However, if workflow exists, we can try enabling without source first (though API may still require it)
            object pagesPayload;
            
            if (workflowExists)
            {
                // Try to enable Pages without source - GitHub should detect workflow automatically
                // Note: GitHub API may still require source, so we'll fall back if this fails
                pagesPayload = new { };
            }
            else
            {
                // No workflow yet - enable with source (legacy mode)
                pagesPayload = new
            {
                source = new
                {
                    branch = "main",
                    path = "/"
                }
            };
            }

            var jsonContent = JsonSerializer.Serialize(pagesPayload);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/pages");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            
            // If we tried without source (workflow exists) and got 422 (Unprocessable Entity), fall back to enabling with source
            if (workflowExists && response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
            {
                _logger.LogInformation("Enabling Pages without source failed (422). Falling back to legacy mode with source branch/path.");
                pagesPayload = new
                {
                    source = new
                    {
                        branch = "main",
                        path = "/"
                    }
                };
                
                jsonContent = JsonSerializer.Serialize(pagesPayload);
                content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                
                // Create a NEW request message - cannot reuse the same HttpRequestMessage
                var fallbackRequest = new HttpRequestMessage(HttpMethod.Post, 
                    $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/pages");
                fallbackRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                fallbackRequest.Content = content;
                response = await _httpClient.SendAsync(fallbackRequest);
            }

            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // 201 Created means success, 409 Conflict means pages already enabled
                _logger.LogInformation("GitHub Pages enabled successfully for {Owner}/{Repository}. " +
                    "If a workflow file exists, GitHub will use workflow-based deployment.", 
                    owner, repositoryName);
                
                // If we got 409, Pages is already enabled - try to update it to workflow-based deployment
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogInformation("GitHub Pages was already enabled (409 Conflict) for {Owner}/{Repository}. " +
                        "Attempting to update to workflow-based deployment if workflow file exists.", 
                        owner, repositoryName);
                    
                    // Try to update Pages configuration to remove source (this allows workflow-based deployment)
                    // GitHub will automatically detect and use the workflow if source is not set
                    try
                    {
                        var updatePayload = new
                        {
                            // Empty payload - GitHub will detect workflow and use workflow-based deployment
                            // Note: GitHub API may not support this, but we try
                        };
                        
                        var updateJson = JsonSerializer.Serialize(updatePayload);
                        var updateContent = new StringContent(updateJson, System.Text.Encoding.UTF8, "application/json");
                        
                        var updateRequest = new HttpRequestMessage(HttpMethod.Put, 
                            $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/pages");
                        updateRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                        updateRequest.Content = updateContent;
                        
                        var updateResponse = await _httpClient.SendAsync(updateRequest);
                        if (updateResponse.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Successfully updated GitHub Pages configuration for {Owner}/{Repository} to workflow-based deployment.", 
                                owner, repositoryName);
                        }
                        else
                        {
                            _logger.LogWarning("Could not update GitHub Pages configuration (Status: {StatusCode}). " +
                                "Pages may remain in legacy mode. GitHub should auto-detect workflow on next deployment.", 
                                updateResponse.StatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update GitHub Pages configuration for {Owner}/{Repository}. " +
                            "Pages may remain in legacy mode, but GitHub should auto-detect workflow on next deployment.", 
                            owner, repositoryName);
                    }
                }
                
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to enable GitHub Pages. Status: {StatusCode}, Error: {Error}", 
                response.StatusCode, errorContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling GitHub Pages for {Owner}/{Repository}: {Message}", 
                owner, repositoryName, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Checks if GitHub Pages is enabled for a repository
    /// </summary>
    public async Task<bool> CheckGitHubPagesStatusAsync(string owner, string repositoryName, string accessToken)
    {
        try
        {
            _logger.LogInformation("[GithubPages] Checking GitHub Pages status for {Owner}/{Repository}", owner, repositoryName);
            
            var request = new HttpRequestMessage(HttpMethod.Get, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/pages");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[GithubPages] Pages status check API Response Status: {StatusCode}", response.StatusCode);
            _logger.LogInformation("[GithubPages] Pages status check API Response Content: {ResponseContent}", responseContent);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var pagesInfo = JsonSerializer.Deserialize<JsonElement>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    var status = pagesInfo.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "unknown";
                    var htmlUrl = pagesInfo.TryGetProperty("html_url", out var htmlUrlProp) ? htmlUrlProp.GetString() : null;
                    var buildType = pagesInfo.TryGetProperty("build_type", out var buildTypeProp) ? buildTypeProp.GetString() : null;
                    var source = pagesInfo.TryGetProperty("source", out var sourceProp) ? sourceProp.ToString() : null;
                    var cname = pagesInfo.TryGetProperty("cname", out var cnameProp) ? cnameProp.GetString() : null;
                    var httpsEnforced = pagesInfo.TryGetProperty("https_enforced", out var httpsProp) ? httpsProp.GetBoolean() : (bool?)null;
                    
                    _logger.LogInformation("[GithubPages] ✅ Pages is enabled - Status: {Status}, Build Type: {BuildType}, URL: {Url}", 
                        status, buildType ?? "unknown", htmlUrl ?? "N/A");
                    _logger.LogInformation("[GithubPages] Pages Details - Source: {Source}, CNAME: {Cname}, HTTPS Enforced: {Https}", 
                        source ?? "N/A", cname ?? "N/A", httpsEnforced?.ToString() ?? "N/A");
                    
                    if (status == "building" || status == "queued")
                    {
                        _logger.LogInformation("[GithubPages] Pages is currently {Status} - deployment in progress", status);
                    }
                    else if (status == "built")
                    {
                        _logger.LogInformation("[GithubPages] ✅ Pages build completed - site should be live at {Url}", htmlUrl ?? "N/A");
                    }
                    
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[GithubPages] Failed to parse Pages status response, but API returned success");
                    _logger.LogInformation("[GithubPages] Raw response: {ResponseContent}", responseContent);
                    return true;
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("[GithubPages] ❌ Pages is NOT enabled (404 - Not Found)");
                _logger.LogWarning("[GithubPages] This means GitHub Pages has not been enabled for this repository yet");
                _logger.LogInformation("[GithubPages] Pages can be enabled manually in repository Settings > Pages, or via API with 'pages:write' scope");
                return false;
            }
            else
            {
                _logger.LogWarning("[GithubPages] Failed to check Pages status: {StatusCode}", response.StatusCode);
                _logger.LogWarning("[GithubPages] Response content: {ResponseContent}", responseContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GithubPages] Error checking GitHub Pages status: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Triggers a GitHub Actions workflow via workflow_dispatch event
    /// </summary>
    public async Task<bool> TriggerWorkflowDispatchAsync(string owner, string repositoryName, string workflowFileName, string accessToken)
    {
        try
        {
            var workflowId = workflowFileName.EndsWith(".yml") || workflowFileName.EndsWith(".yaml") 
                ? workflowFileName 
                : $"{workflowFileName}.yml";
            
            var dispatchPayload = new
            {
                @ref = "main"
            };

            var jsonContent = JsonSerializer.Serialize(dispatchPayload);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            // First, try to get the workflow ID by listing workflows
            // This is more reliable than using the filename
            string? actualWorkflowId = await GetWorkflowIdByNameAsync(owner, repositoryName, workflowId, accessToken);
            
            if (string.IsNullOrEmpty(actualWorkflowId))
            {
                // Fallback: try with filename directly
                actualWorkflowId = workflowId;
            }

            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/actions/workflows/{actualWorkflowId}/dispatches");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = httpContent;

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[WORKFLOW] ✅ Successfully triggered workflow {WorkflowId} for {Owner}/{Repo}", actualWorkflowId, owner, repositoryName);
                return true;
            }
            
            _logger.LogWarning("[WORKFLOW] ❌ Failed to trigger workflow {WorkflowId}: {StatusCode} - {ResponseContent}", 
                actualWorkflowId, response.StatusCode, responseContent);
            
            // If filename didn't work, try with full path
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound && !workflowId.StartsWith(".github/"))
            {
                var fullPathWorkflowId = $".github/workflows/{workflowId}";
                _logger.LogInformation("[WORKFLOW] Retrying with full path: {FullPath}", fullPathWorkflowId);
                var fullPathEndpoint = $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/actions/workflows/{fullPathWorkflowId}/dispatches";
                var retryRequest = new HttpRequestMessage(HttpMethod.Post, fullPathEndpoint);
                retryRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                retryRequest.Content = httpContent;
                response = await _httpClient.SendAsync(retryRequest);
                responseContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[WORKFLOW] ✅ Successfully triggered workflow {WorkflowId} for {Owner}/{Repo}", fullPathWorkflowId, owner, repositoryName);
                    return true;
                }
                
                _logger.LogWarning("[WORKFLOW] ❌ Failed to trigger workflow {WorkflowId}: {StatusCode} - {ResponseContent}", 
                    fullPathWorkflowId, response.StatusCode, responseContent);
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WORKFLOW] Error triggering workflow: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets the workflow ID by listing workflows and finding the one that matches the filename
    /// </summary>
    private async Task<string?> GetWorkflowIdByNameAsync(string owner, string repo, string workflowFileName, string accessToken)
    {
        try
        {
            var url = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/actions/workflows";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[WORKFLOW] Failed to list workflows: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var workflowsData = JsonSerializer.Deserialize<JsonElement>(content);
            
            _logger.LogInformation("[WORKFLOW] Listing workflows for {Owner}/{Repo}", owner, repo);
            
            if (workflowsData.TryGetProperty("workflows", out var workflows))
            {
                var workflowCount = workflows.GetArrayLength();
                _logger.LogInformation("[WORKFLOW] Found {Count} workflows in repository", workflowCount);
                
                var foundWorkflows = new List<string>();
                foreach (var workflow in workflows.EnumerateArray())
                {
                    if (workflow.TryGetProperty("path", out var path) && workflow.TryGetProperty("id", out var id))
                    {
                        var pathValue = path.GetString();
                        var workflowId = id.GetInt64().ToString();
                        foundWorkflows.Add($"ID: {workflowId}, Path: {pathValue}");
                        
                        // Match if the path ends with the workflow filename
                        if (!string.IsNullOrEmpty(pathValue) && pathValue.EndsWith(workflowFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("[WORKFLOW] ✅ Found workflow ID {WorkflowId} for {WorkflowFile}", workflowId, workflowFileName);
                            return workflowId;
                        }
                    }
                }
                
                _logger.LogWarning("[WORKFLOW] Workflow {WorkflowFile} not found in workflows list", workflowFileName);
                _logger.LogInformation("[WORKFLOW] Available workflows: {Workflows}", string.Join("; ", foundWorkflows));
            }
            else
            {
                _logger.LogWarning("[WORKFLOW] No 'workflows' property in response");
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[WORKFLOW] Error getting workflow ID: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Creates or updates a GitHub Actions secret for a repository
    /// GitHub Secrets API requires encryption using the repository's public key (LibSodium sealed box)
    /// </summary>
    public async Task<bool> CreateOrUpdateRepositorySecretAsync(string owner, string repositoryName, string secretName, string secretValue, string accessToken)
    {
        try
        {
            _logger.LogInformation("[GITHUB] Creating/updating secret {SecretName} for {Owner}/{Repo}", secretName, owner, repositoryName);
            
            // Step 1: Get the repository's public key
            var publicKeyRequest = new HttpRequestMessage(HttpMethod.Get, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/actions/secrets/public-key");
            publicKeyRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            publicKeyRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            
            var publicKeyResponse = await _httpClient.SendAsync(publicKeyRequest);
            var publicKeyResponseContent = await publicKeyResponse.Content.ReadAsStringAsync();
            
            if (!publicKeyResponse.IsSuccessStatusCode)
            {
                _logger.LogError("[GITHUB] Failed to get public key for repository: {StatusCode} - {Error}", 
                    publicKeyResponse.StatusCode, publicKeyResponseContent);
                return false;
            }
            
            var publicKeyJson = JsonSerializer.Deserialize<JsonElement>(publicKeyResponseContent);
            var publicKeyBase64 = publicKeyJson.GetProperty("key").GetString();
            var keyId = publicKeyJson.GetProperty("key_id").GetString();
            
            if (string.IsNullOrEmpty(publicKeyBase64) || string.IsNullOrEmpty(keyId))
            {
                _logger.LogError("[GITHUB] Failed to parse public key response");
                return false;
            }
            
            // Step 2: Encrypt the secret using LibSodium sealed box (PublicKeyBox.Seal)
            // GitHub uses X25519 public key encryption with sealed box
            var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
            var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(secretValue);
            
            // Use Sodium.Core for LibSodium sealed box encryption
            // GitHub expects X25519 public key (32 bytes) for sealed box
            var encryptedBytes = Sodium.SealedPublicKeyBox.Create(plaintextBytes, publicKeyBytes);
            var encryptedSecret = Convert.ToBase64String(encryptedBytes);
            
            // Step 3: Create/update the secret
            var secretPayload = new
            {
                encrypted_value = encryptedSecret,
                key_id = keyId
            };
            
            var jsonContent = JsonSerializer.Serialize(secretPayload);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            
            var secretRequest = new HttpRequestMessage(HttpMethod.Put, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/actions/secrets/{secretName}");
            secretRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            secretRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            secretRequest.Content = content;
            
            var secretResponse = await _httpClient.SendAsync(secretRequest);
            var secretResponseContent = await secretResponse.Content.ReadAsStringAsync();
            
            if (secretResponse.IsSuccessStatusCode || secretResponse.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                _logger.LogInformation("[GITHUB] ✅ Successfully created/updated secret {SecretName} for {Owner}/{Repo}", 
                    secretName, owner, repositoryName);
                return true;
            }
            else
            {
                _logger.LogError("[GITHUB] Failed to create/update secret {SecretName}: {StatusCode} - {Error}", 
                    secretName, secretResponse.StatusCode, secretResponseContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GITHUB] Error creating/updating secret {SecretName}: {Message}", secretName, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets the GitHub Pages URL for a repository
    /// </summary>
    public string GetGitHubPagesUrl(string repositoryName)
    {
        // Note: This method is called before we know the actual owner
        // The actual owner will be determined when creating the repository
        // For now, return a placeholder - the actual URL will be set in CreateRepositoryAsync
        return $"https://skill-in-projects.github.io/{repositoryName}/";
    }
    
    public string GetGitHubPagesUrl(string owner, string repositoryName)
    {
        return $"https://{owner}.github.io/{repositoryName}/";
    }

    /// <summary>
    /// Gets the date and time of the last commit by a specific user in a repository
    /// </summary>
    public async Task<DateTime?> GetLastCommitDateByUserAsync(string owner, string repo, string username, string? accessToken = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(username))
            {
                _logger.LogWarning("GetLastCommitDateByUserAsync: Invalid parameters - owner: {Owner}, repo: {Repo}, username: {Username}", 
                    owner ?? "null", repo ?? "null", username ?? "null");
                return null;
            }

            var url = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/commits?author={username}&per_page=1";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            _logger.LogInformation("Fetching last commit for user {Username} in repo {Owner}/{Repo}", username, owner, repo);
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get commits for user {Username} in repo {Owner}/{Repo}. Status: {StatusCode}", 
                    username, owner, repo, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var commits = JsonSerializer.Deserialize<List<JsonElement>>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            if (commits == null || !commits.Any())
            {
                _logger.LogInformation("No commits found for user {Username} in repo {Owner}/{Repo}", username, owner, repo);
                return null;
            }

            var firstCommit = commits[0];
            if (firstCommit.TryGetProperty("commit", out var commitProp))
            {
                if (commitProp.TryGetProperty("author", out var authorProp))
                {
                    if (authorProp.TryGetProperty("date", out var dateProp))
                    {
                        var commitDateStr = dateProp.GetString();
                        if (DateTime.TryParse(commitDateStr, out var commitDate))
                        {
                            _logger.LogInformation("Found last commit for user {Username} in repo {Owner}/{Repo}: {CommitDate}", 
                                username, owner, repo, commitDate);
                            return commitDate;
                        }
                    }
                }
            }
            
            _logger.LogWarning("Could not parse commit date from response for user {Username} in repo {Owner}/{Repo}", username, owner, repo);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last commit date for user {Username} in repo {Owner}/{Repo}: {Message}", 
                username, owner, repo, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Gets the date, time, and message of the last commit by a specific user in a repository
    /// </summary>
    public async Task<GitHubCommitInfo?> GetLastCommitInfoByUserAsync(string owner, string repo, string username, string? accessToken = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(username))
            {
                _logger.LogWarning("GetLastCommitInfoByUserAsync: Invalid parameters - owner: {Owner}, repo: {Repo}, username: {Username}", 
                    owner ?? "null", repo ?? "null", username ?? "null");
                return null;
            }

            var url = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/commits?author={username}&per_page=1";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            _logger.LogInformation("Fetching last commit info for user {Username} in repo {Owner}/{Repo}", username, owner, repo);
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get commits for user {Username} in repo {Owner}/{Repo}. Status: {StatusCode}", 
                    username, owner, repo, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var commits = JsonSerializer.Deserialize<List<JsonElement>>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            if (commits == null || !commits.Any())
            {
                _logger.LogInformation("No commits found for user {Username} in repo {Owner}/{Repo}", username, owner, repo);
                return null;
            }

            var firstCommit = commits[0];
            DateTime? commitDate = null;
            string commitMessage = string.Empty;

            if (firstCommit.TryGetProperty("commit", out var commitProp))
            {
                // Get commit message
                if (commitProp.TryGetProperty("message", out var messageProp))
                {
                    commitMessage = messageProp.GetString() ?? string.Empty;
                }

                // Get commit date
                if (commitProp.TryGetProperty("author", out var authorProp))
                {
                    if (authorProp.TryGetProperty("date", out var dateProp))
                    {
                        var commitDateStr = dateProp.GetString();
                        if (DateTime.TryParse(commitDateStr, out var parsedDate))
                        {
                            commitDate = parsedDate;
                        }
                    }
                }
            }

            if (commitDate.HasValue)
            {
                _logger.LogInformation("Found last commit for user {Username} in repo {Owner}/{Repo}: Date={CommitDate}, Message={CommitMessage}", 
                    username, owner, repo, commitDate.Value, commitMessage);
                return new GitHubCommitInfo
                {
                    CommitDate = commitDate.Value,
                    CommitMessage = commitMessage
                };
            }
            
            _logger.LogWarning("Could not parse commit info from response for user {Username} in repo {Owner}/{Repo}", username, owner, repo);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last commit info for user {Username} in repo {Owner}/{Repo}: {Message}", 
                username, owner, repo, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Gets recent commits by a specific user in a repository
    /// </summary>
    public async Task<List<GitHubCommit>> GetRecentCommitsAsync(string owner, string repo, string username, int count = 10, string? accessToken = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(username))
            {
                _logger.LogWarning("GetRecentCommitsAsync: Invalid parameters");
                return new List<GitHubCommit>();
            }

            var url = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/commits?author={username}&per_page={count}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            _logger.LogInformation("Fetching recent commits for user {Username} in repo {Owner}/{Repo}", username, owner, repo);
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get commits for user {Username} in repo {Owner}/{Repo}. Status: {StatusCode}", 
                    username, owner, repo, response.StatusCode);
                return new List<GitHubCommit>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var commits = JsonSerializer.Deserialize<List<JsonElement>>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            if (commits == null || !commits.Any())
            {
                _logger.LogInformation("No commits found for user {Username} in repo {Owner}/{Repo}", username, owner, repo);
                return new List<GitHubCommit>();
            }

            var result = new List<GitHubCommit>();
            foreach (var commit in commits)
            {
                var sha = commit.TryGetProperty("sha", out var shaProp) ? shaProp.GetString() ?? "" : "";
                var htmlUrl = commit.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                
                string message = "";
                DateTime commitDate = DateTime.UtcNow;
                string author = "";

                if (commit.TryGetProperty("commit", out var commitProp))
                {
                    message = commitProp.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "";
                    
                    if (commitProp.TryGetProperty("author", out var authorProp))
                    {
                        author = authorProp.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                        if (authorProp.TryGetProperty("date", out var dateProp))
                        {
                            var dateStr = dateProp.GetString();
                            if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var parsedDate))
                            {
                                commitDate = parsedDate;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(sha))
                {
                    result.Add(new GitHubCommit
                    {
                        Sha = sha,
                        Message = message,
                        CommitDate = commitDate,
                        Author = author,
                        Url = htmlUrl
                    });
                }
            }

            _logger.LogInformation("Found {Count} commits for user {Username} in repo {Owner}/{Repo}", result.Count, username, owner, repo);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent commits for user {Username} in repo {Owner}/{Repo}", username, owner, repo);
            return new List<GitHubCommit>();
        }
    }

    /// <summary>
    /// Gets file changes for a specific commit
    /// </summary>
    public async Task<List<GitHubFileChange>> GetFileChangesAsync(string owner, string repo, string commitSha, string? accessToken = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(commitSha))
            {
                _logger.LogWarning("GetFileChangesAsync: Invalid parameters");
                return new List<GitHubFileChange>();
            }

            var url = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/commits/{commitSha}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            _logger.LogInformation("Fetching file changes for commit {CommitSha} in repo {Owner}/{Repo}", commitSha, owner, repo);
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get commit {CommitSha} in repo {Owner}/{Repo}. Status: {StatusCode}", 
                    commitSha, owner, repo, response.StatusCode);
                return new List<GitHubFileChange>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var commitData = JsonSerializer.Deserialize<JsonElement>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            // Check if commitData is null or invalid
            if (commitData.ValueKind == JsonValueKind.Null || commitData.ValueKind == JsonValueKind.Undefined)
            {
                return new List<GitHubFileChange>();
            }

            var fileChanges = new List<GitHubFileChange>();

            if (commitData.TryGetProperty("files", out var filesProp) && filesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var file in filesProp.EnumerateArray())
                {
                    var filePath = file.TryGetProperty("filename", out var filenameProp) ? filenameProp.GetString() ?? "" : "";
                    var status = file.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "" : "";
                    var additions = file.TryGetProperty("additions", out var addProp) ? addProp.GetInt32() : 0;
                    var deletions = file.TryGetProperty("deletions", out var delProp) ? delProp.GetInt32() : 0;
                    var changes = file.TryGetProperty("changes", out var changesProp) ? changesProp.GetInt32() : (additions + deletions);
                    
                    // Check if patch property exists
                    bool hasPatchProperty = file.TryGetProperty("patch", out var patchProp);
                    var patch = hasPatchProperty ? patchProp.GetString() : null;
                    
                    // Log patch status for debugging
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        if (hasPatchProperty)
                        {
                            if (!string.IsNullOrEmpty(patch))
                            {
                                _logger.LogInformation("File {FilePath} in commit {CommitSha} has patch content ({Length} chars). Status: {Status}, Additions: {Additions}, Deletions: {Deletions}", 
                                    filePath, commitSha, patch.Length, status, additions, deletions);
                            }
                            else
                            {
                                _logger.LogWarning("File {FilePath} in commit {CommitSha} has patch property but it's null/empty. Status: {Status}, Additions: {Additions}, Deletions: {Deletions}", 
                                    filePath, commitSha, status, additions, deletions);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("File {FilePath} in commit {CommitSha} has NO patch property in GitHub API response. Status: {Status}, Additions: {Additions}, Deletions: {Deletions}", 
                                filePath, commitSha, status, additions, deletions);
                        }
                        
                        fileChanges.Add(new GitHubFileChange
                        {
                            FilePath = filePath,
                            Status = status,
                            Additions = additions,
                            Deletions = deletions,
                            Changes = changes,
                            Patch = patch
                        });
                    }
                }
            }

            _logger.LogInformation("Found {Count} file changes for commit {CommitSha}", fileChanges.Count, commitSha);
            return fileChanges;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file changes for commit {CommitSha} in repo {Owner}/{Repo}", commitSha, owner, repo);
            return new List<GitHubFileChange>();
        }
    }

    /// <summary>
    /// Gets complete diff information for a commit including file changes
    /// </summary>
    public async Task<GitHubCommitDiff?> GetCommitDiffAsync(string owner, string repo, string commitSha, string? accessToken = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(commitSha))
            {
                _logger.LogWarning("GetCommitDiffAsync: Invalid parameters");
                return null;
            }

            var url = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/commits/{commitSha}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            _logger.LogInformation("Fetching commit diff for {CommitSha} in repo {Owner}/{Repo}", commitSha, owner, repo);
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get commit diff for {CommitSha} in repo {Owner}/{Repo}. Status: {StatusCode}", 
                    commitSha, owner, repo, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var commitData = JsonSerializer.Deserialize<JsonElement>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            // Check if commitData is null or invalid
            if (commitData.ValueKind == JsonValueKind.Null || commitData.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            var fileChanges = await GetFileChangesAsync(owner, repo, commitSha, accessToken);

            string message = "";
            DateTime commitDate = DateTime.UtcNow;
            string author = "";

            if (commitData.TryGetProperty("commit", out var commitProp))
            {
                message = commitProp.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "";
                
                if (commitProp.TryGetProperty("author", out var authorProp))
                {
                    author = authorProp.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                    if (authorProp.TryGetProperty("date", out var dateProp))
                    {
                        var dateStr = dateProp.GetString();
                        if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var parsedDate))
                        {
                            commitDate = parsedDate;
                        }
                    }
                }
            }

            var stats = commitData.TryGetProperty("stats", out var statsProp) ? statsProp : default(JsonElement);
            var totalAdditions = stats.ValueKind != JsonValueKind.Undefined && stats.TryGetProperty("additions", out var addProp) 
                ? addProp.GetInt32() 
                : fileChanges.Sum(f => f.Additions);
            var totalDeletions = stats.ValueKind != JsonValueKind.Undefined && stats.TryGetProperty("deletions", out var delProp) 
                ? delProp.GetInt32() 
                : fileChanges.Sum(f => f.Deletions);

            return new GitHubCommitDiff
            {
                CommitSha = commitSha,
                CommitMessage = message,
                CommitDate = commitDate,
                Author = author,
                FileChanges = fileChanges,
                TotalAdditions = totalAdditions,
                TotalDeletions = totalDeletions,
                TotalFilesChanged = fileChanges.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting commit diff for {CommitSha} in repo {Owner}/{Repo}", commitSha, owner, repo);
            return null;
        }
    }

    /// <summary>
    /// Checks if user has recent commits within specified hours
    /// </summary>
    public async Task<bool> HasRecentCommitsAsync(string owner, string repo, string username, int hours = 24, string? accessToken = null)
    {
        try
        {
            var commits = await GetRecentCommitsAsync(owner, repo, username, 1, accessToken);
            
            if (!commits.Any())
            {
                return false;
            }

            var mostRecentCommit = commits.OrderByDescending(c => c.CommitDate).First();
            var hoursSinceCommit = (DateTime.UtcNow - mostRecentCommit.CommitDate).TotalHours;
            
            _logger.LogInformation("Most recent commit for {Username} was {Hours} hours ago", username, hoursSinceCommit);
            return hoursSinceCommit <= hours;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking recent commits for user {Username} in repo {Owner}/{Repo}", username, owner, repo);
            return false;
        }
    }

    /// <summary>
    /// Gets the content of a file from a GitHub repository
    /// </summary>
    public async Task<string?> GetFileContentAsync(string owner, string repo, string filePath, string? accessToken = null, string? branch = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(filePath))
            {
                _logger.LogWarning("GetFileContentAsync: Invalid parameters");
                return null;
            }

            var token = accessToken ?? _configuration["GitHub:AccessToken"];
            var branchName = branch ?? "main";

            var url = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/contents/{filePath}?ref={branchName}";
            
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                // Try 'master' branch if 'main' fails
                if (branchName == "main")
                {
                    url = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/contents/{filePath}?ref=master";
                    // Create a new request for the retry (can't reuse HttpRequestMessage)
                    using var retryRequest = new HttpRequestMessage(HttpMethod.Get, url);
                    if (!string.IsNullOrEmpty(token))
                    {
                        retryRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    }
                    response = await _httpClient.SendAsync(retryRequest);
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get file content for {FilePath} in {Owner}/{Repo}: {StatusCode}", filePath, owner, repo, response.StatusCode);
                    return null;
                }
            }

            var content = await response.Content.ReadAsStringAsync();
            var fileData = JsonSerializer.Deserialize<JsonElement>(content);

            // GitHub API returns base64 encoded content
            if (fileData.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
            {
                var base64Content = contentProp.GetString();
                if (!string.IsNullOrEmpty(base64Content))
                {
                    // Remove newlines from base64 string and decode
                    var cleanedBase64 = base64Content.Replace("\n", "").Trim();
                    var bytes = Convert.FromBase64String(cleanedBase64);
                    return System.Text.Encoding.UTF8.GetString(bytes);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file content for {FilePath} in {Owner}/{Repo}", filePath, owner, repo);
            return null;
        }
    }

    /// <summary>
    /// Updates a file in a GitHub repository
    /// </summary>
    public async Task<bool> UpdateFileAsync(string owner, string repo, string filePath, string content, string message, string? accessToken = null, string? branch = null)
    {
        try
        {
            _logger.LogInformation("📝 [GITHUB] Updating file {FilePath} in {Owner}/{Repo}", filePath, owner, repo);
            
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(filePath))
            {
                _logger.LogWarning("📝 [GITHUB] UpdateFileAsync: Invalid parameters");
                return false;
            }

            var token = accessToken ?? _configuration["GitHub:AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("📝 [GITHUB] UpdateFileAsync: No access token available");
                return false;
            }

            var branchName = branch ?? "main";

            // First, get the current file to get its SHA (required for update)
            var currentFileUrl = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/contents/{filePath}?ref={branchName}";
            string? fileSha = null;
            
            using var getRequest = new HttpRequestMessage(HttpMethod.Get, currentFileUrl);
            getRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            var getResponse = await _httpClient.SendAsync(getRequest);
            if (getResponse.IsSuccessStatusCode)
            {
                var getContent = await getResponse.Content.ReadAsStringAsync();
                var fileData = JsonSerializer.Deserialize<JsonElement>(getContent);
                if (fileData.TryGetProperty("sha", out var shaProp))
                {
                    fileSha = shaProp.GetString();
                    _logger.LogDebug("📝 [GITHUB] Found existing file SHA: {Sha}", fileSha);
                }
            }
            else
            {
                _logger.LogWarning("📝 [GITHUB] File {FilePath} not found, will create new file", filePath);
            }

            // Encode content to base64
            var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
            var base64Content = Convert.ToBase64String(contentBytes);

            // Prepare update payload
            var updatePayload = new
            {
                message = message,
                content = base64Content,
                branch = branchName,
                sha = fileSha // Required for update, null for new file
            };

            var jsonContent = JsonSerializer.Serialize(updatePayload);
            _logger.LogDebug("📝 [GITHUB] Update payload: {Payload}", jsonContent.Replace(base64Content, "[BASE64_CONTENT]"));

            var updateContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var updateRequest = new HttpRequestMessage(HttpMethod.Put, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/contents/{filePath}");
            updateRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            updateRequest.Content = updateContent;

            var updateResponse = await _httpClient.SendAsync(updateRequest);
            var updateResponseContent = await updateResponse.Content.ReadAsStringAsync();

            if (updateResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ [GITHUB] Successfully updated file {FilePath} in {Owner}/{Repo}", filePath, owner, repo);
                return true;
            }
            else
            {
                _logger.LogError("❌ [GITHUB] Failed to update file {FilePath}: {StatusCode} - {Error}", 
                    filePath, updateResponse.StatusCode, updateResponseContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [GITHUB] Error updating file {FilePath} in {Owner}/{Repo}: {Message}", filePath, owner, repo, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Fallback method to create files using Contents API when Git Trees API fails
    /// </summary>
    private async Task<bool> CreateFilesUsingContentsApiAsync(string owner, string repositoryName, 
        Dictionary<string, string> fileContents, string accessToken, string? programmingLanguage)
    {
        try
        {
            _logger.LogInformation("📝 [GITHUB] Creating {FileCount} files using Contents API (fallback method)", fileContents.Count);
            
            var commitMessage = !string.IsNullOrEmpty(programmingLanguage)
                ? $"Initial commit: Add project structure with {programmingLanguage} backend"
                : "Initial commit: Add project landing page";
            
            // Create files sequentially using Contents API
            int successCount = 0;
            int failCount = 0;
            
            foreach (var file in fileContents)
            {
                try
                {
                    // Use CreateFileUsingContentsApiAsync to create new files
                    var success = await CreateFileUsingContentsApiAsync(owner, repositoryName, file.Key, file.Value, 
                        commitMessage, accessToken);
                    
                    if (success)
                    {
                        successCount++;
                        _logger.LogDebug("✅ [GITHUB] Created file: {FilePath}", file.Key);
                    }
                    else
                    {
                        failCount++;
                        _logger.LogWarning("⚠️ [GITHUB] Failed to create file: {FilePath}", file.Key);
                    }
                    
                    // Small delay to avoid rate limiting
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    failCount++;
                    _logger.LogError(ex, "❌ [GITHUB] Error creating file {FilePath}: {Message}", file.Key, ex.Message);
                }
            }
            
            _logger.LogInformation("✅ [GITHUB] Created {SuccessCount}/{TotalCount} files using Contents API", 
                successCount, fileContents.Count);
            
            return successCount > 0 && failCount == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [GITHUB] Error creating files using Contents API: {Message}", ex.Message);
            return false;
        }
    }
    
    /// <summary>
    /// Creates a single file using Contents API
    /// </summary>
    private async Task<bool> CreateFileUsingContentsApiAsync(string owner, string repo, string filePath, 
        string content, string message, string accessToken)
    {
        try
        {
            // Encode content to base64
            var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
            var base64Content = Convert.ToBase64String(contentBytes);

            // Prepare create payload (no SHA for new files)
            var createPayload = new
            {
                message = message,
                content = base64Content,
                branch = "main"
            };

            var jsonContent = JsonSerializer.Serialize(createPayload);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Put, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/contents/{filePath}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = httpContent;

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                var errorPreview = responseContent.Length > 500 ? responseContent.Substring(0, 500) : responseContent;
                _logger.LogWarning("Failed to create file {FilePath}: {StatusCode} - {Error}", 
                    filePath, response.StatusCode, errorPreview);
                
                // Log specific error for nested directories
                if (filePath.Contains("/") && response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
                {
                    _logger.LogWarning("⚠️ [GITHUB] File in nested directory {FilePath} - GitHub Contents API should auto-create parent dirs", filePath);
                }
                
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating file {FilePath}: {Message}", filePath, ex.Message);
            return false;
        }
    }
    
    /// <summary>
    /// Gets the content of a blob by SHA
    /// </summary>
    private async Task<string?> GetBlobContentAsync(string owner, string repositoryName, string blobSha, string accessToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/git/blobs/{blobSha}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var blobData = JsonSerializer.Deserialize<JsonElement>(responseContent);
            
            if (blobData.TryGetProperty("content", out var contentProp))
            {
                var base64Content = contentProp.GetString();
                if (!string.IsNullOrEmpty(base64Content))
                {
                    // Remove newlines from base64 string and decode
                    var cleanedBase64 = base64Content.Replace("\n", "").Trim();
                    var bytes = Convert.FromBase64String(cleanedBase64);
                    return System.Text.Encoding.UTF8.GetString(bytes);
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting blob content for SHA {BlobSha}: {Message}", blobSha, ex.Message);
            return null;
        }
    }

    // Helper methods for Git operations

    private async Task<DefaultBranchInfo?> GetDefaultBranchAsync(string owner, string repositoryName, string accessToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/git/refs/heads/main");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get main branch, trying master");
                // Try 'master' as fallback
                request = new HttpRequestMessage(HttpMethod.Get, 
                    $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/git/refs/heads/master");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                response = await _httpClient.SendAsync(request);
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var branchRef = JsonSerializer.Deserialize<JsonElement>(content);

            var commitSha = branchRef.GetProperty("object").GetProperty("sha").GetString();
            var branchName = branchRef.GetProperty("ref").GetString()?.Replace("refs/heads/", "");

            // Get the commit to find the tree
            var commitRequest = new HttpRequestMessage(HttpMethod.Get, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/git/commits/{commitSha}");
            commitRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var commitResponse = await _httpClient.SendAsync(commitRequest);
            if (!commitResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var commitContent = await commitResponse.Content.ReadAsStringAsync();
            var commitData = JsonSerializer.Deserialize<JsonElement>(commitContent);
            var treeSha = commitData.GetProperty("tree").GetProperty("sha").GetString();

            return new DefaultBranchInfo
            {
                BranchName = branchName ?? "main",
                CommitSha = commitSha ?? "",
                TreeSha = treeSha ?? ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting default branch");
            return null;
        }
    }

    private async Task<string?> CreateBlobAsync(string owner, string repositoryName, string content, string accessToken)
    {
        try
        {
            var blobPayload = new
            {
                content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content)),
                encoding = "base64"
            };

            var jsonContent = JsonSerializer.Serialize(blobPayload);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/git/blobs");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = httpContent;

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var blobData = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return blobData.GetProperty("sha").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating blob");
            return null;
        }
    }

    private async Task<string?> CreateTreeAsync(string owner, string repositoryName, string baseTreeSha, 
        object[] tree, string accessToken)
    {
        try
        {
            _logger.LogDebug("Creating tree with base SHA: {BaseTreeSha}, {FileCount} files", baseTreeSha, tree.Length);
            
            var treePayload = new
            {
                base_tree = baseTreeSha,
                tree = tree
            };

            // GitHub API requires snake_case property names, so we use default serialization
            var jsonContent = JsonSerializer.Serialize(treePayload);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/git/trees");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = httpContent;

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Check for rate limiting
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var rateLimitRemaining = response.Headers.Contains("X-RateLimit-Remaining") 
                        ? response.Headers.GetValues("X-RateLimit-Remaining").FirstOrDefault() 
                        : "unknown";
                    var rateLimitReset = response.Headers.Contains("X-RateLimit-Reset") 
                        ? response.Headers.GetValues("X-RateLimit-Reset").FirstOrDefault() 
                        : "unknown";
                    
                    _logger.LogError("403 Forbidden when creating tree. Rate limit remaining: {Remaining}, Reset: {Reset}", 
                        rateLimitRemaining, rateLimitReset);
                }
                
                _logger.LogError("Failed to create tree. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, responseContent.Length > 1000 ? responseContent.Substring(0, 1000) + "..." : responseContent);
                _logger.LogDebug("Tree payload size: {Size} bytes, File count: {FileCount}", jsonContent.Length, tree.Length);
                
                // Log first few file paths for debugging
                var filePaths = new List<string>();
                foreach (var item in tree.Take(10))
                {
                    var path = item.GetType().GetProperty("path")?.GetValue(item)?.ToString() ?? "unknown";
                    filePaths.Add(path);
                }
                _logger.LogDebug("First {Count} file paths in tree: {Paths}", Math.Min(10, tree.Length), string.Join(", ", filePaths));
                
                return null;
            }

            var treeData = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var treeSha = treeData.GetProperty("sha").GetString();
            _logger.LogDebug("Successfully created tree with SHA: {TreeSha}", treeSha);
            return treeSha;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tree: {Message}", ex.Message);
            return null;
        }
    }

    private async Task<string?> CreateCommitAsync(string owner, string repositoryName, string treeSha, 
        string parentSha, string message, string accessToken)
    {
        try
        {
            var commitPayload = new
            {
                message = message,
                tree = treeSha,
                parents = new[] { parentSha }
            };

            var jsonContent = JsonSerializer.Serialize(commitPayload);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/git/commits");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = httpContent;

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var commitData = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return commitData.GetProperty("sha").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating commit");
            return null;
        }
    }

    private async Task<bool> UpdateReferenceAsync(string owner, string repositoryName, string branchName, 
        string commitSha, string accessToken)
    {
        try
        {
            var refPayload = new
            {
                sha = commitSha,
                force = false
            };

            var jsonContent = JsonSerializer.Serialize(refPayload);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Patch, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/git/refs/heads/{branchName}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = httpContent;

            var response = await _httpClient.SendAsync(request);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating reference");
            return false;
        }
    }

    private string GenerateReadmeContent(string projectTitle, string? databaseConnectionString, string? webApiUrl = null, string? swaggerUrl = null)
    {
        var readme = $"# {projectTitle}\n\n";

        if (!string.IsNullOrWhiteSpace(databaseConnectionString))
        {
            readme += $"## Application Database\n\n";
            readme += $"**Application DB Connection String:** `{databaseConnectionString}`\n\n";
        }

        if (!string.IsNullOrWhiteSpace(webApiUrl))
        {
            readme += $"## Web API\n\n";
            readme += $"**WebApi URL:** {webApiUrl}\n\n";
        }

        if (!string.IsNullOrWhiteSpace(swaggerUrl))
        {
            readme += $"**Swagger API Tester URL:** {swaggerUrl}\n\n";
        }

        readme += "## Recommended Tools\n\n";
        readme += "**Recommended SQL Editor tool (Free):** [pgAdmin](https://www.pgadmin.org/download/)\n";

        return readme;
    }

    /// <summary>
    /// Generates README content for frontend-only repository
    /// </summary>
    private string GenerateFrontendReadmeContent(string projectTitle, string pagesUrl, string? webApiUrl = null)
    {
        var readme = $"# {projectTitle} - Frontend\n\n";
        readme += $"## Frontend Deployment\n\n";
        readme += $"**GitHub Pages URL:** {pagesUrl}\n\n";
        
        if (!string.IsNullOrWhiteSpace(webApiUrl))
        {
            readme += $"## Backend API\n\n";
            readme += $"**API URL:** {webApiUrl}\n\n";
        }
        
        readme += "## Project Structure\n\n";
        readme += "- `index.html` - Main landing page\n";
        readme += "- `config.js` - API configuration\n";
        readme += "- `style.css` - Styling\n\n";
        
        return readme;
    }

    /// <summary>
    /// Generates README content for backend-only repository
    /// </summary>
    private string GenerateBackendReadmeContent(string projectTitle, string? databaseConnectionString, string? webApiUrl = null, string? swaggerUrl = null)
    {
        var readme = $"# {projectTitle} - Backend API\n\n";

        if (!string.IsNullOrWhiteSpace(databaseConnectionString))
        {
            readme += $"## Application Database\n\n";
            readme += $"**Application DB Connection String:** `{databaseConnectionString}`\n\n";
        }

        if (!string.IsNullOrWhiteSpace(webApiUrl))
        {
            readme += $"## Web API\n\n";
            readme += $"**WebApi URL:** {webApiUrl}\n\n";
        }

        if (!string.IsNullOrWhiteSpace(swaggerUrl))
        {
            readme += $"**Swagger API Tester URL:** {swaggerUrl}\n\n";
        }

        readme += "## Recommended Tools\n\n";
        readme += "**Recommended SQL Editor tool (Free):** [pgAdmin](https://www.pgadmin.org/download/)\n\n";
        
        readme += "## Deployment\n\n";
        readme += "This backend is configured for Railway deployment using nixpacks.toml.\n";

        return readme;
    }

    private string GenerateDefaultIndexHtml(string projectTitle, string pagesUrl)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{projectTitle} - Project Page</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }}
        
        .container {{
            max-width: 800px;
            background: white;
            border-radius: 20px;
            padding: 60px 40px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
            text-align: center;
            animation: fadeIn 0.8s ease-in;
        }}
        
        @keyframes fadeIn {{
            from {{ opacity: 0; transform: translateY(20px); }}
            to {{ opacity: 1; transform: translateY(0); }}
        }}
        
        .icon {{
            width: 80px;
            height: 80px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border-radius: 50%;
            margin: 0 auto 30px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 40px;
        }}
        
        h1 {{
            color: #2d3748;
            font-size: 2.5rem;
            margin-bottom: 20px;
            font-weight: 700;
        }}
        
        .subtitle {{
            color: #667eea;
            font-size: 1.2rem;
            margin-bottom: 30px;
            font-weight: 600;
        }}
        
        p {{
            color: #4a5568;
            line-height: 1.8;
            margin-bottom: 20px;
            font-size: 1.1rem;
        }}
        
        .highlight {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
            font-weight: 600;
        }}
        
        .info-box {{
            background: #f7fafc;
            border-left: 4px solid #667eea;
            padding: 20px;
            margin: 30px 0;
            text-align: left;
            border-radius: 8px;
        }}
        
        .info-box h3 {{
            color: #2d3748;
            margin-bottom: 15px;
            font-size: 1.2rem;
        }}
        
        .info-box ul {{
            list-style: none;
            padding-left: 0;
        }}
        
        .info-box li {{
            color: #4a5568;
            margin-bottom: 10px;
            padding-left: 30px;
            position: relative;
        }}
        
        .info-box li:before {{
            content: ""✓"";
            position: absolute;
            left: 0;
            color: #667eea;
            font-weight: bold;
            font-size: 1.2rem;
        }}
        
        .footer {{
            margin-top: 40px;
            padding-top: 30px;
            border-top: 2px solid #e2e8f0;
            color: #718096;
            font-size: 0.9rem;
        }}
        
        .url-box {{
            display: inline-block;
            background: #edf2f7;
            padding: 12px 20px;
            border-radius: 8px;
            font-family: 'Courier New', monospace;
            color: #667eea;
            margin: 10px 0;
            font-weight: 600;
            word-break: break-all;
        }}
        
        @media (max-width: 600px) {{
            .container {{
                padding: 40px 20px;
            }}
            
            h1 {{
                font-size: 2rem;
            }}
            
            .subtitle {{
                font-size: 1rem;
            }}
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""icon"">🚀</div>
        
        <h1>{projectTitle}</h1>
        <p class=""subtitle"">Your Project Landing Page</p>
        
        <p>
            Welcome to your project's <span class=""highlight"">default landing page</span>! 
            This is the beginning of something amazing.
        </p>
        
        <p>
            This page is hosted on <span class=""highlight"">GitHub Pages</span> and is ready to showcase 
            your project to the world. As you develop your prototype, this page will evolve into 
            your final product presentation.
        </p>
        
        <div class=""info-box"">
            <h3>📚 About This Repository</h3>
            <ul>
                <li>Use this repository to collaborate with your team</li>
                <li>Commit your code and track changes using Git</li>
                <li>Deploy your final prototype automatically with GitHub Pages</li>
                <li>Share your progress with stakeholders</li>
            </ul>
        </div>
        
        <p>
            Your project is accessible at:<br>
            <span class=""url-box"">{pagesUrl}</span>
        </p>
        
        <div class=""info-box"">
            <h3>🎯 Next Steps</h3>
            <ul>
                <li>Clone this repository to your local machine</li>
                <li>Start building your prototype</li>
                <li>Push your changes to see them live instantly</li>
                <li>Replace this page with your amazing end product!</li>
            </ul>
        </div>

        <button id=""testButton"" onclick=""testBackend()"">Click Me</button>
        <div id=""response""></div>
        
        <div class=""footer"">
            <p>
                This page will be automatically replaced when you push your project files.<br>
                <strong>Happy coding! 🎉</strong>
            </p>
        </div>
    </div>
    <script>
        // Load config.js and ensure it's available
        let configLoaded = false;
        const configScript = document.createElement('script');
        configScript.src = 'config.js';
        configScript.onload = function() {{
            configLoaded = true;
            console.log('✅ config.js loaded successfully');
            console.log('📋 CONFIG:', typeof CONFIG !== 'undefined' ? CONFIG : 'CONFIG not defined');
            console.log('📋 window.CONFIG:', typeof window !== 'undefined' && window.CONFIG ? window.CONFIG : 'window.CONFIG not defined');
        }};
        configScript.onerror = function() {{
            console.error('❌ Failed to load config.js');
            configLoaded = false;
        }};
        document.head.appendChild(configScript);
    </script>
    <script>
        // Debug: Log config on load
        window.addEventListener('DOMContentLoaded', function() {{
            console.log('📋 Config loaded:', typeof CONFIG !== 'undefined' ? CONFIG : 'CONFIG not defined');
            console.log('📋 window.CONFIG:', typeof window !== 'undefined' && window.CONFIG ? window.CONFIG : 'window.CONFIG not defined');
            console.log('📋 API_URL from config:', typeof CONFIG !== 'undefined' ? CONFIG?.API_URL : (window.CONFIG?.API_URL || 'not found'));
        }});
        
        async function testBackend() {{
            const button = document.getElementById('testButton');
            const responseDiv = document.getElementById('response');
            
            button.disabled = true;
            button.textContent = 'Loading...';
            responseDiv.className = '';
            responseDiv.textContent = '';
            
            try {{
                // Wait a moment for config.js to load if it hasn't yet
                if (!configLoaded) {{
                    await new Promise(resolve => setTimeout(resolve, 100));
                }}
                
                // Try multiple ways to access CONFIG (handle scope issues)
                let config = typeof CONFIG !== 'undefined' ? CONFIG : (typeof window !== 'undefined' && window.CONFIG ? window.CONFIG : null);
                let apiUrl = config?.API_URL || '';
                
                console.log('🔍 CONFIG object:', config);
                console.log('🔍 Original API_URL from config:', apiUrl);
                console.log('🔍 typeof CONFIG:', typeof CONFIG);
                console.log('🔍 window.CONFIG:', typeof window !== 'undefined' ? window.CONFIG : 'window not available');
                console.log('🔍 configLoaded flag:', configLoaded);
                
                if (!apiUrl) {{
                    const errorMsg = 'API URL not configured in config.js. ' +
                        'CONFIG object: ' + JSON.stringify(config) + 
                        ', window.CONFIG: ' + JSON.stringify(typeof window !== 'undefined' ? window.CONFIG : 'N/A') +
                        ', configLoaded: ' + configLoaded;
                    throw new Error(errorMsg);
                }}
                
                // Ensure HTTPS for Railway domains (fix Mixed Content errors)
                if (apiUrl.includes('railway.app')) {{
                    if (apiUrl.startsWith('http://')) {{
                        apiUrl = apiUrl.replace('http://', 'https://');
                        console.warn('⚠️ Converted HTTP to HTTPS for Railway domain');
                    }}
                    if (!apiUrl.startsWith('https://')) {{
                        apiUrl = 'https://' + apiUrl.replace(/^https?:\/\//, '');
                        console.warn('⚠️ Added HTTPS protocol to Railway domain');
                    }}
                }}
                
                // Remove trailing slash from base URL if present
                apiUrl = apiUrl.replace(/\/$/, '');
                console.log('✅ Final API_URL (after normalization):', apiUrl);
                
                // Add trailing slash to prevent Railway redirect (Railway redirects /api/test to /api/test/)
                // This prevents Railway from redirecting HTTPS to HTTP
                const fullUrl = apiUrl + '/api/test/';
                console.log('🌐 Attempting to fetch:', fullUrl);
                console.log('🌐 Full URL type check:', typeof fullUrl, 'Starts with https?:', fullUrl.startsWith('https://'));
                
                // Check if URL is a Railway project URL (not a service URL)
                if (apiUrl.includes('railway.app/project/')) {{
                    responseDiv.className = 'error';
                    responseDiv.innerHTML = `
                        <strong>❌ Configuration Error:</strong><br>
                        <strong>The API URL is pointing to a Railway project page, not a service URL.</strong><br><br>
                        <strong>Current URL:</strong> <code>${{apiUrl}}</code><br><br>
                        <strong>Issue:</strong><br>
                        Railway project pages (<code>railway.app/project/...</code>) are not API endpoints.<br>
                        Your backend service needs to be deployed first to get a real API URL.<br><br>
                        <strong>Solution:</strong><br>
                        1. Deploy your backend code to Railway (push to GitHub and connect to Railway)<br>
                        2. After deployment, Railway will generate a service URL like: <code>https://your-service-name.railway.app</code><br>
                        3. Update <code>config.js</code> with the actual service URL<br>
                        4. The service URL can be found in Railway dashboard: Service → Settings → Domains<br><br>
                        <strong>Note:</strong> The backend code already includes CORS configuration, so once deployed with the correct URL, it will work.
                    `;
                    console.error('Invalid API URL (Railway project URL):', apiUrl);
                    return;
                }}
                
                // Final validation - ensure URL is definitely HTTPS
                if (!fullUrl.startsWith('https://')) {{
                    throw new Error('❌ CRITICAL: URL is not HTTPS! URL: ' + fullUrl);
                }}
                
                let response;
                try {{
                    // Use fetch with explicit options
                    console.log('📡 Making fetch request with explicit options...');
                    console.log('📡 Full URL before fetch (stringified):', String(fullUrl));
                    console.log('📡 Full URL type:', typeof fullUrl);
                    console.log('📡 Full URL startsWith https?:', String(fullUrl).startsWith('https://'));
                    
                    const controller = new AbortController();
                    const timeoutId = setTimeout(() => controller.abort(), 10000); // 10 second timeout
                    
                    // Create URL object to ensure it's valid and has correct protocol
                    const urlObj = new URL(fullUrl);
                    console.log('📡 URL object protocol:', urlObj.protocol);
                    console.log('📡 URL object href:', urlObj.href);
                    
                    // Force HTTPS protocol
                    if (urlObj.protocol !== 'https:') {{
                        urlObj.protocol = 'https:';
                        console.warn('⚠️ FORCED protocol to HTTPS:', urlObj.href);
                    }}
                    
                    const finalFetchUrl = urlObj.href;
                    console.log('📡 Final fetch URL:', finalFetchUrl);
                    
                    response = await fetch(finalFetchUrl, {{
                        method: 'GET',
                        mode: 'cors',
                        credentials: 'omit',
                        redirect: 'error', // Fail on redirect to catch HTTPS->HTTP redirects
                        signal: controller.signal,
                        headers: {{
                            'Accept': 'application/json'
                        }}
                    }});
                    
                    clearTimeout(timeoutId);
                    console.log('✅ Fetch completed, status:', response.status, 'Final URL:', response.url);
                }} catch (fetchError) {{
                    // Network error - check if it's a CORS error or connection error
                    const isCorsError = fetchError.message.includes('CORS') || 
                                       (fetchError.name === 'TypeError' && fetchError.message.includes('Failed to fetch'));
                    
                    let errorDetails = '';
                    if (isCorsError) {{
                        errorDetails = `
                            <strong>❌ CORS Error:</strong><br>
                            <strong>The API server is not allowing requests from this origin.</strong><br><br>
                            <strong>Details:</strong><br>
                            • URL: <code>${{fullUrl}}</code><br>
                            • Origin: <code>${{window.location.origin}}</code><br>
                            • Error: ${{fetchError.message}}<br><br>
                            <strong>Possible causes:</strong><br>
                            • Backend CORS is not configured correctly<br>
                            • Service is not deployed or offline<br>
                            • Wrong API URL (might be pointing to project page instead of service)<br><br>
                            <strong>Note:</strong> If your backend is deployed, make sure CORS allows your GitHub Pages domain.<br>
                        `;
                    }} else {{
                        errorDetails = `
                            <strong>❌ Connection Error:</strong><br>
                            <strong>Cannot connect to the API server</strong><br><br>
                            <strong>Details:</strong><br>
                            • URL: <code>${{fullUrl}}</code><br>
                            • Error: ${{fetchError.message}}<br><br>
                            <strong>Possible causes:</strong><br>
                            • Railway service is not deployed yet<br>
                            • Service is offline or crashed<br>
                            • Network connectivity problem<br>
                            • Wrong API URL<br><br>
                            <strong>Next steps:</strong><br>
                            1. Check Railway dashboard - is the service deployed?<br>
                            2. Check service logs in Railway<br>
                            3. Verify the API URL in config.js is the service URL (not project URL)<br>
                            4. Service URL should be like: <code>https://your-service-name.railway.app</code><br>
                        `;
                    }}
                    
                    responseDiv.className = 'error';
                    responseDiv.innerHTML = errorDetails;
                    console.error('Fetch error:', fetchError);
                    return;
                }}
                
                console.log('Response status:', response.status, response.statusText);
                
                if (response.ok) {{
                    const data = await response.json();
                    responseDiv.className = 'success';
                    responseDiv.innerHTML = '<strong>✅ Success!</strong><br>Backend API is working. Received ' + data.length + ' test projects.';
                }} else {{
                    // HTTP error response
                    let errorText = '';
                    try {{
                        errorText = await response.text();
                    }} catch (e) {{
                        errorText = 'Could not read error response';
                    }}
                    
                    responseDiv.className = 'error';
                    responseDiv.innerHTML = `
                        <strong>❌ API Error:</strong><br>
                        <strong>HTTP Status: ${{response.status}} ${{response.statusText}}</strong><br><br>
                        <strong>URL:</strong> <code>${{fullUrl}}</code><br>
                        <strong>Response:</strong> <pre>${{errorText.substring(0, 200)}}</pre>
                    `;
                    console.error('API error:', response.status, errorText);
                }}
            }} catch (error) {{
                responseDiv.className = 'error';
                responseDiv.innerHTML = `
                    <strong>❌ Unexpected Error:</strong><br>
                    <strong>${{error.name || 'Error'}}:</strong> ${{error.message}}<br><br>
                    <strong>Stack trace:</strong><br>
                    <pre style=""font-size: 10px; max-height: 200px; overflow: auto;"">${{error.stack || 'No stack trace available'}}</pre>
                `;
                console.error('Unexpected error:', error);
            }} finally {{
                button.disabled = false;
                button.textContent = 'Click Me';
            }}
        }}
    </script>
    <link rel=""stylesheet"" href=""style.css"">
</body>
</html>";
    }

    private string GenerateTestProjectsSqlScript()
    {
        return @"-- TestProjects table for initial project setup
-- This table is used for testing and learning database interactions

CREATE TABLE IF NOT EXISTS ""TestProjects"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""Name"" VARCHAR(255) NOT NULL
);

-- Insert mock data
INSERT INTO ""TestProjects"" (""Name"") VALUES
    ('Sample Project 1'),
    ('Sample Project 2'),
    ('Sample Project 3'),
    ('Learning Project'),
    ('Test Project');
";
    }

    private Dictionary<string, string> GenerateBackendFiles(string programmingLanguage, string? webApiUrl)
    {
        var files = new Dictionary<string, string>();

        switch (programmingLanguage?.ToLowerInvariant())
        {
            case "c#":
            case "csharp":
                files = GenerateCSharpBackend(webApiUrl);
                break;
            case "python":
                files = GeneratePythonBackend(webApiUrl);
                break;
            case "nodejs":
            case "node.js":
            case "node":
                files = GenerateNodeJSBackend(webApiUrl);
                break;
            case "java":
                files = GenerateJavaBackend(webApiUrl);
                break;
            case "php":
                files = GeneratePhpBackend(webApiUrl);
                break;
            case "ruby":
                files = GenerateRubyBackend(webApiUrl);
                break;
            case "go":
            case "golang":
                files = GenerateGoBackend(webApiUrl);
                break;
            default:
                // Default to C# if language not recognized
                _logger.LogWarning("Unknown programming language '{Language}', defaulting to C#", programmingLanguage);
                files = GenerateCSharpBackend(webApiUrl);
                break;
        }

        return files;
    }

    private Dictionary<string, string> GenerateCSharpBackend(string? webApiUrl)
    {
        var files = new Dictionary<string, string>();

        // TestProjects Model
        files["backend/Models/TestProjects.cs"] = @"using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models;

public class TestProjects
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
}
";

        // TestController
        files["backend/Controllers/TestController.cs"] = @"using Microsoft.AspNetCore.Mvc;
using Backend.Models;
using Npgsql;

namespace Backend.Controllers;

[ApiController]
[Route(""api/[controller]"")]
public class TestController : ControllerBase
{
    private readonly string _connectionString;

    public TestController(IConfiguration configuration)
    {
        var rawConnectionString = configuration.GetConnectionString(""DefaultConnection"") 
            ?? Environment.GetEnvironmentVariable(""DATABASE_URL"") 
            ?? throw new InvalidOperationException(""Database connection string not found"");
        
        // Convert PostgreSQL URL to Npgsql connection string format if needed
        _connectionString = ConvertPostgresUrlToConnectionString(rawConnectionString);
    }
    
    private string ConvertPostgresUrlToConnectionString(string connectionString)
    {
        // If it's already a connection string (not a URL), return as-is
        if (!connectionString.StartsWith(""postgresql://"", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith(""postgres://"", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }
        
        try
        {
            var uri = new Uri(connectionString);
            var builder = new System.Text.StringBuilder();
            
            // Extract components from URL
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');
            var username = Uri.UnescapeDataString(uri.UserInfo.Split(':')[0]);
            var password = uri.UserInfo.Contains(':') 
                ? Uri.UnescapeDataString(uri.UserInfo.Substring(uri.UserInfo.IndexOf(':') + 1))
                : """";
            
            // Build Npgsql connection string
            builder.Append($""Host={host};Port={port};Database={database};Username={username}"");
            if (!string.IsNullOrEmpty(password))
            {
                builder.Append($"";Password={password}"");
            }
            
            // Parse query string for additional parameters (e.g., sslmode)
            var sslMode = ""Require"";
            if (!string.IsNullOrEmpty(uri.Query) && uri.Query.Length > 1)
            {
                var queryString = uri.Query.Substring(1); // Remove '?'
                var queryParams = queryString.Split('&');
                foreach (var param in queryParams)
                {
                    var parts = param.Split('=');
                    if (parts.Length == 2 && parts[0].Equals(""sslmode"", StringComparison.OrdinalIgnoreCase))
                    {
                        sslMode = Uri.UnescapeDataString(parts[1]);
                        break;
                    }
                }
            }
            builder.Append($"";SSL Mode={sslMode}"");
            
            return builder.ToString();
        }
        catch
        {
            // If parsing fails, return original (Npgsql might handle it)
            return connectionString;
        }
    }

    // GET: api/test
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TestProjects>>> GetAll()
    {
        try
    {
        var projects = new List<TestProjects>();
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
            
            // Set search_path to public schema (required because isolated role has restricted search_path)
            // Note: Using string concatenation to avoid $ interpolation issues
            using var setPathCmd = new NpgsqlCommand(""SET search_path = public, \"""" + ""$"" + ""user\"";"", conn);
            await setPathCmd.ExecuteNonQueryAsync();
            
        var quote = Convert.ToChar(34).ToString(); // Double quote for PostgreSQL identifier quoting
        var sql = ""SELECT "" + quote + ""Id"" + quote + "", "" + quote + ""Name"" + quote + "" FROM "" + quote + ""TestProjects"" + quote + "" ORDER BY "" + quote + ""Id"" + quote + "" "";
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            projects.Add(new TestProjects
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }
        return Ok(projects);
        }
        catch (PostgresException ex) when (ex.SqlState == ""42P01"") // Table does not exist
        {
            // TestProjects table doesn't exist - return empty list gracefully
            // This can happen if the database schema wasn't fully initialized
            return Ok(new List<TestProjects>());
        }
        // Do NOT catch generic Exception - let it bubble up to GlobalExceptionHandlerMiddleware
        // This allows runtime errors to be logged to the error reporting endpoint
    }

    // GET: api/test/5
    [HttpGet(""{id}"")]
    public async Task<ActionResult<TestProjects>> Get(int id)
    {
        try
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
            
            // Set search_path to public schema (required because isolated role has restricted search_path)
            // Note: Using string concatenation to avoid $ interpolation issues
            using var setPathCmd = new NpgsqlCommand(""SET search_path = public, \"""" + ""$"" + ""user\"";"", conn);
            await setPathCmd.ExecuteNonQueryAsync();
            
        var quote = Convert.ToChar(34).ToString(); // Double quote for PostgreSQL identifier quoting
        var sql = ""SELECT "" + quote + ""Id"" + quote + "", "" + quote + ""Name"" + quote + "" FROM "" + quote + ""TestProjects"" + quote + "" WHERE "" + quote + ""Id"" + quote + "" = @id "";
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(""id"", id);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return Ok(new TestProjects
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }
        return NotFound();
        }
        catch (PostgresException ex) when (ex.SqlState == ""42P01"") // Table does not exist
        {
            // TestProjects table doesn't exist - return 404 gracefully
            // This can happen if the database schema wasn't fully initialized
            return NotFound();
        }
        // Do NOT catch generic Exception - let it bubble up to GlobalExceptionHandlerMiddleware
        // This allows runtime errors to be logged to the error reporting endpoint
    }

    // POST: api/test
    [HttpPost]
    public async Task<ActionResult<TestProjects>> Create([FromBody] TestProjects project)
    {
        try
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
            
            // Set search_path to public schema (required because isolated role has restricted search_path)
            // Note: Using string concatenation to avoid $ interpolation issues
            using var setPathCmd = new NpgsqlCommand(""SET search_path = public, \"""" + ""$"" + ""user\"";"", conn);
            await setPathCmd.ExecuteNonQueryAsync();
            
        var quote = Convert.ToChar(34).ToString(); // Double quote for PostgreSQL identifier quoting
        var sql = ""INSERT INTO "" + quote + ""TestProjects"" + quote + "" ("" + quote + ""Name"" + quote + "") VALUES (@name) RETURNING "" + quote + ""Id"" + quote + "" "";
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(""name"", project.Name);
        var id = await cmd.ExecuteScalarAsync();
        project.Id = Convert.ToInt32(id);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, project);
        }
        catch (PostgresException ex) when (ex.SqlState == ""42P01"") // Table does not exist
        {
            // TestProjects table doesn't exist - return 503 gracefully
            // This can happen if the database schema wasn't fully initialized
            return StatusCode(503, new { error = ""Service Unavailable"", message = ""Database schema not initialized. Please contact support."" });
        }
        // Do NOT catch generic Exception - let it bubble up to GlobalExceptionHandlerMiddleware
        // This allows runtime errors to be logged to the error reporting endpoint
    }

    // PUT: api/test/5
    [HttpPut(""{id}"")]
    public async Task<IActionResult> Update(int id, [FromBody] TestProjects project)
    {
        try
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
            
            // Set search_path to public schema (required because isolated role has restricted search_path)
            // Note: Using string concatenation to avoid $ interpolation issues
            using var setPathCmd = new NpgsqlCommand(""SET search_path = public, \"""" + ""$"" + ""user\"";"", conn);
            await setPathCmd.ExecuteNonQueryAsync();
            
        var quote = Convert.ToChar(34).ToString(); // Double quote for PostgreSQL identifier quoting
        var sql = ""UPDATE "" + quote + ""TestProjects"" + quote + "" SET "" + quote + ""Name"" + quote + "" = @name WHERE "" + quote + ""Id"" + quote + "" = @id "";
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(""name"", project.Name);
        cmd.Parameters.AddWithValue(""id"", id);
        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        if (rowsAffected == 0) return NotFound();
        return NoContent();
        }
        catch (PostgresException ex) when (ex.SqlState == ""42P01"") // Table does not exist
        {
            // TestProjects table doesn't exist - return 404 gracefully
            return NotFound();
        }
        // Do NOT catch generic Exception - let it bubble up to GlobalExceptionHandlerMiddleware
        // This allows runtime errors to be logged to the error reporting endpoint
    }

    // DELETE: api/test/5
    [HttpDelete(""{id}"")]
    public async Task<IActionResult> Delete(int id)
    {
        try
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
            
            // Set search_path to public schema (required because isolated role has restricted search_path)
            // Note: Using string concatenation to avoid $ interpolation issues
            using var setPathCmd = new NpgsqlCommand(""SET search_path = public, \"""" + ""$"" + ""user\"";"", conn);
            await setPathCmd.ExecuteNonQueryAsync();
            
        var quote = Convert.ToChar(34).ToString(); // Double quote for PostgreSQL identifier quoting
        var sql = ""DELETE FROM "" + quote + ""TestProjects"" + quote + "" WHERE "" + quote + ""Id"" + quote + "" = @id "";
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(""id"", id);
        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        if (rowsAffected == 0) return NotFound();
        return NoContent();
        }
        catch (PostgresException ex) when (ex.SqlState == ""42P01"") // Table does not exist
        {
            // TestProjects table doesn't exist - return 404 gracefully
            return NotFound();
        }
        // Do NOT catch generic Exception - let it bubble up to GlobalExceptionHandlerMiddleware
        // This allows runtime errors to be logged to the error reporting endpoint
    }

    // GET: api/test/debug-env
    // Debug endpoint to check environment variables and middleware configuration
    [HttpGet(""debug-env"")]
    public IActionResult DebugEnv()
    {
        var endpointUrl = Environment.GetEnvironmentVariable(""RUNTIME_ERROR_ENDPOINT_URL"");
        var allEnvVars = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Where(e => e.Key.ToString().Contains(""RUNTIME"") || 
                       e.Key.ToString().Contains(""ERROR"") || 
                       e.Key.ToString().Contains(""DATABASE"") ||
                       e.Key.ToString().Contains(""PORT""))
            .ToDictionary(e => e.Key.ToString(), e => e.Value?.ToString());
        
        return Ok(new { 
            RUNTIME_ERROR_ENDPOINT_URL = endpointUrl ?? ""NOT SET"",
            EnvironmentVariables = allEnvVars,
            Message = ""Use this endpoint to verify RUNTIME_ERROR_ENDPOINT_URL is set correctly""
        });
    }

    // GET: api/test/test-error/{boardId}
    // Test endpoint that throws an exception to test middleware
    // boardId is included in route so middleware can extract it
    [HttpGet(""test-error/{boardId}"")]
    public IActionResult TestError(string boardId)
    {
        throw new Exception($""Test exception for middleware debugging (BoardId: {boardId}) - this should be caught by GlobalExceptionHandlerMiddleware"");
    }
}
";

        // GlobalExceptionHandlerMiddleware.cs - Runtime error handler
        files["backend/Middleware/GlobalExceptionHandlerMiddleware.cs"] = @"using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Backend.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Log that middleware is being invoked (using Warning level to ensure it shows up)
        _logger.LogWarning(""[MIDDLEWARE] InvokeAsync called for path: {Path}, Method: {Method}"", 
            context.Request.Path, context.Request.Method);
        
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Check if response has already started
            if (context.Response.HasStarted)
            {
                _logger.LogError(""[MIDDLEWARE] Response already started - cannot handle exception. Re-throwing."");
                throw; // Re-throw if response started
            }
            
            _logger.LogError(ex, ""[MIDDLEWARE] Unhandled exception occurred: {Message}"", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Get the error endpoint URL from environment variable
        var errorEndpointUrl = Environment.GetEnvironmentVariable(""RUNTIME_ERROR_ENDPOINT_URL"");
        _logger.LogWarning(""[MIDDLEWARE] RUNTIME_ERROR_ENDPOINT_URL = {Url}"", errorEndpointUrl ?? ""NULL"");
        
        // If endpoint is configured, send error details to it (fire and forget)
        if (!string.IsNullOrWhiteSpace(errorEndpointUrl))
        {
            _logger.LogWarning(""[MIDDLEWARE] Attempting to send error to endpoint: {Url}"", errorEndpointUrl);
            
            // CRITICAL: Extract all values from HttpContext BEFORE Task.Run
            // HttpContext will be disposed after this method returns
            var requestPath = context.Request.Path.ToString();
            var requestMethod = context.Request.Method;
            var userAgent = context.Request.Headers[""User-Agent""].ToString();
            var boardId = ExtractBoardId(context); // Extract BEFORE Task.Run
            
            _logger.LogWarning(""[MIDDLEWARE] Extracted values - Path: {Path}, Method: {Method}, BoardId: {BoardId}"", 
                requestPath, requestMethod, boardId ?? ""NULL"");
            
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogWarning(""[MIDDLEWARE] Task.Run started, calling SendErrorToEndpointAsync"");
                    // Pass extracted values instead of HttpContext
                    await SendErrorToEndpointAsync(errorEndpointUrl, boardId, requestPath, requestMethod, userAgent, exception);
                }
                catch (Exception sendEx)
                {
                    _logger.LogError(sendEx, ""[MIDDLEWARE] Failed to send error to endpoint: {Endpoint}"", errorEndpointUrl);
                }
            });
        }
        else
        {
            _logger.LogWarning(""[MIDDLEWARE] RUNTIME_ERROR_ENDPOINT_URL is not set - skipping error reporting"");
        }

        // Return error response to client
        context.Response.ContentType = ""application/json"";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new
        {
            error = ""An error occurred while processing your request"",
            message = exception.Message
        };

        var json = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(json);
    }

    // Updated method signature - no longer takes HttpContext (which gets disposed)
    private async Task SendErrorToEndpointAsync(
        string endpointUrl, 
        string? boardId, 
        string requestPath, 
        string requestMethod, 
        string userAgent, 
        Exception exception)
    {
        _logger.LogWarning(""[MIDDLEWARE] SendErrorToEndpointAsync called with URL: {Url}"", endpointUrl);
        
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(5);

        _logger.LogWarning(""[MIDDLEWARE] Using extracted boardId: {BoardId}"", boardId ?? ""NULL"");

        var errorPayload = new
        {
            boardId = boardId,
            timestamp = DateTime.UtcNow,
            file = GetFileName(exception),
            line = GetLineNumber(exception),
            stackTrace = exception.StackTrace,
            message = exception.Message,
            exceptionType = exception.GetType().Name,
            requestPath = requestPath,  // Use extracted value
            requestMethod = requestMethod,  // Use extracted value
            userAgent = userAgent,  // Use extracted value
            innerException = exception.InnerException != null ? new
            {
                message = exception.InnerException.Message,
                type = exception.InnerException.GetType().Name,
                stackTrace = exception.InnerException.StackTrace
            } : null
        };

        var json = JsonSerializer.Serialize(errorPayload);
        var content = new StringContent(json, Encoding.UTF8, ""application/json"");

        _logger.LogWarning(""[MIDDLEWARE] Sending POST request to: {Url}"", endpointUrl);
        var response = await httpClient.PostAsync(endpointUrl, content);
        
        _logger.LogWarning(""[MIDDLEWARE] Response status: {StatusCode}"", response.StatusCode);
        
        if (response.IsSuccessStatusCode)
        {
            _logger.LogWarning(""[MIDDLEWARE] Successfully sent runtime error to endpoint: {Endpoint}"", endpointUrl);
        }
        else
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(""[MIDDLEWARE] Error endpoint returned {StatusCode}: {Response}"", response.StatusCode, responseBody);
        }
    }

    private string? ExtractBoardId(HttpContext context)
    {
        // Try route data
        if (context.Request.RouteValues.TryGetValue(""boardId"", out var boardIdObj))
            return boardIdObj?.ToString();
        
        // Try query string
        if (context.Request.Query.TryGetValue(""boardId"", out var boardIdQuery))
            return boardIdQuery.ToString();
        
        // Try header
        if (context.Request.Headers.TryGetValue(""X-Board-Id"", out var boardIdHeader))
            return boardIdHeader.ToString();
        
        // Try environment variable BOARD_ID (set during Railway deployment)
        var boardIdEnv = Environment.GetEnvironmentVariable(""BOARD_ID"");
        if (!string.IsNullOrWhiteSpace(boardIdEnv))
            return boardIdEnv;
        
        // Try to extract from hostname (Railway pattern: webapi{{boardId}}.up.railway.app - no hyphen)
        var host = context.Request.Host.ToString();
        var hostMatch = System.Text.RegularExpressions.Regex.Match(host, @""webapi([a-f0-9]{{24}})"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (hostMatch.Success && hostMatch.Groups.Count > 1)
            return hostMatch.Groups[1].Value;
        
        // Try to extract from RUNTIME_ERROR_ENDPOINT_URL if it contains boardId pattern (no hyphen)
        var endpointUrl = Environment.GetEnvironmentVariable(""RUNTIME_ERROR_ENDPOINT_URL"");
        if (!string.IsNullOrWhiteSpace(endpointUrl))
        {{
            var urlMatch = System.Text.RegularExpressions.Regex.Match(endpointUrl, @""webapi([a-f0-9]{{24}})"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (urlMatch.Success && urlMatch.Groups.Count > 1)
                return urlMatch.Groups[1].Value;
        }}
        
        return null;
    }

    private string? GetFileName(Exception exception)
    {
        var stackTrace = exception.StackTrace;
        if (string.IsNullOrEmpty(stackTrace)) return null;

        // C# stack trace format: ""at Namespace.Class.Method() in /path/to/file.cs:line 123""
        // Pattern: ""in <path>:line <number>"" or ""in <path>:<number>""
        var match = System.Text.RegularExpressions.Regex.Match(
            stackTrace,
            @""in\s+([^:]+):(?:line\s+)?(\d+)"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success && match.Groups.Count > 1)
        {
            var filePath = match.Groups[1].Value.Trim();
            // Extract just the filename from the path
            var lastSlash = filePath.LastIndexOf('/');
            if (lastSlash >= 0)
                return filePath.Substring(lastSlash + 1);
            var lastBackslash = filePath.LastIndexOf('\\');
            if (lastBackslash >= 0)
                return filePath.Substring(lastBackslash + 1);
            return filePath;
        }

        // Fallback: try to get from StackTrace frame if available
        try
        {
            var stackTraceObj = new System.Diagnostics.StackTrace(exception, true);
            if (stackTraceObj.FrameCount > 0)
            {
                var frame = stackTraceObj.GetFrame(0);
                var fileName = frame?.GetFileName();
                if (!string.IsNullOrEmpty(fileName))
                {
                    var lastSlash = fileName.LastIndexOf('/');
                    if (lastSlash >= 0)
                        return fileName.Substring(lastSlash + 1);
                    var lastBackslash = fileName.LastIndexOf('\\');
                    if (lastBackslash >= 0)
                        return fileName.Substring(lastBackslash + 1);
                    return fileName;
                }
            }
        }
        catch
        {
            // Ignore if StackTrace parsing fails
        }

        return null;
    }

    private int? GetLineNumber(Exception exception)
    {
        var stackTrace = exception.StackTrace;
        if (string.IsNullOrEmpty(stackTrace)) return null;

        // C# stack trace format: ""at Namespace.Class.Method() in /path/to/file.cs:line 123""
        // Pattern: "":line 123"" or "":123""
        var match = System.Text.RegularExpressions.Regex.Match(
            stackTrace,
            @""in\s+[^:]+:(?:line\s+)?(\d+)"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success && match.Groups.Count > 1)
        {
            var lineStr = match.Groups[1].Value;
            if (int.TryParse(lineStr, out var line))
                return line;
        }

        // Fallback: try to get from StackTrace frame if available
        try
        {
            var stackTraceObj = new System.Diagnostics.StackTrace(exception, true);
            if (stackTraceObj.FrameCount > 0)
            {
                var frame = stackTraceObj.GetFrame(0);
                var lineNumber = frame?.GetFileLineNumber();
                if (lineNumber > 0)
                    return lineNumber;
            }
        }
        catch
        {
            // Ignore if StackTrace parsing fails
        }

        return null;
    }
}
";

        // Program.cs
        files["backend/Program.cs"] = @"var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS configuration - Allow GitHub Pages and all origins
builder.Services.AddCors(options =>
{
    options.AddPolicy(""AllowAll"", policy =>
    {
        // Allow any origin including GitHub Pages (*.github.io), localhost, and Railway domains
        // Using SetIsOriginAllowed to explicitly allow GitHub Pages and other common origins
        policy.SetIsOriginAllowed(origin =>
        {
            // Allow all origins (GitHub Pages, localhost, Railway, etc.)
            // This is more flexible than AllowAnyOrigin() and allows for future credential support if needed
            if (string.IsNullOrEmpty(origin)) return false;
            
            var uri = new Uri(origin);
            // Allow GitHub Pages (*.github.io)
            if (uri.Host.EndsWith("".github.io"", StringComparison.OrdinalIgnoreCase))
                return true;
            // Allow localhost (development)
            if (uri.Host == ""localhost"" || uri.Host == ""127.0.0.1"")
                return true;
            // Allow Railway domains
            if (uri.Host.EndsWith("".railway.app"", StringComparison.OrdinalIgnoreCase))
                return true;
            // Allow all other origins for maximum flexibility
            return true;
        })
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Database connection string from Railway environment variable
// Handle PostgreSQL URLs (e.g., from Neon) by converting to Npgsql connection string format
var rawConnectionString = Environment.GetEnvironmentVariable(""DATABASE_URL"");
if (!string.IsNullOrEmpty(rawConnectionString))
{
    var connectionString = rawConnectionString;
    
    // If it's a PostgreSQL URL (postgresql://), parse and convert it
    if (connectionString.StartsWith(""postgresql://"", StringComparison.OrdinalIgnoreCase) ||
        connectionString.StartsWith(""postgres://"", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var uri = new Uri(connectionString);
            var connStringBuilder = new System.Text.StringBuilder();
            
            // Extract components from URL
            var dbHost = uri.Host;
            var dbPort = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');
            var username = Uri.UnescapeDataString(uri.UserInfo.Split(':')[0]);
            var password = uri.UserInfo.Contains(':') 
                ? Uri.UnescapeDataString(uri.UserInfo.Substring(uri.UserInfo.IndexOf(':') + 1))
                : """";
            
            // Build Npgsql connection string
            connStringBuilder.Append($""Host={dbHost};Port={dbPort};Database={database};Username={username}"");
            if (!string.IsNullOrEmpty(password))
            {
                connStringBuilder.Append($"";Password={password}"");
            }
            
            // Parse query string for additional parameters (e.g., sslmode)
            var sslMode = ""Require"";
            if (!string.IsNullOrEmpty(uri.Query) && uri.Query.Length > 1)
            {
                var queryString = uri.Query.Substring(1); // Remove '?'
                var queryParams = queryString.Split('&');
                foreach (var param in queryParams)
                {
                    var parts = param.Split('=');
                    if (parts.Length == 2 && parts[0].Equals(""sslmode"", StringComparison.OrdinalIgnoreCase))
                    {
                        sslMode = Uri.UnescapeDataString(parts[1]);
                        break;
                    }
                }
            }
            connStringBuilder.Append($"";SSL Mode={sslMode}"");
            
            connectionString = connStringBuilder.ToString();
        }
        catch (Exception ex)
        {
            // If parsing fails, log and use original connection string (Npgsql might handle it)
            Console.WriteLine($""Warning: Failed to parse PostgreSQL URL: {{ex.Message}}"");
        }
    }
    
    builder.Configuration[""ConnectionStrings:DefaultConnection""] = connectionString;
}

// Configure URL for Railway deployment
var port = Environment.GetEnvironmentVariable(""PORT"");
var url = string.IsNullOrEmpty(port) ? ""http://0.0.0.0:8080"" : $""http://0.0.0.0:{port}"";
builder.WebHost.UseUrls(url);

var app = builder.Build();

// Add global exception handler middleware FIRST (before other middleware)
// This ensures it catches all exceptions in the pipeline
app.UseMiddleware<Backend.Middleware.GlobalExceptionHandlerMiddleware>();

// Enable Swagger in all environments (including production)
app.UseSwagger();
app.UseSwaggerUI();

// CORS must be early in the pipeline, before Authorization
app.UseCors(""AllowAll"");

app.UseAuthorization();
app.MapControllers();

// Add a simple root route to verify the service is running
app.MapGet(""/"", () => new { 
    message = ""Backend API is running"", 
    status = ""ok"",
    swagger = ""/swagger"",
    api = ""/api/test""
});

try
{
app.Run();
}
catch (Exception startupEx)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(startupEx, ""[STARTUP ERROR] Application failed to start: {Message}"", startupEx.Message);
    
    // Send startup error to endpoint (fire and forget)
    var apiBaseUrl = app.Configuration[""ApiBaseUrl""];
    if (!string.IsNullOrWhiteSpace(apiBaseUrl))
    {
        var boardId = Environment.GetEnvironmentVariable(""BOARD_ID"");
        var endpointUrl = $""{apiBaseUrl.TrimEnd('/')}/api/Mentor/runtime-error"";
        
        Task.Run(async () =>
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                
                // Get stack trace line number
                int? lineNumber = null;
                var stackTrace = new System.Diagnostics.StackTrace(startupEx, true);
                var frame = stackTrace.GetFrame(0);
                if (frame?.GetFileLineNumber() > 0)
                {
                    lineNumber = frame.GetFileLineNumber();
                }
                
                var payload = new
                {
                    boardId = boardId,
                    timestamp = DateTime.UtcNow,
                    file = startupEx.Source,
                    line = lineNumber,
                    stackTrace = startupEx.StackTrace,
                    message = startupEx.Message,
                    exceptionType = startupEx.GetType().Name,
                    requestPath = ""STARTUP"",
                    requestMethod = ""STARTUP"",
                    userAgent = ""STARTUP_ERROR""
                };
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, ""application/json"");
                await httpClient.PostAsync(endpointUrl, content);
            }
            catch { /* Ignore */ }
        });
    }
    
    throw; // Re-throw to exit with error code
}
";

        // appsettings.json
        files["backend/appsettings.json"] = @"{
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Warning"",
      ""Microsoft.AspNetCore"": ""Warning"",
      ""Microsoft.EntityFrameworkCore"": ""Warning""
    }
  },
  ""AllowedHosts"": ""*""
}
";

        // .csproj file
        files["backend/Backend.csproj"] = @"<Project Sdk=""Microsoft.NET.Sdk.Web"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Npgsql"" Version=""8.0.0"" />
    <PackageReference Include=""Swashbuckle.AspNetCore"" Version=""6.5.0"" />
  </ItemGroup>
</Project>
";

        // Note: Railway configuration files are generated separately based on programming language in GenerateRailwayConfigFiles
        // Structure:
        // - nixpacks.toml at REPO ROOT (Railway expects this at root, build commands use 'cd backend &&' to access backend files)

        // Note: We don't create a backend-specific README.md here to avoid conflict with root README.md
        // The root README.md contains project overview, backend-specific docs can be added later

        return files;
    }
    
    /// <summary>
    /// Generates Railway configuration files (nixpacks.toml) based on programming language
    /// </summary>
    private Dictionary<string, string> GenerateRailwayConfigFiles(string programmingLanguage)
    {
        var files = new Dictionary<string, string>();
        
        switch (programmingLanguage?.ToLowerInvariant())
        {
            case "c#":
            case "csharp":
                // .NET/C# backend - backend files are in backend/ folder
                // Railway expects nixpacks.toml at REPO ROOT, so we put it there
                // The build commands will cd into backend/ directory
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - .NET/C# Backend
# Backend files are in backend/ folder, so we cd into it for all commands

[phases.setup]
nixPkgs = { dotnet = ""8.0"" }

[phases.install]
cmds = [
  ""cd backend && dotnet restore Backend.csproj""
]

[phases.build]
cmds = [
  ""cd backend && dotnet publish Backend.csproj -c Release -o /app/publish""
]

[start]
cmd = ""dotnet /app/publish/Backend.dll""
";
                break;
                
            case "python":
                // Python/FastAPI backend - backend files are in backend/ folder
                // Railway expects nixpacks.toml at REPO ROOT
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - Python/FastAPI Backend
# Railway will build from repo root, then cd into backend/ directory
# Note: No detectors specified - we provide explicit phases

[phases.setup]
nixPkgs = { python = ""3.12"" }

[phases.install]
cmds = [
  ""cd backend && python -m py_compile $(find . -name '*.py')"",
  ""cd backend && pip install -r requirements.txt""
]

[phases.build]
cmds = [
  ""cd backend && python validate_imports.py""
]

[start]
cmd = ""cd backend && python check_syntax.py && uvicorn main:app --host 0.0.0.0 --port $PORT""
";
                break;
                
            case "nodejs":
            case "node.js":
            case "node":
                // Node.js/Express backend - backend files are in backend/ folder
                // Railway expects nixpacks.toml at REPO ROOT
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - Node.js/Express Backend
# Railway will build from repo root, then cd into backend/ directory
# Note: No detectors specified - we provide explicit phases

[phases.setup]
nixPkgs = { node = ""18"" }

[phases.install]
cmds = [
  ""cd backend && find . -name '*.js' -type f ! -path './node_modules/*' -exec node --check {} \; || (echo 'Syntax errors found in JavaScript files!' && exit 1)"",
  ""cd backend && npm install""
]

[phases.build]
cmds = [
  ""echo 'Node.js syntax validation passed'""
]

[start]
cmd = ""cd backend && node app.js""
";
                break;
                
            case "java":
                // Java/Spring Boot backend - backend files are in backend/ folder
                // Railway expects nixpacks.toml at REPO ROOT
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - Java/Spring Boot Backend
# Railway will build from repo root, then cd into backend/ directory
# Note: No detectors specified - we provide explicit phases

[phases.setup]
nixPkgs = { jdk = ""17"" }

[phases.install]
cmds = [
  ""cd backend && mvn clean install || echo 'Maven not found, assuming Gradle'""
]

[phases.build]
cmds = [
  ""cd backend && mvn package || gradle build || echo 'Build system not detected'""
]

[start]
cmd = ""cd backend && java -jar target/*.jar""
";
                break;
                
            case "php":
                // PHP backend - backend files are in backend/ folder
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - PHP Backend
# Backend files are in backend/ folder

[phases.setup]
nixPkgs = { php = ""8.2"" }

[phases.install]
cmds = [
  ""cd backend && composer install --no-dev --optimize-autoloader""
]

[phases.build]
cmds = [
  ""set -e"",
  ""cd backend"",
  ""find . -name '*.php' -print0 | xargs -0 php -l""
]

[start]
cmd = ""cd backend && php -d display_errors=1 -S 0.0.0.0:$PORT index.php""
";
                break;
                
            case "ruby":
                // Ruby/Sinatra backend - backend files are in backend/ folder
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - Ruby/Sinatra Backend
# Backend files are in backend/ folder

[phases.setup]
nixPkgs = { ruby = ""3.3"" }

[phases.install]
cmds = [
  ""cd backend && rm -f Gemfile.lock && bundle install""
]

[phases.build]
cmds = [
  ""cd backend && ruby build_check.rb""
]

[start]
cmd = ""cd backend && ruby app.rb""
";
                break;
                
            case "go":
            case "golang":
                // Go backend - backend files are in backend/ folder
                // Building to root level for Railway compatibility (Railway expects binaries at root)
                // Disable auto-detection by providing explicit phases and providers
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - Go Backend
# Backend files are in backend/ folder

[variables]
GO_VERSION = ""1.21""

[phases.setup]
nixPkgs = { go = ""1.21"" }

[phases.install]
cmds = [
  ""cd backend && go mod download && go mod tidy""
]

[phases.build]
cmds = [
  ""cd backend && go build -o ../backend main.go""
]

[start]
cmd = ""./backend""
";
                break;
                
            default:
                // Default to .NET if language not recognized - backend files are in backend/ folder
                // Railway expects nixpacks.toml at REPO ROOT
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - Default (.NET)
# Railway will build from repo root, then cd into backend/ directory
# Note: No detectors specified - we provide explicit phases

[phases.setup]
nixPkgs = { csharp = ""8.0"" }

[phases.install]
cmds = [
  ""cd backend && dotnet restore Backend.csproj""
]

[phases.build]
cmds = [
  ""cd backend && dotnet publish Backend.csproj -c Release -o /app/publish""
]

[start]
cmd = ""dotnet /app/publish/Backend.dll""
";
                break;
        }
        
        return files;
    }

    /// <summary>
    /// Generates backend files at root level (no "backend/" prefix) for separate backend repository
    /// </summary>
    private Dictionary<string, string> GenerateBackendFilesAtRoot(string programmingLanguage, string? webApiUrl)
    {
        // First generate files with "backend/" prefix using existing method
        var filesWithPrefix = GenerateBackendFiles(programmingLanguage, webApiUrl);
        
        // Convert to root-level paths by removing "backend/" prefix
        var filesAtRoot = new Dictionary<string, string>();
        foreach (var file in filesWithPrefix)
        {
            var rootPath = file.Key.StartsWith("backend/") 
                ? file.Key.Substring("backend/".Length) 
                : file.Key;
            filesAtRoot[rootPath] = file.Value;
        }
        
        // Also generate Railway config files at root (update nixpacks.toml to not use "cd backend &&")
        var railwayConfigFiles = GenerateRailwayConfigFilesAtRoot(programmingLanguage);
        foreach (var file in railwayConfigFiles)
        {
            filesAtRoot[file.Key] = file.Value;
        }
        
        return filesAtRoot;
    }

    /// <summary>
    /// Generates Railway configuration files for root-level backend files (no "backend/" subdirectory)
    /// </summary>
    private Dictionary<string, string> GenerateRailwayConfigFilesAtRoot(string programmingLanguage)
    {
        var files = new Dictionary<string, string>();
        
        switch (programmingLanguage?.ToLowerInvariant())
        {
            case "c#":
            case "csharp":
                // .NET/C# backend - files are at root, no need to cd
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - .NET/C# Backend
# Backend files are at root level

[phases.setup]
nixPkgs = { dotnet = ""8.0"" }

[phases.install]
cmds = [
  ""dotnet restore Backend.csproj""
]

[phases.build]
cmds = [
  ""dotnet publish Backend.csproj -c Release -o /app/publish""
]

[start]
cmd = ""dotnet /app/publish/Backend.dll""
";
                break;
                
            case "python":
                // Python/FastAPI backend - files are at root
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - Python/FastAPI Backend
# Backend files are at root level

[phases.setup]
nixPkgs = { python = ""3.12"" }

[phases.install]
cmds = [
  ""python -m py_compile $(find . -name '*.py')"",
  ""pip install -r requirements.txt""
]

[phases.build]
cmds = [
  ""python validate_imports.py""
]

[start]
cmd = ""python check_syntax.py && python -m uvicorn main:app --host 0.0.0.0 --port $PORT --lifespan on""
";
                break;
                
            case "nodejs":
            case "node.js":
            case "node":
                // Node.js/Express backend - files are at root
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - Node.js/Express Backend
# Backend files are at root level

[phases.setup]
nixPkgs = { node = ""18"" }

[phases.install]
cmds = [
  ""find . -name '*.js' -type f ! -path './node_modules/*' -exec node --check {} \; || (echo 'Syntax errors found in JavaScript files!' && exit 1)"",
  ""npm install""
]

[phases.build]
cmds = [
  ""echo 'Node.js syntax validation passed'""
]

[start]
cmd = ""node app.js""
";
                break;
                
            case "java":
                // Java/Spring Boot backend - files are at root
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - Java/Spring Boot Backend
# Backend files are at root level

[phases.setup]
nixPkgs = { jdk = ""17"" }

[phases.install]
cmds = [
  ""mvn clean install || echo 'Maven not found, assuming Gradle'""
]

[phases.build]
cmds = [
  ""mvn package || gradle build || echo 'Build system not detected'""
]

[start]
cmd = ""java -jar target/*.jar""
";
                break;
                
            case "php":
                // PHP backend - files are at root
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - PHP Backend
# Backend files are at root level

[phases.setup]
nixPkgs = { php = ""8.2"" }

[phases.install]
cmds = [
  ""composer install --no-dev --optimize-autoloader""
]

[phases.build]
cmds = [
  ""set -e"",
  ""find . -name '*.php' -print0 | xargs -0 php -l""
]

[start]
cmd = ""php -d display_errors=1 -S 0.0.0.0:$PORT index.php""
";
                break;
                
            case "ruby":
                // Ruby/Sinatra backend - files are at root
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - Ruby/Sinatra Backend
# Backend files are at root level

[phases.setup]
nixPkgs = { ruby = ""3.3"" }

[phases.install]
cmds = [
  ""rm -f Gemfile.lock && bundle install""
]

[phases.build]
cmds = [
  ""bundle install"",
  ""ruby build_check.rb""
]

[start]
cmd = ""ruby app.rb""
";
                break;
                
            case "go":
            case "golang":
                // Go backend - files are at root
                // Using relative paths for Railway compatibility
                // Disable auto-detection by providing explicit phases
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - Go Backend
# Backend files are at root level

[variables]
GO_VERSION = ""1.21""

[phases.setup]
nixPkgs = { go = ""1.21"" }

[phases.install]
cmds = [
  ""go mod download && go mod tidy""
]

[phases.build]
cmds = [
  ""go build -o backend main.go""
]

[start]
cmd = ""./backend""
";
                break;
                
            default:
                // Default to .NET if language not recognized
                files["nixpacks.toml"] = @"# Nixpacks configuration for Railway - Default (.NET)
# Backend files are at root level

[phases.setup]
nixPkgs = { dotnet = ""8.0"" }

[phases.install]
cmds = [
  ""dotnet restore Backend.csproj""
]

[phases.build]
cmds = [
  ""dotnet publish Backend.csproj -c Release -o /app/publish""
]

[start]
cmd = ""dotnet /app/publish/Backend.dll""
";
                break;
        }
        
        return files;
    }

    private Dictionary<string, string> GeneratePythonBackend(string? webApiUrl)
    {
        var files = new Dictionary<string, string>();

        // Models/TestProjects.py
        files["backend/Models/TestProjects.py"] = @"from pydantic import BaseModel
from typing import Optional

class TestProjects(BaseModel):
    id: Optional[int] = None
    name: str
";

        // Controllers/TestController.py
        files["backend/Controllers/TestController.py"] = @"from fastapi import APIRouter, HTTPException
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from Models.TestProjects import TestProjects
from psycopg import AsyncConnection
from psycopg.rows import dict_row

router = APIRouter(prefix=""/api/test"", tags=[""test""])

async def get_db_connection():
    """"""Get a database connection - only called when endpoint is accessed, not on import""""""
    connection_string = os.getenv(""DATABASE_URL"")
    if not connection_string:
        raise HTTPException(status_code=500, detail=""DATABASE_URL environment variable not set"")
    try:
        # Use async connection for FastAPI async endpoints
        # psycopg3 async connection - AsyncConnection.connect is the correct method
        conn = await AsyncConnection.connect(connection_string, row_factory=dict_row)
        return conn
    except Exception as e:
        error_msg = f""Database connection error: {str(e)}""
        print(error_msg)
        import traceback
        print(traceback.format_exc())
        raise HTTPException(status_code=503, detail=error_msg)

@router.get(""/"")
async def get_all():
    conn = None
    try:
        conn = await get_db_connection()
        async with conn.cursor() as cur:
            # Set search_path to public schema (required because isolated role has restricted search_path)
            await cur.execute('SET search_path = public, ""$user""')
            await cur.execute('SELECT ""Id"", ""Name"" FROM ""TestProjects"" ORDER BY ""Id""')
            results = await cur.fetchall()
            await conn.commit()
            return results
    finally:
        if conn:
            await conn.close()
    # Do NOT catch generic Exception - let it bubble up to global exception handler

@router.get(""/{id}"")
async def get(id: int):
    conn = None
    try:
        conn = await get_db_connection()
        async with conn.cursor() as cur:
            # Set search_path to public schema (required because isolated role has restricted search_path)
            await cur.execute('SET search_path = public, ""$user""')
            await cur.execute('SELECT ""Id"", ""Name"" FROM ""TestProjects"" WHERE ""Id"" = %s', (id,))
            result = await cur.fetchone()
            if not result:
                raise HTTPException(status_code=404, detail=""Project not found"")
            await conn.commit()
            return result
    except HTTPException:
        raise
    finally:
        if conn:
            await conn.close()
    # Do NOT catch generic Exception - let it bubble up to global exception handler

@router.post(""/"")
async def create(project: TestProjects):
    conn = None
    try:
        conn = await get_db_connection()
        async with conn.cursor() as cur:
            # Set search_path to public schema (required because isolated role has restricted search_path)
            await cur.execute('SET search_path = public, ""$user""')
            await cur.execute('INSERT INTO ""TestProjects"" (""Name"") VALUES (%s) RETURNING ""Id""', (project.name,))
            result = await cur.fetchone()
            project_id = result[""Id""]
            await conn.commit()
            project.id = project_id
            return project
    finally:
        if conn:
            await conn.close()
    # Do NOT catch generic Exception - let it bubble up to global exception handler

@router.put(""/{id}"")
async def update(id: int, project: TestProjects):
    conn = None
    try:
        conn = await get_db_connection()
        async with conn.cursor() as cur:
            # Set search_path to public schema (required because isolated role has restricted search_path)
            await cur.execute('SET search_path = public, ""$user""')
            await cur.execute('UPDATE ""TestProjects"" SET ""Name"" = %s WHERE ""Id"" = %s', (project.name, id))
            if cur.rowcount == 0:
                raise HTTPException(status_code=404, detail=""Project not found"")
            await conn.commit()
            return {""message"": ""Updated successfully""}
    except HTTPException:
        raise
    finally:
        if conn:
            await conn.close()
    # Do NOT catch generic Exception - let it bubble up to global exception handler

@router.delete(""/{id}"")
async def delete(id: int):
    conn = None
    try:
        conn = await get_db_connection()
        async with conn.cursor() as cur:
            # Set search_path to public schema (required because isolated role has restricted search_path)
            await cur.execute('SET search_path = public, ""$user""')
            await cur.execute('DELETE FROM ""TestProjects"" WHERE ""Id"" = %s', (id,))
            if cur.rowcount == 0:
                raise HTTPException(status_code=404, detail=""Project not found"")
            await conn.commit()
            return {""message"": ""Deleted successfully""}
    except HTTPException:
        raise
    finally:
        if conn:
            await conn.close()
    # Do NOT catch generic Exception - let it bubble up to global exception handler
";

        // logging_config.py - Logging configuration
        files["backend/logging_config.py"] = @"import logging
import sys

# Configure logging - Warning and Error only
logging.basicConfig(
    level=logging.WARNING,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(sys.stdout)
    ]
)

# Set specific loggers to WARNING
logging.getLogger('uvicorn').setLevel(logging.WARNING)
logging.getLogger('uvicorn.access').setLevel(logging.WARNING)
logging.getLogger('fastapi').setLevel(logging.WARNING)
";

        // ExceptionHandler.py - Global exception handler for runtime error reporting
        files["backend/ExceptionHandler.py"] = @"import os
import re
import traceback
import logging
import asyncio
from typing import Optional
from starlette.requests import Request
from starlette.responses import JSONResponse
from fastapi import FastAPI
from fastapi.exceptions import RequestValidationError
from starlette.exceptions import HTTPException as StarletteHTTPException
import httpx

logger = logging.getLogger(__name__)

def extract_board_id(request: Request) -> Optional[str]:
    """"""Extract boardId from request (route params, query string, headers, env, hostname)""""""
    # Try route parameters
    if hasattr(request, 'path_params') and 'boardId' in request.path_params:
        board_id = request.path_params['boardId']
        logger.warning(f'[EXCEPTION HANDLER] Extracted boardId from route params: {board_id}')
        return board_id
    
    # Try query parameters
    if 'boardId' in request.query_params:
        board_id = request.query_params['boardId']
        logger.warning(f'[EXCEPTION HANDLER] Extracted boardId from query params: {board_id}')
        return board_id
    
    # Try headers
    if 'X-Board-Id' in request.headers:
        board_id = request.headers['X-Board-Id']
        logger.warning(f'[EXCEPTION HANDLER] Extracted boardId from header: {board_id}')
        return board_id
    
    # Try environment variable
    board_id = os.getenv('BOARD_ID')
    if board_id and board_id.strip():
        logger.warning(f'[EXCEPTION HANDLER] Extracted boardId from BOARD_ID env var: {board_id}')
        return board_id.strip()
    else:
        # Log the actual value to help debug
        board_id_raw = os.getenv('BOARD_ID')
        logger.warning(f'[EXCEPTION HANDLER] BOARD_ID environment variable check failed. Raw value: {repr(board_id_raw)}')
    
    # Try to extract from hostname (Railway pattern: webapi{boardId}.up.railway.app - no hyphen)
    host = request.headers.get('host', '')
    if host:
        logger.warning(f'[EXCEPTION HANDLER] Checking hostname for boardId: {host}')
        match = re.search(r'webapi([a-f0-9]{24})', host, re.IGNORECASE)
        if match:
            board_id = match.group(1)
            logger.warning(f'[EXCEPTION HANDLER] Extracted boardId from hostname: {board_id}')
            return board_id
    
    # Try to extract from RUNTIME_ERROR_ENDPOINT_URL if it contains boardId pattern
    endpoint_url = os.getenv('RUNTIME_ERROR_ENDPOINT_URL', '')
    if endpoint_url:
        logger.warning(f'[EXCEPTION HANDLER] Checking RUNTIME_ERROR_ENDPOINT_URL for boardId: {endpoint_url}')
        match = re.search(r'webapi([a-f0-9]{24})', endpoint_url, re.IGNORECASE)
        if match:
            board_id = match.group(1)
            logger.warning(f'[EXCEPTION HANDLER] Extracted boardId from RUNTIME_ERROR_ENDPOINT_URL: {board_id}')
            return board_id
    else:
        logger.warning('[EXCEPTION HANDLER] RUNTIME_ERROR_ENDPOINT_URL environment variable not set')
    
    logger.warning('[EXCEPTION HANDLER] Could not extract boardId from any source')
    return None

async def send_error_to_endpoint(endpoint_url: str, board_id: Optional[str], request: Request, exception: Exception):
    """"""Send error details to runtime error endpoint (fire and forget)""""""
    try:
        # Extract exception details
        exc_type = type(exception).__name__
        exc_message = str(exception) if exception else 'Unknown error'
        exc_traceback = ''.join(traceback.format_exception(type(exception), exception, exception.__traceback__))
        
        # Get file and line from traceback
        tb_lines = traceback.extract_tb(exception.__traceback__)
        file_name = tb_lines[-1].filename if tb_lines else None
        line_number = tb_lines[-1].lineno if tb_lines else None
        
        # Get current UTC timestamp (ISO 8601 format for C# DateTime parsing)
        from datetime import datetime, timezone
        current_timestamp = datetime.now(timezone.utc).isoformat()
        
        # Build payload - send even if boardId is None (endpoint will handle it)
        # Ensure boardId is a string (not None) for JSON serialization
        # Timestamp must be a valid DateTime string (not None) - C# model requires non-nullable DateTime
        payload = {
            'boardId': board_id if board_id else '',  # Convert None to empty string for JSON
            'timestamp': current_timestamp,  # Send current UTC time (ISO 8601 format)
            'file': file_name,
            'line': line_number,
            'stackTrace': exc_traceback,
            'message': exc_message if exc_message else 'Unknown error',
            'exceptionType': exc_type if exc_type else 'Exception',
            'requestPath': str(request.url.path) if request.url.path else '',
            'requestMethod': request.method if request.method else 'UNKNOWN',
            'userAgent': request.headers.get('user-agent') if request.headers.get('user-agent') else None
        }
        
        # Send in background (fire and forget)
        async with httpx.AsyncClient(timeout=5.0) as client:
            try:
                response = await client.post(endpoint_url, json=payload)
                if response.status_code != 200:
                    response_body = await response.aread()
                    logger.error(f'[EXCEPTION HANDLER] Error endpoint response: {response.status_code} - {response_body.decode(""utf-8"", errors=""ignore"")}')
                else:
                    logger.warning(f'[EXCEPTION HANDLER] Error endpoint response: {response.status_code}')
            except Exception as e:
                logger.error(f'[EXCEPTION HANDLER] Failed to send error to endpoint: {e}')
    except Exception as e:
        logger.error(f'[EXCEPTION HANDLER] Error in send_error_to_endpoint: {e}')

async def global_exception_handler(request: Request, exc: Exception):
    """"""Global exception handler for all unhandled exceptions""""""
    logger.error(f'[EXCEPTION HANDLER] Unhandled exception occurred: {exc}', exc_info=True)
    
    # Extract boardId
    board_id = extract_board_id(request)
    logger.warning(f'[EXCEPTION HANDLER] Extracted boardId: {board_id if board_id else ""NULL""}')
    
    # Send error to runtime error endpoint if configured
    runtime_error_endpoint_url = os.getenv('RUNTIME_ERROR_ENDPOINT_URL')
    if runtime_error_endpoint_url:
        logger.warning(f'[EXCEPTION HANDLER] Sending error to endpoint: {runtime_error_endpoint_url} (boardId: {board_id if board_id else ""NULL""})')
        # Fire and forget - don't await (send even if boardId is None - endpoint will handle it)
        asyncio.create_task(send_error_to_endpoint(runtime_error_endpoint_url, board_id, request, exc))
    else:
        logger.warning('[EXCEPTION HANDLER] RUNTIME_ERROR_ENDPOINT_URL is not set - skipping error reporting')
    
    # Return error response
    return JSONResponse(
        status_code=500,
        content={
            'error': 'An error occurred while processing your request',
            'message': str(exc) if exc else 'Unknown error'
        }
    )

def setup_exception_handlers(app: FastAPI):
    """"""Setup global exception handlers""""""
    # Handle all exceptions (most generic handler)
    app.add_exception_handler(Exception, global_exception_handler)
";

        // main.py
        files["backend/main.py"] = @"import os
import sys
import asyncio
import traceback
import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

# Import logging configuration
import logging_config

# Add current directory to path for imports (where main.py is located)
current_dir = os.path.dirname(os.path.abspath(__file__))
if current_dir not in sys.path:
    sys.path.insert(0, current_dir)

# Note: Models and Controllers are subdirectories of current_dir (root level),
# so they can be imported directly when current_dir is in sys.path

# Get logger
logger = logging.getLogger(__name__)

# Lifespan context manager to handle startup/shutdown gracefully
@asynccontextmanager
async def lifespan(app: FastAPI):
    """"""Handle application lifespan events - startup and shutdown""""""
    # Startup
    logger.warning(""Starting Backend API..."")
    try:
        yield
    except asyncio.CancelledError:
        # Gracefully handle cancellation during shutdown
        logger.warning(""Application shutdown requested"")
        raise
    finally:
        # Shutdown
        logger.warning(""Shutting down Backend API..."")

app = FastAPI(title=""Backend API"", version=""1.0.0"", lifespan=lifespan)

# Setup global exception handlers FIRST (before other middleware)
from ExceptionHandler import setup_exception_handlers
setup_exception_handlers(app)

# CORS configuration - allow all origins for GitHub Pages deployments
app.add_middleware(
    CORSMiddleware,
    allow_origins=[""*""],  # Allow all origins (GitHub Pages uses *.github.io)
    allow_credentials=True,
    allow_methods=[""*""],  # Allow all HTTP methods (GET, POST, PUT, DELETE, OPTIONS)
    allow_headers=[""*""],  # Allow all headers (for CORS preflight)
)

# Import and register router - let it crash if imports fail (this is a build-time error, not runtime)
from Controllers.TestController import router as test_router
app.include_router(test_router)

@app.get(""/"")
async def root():
    return {
        ""message"": ""Backend API is running"",
        ""status"": ""ok"",
        ""swagger"": ""/docs"",
        ""api"": ""/api/test""
    }

@app.get(""/swagger"")
async def swagger_redirect():
    """"""Redirect /swagger to /docs (FastAPI Swagger UI)""""""
    from fastapi.responses import RedirectResponse
    return RedirectResponse(url=""/docs"")

@app.get(""/health"")
async def health():
    """"""Health check endpoint that doesn't require database""""""
    return {
        ""status"": ""healthy"",
        ""service"": ""Backend API""
    }

if __name__ == ""__main__"":
    import uvicorn
    import asyncio
    import traceback
    import httpx
    try:
        port = int(os.getenv(""PORT"", 8000))
        logger.warning(f""Starting server on 0.0.0.0:{port}"")
        # Use lifespan='on' to explicitly enable lifespan handling
        # Configure uvicorn to use WARNING log level
        uvicorn.run(app, host=""0.0.0.0"", port=port, lifespan=""on"", log_level=""warning"")
    except Exception as startup_ex:
        logger.error(f""[STARTUP ERROR] Application failed to start: {startup_ex}"", exc_info=True)
        
        # Send startup error to endpoint (fire and forget)
        runtime_error_endpoint_url = os.getenv(""RUNTIME_ERROR_ENDPOINT_URL"")
        board_id = os.getenv(""BOARD_ID"")
        
        if runtime_error_endpoint_url:
            try:
                # Extract exception details for startup error
                exc_type = type(startup_ex).__name__
                exc_message = str(startup_ex) if startup_ex else 'Unknown error'
                exc_traceback = ''.join(traceback.format_exception(type(startup_ex), startup_ex, startup_ex.__traceback__))
                
                # Get file and line from traceback
                tb_lines = traceback.extract_tb(startup_ex.__traceback__)
                file_name = tb_lines[-1].filename if tb_lines else None
                line_number = tb_lines[-1].lineno if tb_lines else None
                
                # Build payload for startup error
                payload = {
                    'boardId': board_id,
                    'timestamp': None,  # Will be set by backend
                    'file': file_name,
                    'line': line_number,
                    'stackTrace': exc_traceback,
                    'message': exc_message,
                    'exceptionType': exc_type,
                    'requestPath': 'STARTUP',
                    'requestMethod': 'STARTUP',
                    'userAgent': 'STARTUP_ERROR'
                }
                
                # Send in background (fire and forget) - use threading for sync context
                import threading
                def send_startup_error():
                    try:
                        with httpx.Client(timeout=5.0) as client:
                            client.post(runtime_error_endpoint_url, json=payload)
                    except:
                        pass
                threading.Thread(target=send_startup_error, daemon=True).start()
            except Exception as send_ex:
                # Ignore errors in sending startup error
                pass
        
        raise  # Re-raise to exit with error code
";

        // requirements.txt
        files["backend/requirements.txt"] = @"fastapi==0.115.0
uvicorn==0.32.0
psycopg[binary]==3.2.2
pydantic==2.9.0
httpx==0.27.0
";

        // validate_imports.py - Import validation script for build phase
        files["backend/validate_imports.py"] = @"#!/usr/bin/env python3
""""""Import validation script for Python backend
This script validates that all controllers can be imported without errors.
Run during build phase to catch import-time errors before deployment.
""""""
import sys
import os

# Add current directory to path for imports
current_dir = os.path.dirname(os.path.abspath(__file__))
if current_dir not in sys.path:
    sys.path.insert(0, current_dir)

# Import all controllers that should be available
try:
    from Controllers.TestController import router as test_router
    print(""✓ Successfully imported Controllers.TestController"")
except Exception as e:
    print(f""✗ Failed to import Controllers.TestController: {e}"")
    import traceback
    traceback.print_exc()
    sys.exit(1)

print(""✓ All imports validated successfully"")
sys.exit(0)
";

        // check_syntax.py - Syntax check script that runs before uvicorn starts
        // This catches syntax errors BEFORE uvicorn tries to import modules
        // and sends them to the runtime error endpoint
        files["backend/check_syntax.py"] = @"#!/usr/bin/env python3
""""""Syntax check script for Python backend
This script checks syntax of all Python files before uvicorn starts.
If syntax errors are found, they are sent to the runtime error endpoint.
Run during startup phase to catch syntax errors before import.
""""""
import os
import sys
import ast
import traceback
import httpx
import threading

def check_syntax(file_path):
    """"""Check syntax of a Python file""""""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            source = f.read()
        # Use compile() instead of ast.parse() to catch all syntax errors including indentation
        compile(source, file_path, 'exec', flags=0, dont_inherit=True)
        return None
    except SyntaxError as e:
        # Extract just the filename from the full path
        file_name = os.path.basename(file_path)
        return {
            'file': file_name,
            'line': e.lineno,
            'message': str(e.msg) if hasattr(e, 'msg') else str(e),
            'text': e.text,
            'offset': e.offset
        }
    except Exception as e:
        file_name = os.path.basename(file_path)
        return {
            'file': file_name,
            'line': None,
            'message': str(e),
            'text': None,
            'offset': None
        }

def send_syntax_error_to_endpoint(errors):
    """"""Send syntax errors to runtime error endpoint""""""
    runtime_error_endpoint_url = os.getenv('RUNTIME_ERROR_ENDPOINT_URL')
    board_id = os.getenv('BOARD_ID')
    
    if not runtime_error_endpoint_url:
        return
    
    # Format error message
    error_messages = []
    for err in errors:
        msg = f""File: {err['file']}, Line: {err['line']}, Error: {err['message']}""
        if err['text']:
            msg += f"", Code: {err['text'].strip()}""
        error_messages.append(msg)
    
    error_message = '; '.join(error_messages)
    
    # Build stack trace
    stack_trace = '\n'.join([
        f""  File ""{err['file']}"", line {err['line'] or '?'}""
        for err in errors
    ])
    
    payload = {
        'boardId': board_id,
        'timestamp': None,  # Will be set by backend
        'file': errors[0]['file'] if errors else None,
        'line': errors[0]['line'] if errors else None,
        'stackTrace': stack_trace,
        'message': error_message,
        'exceptionType': 'SyntaxError',
        'requestPath': 'SYNTAX_CHECK',
        'requestMethod': 'SYNTAX_CHECK',
        'userAgent': 'SYNTAX_CHECKER'
    }
    
    # Send in background (fire and forget)
    def send_error():
        try:
            with httpx.Client(timeout=5.0) as client:
                client.post(runtime_error_endpoint_url, json=payload)
        except:
            pass
    
    thread = threading.Thread(target=send_error, daemon=True)
    thread.start()
    thread.join(timeout=2.0)  # Wait max 2 seconds for send

if __name__ == '__main__':
    # Get backend directory
    backend_dir = os.path.dirname(os.path.abspath(__file__))
    
    # Find all Python files
    python_files = []
    for root, dirs, files in os.walk(backend_dir):
        # Skip virtual environment
        if '.venv' in root or 'venv' in root or '__pycache__' in root:
            continue
        for file in files:
            if file.endswith('.py'):
                python_files.append(os.path.join(root, file))
    
    # Check syntax of all files
    errors = []
    print(f'Checking syntax of {len(python_files)} Python files...', file=sys.stderr)
    for file_path in python_files:
        error = check_syntax(file_path)
        if error:
            errors.append(error)
            print(f""✗ Syntax error in {file_path}: {error['message']} at line {error['line']}"", file=sys.stderr)
        else:
            print(f'✓ {os.path.basename(file_path)}', file=sys.stderr)
    
    if errors:
        print(f'✗ Found {len(errors)} syntax error(s). Sending to runtime error endpoint...', file=sys.stderr)
        # Send errors to endpoint
        send_syntax_error_to_endpoint(errors)
        print('✗ Syntax check FAILED. Exiting with error code 1.', file=sys.stderr)
        sys.exit(1)
    else:
        print('✓ Syntax check passed. All files are valid.', file=sys.stderr)
        sys.exit(0)
";

        // Note: README.md is created at root level, not here to avoid conflicts

        return files;
    }

    private Dictionary<string, string> GenerateNodeJSBackend(string? webApiUrl)
    {
        var files = new Dictionary<string, string>();

        // Models/TestProjects.js
        files["backend/Models/TestProjects.js"] = @"class TestProjects {
    constructor(id, name) {
        this.id = id;
        this.name = name;
    }
}

module.exports = TestProjects;
";

        // Controllers/TestController.js
        files["backend/Controllers/TestController.js"] = @"const { Pool } = require('pg');

/**
 * @swagger
 * components:
 *   schemas:
 *     TestProject:
 *       type: object
 *       required:
 *         - name
 *       properties:
 *         id:
 *           type: integer
 *           description: The auto-generated id of the project
 *         name:
 *           type: string
 *           description: The name of the project
 */

const pool = new Pool({
    connectionString: process.env.DATABASE_URL,
    ssl: process.env.DATABASE_URL ? { rejectUnauthorized: false } : false
});

/**
 * @swagger
 * /api/test:
 *   get:
 *     summary: Get all test projects
 *     tags: [Test]
 *     responses:
 *       200:
 *         description: List of all test projects
 *         content:
 *           application/json:
 *             schema:
 *               type: array
 *               items:
 *                 $ref: '#/components/schemas/TestProject'
 */
const getAll = async (req, res, next) => {
    // Set search_path to public schema (required because isolated role has restricted search_path)
    await pool.query('SET search_path = public, ""$user""');
        const result = await pool.query('SELECT ""Id"", ""Name"" FROM ""TestProjects"" ORDER BY ""Id""');
        res.json(result.rows);
    // Do NOT catch generic errors - let them bubble up to global error handler middleware
};

/**
 * @swagger
 * /api/test/{id}:
 *   get:
 *     summary: Get a test project by ID
 *     tags: [Test]
 *     parameters:
 *       - in: path
 *         name: id
 *         schema:
 *           type: integer
 *         required: true
 *         description: The project ID
 *     responses:
 *       200:
 *         description: The project data
 *         content:
 *           application/json:
 *             schema:
 *               $ref: '#/components/schemas/TestProject'
 *       404:
 *         description: Project not found
 */
const getById = async (req, res, next) => {
    // Set search_path to public schema (required because isolated role has restricted search_path)
    await pool.query('SET search_path = public, ""$user""');
        const { id } = req.params;
        const result = await pool.query('SELECT ""Id"", ""Name"" FROM ""TestProjects"" WHERE ""Id"" = $1', [id]);
        if (result.rows.length === 0) {
            return res.status(404).json({ error: 'Project not found' });
        }
        res.json(result.rows[0]);
    // Do NOT catch generic errors - let them bubble up to global error handler middleware
};

/**
 * @swagger
 * /api/test:
 *   post:
 *     summary: Create a new test project
 *     tags: [Test]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             $ref: '#/components/schemas/TestProject'
 *     responses:
 *       201:
 *         description: The created project
 *         content:
 *           application/json:
 *             schema:
 *               $ref: '#/components/schemas/TestProject'
 */
const create = async (req, res, next) => {
    // Set search_path to public schema (required because isolated role has restricted search_path)
    await pool.query('SET search_path = public, ""$user""');
        const { name } = req.body;
        const result = await pool.query('INSERT INTO ""TestProjects"" (""Name"") VALUES ($1) RETURNING ""Id"", ""Name""', [name]);
        res.status(201).json(result.rows[0]);
    // Do NOT catch generic errors - let them bubble up to global error handler middleware
};

/**
 * @swagger
 * /api/test/{id}:
 *   put:
 *     summary: Update a test project
 *     tags: [Test]
 *     parameters:
 *       - in: path
 *         name: id
 *         schema:
 *           type: integer
 *         required: true
 *         description: The project ID
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             $ref: '#/components/schemas/TestProject'
 *     responses:
 *       200:
 *         description: The updated project
 *         content:
 *           application/json:
 *             schema:
 *               $ref: '#/components/schemas/TestProject'
 *       404:
 *         description: Project not found
 */
const update = async (req, res, next) => {
    // Set search_path to public schema (required because isolated role has restricted search_path)
    await pool.query('SET search_path = public, ""$user""');
        const { id } = req.params;
        const { name } = req.body;
        const result = await pool.query('UPDATE ""TestProjects"" SET ""Name"" = $1 WHERE ""Id"" = $2 RETURNING ""Id"", ""Name""', [name, id]);
        if (result.rows.length === 0) {
            return res.status(404).json({ error: 'Project not found' });
        }
        res.json(result.rows[0]);
    // Do NOT catch generic errors - let them bubble up to global error handler middleware
};

/**
 * @swagger
 * /api/test/{id}:
 *   delete:
 *     summary: Delete a test project
 *     tags: [Test]
 *     parameters:
 *       - in: path
 *         name: id
 *         schema:
 *           type: integer
 *         required: true
 *         description: The project ID
 *     responses:
 *       200:
 *         description: Success message
 *       404:
 *         description: Project not found
 */
const remove = async (req, res, next) => {
    // Set search_path to public schema (required because isolated role has restricted search_path)
    await pool.query('SET search_path = public, ""$user""');
        const { id } = req.params;
        const result = await pool.query('DELETE FROM ""TestProjects"" WHERE ""Id"" = $1', [id]);
        if (result.rowCount === 0) {
            return res.status(404).json({ error: 'Project not found' });
        }
        res.json({ message: 'Deleted successfully' });
    // Do NOT catch generic errors - let them bubble up to global error handler middleware
};

module.exports = {
    getAll,
    getById,
    create,
    update,
    remove
};
";

        // app.js
        files["backend/app.js"] = @"const express = require('express');
const cors = require('cors');
const swaggerUi = require('swagger-ui-express');
const swaggerJsdoc = require('swagger-jsdoc');
const winston = require('winston');
const testController = require('./Controllers/TestController');

// Configure logging - Warning and Error only
const logger = winston.createLogger({
  level: 'warn',
  format: winston.format.combine(
    winston.format.timestamp(),
    winston.format.json()
  ),
  transports: [
    new winston.transports.Console({
      format: winston.format.combine(
        winston.format.colorize(),
        winston.format.simple()
      )
    })
  ]
});

const app = express();
const PORT = process.env.PORT || 8080;

app.use(cors());
app.use(express.json());

// Swagger configuration
const swaggerOptions = {
    definition: {
        openapi: '3.0.0',
        info: {
            title: 'Backend API',
            version: '1.0.0',
            description: 'Backend API documentation'
        },
        servers: [
            {
                url: '/',
                description: 'Current server'
            }
        ]
    },
    apis: ['./Controllers/*.js']
};

const swaggerSpec = swaggerJsdoc(swaggerOptions);
app.use('/swagger', swaggerUi.serve, swaggerUi.setup(swaggerSpec));
app.use('/docs', swaggerUi.serve, swaggerUi.setup(swaggerSpec)); // Also support /docs like FastAPI

// Async wrapper to catch errors from async route handlers and pass them to error handler
const asyncHandler = (fn) => {
    return (req, res, next) => {
        Promise.resolve(fn(req, res, next)).catch(next);
    };
};

// Routes - wrap async handlers to catch errors
app.get('/api/test', asyncHandler(testController.getAll));
app.get('/api/test/:id', asyncHandler(testController.getById));
app.post('/api/test', asyncHandler(testController.create));
app.put('/api/test/:id', asyncHandler(testController.update));
app.delete('/api/test/:id', asyncHandler(testController.remove));

app.get('/', (req, res) => {
    res.json({ 
        message: 'Backend API is running',
        status: 'ok',
        swagger: '/swagger',
        api: '/api/test'
    });
});

app.get('/health', (req, res) => {
    res.json({ 
        status: 'healthy',
        service: 'Backend API'
    });
});

// Global error handler middleware - MUST be registered AFTER all routes
// This catches all unhandled errors and sends them to the runtime error endpoint
app.use((err, req, res, next) => {
    logger.error('[ERROR HANDLER] Unhandled error occurred:', err);
    
    // Extract boardId from request
    const boardId = extractBoardId(req);
    logger.warn(`[ERROR HANDLER] Extracted boardId: ${boardId || 'NULL'}`);
    
    // Send error to runtime error endpoint if configured
    const runtimeErrorEndpointUrl = process.env.RUNTIME_ERROR_ENDPOINT_URL;
    if (runtimeErrorEndpointUrl) {
        logger.warn(`[ERROR HANDLER] Sending error to endpoint: ${runtimeErrorEndpointUrl}`);
        sendErrorToEndpoint(runtimeErrorEndpointUrl, boardId, req, err).catch(err => {
            logger.error('[ERROR HANDLER] Failed to send error to endpoint:', err);
        });
    } else {
        logger.warn('[ERROR HANDLER] RUNTIME_ERROR_ENDPOINT_URL is not set - skipping error reporting');
    }
    
    // Return error response to client
    res.status(err.status || 500).json({
        error: 'An error occurred while processing your request',
        message: err.message || 'Unknown error'
    });
});

function extractBoardId(req) {
    // Try route parameters
    if (req.params && req.params.boardId) {
        return req.params.boardId;
    }
    
    // Try query parameters
    if (req.query && req.query.boardId) {
        return req.query.boardId;
    }
    
    // Try headers
    if (req.headers['x-board-id']) {
        return req.headers['x-board-id'];
    }
    
    // Try environment variable
    const boardIdEnv = process.env.BOARD_ID;
    if (boardIdEnv) {
        return boardIdEnv;
    }
    
    // Try to extract from hostname (Railway pattern: webapi{boardId}.up.railway.app - no hyphen)
    const host = req.get('host') || req.headers.host || '';
    const hostMatch = host.match(/webapi([a-f0-9]{24})/i);
    if (hostMatch) {
        return hostMatch[1];
    }
    
    // Try to extract from RUNTIME_ERROR_ENDPOINT_URL if it contains boardId pattern
    const endpointUrl = process.env.RUNTIME_ERROR_ENDPOINT_URL || '';
    const urlMatch = endpointUrl.match(/webapi([a-f0-9]{24})/i);
    if (urlMatch) {
        return urlMatch[1];
    }
    
    return null;
}

async function sendErrorToEndpoint(endpointUrl, boardId, req, error) {
    try {
        const http = require('http');
        const https = require('https');
        const { URL } = require('url');
        const url = new URL(endpointUrl);
        const client = url.protocol === 'https:' ? https : http;
        
        // Get stack trace
        const stack = error.stack || 'N/A';
        
        // Get file and line from stack
        // Node.js stack format: ""Error: message\n    at functionName (file:line:column)\n    at ...""
        const stackLines = stack.split('\\n');
        let fileName = null;
        let lineNumber = null;
        
        // Look for the first stack line that contains a file path (skip error message line)
        for (let i = 1; i < stackLines.length; i++) {
            const line = stackLines[i].trim();
            
            // Match pattern: ""at functionName (file:line:column)""
            // Example: ""at getAll (/app/Controllers/TestController.js:43:11)""
            // Find the last occurrence of :digits:digits) pattern to extract file:line:column
            const parenIndex = line.indexOf('(');
            const parenCloseIndex = line.indexOf(')', parenIndex);
            if (parenIndex >= 0 && parenCloseIndex > parenIndex) {
                const content = line.substring(parenIndex + 1, parenCloseIndex);
                // Match :digits:digits at the end
                const match = content.match(/(.+):(\\d+):(\\d+)$/);
                if (match && match[1] && match[2]) {
                    const filePath = match[1].trim();
                    // Extract just the filename
                    const lastSlash = filePath.lastIndexOf('/');
                    fileName = lastSlash >= 0 ? filePath.substring(lastSlash + 1) : filePath;
                    lineNumber = parseInt(match[2], 10);
                    if (fileName && !isNaN(lineNumber)) {
                        break;
                    }
                }
            }
            
            // Match pattern: ""at file:line:column"" (no function name, no parentheses)
            // Example: ""at /app/app.js:57:25""
            if (!fileName || isNaN(lineNumber)) {
                // Find pattern: at followed by file:line:column
                const atIndex = line.indexOf('at ');
                if (atIndex >= 0) {
                    const afterAt = line.substring(atIndex + 3).trim();
                    const match = afterAt.match(/^([^\\s]+):(\\d+):(\\d+)/);
                    if (match && match[1] && match[2]) {
                        const filePath = match[1].trim();
                        // Extract just the filename
                        const lastSlash = filePath.lastIndexOf('/');
                        fileName = lastSlash >= 0 ? filePath.substring(lastSlash + 1) : filePath;
                        lineNumber = parseInt(match[2], 10);
                        if (fileName && !isNaN(lineNumber)) {
                            break;
                        }
                    }
                }
            }
        }
        
        // Debug logging
        if (!fileName || isNaN(lineNumber)) {
            logger.warn(`[ERROR HANDLER] Failed to extract file/line from stack. Stack lines: ${stackLines.length}, First few lines: ${stackLines.slice(0, 3).join(' | ')}`);
        }
        
        const payload = JSON.stringify({
            boardId: boardId,
            timestamp: new Date().toISOString(),
            file: fileName,
            line: lineNumber,
            stackTrace: stack,
            message: error.message || 'Unknown error',
            exceptionType: error.name || 'Error',
            requestPath: req.path || req.url,
            requestMethod: req.method,
            userAgent: req.get('user-agent')
        });
        
        const options = {
            hostname: url.hostname,
            port: url.port || (url.protocol === 'https:' ? 443 : 80),
            path: url.pathname + url.search,
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Content-Length': Buffer.byteLength(payload)
            },
            timeout: 5000
        };
        
        return new Promise((resolve, reject) => {
            const req = client.request(options, (res) => {
                let data = '';
                res.on('data', chunk => data += chunk);
                res.on('end', () => {
                    logger.warn(`[ERROR HANDLER] Error endpoint response: ${res.statusCode} - ${data}`);
                    resolve();
                });
            });
            
            req.on('error', (err) => {
                logger.error('[ERROR HANDLER] Request error:', err);
                reject(err);
            });
            
            req.on('timeout', () => {
                req.destroy();
                reject(new Error('Request timeout'));
            });
            
            req.write(payload);
            req.end();
        });
    } catch (err) {
        logger.error('[ERROR HANDLER] Error in sendErrorToEndpoint:', err);
        throw err;
    }
}

app.listen(PORT, '0.0.0.0', (err) => {
    if (err) {
        logger.error(`[STARTUP ERROR] Failed to start server: ${err.message}`);
        
        // Send startup error to endpoint (fire and forget)
        const runtimeErrorEndpointUrl = process.env.RUNTIME_ERROR_ENDPOINT_URL;
        const boardId = process.env.BOARD_ID;
        
        if (runtimeErrorEndpointUrl) {
            const payload = JSON.stringify({
                boardId: boardId,
                timestamp: new Date().toISOString(),
                file: err.stack ? err.stack.split('\\n')[0] : null,
                line: null,
                stackTrace: err.stack || 'N/A',
                message: err.message || 'Unknown error',
                exceptionType: err.name || 'Error',
                requestPath: 'STARTUP',
                requestMethod: 'STARTUP',
                userAgent: 'STARTUP_ERROR'
            });
            
            const http = require('http');
            const https = require('https');
            const { URL } = require('url');
            const url = new URL(runtimeErrorEndpointUrl);
            const client = url.protocol === 'https:' ? https : http;
            
            const options = {
                hostname: url.hostname,
                port: url.port || (url.protocol === 'https:' ? 443 : 80),
                path: url.pathname + url.search,
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Content-Length': Buffer.byteLength(payload)
                },
                timeout: 5000
            };
            
            const req = client.request(options, () => {});
            req.on('error', () => {});
            req.write(payload);
            req.end();
        }
        
        process.exit(1);
    } else {
        logger.warn(`Server is running on 0.0.0.0:${PORT}`);
    }
});
";

        // package.json
        files["backend/package.json"] = @"{
  ""name"": ""backend"",
  ""version"": ""1.0.0"",
  ""description"": ""Backend API"",
  ""main"": ""app.js"",
  ""scripts"": {
    ""start"": ""node app.js"",
    ""dev"": ""nodemon app.js""
  },
  ""dependencies"": {
    ""express"": ""^4.18.2"",
    ""cors"": ""^2.8.5"",
    ""pg"": ""^8.11.3"",
    ""swagger-ui-express"": ""^5.0.0"",
    ""swagger-jsdoc"": ""^6.2.8"",
    ""winston"": ""^3.11.0""
  },
  ""devDependencies"": {
    ""nodemon"": ""^3.0.2""
  }
}
";

        // Note: README.md is created at root level, not here to avoid conflicts

        return files;
    }

    private Dictionary<string, string> GenerateJavaBackend(string? webApiUrl)
    {
        var files = new Dictionary<string, string>();

        // pom.xml - Maven configuration
        files["backend/pom.xml"] = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0""
         xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
         xsi:schemaLocation=""http://maven.apache.org/POM/4.0.0 
         http://maven.apache.org/xsd/maven-4.0.0.xsd"">
    <modelVersion>4.0.0</modelVersion>

    <parent>
        <groupId>org.springframework.boot</groupId>
        <artifactId>spring-boot-starter-parent</artifactId>
        <version>3.2.0</version>
        <relativePath/>
    </parent>

    <groupId>com.backend</groupId>
    <artifactId>backend</artifactId>
    <version>1.0.0</version>
    <name>Backend API</name>
    <description>Backend API</description>

    <properties>
        <java.version>17</java.version>
    </properties>

    <dependencies>
        <dependency>
            <groupId>org.springframework.boot</groupId>
            <artifactId>spring-boot-starter-web</artifactId>
        </dependency>
        <dependency>
            <groupId>org.springframework.boot</groupId>
            <artifactId>spring-boot-starter-data-jpa</artifactId>
        </dependency>
        <dependency>
            <groupId>org.postgresql</groupId>
            <artifactId>postgresql</artifactId>
            <scope>runtime</scope>
        </dependency>
        <dependency>
            <groupId>org.springdoc</groupId>
            <artifactId>springdoc-openapi-starter-webmvc-ui</artifactId>
            <version>2.3.0</version>
        </dependency>
    </dependencies>

    <build>
        <plugins>
            <plugin>
                <groupId>org.springframework.boot</groupId>
                <artifactId>spring-boot-maven-plugin</artifactId>
            </plugin>
        </plugins>
    </build>
</project>
";

        // Application.java - Spring Boot main class
        files["backend/src/main/java/com/backend/Application.java"] = "package com.backend;\n\n" +
"import org.springframework.boot.SpringApplication;\n" +
"import org.springframework.boot.autoconfigure.SpringBootApplication;\n\n" +
"@SpringBootApplication\n" +
"public class Application {\n" +
"    public static void main(String[] args) {\n" +
"        try {\n" +
"            SpringApplication.run(Application.class, args);\n" +
"        } catch (Exception startupEx) {\n" +
"            System.err.println(\"[STARTUP ERROR] Application failed to start: \" + startupEx.getMessage());\n" +
"            startupEx.printStackTrace();\n" +
"            \n" +
"            // Send startup error to endpoint (fire and forget)\n" +
"            String runtimeErrorEndpointUrl = System.getenv(\"RUNTIME_ERROR_ENDPOINT_URL\");\n" +
"            String boardId = System.getenv(\"BOARD_ID\");\n" +
"            \n" +
"            if (runtimeErrorEndpointUrl != null && !runtimeErrorEndpointUrl.isEmpty()) {\n" +
"                new Thread(() -> {\n" +
"                    try {\n" +
"                        java.net.http.HttpClient httpClient = java.net.http.HttpClient.newBuilder()\n" +
"                            .connectTimeout(java.time.Duration.ofSeconds(5))\n" +
"                            .build();\n" +
"                        \n" +
"                        String stackTrace = getStackTrace(startupEx);\n" +
"                        String file = startupEx.getStackTrace().length > 0 ? \n" +
"                            startupEx.getStackTrace()[0].getFileName() : null;\n" +
"                        Integer line = startupEx.getStackTrace().length > 0 ? \n" +
"                            startupEx.getStackTrace()[0].getLineNumber() : null;\n" +
"                        \n" +
"                        String jsonPayload = String.format(\n" +
"                            \"{\\\"boardId\\\":%s,\\\"timestamp\\\":\\\"%s\\\",\\\"file\\\":%s,\\\"line\\\":%s,\\\"stackTrace\\\":\\\"%s\\\",\\\"message\\\":\\\"%s\\\",\\\"exceptionType\\\":\\\"%s\\\",\\\"requestPath\\\":\\\"STARTUP\\\",\\\"requestMethod\\\":\\\"STARTUP\\\",\\\"userAgent\\\":\\\"STARTUP_ERROR\\\"}\",\n" +
"                            boardId != null ? \"\\\"\" + escapeJson(boardId) + \"\\\"\" : \"null\",\n" +
"                            java.time.Instant.now().toString(),\n" +
"                            file != null ? \"\\\"\" + escapeJson(file) + \"\\\"\" : \"null\",\n" +
"                            line != null ? line.toString() : \"null\",\n" +
"                            escapeJson(stackTrace),\n" +
"                            escapeJson(startupEx.getMessage() != null ? startupEx.getMessage() : \"Unknown error\"),\n" +
"                            escapeJson(startupEx.getClass().getName())\n" +
"                        );\n" +
"                        \n" +
"                        java.net.http.HttpRequest httpRequest = java.net.http.HttpRequest.newBuilder()\n" +
"                            .uri(java.net.URI.create(runtimeErrorEndpointUrl))\n" +
"                            .header(\"Content-Type\", \"application/json\")\n" +
"                            .POST(java.net.http.HttpRequest.BodyPublishers.ofString(jsonPayload))\n" +
"                            .timeout(java.time.Duration.ofSeconds(5))\n" +
"                            .build();\n" +
"                        \n" +
"                        httpClient.send(httpRequest, java.net.http.HttpResponse.BodyHandlers.ofString());\n" +
"                    } catch (Exception e) {\n" +
"                        // Ignore\n" +
"                    }\n" +
"                }).start();\n" +
"            }\n" +
"            \n" +
"            System.exit(1);\n" +
"        }\n" +
"    }\n" +
"    \n" +
"    private static String getStackTrace(Exception exception) {\n" +
"        java.io.StringWriter sw = new java.io.StringWriter();\n" +
"        java.io.PrintWriter pw = new java.io.PrintWriter(sw);\n" +
"        exception.printStackTrace(pw);\n" +
"        return sw.toString();\n" +
"    }\n" +
"    \n" +
"    private static String escapeJson(String str) {\n" +
"        if (str == null) return \"\";\n" +
"        return str.replace(\"\\\\\", \"\\\\\\\\\")\n" +
"                .replace(\"\\\"\", \"\\\\\\\"\")\n" +
"                .replace(\"\\n\", \"\\\\n\")\n" +
"                .replace(\"\\r\", \"\\\\r\")\n" +
"                .replace(\"\\t\", \"\\\\t\");\n" +
"    }\n" +
"}\n";

        // RootController.java - Root endpoint handler
        files["backend/src/main/java/com/backend/Controllers/RootController.java"] = "package com.backend.Controllers;\n\n" +
"import org.springframework.http.ResponseEntity;\n" +
"import org.springframework.web.bind.annotation.GetMapping;\n" +
"import org.springframework.web.bind.annotation.RestController;\n\n" +
"import java.util.Map;\n\n" +
"@RestController\n" +
"public class RootController {\n\n" +
"    @GetMapping(\"/\")\n" +
"    public ResponseEntity<Map<String, String>> root() {\n" +
"        return ResponseEntity.ok(Map.of(\n" +
"            \"message\", \"Backend API is running\",\n" +
"            \"status\", \"ok\",\n" +
"            \"swagger\", \"/swagger-ui/index.html\",\n" +
"            \"api\", \"/api/test\"\n" +
"        ));\n" +
"    }\n" +
"}\n";

        // DataSourceConfig.java - Configuration to convert DATABASE_URL to JDBC format
        files["backend/src/main/java/com/backend/Config/DataSourceConfig.java"] = "package com.backend.Config;\n\n" +
"import com.zaxxer.hikari.HikariConfig;\n" +
"import com.zaxxer.hikari.HikariDataSource;\n" +
"import org.springframework.context.annotation.Bean;\n" +
"import org.springframework.context.annotation.Configuration;\n\n" +
"import javax.sql.DataSource;\n" +
"import java.net.URI;\n" +
"import java.net.URLDecoder;\n" +
"import java.nio.charset.StandardCharsets;\n\n" +
"@Configuration\n" +
"public class DataSourceConfig {\n\n" +
"    private String safeDecode(String value) {\n" +
"        try {\n" +
"            return URLDecoder.decode(value, StandardCharsets.UTF_8);\n" +
"        } catch (IllegalArgumentException e) {\n" +
"            // If decoding fails (e.g., invalid % encoding), return original value\n" +
"            return value;\n" +
"        }\n" +
"    }\n\n" +
"    @Bean\n" +
"    public DataSource dataSource() {\n" +
"        String databaseUrl = System.getenv(\"DATABASE_URL\");\n" +
"        \n" +
"        if (databaseUrl == null || databaseUrl.isEmpty()) {\n" +
"            throw new IllegalStateException(\"DATABASE_URL environment variable is not set\");\n" +
"        }\n\n" +
"        try {\n" +
"            // Parse PostgreSQL URL format: postgresql://user:password@host:port/database\n" +
"            URI dbUri = new URI(databaseUrl);\n" +
"            \n" +
"            String userInfo = dbUri.getUserInfo();\n" +
"            String username;\n" +
"            String password;\n" +
"            \n" +
"            if (userInfo != null && userInfo.contains(\":\")) {\n" +
"                int colonIndex = userInfo.indexOf(\":\");\n" +
"                username = userInfo.substring(0, colonIndex);\n" +
"                password = userInfo.substring(colonIndex + 1);\n" +
"            } else {\n" +
"                username = userInfo != null ? userInfo : \"\";\n" +
"                password = \"\";\n" +
"            }\n" +
"            \n" +
"            // Safely decode username and password\n" +
"            username = safeDecode(username);\n" +
"            password = safeDecode(password);\n" +
"            \n" +
"            String host = dbUri.getHost();\n" +
"            int port = dbUri.getPort() > 0 ? dbUri.getPort() : 5432;\n" +
"            String database = dbUri.getPath().replaceFirst(\"/\", \"\");\n" +
"            \n" +
"            // Build JDBC URL\n" +
"            String jdbcUrl = String.format(\"jdbc:postgresql://%s:%d/%s\", host, port, database);\n" +
"            \n" +
"            // Add query parameters if present (e.g., sslmode)\n" +
"            if (dbUri.getQuery() != null && !dbUri.getQuery().isEmpty()) {\n" +
"                jdbcUrl += \"?\" + dbUri.getQuery();\n" +
"            }\n" +
"            \n" +
"            HikariConfig config = new HikariConfig();\n" +
"            config.setJdbcUrl(jdbcUrl);\n" +
"            config.setUsername(username);\n" +
"            config.setPassword(password);\n" +
"            config.setMaximumPoolSize(10);\n" +
"            \n" +
"            return new HikariDataSource(config);\n" +
"        } catch (Exception e) {\n" +
"            throw new IllegalStateException(\"Failed to parse DATABASE_URL: \" + e.getMessage(), e);\n" +
"        }\n" +
"    }\n" +
"}\n";

        // CorsConfig.java - CORS configuration
        files["backend/src/main/java/com/backend/Config/CorsConfig.java"] = "package com.backend.Config;\n\n" +
"import org.springframework.context.annotation.Bean;\n" +
"import org.springframework.context.annotation.Configuration;\n" +
"import org.springframework.web.servlet.config.annotation.CorsRegistry;\n" +
"import org.springframework.web.servlet.config.annotation.WebMvcConfigurer;\n\n" +
"@Configuration\n" +
"public class CorsConfig {\n\n" +
"    @Bean\n" +
"    public WebMvcConfigurer corsConfigurer() {\n" +
"        return new WebMvcConfigurer() {\n" +
"            @Override\n" +
"            public void addCorsMappings(CorsRegistry registry) {\n" +
"                registry.addMapping(\"/**\")\n" +
"                        .allowedOrigins(\"*\")\n" +
"                        .allowedMethods(\"GET\", \"POST\", \"PUT\", \"DELETE\", \"OPTIONS\")\n" +
"                        .allowedHeaders(\"*\")\n" +
"                        .allowCredentials(false);\n" +
"            }\n" +
"        };\n" +
"    }\n" +
"}\n";

        // TestProjects.java - Model
        files["backend/src/main/java/com/backend/Models/TestProjects.java"] = "package com.backend.Models;\n\n" +
"import jakarta.persistence.*;\n\n" +
"@Entity\n" +
"@Table(name = \"TestProjects\")\n" +
"public class TestProjects {\n" +
"    @Id\n" +
"    @GeneratedValue(strategy = GenerationType.IDENTITY)\n" +
"    @Column(name = \"Id\")\n" +
"    private Integer id;\n\n" +
"    @Column(name = \"Name\", nullable = false, length = 255)\n" +
"    private String name;\n\n" +
"    public Integer getId() {\n" +
"        return id;\n" +
"    }\n\n" +
"    public void setId(Integer id) {\n" +
"        this.id = id;\n" +
"    }\n\n" +
"    public String getName() {\n" +
"        return name;\n" +
"    }\n\n" +
"    public void setName(String name) {\n" +
"        this.name = name;\n" +
"    }\n" +
"}\n";

        // TestController.java - Controller
        files["backend/src/main/java/com/backend/Controllers/TestController.java"] = "package com.backend.Controllers;\n\n" +
"import com.backend.Models.TestProjects;\n" +
"import org.springframework.beans.factory.annotation.Autowired;\n" +
"import org.springframework.http.HttpStatus;\n" +
"import org.springframework.http.ResponseEntity;\n" +
"import org.springframework.transaction.annotation.Transactional;\n" +
"import org.springframework.web.bind.annotation.*;\n\n" +
"import jakarta.persistence.EntityManager;\n" +
"import jakarta.persistence.Query;\n" +
"import org.hibernate.Session;\n" +
"import org.hibernate.jdbc.Work;\n" +
"import java.sql.Connection;\n" +
"import java.sql.Statement;\n" +
"import java.util.List;\n" +
"import java.util.Map;\n\n" +
"@RestController\n" +
"@RequestMapping(\"/api/test\")\n" +
"@CrossOrigin(origins = \"*\")\n" +
"public class TestController {\n\n" +
"    @Autowired\n" +
"    private EntityManager entityManager;\n\n" +
"    private void setSearchPath() {\n" +
"        // Use Hibernate Session.doWork() to execute on the JDBC connection\n" +
"        Session session = entityManager.unwrap(Session.class);\n" +
"        session.doWork(new Work() {\n" +
"            @Override\n" +
"            public void execute(Connection connection) {\n" +
"                try (Statement stmt = connection.createStatement()) {\n" +
"                    stmt.execute(\"SET search_path = public, \\\"$user\\\"\");\n" +
"                } catch (Exception e) {\n" +
"                    throw new RuntimeException(e);\n" +
"                }\n" +
"            }\n" +
"        });\n" +
"    }\n\n" +
"    @GetMapping(value = {\"\", \"/\"})\n" +
"    @Transactional\n" +
"    public ResponseEntity<List<TestProjects>> getAll() {\n" +
"        try {\n" +
"            // Set search_path to public schema (required because isolated role has restricted search_path)\n" +
"            setSearchPath();\n" +
"            \n" +
"            Query query = entityManager.createNativeQuery(\"SELECT \\\"Id\\\", \\\"Name\\\" FROM \\\"TestProjects\\\" ORDER BY \\\"Id\\\"\", TestProjects.class);\n" +
"            @SuppressWarnings(\"unchecked\")\n" +
"            List<TestProjects> projects = query.getResultList();\n" +
"            return ResponseEntity.ok(projects);\n" +
"        } catch (Exception e) {\n" +
"            e.printStackTrace();\n" +
"            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).build();\n" +
"        }\n" +
"    }\n\n" +
"    @GetMapping(\"/{id}\")\n" +
"    @Transactional\n" +
"    public ResponseEntity<TestProjects> getById(@PathVariable Integer id) {\n" +
"        try {\n" +
"            // Set search_path to public schema (required because isolated role has restricted search_path)\n" +
"            setSearchPath();\n" +
"            \n" +
"            Query query = entityManager.createNativeQuery(\"SELECT \\\"Id\\\", \\\"Name\\\" FROM \\\"TestProjects\\\" WHERE \\\"Id\\\" = :id\", TestProjects.class);\n" +
"            query.setParameter(\"id\", id);\n" +
"            @SuppressWarnings(\"unchecked\")\n" +
"            List<TestProjects> results = query.getResultList();\n" +
"            if (results.isEmpty()) {\n" +
"                return ResponseEntity.notFound().build();\n" +
"            }\n" +
"            return ResponseEntity.ok(results.get(0));\n" +
"        } catch (Exception e) {\n" +
"            e.printStackTrace();\n" +
"            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).build();\n" +
"        }\n" +
"    }\n\n" +
"    @PostMapping\n" +
"    @Transactional\n" +
"    public ResponseEntity<TestProjects> create(@RequestBody Map<String, String> request) {\n" +
"        try {\n" +
"            // Set search_path to public schema (required because isolated role has restricted search_path)\n" +
"            setSearchPath();\n" +
"            \n" +
"            String name = request.get(\"name\");\n" +
"            Query query = entityManager.createNativeQuery(\"INSERT INTO \\\"TestProjects\\\" (\\\"Name\\\") VALUES (:name) RETURNING \\\"Id\\\", \\\"Name\\\"\", TestProjects.class);\n" +
"            query.setParameter(\"name\", name);\n" +
"            @SuppressWarnings(\"unchecked\")\n" +
"            List<TestProjects> results = query.getResultList();\n" +
"            if (results.isEmpty()) {\n" +
"                return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).build();\n" +
"            }\n" +
"            return ResponseEntity.status(HttpStatus.CREATED).body(results.get(0));\n" +
"        } catch (Exception e) {\n" +
"            e.printStackTrace();\n" +
"            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).build();\n" +
"        }\n" +
"    }\n\n" +
"    @PutMapping(\"/{id}\")\n" +
"    @Transactional\n" +
"    public ResponseEntity<TestProjects> update(@PathVariable Integer id, @RequestBody Map<String, String> request) {\n" +
"        try {\n" +
"            // Set search_path to public schema (required because isolated role has restricted search_path)\n" +
"            setSearchPath();\n" +
"            \n" +
"            String name = request.get(\"name\");\n" +
"            Query query = entityManager.createNativeQuery(\"UPDATE \\\"TestProjects\\\" SET \\\"Name\\\" = :name WHERE \\\"Id\\\" = :id RETURNING \\\"Id\\\", \\\"Name\\\"\", TestProjects.class);\n" +
"            query.setParameter(\"name\", name);\n" +
"            query.setParameter(\"id\", id);\n" +
"            @SuppressWarnings(\"unchecked\")\n" +
"            List<TestProjects> results = query.getResultList();\n" +
"            if (results.isEmpty()) {\n" +
"                return ResponseEntity.notFound().build();\n" +
"            }\n" +
"            return ResponseEntity.ok(results.get(0));\n" +
"        } catch (Exception e) {\n" +
"            e.printStackTrace();\n" +
"            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).build();\n" +
"        }\n" +
"    }\n\n" +
"    @DeleteMapping(\"/{id}\")\n" +
"    @Transactional\n" +
"    public ResponseEntity<Map<String, String>> delete(@PathVariable Integer id) {\n" +
"        try {\n" +
"            // Set search_path to public schema (required because isolated role has restricted search_path)\n" +
"            setSearchPath();\n" +
"            \n" +
"            Query query = entityManager.createNativeQuery(\"DELETE FROM \\\"TestProjects\\\" WHERE \\\"Id\\\" = :id\");\n" +
"            query.setParameter(\"id\", id);\n" +
"            int deleted = query.executeUpdate();\n" +
"            if (deleted == 0) {\n" +
"                return ResponseEntity.notFound().build();\n" +
"            }\n" +
"            return ResponseEntity.ok(Map.of(\"message\", \"Deleted successfully\"));\n" +
"        } catch (Exception e) {\n" +
"            e.printStackTrace();\n" +
"            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).build();\n" +
"        }\n" +
"    }\n" +
"}\n";

        // GlobalExceptionHandler.java - Global exception handler for runtime error reporting
        files["backend/src/main/java/com/backend/Exception/GlobalExceptionHandler.java"] = "package com.backend.Exception;\n\n" +
"import jakarta.servlet.http.HttpServletRequest;\n" +
"import org.slf4j.Logger;\n" +
"import org.slf4j.LoggerFactory;\n" +
"import org.springframework.http.HttpStatus;\n" +
"import org.springframework.http.ResponseEntity;\n" +
"import org.springframework.web.bind.annotation.ControllerAdvice;\n" +
"import org.springframework.web.bind.annotation.ExceptionHandler;\n" +
"import org.springframework.web.context.request.WebRequest;\n\n" +
"import java.io.PrintWriter;\n" +
"import java.io.StringWriter;\n" +
"import java.net.URI;\n" +
"import java.net.http.HttpClient;\n" +
"import java.net.http.HttpRequest;\n" +
"import java.net.http.HttpResponse;\n" +
"import java.time.Duration;\n" +
"import java.time.Instant;\n" +
"import java.util.regex.Pattern;\n" +
"import java.util.regex.Matcher;\n\n" +
"@ControllerAdvice\n" +
"public class GlobalExceptionHandler {\n\n" +
"    private static final Logger logger = LoggerFactory.getLogger(GlobalExceptionHandler.class);\n" +
"    private static final HttpClient httpClient = HttpClient.newBuilder()\n" +
"            .connectTimeout(Duration.ofSeconds(5))\n" +
"            .build();\n\n" +
"    @ExceptionHandler(Exception.class)\n" +
"    public ResponseEntity<Object> handleAllExceptions(Exception ex, WebRequest request, HttpServletRequest httpRequest) {\n" +
"        logger.error(\"[EXCEPTION HANDLER] Unhandled exception occurred: {}\", ex.getMessage(), ex);\n\n" +
"        // Extract boardId from request\n" +
"        String boardId = extractBoardId(httpRequest);\n" +
"        logger.warn(\"[EXCEPTION HANDLER] Extracted boardId: {}\", boardId != null ? boardId : \"NULL\");\n\n" +
"        // Send error to runtime error endpoint (fire and forget)\n" +
"        String runtimeErrorEndpointUrl = System.getenv(\"RUNTIME_ERROR_ENDPOINT_URL\");\n" +
"        if (runtimeErrorEndpointUrl != null && !runtimeErrorEndpointUrl.isEmpty()) {\n" +
"            logger.warn(\"[EXCEPTION HANDLER] Sending error to endpoint: {}\", runtimeErrorEndpointUrl);\n" +
"            sendErrorToEndpoint(runtimeErrorEndpointUrl, boardId, httpRequest, ex);\n" +
"        } else {\n" +
"            logger.warn(\"[EXCEPTION HANDLER] RUNTIME_ERROR_ENDPOINT_URL is not set - skipping error reporting\");\n" +
"        }\n\n" +
"        // Return error response to client\n" +
"        return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)\n" +
"                .body(java.util.Map.of(\n" +
"                        \"error\", \"An error occurred while processing your request\",\n" +
"                        \"message\", ex.getMessage() != null ? ex.getMessage() : \"Unknown error\"\n" +
"                ));\n" +
"    }\n\n" +
"    private String extractBoardId(HttpServletRequest request) {\n" +
"        // Try route parameter\n" +
"        String boardId = request.getParameter(\"boardId\");\n" +
"        if (boardId != null && !boardId.isEmpty()) {\n" +
"            return boardId;\n" +
"        }\n\n" +
"        // Try header\n" +
"        boardId = request.getHeader(\"X-Board-Id\");\n" +
"        if (boardId != null && !boardId.isEmpty()) {\n" +
"            return boardId;\n" +
"        }\n\n" +
"        // Try environment variable\n" +
"        boardId = System.getenv(\"BOARD_ID\");\n" +
"        if (boardId != null && !boardId.isEmpty()) {\n" +
"            return boardId;\n" +
"        }\n\n" +
"        // Try to extract from hostname (Railway pattern: webapi{boardId}.up.railway.app - no hyphen)\n" +
"        String host = request.getServerName();\n" +
"        if (host != null) {\n" +
"            Pattern pattern = Pattern.compile(\"webapi([a-f0-9]{24})\", Pattern.CASE_INSENSITIVE);\n" +
"            Matcher matcher = pattern.matcher(host);\n" +
"            if (matcher.find()) {\n" +
"                return matcher.group(1);\n" +
"            }\n" +
"        }\n\n" +
"        // Try to extract from RUNTIME_ERROR_ENDPOINT_URL if it contains boardId pattern\n" +
"        String endpointUrl = System.getenv(\"RUNTIME_ERROR_ENDPOINT_URL\");\n" +
"        if (endpointUrl != null && !endpointUrl.isEmpty()) {\n" +
"            Pattern pattern = Pattern.compile(\"webapi([a-f0-9]{24})\", Pattern.CASE_INSENSITIVE);\n" +
"            Matcher matcher = pattern.matcher(endpointUrl);\n" +
"            if (matcher.find()) {\n" +
"                return matcher.group(1);\n" +
"            }\n" +
"        }\n\n" +
"        return null;\n" +
"    }\n\n" +
"    private void sendErrorToEndpoint(String endpointUrl, String boardId, HttpServletRequest request, Exception exception) {\n" +
"        // Run in background thread to avoid blocking the response\n" +
"        new Thread(() -> {\n" +
"            try {\n" +
"                String stackTrace = getStackTrace(exception);\n" +
"                String file = exception.getStackTrace().length > 0 ? exception.getStackTrace()[0].getFileName() : null;\n" +
"                Integer line = exception.getStackTrace().length > 0 ? exception.getStackTrace()[0].getLineNumber() : null;\n\n" +
"                String requestPath = request.getRequestURI();\n" +
"                String requestMethod = request.getMethod();\n" +
"                String userAgent = request.getHeader(\"User-Agent\");\n\n" +
"                // Build JSON payload\n" +
"                String jsonPayload = String.format(\n" +
"                        \"{\\\"boardId\\\":%s,\\\"timestamp\\\":\\\"%s\\\",\\\"file\\\":%s,\\\"line\\\":%s,\\\"stackTrace\\\":%s,\\\"message\\\":%s,\\\"exceptionType\\\":%s,\\\"requestPath\\\":%s,\\\"requestMethod\\\":%s,\\\"userAgent\\\":%s}\",\n" +
"                        boardId != null ? \"\\\"\" + boardId + \"\\\"\" : \"null\",\n" +
"                        Instant.now().toString(),\n" +
"                        file != null ? \"\\\"\" + escapeJson(file) + \"\\\"\" : \"null\",\n" +
"                        line != null ? line.toString() : \"null\",\n" +
"                        \"\\\"\" + escapeJson(stackTrace) + \"\\\"\",\n" +
"                        \"\\\"\" + escapeJson(exception.getMessage() != null ? exception.getMessage() : \"Unknown error\") + \"\\\"\",\n" +
"                        \"\\\"\" + exception.getClass().getName() + \"\\\"\",\n" +
"                        \"\\\"\" + escapeJson(requestPath) + \"\\\"\",\n" +
"                        \"\\\"\" + escapeJson(requestMethod) + \"\\\"\",\n" +
"                        userAgent != null ? \"\\\"\" + escapeJson(userAgent) + \"\\\"\" : \"null\"\n" +
"                );\n\n" +
"                HttpRequest httpRequest = HttpRequest.newBuilder()\n" +
"                        .uri(URI.create(endpointUrl))\n" +
"                        .header(\"Content-Type\", \"application/json\")\n" +
"                        .POST(HttpRequest.BodyPublishers.ofString(jsonPayload))\n" +
"                        .timeout(Duration.ofSeconds(5))\n" +
"                        .build();\n\n" +
"                HttpResponse<String> response = httpClient.send(httpRequest, HttpResponse.BodyHandlers.ofString());\n" +
"                logger.warn(\"[EXCEPTION HANDLER] Error endpoint response: {} - {}\", response.statusCode(), response.body());\n" +
"            } catch (Exception e) {\n" +
"                logger.error(\"[EXCEPTION HANDLER] Failed to send error to endpoint: {}\", e.getMessage(), e);\n" +
"            }\n" +
"        }).start();\n" +
"    }\n\n" +
"    private String getStackTrace(Exception exception) {\n" +
"        StringWriter sw = new StringWriter();\n" +
"        PrintWriter pw = new PrintWriter(sw);\n" +
"        exception.printStackTrace(pw);\n" +
"        return sw.toString();\n" +
"    }\n\n" +
"    private String escapeJson(String str) {\n" +
"        if (str == null) return \"\";\n" +
"        return str.replace(\"\\\\\", \"\\\\\\\\\")\n" +
"                .replace(\"\\\"\", \"\\\\\\\"\")\n" +
"                .replace(\"\\n\", \"\\\\n\")\n" +
"                .replace(\"\\r\", \"\\\\r\")\n" +
"                .replace(\"\\t\", \"\\\\t\");\n" +
"    }\n" +
"}\n";

        // application.properties - Configuration
        files["backend/src/main/resources/application.properties"] = "spring.application.name=Backend API\n" +
"server.port=${PORT:8080}\n\n" +
"# Handle forwarded headers for Railway (HTTPS termination)\n" +
"server.forward-headers-strategy=framework\n\n" +
"# Web configuration - allow trailing slashes\n" +
"spring.web.resources.add-mappings=true\n\n" +
"# Database configuration\n" +
"# DATABASE_URL is converted to JDBC format in DataSourceConfig.java\n" +
"spring.jpa.hibernate.ddl-auto=none\n" +
"spring.jpa.show-sql=false\n" +
"spring.jpa.properties.hibernate.dialect=org.hibernate.dialect.PostgreSQLDialect\n\n" +
"# Logging configuration - Warning and Error only\n" +
"logging.level.root=WARN\n" +
"logging.level.com.backend=WARN\n" +
"logging.level.org.springframework=WARN\n" +
"logging.level.org.hibernate=WARN\n\n" +
"# Swagger/OpenAPI configuration\n" +
"springdoc.api-docs.path=/api-docs\n" +
"springdoc.swagger-ui.path=/swagger\n";

        // Note: README.md is created at root level, not here to avoid conflicts

        return files;
    }

    private Dictionary<string, string> GeneratePhpBackend(string? webApiUrl)
    {
        var files = new Dictionary<string, string>();

        // Models/TestProjects.php
        files["backend/Models/TestProjects.php"] = @"<?php

namespace App\Models;

class TestProjects
{
    public ?int $id;
    public string $name;

    public function __construct(?int $id = null, string $name = '')
    {
        $this->id = $id;
        $this->name = $name;
    }

    public function toArray(): array
    {
        return [
            'id' => $this->id,
            'name' => $this->name
        ];
    }
}
";

        // Controllers/TestController.php
        files["backend/Controllers/TestController.php"] = @"<?php

namespace App\Controllers;

use App\Models\TestProjects;
use PDO;
use PDOException;

class TestController
{
    private PDO $db;

    public function __construct(PDO $db)
    {
        $this->db = $db;
    }

    private function setSearchPath(): void
    {
        // Set search_path to public schema (required because isolated role has restricted search_path)
        // Using string concatenation to avoid C# string interpolation issues with $user
        $dollarSign = '$';
        $query = 'SET search_path = public, ""' . $dollarSign . 'user""';
        $this->db->exec($query);
    }

    public function getAll(): array
    {
        // Set search_path to public schema (required because isolated role has restricted search_path)
        $this->setSearchPath();
        $stmt = $this->db->query('SELECT ""Id"", ""Name"" FROM ""TestProjects"" ORDER BY ""Id""');
        $results = $stmt->fetchAll(PDO::FETCH_ASSOC);
        
        $projects = [];
        foreach ($results as $row) {
            $projects[] = [
                'Id' => (int)$row['Id'],
                'Name' => $row['Name']
            ];
        }
        return $projects;
        // Do NOT catch generic Exception - let it bubble up to global exception handler
    }

    public function getById(int $id): ?array
    {
        // Set search_path to public schema (required because isolated role has restricted search_path)
        $this->setSearchPath();
        $stmt = $this->db->prepare('SELECT ""Id"", ""Name"" FROM ""TestProjects"" WHERE ""Id"" = :id');
        $stmt->execute(['id' => $id]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        
        if (!$row) {
            return null;
        }
        
        return [
            'Id' => (int)$row['Id'],
            'Name' => $row['Name']
        ];
        // Do NOT catch generic Exception - let it bubble up to global exception handler
    }

    public function create(array $data): array
    {
        // Set search_path to public schema (required because isolated role has restricted search_path)
        $this->setSearchPath();
        $stmt = $this->db->prepare('INSERT INTO ""TestProjects"" (""Name"") VALUES (:name) RETURNING ""Id"", ""Name""');
        $stmt->execute(['name' => $data['name']]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        
        return [
            'Id' => (int)$row['Id'],
            'Name' => $row['Name']
        ];
        // Do NOT catch generic Exception - let it bubble up to global exception handler
    }

    public function update(int $id, array $data): ?array
    {
        // Set search_path to public schema (required because isolated role has restricted search_path)
        $this->setSearchPath();
        $stmt = $this->db->prepare('UPDATE ""TestProjects"" SET ""Name"" = :name WHERE ""Id"" = :id RETURNING ""Id"", ""Name""');
        $stmt->execute(['id' => $id, 'name' => $data['name']]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        
        if (!$row) {
            return null;
        }
        
        return [
            'Id' => (int)$row['Id'],
            'Name' => $row['Name']
        ];
        // Do NOT catch generic Exception - let it bubble up to global exception handler
    }

    public function delete(int $id): bool
    {
        // Set search_path to public schema (required because isolated role has restricted search_path)
        $this->setSearchPath();
        $stmt = $this->db->prepare('DELETE FROM ""TestProjects"" WHERE ""Id"" = :id');
        $stmt->execute(['id' => $id]);
        return $stmt->rowCount() > 0;
        // Do NOT catch generic Exception - let it bubble up to global exception handler
    }
}
";

        // index.php - Main entry point
        files["backend/index.php"] = @"<?php

require_once __DIR__ . ""/vendor/autoload.php"";

use App\Controllers\TestController;
use PDO;

try {
// Configure logging - Warning and Error only
ini_set('log_errors', 1);
ini_set('error_log', __DIR__ . '/logs/php_errors.log');
ini_set('error_reporting', E_WARNING | E_ERROR | E_PARSE | E_CORE_ERROR | E_COMPILE_ERROR | E_RECOVERABLE_ERROR);
ini_set('display_errors', 0); // Don't display errors, only log them

// Create logs directory if it doesn't exist
$logsDir = __DIR__ . '/logs';
if (!is_dir($logsDir)) {
    mkdir($logsDir, 0755, true);
}

// Global error and exception handlers for runtime error reporting
function extractBoardId() {
    // Try query parameter
    if (isset($_GET['boardId']) && !empty($_GET['boardId'])) {
        return $_GET['boardId'];
    }
    
    // Try header
    $headers = getallheaders();
    if (isset($headers['X-Board-Id']) && !empty($headers['X-Board-Id'])) {
        return $headers['X-Board-Id'];
    }
    
    // Try environment variable
    $boardId = getenv('BOARD_ID');
    if ($boardId) {
        return $boardId;
    }
    
    // Try to extract from hostname (Railway pattern: webapi{boardId}.up.railway.app - no hyphen)
    $host = $_SERVER['HTTP_HOST'] ?? $_SERVER['SERVER_NAME'] ?? '';
    if (preg_match('/webapi([a-f0-9]{24})/i', $host, $matches)) {
        return $matches[1];
    }
    
    // Try to extract from RUNTIME_ERROR_ENDPOINT_URL if it contains boardId pattern
    $endpointUrl = getenv('RUNTIME_ERROR_ENDPOINT_URL') ?: '';
    if (preg_match('/webapi([a-f0-9]{24})/i', $endpointUrl, $matches)) {
        return $matches[1];
    }
    
    return null;
}

function sendErrorToEndpoint($endpointUrl, $boardId, $exception) {
    // Run in background (fire and forget) using file_get_contents with stream context
    $payload = json_encode([
        'boardId' => $boardId,
        'timestamp' => gmdate('c'),
        'file' => $exception->getFile(),
        'line' => $exception->getLine(),
        'stackTrace' => $exception->getTraceAsString(),
        'message' => $exception->getMessage(),
        'exceptionType' => get_class($exception),
        'requestPath' => $_SERVER['REQUEST_URI'] ?? '/',
        'requestMethod' => $_SERVER['REQUEST_METHOD'] ?? 'GET',
        'userAgent' => $_SERVER['HTTP_USER_AGENT'] ?? null
    ]);
    
    $opts = [
        'http' => [
            'method' => 'POST',
            'header' => 'Content-Type: application/json',
            'content' => $payload,
            'timeout' => 5,
            'ignore_errors' => true
        ]
    ];
    
    // Fire and forget - don't wait for response
    @file_get_contents($endpointUrl, false, stream_context_create($opts));
}

// Set exception handler
set_exception_handler(function ($exception) {
    error_log('[EXCEPTION HANDLER] Unhandled exception: ' . $exception->getMessage());
    
    $boardId = extractBoardId();
    error_log('[EXCEPTION HANDLER] Extracted boardId: ' . ($boardId ?? 'NULL'));
    
    $runtimeErrorEndpointUrl = getenv('RUNTIME_ERROR_ENDPOINT_URL');
    if ($runtimeErrorEndpointUrl) {
        error_log('[EXCEPTION HANDLER] Sending error to endpoint: ' . $runtimeErrorEndpointUrl);
        sendErrorToEndpoint($runtimeErrorEndpointUrl, $boardId, $exception);
    } else {
        error_log('[EXCEPTION HANDLER] RUNTIME_ERROR_ENDPOINT_URL is not set - skipping error reporting');
    }
    
    http_response_code(500);
    header('Content-Type: application/json');
    echo json_encode([
        'error' => 'An error occurred while processing your request',
        'message' => $exception->getMessage()
    ]);
    exit;
});

// Set error handler for non-fatal errors
set_error_handler(function ($severity, $message, $file, $line) {
    if (!(error_reporting() & $severity)) {
        return false; // Don't handle if error reporting is disabled for this severity
    }
    
    // Only convert to exception for non-fatal errors (fatal errors are handled by shutdown function)
    if ($severity !== E_PARSE && $severity !== E_CORE_ERROR && $severity !== E_COMPILE_ERROR) {
        throw new ErrorException($message, 0, $severity, $file, $line);
    }
    
    return false; // Let PHP handle fatal errors normally (they'll be caught by shutdown function)
}, E_WARNING | E_ERROR | E_PARSE | E_CORE_ERROR | E_COMPILE_ERROR | E_RECOVERABLE_ERROR);

// Register shutdown function to catch fatal errors (including parse errors)
register_shutdown_function(function () {
    $error = error_get_last();
    if ($error !== null && in_array($error['type'], [E_PARSE, E_CORE_ERROR, E_COMPILE_ERROR, E_ERROR])) {
        error_log('[FATAL ERROR HANDLER] Fatal error occurred: ' . $error['message']);
        
        $boardId = extractBoardId();
        error_log('[FATAL ERROR HANDLER] Extracted boardId: ' . ($boardId ?? 'NULL'));
        
        $runtimeErrorEndpointUrl = getenv('RUNTIME_ERROR_ENDPOINT_URL');
        if ($runtimeErrorEndpointUrl) {
            error_log('[FATAL ERROR HANDLER] Sending error to endpoint: ' . $runtimeErrorEndpointUrl);
            
            // Create a synthetic exception for fatal errors
            $exception = new ErrorException(
                $error['message'],
                0,
                $error['type'],
                $error['file'],
                $error['line']
            );
            
            sendErrorToEndpoint($runtimeErrorEndpointUrl, $boardId, $exception);
        } else {
            error_log('[FATAL ERROR HANDLER] RUNTIME_ERROR_ENDPOINT_URL is not set - skipping error reporting');
        }
        
        // Send error response
        if (!headers_sent()) {
            http_response_code(500);
            header('Content-Type: application/json');
            echo json_encode([
                'error' => 'A fatal error occurred',
                'message' => $error['message'],
                'file' => $error['file'],
                'line' => $error['line']
            ]);
        }
    }
});

// Get request method and path first (before database connection)
$method = $_SERVER['REQUEST_METHOD'] ?? 'GET';
$path = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH);

// CORS headers
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type');

// Handle preflight
if ($method === 'OPTIONS') {
    http_response_code(200);
    exit;
}

// Handle routes that don't require database connection first
if ($path === '/swagger') {
    // Swagger UI endpoint - serve interactive Swagger UI HTML page
    header('Content-Type: text/html');
    echo '<!DOCTYPE html>
<html>
<head>
    <title>Backend API - Swagger UI</title>
    <link rel=""stylesheet"" type=""text/css"" href=""https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui.css"" />
    <style>
        html { box-sizing: border-box; overflow: -moz-scrollbars-vertical; overflow-y: scroll; }
        *, *:before, *:after { box-sizing: inherit; }
        body { margin:0; background: #fafafa; }
    </style>
</head>
<body>
    <div id=""swagger-ui""></div>
    <script src=""https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui-bundle.js""></script>
    <script src=""https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui-standalone-preset.js""></script>
    <script>
        window.onload = function() {
            const ui = SwaggerUIBundle({
                url: ""/swagger.json"",
                dom_id: ""#swagger-ui"",
                deepLinking: true,
                presets: [
                    SwaggerUIBundle.presets.apis,
                    SwaggerUIStandalonePreset
                ],
                plugins: [
                    SwaggerUIBundle.plugins.DownloadUrl
                ],
                layout: ""StandaloneLayout""
            });
        };
    </script>
</body>
</html>';
    exit;
} elseif ($path === '/swagger.json') {
    // Swagger JSON endpoint - return OpenAPI spec as JSON
    header('Content-Type: application/json');
    echo json_encode([
        'openapi' => '3.0.0',
        'info' => [
            'title' => 'Backend API',
            'version' => '1.0.0',
            'description' => 'PHP Backend API Documentation'
        ],
        'paths' => [
            '/api/test' => [
                'get' => [
                    'summary' => 'Get all test projects',
                    'responses' => [
                        '200' => [
                            'description' => 'List of test projects',
                            'content' => [
                                'application/json' => [
                                    'schema' => [
                                        'type' => 'array',
                                        'items' => ['$ref' => '#/components/schemas/TestProjects']
                                    ]
                                ]
                            ]
                        ]
                    ]
                ],
                'post' => [
                    'summary' => 'Create a new test project',
                    'requestBody' => [
                        'required' => true,
                        'content' => [
                            'application/json' => [
                                'schema' => ['$ref' => '#/components/schemas/TestProjectsInput']
                            ]
                        ]
                    ],
                    'responses' => [
                        '201' => [
                            'description' => 'Created test project',
                            'content' => [
                                'application/json' => [
                                    'schema' => ['$ref' => '#/components/schemas/TestProjects']
                                ]
                            ]
                        ]
                    ]
                ]
            ],
            '/api/test/{id}' => [
                'get' => [
                    'summary' => 'Get test project by ID',
                    'parameters' => [
                        [
                            'name' => 'id',
                            'in' => 'path',
                            'required' => true,
                            'schema' => ['type' => 'integer']
                        ]
                    ],
                    'responses' => [
                        '200' => [
                            'description' => 'Test project found',
                            'content' => [
                                'application/json' => [
                                    'schema' => ['$ref' => '#/components/schemas/TestProjects']
                                ]
                            ]
                        ],
                        '404' => ['description' => 'Project not found']
                    ]
                ],
                'put' => [
                    'summary' => 'Update test project',
                    'parameters' => [
                        [
                            'name' => 'id',
                            'in' => 'path',
                            'required' => true,
                            'schema' => ['type' => 'integer']
                        ]
                    ],
                    'requestBody' => [
                        'required' => true,
                        'content' => [
                            'application/json' => [
                                'schema' => ['$ref' => '#/components/schemas/TestProjectsInput']
                            ]
                        ]
                    ],
                    'responses' => [
                        '200' => ['description' => 'Updated test project'],
                        '404' => ['description' => 'Project not found']
                    ]
                ],
                'delete' => [
                    'summary' => 'Delete test project',
                    'parameters' => [
                        [
                            'name' => 'id',
                            'in' => 'path',
                            'required' => true,
                            'schema' => ['type' => 'integer']
                        ]
                    ],
                    'responses' => [
                        '200' => ['description' => 'Deleted successfully'],
                        '404' => ['description' => 'Project not found']
                    ]
                ]
            ]
        ],
        'components' => [
            'schemas' => [
                'TestProjects' => [
                    'type' => 'object',
                    'properties' => [
                        'Id' => ['type' => 'integer'],
                        'Name' => ['type' => 'string']
                    ]
                ],
                'TestProjectsInput' => [
                    'type' => 'object',
                    'required' => ['Name'],
                    'properties' => [
                        'Name' => ['type' => 'string']
                    ]
                ]
            ]
        ]
    ], JSON_PRETTY_PRINT);
    exit;
} elseif ($path === '/' || $path === '') {
    header('Content-Type: application/json');
    echo json_encode([
        'message' => 'Backend API is running',
        'status' => 'ok',
        'swagger' => '/swagger',
        'api' => '/api/test'
    ]);
    exit;
} elseif ($path === '/health') {
    header('Content-Type: application/json');
    echo json_encode([
        'status' => 'healthy',
        'service' => 'Backend API'
    ]);
    exit;
}

// Routes that require database connection
try {
    // Parse DATABASE_URL
    $databaseUrl = getenv('DATABASE_URL');
    if (!$databaseUrl) {
        http_response_code(500);
        header('Content-Type: application/json');
        echo json_encode(['error' => 'DATABASE_URL environment variable not set']);
        exit;
    }

    // Parse PostgreSQL connection string
    // Use parse_url with PHP_URL_* components to ensure proper parsing
    $url = parse_url($databaseUrl);
    
    if ($url === false) {
        throw new Exception('Invalid DATABASE_URL format');
    }
    
    $host = $url['host'] ?? 'localhost';
    $port = isset($url['port']) ? (int)$url['port'] : 5432;
    
    // Extract database name from path - ensure full path is extracted
    // Database name is in the path component (e.g., /AppDB_69626b9aa83a298b692f8150)
    $dbPath = $url['path'] ?? '/postgres';
    // Remove leading slash - database name should not have leading slash
    $dbname = ltrim($dbPath, '/');
    // URL decode in case database name has encoded characters (though it shouldn't normally)
    $dbname = urldecode($dbname);
    // If path is empty after trimming, use default
    if (empty($dbname)) {
        $dbname = 'postgres';
    }
    
    // Extract and decode username and password
    $username = isset($url['user']) ? urldecode($url['user']) : 'postgres';
    $password = isset($url['pass']) ? urldecode($url['pass']) : '';
    
    // Build DSN string - PDO PostgreSQL DSN format uses semicolon-separated parameters, NOT query string
    // Format: pgsql:host=...;port=...;dbname=...;sslmode=require
    $dsn = ""pgsql:host="" . $host . "";port="" . $port . "";dbname="" . $dbname;
    
    // Parse query parameters from original URL to check for sslmode
    $sslMode = 'require'; // Default to require for Neon
    if (isset($url['query']) && !empty($url['query'])) {
        parse_str($url['query'], $queryParams);
        if (isset($queryParams['sslmode'])) {
            $sslMode = $queryParams['sslmode'];
        }
    }
    
    // Add SSL mode as a semicolon-separated parameter (NOT as query string)
    $dsn .= ';sslmode=' . $sslMode;
    
    // Create PDO connection with error handling
    try {
        $pdo = new PDO($dsn, $username, $password, [
            PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
            PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC
        ]);
    } catch (PDOException $e) {
        // Log connection details for debugging (without exposing password)
        error_log('Database connection failed. Host: ' . $host . ', Port: ' . $port . ', Database: ' . $dbname . ', User: ' . $username);
        throw $e;
    }
    
    // Create controller
    $controller = new TestController($pdo);
    header('Content-Type: application/json');
    
    // Route handling for API endpoints
    if ($path === '/api/test' || $path === '/api/test/') {
        if ($method === 'GET') {
            echo json_encode($controller->getAll());
            exit;
        } elseif ($method === 'POST') {
            $data = json_decode(file_get_contents('php://input'), true);
            http_response_code(201);
            echo json_encode($controller->create($data));
            exit;
        }
    } elseif (preg_match('#^/api/test/(\d+)$#', $path, $matches)) {
        $id = (int)$matches[1];
        
        if ($method === 'GET') {
            $result = $controller->getById($id);
            if ($result === null) {
                http_response_code(404);
                echo json_encode(['error' => 'Project not found']);
            } else {
                echo json_encode($result);
            }
            exit;
        } elseif ($method === 'PUT') {
            $data = json_decode(file_get_contents('php://input'), true);
            $result = $controller->update($id, $data);
            if ($result === null) {
                http_response_code(404);
                echo json_encode(['error' => 'Project not found']);
            } else {
                echo json_encode($result);
            }
            exit;
        } elseif ($method === 'DELETE') {
            if ($controller->delete($id)) {
                echo json_encode(['message' => 'Deleted successfully']);
            } else {
                http_response_code(404);
                echo json_encode(['error' => 'Project not found']);
            }
            exit;
        }
    }
    
    // 404 Not Found for API routes
    http_response_code(404);
    header('Content-Type: application/json');
    echo json_encode(['error' => 'Not found']);
    
} catch (Exception $e) {
    // Let the global exception handler handle it
    throw $e;
}
} catch (Throwable $startupEx) {
    // Startup error handler - catch errors during require_once or initialization
    error_log('[STARTUP ERROR] Application failed to start: ' . $startupEx->getMessage());
    
    // Send startup error to endpoint
    $runtimeErrorEndpointUrl = getenv('RUNTIME_ERROR_ENDPOINT_URL');
    $boardId = getenv('BOARD_ID');
    
    if ($runtimeErrorEndpointUrl) {
        $payload = json_encode([
            'boardId' => $boardId,
            'timestamp' => gmdate('c'),
            'file' => $startupEx->getFile(),
            'line' => $startupEx->getLine(),
            'stackTrace' => $startupEx->getTraceAsString(),
            'message' => $startupEx->getMessage(),
            'exceptionType' => get_class($startupEx),
            'requestPath' => 'STARTUP',
            'requestMethod' => 'STARTUP',
            'userAgent' => 'STARTUP_ERROR'
        ]);
        
        $opts = [
            'http' => [
                'method' => 'POST',
                'header' => 'Content-Type: application/json',
                'content' => $payload,
                'timeout' => 5,
                'ignore_errors' => true
            ]
        ];
        
        @file_get_contents($runtimeErrorEndpointUrl, false, stream_context_create($opts));
    }
    
    http_response_code(500);
    header('Content-Type: application/json');
    echo json_encode([
        'error' => 'Application failed to start',
        'message' => $startupEx->getMessage()
    ]);
    exit(1);
}
";

        // composer.json
        files["backend/composer.json"] = @"{
    ""name"": ""backend/api"",
    ""description"": ""Backend API"",
    ""type"": ""project"",
    ""require"": {
        ""php"": "">=8.1"",
        ""ext-pdo"": ""*"",
        ""ext-pdo_pgsql"": ""*""
    },
    ""autoload"": {
        ""psr-4"": {
            ""App\\"": """"
        }
    }
}";

        // .htaccess for Apache
        files["backend/.htaccess"] = @"RewriteEngine On
RewriteCond %{REQUEST_FILENAME} !-f
RewriteCond %{REQUEST_FILENAME} !-d
RewriteRule ^(.*)$ index.php [QSA,L]
";

        // build_check.php - Full application build validation for PHP
        files["backend/build_check.php"] = @"<?php
error_reporting(E_ALL);
ini_set('display_errors', '1');
ini_set('display_startup_errors', '1');

set_error_handler(function ($severity, $message, $file, $line) {
    throw new ErrorException($message, 0, $severity, $file, $line);
});

set_exception_handler(function ($e) {
    fwrite(STDERR, ""BUILD FAILED: "" . $e->getMessage() . PHP_EOL);
    exit(1);
});

require __DIR__ . '/vendor/autoload.php';
require __DIR__ . '/index.php';

echo ""BUILD OK\n"";
";

        return files;
    }

    private Dictionary<string, string> GenerateRubyBackend(string? webApiUrl)
    {
        var files = new Dictionary<string, string>();

        // Models/test_projects.rb
        files["backend/Models/test_projects.rb"] = @"class TestProjects
    attr_accessor :id, :name

    def initialize(id: nil, name: '')
        @id = id
        @name = name
    end

    def to_hash
        {
            id: @id,
            name: @name
        }
    end
end
";

        // Controllers/test_controller.rb
        files["backend/Controllers/test_controller.rb"] = @"require_relative '../Models/test_projects'

class TestController
    def initialize(db)
        @db = db
    end

    def set_search_path
        # Set search_path to public schema (required because isolated role has restricted search_path)
        # Using string concatenation to avoid C# string interpolation issues
        @db.exec('SET search_path = public, ""' + '$' + 'user""')
    end

    def get_all
        # Set search_path to public schema (required because isolated role has restricted search_path)
        set_search_path
        result = @db.exec('SELECT ""Id"", ""Name"" FROM ""TestProjects"" ORDER BY ""Id""')
        result.map do |row|
            {
                'Id' => row['Id'].to_i,
                'Name' => row['Name']
            }
        end
        # Do NOT catch generic Exception - let it bubble up to Sinatra error handler
        # PG::Error will be caught by Sinatra's error handler and sent to runtime error endpoint
    end

    def get_by_id(id)
        # Set search_path to public schema (required because isolated role has restricted search_path)
        set_search_path
        result = @db.exec_params('SELECT ""Id"", ""Name"" FROM ""TestProjects"" WHERE ""Id"" = $1', [id])
        return nil if result.ntuples == 0
        
        row = result[0]
        {
            'Id' => row['Id'].to_i,
            'Name' => row['Name']
        }
        # Do NOT catch generic Exception - let it bubble up to Sinatra error handler
    end

    def create(data)
        # Set search_path to public schema (required because isolated role has restricted search_path)
        set_search_path
        result = @db.exec_params('INSERT INTO ""TestProjects"" (""Name"") VALUES ($1) RETURNING ""Id"", ""Name""', [data['name']])
        row = result[0]
        {
            'Id' => row['Id'].to_i,
            'Name' => row['Name']
        }
        # Do NOT catch generic Exception - let it bubble up to Sinatra error handler
    end

    def update(id, data)
        # Set search_path to public schema (required because isolated role has restricted search_path)
        set_search_path
        result = @db.exec_params('UPDATE ""TestProjects"" SET ""Name"" = $1 WHERE ""Id"" = $2 RETURNING ""Id"", ""Name""', [data['name'], id])
        return nil if result.ntuples == 0
        
        row = result[0]
        {
            'Id' => row['Id'].to_i,
            'Name' => row['Name']
        }
        # Do NOT catch generic Exception - let it bubble up to Sinatra error handler
    end

    def delete(id)
        # Set search_path to public schema (required because isolated role has restricted search_path)
        set_search_path
        result = @db.exec_params('DELETE FROM ""TestProjects"" WHERE ""Id"" = $1', [id])
        result.cmd_tuples > 0
        # Do NOT catch generic Exception - let it bubble up to Sinatra error handler
    end
end
";

        // app.rb - Main Sinatra application
        files["backend/app.rb"] = @"require 'sinatra'
require 'pg'
require 'json'
require 'logger'
require_relative 'Controllers/test_controller'

# Configure logging - Warning and Error only
LOG_LEVEL = ENV['LOG_LEVEL'] || 'WARN'
logger = Logger.new(STDOUT)
logger.level = Logger.const_get(LOG_LEVEL)
logger.formatter = proc do |severity, datetime, progname, msg|
  ""[#{severity}] #{datetime}: #{msg}\n""
end

# Use logger in Sinatra
set :logger, logger

# Configure Sinatra to use custom error handler (not show_exceptions)
set :show_exceptions, false  # Disable default error page
set :raise_errors, false     # Don't re-raise errors, use error handler instead

# Port and bind settings - Puma config file (puma.rb) will override these
# But we set them here as fallback
set :port, (ENV['PORT'] || 8080).to_i
set :bind, '0.0.0.0'

# CORS headers
before do
    headers 'Access-Control-Allow-Origin' => '*',
            'Access-Control-Allow-Methods' => 'GET, POST, PUT, DELETE, OPTIONS',
            'Access-Control-Allow-Headers' => 'Content-Type'
end

options '*' do
    200
end

# Global error handler for all exceptions
# This catches ALL exceptions, including those raised in routes
error do
    exception = env['sinatra.error']
    logger.error(""[ERROR HANDLER] Unhandled exception occurred: #{exception.message}"")
    logger.error(""[ERROR HANDLER] Exception class: #{exception.class}"")
    logger.error(exception.backtrace.join(""\n"")) if exception.backtrace
    
    # Extract boardId from request
    board_id = extract_board_id(request)
    logger.warn(""[ERROR HANDLER] Extracted boardId: #{board_id || 'NULL'}"")
    
    # Send error to runtime error endpoint if configured
    runtime_error_endpoint_url = ENV['RUNTIME_ERROR_ENDPOINT_URL']
    logger.warn(""[ERROR HANDLER] RUNTIME_ERROR_ENDPOINT_URL: #{runtime_error_endpoint_url || 'NOT SET'}"")
    
    if runtime_error_endpoint_url && !runtime_error_endpoint_url.empty?
        logger.warn(""[ERROR HANDLER] Sending error to endpoint: #{runtime_error_endpoint_url} (boardId: #{board_id || 'NULL'})"")
        # Use Thread.new for fire-and-forget, but ensure it doesn't die silently
        Thread.new do
            begin
                send_error_to_endpoint(runtime_error_endpoint_url, board_id, request, exception)
            rescue => e
                logger.error(""[ERROR HANDLER] Failed to send error to endpoint: #{e.message}"")
                logger.error(""[ERROR HANDLER] Error backtrace: #{e.backtrace.join(""\n"")}"") if e.backtrace
            end
        end
    else
        logger.warn(""[ERROR HANDLER] RUNTIME_ERROR_ENDPOINT_URL is not set - skipping error reporting"")
    end
    
    # Return error response
    status 500
    content_type :json
    { error: 'An error occurred while processing your request', message: exception.message }.to_json
end

def extract_board_id(request)
    # Try query parameter
    return params['boardId'] if params['boardId']
    
    # Try header
    return request.env['HTTP_X_BOARD_ID'] if request.env['HTTP_X_BOARD_ID']
    
    # Try environment variable
    board_id = ENV['BOARD_ID']
    return board_id if board_id && !board_id.empty?
    
    # Try to extract from hostname (Railway pattern: webapi{boardId}.up.railway.app - no hyphen)
    host = request.host
    if host && (match = host.match(/webapi([a-f0-9]{24})/i))
        return match[1]
    end
    
    # Try to extract from RUNTIME_ERROR_ENDPOINT_URL if it contains boardId pattern
    endpoint_url = ENV['RUNTIME_ERROR_ENDPOINT_URL'] || ''
    if (match = endpoint_url.match(/webapi([a-f0-9]{24})/i))
        return match[1]
    end
    
    nil
end

def send_error_to_endpoint(endpoint_url, board_id, request, exception)
    require 'net/http'
    require 'uri'
    require 'json'
    
    # Get stack trace
    stack_trace = exception.backtrace ? exception.backtrace.join(""\n"") : 'N/A'
    
    # Get file and line from backtrace
    first_line = exception.backtrace ? exception.backtrace.first : nil
    file_name = nil
    line_number = nil
    if first_line && (match = first_line.match(/(.+):(\d+):/))
        file_name = match[1]
        line_number = match[2].to_i
    end
    
    # Ensure boardId is a string (not nil) - C# endpoint requires non-null
    board_id_str = board_id.nil? ? '' : board_id.to_s
    
    payload = {
        boardId: board_id_str,
        timestamp: Time.now.utc.iso8601,
        file: file_name || '',
        line: line_number,
        stackTrace: stack_trace || 'N/A',
        message: exception.message || 'Unknown error',
        exceptionType: exception.class.name || 'Exception',
        requestPath: request.path || '/',
        requestMethod: request.request_method || 'GET',
        userAgent: request.user_agent || ''
    }.to_json
    
    uri = URI(endpoint_url)
    http = Net::HTTP.new(uri.host, uri.port)
    http.use_ssl = (uri.scheme == 'https')
    http.open_timeout = 5
    http.read_timeout = 5
    
    request_obj = Net::HTTP::Post.new(uri.path)
    request_obj['Content-Type'] = 'application/json'
    request_obj.body = payload
    
    begin
        response = http.request(request_obj)
        if response.code.to_i != 200
            logger.warn(""[ERROR HANDLER] Error endpoint response: #{response.code} - #{response.body}"")
        else
            logger.warn(""[ERROR HANDLER] Error endpoint response: #{response.code}"")
        end
    rescue => e
        logger.error(""[ERROR HANDLER] Failed to send error to endpoint: #{e.message}"")
        logger.error(""[ERROR HANDLER] Error backtrace: #{e.backtrace.join(""\n"")}"") if e.backtrace
    end
end

# Database connection
def get_db
    database_url = ENV['DATABASE_URL']
    raise 'DATABASE_URL environment variable not set' unless database_url
    
    PG.connect(database_url)
end

# Helper to parse JSON body
def parse_json_body
    request.body.rewind
    JSON.parse(request.body.read)
rescue JSON::ParserError
    {}
end

# Root endpoint
get '/' do
    content_type :json
    {
        message: 'Backend API is running',
        status: 'ok',
        swagger: '/swagger',
        api: '/api/test'
    }.to_json
end

# Health check
get '/health' do
    content_type :json
    {
        status: 'healthy',
        service: 'Backend API'
    }.to_json
end

# Swagger UI endpoint - serve interactive Swagger UI HTML page
get '/swagger' do
    content_type :html
    <<-HTML
<!DOCTYPE html>
<html>
<head>
    <title>Backend API - Swagger UI</title>
    <link rel=""stylesheet"" type=""text/css"" href=""https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui.css"" />
    <style>
        html { box-sizing: border-box; overflow: -moz-scrollbars-vertical; overflow-y: scroll; }
        *, *:before, *:after { box-sizing: inherit; }
        body { margin:0; background: #fafafa; }
    </style>
</head>
<body>
    <div id=""swagger-ui""></div>
    <script src=""https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui-bundle.js""></script>
    <script src=""https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui-standalone-preset.js""></script>
    <script>
        window.onload = function() {
            const ui = SwaggerUIBundle({
                url: ""/swagger.json"",
                dom_id: ""#swagger-ui"",
                deepLinking: true,
                presets: [
                    SwaggerUIBundle.presets.apis,
                    SwaggerUIStandalonePreset
                ],
                plugins: [
                    SwaggerUIBundle.plugins.DownloadUrl
                ],
                layout: ""StandaloneLayout""
            });
        };
    </script>
</body>
</html>
    HTML
end

# Swagger JSON endpoint - return OpenAPI spec as JSON
get '/swagger.json' do
    content_type :json
    {
        openapi: '3.0.0',
        info: {
            title: 'Backend API',
            version: '1.0.0',
            description: 'Ruby Backend API Documentation'
        },
        paths: {
            '/api/test' => {
                get: {
                    summary: 'Get all test projects',
                    responses: {
                        '200' => {
                            description: 'List of test projects',
                            content: {
                                'application/json' => {
                                    schema: {
                                        type: 'array',
                                        items: { '$ref' => '#/components/schemas/TestProjects' }
                                    }
                                }
                            }
                        }
                    }
                },
                post: {
                    summary: 'Create a new test project',
                    requestBody: {
                        required: true,
                        content: {
                            'application/json' => {
                                schema: { '$ref' => '#/components/schemas/TestProjectsInput' }
                            }
                        }
                    },
                    responses: {
                        '201' => {
                            description: 'Created test project',
                            content: {
                                'application/json' => {
                                    schema: { '$ref' => '#/components/schemas/TestProjects' }
                                }
                            }
                        }
                    }
                }
            },
            '/api/test/{id}' => {
                get: {
                    summary: 'Get test project by ID',
                    parameters: [
                        {
                            name: 'id',
                            'in' => 'path',
                            required: true,
                            schema: { type: 'integer' }
                        }
                    ],
                    responses: {
                        '200' => {
                            description: 'Test project found',
                            content: {
                                'application/json' => {
                                    schema: { '$ref' => '#/components/schemas/TestProjects' }
                                }
                            }
                        },
                        '404' => { description: 'Project not found' }
                    }
                },
                put: {
                    summary: 'Update test project',
                    parameters: [
                        {
                            name: 'id',
                            'in' => 'path',
                            required: true,
                            schema: { type: 'integer' }
                        }
                    ],
                    requestBody: {
                        required: true,
                        content: {
                            'application/json' => {
                                schema: { '$ref' => '#/components/schemas/TestProjectsInput' }
                            }
                        }
                    },
                    responses: {
                        '200' => { description: 'Updated test project' },
                        '404' => { description: 'Project not found' }
                    }
                },
                delete: {
                    summary: 'Delete test project',
                    parameters: [
                        {
                            name: 'id',
                            'in' => 'path',
                            required: true,
                            schema: { type: 'integer' }
                        }
                    ],
                    responses: {
                        '200' => { description: 'Deleted successfully' },
                        '404' => { description: 'Project not found' }
                    }
                }
            }
        },
        components: {
            schemas: {
                TestProjects: {
                    type: 'object',
                    properties: {
                        Id: { type: 'integer' },
                        Name: { type: 'string' }
                    }
                },
                TestProjectsInput: {
                    type: 'object',
                    required: ['Name'],
                    properties: {
                        Name: { type: 'string' }
                    }
                }
            }
        }
    }.to_json
end

# GET /api/test - Get all projects
get '/api/test' do
    content_type :json
    db = get_db
    begin
        controller = TestController.new(db)
        controller.get_all.to_json
    rescue => e
        # Re-raise to trigger Sinatra error handler
        raise e
    ensure
        db&.close
    end
end

get '/api/test/' do
    content_type :json
    db = get_db
    begin
        controller = TestController.new(db)
        controller.get_all.to_json
    ensure
        db&.close
    end
    # Do NOT catch generic Exception - let it bubble up to Sinatra error handler
end

# GET /api/test/:id - Get project by ID
get '/api/test/:id' do
    content_type :json
    db = get_db
    begin
        controller = TestController.new(db)
        result = controller.get_by_id(params['id'].to_i)
        
        if result.nil?
            status 404
            { error: 'Project not found' }.to_json
        else
            result.to_json
        end
    ensure
        db&.close
    end
    # Do NOT catch generic Exception - let it bubble up to Sinatra error handler
end

# POST /api/test - Create project
post '/api/test' do
    content_type :json
    db = get_db
    begin
        controller = TestController.new(db)
        data = parse_json_body
        result = controller.create(data)
        status 201
        result.to_json
    ensure
        db&.close
    end
    # Do NOT catch generic Exception - let it bubble up to Sinatra error handler
end

post '/api/test/' do
    content_type :json
    db = get_db
    begin
        controller = TestController.new(db)
        data = parse_json_body
        result = controller.create(data)
        status 201
        result.to_json
    ensure
        db&.close
    end
    # Do NOT catch generic Exception - let it bubble up to Sinatra error handler
end

# PUT /api/test/:id - Update project
put '/api/test/:id' do
    content_type :json
    db = get_db
    begin
        controller = TestController.new(db)
        data = parse_json_body
        result = controller.update(params['id'].to_i, data)
        
        if result.nil?
            status 404
            { error: 'Project not found' }.to_json
        else
            result.to_json
        end
    ensure
        db&.close
    end
    # Do NOT catch generic Exception - let it bubble up to Sinatra error handler
end

# DELETE /api/test/:id - Delete project
delete '/api/test/:id' do
    content_type :json
    db = get_db
    begin
        controller = TestController.new(db)
        
        if controller.delete(params['id'].to_i)
            { message: 'Deleted successfully' }.to_json
        else
            status 404
            { error: 'Project not found' }.to_json
        end
    ensure
        db&.close
    end
    # Do NOT catch generic Exception - let it bubble up to Sinatra error handler
end

# Startup error handler - catch errors during require or initialization
at_exit do
    if $!
        exception = $!
        logger.error(""[STARTUP ERROR] Application failed to start: #{exception.message}"")
        logger.error(exception.backtrace.join(""\n"")) if exception.backtrace
        
        # Send startup error to endpoint (fire and forget)
        runtime_error_endpoint_url = ENV['RUNTIME_ERROR_ENDPOINT_URL']
        board_id = ENV['BOARD_ID']
        
        if runtime_error_endpoint_url && !runtime_error_endpoint_url.empty?
            Thread.new do
                begin
                    require 'net/http'
                    require 'uri'
                    require 'json'
                    
                    stack_trace = exception.backtrace ? exception.backtrace.join(""\n"") : 'N/A'
                    first_line = exception.backtrace ? exception.backtrace.first : nil
                    file_name = nil
                    line_number = nil
                    if first_line && (match = first_line.match(/(.+):(\d+):/))
                        file_name = match[1]
                        line_number = match[2].to_i
                    end
                    
                    payload = {
                        boardId: board_id,
                        timestamp: Time.now.utc.iso8601,
                        file: file_name,
                        line: line_number,
                        stackTrace: stack_trace,
                        message: exception.message || 'Unknown error',
                        exceptionType: exception.class.name,
                        requestPath: 'STARTUP',
                        requestMethod: 'STARTUP',
                        userAgent: 'STARTUP_ERROR'
                    }.to_json
                    
                    uri = URI(runtime_error_endpoint_url)
                    http = Net::HTTP.new(uri.host, uri.port)
                    http.use_ssl = (uri.scheme == 'https')
                    http.open_timeout = 5
                    http.read_timeout = 5
                    
                    request_obj = Net::HTTP::Post.new(uri.path)
                    request_obj['Content-Type'] = 'application/json'
                    request_obj.body = payload
                    
                    http.request(request_obj)
                rescue => e
                    # Ignore
                end
            end
        end
    end
end
";

        // Gemfile
        files["backend/Gemfile"] = @"source 'https://rubygems.org'

ruby '>=3.0'

gem 'sinatra', '~> 3.0'
gem 'pg', '>= 1.5'
gem 'json'
gem 'puma', '~> 6.0'
";

        // Procfile - Railway uses this if present
        files["backend/Procfile"] = @"web: bundle exec puma -b tcp://0.0.0.0:${PORT:-8080} config.ru
";

        // Gemfile.lock - Minimal lockfile that bundler will update during install
        // We create a minimal version with just direct dependencies to avoid conflicts
        // Bundler will automatically resolve and update transitive dependencies during bundle install
        // The install command removes any existing lockfile first to ensure clean resolution
        // Note: Only x86_64-linux platform (not 'ruby') because pg is a native extension gem
        // and Railway builds on Linux. Including 'ruby' causes bundler to fail resolution.
        files["backend/Gemfile.lock"] = @"GEM
  remote: https://rubygems.org/
  specs:

PLATFORMS
  x86_64-linux

DEPENDENCIES
  json
  pg (>= 1.5)
  puma (~> 6.0)
  sinatra (~> 3.0)

BUNDLED WITH
   2.5.23
";

        // config.ru
        files["backend/config.ru"] = @"require_relative 'app'
run Sinatra::Application
";

        // puma.rb - Puma configuration file to ensure correct binding
        files["backend/puma.rb"] = @"# Puma configuration for Railway deployment
# This ensures Puma binds to 0.0.0.0 (all interfaces) instead of localhost
# Railway sets PORT environment variable - use it or default to 8080

bind ""tcp://0.0.0.0:#{ENV.fetch('PORT', '8080')}""
workers 0  # Single worker mode for Railway
threads 0, 5
";

        // build_check.rb - Full application build validation for Ruby
        files["backend/build_check.rb"] = @"$stdout.sync = true
$stderr.sync = true

begin
  require_relative './app'
  puts ""BUILD OK""
rescue Exception => e
  STDERR.puts ""BUILD FAILED: #{e.class} - #{e.message}""
  STDERR.puts e.backtrace.join(""\n"")
  exit(1)
end
";

        return files;
    }

    private Dictionary<string, string> GenerateGoBackend(string? webApiUrl)
    {
        var files = new Dictionary<string, string>();

        // Models/test_projects.go
        files["backend/Models/test_projects.go"] = @"package models

type TestProjects struct {
    Id   int    `json:""Id"" db:""Id""`
    Name string `json:""Name"" db:""Name""`
}
";

        // Controllers/test_controller.go
        files["backend/Controllers/test_controller.go"] = @"package controllers

import (
    ""database/sql""
    ""encoding/json""
    ""net/http""
    ""strconv""
    
    ""backend/Models""
    _ ""github.com/lib/pq""
)

type TestController struct {
    DB *sql.DB
}

func NewTestController(db *sql.DB) *TestController {
    return &TestController{DB: db}
}

func (tc *TestController) setSearchPath() error {
    // Set search_path to public schema (required because isolated role has restricted search_path)
    // Using string concatenation to avoid C# string interpolation issues
    _, err := tc.DB.Exec(`SET search_path = public, ""$` + `user""`)
    return err
}

func (tc *TestController) GetAll(w http.ResponseWriter, r *http.Request) {
    if err := tc.setSearchPath(); err != nil {
        http.Error(w, ""Database error: ""+err.Error(), http.StatusInternalServerError)
        return
    }
    
    rows, err := tc.DB.Query(`SELECT ""Id"", ""Name"" FROM ""TestProjects"" ORDER BY ""Id""`)
    if err != nil {
        http.Error(w, ""Database error: ""+err.Error(), http.StatusInternalServerError)
        return
    }
    defer rows.Close()
    
    var projects []models.TestProjects
    for rows.Next() {
        var project models.TestProjects
        if err := rows.Scan(&project.Id, &project.Name); err != nil {
            http.Error(w, ""Database error: ""+err.Error(), http.StatusInternalServerError)
            return
        }
        projects = append(projects, project)
    }
    
    w.Header().Set(""Content-Type"", ""application/json"")
    json.NewEncoder(w).Encode(projects)
}

func (tc *TestController) GetById(w http.ResponseWriter, r *http.Request, id int) {
    if err := tc.setSearchPath(); err != nil {
        http.Error(w, ""Database error: ""+err.Error(), http.StatusInternalServerError)
        return
    }
    
    var project models.TestProjects
    err := tc.DB.QueryRow(`SELECT ""Id"", ""Name"" FROM ""TestProjects"" WHERE ""Id"" = $1`, id).
        Scan(&project.Id, &project.Name)

    if err == sql.ErrNoRows {
        http.Error(w, ""Project not found"", http.StatusNotFound)
        return
    }
    if err != nil {
        http.Error(w, ""Database error: ""+err.Error(), http.StatusInternalServerError)
        return
    }
    
    w.Header().Set(""Content-Type"", ""application/json"")
    json.NewEncoder(w).Encode(project)
}

func (tc *TestController) Create(w http.ResponseWriter, r *http.Request) {
    var project models.TestProjects
    if err := json.NewDecoder(r.Body).Decode(&project); err != nil {
        http.Error(w, ""Invalid JSON: ""+err.Error(), http.StatusBadRequest)
        return
    }
    
    if err := tc.setSearchPath(); err != nil {
        http.Error(w, ""Database error: ""+err.Error(), http.StatusInternalServerError)
        return
    }
    
    err := tc.DB.QueryRow(
        `INSERT INTO ""TestProjects"" (""Name"") VALUES ($1) RETURNING ""Id"", ""Name""`,
        project.Name,
    ).Scan(&project.Id, &project.Name)

    if err != nil {
        http.Error(w, ""Database error: ""+err.Error(), http.StatusInternalServerError)
        return
    }
    
    w.Header().Set(""Content-Type"", ""application/json"")
    w.WriteHeader(http.StatusCreated)
    json.NewEncoder(w).Encode(project)
}

func (tc *TestController) Update(w http.ResponseWriter, r *http.Request, id int) {
    var project models.TestProjects
    if err := json.NewDecoder(r.Body).Decode(&project); err != nil {
        http.Error(w, ""Invalid JSON: ""+err.Error(), http.StatusBadRequest)
        return
    }
    
    if err := tc.setSearchPath(); err != nil {
        http.Error(w, ""Database error: ""+err.Error(), http.StatusInternalServerError)
        return
    }
    
    result, err := tc.DB.Exec(
        `UPDATE ""TestProjects"" SET ""Name"" = $1 WHERE ""Id"" = $2`,
        project.Name, id,
    )
    if err != nil {
        http.Error(w, ""Database error: ""+err.Error(), http.StatusInternalServerError)
        return
    }
    
    rowsAffected, err := result.RowsAffected()
    if err != nil {
        http.Error(w, ""Database error: ""+err.Error(), http.StatusInternalServerError)
        return
    }
    
    if rowsAffected == 0 {
        http.Error(w, ""Project not found"", http.StatusNotFound)
        return
    }
    
    project.Id = id
    w.Header().Set(""Content-Type"", ""application/json"")
    json.NewEncoder(w).Encode(project)
}

func (tc *TestController) Delete(w http.ResponseWriter, r *http.Request, id int) {
    if err := tc.setSearchPath(); err != nil {
        http.Error(w, ""Database error: ""+err.Error(), http.StatusInternalServerError)
        return
    }
    
    result, err := tc.DB.Exec(`DELETE FROM ""TestProjects"" WHERE ""Id"" = $1`, id)
    if err != nil {
        http.Error(w, ""Database error: ""+err.Error(), http.StatusInternalServerError)
        return
    }
    
    rowsAffected, err := result.RowsAffected()
    if err != nil {
        http.Error(w, ""Database error: ""+err.Error(), http.StatusInternalServerError)
        return
    }
    
    if rowsAffected == 0 {
        http.Error(w, ""Project not found"", http.StatusNotFound)
        return
    }
    
    w.Header().Set(""Content-Type"", ""application/json"")
    json.NewEncoder(w).Encode(map[string]string{""message"": ""Deleted successfully""})
}

func ExtractId(path string) (int, error) {
    // Extract ID from path like /api/test/123
    idStr := path[len(""/api/test/""):]
    return strconv.Atoi(idStr)
}
";

        // main.go
        files["backend/main.go"] = @"package main

import (
    ""database/sql""
    ""fmt""
    ""io""
    ""log""
    ""net/http""
    ""os""
    ""runtime""
    ""strconv""
    ""strings""
    ""time""

    ""backend/Controllers""
    _ ""github.com/lib/pq""
)

// Configure logging - Warning and Error only
// Create a custom logger that only shows warnings and errors
func init() {
    // Set log flags to include timestamp
    log.SetFlags(log.Ldate | log.Ltime | log.Lshortfile)
    // Note: Go's standard log package doesn't have severity levels,
    // but we can use log.Printf for warnings and log.Fatal/panic for errors
    // For production, consider using logrus or zap for proper log levels
}

func corsMiddleware(next http.Handler) http.Handler {
    return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
        w.Header().Set(""Access-Control-Allow-Origin"", ""*"")
        w.Header().Set(""Access-Control-Allow-Methods"", ""GET, POST, PUT, DELETE, OPTIONS"")
        w.Header().Set(""Access-Control-Allow-Headers"", ""Content-Type"")

        if r.Method == ""OPTIONS"" {
            w.WriteHeader(http.StatusOK)
            return
        }

        next.ServeHTTP(w, r)
    })
}

func panicRecoveryMiddleware(next http.Handler) http.Handler {
    return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
        defer func() {
            if err := recover(); err != nil {
                log.Printf(""[PANIC RECOVERY] Recovered from panic: %v"", err)
                
                // Capture full stack trace including all goroutines to find the actual panic location
                // Use true to get all goroutines, which will include the panic location
                buf := make([]byte, 8192)
                n := runtime.Stack(buf, true)
                stackTrace := string(buf[:n])
                
                // Extract boardId
                boardId := extractBoardId(r)
                log.Printf(""[PANIC RECOVERY] Extracted boardId: %s"", func() string {
                    if boardId == """" { return ""NULL"" }
                    return boardId
                }())
                
                // Send error to runtime error endpoint if configured
                runtimeErrorEndpointUrl := os.Getenv(""RUNTIME_ERROR_ENDPOINT_URL"")
                if runtimeErrorEndpointUrl != """" {
                    log.Printf(""[PANIC RECOVERY] Sending error to endpoint: %s"", runtimeErrorEndpointUrl)
                    go sendErrorToEndpoint(runtimeErrorEndpointUrl, boardId, r, err, stackTrace)
                } else {
                    log.Printf(""[PANIC RECOVERY] RUNTIME_ERROR_ENDPOINT_URL is not set - skipping error reporting"")
                }
                
                // Return error response
                w.Header().Set(""Content-Type"", ""application/json"")
                w.WriteHeader(http.StatusInternalServerError)
                fmt.Fprintf(w, `{""error"":""An error occurred while processing your request"",""message"":""%s""}`, fmt.Sprintf(""%v"", err))
            }
        }()
        
        next.ServeHTTP(w, r)
    })
}

func extractBoardId(r *http.Request) string {
    // Try query parameter
    if boardId := r.URL.Query().Get(""boardId""); boardId != """" {
        return boardId
    }
    
    // Try header
    if boardId := r.Header.Get(""X-Board-Id""); boardId != """" {
        return boardId
    }
    
    // Try environment variable
    if boardId := os.Getenv(""BOARD_ID""); boardId != """" {
        return boardId
    }
    
    // Try to extract from hostname (Railway pattern: webapi{boardId}.up.railway.app - no hyphen)
    host := r.Host
    if host != """" {
        // Simple regex-like matching using strings
        if idx := strings.Index(strings.ToLower(host), ""webapi""); idx >= 0 {
            remaining := host[idx+6:] // Skip ""webapi""
            if len(remaining) >= 24 {
                // Check if next 24 chars are hex
                boardId := remaining[:24]
                if isValidHex(boardId) {
                    return boardId
                }
            }
        }
    }
    
    // Try to extract from RUNTIME_ERROR_ENDPOINT_URL if it contains boardId pattern
    endpointUrl := os.Getenv(""RUNTIME_ERROR_ENDPOINT_URL"")
    if endpointUrl != """" {
        if idx := strings.Index(strings.ToLower(endpointUrl), ""webapi""); idx >= 0 {
            remaining := endpointUrl[idx+6:]
            if len(remaining) >= 24 {
                boardId := remaining[:24]
                if isValidHex(boardId) {
                    return boardId
                }
            }
        }
    }
    
    return """"
}

func isValidHex(s string) bool {
    for _, c := range s {
        if !((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')) {
            return false
        }
    }
    return true
}

func sendErrorToEndpoint(endpointUrl, boardId string, r *http.Request, err interface{}, stackTrace string) {
    // Parse stack trace to extract file and line number from the actual panic location
    // Go stack trace format: 
    // goroutine X [running]:
    // main.functionName(...)
    //     /path/to/file.go:123 +0x...
    var fileName string
    var lineNumber int
    
    lines := strings.Split(stackTrace, ""\n"")
    // Go stack trace format (with all goroutines):
    // goroutine X [running]:
    // main.panicRecoveryMiddleware.func1.1(...)
    //     /app/main.go:61 +0x...
    // goroutine Y [running]:
    // main.testController.GetAll(...)
    //     /app/Controllers/test_controller.go:33 +0x...
    // 
    // Look through all goroutines to find the actual panic location
    // Skip panic recovery and error sending functions
    for i, line := range lines {
        // Skip goroutine header lines
        if strings.HasPrefix(line, ""goroutine"") {
            continue
        }
        
        // Look for file:line entries
        if strings.Contains(line, "".go:"") && i > 0 {
            // Get the previous line (function name)
            prevLine := """"
            if i > 0 {
                prevLine = lines[i-1]
            }
            
            // Skip if it's from panic recovery, error sending, or runtime functions
            if strings.Contains(prevLine, ""panicRecoveryMiddleware"") || 
               strings.Contains(prevLine, ""sendErrorToEndpoint"") ||
               strings.Contains(prevLine, ""runtime.Stack"") ||
               strings.Contains(prevLine, ""runtime.gopanic"") ||
               strings.Contains(prevLine, ""created by"") ||
               strings.Contains(prevLine, ""panic("") {
                continue
            }
            
            // Extract file path and line number from the indented line
            // Format: ""\t/path/to/file.go:123 +0x...""
            trimmedLine := strings.TrimSpace(line)
            parts := strings.Split(trimmedLine, "":"")
            if len(parts) >= 2 {
                // Get file path (everything before the last "":"")
                filePath := strings.TrimSpace(strings.Join(parts[:len(parts)-1], "":""))
                
                // Skip standard library and runtime files
                // Check for common Go standard library paths
                if strings.Contains(filePath, ""/runtime/"") ||
                   strings.Contains(filePath, ""/mise/installs/go/"") ||
                   strings.Contains(filePath, ""/src/runtime/"") ||
                   strings.Contains(filePath, ""/src/net/"") ||
                   strings.Contains(filePath, ""/src/syscall/"") ||
                   strings.Contains(filePath, ""/src/internal/"") ||
                   strings.Contains(filePath, ""/src/database/"") ||
                   strings.Contains(filePath, ""/usr/local/go/"") ||
                   strings.Contains(filePath, ""/usr/lib/go/"") {
                    continue
                }
                
                // Get the last part which should be the line number (may have offset like ""123 +0x9c"")
                lineStr := strings.TrimSpace(parts[len(parts)-1])
                // Remove any offset info (e.g., "" +0x9c"")
                if spaceIdx := strings.Index(lineStr, "" ""); spaceIdx > 0 {
                    lineStr = lineStr[:spaceIdx]
                }
                if lineNum, parseErr := strconv.Atoi(lineStr); parseErr == nil {
                    lineNumber = lineNum
                    // Extract just the filename
                    if lastSlash := strings.LastIndex(filePath, ""/""); lastSlash >= 0 {
                        fileName = filePath[lastSlash+1:]
                    } else {
                        fileName = filePath
                    }
                    // Found a valid file/line that's not in recovery or standard library - use it
                    break
                }
            }
        }
    }
    
    // Escape stack trace for JSON (handle newlines, backslashes, and quotes)
    escapedStackTrace := strings.ReplaceAll(stackTrace, `\`, `\\`)
    escapedStackTrace = strings.ReplaceAll(escapedStackTrace, `""`, `\""`)
    escapedStackTrace = strings.ReplaceAll(escapedStackTrace, ""\n"", `\n`)
    escapedStackTrace = strings.ReplaceAll(escapedStackTrace, ""\r"", `\r`)
    escapedStackTrace = strings.ReplaceAll(escapedStackTrace, ""\t"", `\t`)
    
    message := strings.ReplaceAll(strings.ReplaceAll(fmt.Sprintf(""%v"", err), `\`, `\\`), `""`, `\""`)
    
    // Build payload with file and line information
    fileJson := ""null""
    if fileName != """" {
        fileJson = `""` + strings.ReplaceAll(fileName, `""`, `\""`) + `""`
    }
    
    lineJson := ""null""
    if lineNumber > 0 {
        lineJson = fmt.Sprintf(""%d"", lineNumber)
    }
    
    payload := fmt.Sprintf(`{
        ""boardId"":%s,
        ""timestamp"":""%s"",
        ""file"":%s,
        ""line"":%s,
        ""stackTrace"":""%s"",
        ""message"":""%s"",
        ""exceptionType"":""panic"",
        ""requestPath"":""%s"",
        ""requestMethod"":""%s"",
        ""userAgent"":""%s""
    }`,
        func() string {
            if boardId == """" { return ""null"" }
            return `""` + boardId + `""`
        }(),
        time.Now().UTC().Format(time.RFC3339),
        fileJson,
        lineJson,
        escapedStackTrace,
        message,
        r.URL.Path,
        r.Method,
        r.UserAgent(),
    )
    
    // Send POST request (fire and forget)
    req, err2 := http.NewRequest(""POST"", endpointUrl, strings.NewReader(payload))
    if err2 != nil {
        log.Printf(""[PANIC RECOVERY] Failed to create request: %v"", err2)
        return
    }
    
    req.Header.Set(""Content-Type"", ""application/json"")
    client := &http.Client{Timeout: 5 * time.Second}
    
    resp, err2 := client.Do(req)
    if err2 != nil {
        log.Printf(""[PANIC RECOVERY] Failed to send error to endpoint: %v"", err2)
        return
    }
    defer resp.Body.Close()
    
    if resp.StatusCode != 200 {
        body, _ := io.ReadAll(resp.Body)
        log.Printf(""[PANIC RECOVERY] Error endpoint response: %d - %s"", resp.StatusCode, string(body))
    } else {
        log.Printf(""[PANIC RECOVERY] Error endpoint response: %d"", resp.StatusCode)
    }
}

func main() {
    databaseUrl := os.Getenv(""DATABASE_URL"")
    if databaseUrl == """" {
        log.Fatal(""DATABASE_URL environment variable not set"")
    }

    db, err := sql.Open(""postgres"", databaseUrl)
    if err != nil {
        log.Fatal(""Failed to connect to database: "", err)
    }
    defer db.Close()

    if err := db.Ping(); err != nil {
        log.Fatal(""Failed to ping database: "", err)
    }

    controller := controllers.NewTestController(db)
    mux := http.NewServeMux()

    // Apply panic recovery middleware to all routes
    handler := panicRecoveryMiddleware(corsMiddleware(mux))

    mux.HandleFunc(""/"", func(w http.ResponseWriter, r *http.Request) {
        if r.URL.Path != ""/"" {
            http.NotFound(w, r)
            return
        }
        w.Header().Set(""Content-Type"", ""application/json"")
        fmt.Fprintf(w, `{""message"":""Backend API is running"",""status"":""ok"",""swagger"":""/swagger"",""api"":""/api/test""}`)
    })

    mux.HandleFunc(""/health"", func(w http.ResponseWriter, r *http.Request) {
        w.Header().Set(""Content-Type"", ""application/json"")
        fmt.Fprintf(w, `{""status"":""healthy"",""service"":""Backend API""}`)
    })

    // Swagger UI endpoint - serve interactive Swagger UI HTML page
    mux.HandleFunc(""/swagger"", func(w http.ResponseWriter, r *http.Request) {
        w.Header().Set(""Content-Type"", ""text/html"")
        fmt.Fprintf(w, `<!DOCTYPE html>
<html>
<head>
    <title>Backend API - Swagger UI</title>
    <link rel=""stylesheet"" type=""text/css"" href=""https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui.css"" />
    <style>
        html { box-sizing: border-box; overflow: -moz-scrollbars-vertical; overflow-y: scroll; }
        *, *:before, *:after { box-sizing: inherit; }
        body { margin:0; background: #fafafa; }
    </style>
</head>
<body>
    <div id=""swagger-ui""></div>
    <script src=""https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui-bundle.js""></script>
    <script src=""https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui-standalone-preset.js""></script>
    <script>
        window.onload = function() {
            const ui = SwaggerUIBundle({
                url: ""/swagger.json"",
                dom_id: ""#swagger-ui"",
                deepLinking: true,
                presets: [
                    SwaggerUIBundle.presets.apis,
                    SwaggerUIStandalonePreset
                ],
                plugins: [
                    SwaggerUIBundle.plugins.DownloadUrl
                ],
                layout: ""StandaloneLayout""
            });
        };
    </script>
</body>
</html>`)
    })

    // Swagger JSON endpoint - return OpenAPI spec as JSON
    mux.HandleFunc(""/swagger.json"", func(w http.ResponseWriter, r *http.Request) {
        w.Header().Set(""Content-Type"", ""application/json"")
        fmt.Fprintf(w, `{
  ""openapi"": ""3.0.0"",
  ""info"": {
    ""title"": ""Backend API"",
    ""version"": ""1.0.0"",
    ""description"": ""Go Backend API Documentation""
  },
  ""paths"": {
    ""/api/test"": {
      ""get"": {
        ""summary"": ""Get all test projects"",
        ""responses"": {
          ""200"": {
            ""description"": ""List of test projects"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""type"": ""array"",
                  ""items"": {
                    ""$ref"": ""#/components/schemas/TestProjects""
                  }
                }
              }
            }
          }
        }
      },
      ""post"": {
        ""summary"": ""Create a new test project"",
        ""requestBody"": {
          ""required"": true,
          ""content"": {
            ""application/json"": {
              ""schema"": {
                ""$ref"": ""#/components/schemas/TestProjectsInput""
              }
            }
          }
        },
        ""responses"": {
          ""201"": {
            ""description"": ""Created test project"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/TestProjects""
                }
              }
            }
          }
        }
      }
    },
    ""/api/test/{id}"": {
      ""get"": {
        ""summary"": ""Get test project by ID"",
        ""parameters"": [
          {
            ""name"": ""id"",
            ""in"": ""path"",
            ""required"": true,
            ""schema"": {
              ""type"": ""integer""
            }
          }
        ],
        ""responses"": {
          ""200"": {
            ""description"": ""Test project found"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/TestProjects""
                }
              }
            }
          },
          ""404"": {
            ""description"": ""Project not found""
          }
        }
      },
      ""put"": {
        ""summary"": ""Update test project"",
        ""parameters"": [
          {
            ""name"": ""id"",
            ""in"": ""path"",
            ""required"": true,
            ""schema"": {
              ""type"": ""integer""
            }
          }
        ],
        ""requestBody"": {
          ""required"": true,
          ""content"": {
            ""application/json"": {
              ""schema"": {
                ""$ref"": ""#/components/schemas/TestProjectsInput""
              }
            }
          }
        },
        ""responses"": {
          ""200"": {
            ""description"": ""Updated test project""
          },
          ""404"": {
            ""description"": ""Project not found""
          }
        }
      },
      ""delete"": {
        ""summary"": ""Delete test project"",
        ""parameters"": [
          {
            ""name"": ""id"",
            ""in"": ""path"",
            ""required"": true,
            ""schema"": {
              ""type"": ""integer""
            }
          }
        ],
        ""responses"": {
          ""200"": {
            ""description"": ""Deleted successfully""
          },
          ""404"": {
            ""description"": ""Project not found""
          }
        }
      }
    }
  },
  ""components"": {
    ""schemas"": {
      ""TestProjects"": {
        ""type"": ""object"",
        ""properties"": {
          ""Id"": {
            ""type"": ""integer""
          },
          ""Name"": {
            ""type"": ""string""
          }
        }
      },
      ""TestProjectsInput"": {
        ""type"": ""object"",
        ""required"": [""Name""],
        ""properties"": {
          ""Name"": {
            ""type"": ""string""
          }
        }
      }
    }
  }
}`)
    })

    // API routes handler function
    apiTestHandler := func(w http.ResponseWriter, r *http.Request) {
        path := r.URL.Path
        
        // Handle /api/test and /api/test/ (no ID) - normalize trailing slash
        if path == ""/api/test"" || path == ""/api/test/"" {
            switch r.Method {
            case ""GET"":
                controller.GetAll(w, r)
            case ""POST"":
                controller.Create(w, r)
            default:
                http.Error(w, ""Method not allowed"", http.StatusMethodNotAllowed)
            }
            return
        }
        
        // Handle /api/test/:id
        if strings.HasPrefix(path, ""/api/test/"") {
            idStr := strings.TrimPrefix(path, ""/api/test/"")
            if idStr == """" {
                // Empty ID after /api/test/, treat as /api/test/
                switch r.Method {
                case ""GET"":
                    controller.GetAll(w, r)
                case ""POST"":
                    controller.Create(w, r)
                default:
                    http.Error(w, ""Method not allowed"", http.StatusMethodNotAllowed)
                }
                return
            }
            
            id, err := strconv.Atoi(idStr)
            if err != nil {
                http.Error(w, ""Invalid ID"", http.StatusBadRequest)
                return
            }
            
            switch r.Method {
            case ""GET"":
                controller.GetById(w, r, id)
            case ""PUT"":
                controller.Update(w, r, id)
            case ""DELETE"":
                controller.Delete(w, r, id)
            default:
                http.Error(w, ""Method not allowed"", http.StatusMethodNotAllowed)
            }
            return
        }
        
        http.NotFound(w, r)
    }

    // Register both /api/test and /api/test/ to handle trailing slashes
    mux.HandleFunc(""/api/test"", apiTestHandler)
    mux.HandleFunc(""/api/test/"", apiTestHandler)

    // Apply panic recovery middleware FIRST, then CORS middleware
    // Note: handler is already declared above, so use assignment instead of declaration
    handler = panicRecoveryMiddleware(corsMiddleware(mux))

    port := os.Getenv(""PORT"")
    if port == """" {
        port = ""8080""
    }

    log.Printf(""Server starting on 0.0.0.0:%s"", port)
    
    // Declare variables for startup error handling (used in defer and error handler)
    runtimeErrorEndpointUrl := os.Getenv(""RUNTIME_ERROR_ENDPOINT_URL"")
    boardId := os.Getenv(""BOARD_ID"")
    
    // Startup error handler
    defer func() {
        if r := recover(); r != nil {
            log.Printf(""[STARTUP ERROR] Application failed to start: %v"", r)
            
            // Send startup error to endpoint (fire and forget)
            if runtimeErrorEndpointUrl != """" {
                go func() {
                    // Get full stack trace
                    buf := make([]byte, 4096)
                    n := runtime.Stack(buf, false)
                    stackTrace := string(buf[:n])
                    
                    // Parse stack trace to extract file and line number
                    var fileName string
                    var lineNumber int
                    
                    lines := strings.Split(stackTrace, ""\n"")
                    for i, line := range lines {
                        if strings.Contains(line, "".go:"") && i > 0 {
                            parts := strings.Split(line, "":"")
                            if len(parts) >= 2 {
                                lineStr := strings.TrimSpace(parts[len(parts)-1])
                                if lineNum, parseErr := strconv.Atoi(lineStr); parseErr == nil {
                                    lineNumber = lineNum
                                    filePath := strings.TrimSpace(strings.Join(parts[:len(parts)-1], "":""))
                                    if lastSlash := strings.LastIndex(filePath, ""/""); lastSlash >= 0 {
                                        fileName = filePath[lastSlash+1:]
                                    } else {
                                        fileName = filePath
                                    }
                                    break
                                }
                            }
                        }
                    }
                    
                    // Escape stack trace for JSON (handle newlines, backslashes, and quotes)
                    escapedStackTrace := strings.ReplaceAll(stackTrace, `\`, `\\`)
                    escapedStackTrace = strings.ReplaceAll(escapedStackTrace, `""`, `\""`)
                    escapedStackTrace = strings.ReplaceAll(escapedStackTrace, ""\n"", `\n`)
                    escapedStackTrace = strings.ReplaceAll(escapedStackTrace, ""\r"", `\r`)
                    escapedStackTrace = strings.ReplaceAll(escapedStackTrace, ""\t"", `\t`)
                    
                    message := strings.ReplaceAll(strings.ReplaceAll(fmt.Sprintf(""%v"", r), `\`, `\\`), `""`, `\""`)
                    
                    fileJson := ""null""
                    if fileName != """" {
                        fileJson = `""` + strings.ReplaceAll(fileName, `""`, `\""`) + `""`
                    }
                    
                    lineJson := ""null""
                    if lineNumber > 0 {
                        lineJson = fmt.Sprintf(""%d"", lineNumber)
                    }
                    
                    payload := fmt.Sprintf(`{
                        ""boardId"":%s,
                        ""timestamp"":""%s"",
                        ""file"":%s,
                        ""line"":%s,
                        ""stackTrace"":""%s"",
                        ""message"":""%s"",
                        ""exceptionType"":""panic"",
                        ""requestPath"":""STARTUP"",
                        ""requestMethod"":""STARTUP"",
                        ""userAgent"":""STARTUP_ERROR""
                    }`,
                        func() string {
                            if boardId == """" { return ""null"" }
                            return `""` + boardId + `""`
                        }(),
                        time.Now().UTC().Format(time.RFC3339),
                        fileJson,
                        lineJson,
                        escapedStackTrace,
                        message,
                    )
                    
                    req, err2 := http.NewRequest(""POST"", runtimeErrorEndpointUrl, strings.NewReader(payload))
                    if err2 != nil {
                        return
                    }
                    
                    req.Header.Set(""Content-Type"", ""application/json"")
                    client := &http.Client{Timeout: 5 * time.Second}
                    
                    client.Do(req) // Fire and forget
                }()
            }
            
            os.Exit(1)
        }
    }()
    
    if err = http.ListenAndServe(""0.0.0.0:""+port, handler); err != nil {
        log.Printf(""[STARTUP ERROR] Server failed to start: %v"", err)
        
        // Send startup error to endpoint (same as above)
        // Note: runtimeErrorEndpointUrl and boardId are already declared above
        if runtimeErrorEndpointUrl != """" {
            go func() {
                // Get full stack trace
                buf := make([]byte, 4096)
                n := runtime.Stack(buf, false)
                stackTrace := string(buf[:n])
                
                // Parse stack trace to extract file and line number
                var fileName string
                var lineNumber int
                
                lines := strings.Split(stackTrace, ""\n"")
                for i, line := range lines {
                    if strings.Contains(line, "".go:"") && i > 0 {
                        parts := strings.Split(line, "":"")
                        if len(parts) >= 2 {
                            lineStr := strings.TrimSpace(parts[len(parts)-1])
                            if lineNum, parseErr := strconv.Atoi(lineStr); parseErr == nil {
                                lineNumber = lineNum
                                filePath := strings.TrimSpace(strings.Join(parts[:len(parts)-1], "":""))
                                if lastSlash := strings.LastIndex(filePath, ""/""); lastSlash >= 0 {
                                    fileName = filePath[lastSlash+1:]
                                } else {
                                    fileName = filePath
                                }
                                break
                            }
                        }
                    }
                }
                
                // Escape stack trace for JSON (handle newlines, backslashes, and quotes)
                escapedStackTrace := strings.ReplaceAll(stackTrace, `\`, `\\`)
                escapedStackTrace = strings.ReplaceAll(escapedStackTrace, `""`, `\""`)
                escapedStackTrace = strings.ReplaceAll(escapedStackTrace, ""\n"", `\n`)
                escapedStackTrace = strings.ReplaceAll(escapedStackTrace, ""\r"", `\r`)
                escapedStackTrace = strings.ReplaceAll(escapedStackTrace, ""\t"", `\t`)
                
                message := strings.ReplaceAll(strings.ReplaceAll(fmt.Sprintf(""%v"", err), `\`, `\\`), `""`, `\""`)
                
                fileJson := ""null""
                if fileName != """" {
                    fileJson = `""` + strings.ReplaceAll(fileName, `""`, `\""`) + `""`
                }
                
                lineJson := ""null""
                if lineNumber > 0 {
                    lineJson = fmt.Sprintf(""%d"", lineNumber)
                }
                
                payload := fmt.Sprintf(`{
                    ""boardId"":%s,
                    ""timestamp"":""%s"",
                    ""file"":%s,
                    ""line"":%s,
                    ""stackTrace"":""%s"",
                    ""message"":""%s"",
                    ""exceptionType"":""error"",
                    ""requestPath"":""STARTUP"",
                    ""requestMethod"":""STARTUP"",
                    ""userAgent"":""STARTUP_ERROR""
                }`,
                    func() string {
                        if boardId == """" { return ""null"" }
                        return `""` + boardId + `""`
                    }(),
                    time.Now().UTC().Format(time.RFC3339),
                    fileJson,
                    lineJson,
                    escapedStackTrace,
                    message,
                )
                
                req, err2 := http.NewRequest(""POST"", runtimeErrorEndpointUrl, strings.NewReader(payload))
                if err2 != nil {
                    return
                }
                
                req.Header.Set(""Content-Type"", ""application/json"")
                client := &http.Client{Timeout: 5 * time.Second}
                
                client.Do(req) // Fire and forget
            }()
        }
        
        os.Exit(1)
    }
}
";

        // go.mod
        files["backend/go.mod"] = @"module backend

go 1.21

require (
    github.com/lib/pq v1.10.9
)
";

        // go.sum - Include valid checksums to allow Railway's auto-detection to build
        // Railway may auto-detect Go and build before nixpacks.toml phases run
        // These are the correct checksums for github.com/lib/pq v1.10.9
        // If checksum mismatch occurs, Railway's `go mod tidy` in install phase will update them
        files["backend/go.sum"] = @"github.com/lib/pq v1.10.9 h1:YXG7RB+JIjhP29X+OtkiDnYaXQwpS4JEWq7dtCCRUEw=
github.com/lib/pq v1.10.9/go.mod h1:AlVN5x4E4T544tWzH6hKfbfQvm3HdbOxrmggDNAPY9o=
";

        return files;
    }

    private string GenerateGitHubActionsWorkflow()
    {
        return @"name: Deploy Frontend to GitHub Pages

on:
  push:
    branches:
      - main
    paths:
      - 'index.html'
      - 'config.js'
      - 'style.css'
      - 'frontend/**'
      - '.github/workflows/deploy-frontend.yml'
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

concurrency:
  group: ""pages""
  cancel-in-progress: false

jobs:
  deploy:
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4
      
      - name: Setup Pages
        uses: actions/configure-pages@v4
      
      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          # Upload root-level frontend files (index.html, config.js, style.css are at root)
          path: '.'
      
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
";
    }

    /// <summary>
    /// Generates GitHub Actions workflow for deploying backend to Railway
    /// </summary>
    private string GenerateRailwayDeploymentWorkflow(string programmingLanguage)
    {
        // Build commands vary by programming language
        var buildCommands = programmingLanguage?.ToLowerInvariant() switch
        {
            "c#" or "csharp" => @"      - name: Build .NET application
        working-directory: ./backend
        run: |
          dotnet restore Backend.csproj
          dotnet publish Backend.csproj -c Release -o ./publish",
            "python" => @"      - name: Install Python dependencies
        working-directory: ./backend
        run: |
          pip install -r requirements.txt",
            "nodejs" or "node.js" or "node" => @"      - name: Install Node.js dependencies
        working-directory: ./backend
        run: |
          npm install",
            "java" => @"      - name: Build Java application
        working-directory: ./backend
        run: |
          mvn clean package || gradle build || echo 'Build system not detected'",
            _ => @"      - name: Build .NET application (default)
        working-directory: ./backend
        run: |
          dotnet restore Backend.csproj
          dotnet publish Backend.csproj -c Release -o ./publish"
        };

        return $@"name: Deploy Backend to Railway

on:
  push:
    branches:
      - main
    paths:
      - 'backend/**'
  workflow_dispatch:

permissions:
  contents: read

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4
      
      - name: Setup Railway CLI
        uses: bervProject/setup-railway@v2.0.0
        with:
          railway_token: ${{{{ secrets.RAILWAY_TOKEN }}}}
      
{buildCommands}
      
      - name: Deploy to Railway
        working-directory: ./backend
        env:
          RAILWAY_SERVICE_ID: ${{{{ secrets.RAILWAY_SERVICE_ID }}}}
        run: |
          railway up --service $RAILWAY_SERVICE_ID --detach
";
    }

    /// <summary>
    /// Generates GitHub Actions workflow for deploying backend to Railway (files at root level)
    /// </summary>
    private string GenerateRailwayDeploymentWorkflowAtRoot(string programmingLanguage)
    {
        // Build commands vary by programming language (files are at root, no backend/ directory)
        var buildCommands = programmingLanguage?.ToLowerInvariant() switch
        {
            "c#" or "csharp" => @"      - name: Build .NET application
        run: |
          dotnet restore Backend.csproj
          dotnet publish Backend.csproj -c Release -o ./out",
            "python" => @"      - name: Install Python dependencies
        run: |
          pip install -r requirements.txt",
            "nodejs" or "node.js" or "node" => @"      - name: Install Node.js dependencies
        run: |
          npm install",
            "java" => @"      - name: Build Java application
        run: |
          mvn clean package || gradle build || echo 'Build system not detected'",
            _ => @"      - name: Build .NET application (default)
        run: |
          dotnet restore Backend.csproj
          dotnet publish Backend.csproj -c Release -o ./out"
        };

        return $@"name: Deploy Backend to Railway

on:
  push:
    branches:
      - main
  workflow_dispatch:

permissions:
  contents: read

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4
      
      - name: Setup Railway CLI
        uses: bervProject/setup-railway@v2.0.0
        with:
          railway_token: ${{{{ secrets.RAILWAY_TOKEN }}}}
      
{buildCommands}
      
      - name: Deploy to Railway
        env:
          RAILWAY_SERVICE_ID: ${{{{ secrets.RAILWAY_SERVICE_ID }}}}
        run: |
          railway up --service $RAILWAY_SERVICE_ID --detach
";
    }

    public string GenerateConfigJs(string? webApiUrl, string? mentorApiBaseUrl = null)
    {
        // Don't use Railway project URLs - they're not valid API endpoints
        var isProjectUrl = !string.IsNullOrEmpty(webApiUrl) && webApiUrl.Contains("railway.app/project/");
        var apiUrl = !string.IsNullOrEmpty(webApiUrl) && !isProjectUrl ? webApiUrl : "";
        
        // Convert HTTP to HTTPS for Railway URLs (required for Mixed Content security)
        if (!string.IsNullOrEmpty(apiUrl) && apiUrl.StartsWith("http://") && apiUrl.Contains("railway.app"))
        {
            apiUrl = apiUrl.Replace("http://", "https://");
        }
        
        // Mentor API base URL (StrAppers backend) - used for frontend error logging
        var mentorApiUrl = !string.IsNullOrEmpty(mentorApiBaseUrl) ? mentorApiBaseUrl.TrimEnd('/') : "";
        
        var warningComment = string.IsNullOrEmpty(apiUrl)
            ? @"// API Configuration
// The backend service URL will be automatically configured after deployment.
// If this is empty after deployment, check Railway dashboard:
// Service → Settings → Domains to get your service URL.
"
            : @"// API Configuration
// Backend service URL (automatically configured)
";
        
        return $@"// 1. Define Configuration First
{warningComment}const CONFIG = {{
    API_URL: ""{apiUrl}""
}};

// Ensure CONFIG is globally accessible
if (typeof window !== 'undefined') {{
    window.CONFIG = CONFIG;
}}

// 2. Mentor Tracking Logic
// --- MENTOR TRACKING START ---
(function() {{
    // Extract boardId from API_URL pattern: https://webapi{{boardId}}.railway.app (no hyphen)
    function getBoardId() {{
        // Try to extract from CONFIG.API_URL pattern: https://webapi{{boardId}}.railway.app
        // Fixed Regex: Removed the hyphen after 'webapi' to match actual Railway URL pattern
        const apiUrl = (typeof CONFIG !== 'undefined' && CONFIG?.API_URL) ? CONFIG.API_URL : '';
        if (apiUrl) {{
            const match = apiUrl.match(/webapi([a-f0-9]{{24}})/i);
            if (match && match[1]) {{
                return match[1];
            }}
        }}
        
        // No fallback - if CONFIG.API_URL is not available or doesn't contain boardId, return null
        // This prevents logging with incorrect boardIds
        console.warn('Mentor tracking: CONFIG.API_URL not available or does not contain valid boardId pattern');
        return null;
    }}
    
    // Get Mentor API base URL (StrAppers backend) for error logging
    function getMentorApiBaseUrl() {{
        // Use configured Mentor API URL if available, otherwise fallback to current origin
        {(string.IsNullOrEmpty(mentorApiUrl) ? "return window.location.origin;" : $"return \"{mentorApiUrl}\";")}
    }}
    
    // Log successful page load (fires after page is fully loaded)
    // Delay to ensure CONFIG is loaded from config.js
    window.addEventListener('load', function() {{
        // Wait a bit for config.js to load if it's loaded asynchronously
        setTimeout(function() {{
            const boardId = getBoardId();
            if (!boardId) {{
                console.warn('Mentor tracking: BoardId not found, skipping success log. CONFIG.API_URL may not be loaded yet.');
                return;
            }}
            
            const mentorApiBaseUrl = getMentorApiBaseUrl();
            const frontendLogEndpoint = mentorApiBaseUrl + '/api/Mentor/runtime-error-frontend';
            
            fetch(frontendLogEndpoint, {{
                method: 'POST',
                headers: {{ 'Content-Type': 'application/json' }},
                body: JSON.stringify({{
                    boardId: boardId,
                    type: 'FRONTEND_SUCCESS',
                    timestamp: new Date().toISOString(),
                    message: 'Frontend page loaded successfully'
                }})
            }}).catch(err => console.warn(""Mentor success log failed"", err));
        }}, 100); // Small delay to ensure config.js is loaded
    }});
    
    // Catch JavaScript errors
    window.onerror = function(message, source, lineno, colno, error) {{
        const boardId = getBoardId();
        if (!boardId) {{
            console.warn('Mentor tracking: BoardId not found, skipping error log. CONFIG.API_URL:', 
                (typeof CONFIG !== 'undefined' && CONFIG?.API_URL) ? CONFIG.API_URL : 'CONFIG not defined');
            return false;
        }}
        
        console.log('Mentor tracking: Logging runtime error with boardId:', boardId);
        
        const mentorApiBaseUrl = getMentorApiBaseUrl();
        const frontendLogEndpoint = mentorApiBaseUrl + '/api/Mentor/runtime-error-frontend';
        
        const payload = {{
            boardId: boardId,
            type: 'FRONTEND_RUNTIME',
            message: message || 'Unknown error',
            file: source || 'Unknown',
            line: lineno || null,
            column: colno || null,
            stack: error ? error.stack : 'N/A',
            timestamp: new Date().toISOString()
        }};

        fetch(frontendLogEndpoint, {{
            method: 'POST',
            headers: {{ 'Content-Type': 'application/json' }},
            body: JSON.stringify(payload)
        }}).then(response => {{
            if (!response.ok) {{
                console.warn('Mentor error log failed:', response.status, response.statusText);
            }}
        }}).catch(err => console.warn(""Mentor error log failed"", err));

        return false; // Allows the error to still appear in the browser console
    }};

    // Catch unhandled promise rejections (failed API calls)
    window.onunhandledrejection = function(event) {{
        const boardId = getBoardId();
        if (!boardId) {{
            console.warn('Mentor tracking: BoardId not found, skipping promise rejection log');
            return;
        }}
        
        const mentorApiBaseUrl = getMentorApiBaseUrl();
        const frontendLogEndpoint = mentorApiBaseUrl + '/api/Mentor/runtime-error-frontend';
        
        const payload = {{
            boardId: boardId,
            type: 'FRONTEND_PROMISE_REJECTION',
            message: event.reason?.message || String(event.reason) || 'Unhandled promise rejection',
            stack: event.reason?.stack || 'N/A',
            timestamp: new Date().toISOString()
        }};

        fetch(frontendLogEndpoint, {{
            method: 'POST',
            headers: {{ 'Content-Type': 'application/json' }},
            body: JSON.stringify(payload)
        }}).catch(err => console.warn(""Mentor promise rejection log failed"", err));
    }};
}})();
// --- MENTOR TRACKING END ---
";
    }

    private string GenerateStyleCss()
    {
        return @"/* Global Styles */
* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
    line-height: 1.6;
    color: #333;
    background-color: #f4f4f4;
}

.container {
    max-width: 1200px;
    margin: 0 auto;
    padding: 20px;
}

button {
    padding: 10px 20px;
    background-color: #667eea;
    color: white;
    border: none;
    border-radius: 5px;
    cursor: pointer;
    font-size: 16px;
    transition: background-color 0.3s;
}

button:hover {
    background-color: #5568d3;
}

button:disabled {
    background-color: #ccc;
    cursor: not-allowed;
}

#response {
    margin-top: 20px;
    padding: 15px;
    border-radius: 5px;
    display: none;
}

#response.success {
    background-color: #d4edda;
    border: 1px solid #c3e6cb;
    color: #155724;
    display: block;
}

#response.error {
    background-color: #f8d7da;
    border: 1px solid #f5c6cb;
    color: #721c24;
    display: block;
}
";
    }

    private string GenerateGitIgnore(string programmingLanguage)
    {
        var lang = programmingLanguage?.ToLowerInvariant() ?? "csharp";
        
        switch (lang)
        {
            case "c#":
            case "csharp":
                return @"## Ignore Visual Studio temporary files, build results, and
## files generated by popular Visual Studio add-ons.

# User-specific files
*.rsuser
*.suo
*.user
*.userosscache
*.sln.docstates

# Build results
[Dd]ebug/
[Dd]ebugPublic/
[Rr]elease/
[Rr]eleases/
x64/
x86/
[Ww][Ii][Nn]32/
[Aa][Rr][Mm]/
[Aa][Rr][Mm]64/
bld/
[Bb]in/
[Oo]bj/
[Ll]og/
[Ll]ogs/

# .NET Core
project.lock.json
project.fragment.lock.json
artifacts/

# NuGet Packages
*.nupkg
*.snupkg

# Environment variables
.env
.env.local

# IDE
.vs/
.idea/
*.swp
*.swo
*~
";
            case "python":
                return @"# Byte-compiled / optimized / DLL files
__pycache__/
*.py[cod]
*$py.class

# Virtual Environment
venv/
env/
ENV/

# IDE
.vscode/
.idea/
*.swp
*.swo

# Environment variables
.env
.env.local

# Distribution / packaging
dist/
build/
*.egg-info/
";
            case "nodejs":
            case "node.js":
            case "node":
                return @"# Dependencies
node_modules/
npm-debug.log*
yarn-debug.log*
yarn-error.log*

# Environment variables
.env
.env.local

# IDE
.vscode/
.idea/
*.swp
*.swo

# Logs
logs
*.log

# OS
.DS_Store
Thumbs.db
";
            case "java":
                return @"# Compiled class file
*.class

# Log files
*.log

# BlueJ files
*.ctxt

# Package Files
*.jar
*.war
*.nar
*.ear
*.zip
*.tar.gz
*.rar

# Maven
target/
pom.xml.tag
pom.xml.releaseBackup
pom.xml.versionsBackup
pom.xml.next
release.properties

# IDE
.idea/
*.iml
.vscode/
*.swp
*.swo

# Environment variables
.env
.env.local
";
            default:
                return @"# Environment variables
.env
.env.local

# IDE
.vscode/
.idea/
*.swp
*.swo
";
        }
    }

    /// <summary>
    /// Creates a GitHub repository ruleset to restrict branch creation to admins only (blocks collaborators from creating branches)
    /// </summary>
    public async Task<bool> CreateRepositoryRulesetAsync(string owner, string repo, string repoType, string accessToken)
    {
        try
        {
            _logger.LogInformation("🔒 [GITHUB RULESET] Creating ruleset for {Owner}/{Repo} (Type: {RepoType})", owner, repo, repoType);

            var enableRulesets = _configuration.GetValue<bool>("GitHub:EnableRulesets", true);

            if (!enableRulesets)
            {
                _logger.LogInformation("🔒 [GITHUB RULESET] Rulesets are disabled in configuration, skipping");
                return true;
            }

            // Create ruleset payload to block branch creation for non-admins
            // The owner (PAT holder) is an implicit bypass actor, so they can still create branches
            var rulesetPayload = new
            {
                name = $"Restrict Branch Creation - {repoType}",
                target = "branch",
                enforcement = "active",
                conditions = new
                {
                    ref_name = new
                    {
                        include = new[] { "~ALL" },
                        exclude = new string[] { }
                    }
                },
                rules = new[]
                {
                    new
                    {
                        type = "creation"
                    }
                },
                bypass_actors = new object[] { }
            };

            var jsonContent = JsonSerializer.Serialize(rulesetPayload);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var endpoint = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/rulesets";
            
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            httpRequest.Content = content;

            _logger.LogInformation("🔒 [GITHUB RULESET] Sending request to {Endpoint}", endpoint);
            var response = await _httpClient.SendAsync(httpRequest);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("✅ [GITHUB RULESET] Successfully created ruleset for {Owner}/{Repo}: {Response}", 
                    owner, repo, responseContent);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("⚠️ [GITHUB RULESET] Failed to create ruleset for {Owner}/{Repo}. Status: {StatusCode}, Error: {Error}", 
                    owner, repo, response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [GITHUB RULESET] Error creating ruleset for {Owner}/{Repo}: {Message}", owner, repo, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Creates a new branch from a source branch using the GitHub API
    /// </summary>
    public async Task<CreateBranchResponse> CreateBranchAsync(string owner, string repo, string branchName, string sourceBranch, string accessToken)
    {
        try
        {
            _logger.LogInformation("🌿 [GITHUB BRANCH] Creating branch '{BranchName}' from '{SourceBranch}' in {Owner}/{Repo}", 
                branchName, sourceBranch, owner, repo);

            // First, get the SHA of the source branch
            var refEndpoint = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/git/refs/heads/{sourceBranch}";
            var refRequest = new HttpRequestMessage(HttpMethod.Get, refEndpoint);
            refRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var refResponse = await _httpClient.SendAsync(refRequest);
            if (!refResponse.IsSuccessStatusCode)
            {
                var errorContent = await refResponse.Content.ReadAsStringAsync();
                _logger.LogError("❌ [GITHUB BRANCH] Failed to get source branch '{SourceBranch}' SHA. Status: {StatusCode}, Error: {Error}", 
                    sourceBranch, refResponse.StatusCode, errorContent);
                return new CreateBranchResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to get source branch SHA: {errorContent}",
                    StatusCode = (int)refResponse.StatusCode
                };
            }

            var refContent = await refResponse.Content.ReadAsStringAsync();
            var refJson = JsonDocument.Parse(refContent);
            var sourceSha = refJson.RootElement.GetProperty("object").GetProperty("sha").GetString();

            if (string.IsNullOrEmpty(sourceSha))
            {
                _logger.LogError("❌ [GITHUB BRANCH] Source branch SHA is null or empty");
                return new CreateBranchResponse
                {
                    Success = false,
                    ErrorMessage = "Source branch SHA is null or empty",
                    StatusCode = 500
                };
            }

            // Create the new branch by creating a new reference
            var createRefEndpoint = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/git/refs";
            var createRefPayload = new
            {
                @ref = $"refs/heads/{branchName}",
                sha = sourceSha
            };

            var createRefJson = JsonSerializer.Serialize(createRefPayload);
            var createRefContent = new StringContent(createRefJson, System.Text.Encoding.UTF8, "application/json");
            var createRefRequest = new HttpRequestMessage(HttpMethod.Post, createRefEndpoint);
            createRefRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            createRefRequest.Content = createRefContent;

            _logger.LogInformation("🌿 [GITHUB BRANCH] Creating branch reference at {Endpoint}", createRefEndpoint);
            var createRefResponse = await _httpClient.SendAsync(createRefRequest);

            if (createRefResponse.IsSuccessStatusCode)
            {
                var responseContent = await createRefResponse.Content.ReadAsStringAsync();
                var branchUrl = $"https://github.com/{owner}/{repo}/tree/{branchName}";
                
                _logger.LogInformation("✅ [GITHUB BRANCH] Successfully created branch '{BranchName}' in {Owner}/{Repo}: {BranchUrl}", 
                    branchName, owner, repo, branchUrl);

                return new CreateBranchResponse
                {
                    Success = true,
                    BranchUrl = branchUrl,
                    BranchName = branchName,
                    GitHubResponse = responseContent,
                    StatusCode = (int)createRefResponse.StatusCode
                };
            }
            else
            {
                var errorContent = await createRefResponse.Content.ReadAsStringAsync();
                _logger.LogError("❌ [GITHUB BRANCH] Failed to create branch '{BranchName}'. Status: {StatusCode}, Error: {Error}", 
                    branchName, createRefResponse.StatusCode, errorContent);
                return new CreateBranchResponse
                {
                    Success = false,
                    ErrorMessage = errorContent,
                    StatusCode = (int)createRefResponse.StatusCode
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [GITHUB BRANCH] Error creating branch '{BranchName}' in {Owner}/{Repo}: {Message}", 
                branchName, owner, repo, ex.Message);
            return new CreateBranchResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Creates branch protection rules for the main branch to require PR reviews
    /// </summary>
    public async Task<bool> CreateBranchProtectionAsync(string owner, string repo, string branchName, string accessToken)
    {
        try
        {
            _logger.LogInformation("🛡️ [GITHUB BRANCH PROTECTION] Creating branch protection for {Owner}/{Repo}:{Branch}", owner, repo, branchName);

            var enableBranchProtection = _configuration.GetValue<bool>("GitHub:EnableBranchProtection", true);

            if (!enableBranchProtection)
            {
                _logger.LogInformation("🛡️ [GITHUB BRANCH PROTECTION] Branch protection is disabled in configuration, skipping");
                return true;
            }

            // Create branch protection payload
            var protectionPayload = new
            {
                required_status_checks = (object?)null,
                enforce_admins = false,
                required_pull_request_reviews = new
                {
                    required_approving_review_count = 1,
                    dismiss_stale_reviews = false,
                    require_code_owner_reviews = false,
                    require_last_push_approval = false
                },
                restrictions = (object?)null,
                allow_force_pushes = false,
                allow_deletions = false,
                block_creations = false,
                required_conversation_resolution = true,
                lock_branch = false,
                allow_fork_syncing = false
            };

            var jsonContent = JsonSerializer.Serialize(protectionPayload);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var endpoint = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/branches/{branchName}/protection";
            
            var httpRequest = new HttpRequestMessage(HttpMethod.Put, endpoint);
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Content = content;

            _logger.LogInformation("🛡️ [GITHUB BRANCH PROTECTION] Sending request to {Endpoint}", endpoint);
            var response = await _httpClient.SendAsync(httpRequest);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("✅ [GITHUB BRANCH PROTECTION] Successfully created branch protection for {Owner}/{Repo}:{Branch}: {Response}", 
                    owner, repo, branchName, responseContent);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("⚠️ [GITHUB BRANCH PROTECTION] Failed to create branch protection for {Owner}/{Repo}:{Branch}. Status: {StatusCode}, Error: {Error}", 
                    owner, repo, branchName, response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [GITHUB BRANCH PROTECTION] Error creating branch protection for {Owner}/{Repo}:{Branch}: {Message}", 
                owner, repo, branchName, ex.Message);
            return false;
        }
    }
}

/// <summary>
/// GitHub user model (simplified)
/// </summary>
public class GitHubUser
{
    public string Login { get; set; } = string.Empty;
    public long Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Bio { get; set; }
    public string? Company { get; set; }
    public string? Location { get; set; }
    public string? Blog { get; set; }
    public string HtmlUrl { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public int PublicRepos { get; set; }
    public int Followers { get; set; }
    public int Following { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// GitHub user info model for OAuth authentication
/// </summary>
public class GitHubUserInfo
{
    public string Login { get; set; } = string.Empty;
    public long Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Bio { get; set; }
    public string? Company { get; set; }
    public string? Location { get; set; }
    public string? Blog { get; set; }
    public string HtmlUrl { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public int PublicRepos { get; set; }
    public int Followers { get; set; }
    public int Following { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// GitHub commit information
/// </summary>
public class GitHubCommitInfo
{
    public DateTime CommitDate { get; set; }
    public string CommitMessage { get; set; } = string.Empty;
}

// Models for Code Review Agent
public class GitHubCommit
{
    public string Sha { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CommitDate { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class GitHubFileChange
{
    public string FilePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "added", "modified", "removed", "renamed"
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public string? Patch { get; set; } // Unified diff format
    public string? Content { get; set; } // Full file content (if needed)
    public int? Changes { get; set; } // Total changes (additions + deletions)
}

public class GitHubCommitDiff
{
    public string CommitSha { get; set; } = string.Empty;
    public string CommitMessage { get; set; } = string.Empty;
    public DateTime CommitDate { get; set; }
    public string Author { get; set; } = string.Empty;
    public List<GitHubFileChange> FileChanges { get; set; } = new();
    public int TotalAdditions { get; set; }
    public int TotalDeletions { get; set; }
    public int TotalFilesChanged { get; set; }
}

/// <summary>
/// Request model for creating a GitHub repository
/// </summary>
public class CreateRepositoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPrivate { get; set; } = true;
    public List<string> Collaborators { get; set; } = new List<string>();
    public string? ProjectTitle { get; set; }  // Display name for the project
    public string? DatabaseConnectionString { get; set; }  // Database connection string for README
    public string? WebApiUrl { get; set; }  // Railway WebApi URL for README
    public string? SwaggerUrl { get; set; }  // Swagger API tester URL for README
    public string? ProgrammingLanguage { get; set; }  // Programming language for backend code generation (e.g., "C#", "Python", "NodeJS", "Java")
}

/// <summary>
/// Response model for repository creation
/// </summary>
public class CreateRepositoryResponse
{
    public string RepositoryUrl { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> AddedCollaborators { get; set; } = new List<string>();
    public List<string> FailedCollaborators { get; set; } = new List<string>();
    public string? GitHubPagesUrl { get; set; }
    public bool GitHubPagesEnabled { get; set; }
    public bool InitialCommitCreated { get; set; }
}

/// <summary>
/// GitHub repository model
/// </summary>
public class GitHubRepository
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;
    
    [JsonPropertyName("clone_url")]
    public string CloneUrl { get; set; } = string.Empty;
    
    [JsonPropertyName("ssh_url")]
    public string SshUrl { get; set; } = string.Empty;
    
    [JsonPropertyName("private")]
    public bool IsPrivate { get; set; }
    
    [JsonPropertyName("default_branch")]
    public string DefaultBranch { get; set; } = string.Empty;
    
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Helper class for branch information
/// </summary>
public class DefaultBranchInfo
{
    public string BranchName { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public string TreeSha { get; set; } = string.Empty;
}

/// <summary>
/// Response model for branch creation
/// </summary>
public class CreateBranchResponse
{
    public bool Success { get; set; }
    public string? BranchUrl { get; set; }
    public string? BranchName { get; set; }
    public string? GitHubResponse { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }
}


