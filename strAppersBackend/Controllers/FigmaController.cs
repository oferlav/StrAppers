using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using System.Text.Json;
using System.Text;

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
                
                var oauthUrl = $"https://www.figma.com/oauth?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=file_content:read&state={state}&response_type=code";

                _logger.LogInformation("Generated OAuth URL for Figma authentication");

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

                var response = await _httpClient.PostAsync("https://www.figma.com/oauth/token", content);
                var responseContent = await response.Content.ReadAsStringAsync();

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
                    return BadRequest("Invalid token response from Figma");
                }

                // Check if Figma integration already exists for this board
                var existingFigma = await _context.Figma
                    .FirstOrDefaultAsync(f => f.BoardId == request.BoardId);

                if (existingFigma != null)
                {
                    // Update existing record
                    existingFigma.FigmaAccessToken = tokenResponse.AccessToken;
                    existingFigma.FigmaRefreshToken = tokenResponse.RefreshToken;
                    existingFigma.FigmaTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    existingFigma.FigmaLastSync = DateTime.UtcNow;
                    existingFigma.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Create new record
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
                _logger.LogError(ex, "Error storing Figma tokens for board {BoardId}", request.BoardId);
                return StatusCode(500, "An error occurred while storing tokens");
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
                var isValid = await ValidateFigmaFile(figma.FigmaAccessToken!, request.FileKey);
                if (!isValid)
                {
                    return BadRequest("Invalid or inaccessible Figma file");
                }

                // Update the file information
                figma.FigmaFileUrl = request.FileUrl;
                figma.FigmaFileKey = request.FileKey;
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
        /// Refresh access token if expired
        /// </summary>
        private async Task RefreshTokenIfNeeded(Figma figma)
        {
            if (figma.FigmaTokenExpiry.HasValue && figma.FigmaTokenExpiry.Value > DateTime.UtcNow)
            {
                return; // Token is still valid
            }

            if (string.IsNullOrEmpty(figma.FigmaRefreshToken))
            {
                throw new InvalidOperationException("No refresh token available");
            }

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

            var response = await _httpClient.PostAsync("https://www.figma.com/oauth/token", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
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
        /// Validate Figma file accessibility
        /// </summary>
        private async Task<bool> ValidateFigmaFile(string accessToken, string fileKey)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var response = await _httpClient.GetAsync($"https://api.figma.com/v1/files/{fileKey}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get Figma file metadata
        /// </summary>
        private async Task<object?> GetFigmaFileMetadata(string accessToken, string fileKey)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
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
        public string FileKey { get; set; } = string.Empty;
    }

    public class DownloadMetadataRequest
    {
        public string BoardId { get; set; } = string.Empty;
    }

    public class FigmaTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }
}
