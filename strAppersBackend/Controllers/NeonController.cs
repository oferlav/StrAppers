using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using strAppersBackend.Data;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]/use")]
public class NeonController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NeonController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public NeonController(
        ApplicationDbContext context,
        ILogger<NeonController> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Get Neon configuration status
    /// </summary>
    [HttpGet("status")]
    public ActionResult<object> GetStatus()
    {
        try
        {
            var apiKey = _configuration["Neon:ApiKey"];
            var baseUrl = _configuration["Neon:BaseUrl"];
            var projectId = _configuration["Neon:ProjectId"];
            var branchId = _configuration["Neon:BranchId"];

            var isConfigured = !string.IsNullOrEmpty(apiKey) && 
                               apiKey != "your-neon-api-key-here" &&
                               !string.IsNullOrEmpty(baseUrl) &&
                               !string.IsNullOrEmpty(projectId) &&
                               !string.IsNullOrEmpty(branchId);

            return Ok(new
            {
                Success = true,
                IsConfigured = isConfigured,
                HasApiKey = !string.IsNullOrEmpty(apiKey) && apiKey != "your-neon-api-key-here",
                BaseUrl = baseUrl ?? "Not configured",
                ProjectId = projectId ?? "Not configured",
                BranchId = branchId ?? "Not configured"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Neon status");
            return StatusCode(500, new { Success = false, Message = $"An error occurred: {ex.Message}" });
        }
    }

    /// <summary>
    /// Create a new database in Neon and return the connection string
    /// </summary>
    /// <param name="dbName">Name of the database to create</param>
    /// <returns>Connection string for the created database</returns>
    [HttpPost("create-database/{dbName}")]
    public async Task<ActionResult<object>> CreateDatabase(string dbName)
    {
        try
        {
            var apiKey = _configuration["Neon:ApiKey"];
            var baseUrl = _configuration["Neon:BaseUrl"];
            var projectId = _configuration["Neon:ProjectId"];
            var branchId = _configuration["Neon:BranchId"];
            var defaultOwnerName = _configuration["Neon:DefaultOwnerName"] ?? "neondb_owner";

            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "your-neon-api-key-here")
            {
                _logger.LogWarning("Neon API key not configured");
                return BadRequest(new { Success = false, Message = "Neon API key is not configured. Please set it in appsettings.json" });
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                _logger.LogWarning("Neon base URL not configured");
                return BadRequest(new { Success = false, Message = "Neon base URL is not configured" });
            }

            if (string.IsNullOrWhiteSpace(projectId) || projectId == "your-neon-project-id-here")
            {
                _logger.LogWarning("Neon project ID not configured");
                return BadRequest(new { Success = false, Message = "Neon project ID is not configured. Please set it in appsettings.json" });
            }

            if (string.IsNullOrWhiteSpace(branchId))
            {
                _logger.LogWarning("Neon branch ID not configured");
                return BadRequest(new { Success = false, Message = "Neon branch ID is not configured. Please set it in appsettings.json" });
            }

            if (string.IsNullOrWhiteSpace(dbName))
            {
                return BadRequest(new { Success = false, Message = "Database name is required" });
            }

            _logger.LogInformation("Creating Neon database: {DbName} in project: {ProjectId}, branch: {BranchId}", dbName, projectId, branchId);

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Create database request body
            var createDbRequest = new
            {
                database = new
                {
                    name = dbName,
                    owner_name = defaultOwnerName
                }
            };

            var requestBody = JsonSerializer.Serialize(createDbRequest);
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            // POST to Neon API: /projects/{project_id}/branches/{branch_id}/databases
            var apiUrl = $"{baseUrl}/projects/{projectId}/branches/{branchId}/databases";
            _logger.LogInformation("Calling Neon API: POST {Url}", apiUrl);

            var response = await httpClient.PostAsync(apiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Neon API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return StatusCode((int)response.StatusCode, new
                {
                    Success = false,
                    Message = $"Failed to create database. Neon API returned: {response.StatusCode}",
                    Error = errorContent
                });
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Neon API response: {Response}", responseContent);

            // Parse the response
            var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            // Extract database information
            string? databaseId = null;
            string? connectionString = null;

            if (root.TryGetProperty("database", out var databaseObj))
            {
                if (databaseObj.TryGetProperty("id", out var idProp))
                {
                    // ID can be either string or number, handle both
                    if (idProp.ValueKind == JsonValueKind.String)
                    {
                        databaseId = idProp.GetString();
                    }
                    else if (idProp.ValueKind == JsonValueKind.Number)
                    {
                        databaseId = idProp.GetInt32().ToString();
                    }
                }
                if (databaseObj.TryGetProperty("name", out var nameProp))
                {
                    // Verify the name matches
                    var returnedName = nameProp.GetString();
                    if (returnedName != dbName)
                    {
                        _logger.LogWarning("Database name mismatch: requested {Requested}, got {Returned}", dbName, returnedName);
                    }
                }
            }

            // Get connection string from Neon API
            // The connection_uri endpoint requires database_name and role_name as query parameters
            try
            {
                var connectionUrl = $"{baseUrl}/projects/{projectId}/connection_uri?database_name={Uri.EscapeDataString(dbName)}&role_name={Uri.EscapeDataString(defaultOwnerName)}&branch_id={Uri.EscapeDataString(branchId)}&pooled=false";
                _logger.LogInformation("Fetching connection string from: {Url}", connectionUrl);
                
                var connResponse = await httpClient.GetAsync(connectionUrl);
                if (connResponse.IsSuccessStatusCode)
                {
                    var connContent = await connResponse.Content.ReadAsStringAsync();
                    _logger.LogInformation("Connection string API response: {Response}", connContent);
                    
                    var connDoc = JsonDocument.Parse(connContent);
                    if (connDoc.RootElement.TryGetProperty("uri", out var uriProp))
                    {
                        connectionString = uriProp.GetString();
                        _logger.LogInformation("Successfully retrieved connection string for database: {DbName}", dbName);
                    }
                }
                else
                {
                    var errorContent = await connResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to get connection string. Status: {StatusCode}, Error: {Error}", connResponse.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve connection string from Neon API. Database created but connection string not available.");
            }

            return Ok(new
            {
                Success = true,
                Message = "Database created successfully",
                DatabaseName = dbName,
                DatabaseId = databaseId,
                ConnectionString = connectionString ?? "Connection string not available. Please retrieve it manually from Neon console.",
                ProjectId = projectId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Neon database: {DbName}", dbName);
            return StatusCode(500, new { Success = false, Message = $"An error occurred: {ex.Message}" });
        }
    }
}

