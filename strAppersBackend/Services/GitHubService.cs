using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace strAppersBackend.Services;

public interface IGitHubService
{
    Task<bool> ValidateGitHubUserAsync(string username);
    Task<GitHubUser?> GetGitHubUserAsync(string username);
    Task<string?> ExchangeCodeForTokenAsync(string code);
    Task<GitHubUserInfo?> GetGitHubUserInfoAsync(string accessToken);
    Task<CreateRepositoryResponse> CreateRepositoryAsync(CreateRepositoryRequest request);
    Task<bool> AddCollaboratorAsync(string repositoryName, string collaboratorUsername, string accessToken);
    Task<bool> CreateInitialCommitAsync(string owner, string repositoryName, string projectTitle, string accessToken);
    Task<bool> EnableGitHubPagesAsync(string owner, string repositoryName, string accessToken);
    string GetGitHubPagesUrl(string repositoryName);
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

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{GitHubApiBaseUrl}/user/repos");
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Content = content;

            var createResponse = await _httpClient.SendAsync(httpRequest);

            if (!createResponse.IsSuccessStatusCode)
            {
                var errorContent = await createResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create GitHub repository. Status: {StatusCode}, Error: {Error}", 
                    createResponse.StatusCode, errorContent);
                
                // Provide more specific error messages
                if (createResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    response.ErrorMessage = "Access denied. Please check that the GitHub access token has 'repo' permissions and is valid.";
                }
                else if (createResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    response.ErrorMessage = "Unauthorized. Please check that the GitHub access token is valid and not expired.";
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

            _logger.LogInformation("Parsed repository - HtmlUrl: {HtmlUrl}, Name: {Name}", repository.HtmlUrl, repository.Name);
            response.RepositoryUrl = repository.HtmlUrl;
            response.Success = true;

            _logger.LogInformation("Successfully created GitHub repository: {RepositoryUrl}", repository.HtmlUrl);

            // Get current user info for subsequent operations
            var userInfo = await GetGitHubUserInfoAsync(accessToken);
            if (userInfo == null)
            {
                _logger.LogError("Failed to get current user info");
                response.ErrorMessage = "Failed to get user information for repository operations";
                return response;
            }

            // Set the GitHub Pages URL (based on CNAME file)
            response.GitHubPagesUrl = GetGitHubPagesUrl(request.Name);
            _logger.LogInformation("GitHub Pages URL will be: {PagesUrl}", response.GitHubPagesUrl);

            // Create initial commit with index.html
            _logger.LogInformation("Creating initial commit for repository");
            var projectTitle = request.ProjectTitle ?? request.Name;  // Use project title if provided, otherwise use repo name
            response.InitialCommitCreated = await CreateInitialCommitAsync(userInfo.Login, request.Name, projectTitle, accessToken);
            
            if (!response.InitialCommitCreated)
            {
                _logger.LogWarning("Failed to create initial commit, but repository was created successfully");
            }

            // Enable GitHub Pages
            _logger.LogInformation("Enabling GitHub Pages for repository");
            response.GitHubPagesEnabled = await EnableGitHubPagesAsync(userInfo.Login, request.Name, accessToken);
            
            if (response.GitHubPagesEnabled)
            {
                _logger.LogInformation("GitHub Pages enabled successfully at: {PagesUrl}", response.GitHubPagesUrl);
            }
            else
            {
                _logger.LogWarning("Failed to enable GitHub Pages (check token permissions), but CNAME is configured for: {PagesUrl}", response.GitHubPagesUrl);
            }

            // Add collaborators if any
            if (request.Collaborators.Any())
            {
                _logger.LogInformation("Adding {Count} collaborators to repository", request.Collaborators.Count);
                
                foreach (var collaborator in request.Collaborators)
                {
                    try
                    {
                        var collaboratorAdded = await AddCollaboratorAsync(request.Name, collaborator, accessToken);
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
    public async Task<bool> AddCollaboratorAsync(string repositoryName, string collaboratorUsername, string accessToken)
    {
        try
        {
            _logger.LogInformation("Adding collaborator {Collaborator} to repository {Repository}", 
                collaboratorUsername, repositoryName);

            // Get current user to determine the repository owner
            var userInfo = await GetGitHubUserInfoAsync(accessToken);
            if (userInfo == null)
            {
                _logger.LogError("Failed to get current user info for adding collaborator");
                return false;
            }

            var collaboratorPayload = new
            {
                permission = "push" // Give push access to collaborators
            };

            var jsonContent = JsonSerializer.Serialize(collaboratorPayload);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Put, 
                $"{GitHubApiBaseUrl}/repos/{userInfo.Login}/{repositoryName}/collaborators/{collaboratorUsername}");
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
    public async Task<bool> CreateInitialCommitAsync(string owner, string repositoryName, string projectTitle, string accessToken)
    {
        try
        {
            _logger.LogInformation("Creating initial commit for repository {Owner}/{Repository}", owner, repositoryName);

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

            // Create blob for index.html only (no CNAME file)
            var indexHtmlSha = await CreateBlobAsync(owner, repositoryName, indexHtmlContent, accessToken);

            if (string.IsNullOrEmpty(indexHtmlSha))
            {
                _logger.LogError("Failed to create blob for index.html");
                return false;
            }

            // Create a tree with only index.html
            var tree = await CreateTreeAsync(owner, repositoryName, branchInfo.TreeSha, 
                new[] 
                { 
                    new { path = "index.html", mode = "100644", type = "blob", sha = indexHtmlSha }
                }, 
                accessToken);

            if (string.IsNullOrEmpty(tree))
            {
                _logger.LogError("Failed to create tree");
                return false;
            }

            // Create a commit
            var commit = await CreateCommitAsync(owner, repositoryName, tree, branchInfo.CommitSha, 
                "Initial commit: Add project landing page", accessToken);

            if (string.IsNullOrEmpty(commit))
            {
                _logger.LogError("Failed to create commit");
                return false;
            }

            // Update the branch reference
            var updated = await UpdateReferenceAsync(owner, repositoryName, branchInfo.BranchName, commit, accessToken);

            if (updated)
            {
                _logger.LogInformation("Successfully created initial commit for {Owner}/{Repository}", owner, repositoryName);
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
    /// Enables GitHub Pages for a repository
    /// </summary>
    public async Task<bool> EnableGitHubPagesAsync(string owner, string repositoryName, string accessToken)
    {
        try
        {
            _logger.LogInformation("Enabling GitHub Pages for repository {Owner}/{Repository}", owner, repositoryName);

            var pagesPayload = new
            {
                source = new
                {
                    branch = "main",
                    path = "/"
                }
            };

            var jsonContent = JsonSerializer.Serialize(pagesPayload);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/pages");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // 201 Created means success, 409 Conflict means pages already enabled
                _logger.LogInformation("GitHub Pages enabled successfully for {Owner}/{Repository}", owner, repositoryName);
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
    /// Gets the GitHub Pages URL for a repository
    /// </summary>
    public string GetGitHubPagesUrl(string repositoryName)
    {
        return $"https://skill-in-projects.github.io/{repositoryName}/";
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
            var treePayload = new
            {
                base_tree = baseTreeSha,
                tree = tree
            };

            var jsonContent = JsonSerializer.Serialize(treePayload);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{GitHubApiBaseUrl}/repos/{owner}/{repositoryName}/git/trees");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = httpContent;

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var treeData = JsonSerializer.Deserialize<JsonElement>(responseContent);
            return treeData.GetProperty("sha").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tree");
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
        
        <div class=""footer"">
            <p>
                This page will be automatically replaced when you push your project files.<br>
                <strong>Happy coding! 🎉</strong>
            </p>
        </div>
    </div>
</body>
</html>";
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
/// Request model for creating a GitHub repository
/// </summary>
public class CreateRepositoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPrivate { get; set; } = true;
    public List<string> Collaborators { get; set; } = new List<string>();
    public string? ProjectTitle { get; set; }  // Display name for the project
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

