using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using strAppersBackend.Data;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]/use")]
public class RailwayController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RailwayController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public RailwayController(
        ApplicationDbContext context,
        ILogger<RailwayController> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Get Railway configuration status
    /// </summary>
    [HttpGet("status")]
    public ActionResult<object> GetStatus()
    {
        try
        {
            var apiToken = _configuration["Railway:ApiToken"];
            var apiUrl = _configuration["Railway:ApiUrl"];

            var isConfigured = !string.IsNullOrEmpty(apiToken) && 
                               apiToken != "your-railway-api-token-here" &&
                               !string.IsNullOrEmpty(apiUrl);

            var maskedToken = apiToken?.Length > 12 
                ? $"{apiToken.Substring(0, 8)}...{apiToken.Substring(apiToken.Length - 4)}" 
                : "***";

            return Ok(new
            {
                Success = true,
                IsConfigured = isConfigured,
                HasApiToken = !string.IsNullOrEmpty(apiToken) && apiToken != "your-railway-api-token-here",
                ApiUrl = apiUrl ?? "Not configured",
                TokenLength = apiToken?.Length ?? 0,
                MaskedToken = maskedToken
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Railway status");
            return StatusCode(500, new { Success = false, Message = $"An error occurred: {ex.Message}" });
        }
    }

    /// <summary>
    /// Test Railway API connectivity with a simple query - tries multiple endpoint variations
    /// </summary>
    [HttpPost("test-connection")]
    public async Task<ActionResult<object>> TestConnection()
    {
        try
        {
            var apiToken = _configuration["Railway:ApiToken"];

            if (string.IsNullOrWhiteSpace(apiToken) || apiToken == "your-railway-api-token-here")
            {
                return BadRequest(new { Success = false, Message = "Railway API token is not configured" });
            }

            var maskedToken = apiToken.Length > 12 
                ? $"{apiToken.Substring(0, 8)}...{apiToken.Substring(apiToken.Length - 4)}" 
                : "***";

            // Try multiple endpoint variations (including correct .com domain)
            var endpointsToTest = new[]
            {
                "https://backboard.railway.com/graphql/v2",  // Known correct endpoint
                "https://backboard.railway.com/graphql",
                "https://backboard.railway.com/graphql/v1",
                "https://backboard.railway.app/graphql",
                "https://backboard.railway.app/graphql/v1",
                "https://backboard.railway.app/graphql/v2",
                "https://api.railway.app/graphql",
                "https://railway.app/api/graphql"
            };

            var results = new List<object>();

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppersBackend/1.0");

            // Try a simple GraphQL introspection query
            var testQuery = new
            {
                query = "{ __typename }"
            };

            var requestBody = JsonSerializer.Serialize(testQuery);

            foreach (var endpoint in endpointsToTest)
            {
                try
                {
                    var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                    
                    _logger.LogInformation("Testing Railway API endpoint: {Endpoint}", endpoint);
                    
                    var response = await httpClient.PostAsync(endpoint, content);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    var result = new
                    {
                        Endpoint = endpoint,
                        Success = response.IsSuccessStatusCode,
                        StatusCode = (int)response.StatusCode,
                        StatusText = response.StatusCode.ToString(),
                        ResponseContent = responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent
                    };

                    results.Add(result);
                    
                    _logger.LogInformation("Endpoint {Endpoint} - Status: {StatusCode}, Success: {Success}", 
                        endpoint, response.StatusCode, response.IsSuccessStatusCode);

                    if (response.IsSuccessStatusCode)
                    {
                        // Found working endpoint!
                        return Ok(new
                        {
                            Success = true,
                            WorkingEndpoint = endpoint,
                            TestResults = results,
                            Message = $"Found working endpoint: {endpoint}"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error testing endpoint: {Endpoint}", endpoint);
                    results.Add(new
                    {
                        Endpoint = endpoint,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            // None worked
            return Ok(new
            {
                Success = false,
                Message = "All endpoint variations failed. Please check Railway API documentation.",
                TestResults = results,
                RecommendedAction = "Check Railway API documentation for the correct GraphQL endpoint URL"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Railway connection");
            return StatusCode(500, new { Success = false, Message = $"An error occurred: {ex.Message}", StackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// Generate a web API host on Railway and return the publish-to URL
    /// </summary>
    /// <param name="name">Name for the Railway project/service</param>
    /// <returns>Public URL where the API can be published/deployed</returns>
    [HttpPost("create-host/{name}")]
    public async Task<ActionResult<object>> CreateHost(string name)
    {
        try
        {
            var apiToken = _configuration["Railway:ApiToken"];
            var apiUrl = _configuration["Railway:ApiUrl"];

            if (string.IsNullOrWhiteSpace(apiToken) || apiToken == "your-railway-api-token-here")
            {
                _logger.LogWarning("Railway API token not configured");
                return BadRequest(new { Success = false, Message = "Railway API token is not configured. Please set it in appsettings.json" });
            }

            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                _logger.LogWarning("Railway API URL not configured");
                return BadRequest(new { Success = false, Message = "Railway API URL is not configured" });
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { Success = false, Message = "Name is required" });
            }

            // Sanitize name for Railway (alphanumeric, hyphens, underscores only)
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9-_]", "-");

            // Mask API token for logging (show first 8 and last 4 characters)
            var maskedToken = apiToken?.Length > 12 
                ? $"{apiToken.Substring(0, 8)}...{apiToken.Substring(apiToken.Length - 4)}" 
                : "***";

            _logger.LogInformation("Creating Railway host: {Name} (sanitized: {SanitizedName})", name, sanitizedName);
            _logger.LogDebug("Railway API Configuration - URL: {ApiUrl}, Token: {MaskedToken} (Length: {TokenLength})", 
                apiUrl, maskedToken, apiToken?.Length ?? 0);

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppersBackend/1.0");

            // Railway uses GraphQL API
            // Create a project with the given name
            var createProjectMutation = new
            {
                query = @"
                    mutation CreateProject($name: String!) {
                        projectCreate(input: { name: $name }) {
                            id
                            name
                        }
                    }",
                variables = new
                {
                    name = sanitizedName
                }
            };

            var requestBody = JsonSerializer.Serialize(createProjectMutation);
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling Railway GraphQL API: POST {Url}", apiUrl);
            _logger.LogDebug("Request Headers - Authorization: Bearer {MaskedToken}, Content-Type: {ContentType}", 
                maskedToken, content.Headers.ContentType?.MediaType);
            _logger.LogDebug("Request Body: {RequestBody}", requestBody);

            var response = await httpClient.PostAsync(apiUrl, content);

            // Log response details
            _logger.LogDebug("Response Status: {StatusCode} {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
            _logger.LogDebug("Response Headers: {Headers}", 
                string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));

            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Railway API error: {StatusCode} {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
                _logger.LogError("Error Response Headers: {Headers}", 
                    string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));
                _logger.LogError("Error Response Body: {ErrorContent}", responseContent);
                _logger.LogError("Request Details - URL: {Url}, Method: POST, Auth Header Present: {HasAuth}, Content-Type: {ContentType}", 
                    apiUrl, !string.IsNullOrEmpty(apiToken), content.Headers.ContentType?.MediaType);
                
                return StatusCode((int)response.StatusCode, new
                {
                    Success = false,
                    Message = $"Failed to create Railway project. API returned: {response.StatusCode} {response.ReasonPhrase}",
                    Error = responseContent,
                    RequestUrl = apiUrl,
                    HasAuthToken = !string.IsNullOrEmpty(apiToken),
                    MaskedToken = maskedToken
                });
            }

            _logger.LogInformation("Railway API response received successfully");
            _logger.LogDebug("Response Content: {ResponseContent}", responseContent);

            // Parse the GraphQL response
            var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            string? projectId = null;
            string? projectName = null;
            string? serviceId = null;
            string? publicUrl = null;

            // Extract project information
            if (root.TryGetProperty("data", out var dataObj))
            {
                if (dataObj.TryGetProperty("projectCreate", out var projectObj))
                {
                    if (projectObj.TryGetProperty("id", out var idProp))
                    {
                        projectId = idProp.GetString();
                    }
                    if (projectObj.TryGetProperty("name", out var nameProp))
                    {
                        projectName = nameProp.GetString();
                    }
                }

                // Check for errors
                if (root.TryGetProperty("errors", out var errorsProp))
                {
                    var errors = errorsProp.EnumerateArray().Select(e => e.GetRawText()).ToList();
                    _logger.LogWarning("Railway GraphQL errors: {Errors}", string.Join(", ", errors));
                    
                    // Try to extract error message
                    if (errors.Any())
                    {
                        var firstError = JsonDocument.Parse(errors[0]);
                        var errorMessage = firstError.RootElement.TryGetProperty("message", out var msgProp) 
                            ? msgProp.GetString() 
                            : "Unknown error";
                        
                        return BadRequest(new
                        {
                            Success = false,
                            Message = $"Failed to create Railway project: {errorMessage}",
                            Errors = errors
                        });
                    }
                }
            }

            if (string.IsNullOrEmpty(projectId))
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Failed to create Railway project. Project ID not returned."
                });
            }

            // Create a service within the project
            // Railway will automatically create a public URL for the service
            var createServiceMutation = new
            {
                query = @"
                    mutation CreateService($projectId: String!, $name: String) {
                        serviceCreate(input: { projectId: $projectId, name: $name }) {
                            id
                            name
                            url
                        }
                    }",
                variables = new
                {
                    projectId = projectId,
                    name = sanitizedName
                }
            };

            var serviceRequestBody = JsonSerializer.Serialize(createServiceMutation);
            var serviceContent = new StringContent(serviceRequestBody, Encoding.UTF8, "application/json");

            var serviceResponse = await httpClient.PostAsync(apiUrl, serviceContent);

            if (serviceResponse.IsSuccessStatusCode)
            {
                var serviceResponseContent = await serviceResponse.Content.ReadAsStringAsync();
                _logger.LogInformation("Railway service creation response: {Response}", serviceResponseContent);

                var serviceJsonDoc = JsonDocument.Parse(serviceResponseContent);
                var serviceRoot = serviceJsonDoc.RootElement;

                if (serviceRoot.TryGetProperty("data", out var serviceDataObj))
                {
                    if (serviceDataObj.TryGetProperty("serviceCreate", out var serviceObj))
                    {
                        if (serviceObj.TryGetProperty("id", out var serviceIdProp))
                        {
                            serviceId = serviceIdProp.GetString();
                        }
                        if (serviceObj.TryGetProperty("url", out var urlProp))
                        {
                            publicUrl = urlProp.GetString();
                        }
                    }
                }
            }
            else
            {
                _logger.LogWarning("Failed to create Railway service, but project was created. Service can be created manually in Railway console.");
            }

            // If we don't have a public URL yet, construct the Railway project URL
            // Users can deploy to this project and Railway will generate the public URL
            if (string.IsNullOrEmpty(publicUrl))
            {
                publicUrl = $"https://railway.app/project/{projectId}";
            }

            return Ok(new
            {
                Success = true,
                Message = "Railway host created successfully",
                ProjectId = projectId,
                ProjectName = projectName ?? sanitizedName,
                ServiceId = serviceId,
                PublishUrl = publicUrl,
                DeploymentUrl = publicUrl, // Same URL for publishing
                Instructions = new
                {
                    Step1 = "Deploy your API code to this Railway project",
                    Step2 = "Use Railway CLI: railway link",
                    Step3 = "Deploy: railway up",
                    Step4 = "Your API will be available at the generated public URL with /swagger for Swagger UI"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Railway host: {Name}", name);
            return StatusCode(500, new { Success = false, Message = $"An error occurred: {ex.Message}" });
        }
    }
}




