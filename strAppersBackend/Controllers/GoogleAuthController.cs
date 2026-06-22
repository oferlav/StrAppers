using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;

namespace strAppersBackend.Controllers;

/// <summary>
/// Browser Google OAuth for app login (separate from Google Workspace API credentials used for Meet/Gmail).
/// Redirect URI must be registered in Google Cloud Console: {backend}/auth/google/callback
/// </summary>
[ApiController]
[Route("auth/google")]
public class GoogleAuthController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleAuthController> _logger;
    private readonly ApplicationDbContext _db;

    public GoogleAuthController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GoogleAuthController> logger,
        ApplicationDbContext db)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _db = db;
    }

    private string? GetClientId() =>
        _configuration["GoogleAuth:ClientId"]
        ?? _configuration["GoogleWorkspace:OAuth:ClientId"]
        ?? _configuration["GoogleWorkspace:ClientId"];

    private string? GetClientSecret() =>
        _configuration["GoogleAuth:ClientSecret"]
        ?? _configuration["GoogleWorkspace:OAuth:ClientSecret"]
        ?? _configuration["GoogleWorkspace:ClientSecret"];

    private string? GetRedirectUri() =>
        _configuration["GoogleAuth:RedirectUri"];

    private string GetFrontendBaseUrl() =>
        (_configuration["GoogleAuth:FrontendBaseUrl"]
         ?? _configuration["GitHub:FrontendBaseUrl"]
         ?? "https://skill-in.com").TrimEnd('/');

    /// <summary>
    /// JSON for SPA: Google authorization URL (avoids exposing client id in frontend env if undesired).
    /// </summary>
    [HttpGet("login-url")]
    public IActionResult GetLoginUrl([FromQuery] string? returnUrl = null)
    {
        var clientId = GetClientId();
        var redirectUri = GetRedirectUri();

        if (string.IsNullOrEmpty(clientId))
        {
            _logger.LogError("Google OAuth ClientId is not configured (GoogleAuth:ClientId or GoogleWorkspace)");
            return BadRequest(new { error = "Google OAuth is not configured" });
        }

        if (string.IsNullOrEmpty(redirectUri))
        {
            _logger.LogError("GoogleAuth:RedirectUri is not configured");
            return BadRequest(new { error = "Google OAuth redirect URI is not configured on the server" });
        }

        var state = string.IsNullOrEmpty(returnUrl)
            ? $"{GetFrontendBaseUrl()}/GoogleCallback"
            : returnUrl;

        var scope = Uri.EscapeDataString("openid email profile");
        var googleAuthUrl =
            "https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            "&response_type=code" +
            $"&scope={scope}" +
            "&access_type=offline" +
            "&prompt=select_account" +
            $"&state={Uri.EscapeDataString(state)}";

        _logger.LogInformation("Returning Google OAuth URL (state return path length: {Len})", state.Length);

        return Ok(new
        {
            authUrl = googleAuthUrl,
            redirectUri,
            returnUrl = state
        });
    }

    /// <summary>
    /// OAuth redirect target registered in Google Cloud Console.
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state,
        [FromQuery] string? error = null)
    {
        var frontendBase = GetFrontendBaseUrl();

        // Resolve the effective frontend base from the state URL (= the returnUrl the SPA sent).
        // This lets staging / branch-preview deployments receive the callback on their own origin
        // instead of always being redirected to the configured production FrontendBaseUrl.
        // The origin is validated against CorsOriginHelper before use.
        var effectiveFrontendBase = frontendBase;
        if (!string.IsNullOrEmpty(state))
        {
            try
            {
                var stateUri = new Uri(state, UriKind.Absolute);
                var port = stateUri.IsDefaultPort ? "" : $":{stateUri.Port}";
                var stateOrigin = $"{stateUri.Scheme}://{stateUri.Host}{port}";
                if (CorsOriginHelper.IsOriginAllowed(stateOrigin, null, Array.Empty<string>()))
                {
                    effectiveFrontendBase = stateOrigin;
                    _logger.LogInformation("Google OAuth callback: using origin from state ({Origin})", stateOrigin);
                }
            }
            catch { /* leave effectiveFrontendBase as configured frontendBase */ }
        }

        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("Google OAuth error query: {Error}", error);
            return Redirect(BuildGoogleCallbackUrl(effectiveFrontendBase,
                ("status", "error"),
                ("message", error)));
        }

        if (string.IsNullOrEmpty(code))
        {
            _logger.LogWarning("Google OAuth callback missing code");
            return Redirect(BuildGoogleCallbackUrl(effectiveFrontendBase,
                ("status", "error"),
                ("message", "missing_code")));
        }

        var clientId = GetClientId();
        var clientSecret = GetClientSecret();
        var redirectUri = GetRedirectUri();

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(redirectUri))
        {
            _logger.LogError("Google OAuth not fully configured for token exchange");
            return Redirect(BuildGoogleCallbackUrl(effectiveFrontendBase,
                ("status", "error"),
                ("message", "server_misconfigured")));
        }

        string email;
        string? name;
        try
        {
            var tokenJson = await ExchangeCodeForTokenAsync(code, clientId, clientSecret, redirectUri);
            var accessToken = tokenJson.GetProperty("access_token").GetString();
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("Google token response missing access_token");
                return Redirect(BuildGoogleCallbackUrl(effectiveFrontendBase,
                    ("status", "error"),
                    ("message", "token_exchange_failed")));
            }

            var userInfo = await GetUserInfoAsync(accessToken);
            email = userInfo.email ?? "";
            name = userInfo.name;

            if (string.IsNullOrWhiteSpace(email))
            {
                return Redirect(BuildGoogleCallbackUrl(effectiveFrontendBase,
                    ("status", "error"),
                    ("message", "no_email")));
            }

            if (userInfo.verifiedEmail == false)
            {
                return Redirect(BuildGoogleCallbackUrl(effectiveFrontendBase,
                    ("status", "error"),
                    ("message", "email_not_verified")));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google OAuth callback failed");
            return Redirect(BuildGoogleCallbackUrl(effectiveFrontendBase,
                ("status", "error"),
                ("message", "oauth_failed")));
        }

        var normalizedEmail = email.Trim();

        // Parse userTypeHint from state (e.g. state = "https://host/GoogleCallback?userTypeHint=student")
        var userTypeHint = "";
        if (!string.IsNullOrEmpty(state))
        {
            try
            {
                var stateUri = new Uri(state, UriKind.Absolute);
                var stateQuery = System.Web.HttpUtility.ParseQueryString(stateUri.Query);
                userTypeHint = stateQuery["userTypeHint"] ?? "";
            }
            catch { /* state is not a valid URI, ignore */ }
        }

        var employer = await _db.Employers
            .AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.ContactEmail != null &&
                e.ContactEmail.ToLower() == normalizedEmail.ToLower());

        if (employer != null)
        {
            return Redirect(BuildGoogleCallbackUrl(effectiveFrontendBase,
                ("status", "ok"),
                ("userType", "employer"),
                ("email", normalizedEmail)));
        }

        // Match authPrecheckStrAppers.invokeCheckUserByEmail order: employer → organization → student
        var organization = await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o =>
                o.ContactEmail != null &&
                o.ContactEmail.ToLower() == normalizedEmail.ToLower());

        if (organization != null)
        {
            return Redirect(BuildGoogleCallbackUrl(effectiveFrontendBase,
                ("status", "ok"),
                ("userType", "organization"),
                ("email", normalizedEmail),
                ("organizationId", organization.Id.ToString())));
        }

        // Institute contacts — if userTypeHint=student and a student record also exists, fall through
        var institute = await _db.Institutes
            .AsNoTracking()
            .FirstOrDefaultAsync(i =>
                i.ContactEmail != null &&
                i.ContactEmail.ToLower() == normalizedEmail.ToLower());

        if (institute != null)
        {
            if (userTypeHint == "student")
            {
                var studentExists = await _db.Students
                    .AsNoTracking()
                    .AnyAsync(s => s.Email != null && s.Email.ToLower() == normalizedEmail.ToLower());
                if (studentExists)
                {
                    _logger.LogInformation(
                        "Google login: userTypeHint=student overrides institute contact record for {Email}", normalizedEmail);
                    // fall through to student lookup below
                }
                else
                {
                    return Redirect(BuildGoogleCallbackUrl(effectiveFrontendBase,
                        ("status", "ok"),
                        ("userType", "institute"),
                        ("email", normalizedEmail),
                        ("instituteId", institute.Id.ToString())));
                }
            }
            else
            {
                return Redirect(BuildGoogleCallbackUrl(effectiveFrontendBase,
                    ("status", "ok"),
                    ("userType", "institute"),
                    ("email", normalizedEmail),
                    ("instituteId", institute.Id.ToString())));
            }
        }

        // Teacher accounts (individual staff, not the institute contact email)
        var teacher = await _db.Teachers
            .AsNoTracking()
            .Include(t => t.Institute)
            .FirstOrDefaultAsync(t =>
                t.Email != null &&
                t.Email.ToLower() == normalizedEmail.ToLower());

        if (teacher != null && teacher.Institute?.IsActive == true)
        {
            // If caller hinted student preference, check if a student record also exists and prefer it
            if (userTypeHint == "student")
            {
                var studentExists = await _db.Students
                    .AsNoTracking()
                    .AnyAsync(s => s.Email != null && s.Email.ToLower() == normalizedEmail.ToLower());
                if (studentExists)
                {
                    _logger.LogInformation(
                        "Google login: userTypeHint=student overrides teacher record for {Email}", normalizedEmail);
                    // fall through to student lookup below
                }
                else
                {
                    return Redirect(BuildGoogleCallbackUrl(effectiveFrontendBase,
                        ("status", "ok"),
                        ("userType", "institute"),
                        ("email", normalizedEmail),
                        ("instituteId", teacher.InstituteId.ToString())));
                }
            }
            else
            {
                return Redirect(BuildGoogleCallbackUrl(effectiveFrontendBase,
                    ("status", "ok"),
                    ("userType", "institute"),
                    ("email", normalizedEmail),
                    ("instituteId", teacher.InstituteId.ToString())));
            }
        }

        var student = await _db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.Email != null &&
                s.Email.ToLower() == normalizedEmail.ToLower());

        if (student != null)
        {
            return Redirect(BuildGoogleCallbackUrl(effectiveFrontendBase,
                ("status", "ok"),
                ("userType", "student"),
                ("email", normalizedEmail),
                ("studentId", student.Id.ToString()),
                ("boardId", student.BoardId ?? ""),
                ("studentStatus", student.Status?.ToString() ?? "")));
        }

        // No local account — SPA decides signup vs error using session authMode
        return Redirect(BuildGoogleCallbackUrl(effectiveFrontendBase,
            ("status", "no_account"),
            ("email", normalizedEmail),
            ("name", name ?? "")));
    }

    private static string BuildGoogleCallbackUrl(string frontendBase, params (string key, string value)[] query)
    {
        var qs = string.Join("&", query
            .Where(p => !string.IsNullOrEmpty(p.key))
            .Select(p => $"{Uri.EscapeDataString(p.key)}={Uri.EscapeDataString(p.value ?? "")}"));
        return $"{frontendBase.TrimEnd('/')}/GoogleCallback?{qs}";
    }

    private async Task<JsonElement> ExchangeCodeForTokenAsync(string code, string clientId, string clientSecret,
        string redirectUri)
    {
        var client = _httpClientFactory.CreateClient("GoogleOAuth");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        using var response = await client.PostAsync("https://oauth2.googleapis.com/token", content);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google token endpoint error {Status}: {Body}", response.StatusCode, body);
            throw new InvalidOperationException("Token exchange failed");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private async Task<(string? email, string? name, bool? verifiedEmail)> GetUserInfoAsync(string accessToken)
    {
        var client = _httpClientFactory.CreateClient("GoogleOAuth");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google userinfo error {Status}: {Body}", response.StatusCode, body);
            throw new InvalidOperationException("Userinfo failed");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
        var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
        bool? verified = root.TryGetProperty("verified_email", out var v) && v.ValueKind == JsonValueKind.True
            ? true
            : root.TryGetProperty("verified_email", out v) && v.ValueKind == JsonValueKind.False
                ? false
                : null;

        return (email, name, verified);
    }
}
