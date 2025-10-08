using Microsoft.AspNetCore.Mvc;
using strAppersBackend.Services;
using System.Text.Json;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("auth/github")]
public class GitHubController : ControllerBase
{
    private readonly ILogger<GitHubController> _logger;
    private readonly IGitHubService _githubService;
    private readonly IConfiguration _configuration;

    public GitHubController(
        ILogger<GitHubController> logger,
        IGitHubService githubService,
        IConfiguration configuration)
    {
        _logger = logger;
        _githubService = githubService;
        _configuration = configuration;
    }

    /// <summary>
    /// Redirect the user to GitHub's OAuth authorization page
    /// </summary>
    [HttpGet("login")]
    public IActionResult Login()
    {
        try
        {
            var clientId = _configuration["GitHub:ClientId"];
            var redirectUri = _configuration["GitHub:RedirectUri"];

            if (string.IsNullOrEmpty(clientId))
            {
                _logger.LogError("GitHub ClientId is not configured");
                return BadRequest("GitHub OAuth is not configured");
            }

            if (string.IsNullOrEmpty(redirectUri))
            {
                _logger.LogError("GitHub RedirectUri is not configured");
                return BadRequest("GitHub OAuth redirect URI is not configured");
            }

            var githubAuthUrl = $"https://github.com/login/oauth/authorize" +
                               $"?client_id={clientId}" +
                               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                               $"&scope=read:user";

            _logger.LogInformation("Redirecting to GitHub OAuth: {Url}", githubAuthUrl);
            return Redirect(githubAuthUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating GitHub OAuth login");
            return StatusCode(500, "An error occurred during GitHub OAuth login");
        }
    }

    /// <summary>
    /// Handle GitHub OAuth callback and exchange code for access token
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string? state = null)
    {
        try
        {
            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("GitHub OAuth callback received without code parameter");
                return BadRequest("Authorization code is missing");
            }

            _logger.LogInformation("GitHub OAuth callback received with code");

            // Exchange code for access token
            var accessToken = await _githubService.ExchangeCodeForTokenAsync(code);
            
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("Failed to exchange GitHub OAuth code for access token");
                return BadRequest("Failed to authenticate with GitHub");
            }

            // Store the access token in session or temporary storage
            HttpContext.Session.SetString("GitHubAccessToken", accessToken);

            // Redirect to get user info
            return Redirect("/auth/github/user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling GitHub OAuth callback");
            return StatusCode(500, "An error occurred during GitHub OAuth callback");
        }
    }

    /// <summary>
    /// Fetch GitHub user information using the access token
    /// </summary>
    [HttpGet("user")]
    public async Task<IActionResult> GetUser()
    {
        try
        {
            // Retrieve access token from session
            var accessToken = HttpContext.Session.GetString("GitHubAccessToken");
            
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("No GitHub access token found in session");
                return Unauthorized("No valid GitHub session found");
            }

            // Get user info from GitHub
            var userInfo = await _githubService.GetGitHubUserInfoAsync(accessToken);
            
            if (userInfo == null)
            {
                _logger.LogError("Failed to retrieve GitHub user information");
                return BadRequest("Failed to retrieve GitHub user information");
            }

            _logger.LogInformation("Retrieved GitHub user info for: {Username}", userInfo.Login);

            // Store username in session for the complete endpoint
            HttpContext.Session.SetString("GitHubUsername", userInfo.Login);

            // Redirect to complete endpoint
            return Redirect("/auth/github/complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving GitHub user information");
            return StatusCode(500, "An error occurred while retrieving GitHub user information");
        }
    }

    /// <summary>
    /// Return GitHub username to frontend
    /// </summary>
    [HttpGet("complete")]
    public IActionResult Complete()
    {
        try
        {
            // Retrieve username from session
            var username = HttpContext.Session.GetString("GitHubUsername");
            
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("No GitHub username found in session");
                return Unauthorized("No valid GitHub session found");
            }

            _logger.LogInformation("GitHub OAuth flow completed for user: {Username}", username);

            // Clear session data
            HttpContext.Session.Remove("GitHubAccessToken");
            HttpContext.Session.Remove("GitHubUsername");

            // Option 1: Redirect to register page with username
            var redirectUrl = $"/register?username={Uri.EscapeDataString(username)}";
            
            // Option 2: Return JSON response (uncomment if you prefer JSON over redirect)
            // return Ok(new { username = username });

            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing GitHub OAuth flow");
            return StatusCode(500, "An error occurred while completing GitHub authentication");
        }
    }

    /// <summary>
    /// Get current GitHub user info as JSON (alternative to redirect flow)
    /// </summary>
    [HttpGet("info")]
    public async Task<IActionResult> GetUserInfo()
    {
        try
        {
            // Retrieve access token from session
            var accessToken = HttpContext.Session.GetString("GitHubAccessToken");
            
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized("No valid GitHub session found");
            }

            // Get user info from GitHub
            var userInfo = await _githubService.GetGitHubUserInfoAsync(accessToken);
            
            if (userInfo == null)
            {
                return BadRequest("Failed to retrieve GitHub user information");
            }

            return Ok(new
            {
                username = userInfo.Login,
                email = userInfo.Email,
                name = userInfo.Name,
                avatarUrl = userInfo.AvatarUrl,
                htmlUrl = userInfo.HtmlUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving GitHub user information");
            return StatusCode(500, "An error occurred while retrieving GitHub user information");
        }
    }

    /// <summary>
    /// Test GitHub token validity
    /// </summary>
    [HttpGet("test-token")]
    public async Task<IActionResult> TestToken()
    {
        try
        {
            var accessToken = _configuration["GitHub:AccessToken"];
            if (string.IsNullOrEmpty(accessToken))
            {
                return BadRequest("GitHub access token is not configured");
            }

            var userInfo = await _githubService.GetGitHubUserInfoAsync(accessToken);
            if (userInfo == null)
            {
                return BadRequest("Invalid or expired GitHub access token");
            }

            return Ok(new
            {
                success = true,
                message = "GitHub token is valid",
                username = userInfo.Login,
                tokenPrefix = accessToken.Length > 10 ? accessToken.Substring(0, 10) + "..." : accessToken
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing GitHub token");
            return StatusCode(500, "Error testing GitHub token");
        }
    }

    /// <summary>
    /// Create a new GitHub repository and add collaborators
    /// </summary>
    [HttpPost("create-repo")]
    public async Task<IActionResult> CreateRepository([FromBody] CreateRepositoryRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("CreateRepository called with null request");
                return BadRequest("Request body is required");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                _logger.LogWarning("CreateRepository called with empty repository name");
                return BadRequest("Repository name is required");
            }

            _logger.LogInformation("Creating GitHub repository: {RepositoryName} with {CollaboratorCount} collaborators", 
                request.Name, request.Collaborators?.Count ?? 0);

            var result = await _githubService.CreateRepositoryAsync(request);

            if (result.Success)
            {
                _logger.LogInformation("Successfully created repository: {RepositoryUrl}", result.RepositoryUrl);
                return Ok(result);
            }
            else
            {
                _logger.LogError("Failed to create repository: {ErrorMessage}", result.ErrorMessage);
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating GitHub repository: {Message}", ex.Message);
            return StatusCode(500, new CreateRepositoryResponse
            {
                Success = false,
                ErrorMessage = "An error occurred while creating the repository"
            });
        }
    }
}

