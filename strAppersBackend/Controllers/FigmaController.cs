using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;

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
        /// Generate OAuth URL for Figma authentication
        /// </summary>
        [HttpGet("use/oauth")]
        public ActionResult<object> GetOAuthUrl()
        {
            try
            {
                var clientId = _configuration["Figma:ClientId"];
                var redirectUrls = _configuration.GetSection("Figma:RedirectUrls").Get<string[]>();
                
                if (string.IsNullOrEmpty(clientId) || redirectUrls == null || redirectUrls.Length == 0)
                {
                    return BadRequest("Figma configuration is missing");
                }

                // Generate unique state for security
                var state = Guid.NewGuid().ToString();
                
                // Use the first redirect URL (or you could make this configurable)
                var redirectUri = redirectUrls[0];
                
                var oauthUrl = $"https://www.figma.com/oauth?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=file_content:read,current_user:read,projects:read,team_library_content:read,file_metadata:read,file_versions:read,library_assets:read,library_content:read&state={state}&response_type=code";

                _logger.LogInformation("Generated OAuth URL for Figma authentication");
                _logger.LogInformation("Requested scopes: file_content:read,current_user:read,projects:read,team_library_content:read,file_metadata:read,file_versions:read,library_assets:read,library_content:read (file_variables:read and library_analytics:read removed - Enterprise only)");

                return Ok(new
                {
                    success = true,
                    oauthUrl = oauthUrl,
                    state = state,
                    redirectUri = redirectUri
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating OAuth URL");
                return StatusCode(500, "An error occurred while generating OAuth URL");
            }
        }

        /// <summary>
        /// Exchange authorization code for tokens and store them
        /// </summary>
        [HttpPost("use/store-tokens")]
        public async Task<ActionResult<object>> StoreTokens([FromBody] StoreTokensRequest request)
        {
            try
            {
                _logger.LogInformation("Storing Figma tokens for board {BoardId}", request.BoardId);

                var clientId = _configuration["Figma:ClientId"];
                var clientSecret = _configuration["Figma:ClientSecret"];
                var redirectUrls = _configuration.GetSection("Figma:RedirectUrls").Get<string[]>();

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || redirectUrls == null || redirectUrls.Length == 0)
                {
                    return BadRequest("Figma configuration is missing");
                }

                var redirectUri = redirectUrls[0];

                // Exchange authorization code for tokens
                var formData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri),
                    new KeyValuePair<string, string>("code", request.AuthCode),
                    new KeyValuePair<string, string>("grant_type", "authorization_code")
                };

                var content = new FormUrlEncodedContent(formData);

                var response = await _httpClient.PostAsync("https://api.figma.com/v1/oauth/token", content);
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

                _logger.LogInformation("Parsed token response - AccessToken: {AccessToken}, RefreshToken: {RefreshToken}, ExpiresIn: {ExpiresIn}", 
                    tokenResponse?.AccessToken, tokenResponse?.RefreshToken, tokenResponse?.ExpiresIn);
                
                // Log the raw response for debugging
                _logger.LogInformation("Raw Figma OAuth response content: {RawContent}", responseContent);

                if (tokenResponse?.AccessToken == null)
                {
                    _logger.LogError("Invalid token response from Figma: {Response}", responseContent);
                    return BadRequest("Invalid token response from Figma");
                }

                // Check if Figma integration already exists for this board
                var existingFigma = await _context.Figma
                    .FirstOrDefaultAsync(f => f.BoardId == request.BoardId);

                if (existingFigma != null)
                {
                    // Update existing record
                    _logger.LogInformation("Updating existing Figma record for board {BoardId}", request.BoardId);
                    existingFigma.FigmaAccessToken = tokenResponse.AccessToken;
                    existingFigma.FigmaRefreshToken = tokenResponse.RefreshToken;
                    existingFigma.FigmaTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    existingFigma.FigmaLastSync = DateTime.UtcNow;
                    existingFigma.UpdatedAt = DateTime.UtcNow;
                    
                    _logger.LogInformation("Updated Figma record - AccessToken: {AccessToken}, RefreshToken: {RefreshToken}", 
                        existingFigma.FigmaAccessToken, existingFigma.FigmaRefreshToken);
                }
                else
                {
                    // Create new record
                    _logger.LogInformation("Creating new Figma record for board {BoardId}", request.BoardId);
                    var figma = new Figma
                    {
                        BoardId = request.BoardId,
                        FigmaAccessToken = tokenResponse.AccessToken,
                        FigmaRefreshToken = tokenResponse.RefreshToken,
                        FigmaTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                        FigmaLastSync = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _logger.LogInformation("New Figma record - AccessToken: {AccessToken}, RefreshToken: {RefreshToken}", 
                        figma.FigmaAccessToken, figma.FigmaRefreshToken);

                    _context.Figma.Add(figma);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully saved Figma tokens to database for board {BoardId}", request.BoardId);

                _logger.LogInformation("Successfully stored Figma tokens for board {BoardId}", request.BoardId);

                return Ok(new
                {
                    success = true,
                    message = "Tokens stored successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing Figma tokens for board {BoardId}", request.BoardId);
                return StatusCode(500, "An error occurred while storing tokens");
            }
        }

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

                // Log token information for debugging
                _logger.LogInformation("Figma token details - AccessToken: {AccessToken}, RefreshToken: {RefreshToken}, Expiry: {Expiry}, LastSync: {LastSync}", 
                    figma.FigmaAccessToken?.Substring(0, Math.Min(10, figma.FigmaAccessToken.Length)) + "...",
                    figma.FigmaRefreshToken?.Substring(0, Math.Min(10, figma.FigmaRefreshToken.Length)) + "...",
                    figma.FigmaTokenExpiry,
                    figma.FigmaLastSync);

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
        /// Download metadata of the Figma file as JSON
        /// Requires file_content:read scope for Figma API access
        /// </summary>
        [HttpPost("use/download-metadata")]
        public async Task<ActionResult> DownloadMetadata([FromBody] DownloadMetadataRequest request)
        {
            try
            {
                _logger.LogInformation("Downloading metadata for board {BoardId}", request.BoardId);

                // Get Figma integration for this board
                var figma = await _context.Figma
                    .FirstOrDefaultAsync(f => f.BoardId == request.BoardId);

                if (figma == null)
                {
                    return NotFound($"Figma integration not found for board {request.BoardId}");
                }

                if (string.IsNullOrEmpty(figma.FigmaFileKey))
                {
                    return BadRequest("No Figma file linked to this board");
                }

                // Check if access token is expired and refresh if needed
                await RefreshTokenIfNeeded(figma);

                // Fetch file metadata from Figma API
                var metadata = await GetFigmaFileMetadata(figma.FigmaAccessToken!, figma.FigmaFileKey);
                if (metadata == null)
                {
                    return BadRequest("Failed to fetch file metadata from Figma");
                }

                // Update last sync time
                figma.FigmaLastSync = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Return as downloadable JSON file
                var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
                
                return File(jsonBytes, "application/json", $"figma-metadata-{request.BoardId}.json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading metadata for board {BoardId}", request.BoardId);
                return StatusCode(500, "An error occurred while downloading metadata");
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

                var fileKey = ExtractFileKeyFromUrl(request.FileUrl);
                if (string.IsNullOrEmpty(fileKey))
                {
                    return BadRequest("Unable to extract file key from the provided URL.");
                }

                var metadata = await GetFigmaFileMetadata(tokenPayload.AccessToken, fileKey);
                if (metadata == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Failed to fetch file metadata from Figma. Ensure the authenticated user has access to the file."
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

            _logger.LogInformation("Token expired, attempting refresh with refresh token: {RefreshToken}", 
                figma.FigmaRefreshToken.Substring(0, Math.Min(10, figma.FigmaRefreshToken.Length)) + "...");

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

            _logger.LogInformation("Force refreshing token with refresh token: {RefreshToken}", 
                figma.FigmaRefreshToken.Substring(0, Math.Min(10, figma.FigmaRefreshToken.Length)) + "...");

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
                _httpClient.DefaultRequestHeaders.Add("X-Figma-Token", accessToken);
                
                _logger.LogInformation("Validating Figma file with key: {FileKey}", fileKey);
                _logger.LogInformation("Request headers: {Headers}", string.Join(", ", _httpClient.DefaultRequestHeaders.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));
                
                var response = await _httpClient.GetAsync($"https://api.figma.com/v1/files/{fileKey}");
                var content = await response.Content.ReadAsStringAsync();
                
                _logger.LogInformation("Figma file validation response - Status: {StatusCode}, Content: {Content}", response.StatusCode, content);
                _logger.LogInformation("Response headers: {Headers}", string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating Figma file {FileKey}", fileKey);
                return false;
            }
        }

        /// <summary>
        /// Get user's Figma structure (teams/projects/files)
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
        /// Get Figma file metadata
        /// </summary>
        private async Task<object?> GetFigmaFileMetadata(string accessToken, string fileKey)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-Figma-Token", accessToken);
                var response = await _httpClient.GetAsync($"https://api.figma.com/v1/files/{fileKey}");
                
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<object>(content);
            }
            catch
            {
                return null;
            }
        }
    }

    // Request models for the new endpoints
    public class StoreTokensRequest
    {
        public string AuthCode { get; set; } = string.Empty;
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
