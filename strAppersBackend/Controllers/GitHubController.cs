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
    public IActionResult Login([FromQuery] string? returnUrl = null)
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

            // Add state parameter if returnUrl provided
            if (!string.IsNullOrEmpty(returnUrl))
            {
                githubAuthUrl += $"&state={Uri.EscapeDataString(returnUrl)}";
            }

            _logger.LogInformation("Redirecting to GitHub OAuth: {Url}, returnUrl: {ReturnUrl}", githubAuthUrl, returnUrl ?? "default");
            return Redirect(githubAuthUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating GitHub OAuth login");
            return StatusCode(500, "An error occurred during GitHub OAuth login");
        }
    }

    /// <summary>
    /// Get GitHub OAuth URL without redirecting (returns JSON for API clients)
    /// </summary>
    [HttpGet("login-url")]
    public IActionResult GetLoginUrl([FromQuery] string? returnUrl = null)
    {
        try
        {
            var clientId = _configuration["GitHub:ClientId"];
            var redirectUri = _configuration["GitHub:RedirectUri"];

            if (string.IsNullOrEmpty(clientId))
            {
                _logger.LogError("GitHub ClientId is not configured");
                return BadRequest(new { error = "GitHub OAuth is not configured" });
            }

            if (string.IsNullOrEmpty(redirectUri))
            {
                _logger.LogError("GitHub RedirectUri is not configured");
                return BadRequest(new { error = "GitHub OAuth redirect URI is not configured" });
            }

            // Build GitHub OAuth URL with state parameter (return URL)
            var githubAuthUrl = $"https://github.com/login/oauth/authorize" +
                               $"?client_id={clientId}" +
                               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                               $"&scope=read:user";

            // Add state parameter if returnUrl provided
            if (!string.IsNullOrEmpty(returnUrl))
            {
                githubAuthUrl += $"&state={Uri.EscapeDataString(returnUrl)}";
            }

            _logger.LogInformation("Returning GitHub OAuth URL: {Url}, returnUrl: {ReturnUrl}", githubAuthUrl, returnUrl ?? "default");
            return Ok(new { 
                authUrl = githubAuthUrl,
                redirectUri = redirectUri,
                clientId = clientId,
                returnUrl = returnUrl ?? "/StudentRegistration"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting GitHub OAuth URL");
            return StatusCode(500, new { error = "An error occurred while getting GitHub OAuth URL" });
        }
    }

    /// <summary>
    /// Handle GitHub OAuth callback and exchange code for access token
    /// Redirects back to frontend with username in URL
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string? state = null)
    {
        _logger.LogInformation("=== GitHub OAuth Callback START ===");
        _logger.LogInformation("Code: {Code} (length: {Length})", code?.Substring(0, Math.Min(10, code?.Length ?? 0)) + "...", code?.Length ?? 0);
        _logger.LogInformation("State: {State}", state);
        
        try
        {
            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("GitHub OAuth callback received without code parameter");
                return BuildErrorRedirectResponse(state, "Authorization code is missing");
            }

            _logger.LogInformation("Step 1: Exchanging code for access token...");
            // Exchange code for access token
            var accessToken = await _githubService.ExchangeCodeForTokenAsync(code);
            
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("Step 1 FAILED: Failed to exchange GitHub OAuth code for access token");
                return BuildErrorRedirectResponse(state, "Failed to authenticate with GitHub");
            }
            
            _logger.LogInformation("Step 1 SUCCESS: Access token received (length: {Length})", accessToken.Length);

            _logger.LogInformation("Step 2: Getting GitHub user info...");
            // Get user info immediately
            var userInfo = await _githubService.GetGitHubUserInfoAsync(accessToken);
            
            if (userInfo == null)
            {
                _logger.LogError("Step 2 FAILED: Failed to retrieve GitHub user information");
                return BuildErrorRedirectResponse(state, "Failed to retrieve GitHub user information");
            }

            _logger.LogInformation("Step 2 SUCCESS: User info retrieved - Username: {Username}", userInfo.Login);

            // Determine return URL from state parameter or use default
            var returnUrl = !string.IsNullOrEmpty(state) 
                ? Uri.UnescapeDataString(state) 
                : "/StudentRegistration"; // Default frontend page

            // Get frontend base URL from configuration (for building absolute URLs from relative paths)
            var frontendBaseUrl = _configuration["GitHub:FrontendBaseUrl"] ?? "https://skill-in.com";
            
            // Build redirect URL with username - use absolute URL if returnUrl is relative
            string redirectUrl;
            if (returnUrl.StartsWith("http://") || returnUrl.StartsWith("https://"))
            {
                // Already absolute URL - use it directly
                var separator = returnUrl.Contains('?') ? '&' : '?';
                redirectUrl = $"{returnUrl}{separator}githubUsername={Uri.EscapeDataString(userInfo.Login)}";
            }
            else
            {
                // Relative URL - build absolute URL using configured frontend base URL
                // (not the backend host, since we're redirecting to the frontend)
                var separator = returnUrl.Contains('?') ? '&' : '?';
                redirectUrl = $"{frontendBaseUrl.TrimEnd('/')}/{returnUrl.TrimStart('/')}{separator}githubUsername={Uri.EscapeDataString(userInfo.Login)}";
            }

            _logger.LogInformation("Step 3: Building redirect URL...");
            _logger.LogInformation("Return URL from state: {ReturnUrl}", returnUrl);
            _logger.LogInformation("Final redirect URL: {RedirectUrl}", redirectUrl);
            _logger.LogInformation("Step 4: Performing redirect...");
            
            // Primary: Use HTTP redirect (302 Found)
            // Fallback: HTML page with JavaScript redirect
            try
            {
                _logger.LogInformation("Attempting HTTP 302 redirect to: {RedirectUrl}", redirectUrl);
                return Redirect(redirectUrl);
            }
            catch (Exception redirectEx)
            {
                _logger.LogWarning(redirectEx, "HTTP redirect failed, using HTML fallback: {Message}", redirectEx.Message);
                _logger.LogInformation("Using HTML fallback redirect to: {RedirectUrl}", redirectUrl);
                
                // Fallback: HTML page with JavaScript redirect
                var htmlContent = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <meta http-equiv=""refresh"" content=""0;url={redirectUrl}"">
    <title>GitHub Authentication Success</title>
    <script>
        // Immediate redirect
        window.location.href = '{redirectUrl.Replace("'", "\\'")}';
    </script>
</head>
<body>
    <p>GitHub authentication successful! Redirecting...</p>
    <p>If you are not redirected automatically, <a href=""{redirectUrl}"">click here</a>.</p>
    <script>
        setTimeout(function() {{
            window.location.href = '{redirectUrl.Replace("'", "\\'")}';
        }}, 100);
    </script>
</body>
</html>";
            
                return Content(htmlContent, "text/html");
            }
            
            _logger.LogInformation("=== GitHub OAuth Callback END (redirect sent) ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== GitHub OAuth Callback ERROR ===");
            _logger.LogError(ex, "Error handling GitHub OAuth callback: {Message}", ex.Message);
            _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
            return BuildErrorRedirectResponse(state, $"An error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper method to build redirect URL with error parameter and return HTML redirect page
    /// </summary>
    private IActionResult BuildErrorRedirectResponse(string? state, string errorMessage)
    {
        var returnUrl = !string.IsNullOrEmpty(state) 
            ? Uri.UnescapeDataString(state) 
            : "/StudentRegistration";
        
        // Get frontend base URL from configuration (for building absolute URLs from relative paths)
        var frontendBaseUrl = _configuration["GitHub:FrontendBaseUrl"] ?? "https://skill-in.com";
        
        string errorRedirectUrl;
        if (returnUrl.StartsWith("http://") || returnUrl.StartsWith("https://"))
        {
            var separator = returnUrl.Contains('?') ? '&' : '?';
            errorRedirectUrl = $"{returnUrl}{separator}githubError={Uri.EscapeDataString(errorMessage)}";
        }
        else
        {
            // Relative URL - use configured frontend base URL (not backend host)
            var separator = returnUrl.Contains('?') ? '&' : '?';
            errorRedirectUrl = $"{frontendBaseUrl.TrimEnd('/')}/{returnUrl.TrimStart('/')}{separator}githubError={Uri.EscapeDataString(errorMessage)}";
        }
        
        var htmlContent = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <meta http-equiv=""refresh"" content=""2;url={errorRedirectUrl}"">
    <title>GitHub Authentication Error</title>
    <script>
        setTimeout(function() {{
            window.location.href = '{errorRedirectUrl.Replace("'", "\\'")}';
        }}, 2000);
    </script>
</head>
<body>
    <p>Error: {errorMessage}</p>
    <p>Redirecting back to registration page...</p>
    <p>If you are not redirected automatically, <a href=""{errorRedirectUrl}"">click here</a>.</p>
</body>
</html>";
        
        return Content(htmlContent, "text/html");
    }

    /// <summary>
    /// Helper method to build redirect URL with error parameter
    /// </summary>
    private string BuildErrorRedirectUrl(string? state, string errorMessage)
    {
        var returnUrl = !string.IsNullOrEmpty(state) 
            ? Uri.UnescapeDataString(state) 
            : "/StudentRegistration";
        
        var request = HttpContext.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}";
        
        if (returnUrl.StartsWith("http://") || returnUrl.StartsWith("https://"))
        {
            var separator = returnUrl.Contains('?') ? '&' : '?';
            return $"{returnUrl}{separator}githubError={Uri.EscapeDataString(errorMessage)}";
        }
        else
        {
            var separator = returnUrl.Contains('?') ? '&' : '?';
            return $"{baseUrl}{returnUrl}{separator}githubError={Uri.EscapeDataString(errorMessage)}";
        }
    }

    /// <summary>
    /// Handle GitHub OAuth callback and return user info as JSON (for frontend SPAs)
    /// </summary>
    [HttpGet("callback-json")]
    public async Task<IActionResult> CallbackJson([FromQuery] string code, [FromQuery] string? state = null)
    {
        try
        {
            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("GitHub OAuth callback received without code parameter");
                return BadRequest(new { error = "Authorization code is missing" });
            }

            _logger.LogInformation("GitHub OAuth callback received with code (JSON mode)");

            // Exchange code for access token
            var accessToken = await _githubService.ExchangeCodeForTokenAsync(code);
            
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("Failed to exchange GitHub OAuth code for access token");
                return BadRequest(new { error = "Failed to authenticate with GitHub" });
            }

            // Get user info directly
            var userInfo = await _githubService.GetGitHubUserInfoAsync(accessToken);
            
            if (userInfo == null)
            {
                _logger.LogError("Failed to retrieve GitHub user information");
                return BadRequest(new { error = "Failed to retrieve GitHub user information" });
            }

            _logger.LogInformation("Successfully retrieved GitHub user info for: {Username}", userInfo.Login);

            return Ok(new
            {
                success = true,
                username = userInfo.Login,
                email = userInfo.Email,
                name = userInfo.Name,
                avatarUrl = userInfo.AvatarUrl,
                htmlUrl = userInfo.HtmlUrl,
                bio = userInfo.Bio,
                company = userInfo.Company,
                location = userInfo.Location
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling GitHub OAuth callback (JSON mode)");
            return StatusCode(500, new { error = "An error occurred during GitHub OAuth callback" });
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
    /// Test endpoint to verify callback is being called
    /// </summary>
    [HttpGet("callback-test")]
    public IActionResult CallbackTest([FromQuery] string? code = null, [FromQuery] string? state = null)
    {
        return Ok(new
        {
            message = "Callback endpoint reached",
            hasCode = !string.IsNullOrEmpty(code),
            code = code?.Substring(0, Math.Min(10, code?.Length ?? 0)) + "...",
            state = state,
            timestamp = DateTime.UtcNow
        });
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

