using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using System.Net;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using Microsoft.Extensions.Primitives;

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FigmaController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FigmaController> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public FigmaController(ApplicationDbContext context, ILogger<FigmaController> logger, IConfiguration configuration, HttpClient httpClient)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClient;
        }

        /// <summary>Diagnostics only: never log raw secrets.</summary>
        private static string DescribeSecret(string? value)
        {
            if (value == null) return "null";
            if (value.Length == 0) return "empty";
            return $"present,len={value.Length}";
        }

        private static string SanitizeFileNameSegment(string segment)
        {
            var s = segment.Replace(':', '-').Replace(',', '_');
            return string.Join("_", s.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        }

        private void LogFigmaIntegrationSnapshot(string phase, Figma figma)
        {
            _logger.LogInformation(
                "Figma row snapshot {Phase}: BoardId={BoardId} FigmaId={FigmaId} " +
                "AccessToken={Access} RefreshToken={Refresh} TokenExpiryUtc={Expiry} FigmaUserId={UserId} " +
                "HasFigmaFileUrl={HasUrl} FigmaFileKey={FileKey} UpdatedAtUtc={UpdatedAt}",
                phase,
                figma.BoardId,
                figma.Id,
                DescribeSecret(figma.FigmaAccessToken),
                DescribeSecret(figma.FigmaRefreshToken),
                figma.FigmaTokenExpiry,
                figma.FigmaUserId ?? "(null)",
                !string.IsNullOrEmpty(figma.FigmaFileUrl),
                figma.FigmaFileKey ?? "(null)",
                figma.UpdatedAt);
        }

        /// <summary>Result of Figma GET /v1/images/… (export URL only; PNG bytes fetched separately).</summary>
        private sealed record FigmaPngUrlResult(
            string? ImageUrl,
            string? ErrorMessage,
            int? ClientStatusCode,
            int? RetryAfterSeconds,
            string? ErrorCode,
            int? FigmaRetryAfterRawSeconds = null);

        private static int? TryGetRetryAfterSeconds(HttpResponseMessage response)
        {
            if (response.Headers.RetryAfter?.Delta is { TotalSeconds: > 0 } d)
            {
                return (int)Math.Ceiling(d.TotalSeconds);
            }

            if (response.Headers.TryGetValues("Retry-After", out var values))
            {
                var raw = values.FirstOrDefault();
                if (raw != null &&
                    int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var sec) &&
                    sec >= 0)
                {
                    return sec;
                }
            }

            return null;
        }

        /// <summary>Response + content headers for diagnostics (e.g. <c>Retry-After</c> on 429). Figma error responses should not include secrets.</summary>
        private static string FormatFigmaResponseHeadersForLog(HttpResponseMessage response)
        {
            var sb = new StringBuilder(256);
            void Append(string key, IEnumerable<string> values)
            {
                if (sb.Length > 0)
                    sb.Append("; ");
                sb.Append(key).Append('=').Append(string.Join(",", values));
            }

            foreach (var h in response.Headers)
                Append(h.Key, h.Value);
            foreach (var h in response.Content.Headers)
                Append(h.Key, h.Value);

            return sb.Length == 0 ? "(no headers)" : sb.ToString();
        }

        /// <summary>Forwarded to clients when Figma returns 429 so the UI can show plan tier, rate-limit type, and Retry-After.</summary>
        private sealed record FigmaFilesApiFailureExtras(int? RetryAfterSeconds, string? RateLimitType, string? PlanTier);

        private static string? GetFigmaHeaderFirstValue(HttpResponseMessage response, string headerName)
        {
            if (response.Headers.TryGetValues(headerName, out var v1))
                return v1.FirstOrDefault();
            if (response.Content.Headers.TryGetValues(headerName, out var v2))
                return v2.FirstOrDefault();
            foreach (var h in response.Headers)
            {
                if (string.Equals(h.Key, headerName, StringComparison.OrdinalIgnoreCase))
                    return h.Value.FirstOrDefault();
            }

            foreach (var h in response.Content.Headers)
            {
                if (string.Equals(h.Key, headerName, StringComparison.OrdinalIgnoreCase))
                    return h.Value.FirstOrDefault();
            }

            return null;
        }

        private static FigmaFilesApiFailureExtras? BuildFigmaFilesApiFailureExtras(HttpResponseMessage response, int httpStatus)
        {
            if (httpStatus != 429)
                return null;
            var retry = TryGetRetryAfterSeconds(response);
            var rateType = GetFigmaHeaderFirstValue(response, "X-Figma-Rate-Limit-Type");
            var planTier = GetFigmaHeaderFirstValue(response, "X-Figma-Plan-Tier");
            return new FigmaFilesApiFailureExtras(retry, rateType, planTier);
        }

        /// <summary>
        /// Figma sends a <c>Retry-After</c> value in seconds (RFC 7231). It can be very large when an API quota resets in
        /// days (e.g. some monthly/low tiers). Cap what we mirror on our HTTP response so clients get a usable backoff hint.
        /// </summary>
        private int? ToClientRetryAfterSeconds(int? figmaRetrySeconds)
        {
            const int maxClientHintSeconds = 3600;
            if (figmaRetrySeconds is null or <= 0)
            {
                return null;
            }

            if (figmaRetrySeconds <= maxClientHintSeconds)
            {
                return figmaRetrySeconds;
            }

            _logger.LogWarning(
                "Figma Retry-After is {RawSeconds}s (~{Days:F2} days). Large values usually mean time until quota/meter reset, not a bug. Capping API Retry-After / JSON hint to {Cap}s; see https://developers.figma.com/docs/rest-api/rate-limits/",
                figmaRetrySeconds.Value,
                figmaRetrySeconds.Value / 86400.0,
                maxClientHintSeconds);
            return maxClientHintSeconds;
        }

        private static int MapFigmaImagesHttpStatusToClient(HttpStatusCode status)
        {
            var code = (int)status;
            if (code >= 500)
            {
                return 502;
            }

            return status switch
            {
                HttpStatusCode.Unauthorized => 401,
                HttpStatusCode.Forbidden => 403,
                HttpStatusCode.NotFound => 404,
                HttpStatusCode.TooManyRequests => 429,
                HttpStatusCode.BadRequest => 400,
                _ => 400
            };
        }

        /// <summary>Stable machine-readable code for clients (e.g. reconnect banner).</summary>
        private static string? ClientStatusToErrorCode(int clientStatus) =>
            clientStatus switch
            {
                401 => "figma_reconnect_required",
                403 => "figma_access_denied",
                404 => "figma_not_found",
                429 => "figma_rate_limited",
                502 => "figma_upstream_error",
                504 => "figma_timeout",
                500 => "figma_internal",
                400 => "figma_bad_request",
                _ => "figma_error"
            };

        private static int Compute429BackoffMs(int? retryAfterSeconds, int baseDelayMs, int attemptZeroBased)
        {
            var fromHeaderMs = retryAfterSeconds.HasValue
                ? Math.Clamp(retryAfterSeconds.Value * 1000, 0, 120_000)
                : 0;
            var capMs = 30_000;
            var exponential = Math.Min(baseDelayMs * (1 << attemptZeroBased), capMs);
            var delay = Math.Max(fromHeaderMs, exponential);
            delay = Math.Min(delay, 120_000);
            delay += Random.Shared.Next(0, 250);
            return delay;
        }

        /// <summary>
        /// Get all Figma integrations
        /// </summary>
        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<FigmaResponse>>> GetAllFigma()
        {
            try
            {
                var figmaRecords = await _context.Figma
                    .OrderBy(f => f.CreatedAt)
                    .Select(f => new FigmaResponse
                    {
                        Id = f.Id,
                        BoardId = f.BoardId,
                        FigmaAccessToken = f.FigmaAccessToken,
                        FigmaRefreshToken = f.FigmaRefreshToken,
                        FigmaTokenExpiry = f.FigmaTokenExpiry,
                        FigmaUserId = f.FigmaUserId,
                        FigmaFileUrl = f.FigmaFileUrl,
                        FigmaFileKey = f.FigmaFileKey,
                        FigmaLastSync = f.FigmaLastSync,
                        CreatedAt = f.CreatedAt,
                        UpdatedAt = f.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(figmaRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all Figma records");
                return StatusCode(500, "An error occurred while retrieving Figma records");
            }
        }

        /// <summary>
        /// Get Figma integration by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<FigmaResponse>> GetFigmaById(int id)
        {
            try
            {
                var figma = await _context.Figma.FindAsync(id);
                if (figma == null)
                {
                    return NotFound($"Figma integration with ID {id} not found");
                }

                var response = new FigmaResponse
                {
                    Id = figma.Id,
                    BoardId = figma.BoardId,
                    FigmaAccessToken = figma.FigmaAccessToken,
                    FigmaRefreshToken = figma.FigmaRefreshToken,
                    FigmaTokenExpiry = figma.FigmaTokenExpiry,
                    FigmaUserId = figma.FigmaUserId,
                    FigmaFileUrl = figma.FigmaFileUrl,
                    FigmaFileKey = figma.FigmaFileKey,
                    FigmaLastSync = figma.FigmaLastSync,
                    CreatedAt = figma.CreatedAt,
                    UpdatedAt = figma.UpdatedAt
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Figma record with ID {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the Figma record");
            }
        }

        /// <summary>
        /// Get Figma integration by Board ID
        /// </summary>
        [HttpGet("by-board/{boardId}")]
        public async Task<ActionResult<FigmaResponse>> GetFigmaByBoardId(string boardId)
        {
            try
            {
                var figma = await _context.Figma
                    .FirstOrDefaultAsync(f => f.BoardId == boardId);

                if (figma == null)
                {
                    return NotFound($"Figma integration for board {boardId} not found");
                }

                var response = new FigmaResponse
                {
                    Id = figma.Id,
                    BoardId = figma.BoardId,
                    FigmaAccessToken = figma.FigmaAccessToken,
                    FigmaRefreshToken = figma.FigmaRefreshToken,
                    FigmaTokenExpiry = figma.FigmaTokenExpiry,
                    FigmaUserId = figma.FigmaUserId,
                    FigmaFileUrl = figma.FigmaFileUrl,
                    FigmaFileKey = figma.FigmaFileKey,
                    FigmaLastSync = figma.FigmaLastSync,
                    CreatedAt = figma.CreatedAt,
                    UpdatedAt = figma.UpdatedAt
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Figma record for board {BoardId}", boardId);
                return StatusCode(500, "An error occurred while retrieving the Figma record");
            }
        }

        /// <summary>
        /// Create new Figma integration
        /// </summary>
        [HttpPost("create")]
        public async Task<ActionResult<FigmaResponse>> CreateFigma([FromBody] CreateFigmaRequest request)
        {
            try
            {
                _logger.LogInformation("Creating Figma integration for board {BoardId}", request.BoardId);

                // Validate that the board exists
                var board = await _context.ProjectBoards
                    .FirstOrDefaultAsync(pb => pb.Id == request.BoardId);
                
                if (board == null)
                {
                    return BadRequest($"Board with ID {request.BoardId} not found");
                }

                // Check if Figma integration already exists for this board
                var existingFigma = await _context.Figma
                    .FirstOrDefaultAsync(f => f.BoardId == request.BoardId);
                
                if (existingFigma != null)
                {
                    return BadRequest($"Figma integration already exists for board {request.BoardId}");
                }

                var figma = new Figma
                {
                    BoardId = request.BoardId,
                    FigmaAccessToken = request.FigmaAccessToken,
                    FigmaRefreshToken = request.FigmaRefreshToken,
                    FigmaTokenExpiry = request.FigmaTokenExpiry,
                    FigmaUserId = request.FigmaUserId,
                    FigmaFileUrl = request.FigmaFileUrl,
                    FigmaFileKey = request.FigmaFileKey,
                    FigmaLastSync = request.FigmaLastSync,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Figma.Add(figma);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully created Figma integration with ID {Id} for board {BoardId}", 
                    figma.Id, figma.BoardId);

                var response = new FigmaResponse
                {
                    Id = figma.Id,
                    BoardId = figma.BoardId,
                    FigmaAccessToken = figma.FigmaAccessToken,
                    FigmaRefreshToken = figma.FigmaRefreshToken,
                    FigmaTokenExpiry = figma.FigmaTokenExpiry,
                    FigmaUserId = figma.FigmaUserId,
                    FigmaFileUrl = figma.FigmaFileUrl,
                    FigmaFileKey = figma.FigmaFileKey,
                    FigmaLastSync = figma.FigmaLastSync,
                    CreatedAt = figma.CreatedAt,
                    UpdatedAt = figma.UpdatedAt
                };

                return CreatedAtAction(nameof(GetFigmaById), new { id = figma.Id }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Figma integration for board {BoardId}", request.BoardId);
                return StatusCode(500, "An error occurred while creating the Figma integration");
            }
        }

        /// <summary>
        /// Update Figma integration
        /// </summary>
        [HttpPost("update/{id}")]
        public async Task<ActionResult<FigmaResponse>> UpdateFigma(int id, [FromBody] UpdateFigmaRequest request)
        {
            try
            {
                _logger.LogInformation("Updating Figma integration with ID {Id}", id);

                var figma = await _context.Figma.FindAsync(id);
                if (figma == null)
                {
                    return NotFound($"Figma integration with ID {id} not found");
                }

                // Update fields if provided
                if (request.FigmaAccessToken != null)
                    figma.FigmaAccessToken = request.FigmaAccessToken;
                
                if (request.FigmaRefreshToken != null)
                    figma.FigmaRefreshToken = request.FigmaRefreshToken;
                
                if (request.FigmaTokenExpiry.HasValue)
                    figma.FigmaTokenExpiry = request.FigmaTokenExpiry;
                
                if (request.FigmaUserId != null)
                    figma.FigmaUserId = request.FigmaUserId;
                
                if (request.FigmaFileUrl != null)
                    figma.FigmaFileUrl = request.FigmaFileUrl;
                
                if (request.FigmaFileKey != null)
                    figma.FigmaFileKey = request.FigmaFileKey;
                
                if (request.FigmaLastSync.HasValue)
                    figma.FigmaLastSync = request.FigmaLastSync;

                figma.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully updated Figma integration with ID {Id}", id);

                var response = new FigmaResponse
                {
                    Id = figma.Id,
                    BoardId = figma.BoardId,
                    FigmaAccessToken = figma.FigmaAccessToken,
                    FigmaRefreshToken = figma.FigmaRefreshToken,
                    FigmaTokenExpiry = figma.FigmaTokenExpiry,
                    FigmaUserId = figma.FigmaUserId,
                    FigmaFileUrl = figma.FigmaFileUrl,
                    FigmaFileKey = figma.FigmaFileKey,
                    FigmaLastSync = figma.FigmaLastSync,
                    CreatedAt = figma.CreatedAt,
                    UpdatedAt = figma.UpdatedAt
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Figma integration with ID {Id}", id);
                return StatusCode(500, "An error occurred while updating the Figma integration");
            }
        }

        /// <summary>
        /// Delete Figma integration
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteFigma(int id)
        {
            try
            {
                _logger.LogInformation("Deleting Figma integration with ID {Id}", id);

                var figma = await _context.Figma.FindAsync(id);
                if (figma == null)
                {
                    return NotFound($"Figma integration with ID {id} not found");
                }

                _context.Figma.Remove(figma);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted Figma integration with ID {Id}", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Figma integration with ID {Id}", id);
                return StatusCode(500, "An error occurred while deleting the Figma integration");
            }
        }

        /// <summary>
        /// Whether the board has a stored Figma OAuth token (no secrets returned).
        /// </summary>
        [HttpGet("use/connection")]
        public async Task<ActionResult<object>> GetConnection([FromQuery] string boardId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(boardId))
                {
                    return BadRequest("boardId is required.");
                }

                var figma = await _context.Figma.FirstOrDefaultAsync(f => f.BoardId == boardId);
                if (figma == null)
                {
                    return Ok(new
                    {
                        success = true,
                        boardId,
                        hasOAuthToken = false,
                        hasFigmaFileUrl = false
                    });
                }

                return Ok(new
                {
                    success = true,
                    boardId,
                    hasOAuthToken = !string.IsNullOrEmpty(figma.FigmaAccessToken),
                    hasFigmaFileUrl = !string.IsNullOrEmpty(figma.FigmaFileUrl)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading Figma connection for board {BoardId}", boardId);
                return StatusCode(500, "An error occurred while reading Figma connection status.");
            }
        }

        /// <summary>
        /// Generate OAuth URL for Figma authentication.
        /// Optional <paramref name="redirectUri"/> must exactly match an entry in <c>Figma:RedirectUrls</c> and in the Figma app (e.g. BoardRoom vs StudentRegistration).
        /// </summary>
        [HttpGet("use/oauth")]
        public ActionResult<object> GetOAuthUrl([FromQuery] string? redirectUri = null)
        {
            try
            {
                var clientId = _configuration["Figma:ClientId"];
                var redirectUrls = _configuration.GetSection("Figma:RedirectUrls").Get<string[]>();
                
                if (string.IsNullOrEmpty(clientId) || redirectUrls == null || redirectUrls.Length == 0)
                {
                    return BadRequest("Figma configuration is missing");
                }

                var redirectUriToUse = PickRedirectUri(redirectUri, redirectUrls);
                if (redirectUriToUse == null)
                {
                    return BadRequest("redirectUri is not listed in Figma:RedirectUrls.");
                }

                // Generate unique state for security
                var state = Guid.NewGuid().ToString();
                
                // Omit projects:read — not available for public OAuth apps; team/project listing APIs require a private app.
                var oauthUrl = $"https://www.figma.com/oauth?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUriToUse)}&scope=file_content:read,current_user:read,team_library_content:read,file_metadata:read,file_versions:read,library_assets:read,library_content:read&state={state}&response_type=code";

                _logger.LogInformation("Generated OAuth URL for Figma authentication");
                _logger.LogInformation("Requested scopes: file_content:read,current_user:read,team_library_content:read,file_metadata:read,file_versions:read,library_assets:read,library_content:read (projects:read omitted for public OAuth apps; file_variables:read and library_analytics:read are Enterprise-only)");

                return Ok(new
                {
                    success = true,
                    oauthUrl = oauthUrl,
                    state = state,
                    redirectUri = redirectUriToUse
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating OAuth URL");
                return StatusCode(500, "An error occurred while generating OAuth URL");
            }
        }

        /// <summary>Returns null if <paramref name="requested"/> is set but not allowed.</summary>
        private static string? PickRedirectUri(string? requested, string[] allowed)
        {
            if (string.IsNullOrWhiteSpace(requested))
            {
                return allowed[0];
            }

            foreach (var a in allowed)
            {
                if (string.Equals(a.Trim(), requested.Trim(), StringComparison.Ordinal))
                {
                    return a;
                }
            }

            return null;
        }

        /// <summary>
        /// Exchange authorization code for tokens and store them against a board or (during registration) a pending email.
        /// </summary>
        [HttpPost("use/store-tokens")]
        public async Task<ActionResult<object>> StoreTokens([FromBody] StoreTokensRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.AuthCode))
                {
                    return BadRequest("AuthCode is required.");
                }

                var clientId = _configuration["Figma:ClientId"];
                var clientSecret = _configuration["Figma:ClientSecret"];
                var redirectUrls = _configuration.GetSection("Figma:RedirectUrls").Get<string[]>();

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || redirectUrls == null || redirectUrls.Length == 0)
                {
                    return BadRequest("Figma configuration is missing");
                }

                var redirectUriForExchange = PickRedirectUri(request.RedirectUri, redirectUrls);
                if (redirectUriForExchange == null)
                {
                    return BadRequest("redirectUri is not listed in Figma:RedirectUrls.");
                }

                var formData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("redirect_uri", redirectUriForExchange),
                    new KeyValuePair<string, string>("code", request.AuthCode),
                    new KeyValuePair<string, string>("grant_type", "authorization_code")
                };

                var response = await _httpClient.PostAsync("https://api.figma.com/v1/oauth/token", new FormUrlEncodedContent(formData));
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Figma OAuth response: {StatusCode} - {Content}", response.StatusCode, responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Figma token exchange failed: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return BadRequest($"Failed to exchange authorization code: {responseContent}");
                }

                var tokenResponse = JsonSerializer.Deserialize<FigmaTokenResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (tokenResponse?.AccessToken == null)
                {
                    _logger.LogError("Invalid token response from Figma: {Response}", responseContent);
                    return BadRequest("Invalid token response from Figma");
                }

                var hasBoard = !string.IsNullOrWhiteSpace(request.BoardId);
                var hasEmail = !string.IsNullOrWhiteSpace(request.Email);

                if (hasBoard && hasEmail)
                {
                    return BadRequest("Provide BoardId or Email, not both.");
                }

                if (!hasBoard && !hasEmail)
                {
                    return BadRequest("BoardId or Email is required.");
                }

                if (hasEmail)
                {
                    var emailNorm = NormalizeRegistrationEmail(request.Email!);
                    _logger.LogInformation("Storing Figma tokens for registration email (pending board) {Email}", emailNorm);

                    var pending = await _context.FigmaOAuthPending.FirstOrDefaultAsync(p => p.Email == emailNorm);
                    if (pending != null)
                    {
                        pending.FigmaAccessToken = tokenResponse.AccessToken;
                        pending.FigmaRefreshToken = tokenResponse.RefreshToken;
                        pending.FigmaTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                        pending.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        _context.FigmaOAuthPending.Add(new FigmaOAuthPending
                        {
                            Email = emailNorm,
                            FigmaAccessToken = tokenResponse.AccessToken,
                            FigmaRefreshToken = tokenResponse.RefreshToken,
                            FigmaTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }

                    await _context.SaveChangesAsync();
                    return Ok(new
                    {
                        success = true,
                        message = "Tokens stored for your account. They will apply when you are assigned a project board."
                    });
                }

                _logger.LogInformation("Storing Figma tokens for board {BoardId}", request.BoardId);

                var existingFigma = await _context.Figma
                    .FirstOrDefaultAsync(f => f.BoardId == request.BoardId);

                if (existingFigma != null)
                {
                    _logger.LogInformation("Updating existing Figma record for board {BoardId}", request.BoardId);
                    existingFigma.FigmaAccessToken = tokenResponse.AccessToken;
                    existingFigma.FigmaRefreshToken = tokenResponse.RefreshToken;
                    existingFigma.FigmaTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    existingFigma.FigmaLastSync = DateTime.UtcNow;
                    existingFigma.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _logger.LogInformation("Creating new Figma record for board {BoardId}", request.BoardId);
                    var figma = new Figma
                    {
                        BoardId = request.BoardId!,
                        FigmaAccessToken = tokenResponse.AccessToken,
                        FigmaRefreshToken = tokenResponse.RefreshToken,
                        FigmaTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                        FigmaLastSync = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Figma.Add(figma);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully stored Figma tokens for board {BoardId}", request.BoardId);

                return Ok(new
                {
                    success = true,
                    message = "Tokens stored successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing Figma tokens");
                return StatusCode(500, "An error occurred while storing tokens");
            }
        }

        /// <summary>
        /// Moves tokens from <see cref="FigmaOAuthPending"/> to <see cref="Figma"/> when the student gets a board.
        /// </summary>
        [HttpPost("use/apply-pending")]
        public async Task<ActionResult<object>> ApplyPendingFigmaTokens([FromBody] ApplyPendingFigmaRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.BoardId))
                {
                    return BadRequest("Email and BoardId are required.");
                }

                var emailNorm = NormalizeRegistrationEmail(request.Email);
                var student = await _context.Students.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Email != null && s.Email.ToLower() == emailNorm && s.BoardId == request.BoardId);

                if (student == null)
                {
                    return NotFound("No student matches this email and board.");
                }

                var pending = await _context.FigmaOAuthPending.FirstOrDefaultAsync(p => p.Email == emailNorm);
                if (pending == null || string.IsNullOrEmpty(pending.FigmaAccessToken))
                {
                    return Ok(new { success = true, applied = false });
                }

                var existing = await _context.Figma.FirstOrDefaultAsync(f => f.BoardId == request.BoardId);
                if (existing != null)
                {
                    existing.FigmaAccessToken = pending.FigmaAccessToken;
                    existing.FigmaRefreshToken = pending.FigmaRefreshToken;
                    existing.FigmaTokenExpiry = pending.FigmaTokenExpiry;
                    existing.FigmaLastSync = DateTime.UtcNow;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.Figma.Add(new Figma
                    {
                        BoardId = request.BoardId,
                        FigmaAccessToken = pending.FigmaAccessToken,
                        FigmaRefreshToken = pending.FigmaRefreshToken,
                        FigmaTokenExpiry = pending.FigmaTokenExpiry,
                        FigmaLastSync = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                _context.FigmaOAuthPending.Remove(pending);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, applied = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying pending Figma tokens");
                return StatusCode(500, "An error occurred while applying pending Figma tokens.");
            }
        }

        private static string NormalizeRegistrationEmail(string email) => email.Trim().ToLowerInvariant();

        /// <summary>
        /// Get list of available Figma files for the user
        /// </summary>
        [HttpGet("use/get-public-files")]
        public async Task<ActionResult<object>> GetPublicFiles([FromQuery] string boardId)
        {
            try
            {
                _logger.LogInformation("Getting public files for board {BoardId}", boardId);

                // Get Figma integration for this board
                var figma = await _context.Figma
                    .FirstOrDefaultAsync(f => f.BoardId == boardId);

                if (figma == null)
                {
                    return NotFound($"Figma integration not found for board {boardId}");
                }

                // Check if access token is expired and refresh if needed
                await RefreshTokenIfNeeded(figma);

                LogFigmaIntegrationSnapshot("get-public-files", figma);

                // Fetch user's teams/projects/files structure from Figma API
                var teamsStructure = await GetUserFigmaStructure(figma.FigmaAccessToken!);
                if (teamsStructure == null)
                {
                    return BadRequest("Failed to fetch structure from Figma. The existing token may not have the required scopes. Please re-authenticate with Figma to get updated permissions.");
                }

                _logger.LogInformation("Successfully retrieved structure for board {BoardId}", boardId);

                return Ok(new
                {
                    success = true,
                    teams = teamsStructure
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting public files for board {BoardId}", boardId);
                return StatusCode(500, "An error occurred while fetching files");
            }
        }

        /// <summary>
        /// Store the Figma file URL after user selection
        /// </summary>
        [HttpPost("use/set-public-file")]
        public async Task<ActionResult<object>> SetPublicFile([FromBody] SetPublicFileRequest request)
        {
            try
            {
                _logger.LogInformation("Setting public file for board {BoardId}", request.BoardId);

                // Extract file key from URL
                var fileKey = ExtractFileKeyFromUrl(request.FileUrl);
                if (string.IsNullOrEmpty(fileKey))
                {
                    return BadRequest("Could not extract file key from the provided URL");
                }

                _logger.LogInformation("Extracted file key: {FileKey}", fileKey);

                // Get Figma integration for this board
                var figma = await _context.Figma
                    .FirstOrDefaultAsync(f => f.BoardId == request.BoardId);

                if (figma == null)
                {
                    return NotFound($"Figma integration not found for board {request.BoardId}");
                }

                // Check if access token is expired and refresh if needed
                await RefreshTokenIfNeeded(figma);

                // Validate the file using Figma API
                var isValid = await ValidateFigmaFile(figma.FigmaAccessToken!, fileKey);
                if (!isValid)
                {
                    _logger.LogWarning("File validation failed, attempting forced token refresh and retry");
                    
                    // Force refresh the token (ignore expiry check) and validate again
                    await ForceRefreshToken(figma);
                    var retryIsValid = await ValidateFigmaFile(figma.FigmaAccessToken!, fileKey);
                    
                    if (!retryIsValid)
                    {
                        return BadRequest("Invalid or inaccessible Figma file. Please re-authenticate with Figma.");
                    }
                }

                // Update the file information
                figma.FigmaFileUrl = request.FileUrl;
                figma.FigmaFileKey = fileKey;
                figma.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully set public file for board {BoardId}", request.BoardId);

                return Ok(new
                {
                    success = true,
                    message = "File URL set successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting public file for board {BoardId}", request.BoardId);
                return StatusCode(500, "An error occurred while setting public file");
            }
        }

        /// <summary>
        /// Extract file key from Figma URL
        /// </summary>
        private string ExtractFileKeyFromUrl(string url)
        {
            try
            {
                _logger.LogInformation("Extracting file key from URL: {Url}", url);

                if (string.IsNullOrWhiteSpace(url))
                {
                    return string.Empty;
                }

                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var withScheme = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? url
                        : $"https://{url.TrimStart('/')}";

                    if (!Uri.TryCreate(withScheme, UriKind.Absolute, out uri))
                    {
                        _logger.LogWarning("Unable to parse URL even after adding scheme: {Url}", url);
                        return string.Empty;
                    }
                }

                var path = uri.AbsolutePath;
                _logger.LogInformation("URL path: {Path}", path);

                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                _logger.LogInformation("Path segments: {Segments}", string.Join(", ", segments));

                // /file/{key}/..., /design/{key}/..., /proto/{key}/... (browser URLs)
                for (int i = 0; i < segments.Length - 1; i++)
                {
                    if (segments[i].Equals("file", StringComparison.OrdinalIgnoreCase) ||
                        segments[i].Equals("design", StringComparison.OrdinalIgnoreCase) ||
                        segments[i].Equals("proto", StringComparison.OrdinalIgnoreCase))
                    {
                        var candidate = segments[i + 1];
                        if (!string.IsNullOrEmpty(candidate))
                        {
                            _logger.LogInformation("Found file key (Figma path): {FileKey}", candidate);
                            return candidate;
                        }
                    }
                }

                // Handle community URL structure: /community/file/{key}/{title}
                for (int i = 0; i < segments.Length - 1; i++)
                {
                    if (segments[i].Equals("file", StringComparison.OrdinalIgnoreCase))
                    {
                        var candidate = segments[i + 1];
                        if (!string.IsNullOrEmpty(candidate))
                        {
                            _logger.LogInformation("Found file key (community path): {FileKey}", candidate);
                            return candidate;
                        }
                    }
                }

                // Fallback: scan for alphanumeric segments resembling file keys (20-40 chars typical)
                foreach (var segment in segments)
                {
                    if (segment.Length >= 16 && segment.Length <= 40 && segment.All(c => char.IsLetterOrDigit(c)))
                    {
                        _logger.LogInformation("Found file key (fallback scan): {FileKey}", segment);
                        return segment;
                    }
                }

                _logger.LogWarning("No valid file key found in URL: {Url}", url);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting file key from URL: {Url}", url);
                return string.Empty;
            }
        }

        /// <summary>
        /// Get Figma file URL for a board
        /// </summary>
        [HttpGet("use/get-url")]
        public async Task<ActionResult<object>> GetFigmaUrl([FromQuery] string boardId)
        {
            try
            {
                _logger.LogInformation("Getting Figma URL for board {BoardId}", boardId);

                var figma = await _context.Figma
                    .FirstOrDefaultAsync(f => f.BoardId == boardId);

                if (figma == null)
                {
                    _logger.LogInformation("No Figma integration found for board {BoardId}, returning empty result", boardId);
                    return Ok(new
                    {
                        success = true,
                        boardId = boardId,
                        figmaFileUrl = string.Empty
                    });
                }

                return Ok(new
                {
                    success = true,
                    boardId = boardId,
                    figmaFileUrl = figma.FigmaFileUrl ?? string.Empty
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Figma URL for board {BoardId}", boardId);
                return StatusCode(500, "An error occurred while getting Figma URL");
            }
        }

        /// <summary>
        /// Set Figma file URL for a board
        /// </summary>
        [HttpPost("use/set-url")]
        public async Task<ActionResult<object>> SetFigmaUrl([FromBody] SetFigmaUrlRequest request)
        {
            try
            {
                _logger.LogInformation("Setting Figma URL for board {BoardId}", request.BoardId);

                var figma = await _context.Figma
                    .FirstOrDefaultAsync(f => f.BoardId == request.BoardId);

                if (figma == null)
                {
                    // Create new Figma record
                    _logger.LogInformation("Creating new Figma record for board {BoardId}", request.BoardId);
                    figma = new Figma
                    {
                        BoardId = request.BoardId,
                        FigmaFileUrl = request.Url,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Figma.Add(figma);
                }
                else
                {
                    // Update existing Figma record
                    _logger.LogInformation("Updating existing Figma record for board {BoardId}", request.BoardId);
                    figma.FigmaFileUrl = request.Url;
                    figma.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully set Figma URL for board {BoardId}", request.BoardId);

                return Ok(new
                {
                    success = true,
                    message = "Figma URL set successfully",
                    boardId = request.BoardId,
                    figmaFileUrl = request.Url
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting Figma URL for board {BoardId}", request.BoardId);
                return StatusCode(500, "An error occurred while setting Figma URL");
            }
        }

        /// <summary>
        /// Download file document JSON from Figma REST <c>GET /v1/files/:file_key</c> (OAuth Bearer).
        /// <see cref="DownloadMetadataRequest.FigmaFileUrl"/> <b>must</b> include <c>node-id</c> or <c>node_id</c> (e.g. “Copy link to selection”); full-file export is not allowed on this endpoint.
        /// <see cref="Figma"/> row is used only for OAuth tokens (<see cref="Figma.BoardId"/>).
        /// </summary>
        [HttpPost("use/download-metadata")]
        public async Task<ActionResult> DownloadMetadata([FromBody] DownloadMetadataRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.BoardId))
                {
                    return BadRequest("BoardId is required.");
                }

                if (string.IsNullOrWhiteSpace(request.FigmaFileUrl))
                {
                    return BadRequest("FigmaFileUrl is required (pass the file URL from your Resources or client).");
                }

                _logger.LogInformation("Downloading Figma file JSON for board {BoardId}", request.BoardId);

                var figma = await _context.Figma
                    .FirstOrDefaultAsync(f => f.BoardId == request.BoardId);

                if (figma == null)
                {
                    return NotFound($"Figma integration not found for board {request.BoardId}");
                }

                if (string.IsNullOrEmpty(figma.FigmaAccessToken))
                {
                    return BadRequest("No Figma access token for this board. Complete Figma OAuth first.");
                }

                var trimmedUrl = request.FigmaFileUrl.Trim();
                var fileKey = ExtractFileKeyFromUrl(trimmedUrl);
                if (string.IsNullOrEmpty(fileKey))
                {
                    return BadRequest("Could not extract a file key from FigmaFileUrl. Use a standard figma.com design or file URL.");
                }

                var nodeIdsFilter = TryExtractNodeIdFromFigmaUrl(trimmedUrl);
                if (string.IsNullOrEmpty(nodeIdsFilter))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "FigmaFileUrl must include a node id. Use a Figma “Copy link to selection” URL with ?node-id=… (or node_id=…) so only that subtree is fetched."
                    });
                }

                _logger.LogInformation(
                    "download-metadata: restricting GET /v1/files to node id(s) from URL (ids={Ids})",
                    nodeIdsFilter);

                await RefreshTokenIfNeeded(figma);

                if (string.IsNullOrEmpty(figma.FigmaAccessToken))
                {
                    return BadRequest("No Figma access token after refresh. Reconnect Figma for this board.");
                }

                var (metadata, figmaHttp, figmaErrBody, failureExtras) =
                    await GetFigmaFileMetadata(figma.FigmaAccessToken, fileKey, nodeIdsFilter, request.Depth);
                if (metadata == null)
                {
                    const int maxFigmaErrChars = 500_000;
                    var errPreview = string.IsNullOrEmpty(figmaErrBody)
                        ? "(empty body)"
                        : (figmaErrBody.Length > maxFigmaErrChars ? figmaErrBody[..maxFigmaErrChars] + "…" : figmaErrBody);
                    var payload = new
                    {
                        success = false,
                        message = "Figma GET /v1/files did not return file JSON. Use figmaHttpStatus and figmaResponse below; typical fixes: share the file with the same Figma account that connected OAuth for this board, reconnect OAuth if the token is stale, or wait and retry after HTTP 429 (rate limit).",
                        figmaHttpStatus = figmaHttp,
                        figmaResponse = errPreview,
                        fileKey,
                        hadNodeIdsFilter = !string.IsNullOrEmpty(nodeIdsFilter),
                        figmaRetryAfterSeconds = failureExtras?.RetryAfterSeconds,
                        figmaRateLimitType = failureExtras?.RateLimitType,
                        figmaPlanTier = failureExtras?.PlanTier
                    };
                    if (figmaHttp is >= 400 and <= 599)
                        return StatusCode(figmaHttp, payload);
                    return StatusCode(502, payload);
                }

                await _context.SaveChangesAsync();

                var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));

                var safeKey = string.Join("_", fileKey.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
                var filename = string.IsNullOrEmpty(nodeIdsFilter)
                    ? $"figma-file-{request.BoardId}-{safeKey}.json"
                    : $"figma-node-{request.BoardId}-{safeKey}-{SanitizeFileNameSegment(nodeIdsFilter)}.json";
                return File(jsonBytes, "application/json", filename);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("refresh", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(ex, "Figma token refresh failed for board {BoardId}", request?.BoardId);
                return BadRequest("Figma token could not be refreshed. Reconnect Figma for this board.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading metadata for board {BoardId}", request?.BoardId);
                return StatusCode(500, "An error occurred while downloading metadata");
            }
        }

        /// <summary>Shared resolution for <see cref="ExportPngRequest"/> (board token, file key, node id).</summary>
        private async Task<(ActionResult? Error, Figma? FigmaEntity, string? FileKey, string? NodeId, double Scale)> ResolveExportPngAsync(
            ExportPngRequest request,
            string logLabel)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.BoardId))
            {
                return (BadRequest("BoardId is required."), null, null, null, 1d);
            }

            var boardFigmaRowCount = await _context.Figma.CountAsync(f => f.BoardId == request.BoardId);
            _logger.LogInformation(
                "{LogLabel}: BoardId={BoardId} matchingFigmaRows={RowCount}",
                logLabel,
                request.BoardId,
                boardFigmaRowCount);
            if (boardFigmaRowCount > 1)
            {
                _logger.LogWarning(
                    "{LogLabel}: multiple Figma rows for BoardId={BoardId} count={Count}; FirstOrDefault may not match the row you queried in SQL.",
                    logLabel,
                    request.BoardId,
                    boardFigmaRowCount);
            }

            var figma = await _context.Figma.FirstOrDefaultAsync(f => f.BoardId == request.BoardId);
            if (figma == null)
            {
                _logger.LogWarning("{LogLabel}: no Figma row after count BoardId={BoardId}", logLabel, request.BoardId);
                return (NotFound($"Figma integration not found for board {request.BoardId}"), null, null, null, 1d);
            }

            LogFigmaIntegrationSnapshot($"{logLabel}:loaded", figma);

            if (string.IsNullOrEmpty(figma.FigmaAccessToken))
            {
                _logger.LogWarning(
                    "{LogLabel}: blocked — empty access token BoardId={BoardId} FigmaId={FigmaId}",
                    logLabel,
                    request.BoardId,
                    figma.Id);
                return (BadRequest("No Figma access token for this board. Complete Figma OAuth first."), null, null, null, 1d);
            }

            var effectiveUrl = !string.IsNullOrWhiteSpace(request.FileUrl)
                ? request.FileUrl.Trim()
                : (figma.FigmaFileUrl ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(effectiveUrl))
            {
                return (BadRequest("FileUrl is missing and no FigmaFileUrl is stored for this board. Pass FileUrl or save a file URL on the Figma integration."), null, null, null, 1d);
            }

            await RefreshTokenIfNeeded(figma);
            LogFigmaIntegrationSnapshot($"{logLabel}:after-refresh", figma);

            var fileKey = ExtractFileKeyFromUrl(effectiveUrl);
            if (string.IsNullOrEmpty(fileKey) && !string.IsNullOrEmpty(figma.FigmaFileKey))
            {
                fileKey = figma.FigmaFileKey;
            }

            if (string.IsNullOrEmpty(fileKey))
            {
                return (BadRequest("Could not determine file key from URL or database."), null, null, null, 1d);
            }

            var nodeRaw = !string.IsNullOrWhiteSpace(request.NodeId)
                ? request.NodeId.Trim()
                : TryExtractNodeIdFromFigmaUrl(effectiveUrl);

            if (string.IsNullOrEmpty(nodeRaw))
            {
                return (BadRequest("NodeId is required, or FileUrl must include a node-id query parameter (e.g. …?node-id=1-2)."), null, null, null, 1d);
            }

            var nodeId = NormalizeFigmaNodeId(nodeRaw);
            var scale = request.Scale is > 0 and <= 4 ? request.Scale.Value : 1d;

            return (null, figma, fileKey, nodeId, scale);
        }

        private ActionResult<object>? MapFigmaPngUrlErrorToResult(FigmaPngUrlResult pngUrlResult)
        {
            if (pngUrlResult.ErrorMessage == null)
            {
                return null;
            }

            if (pngUrlResult.RetryAfterSeconds is { } ras)
            {
                Response.Headers.Append("Retry-After", ras.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            var errPayload = new
            {
                success = false,
                message = pngUrlResult.ErrorMessage,
                retryAfterSeconds = pngUrlResult.RetryAfterSeconds,
                figmaRetryAfterSeconds = pngUrlResult.FigmaRetryAfterRawSeconds,
                errorCode = pngUrlResult.ErrorCode
            };
            return pngUrlResult.ClientStatusCode is { } code
                ? StatusCode(code, errPayload)
                : BadRequest(errPayload);
        }

        /// <summary>
        /// Rasterize a Figma node to PNG and return base64. Requires a stored OAuth token for the board.
        /// Omit <see cref="ExportPngRequest.FileUrl"/> to use <see cref="Figma.FigmaFileUrl"/> from the database (must include <c>node-id</c> or pass <see cref="ExportPngRequest.NodeId"/>).
        /// </summary>
        [HttpPost("use/export-png")]
        public async Task<ActionResult<object>> ExportPngAsBase64([FromBody] ExportPngRequest request)
        {
            try
            {
                var (resolveErr, figma, fileKey, nodeId, scale) = await ResolveExportPngAsync(request, "export-png");
                if (resolveErr != null)
                {
                    return resolveErr;
                }

                _logger.LogInformation(
                    "export-png: invoking Figma images API BoardId={BoardId} FigmaId={FigmaId} FileKey={FileKey} NodeId={NodeId} Scale={Scale} BearerToken={Bearer}",
                    request!.BoardId,
                    figma!.Id,
                    fileKey,
                    nodeId,
                    scale,
                    DescribeSecret(figma.FigmaAccessToken));

                var pngUrlResult = await RequestFigmaPngExportUrl(figma.FigmaAccessToken!, fileKey!, nodeId!, scale);
                var errResult = MapFigmaPngUrlErrorToResult(pngUrlResult);
                if (errResult != null)
                {
                    return errResult;
                }

                var imageUrl = pngUrlResult.ImageUrl;
                if (string.IsNullOrEmpty(imageUrl))
                {
                    return BadRequest(new { success = false, message = "Figma did not return an image URL for this node. Check node id and file access." });
                }

                _httpClient.DefaultRequestHeaders.Clear();
                using var pngResponse = await _httpClient.GetAsync(imageUrl);
                var pngBytes = await pngResponse.Content.ReadAsByteArrayAsync();

                if (!pngResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Figma PNG download failed: {Status}", pngResponse.StatusCode);
                    var downstream = pngResponse.StatusCode == HttpStatusCode.NotFound ? 404 : 502;
                    return StatusCode(downstream, new { success = false, message = "Failed to download rendered PNG from Figma." });
                }

                var pngBase64 = Convert.ToBase64String(pngBytes);

                return Ok(new
                {
                    success = true,
                    fileKey,
                    nodeId,
                    scale,
                    pngBase64
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Figma PNG for board {BoardId}", request?.BoardId);
                return StatusCode(500, "An error occurred while exporting the Figma PNG.");
            }
        }

        /// <summary>
        /// Same flow as <c>export-png</c> through Figma <c>/v1/images</c>, but returns only the short-lived CDN <c>imageUrl</c> (no base64). Use immediately — link expires.
        /// </summary>
        [HttpPost("use/export-png-url")]
        public async Task<ActionResult<object>> ExportPngRenderUrl([FromBody] ExportPngRequest request)
        {
            try
            {
                var (resolveErr, figma, fileKey, nodeId, scale) = await ResolveExportPngAsync(request, "export-png-url");
                if (resolveErr != null)
                {
                    return resolveErr;
                }

                _logger.LogInformation(
                    "export-png-url: invoking Figma images API BoardId={BoardId} FigmaId={FigmaId} FileKey={FileKey} NodeId={NodeId} Scale={Scale} BearerToken={Bearer}",
                    request!.BoardId,
                    figma!.Id,
                    fileKey,
                    nodeId,
                    scale,
                    DescribeSecret(figma.FigmaAccessToken));

                var pngUrlResult = await RequestFigmaPngExportUrl(figma.FigmaAccessToken!, fileKey!, nodeId!, scale);
                var errResult = MapFigmaPngUrlErrorToResult(pngUrlResult);
                if (errResult != null)
                {
                    return errResult;
                }

                var imageUrl = pngUrlResult.ImageUrl;
                if (string.IsNullOrEmpty(imageUrl))
                {
                    return BadRequest(new { success = false, message = "Figma did not return an image URL for this node. Check node id and file access." });
                }

                return Ok(new
                {
                    success = true,
                    fileKey,
                    nodeId,
                    scale,
                    imageUrl,
                    hint = "URL expires soon; open or download in the browser immediately. Same rate limits as export-png."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving Figma PNG URL for board {BoardId}", request?.BoardId);
                return StatusCode(500, "An error occurred while resolving the Figma image URL.");
            }
        }

        /// <summary>
        /// Retrieve the raw JSON representation of a public Figma file using its shared URL
        /// </summary>
        [HttpPost("use/fetch-public-file")]
        public async Task<ActionResult> FetchPublicFile([FromBody] FetchPublicFileRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.FileUrl))
                {
                    return BadRequest("FileUrl is required.");
                }

                var fileKey = ExtractFileKeyFromUrl(request.FileUrl);
                if (string.IsNullOrEmpty(fileKey))
                {
                    return BadRequest("Unable to extract a file key from the provided URL.");
                }

                _logger.LogInformation("Fetching public Figma JSON for file key {FileKey}", fileKey);

                var candidateEndpoints = new[]
                {
                    $"https://www.figma.com/api/design/{fileKey}.json?format=figma",
                    $"https://www.figma.com/api/design/{fileKey}.json",
                    $"https://www.figma.com/file/{fileKey}.json",
                    $"https://www.figma.com/file/{fileKey}.json?raw=1"
                };

                foreach (var endpoint in candidateEndpoints)
                {
                    try
                    {
                        _httpClient.DefaultRequestHeaders.Clear();
                        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SkillIn", "1.0"));
                        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        _logger.LogInformation("Attempting to fetch public Figma JSON from {Endpoint}", endpoint);

                        using var response = await _httpClient.GetAsync(endpoint);
                        var content = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Public Figma fetch failed from {Endpoint}: {StatusCode}", endpoint, response.StatusCode);
                            continue;
                        }

                        try
                        {
                            using var _ = JsonDocument.Parse(content);
                            _logger.LogInformation("Successfully retrieved public JSON from {Endpoint}", endpoint);
                            return Content(content, "application/json");
                        }
                        catch (JsonException jsonEx)
                        {
                            _logger.LogWarning(jsonEx, "Response from {Endpoint} was not valid JSON", endpoint);
                        }
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogWarning(innerEx, "Error attempting to fetch public Figma JSON from {Endpoint}", endpoint);
                    }
                }

                return StatusCode(502, new
                {
                    success = false,
                    message = "Unable to retrieve public JSON for the supplied Figma URL. Ensure the file is shared publicly or via the Figma Community."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching public Figma JSON for URL {Url}", request?.FileUrl);
                return StatusCode(500, "An error occurred while fetching the public Figma file JSON.");
            }
        }

        /// <summary>
        /// Exchange an authorization code for tokens and immediately fetch Figma file metadata without persisting anything
        /// </summary>
        [HttpPost("use/exchange-code-and-fetch")]
        public async Task<ActionResult> ExchangeCodeAndFetch([FromBody] ExchangeCodeFetchRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.AuthCode) || string.IsNullOrWhiteSpace(request.FileUrl))
                {
                    return BadRequest("AuthCode and FileUrl are required.");
                }

                var clientId = _configuration["Figma:ClientId"];
                var clientSecret = _configuration["Figma:ClientSecret"];
                var redirectUrls = _configuration.GetSection("Figma:RedirectUrls").Get<string[]>() ?? Array.Empty<string>();

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    return BadRequest("Figma OAuth configuration is missing.");
                }

                var redirectUri = request.RedirectUri;
                if (string.IsNullOrWhiteSpace(redirectUri))
                {
                    redirectUri = redirectUrls.FirstOrDefault();
                }

                if (string.IsNullOrWhiteSpace(redirectUri))
                {
                    return BadRequest("RedirectUri is required (either in request or configuration).");
                }

                var tokenForm = new List<KeyValuePair<string, string>>
                {
                    new("client_id", clientId!),
                    new("client_secret", clientSecret!),
                    new("redirect_uri", redirectUri),
                    new("code", request.AuthCode),
                    new("grant_type", "authorization_code")
                };

                var tokenResponse = await _httpClient.PostAsync("https://api.figma.com/v1/oauth/token", new FormUrlEncodedContent(tokenForm));
                var tokenContent = await tokenResponse.Content.ReadAsStringAsync();

                _logger.LogInformation("Figma OAuth exchange response: {StatusCode} - {Content}", tokenResponse.StatusCode, tokenContent);

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Failed to exchange authorization code",
                        details = tokenContent
                    });
                }

                var tokenPayload = JsonSerializer.Deserialize<FigmaTokenResponse>(tokenContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (tokenPayload?.AccessToken == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid token response from Figma",
                        details = tokenContent
                    });
                }

                var fileUrlTrimmed = request.FileUrl.Trim();
                var fileKey = ExtractFileKeyFromUrl(fileUrlTrimmed);
                if (string.IsNullOrEmpty(fileKey))
                {
                    return BadRequest("Unable to extract file key from the provided URL.");
                }

                var nodeIdsFilter = TryExtractNodeIdFromFigmaUrl(fileUrlTrimmed);
                var (metadata, figmaHttp, figmaErrBody, failureExtras) =
                    await GetFigmaFileMetadata(tokenPayload.AccessToken, fileKey, nodeIdsFilter, null);
                if (metadata == null)
                {
                    var errPreview = string.IsNullOrEmpty(figmaErrBody)
                        ? "(empty body)"
                        : (figmaErrBody.Length > 4000 ? figmaErrBody[..4000] + "…" : figmaErrBody);
                    return BadRequest(new
                    {
                        success = false,
                        message = "Failed to fetch file metadata from Figma.",
                        figmaHttpStatus = figmaHttp,
                        figmaResponse = errPreview,
                        figmaRetryAfterSeconds = failureExtras?.RetryAfterSeconds,
                        figmaRateLimitType = failureExtras?.RateLimitType,
                        figmaPlanTier = failureExtras?.PlanTier
                    });
                }

                return Ok(new
                {
                    success = true,
                    metadata
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exchanging code and fetching Figma metadata for URL {Url}", request?.FileUrl);
                return StatusCode(500, "An error occurred while exchanging code and fetching metadata.");
            }
        }

        /// <summary>
        /// Refresh access token if expired
        /// </summary>
        private async Task RefreshTokenIfNeeded(Figma figma)
        {
            _logger.LogInformation("Checking token expiry - Current time: {CurrentTime}, Token expiry: {TokenExpiry}, Is expired: {IsExpired}", 
                DateTime.UtcNow, figma.FigmaTokenExpiry, 
                figma.FigmaTokenExpiry.HasValue ? figma.FigmaTokenExpiry.Value <= DateTime.UtcNow : true);

            if (figma.FigmaTokenExpiry.HasValue && figma.FigmaTokenExpiry.Value > DateTime.UtcNow)
            {
                _logger.LogInformation("Token is still valid, no refresh needed");
                return; // Token is still valid
            }

            if (string.IsNullOrEmpty(figma.FigmaRefreshToken))
            {
                _logger.LogError("No refresh token available for token refresh");
                throw new InvalidOperationException("No refresh token available");
            }

            _logger.LogInformation("Token expired, attempting refresh; refresh token field {Refresh}", DescribeSecret(figma.FigmaRefreshToken));

            var clientId = _configuration["Figma:ClientId"];
            var clientSecret = _configuration["Figma:ClientSecret"];

            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("refresh_token", figma.FigmaRefreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token")
            };

            var content = new FormUrlEncodedContent(formData);

            var response = await _httpClient.PostAsync("https://api.figma.com/v1/oauth/token", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Token refresh response - Status: {StatusCode}, Content: {Content}", response.StatusCode, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to refresh token: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new InvalidOperationException($"Failed to refresh token: {responseContent}");
            }

            var tokenResponse = JsonSerializer.Deserialize<FigmaTokenResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse?.AccessToken != null)
            {
                figma.FigmaAccessToken = tokenResponse.AccessToken;
                figma.FigmaTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                {
                    figma.FigmaRefreshToken = tokenResponse.RefreshToken;
                }
                figma.UpdatedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Force refresh token regardless of expiry time
        /// </summary>
        private async Task ForceRefreshToken(Figma figma)
        {
            if (string.IsNullOrEmpty(figma.FigmaRefreshToken))
            {
                _logger.LogError("No refresh token available for forced token refresh");
                throw new InvalidOperationException("No refresh token available");
            }

            _logger.LogInformation("Force refreshing token; refresh token field {Refresh}", DescribeSecret(figma.FigmaRefreshToken));

            var clientId = _configuration["Figma:ClientId"];
            var clientSecret = _configuration["Figma:ClientSecret"];

            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("refresh_token", figma.FigmaRefreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token")
            };

            var content = new FormUrlEncodedContent(formData);

            var response = await _httpClient.PostAsync("https://api.figma.com/v1/oauth/token", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Force token refresh response - Status: {StatusCode}, Content: {Content}", response.StatusCode, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to force refresh token: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new InvalidOperationException($"Failed to force refresh token: {responseContent}");
            }

            var tokenResponse = JsonSerializer.Deserialize<FigmaTokenResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse?.AccessToken != null)
            {
                figma.FigmaAccessToken = tokenResponse.AccessToken;
                figma.FigmaTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                {
                    figma.FigmaRefreshToken = tokenResponse.RefreshToken;
                }
                figma.UpdatedAt = DateTime.UtcNow;
                
                _logger.LogInformation("Successfully force refreshed token");
            }
        }

        /// <summary>
        /// Validate Figma file accessibility
        /// </summary>
        private async Task<bool> ValidateFigmaFile(string accessToken, string fileKey)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                _logger.LogInformation("Validating Figma file with key: {FileKey}", fileKey);

                using var response = await _httpClient.GetAsync($"https://api.figma.com/v1/files/{Uri.EscapeDataString(fileKey)}");
                var ok = response.IsSuccessStatusCode;
                if (!ok)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    var preview = err.Length > 400 ? err.Substring(0, 400) + "…" : err;
                    _logger.LogWarning("Figma file validation failed: {Status} {Body}", response.StatusCode, preview);
                }

                return ok;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating Figma file {FileKey}", fileKey);
                return false;
            }
        }

        /// <summary>
        /// Get user's Figma structure (teams/projects/files). Requires <c>projects:read</c> for project endpoints;
        /// public OAuth apps do not grant that scope — expect failures or empty data; file-level features still work with granular file scopes.
        /// </summary>
        private async Task<List<FigmaTeamStructure>?> GetUserFigmaStructure(string accessToken)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();

                // Try both authentication methods
                _httpClient.DefaultRequestHeaders.Add("X-Figma-Token", accessToken);
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                // Try to get user info first to see what scopes we have
                _logger.LogInformation("Making request to Figma API with token: {Token}", accessToken.Substring(0, Math.Min(10, accessToken.Length)) + "...");
                
                // First try to get user info
                var meResponse = await _httpClient.GetAsync("https://api.figma.com/v1/me");
                var meContent = await meResponse.Content.ReadAsStringAsync();
                _logger.LogInformation("User info response - Status: {StatusCode}, Content: {Content}", meResponse.StatusCode, meContent);
                
                if (!meResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get user info from Figma API: {StatusCode} - {Content}", meResponse.StatusCode, meContent);
                    
                    // If we can't get user info, try a different approach - use a known file key
                    _logger.LogInformation("Trying alternative approach with known file key");
                    
                    // For now, return a simple structure indicating the user needs to re-authenticate
                    return new List<FigmaTeamStructure>
                    {
                        new FigmaTeamStructure
                        {
                            Id = "re-auth-required",
                            Name = "Re-authentication Required",
                            Projects = new List<FigmaProjectStructure>
                            {
                                new FigmaProjectStructure
                                {
                                    Id = "re-auth-project",
                                    Name = "Please re-authenticate to access your Figma files",
                                    Files = new List<FigmaFileInfo>()
                                }
                            }
                        }
                    };
                }

                var userInfo = JsonSerializer.Deserialize<FigmaUserInfo>(meContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("User info - ID: {UserId}, Email: {Email}", userInfo?.Id, userInfo?.Email);

                // If we have user info, try to get teams using the teams endpoint
                _logger.LogInformation("User authentication successful, trying to get teams");
                
                var teamsStructure = new List<FigmaTeamStructure>();
                
                // Try to get teams directly using /v1/teams endpoint
                _logger.LogInformation("Attempting to call /v1/teams endpoint with current token");
                _logger.LogInformation("Request headers: {Headers}", string.Join(", ", _httpClient.DefaultRequestHeaders.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));
                
                var teamsResponse = await _httpClient.GetAsync("https://api.figma.com/v1/teams");
                var teamsContent = await teamsResponse.Content.ReadAsStringAsync();
                
                _logger.LogInformation("Teams response - Status: {StatusCode}, Content: {Content}", teamsResponse.StatusCode, teamsContent);
                _logger.LogInformation("Response headers: {Headers}", string.Join(", ", teamsResponse.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));
                
                if (teamsResponse.IsSuccessStatusCode)
                {
                    var teams = JsonSerializer.Deserialize<FigmaTeamsResponse>(teamsContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    _logger.LogInformation("Found {TeamsCount} teams", teams?.Teams?.Count ?? 0);

                    if (teams?.Teams != null)
                    {
                        foreach (var team in teams.Teams)
                        {
                            _logger.LogInformation("Processing team {TeamId} ({TeamName})", team.Id, team.Name);
                            
                            var teamStructure = new FigmaTeamStructure
                            {
                                Id = team.Id,
                                Name = team.Name,
                                Projects = new List<FigmaProjectStructure>()
                            };

                            // Get projects for each team
                            var projectsResponse = await _httpClient.GetAsync($"https://api.figma.com/v1/teams/{team.Id}/projects");
                            var projectsContent = await projectsResponse.Content.ReadAsStringAsync();
                            _logger.LogInformation("Projects response for team {TeamId} - Status: {StatusCode}, Content: {Content}",
                                team.Id, projectsResponse.StatusCode, projectsContent);

                            if (projectsResponse.IsSuccessStatusCode)
                            {
                                var projects = JsonSerializer.Deserialize<FigmaProjectsResponse>(projectsContent, new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                });

                                _logger.LogInformation("Found {ProjectsCount} projects in team {TeamId}", projects?.Projects?.Count ?? 0, team.Id);

                                if (projects?.Projects != null)
                                {
                                    foreach (var project in projects.Projects)
                                    {
                                        var projectStructure = new FigmaProjectStructure
                                        {
                                            Id = project.Id,
                                            Name = project.Name,
                                            Files = new List<FigmaFileInfo>()
                                        };

                                        // Get files for each project
                                        var projectFilesResponse = await _httpClient.GetAsync($"https://api.figma.com/v1/projects/{project.Id}/files");
                                        var projectFilesContent = await projectFilesResponse.Content.ReadAsStringAsync();
                                        _logger.LogInformation("Files response for project {ProjectId} - Status: {StatusCode}, Content: {Content}",
                                            project.Id, projectFilesResponse.StatusCode, projectFilesContent);

                                        if (projectFilesResponse.IsSuccessStatusCode)
                                        {
                                            var projectFiles = JsonSerializer.Deserialize<FigmaFilesResponse>(projectFilesContent, new JsonSerializerOptions
                                            {
                                                PropertyNameCaseInsensitive = true
                                            });

                                            _logger.LogInformation("Found {FilesCount} files in project {ProjectId}", projectFiles?.Files?.Count ?? 0, project.Id);

                                            if (projectFiles?.Files != null)
                                            {
                                                projectStructure.Files = projectFiles.Files;
                                            }
                                        }

                                        teamStructure.Projects.Add(projectStructure);
                                    }
                                }
                            }

                            teamsStructure.Add(teamStructure);
                        }
                    }

                    return teamsStructure;
                }
                else
                {
                    _logger.LogError("Failed to get teams from Figma API: {StatusCode} - {Content}", teamsResponse.StatusCode, teamsContent);
                    
                    // Return re-authentication message if teams endpoint fails
                    return new List<FigmaTeamStructure>
                    {
                        new FigmaTeamStructure
                        {
                            Id = "re-auth-required",
                            Name = "Re-authentication Required",
                            Projects = new List<FigmaProjectStructure>
                            {
                                new FigmaProjectStructure
                                {
                                    Id = "re-auth-project",
                                    Name = "Please re-authenticate with updated scopes to access your Figma files",
                                    Files = new List<FigmaFileInfo>()
                                }
                            }
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Figma structure");
                return null;
            }
        }

        /// <summary>
        /// Figma URLs use <c>node-id=1-2</c>; REST API expects <c>1:2</c>. Replace every hyphen with a colon.
        /// See <see href="https://developers.figma.com/docs/plugins/api/properties/nodes-id/">Figma node id</see>.
        /// </summary>
        private static string NormalizeFigmaNodeId(string raw)
        {
            raw = raw.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                return raw;
            }

            // Normalize fancy dashes sometimes pasted from URLs
            raw = raw.Replace('\u2011', '-').Replace('\u2010', '-').Replace('\u2013', '-').Replace('\u2014', '-');

            if (raw.Contains(':', StringComparison.Ordinal))
            {
                return raw;
            }

            return raw.Replace('-', ':');
        }

        private static string? TryExtractNodeIdFromFigmaUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return null;
            }

            var q = QueryHelpers.ParseQuery(uri.Query);
            if (q.TryGetValue("node-id", out var v) && !StringValues.IsNullOrEmpty(v))
            {
                return NormalizeFigmaNodeId(v.ToString());
            }

            if (q.TryGetValue("node_id", out var v2) && !StringValues.IsNullOrEmpty(v2))
            {
                return NormalizeFigmaNodeId(v2.ToString());
            }

            return null;
        }

        private async Task<FigmaPngUrlResult> RequestFigmaPngExportUrl(string accessToken, string fileKey, string nodeId, double scale)
        {
            var maxExtraRetries = Math.Clamp(_configuration.GetValue("Figma:PngExport429ExtraRetries", 3), 0, 10);
            var baseDelayMs = Math.Clamp(_configuration.GetValue("Figma:PngExport429BaseDelayMs", 2000), 200, 60_000);
            var maxAttempts = maxExtraRetries + 1;

            _logger.LogInformation(
                "Figma images API: FileKey={FileKey} NodeId={NodeId} Scale={Scale} AccessToken={Access} Png429ExtraRetries={ExtraRetries}",
                fileKey,
                nodeId,
                scale,
                DescribeSecret(accessToken),
                maxExtraRetries);

            var scaleStr = scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var requestUri =
                $"https://api.figma.com/v1/images/{Uri.EscapeDataString(fileKey)}" +
                $"?ids={Uri.EscapeDataString(nodeId)}&format=png&scale={Uri.EscapeDataString(scaleStr)}";

            try
            {
                for (var attempt = 0; attempt < maxAttempts; attempt++)
                {
                    HttpResponseMessage response;
                    string content;
                    try
                    {
                        _httpClient.DefaultRequestHeaders.Clear();
                        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                        response = await _httpClient.GetAsync(requestUri);
                        content = await response.Content.ReadAsStringAsync();
                    }
                    catch (TaskCanceledException ex)
                    {
                        _logger.LogWarning(ex, "Figma images API timeout for file {FileKey}", fileKey);
                        return new FigmaPngUrlResult(null, "Request to Figma timed out. Try again.", 504, null, ClientStatusToErrorCode(504));
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogWarning(ex, "Figma images API network error for file {FileKey}", fileKey);
                        return new FigmaPngUrlResult(null, "Could not reach Figma. Check connectivity and try again.", 502, null, ClientStatusToErrorCode(502));
                    }

                    var retryAfterSec = TryGetRetryAfterSeconds(response);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < maxAttempts - 1)
                    {
                        var waitMs = Compute429BackoffMs(retryAfterSec, baseDelayMs, attempt);
                        _logger.LogWarning(
                            "Figma images API 429, attempt {Attempt}/{Max}; waiting {WaitMs}ms. RetryAfterSeconds={RetryAfter}; Headers: {Headers}; Body: {Body}",
                            attempt + 1,
                            maxAttempts,
                            waitMs,
                            retryAfterSec,
                            FormatFigmaResponseHeadersForLog(response),
                            content);
                        await Task.Delay(waitMs);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.TooManyRequests)
                        {
                            _logger.LogWarning(
                                "Figma images API failed: {Status}. RetryAfterSeconds={RetryAfter}; Headers: {Headers}; Body: {Body}",
                                response.StatusCode,
                                retryAfterSec,
                                FormatFigmaResponseHeadersForLog(response),
                                content.Length > 4000 ? content[..4000] + "…" : content);
                        }
                        else
                            _logger.LogWarning("Figma images API failed: {Status} {Body}", response.StatusCode, content);
                        var clientStatus = MapFigmaImagesHttpStatusToClient(response.StatusCode);
                        var detail = string.IsNullOrWhiteSpace(content)
                            ? $"HTTP {(int)response.StatusCode}"
                            : content.Trim();

                        if (clientStatus == 429)
                        {
                            var platformPat = _configuration["Figma:ImageExportPersonalAccessToken"]?.Trim();
                            if (!string.IsNullOrEmpty(platformPat))
                            {
                                try
                                {
                                    _logger.LogInformation(
                                        "Figma images 429 on user OAuth token; trying platform PAT. " +
                                        "File must be accessible to that account. " +
                                        "Note: for files in Starter, Figma still applies Starter Tier-1 caps (~6/month) even for an Enterprise PAT — host project files on your paid Org/Enterprise team to get per-minute limits.");
                                    _httpClient.DefaultRequestHeaders.Clear();
                                    _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Figma-Token", platformPat);
                                    using var patResponse = await _httpClient.GetAsync(requestUri);
                                    var patContent = await patResponse.Content.ReadAsStringAsync();
                                    if (patResponse.IsSuccessStatusCode)
                                    {
                                        var fromPat = BuildResultFromFigmaImagesJson(patContent, nodeId, fileKey);
                                        if (fromPat.ImageUrl != null)
                                        {
                                            return fromPat;
                                        }

                                        _logger.LogWarning("Platform PAT returned OK but no image URL: {Body}", patContent);
                                    }
                                    else
                                    {
                                        if (patResponse.StatusCode == HttpStatusCode.TooManyRequests)
                                        {
                                            var patRetry = TryGetRetryAfterSeconds(patResponse);
                                            _logger.LogWarning(
                                                "Platform PAT Figma images request 429. RetryAfterSeconds={RetryAfter}; Headers: {Headers}; Body: {Body}",
                                                patRetry,
                                                FormatFigmaResponseHeadersForLog(patResponse),
                                                patContent.Length > 2000 ? patContent[..2000] + "…" : patContent);
                                        }
                                        else
                                        {
                                            _logger.LogWarning(
                                                "Platform PAT Figma images request failed: {Status} {Body}",
                                                patResponse.StatusCode,
                                                patContent.Length > 500 ? patContent.Substring(0, 500) + "…" : patContent);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Platform PAT Figma images fallback failed");
                                }
                            }
                        }

                        var human = clientStatus switch
                        {
                            401 => "Figma rejected the access token. Reconnect Figma for this board.",
                            403 => "Figma denied access to this file for the connected account.",
                            404 => "Figma file or resource was not found. Check the file key and link.",
                            429 when retryAfterSec is int r429 && r429 > 3600 =>
                                $"Figma rate limit exceeded. For this account/API tier Figma reports a long cooldown of about {r429 / 86400.0:F1} days (Retry-After seconds until quota may reset).",
                            429 => "Figma rate limit exceeded after automatic retries. Try again later.",
                            502 => "Figma had a temporary server error. Try again later.",
                            _ => $"Figma images API error ({(int)response.StatusCode})."
                        };
                        var message = clientStatus == 400
                            ? $"Figma images API error: {(int)response.StatusCode} - {detail}"
                            : $"{human} ({detail})";
                        var clientRetry = clientStatus == 429 ? ToClientRetryAfterSeconds(retryAfterSec) : null;
                        return new FigmaPngUrlResult(
                            null,
                            message,
                            clientStatus,
                            clientRetry,
                            ClientStatusToErrorCode(clientStatus),
                            clientStatus == 429 ? retryAfterSec : null);
                    }

                    return BuildResultFromFigmaImagesJson(content, nodeId, fileKey);
                }

                throw new InvalidOperationException("Figma images export loop exited without result.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Figma images API for file {FileKey}", fileKey);
                return new FigmaPngUrlResult(null, "An unexpected error occurred while calling Figma.", 500, null, ClientStatusToErrorCode(500));
            }
        }

        private FigmaPngUrlResult BuildResultFromFigmaImagesJson(string content, string nodeId, string fileKey)
        {
            FigmaImagesApiResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<FigmaImagesApiResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Figma images JSON parse failed for file {FileKey}", fileKey);
                return new FigmaPngUrlResult(null, "Invalid response from Figma images API.", 400, null, ClientStatusToErrorCode(400));
            }

            if (parsed == null)
            {
                return new FigmaPngUrlResult(null, "Invalid response from Figma images API.", 400, null, ClientStatusToErrorCode(400));
            }

            if (!string.IsNullOrEmpty(parsed.Err))
            {
                return new FigmaPngUrlResult(null, parsed.Err, 400, null, ClientStatusToErrorCode(400));
            }

            if (parsed.Images == null || parsed.Images.Count == 0)
            {
                return new FigmaPngUrlResult(null, "Figma returned no images.", 400, null, ClientStatusToErrorCode(400));
            }

            string? url;
            if (!parsed.Images.TryGetValue(nodeId, out url) || string.IsNullOrEmpty(url))
            {
                url = parsed.Images.Values.FirstOrDefault(u => !string.IsNullOrEmpty(u));
            }

            return new FigmaPngUrlResult(url, null, null, null, null);
        }

        /// <summary>
        /// Figma REST <c>GET /v1/files/:key</c> — OAuth access tokens must use <c>Authorization: Bearer</c> (not <c>X-Figma-Token</c>).
        /// When <paramref name="ids"/> is set (comma-separated node ids), Figma returns only those subtrees — required for selection links to avoid multi‑100MB responses.
        /// <paramref name="depth"/> maps to Figma’s <c>depth</c> query param (limits tree depth; smaller payloads).
        /// </summary>
        /// <returns>Metadata JSON on success; on failure <paramref name="metadata"/> is null and <paramref name="figmaHttpStatus"/> / <paramref name="figmaErrorBody"/> describe Figma’s response or transport error. <paramref name="failureExtras"/> is set for 429 responses.</returns>
        private async Task<(object? metadata, int figmaHttpStatus, string? figmaErrorBody, FigmaFilesApiFailureExtras? failureExtras)> GetFigmaFileMetadata(string accessToken, string fileKey, string? ids = null, int? depth = null)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var path = $"https://api.figma.com/v1/files/{Uri.EscapeDataString(fileKey)}";
                var url = path;
                if (!string.IsNullOrEmpty(ids))
                    url = QueryHelpers.AddQueryString(url, "ids", ids);
                if (depth is > 0)
                    url = QueryHelpers.AddQueryString(url, "depth", depth.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));

                using var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();
                var status = (int)response.StatusCode;

                if (!response.IsSuccessStatusCode)
                {
                    var extras = BuildFigmaFilesApiFailureExtras(response, status);
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        var retryAfterSec = TryGetRetryAfterSeconds(response);
                        _logger.LogWarning(
                            "Figma GET /v1/files 429 TooManyRequests. RetryAfterSeconds={RetryAfter}; Headers: {Headers}; Body: {Body}",
                            retryAfterSec,
                            FormatFigmaResponseHeadersForLog(response),
                            content);
                    }
                    else
                    {
                        var preview = content.Length > 500 ? content.Substring(0, 500) + "…" : content;
                        _logger.LogWarning("Figma GET /v1/files failed: {Status} {Body}", response.StatusCode, preview);
                    }

                    return (null, status, content, extras);
                }

                try
                {
                    var doc = JsonSerializer.Deserialize<object>(content);
                    return (doc, status, null, null);
                }
                catch (JsonException jex)
                {
                    _logger.LogWarning(jex, "Figma GET /v1/files returned 200 but body is not valid JSON for key {FileKey}", fileKey);
                    return (null, status, content.Length > 2000 ? content[..2000] + "…" : content, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calling Figma GET /v1/files for key {FileKey}", fileKey);
                return (null, 0, ex.Message, null);
            }
        }
    }

    // Request models for the new endpoints
    public class StoreTokensRequest
    {
        public string AuthCode { get; set; } = string.Empty;
        /// <summary>When set, tokens are stored on the board (normal flow).</summary>
        public string? BoardId { get; set; }
        /// <summary>During registration (no board yet); normalized server-side.</summary>
        public string? Email { get; set; }
        /// <summary>Must match the redirect URI used in the authorize step (must be in <c>Figma:RedirectUrls</c>).</summary>
        public string? RedirectUri { get; set; }
    }

    public class ApplyPendingFigmaRequest
    {
        public string Email { get; set; } = string.Empty;
        public string BoardId { get; set; } = string.Empty;
    }

    public class SetPublicFileRequest
    {
        public string BoardId { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
    }

    public class SetFigmaUrlRequest
    {
        public string BoardId { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public class DownloadMetadataRequest
    {
        public string BoardId { get; set; } = string.Empty;

        /// <summary>Required. Figma URL with <c>?node-id=…</c> or <c>node_id=…</c> (e.g. “Copy link to selection”). Required for this endpoint.</summary>
        public string FigmaFileUrl { get; set; } = string.Empty;

        /// <summary>Optional. Figma <c>GET /v1/files … &amp;depth=</c> — limits how many levels deep to traverse under the selected node(s). Use ~4–12 to shrink huge frames while keeping hierarchy.</summary>
        public int? Depth { get; set; }
    }

    public class FetchPublicFileRequest
    {
        public string FileUrl { get; set; } = string.Empty;
    }

    public class ExchangeCodeFetchRequest
    {
        public string AuthCode { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string? RedirectUri { get; set; }
    }

    public class ExportPngRequest
    {
        public string BoardId { get; set; } = string.Empty;
        /// <summary>Optional; when omitted, uses <see cref="Figma.FigmaFileUrl"/> for this board.</summary>
        public string? FileUrl { get; set; }
        /// <summary>Figma node id (e.g. <c>1:23</c> or <c>1-23</c>). Optional if the effective URL contains <c>node-id</c>.</summary>
        public string? NodeId { get; set; }
        /// <summary>Export scale; Figma allows positive values (often 1–4). Default 1.</summary>
        public double? Scale { get; set; }
    }

    public class FigmaImagesApiResponse
    {
        [JsonPropertyName("err")]
        public string? Err { get; set; }

        [JsonPropertyName("images")]
        public Dictionary<string, string?>? Images { get; set; }
    }

    public class FigmaTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
        
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;
        
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
        
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
    }

    public class FigmaFilesResponse
    {
        [JsonPropertyName("files")]
        public List<FigmaFileInfo> Files { get; set; } = new List<FigmaFileInfo>();
    }

    public class FigmaFileInfo
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("thumbnail_url")]
        public string ThumbnailUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("last_modified")]
        public string LastModified { get; set; } = string.Empty;
        
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    public class FigmaTeamsResponse
    {
        [JsonPropertyName("teams")]
        public List<FigmaTeam> Teams { get; set; } = new List<FigmaTeam>();
    }

    public class FigmaTeam
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class FigmaProjectsResponse
    {
        [JsonPropertyName("projects")]
        public List<FigmaProject> Projects { get; set; } = new List<FigmaProject>();
    }

    public class FigmaProject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class FigmaTeamStructure
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<FigmaProjectStructure> Projects { get; set; } = new List<FigmaProjectStructure>();
    }

    public class FigmaProjectStructure
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<FigmaFileInfo> Files { get; set; } = new List<FigmaFileInfo>();
    }

    public class FigmaUserInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
        
        [JsonPropertyName("handle")]
        public string Handle { get; set; } = string.Empty;
        
        [JsonPropertyName("img_url")]
        public string ImgUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("teams")]
        public List<FigmaTeam> Teams { get; set; } = new List<FigmaTeam>();
    }

    public class FigmaRecentFilesResponse
    {
        [JsonPropertyName("files")]
        public List<FigmaRecentFile> Files { get; set; } = new List<FigmaRecentFile>();
    }

    public class FigmaRecentFile
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("thumbnail_url")]
        public string ThumbnailUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("last_modified")]
        public string LastModified { get; set; } = string.Empty;
        
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
        
        [JsonPropertyName("team_id")]
        public string TeamId { get; set; } = string.Empty;
        
        [JsonPropertyName("team_name")]
        public string TeamName { get; set; } = string.Empty;
    }
}
