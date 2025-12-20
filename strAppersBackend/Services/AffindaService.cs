using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using strAppersBackend.Models;
using System.Net.Http.Headers;

namespace strAppersBackend.Services;

public interface IAffindaService
{
    Task<ParseResumeResponse> ParseResumeAsync(string fileBase64);
    Task<List<AffindaWorkspace>> GetWorkspacesAsync();
}

public class AffindaService : IAffindaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AffindaService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public AffindaService(HttpClient httpClient, ILogger<AffindaService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        
        _apiKey = _configuration["Affinda:ApiKey"] ?? throw new InvalidOperationException("Affinda API key not configured");
        _baseUrl = _configuration["Affinda:BaseUrl"] ?? "https://api.affinda.com/v3";
        
        // Set up HTTP client headers
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Resume parsing can take time
    }

    public async Task<ParseResumeResponse> ParseResumeAsync(string fileBase64)
    {
        try
        {
            _logger.LogInformation("Starting resume parsing via Affinda API");
            
            if (string.IsNullOrWhiteSpace(fileBase64))
            {
                _logger.LogWarning("FileBase64 is null or empty");
                throw new ArgumentException("FileBase64 cannot be null or empty", nameof(fileBase64));
            }

            // Prepare the request body for Affinda API
            // Affinda API v3 expects multipart/form-data with the file
            // Convert base64 to bytes and send as multipart
            byte[] fileBytes;
            try
            {
                // Remove data URL prefix if present (e.g., "data:application/pdf;base64,")
                var base64String = fileBase64;
                if (base64String.Contains(","))
                {
                    base64String = base64String.Split(',')[1];
                }
                fileBytes = Convert.FromBase64String(base64String);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Invalid base64 string format");
                throw new ArgumentException("Invalid base64 string format", nameof(fileBase64), ex);
            }

            // Create multipart form data content
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            
            // Determine content type based on file signature or default to PDF
            string contentType = "application/pdf";
            if (fileBytes.Length > 0)
            {
                // Check for DOCX signature (PK header - ZIP format)
                if (fileBytes[0] == 0x50 && fileBytes[1] == 0x4B)
                {
                    contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                }
            }
            
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "file", "resume.pdf");
            
            // Workspace is required by Affinda API
            var workspaceId = _configuration["Affinda:WorkspaceId"];
            if (string.IsNullOrWhiteSpace(workspaceId))
            {
                _logger.LogError("Affinda WorkspaceId is not configured. Please set Affinda:WorkspaceId in configuration.");
                throw new InvalidOperationException("Affinda WorkspaceId is required but not configured. Please set Affinda:WorkspaceId in appsettings.json or Azure App Settings.");
            }
            
            content.Add(new StringContent(workspaceId), "workspace");

            // Affinda API v3: Use /documents endpoint with documentType parameter for resume parsing
            // The documentType parameter should be the Document Type ID from Affinda configuration
            var documentType = _configuration["Affinda:Extractor"] ?? "BoQeGyDI";
            content.Add(new StringContent(documentType), "documentType");
            
            string endpoint = $"{_baseUrl}/documents";
            
            _logger.LogInformation("Calling Affinda API: POST {Endpoint}", endpoint);
            var response = await _httpClient.PostAsync(endpoint, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Affinda API response status: {StatusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Affinda API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new HttpRequestException($"Affinda API error: {response.StatusCode} - {responseContent}");
            }

            // Extract identifier and ready status from raw JSON immediately
            string? documentIdentifier = null;
            bool isReady = false;
            
            // Wrap everything in try-catch to ensure we catch any exceptions
            try
            {
                // Log the raw response for debugging (first 2000 chars to avoid logging huge responses)
                var responsePreview = responseContent.Length > 2000 ? responseContent.Substring(0, 2000) + "..." : responseContent;
                _logger.LogInformation("Affinda API raw response preview: {Response}", responsePreview);
                
                _logger.LogInformation("STEP 1: Starting identifier extraction from raw JSON...");
                try
                {
                    _logger.LogInformation("STEP 2: Deserializing JSON element...");
                    var rawJson = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    _logger.LogInformation("STEP 3: JSON deserialized, looking for meta property...");
                    
                    if (rawJson.TryGetProperty("meta", out var metaElement))
                    {
                        _logger.LogInformation("STEP 4: Found meta property, extracting identifier...");
                        if (metaElement.TryGetProperty("identifier", out var identifierElement))
                        {
                            documentIdentifier = identifierElement.GetString();
                            _logger.LogInformation("✓ Extracted identifier: {Identifier}", documentIdentifier);
                        }
                        else
                        {
                            _logger.LogWarning("meta.identifier property not found");
                        }
                        
                        _logger.LogInformation("STEP 5: Extracting ready status...");
                        if (metaElement.TryGetProperty("ready", out var readyElement))
                        {
                            isReady = readyElement.GetBoolean();
                            _logger.LogInformation("✓ Document ready status: {Ready}", isReady);
                        }
                        else
                        {
                            _logger.LogWarning("meta.ready property not found");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("meta property not found in JSON response");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract identifier from raw JSON: {Message}, StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                }
                
                _logger.LogInformation("STEP 6: Identifier extraction complete. Identifier: {Identifier}, Ready: {Ready}", documentIdentifier ?? "null", isReady);
                
                // If we have identifier and document is ready, fetch it immediately
                if (!string.IsNullOrWhiteSpace(documentIdentifier) && isReady)
                {
                    _logger.LogInformation("STEP 7: Document is ready. Fetching parsed data for identifier: {Identifier}", documentIdentifier);
                    return await FetchAndParseDocumentAsync(documentIdentifier, responseContent);
                }
                
                _logger.LogWarning("STEP 8: Cannot fetch document - Identifier: {Identifier}, Ready: {Ready}", documentIdentifier ?? "null", isReady);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CRITICAL ERROR in response processing: {Message}, StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                // Return raw response on any error
                try
                {
                    var rawJson = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    return new ParseResumeResponse
                    {
                        RawAffindaResponse = rawJson
                    };
                }
                catch
                {
                    throw;
                }
            }
            
            // If we got here and still don't have identifier/ready, try fallback deserialization approach
            if (string.IsNullOrWhiteSpace(documentIdentifier))
            {
                _logger.LogInformation("Attempting deserialization approach as fallback...");

                // Parse the Affinda response
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                AffindaResumeResponse? affindaResponse;
                try
                {
                    _logger.LogInformation("Deserializing Affinda response...");
                    affindaResponse = JsonSerializer.Deserialize<AffindaResumeResponse>(responseContent, jsonOptions);
                    _logger.LogInformation("Deserialization complete. Response is null: {IsNull}", affindaResponse == null);
                    
                    if (affindaResponse != null && affindaResponse.Meta != null)
                    {
                        documentIdentifier = affindaResponse.Meta.Identifier;
                        isReady = affindaResponse.Meta.Ready;
                        _logger.LogInformation("Got identifier from deserialized Meta: {Identifier}, Ready: {Ready}", documentIdentifier, isReady);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize Affinda response: {Message}", ex.Message);
                }
            }
            
            // Final check - if we have identifier and ready, fetch it
            if (!string.IsNullOrWhiteSpace(documentIdentifier) && isReady)
            {
                _logger.LogInformation("Final attempt: Fetching parsed data for identifier: {Identifier}", documentIdentifier);
                return await FetchAndParseDocumentAsync(documentIdentifier, responseContent);
            }
            
            // If we still don't have what we need, return raw response
            _logger.LogWarning("Unable to fetch document. Returning raw response. Identifier: {Identifier}, Ready: {Ready}", 
                documentIdentifier ?? "null", isReady);
            try
            {
                var rawJson = JsonSerializer.Deserialize<JsonElement>(responseContent);
                return new ParseResumeResponse
                {
                    RawAffindaResponse = rawJson
                };
            }
            catch
            {
                throw new InvalidOperationException("Failed to process Affinda API response");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing resume via Affinda API: {Message}", ex.Message);
            throw;
        }
    }

    private async Task<ParseResumeResponse> FetchAndParseDocumentAsync(string documentIdentifier, string originalResponseContent)
    {
        try
        {
            _logger.LogInformation("Fetching document: GET {BaseUrl}/documents/{Identifier}", _baseUrl, documentIdentifier);
            var documentResponse = await _httpClient.GetAsync($"{_baseUrl}/documents/{documentIdentifier}");
            var documentResponseContent = await documentResponse.Content.ReadAsStringAsync();

            _logger.LogInformation("Document fetch response status: {StatusCode}", documentResponse.StatusCode);

            if (!documentResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch document: {StatusCode} - {Content}", 
                    documentResponse.StatusCode, documentResponseContent.Substring(0, Math.Min(500, documentResponseContent.Length)));
                // Return original response if fetch fails
                try
                {
                    var rawJson = JsonSerializer.Deserialize<JsonElement>(originalResponseContent);
                    return new ParseResumeResponse
                    {
                        RawAffindaResponse = rawJson
                    };
                }
                catch
                {
                    throw new HttpRequestException($"Failed to fetch document: {documentResponse.StatusCode}");
                }
            }

            _logger.LogInformation("Successfully fetched document. Parsing response...");
            
            // Parse the fetched document response
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            AffindaResumeResponse? documentData;
            try
            {
                documentData = JsonSerializer.Deserialize<AffindaResumeResponse>(documentResponseContent, jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize fetched document response");
                // Return raw response
                try
                {
                    var rawJson = JsonSerializer.Deserialize<JsonElement>(documentResponseContent);
                    return new ParseResumeResponse
                    {
                        RawAffindaResponse = rawJson
                    };
                }
                catch
                {
                    throw new InvalidOperationException($"Failed to deserialize fetched document: {ex.Message}");
                }
            }

            // Map the fetched document data to our custom DTO
            var parseResponse = MapAffindaResponseToParseResponse(documentData ?? new AffindaResumeResponse(), documentResponseContent);

            _logger.LogInformation("Successfully parsed resume. Candidate: {FirstName} {LastName}", 
                parseResponse.CandidateMetaData?.FirstName, parseResponse.CandidateMetaData?.LastName);

            return parseResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching and parsing document: {Message}", ex.Message);
            // Return original response on error
            try
            {
                var rawJson = JsonSerializer.Deserialize<JsonElement>(originalResponseContent);
                return new ParseResumeResponse
                {
                    RawAffindaResponse = rawJson
                };
            }
            catch
            {
                throw;
            }
        }
    }

    private ParseResumeResponse MapAffindaResponseToParseResponse(AffindaResumeResponse affindaResponse, string rawResponseContent)
    {
        try
        {
            // Parse raw response to access the new structure
            JsonElement rawJson;
            try
            {
                rawJson = JsonSerializer.Deserialize<JsonElement>(rawResponseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize raw response JSON");
                throw;
            }
            
            // Extract data element - new structure has data directly in response.data
            JsonElement? dataElement = null;
            if (rawJson.TryGetProperty("data", out var dataProp))
            {
                dataElement = dataProp;
            }
            else if (affindaResponse.Data.HasValue)
            {
                dataElement = affindaResponse.Data.Value;
            }

            if (!dataElement.HasValue)
            {
                _logger.LogWarning("No data element found in Affinda response");
                return new ParseResumeResponse
                {
                    RawAffindaResponse = rawJson
                };
            }

            var data = dataElement.Value;
            _logger.LogDebug("Successfully extracted data element from Affinda response");

            // Initialize variables with defaults
            string? firstName = null;
            string? lastName = null;
            string? email = null;
            string? phone = null;
            string? location = null;
            string? linkedInUrl = null;
            string? jobTitle = null;
            int? totalYearsExperience = null;
            var skills = new List<string>();
            string? educationLevel = null;

            try
            {
            // Extract candidate name - check parsed structure
            if (data.TryGetProperty("candidateName", out var candidateNameElement) && candidateNameElement.ValueKind != JsonValueKind.Null)
            {
                if (candidateNameElement.TryGetProperty("parsed", out var nameParsed))
                {
                    if (nameParsed.ValueKind == JsonValueKind.Object)
                    {
                        if (nameParsed.TryGetProperty("first", out var firstElement))
                            firstName = firstElement.GetString();
                        if (nameParsed.TryGetProperty("last", out var lastElement))
                            lastName = lastElement.GetString();
                    }
                }
            }

            // Extract email (array with parsed field) - trim trailing pipes that Affinda sometimes includes
            if (data.TryGetProperty("email", out var emailArray) && emailArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var emailItem in emailArray.EnumerateArray())
                {
                    if (emailItem.TryGetProperty("parsed", out var emailParsed))
                    {
                        email = emailParsed.GetString()?.TrimEnd('|').Trim();
                        break; // Take first email
                    }
                }
            }

            // Extract phone number (array with parsed field)
            if (data.TryGetProperty("phoneNumber", out var phoneArray) && phoneArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var phoneItem in phoneArray.EnumerateArray())
                {
                    if (phoneItem.TryGetProperty("parsed", out var phoneParsed))
                    {
                        if (phoneParsed.ValueKind == JsonValueKind.String)
                        {
                            phone = phoneParsed.GetString();
                        }
                        else if (phoneParsed.ValueKind == JsonValueKind.Object)
                        {
                            // Try formattedNumber first, then nationalNumber, then rawText
                            if (phoneParsed.TryGetProperty("formattedNumber", out var formatted))
                                phone = formatted.GetString();
                            else if (phoneParsed.TryGetProperty("nationalNumber", out var national))
                                phone = national.GetString();
                            else if (phoneParsed.TryGetProperty("rawText", out var rawText))
                                phone = rawText.GetString();
                        }
                        break; // Take first phone
                    }
                }
            }

            // Extract location - check both direct location and from work experience
            if (data.TryGetProperty("location", out var locationElement) && locationElement.ValueKind != JsonValueKind.Null)
            {
                if (locationElement.TryGetProperty("parsed", out var locationParsed))
                {
                    if (locationParsed.ValueKind == JsonValueKind.Object)
                    {
                        if (locationParsed.TryGetProperty("formatted", out var formattedLoc))
                            location = formattedLoc.GetString();
                        else if (locationParsed.TryGetProperty("rawInput", out var rawInputLoc))
                            location = rawInputLoc.GetString();
                        else if (locationParsed.TryGetProperty("city", out var city) && locationParsed.TryGetProperty("country", out var country))
                        {
                            var cityStr = city.GetString();
                            var countryStr = country.GetString();
                            if (!string.IsNullOrWhiteSpace(cityStr) && !string.IsNullOrWhiteSpace(countryStr))
                                location = $"{cityStr}, {countryStr}";
                        }
                    }
                }
            }
            
            // If location not found, try to get from most recent work experience
            if (string.IsNullOrWhiteSpace(location))
            {
                if (data.TryGetProperty("workExperience", out var workExpArrayForLoc) && workExpArrayForLoc.ValueKind == JsonValueKind.Array)
                {
                    foreach (var workExpItem in workExpArrayForLoc.EnumerateArray())
                    {
                        if (workExpItem.TryGetProperty("parsed", out var workExpParsed))
                        {
                            if (workExpParsed.TryGetProperty("workExperienceLocation", out var workLocElement) && workLocElement.ValueKind != JsonValueKind.Null)
                            {
                                if (workLocElement.TryGetProperty("parsed", out var workLocParsed) && workLocParsed.ValueKind == JsonValueKind.Object)
                                {
                                    // Try formatted first (but only if it's not null)
                                    if (workLocParsed.TryGetProperty("formatted", out var formattedWorkLoc) && formattedWorkLoc.ValueKind != JsonValueKind.Null)
                                    {
                                        var formattedLocStr = formattedWorkLoc.GetString();
                                        if (!string.IsNullOrWhiteSpace(formattedLocStr))
                                        {
                                            location = formattedLocStr;
                                            break; // Found a good location
                                        }
                                    }
                                    
                                    // Then try city + country combination
                                    if (string.IsNullOrWhiteSpace(location))
                                    {
                                        string? workCityStr = null;
                                        string? workCountryStr = null;
                                        
                                        if (workLocParsed.TryGetProperty("city", out var workCity) && workCity.ValueKind == JsonValueKind.String)
                                        {
                                            workCityStr = workCity.GetString();
                                        }
                                        
                                        if (workLocParsed.TryGetProperty("country", out var workCountry) && workCountry.ValueKind == JsonValueKind.String)
                                        {
                                            workCountryStr = workCountry.GetString();
                                        }
                                        
                                        if (!string.IsNullOrWhiteSpace(workCityStr) && !string.IsNullOrWhiteSpace(workCountryStr))
                                        {
                                            location = $"{workCityStr}, {workCountryStr}";
                                            break; // Found a good location
                                        }
                                    }
                                    
                                    // Fallback to rawInput if available (but filter out parenthetical values)
                                    if (string.IsNullOrWhiteSpace(location))
                                    {
                                        if (workLocParsed.TryGetProperty("rawInput", out var rawInputWorkLoc) && rawInputWorkLoc.ValueKind == JsonValueKind.String)
                                        {
                                            var rawInputStr = rawInputWorkLoc.GetString();
                                            if (!string.IsNullOrWhiteSpace(rawInputStr) && 
                                                !rawInputStr.StartsWith("(") && 
                                                !rawInputStr.EndsWith(")") &&
                                                rawInputStr.Length > 3) // Must be meaningful
                                            {
                                                location = rawInputStr;
                                                break; // Found a good location
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Extract LinkedIn URL from websites - check if website is array or single object
            if (data.TryGetProperty("website", out var websiteElement) && websiteElement.ValueKind != JsonValueKind.Null)
            {
                // Check if website is an array
                if (websiteElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var websiteItem in websiteElement.EnumerateArray())
                    {
                        if (websiteItem.TryGetProperty("parsed", out var websiteParsed))
                        {
                            var websiteStr = websiteParsed.GetString();
                            if (!string.IsNullOrWhiteSpace(websiteStr) && websiteStr.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase))
                            {
                                linkedInUrl = websiteStr;
                                break;
                            }
                        }
                    }
                }
                // Check if website is a single object
                else if (websiteElement.ValueKind == JsonValueKind.Object)
                {
                    if (websiteElement.TryGetProperty("parsed", out var websiteParsed))
                    {
                        var websiteStr = websiteParsed.GetString();
                        if (!string.IsNullOrWhiteSpace(websiteStr) && websiteStr.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase))
                            linkedInUrl = websiteStr;
                    }
                }
            }

            // Extract skills (array with parsed.name field)
            if (data.TryGetProperty("skill", out var skillArray) && skillArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var skillItem in skillArray.EnumerateArray())
                {
                    try
                    {
                        if (skillItem.TryGetProperty("parsed", out var skillParsed) && skillParsed.ValueKind == JsonValueKind.Object)
                        {
                            if (skillParsed.TryGetProperty("name", out var skillName) && skillName.ValueKind == JsonValueKind.String)
                            {
                                var skillNameStr = skillName.GetString();
                                if (!string.IsNullOrWhiteSpace(skillNameStr) && !skills.Contains(skillNameStr, StringComparer.OrdinalIgnoreCase))
                                    skills.Add(skillNameStr);
                            }
                        }
                    }
                    catch (Exception skillEx)
                    {
                        _logger.LogWarning(skillEx, "Error extracting skill item");
                    }
                }
            }

            // Extract total years of experience
            if (data.TryGetProperty("totalYearsExperience", out var expElement) && expElement.ValueKind != JsonValueKind.Null)
            {
                try
                {
                    if (expElement.TryGetProperty("parsed", out var expParsed))
                    {
                        if (expParsed.ValueKind == JsonValueKind.Number)
                        {
                            var expValue = expParsed.GetDouble();
                            totalYearsExperience = (int)Math.Round(expValue);
                        }
                        else if (expParsed.ValueKind == JsonValueKind.String)
                        {
                            // Sometimes it's a string representation
                            if (double.TryParse(expParsed.GetString(), out var expValue))
                            {
                                totalYearsExperience = (int)Math.Round(expValue);
                            }
                        }
                    }
                }
                catch (Exception expEx)
                {
                    _logger.LogWarning(expEx, "Error extracting total years of experience");
                }
            }

            // Extract job title - first try profession field, then work experience
            
            // Check for profession field at top level
            if (data.TryGetProperty("profession", out var professionElement) && professionElement.ValueKind != JsonValueKind.Null)
            {
                if (professionElement.TryGetProperty("parsed", out var professionParsed))
                {
                    jobTitle = professionParsed.GetString();
                }
                else if (professionElement.ValueKind == JsonValueKind.String)
                {
                    jobTitle = professionElement.GetString();
                }
            }
            
            // If no profession, get from most recent work experience
            if (string.IsNullOrWhiteSpace(jobTitle))
            {
                if (data.TryGetProperty("workExperience", out var workExpArray) && workExpArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var workExpItem in workExpArray.EnumerateArray())
                    {
                        if (workExpItem.TryGetProperty("parsed", out var workExpParsed))
                        {
                            if (workExpParsed.TryGetProperty("workExperienceJobTitle", out var jobTitleElement))
                            {
                                // Get both raw and parsed to compare
                                string? rawTitle = null;
                                string? parsedTitle = null;
                                
                                if (jobTitleElement.TryGetProperty("raw", out var jobTitleRaw))
                                {
                                    rawTitle = jobTitleRaw.GetString();
                                }
                                
                                if (jobTitleElement.TryGetProperty("parsed", out var jobTitleParsed))
                                {
                                    parsedTitle = jobTitleParsed.GetString();
                                }
                                
                                // Use the shorter one if both exist, or whichever is available
                                string? titleToUse = null;
                                if (!string.IsNullOrWhiteSpace(rawTitle) && !string.IsNullOrWhiteSpace(parsedTitle))
                                {
                                    // If they're the same, use either
                                    if (rawTitle.Equals(parsedTitle, StringComparison.OrdinalIgnoreCase))
                                    {
                                        titleToUse = rawTitle;
                                    }
                                    else
                                    {
                                        // Use the shorter one (likely more accurate)
                                        titleToUse = rawTitle.Length <= parsedTitle.Length ? rawTitle : parsedTitle;
                                    }
                                }
                                else if (!string.IsNullOrWhiteSpace(rawTitle))
                                {
                                    titleToUse = rawTitle;
                                }
                                else if (!string.IsNullOrWhiteSpace(parsedTitle))
                                {
                                    titleToUse = parsedTitle;
                                }
                                
                                // If title is very long (>80 chars), try to extract just the job title part
                                // (sometimes it includes company description before the title)
                                if (!string.IsNullOrWhiteSpace(titleToUse) && titleToUse.Length > 80)
                                {
                                    // Common job title keywords to find where the actual title starts
                                    // Order matters - check more specific titles first
                                    var jobTitleKeywords = new[] { "Director", "Manager", "Engineer", "Consultant", "Developer", "Analyst", "Specialist", "Coordinator", "Lead", "Senior", "Principal", "Architect", "Executive", "Head", "Chief" };
                                    
                                    int bestIndex = -1;
                                    string? bestKeyword = null;
                                    
                                    // Find the last occurrence of any keyword (most likely the actual title)
                                    foreach (var keyword in jobTitleKeywords)
                                    {
                                        var keywordIndex = titleToUse.LastIndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                                        if (keywordIndex > bestIndex && keywordIndex >= 0)
                                        {
                                            bestIndex = keywordIndex;
                                            bestKeyword = keyword;
                                        }
                                    }
                                    
                                    if (bestIndex >= 0 && bestKeyword != null)
                                    {
                                        // Extract from keyword to end, but limit to reasonable length
                                        var extractedTitle = titleToUse.Substring(bestIndex).Trim();
                                        // Clean up: remove trailing commas, periods, and extra spaces
                                        extractedTitle = extractedTitle.TrimEnd(',', '.', ' ').Trim();
                                        
                                        if (extractedTitle.Length > 5 && extractedTitle.Length < 200)
                                        {
                                            jobTitle = extractedTitle;
                                        }
                                        else
                                        {
                                            jobTitle = titleToUse;
                                        }
                                    }
                                    else
                                    {
                                        // If no keyword found, try to find a comma and take the part after the last comma
                                        var lastCommaIndex = titleToUse.LastIndexOf(',');
                                        if (lastCommaIndex > 0 && lastCommaIndex < titleToUse.Length - 5)
                                        {
                                            var afterComma = titleToUse.Substring(lastCommaIndex + 1).Trim();
                                            if (afterComma.Length > 5 && afterComma.Length < 100)
                                            {
                                                jobTitle = afterComma;
                                            }
                                            else
                                            {
                                                jobTitle = titleToUse;
                                            }
                                        }
                                        else
                                        {
                                            jobTitle = titleToUse;
                                        }
                                    }
                                }
                                else if (!string.IsNullOrWhiteSpace(titleToUse))
                                {
                                    jobTitle = titleToUse;
                                }
                                
                                if (!string.IsNullOrWhiteSpace(jobTitle))
                                    break; // Take first (most recent) job title
                            }
                        }
                    }
                }
            }

            // Extract education level from education entries
            // Check both educationLevel and educationAccreditation fields
            if (data.TryGetProperty("education", out var educationArray) && educationArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var eduItem in educationArray.EnumerateArray())
                {
                    if (eduItem.TryGetProperty("parsed", out var eduParsed))
                    {
                        // First try educationLevel field
                        if (eduParsed.TryGetProperty("educationLevel", out var eduLevelElement))
                        {
                            if (eduLevelElement.TryGetProperty("parsed", out var eduLevelParsed))
                            {
                                if (eduLevelParsed.ValueKind == JsonValueKind.Object)
                                {
                                    if (eduLevelParsed.TryGetProperty("value", out var eduLevelValue))
                                    {
                                        var levelStr = eduLevelValue.GetString();
                                        if (!string.IsNullOrWhiteSpace(levelStr) && !levelStr.Equals("Course/Certificate", StringComparison.OrdinalIgnoreCase))
                                        {
                                            educationLevel = levelStr;
                                            break; // Take first non-certificate education level
                                        }
                                    }
                                }
                                else if (eduLevelParsed.ValueKind == JsonValueKind.String)
                                {
                                    var levelStr = eduLevelParsed.GetString();
                                    if (!string.IsNullOrWhiteSpace(levelStr) && !levelStr.Equals("Course/Certificate", StringComparison.OrdinalIgnoreCase))
                                    {
                                        educationLevel = levelStr;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        // If no valid educationLevel found, check educationAccreditation for degree name
                        // This might contain "BA Computer Science & Management" etc.
                        if (string.IsNullOrWhiteSpace(educationLevel) && eduParsed.TryGetProperty("educationAccreditation", out var eduAccredElement))
                        {
                            if (eduAccredElement.TryGetProperty("parsed", out var eduAccredParsed))
                            {
                                var accredStr = eduAccredParsed.GetString();
                                // Check if it looks like a degree (contains BA, BS, MA, MS, PhD, Bachelor, Master, etc.)
                                if (!string.IsNullOrWhiteSpace(accredStr) && 
                                    (accredStr.Contains("BA", StringComparison.OrdinalIgnoreCase) ||
                                     accredStr.Contains("BS", StringComparison.OrdinalIgnoreCase) ||
                                     accredStr.Contains("B.A.", StringComparison.OrdinalIgnoreCase) ||
                                     accredStr.Contains("B.S.", StringComparison.OrdinalIgnoreCase) ||
                                     accredStr.Contains("MA", StringComparison.OrdinalIgnoreCase) ||
                                     accredStr.Contains("MS", StringComparison.OrdinalIgnoreCase) ||
                                     accredStr.Contains("M.A.", StringComparison.OrdinalIgnoreCase) ||
                                     accredStr.Contains("M.S.", StringComparison.OrdinalIgnoreCase) ||
                                     accredStr.Contains("PhD", StringComparison.OrdinalIgnoreCase) ||
                                     accredStr.Contains("Ph.D.", StringComparison.OrdinalIgnoreCase) ||
                                     accredStr.Contains("Bachelor", StringComparison.OrdinalIgnoreCase) ||
                                     accredStr.Contains("Master", StringComparison.OrdinalIgnoreCase) ||
                                     accredStr.Contains("Doctorate", StringComparison.OrdinalIgnoreCase) ||
                                     accredStr.Contains("Degree", StringComparison.OrdinalIgnoreCase)))
                                {
                                    educationLevel = accredStr;
                                    break;
                                }
                            }
                        }
                        
                        // Also check educationMajor field which might contain degree information
                        if (string.IsNullOrWhiteSpace(educationLevel) && eduParsed.TryGetProperty("educationMajor", out var eduMajorElement))
                        {
                            if (eduMajorElement.TryGetProperty("parsed", out var eduMajorParsed))
                            {
                                var majorStr = eduMajorParsed.GetString();
                                if (!string.IsNullOrWhiteSpace(majorStr))
                                {
                                    educationLevel = majorStr;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            }
            catch (Exception extractionEx)
            {
                _logger.LogError(extractionEx, "Error during data extraction: {Message}. StackTrace: {StackTrace}", extractionEx.Message, extractionEx.StackTrace);
                // Continue with whatever we've extracted so far
            }

            // NEW: Extract additional candidate metadata fields
            string? dateOfBirth = null;
            string? headshot = null;
            string? nationality = null;
            string? availability = null;
            string? preferredWorkLocation = null;
            bool? willingToRelocate = null;
            string? rightToWork = null;
            string? objective = null;
            string? summary = null;
            string? expectedSalary = null;
            var educationDetails = new List<EducationDetail>();
            var workExperienceDetails = new List<WorkExperienceDetail>();
            var projects = new List<string>();
            var achievements = new List<string>();
            var associations = new List<string>();
            var patents = new List<string>();
            var publications = new List<string>();
            var hobbies = new List<string>();
            var referees = new List<string>();
            var languages = new List<LanguageDetail>();

            try
            {
                if (data.TryGetProperty("dateOfBirth", out var dobElement) && dobElement.ValueKind != JsonValueKind.Null)
                {
                    if (dobElement.TryGetProperty("parsed", out var dobParsed))
                    {
                        if (dobParsed.ValueKind == JsonValueKind.Object && dobParsed.TryGetProperty("date", out var dobDate))
                            dateOfBirth = dobDate.GetString();
                        else if (dobParsed.ValueKind == JsonValueKind.String)
                            dateOfBirth = dobParsed.GetString();
                    }
                }

                if (data.TryGetProperty("headshot", out var headshotElement) && headshotElement.ValueKind != JsonValueKind.Null)
                {
                    if (headshotElement.TryGetProperty("parsed", out var headshotParsed))
                        headshot = headshotParsed.GetString();
                }

                if (data.TryGetProperty("nationality", out var nationalityElement) && nationalityElement.ValueKind != JsonValueKind.Null)
                {
                    if (nationalityElement.TryGetProperty("parsed", out var nationalityParsed))
                        nationality = nationalityParsed.GetString();
                }

                if (data.TryGetProperty("availability", out var availabilityElement) && availabilityElement.ValueKind != JsonValueKind.Null)
                {
                    if (availabilityElement.TryGetProperty("parsed", out var availabilityParsed))
                    {
                        if (availabilityParsed.ValueKind == JsonValueKind.Object && availabilityParsed.TryGetProperty("value", out var availValue))
                            availability = availValue.GetString();
                        else if (availabilityParsed.ValueKind == JsonValueKind.String)
                            availability = availabilityParsed.GetString();
                    }
                }

                if (data.TryGetProperty("preferredWorkLocation", out var prefLocElement) && prefLocElement.ValueKind != JsonValueKind.Null)
                {
                    if (prefLocElement.TryGetProperty("parsed", out var prefLocParsed))
                    {
                        if (prefLocParsed.ValueKind == JsonValueKind.Object && prefLocParsed.TryGetProperty("formatted", out var prefFormatted))
                            preferredWorkLocation = prefFormatted.GetString();
                        else if (prefLocParsed.ValueKind == JsonValueKind.String)
                            preferredWorkLocation = prefLocParsed.GetString();
                    }
                }

                if (data.TryGetProperty("willingToRelocate", out var relocateElement) && relocateElement.ValueKind != JsonValueKind.Null)
                {
                    if (relocateElement.TryGetProperty("parsed", out var relocateParsed))
                    {
                        if (relocateParsed.ValueKind == JsonValueKind.True || relocateParsed.ValueKind == JsonValueKind.False)
                            willingToRelocate = relocateParsed.GetBoolean();
                        else if (relocateParsed.ValueKind == JsonValueKind.String)
                        {
                            var relocateStr = relocateParsed.GetString();
                            willingToRelocate = relocateStr != null && 
                                              (relocateStr.Equals("yes", StringComparison.OrdinalIgnoreCase) || 
                                               relocateStr.Equals("true", StringComparison.OrdinalIgnoreCase));
                        }
                    }
                }

                if (data.TryGetProperty("rightToWork", out var rightToWorkElement) && rightToWorkElement.ValueKind != JsonValueKind.Null)
                {
                    if (rightToWorkElement.TryGetProperty("parsed", out var rightToWorkParsed))
                    {
                        if (rightToWorkParsed.ValueKind == JsonValueKind.Object && rightToWorkParsed.TryGetProperty("value", out var rightValue))
                            rightToWork = rightValue.GetString();
                        else if (rightToWorkParsed.ValueKind == JsonValueKind.String)
                            rightToWork = rightToWorkParsed.GetString();
                    }
                }

                // NEW: Extract professional content fields
                if (data.TryGetProperty("objective", out var objectiveElement) && objectiveElement.ValueKind != JsonValueKind.Null)
                {
                    if (objectiveElement.TryGetProperty("parsed", out var objectiveParsed))
                        objective = objectiveParsed.GetString();
                }

                if (data.TryGetProperty("summary", out var summaryElement) && summaryElement.ValueKind != JsonValueKind.Null)
                {
                    if (summaryElement.TryGetProperty("parsed", out var summaryParsed))
                        summary = summaryParsed.GetString();
                }

                if (data.TryGetProperty("expectedSalary", out var salaryElement) && salaryElement.ValueKind != JsonValueKind.Null)
                {
                    if (salaryElement.TryGetProperty("parsed", out var salaryParsed))
                    {
                        if (salaryParsed.ValueKind == JsonValueKind.Object)
                        {
                            if (salaryParsed.TryGetProperty("formatted", out var salaryFormatted))
                                expectedSalary = salaryFormatted.GetString();
                            else if (salaryParsed.TryGetProperty("amount", out var salaryAmount))
                                expectedSalary = salaryAmount.GetString();
                        }
                        else if (salaryParsed.ValueKind == JsonValueKind.String)
                            expectedSalary = salaryParsed.GetString();
                    }
                }

                // NEW: Extract full education details
                if (data.TryGetProperty("education", out var educationArrayFull) && educationArrayFull.ValueKind == JsonValueKind.Array)
                {
                    foreach (var eduItem in educationArrayFull.EnumerateArray())
                    {
                        try
                        {
                            if (eduItem.TryGetProperty("parsed", out var eduParsedFull))
                            {
                                var eduDetail = new EducationDetail();

                                if (eduParsedFull.TryGetProperty("educationOrganization", out var orgElement))
                                {
                                    if (orgElement.TryGetProperty("parsed", out var orgParsed))
                                        eduDetail.Organization = orgParsed.GetString();
                                }

                                if (eduParsedFull.TryGetProperty("educationLevel", out var levelElement))
                                {
                                    if (levelElement.TryGetProperty("parsed", out var levelParsed))
                                    {
                                        if (levelParsed.ValueKind == JsonValueKind.Object && levelParsed.TryGetProperty("value", out var levelValue))
                                            eduDetail.Level = levelValue.GetString();
                                        else if (levelParsed.ValueKind == JsonValueKind.String)
                                            eduDetail.Level = levelParsed.GetString();
                                    }
                                }

                                if (eduParsedFull.TryGetProperty("educationMajor", out var majorElement))
                                {
                                    if (majorElement.TryGetProperty("parsed", out var majorParsed))
                                        eduDetail.Major = majorParsed.GetString();
                                }

                                if (eduParsedFull.TryGetProperty("educationMinor", out var minorElement))
                                {
                                    if (minorElement.TryGetProperty("parsed", out var minorParsed))
                                        eduDetail.Minor = minorParsed.GetString();
                                }

                                if (eduParsedFull.TryGetProperty("educationLocation", out var eduLocElement))
                                {
                                    if (eduLocElement.TryGetProperty("parsed", out var eduLocParsed))
                                    {
                                        if (eduLocParsed.ValueKind == JsonValueKind.Object && eduLocParsed.TryGetProperty("formatted", out var eduLocFormatted))
                                            eduDetail.Location = eduLocFormatted.GetString();
                                        else if (eduLocParsed.ValueKind == JsonValueKind.String)
                                            eduDetail.Location = eduLocParsed.GetString();
                                    }
                                }

                                if (eduParsedFull.TryGetProperty("educationDates", out var eduDatesElement))
                                {
                                    if (eduDatesElement.TryGetProperty("parsed", out var eduDatesParsed) && eduDatesParsed.ValueKind == JsonValueKind.Object)
                                    {
                                        if (eduDatesParsed.TryGetProperty("start", out var startDate) && startDate.ValueKind == JsonValueKind.Object && startDate.TryGetProperty("date", out var startDateStr))
                                            eduDetail.StartDate = startDateStr.GetString();
                                        if (eduDatesParsed.TryGetProperty("end", out var endDate) && endDate.ValueKind == JsonValueKind.Object && endDate.TryGetProperty("date", out var endDateStr))
                                            eduDetail.EndDate = endDateStr.GetString();
                                    }
                                }

                                if (eduParsedFull.TryGetProperty("educationGrade", out var gradeElement))
                                {
                                    if (gradeElement.TryGetProperty("parsed", out var gradeParsed))
                                        eduDetail.Grade = gradeParsed.GetString();
                                }

                                if (eduParsedFull.TryGetProperty("educationAccreditation", out var accredElement))
                                {
                                    if (accredElement.TryGetProperty("parsed", out var accredParsed))
                                        eduDetail.Accreditation = accredParsed.GetString();
                                }

                                educationDetails.Add(eduDetail);
                            }
                        }
                        catch (Exception eduEx)
                        {
                            _logger.LogWarning(eduEx, "Error extracting education detail");
                        }
                    }
                }

                // NEW: Extract full work experience details
                if (data.TryGetProperty("workExperience", out var workExpArrayFull) && workExpArrayFull.ValueKind == JsonValueKind.Array)
                {
                    foreach (var workExpItem in workExpArrayFull.EnumerateArray())
                    {
                        try
                        {
                            if (workExpItem.TryGetProperty("parsed", out var workExpParsedFull))
                            {
                                var workExpDetail = new WorkExperienceDetail();

                                if (workExpParsedFull.TryGetProperty("workExperienceJobTitle", out var jobTitleElement))
                                {
                                    if (jobTitleElement.TryGetProperty("parsed", out var jobTitleParsed))
                                        workExpDetail.JobTitle = jobTitleParsed.GetString();
                                    else if (jobTitleElement.TryGetProperty("raw", out var jobTitleRaw))
                                        workExpDetail.JobTitle = jobTitleRaw.GetString();
                                }

                                if (workExpParsedFull.TryGetProperty("workExperienceOrganization", out var orgElement))
                                {
                                    if (orgElement.TryGetProperty("parsed", out var orgParsed))
                                        workExpDetail.Organization = orgParsed.GetString();
                                }

                                if (workExpParsedFull.TryGetProperty("workExperienceLocation", out var workLocElement))
                                {
                                    if (workLocElement.TryGetProperty("parsed", out var workLocParsed))
                                    {
                                        if (workLocParsed.ValueKind == JsonValueKind.Object && workLocParsed.TryGetProperty("formatted", out var workLocFormatted))
                                            workExpDetail.Location = workLocFormatted.GetString();
                                        else if (workLocParsed.ValueKind == JsonValueKind.String)
                                            workExpDetail.Location = workLocParsed.GetString();
                                    }
                                }

                                if (workExpParsedFull.TryGetProperty("workExperienceDates", out var workDatesElement))
                                {
                                    if (workDatesElement.TryGetProperty("parsed", out var workDatesParsed) && workDatesParsed.ValueKind == JsonValueKind.Object)
                                    {
                                        if (workDatesParsed.TryGetProperty("start", out var startDate) && startDate.ValueKind == JsonValueKind.Object && startDate.TryGetProperty("date", out var startDateStr))
                                            workExpDetail.StartDate = startDateStr.GetString();
                                        if (workDatesParsed.TryGetProperty("end", out var endDate) && endDate.ValueKind == JsonValueKind.Object && endDate.TryGetProperty("date", out var endDateStr))
                                            workExpDetail.EndDate = endDateStr.GetString();
                                    }
                                }

                                if (workExpParsedFull.TryGetProperty("workExperienceDescription", out var descElement))
                                {
                                    if (descElement.TryGetProperty("parsed", out var descParsed))
                                        workExpDetail.Description = descParsed.GetString();
                                }

                                if (workExpParsedFull.TryGetProperty("workExperienceType", out var typeElement))
                                {
                                    if (typeElement.TryGetProperty("parsed", out var typeParsed))
                                    {
                                        if (typeParsed.ValueKind == JsonValueKind.Object && typeParsed.TryGetProperty("value", out var typeValue))
                                            workExpDetail.Type = typeValue.GetString();
                                        else if (typeParsed.ValueKind == JsonValueKind.String)
                                            workExpDetail.Type = typeParsed.GetString();
                                    }
                                }

                                workExperienceDetails.Add(workExpDetail);
                            }
                        }
                        catch (Exception workEx)
                        {
                            _logger.LogWarning(workEx, "Error extracting work experience detail");
                        }
                    }
                }

                // NEW: Extract projects
                if (data.TryGetProperty("project", out var projectArray) && projectArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var projectItem in projectArray.EnumerateArray())
                    {
                        if (projectItem.TryGetProperty("parsed", out var projectParsed))
                        {
                            var projectStr = projectParsed.GetString();
                            if (!string.IsNullOrWhiteSpace(projectStr))
                                projects.Add(projectStr);
                        }
                    }
                }

                // NEW: Extract achievements
                if (data.TryGetProperty("achievement", out var achievementArray) && achievementArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var achievementItem in achievementArray.EnumerateArray())
                    {
                        if (achievementItem.TryGetProperty("parsed", out var achievementParsed))
                        {
                            var achievementStr = achievementParsed.GetString();
                            if (!string.IsNullOrWhiteSpace(achievementStr))
                                achievements.Add(achievementStr);
                        }
                    }
                }

                // NEW: Extract associations
                if (data.TryGetProperty("association", out var associationArray) && associationArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var associationItem in associationArray.EnumerateArray())
                    {
                        if (associationItem.TryGetProperty("parsed", out var associationParsed))
                        {
                            var associationStr = associationParsed.GetString();
                            if (!string.IsNullOrWhiteSpace(associationStr))
                                associations.Add(associationStr);
                        }
                    }
                }

                // NEW: Extract patents
                if (data.TryGetProperty("patent", out var patentArray) && patentArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var patentItem in patentArray.EnumerateArray())
                    {
                        if (patentItem.TryGetProperty("parsed", out var patentParsed))
                        {
                            var patentStr = patentParsed.GetString();
                            if (!string.IsNullOrWhiteSpace(patentStr))
                                patents.Add(patentStr);
                        }
                    }
                }

                // NEW: Extract publications
                if (data.TryGetProperty("publication", out var publicationArray) && publicationArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var publicationItem in publicationArray.EnumerateArray())
                    {
                        if (publicationItem.TryGetProperty("parsed", out var publicationParsed))
                        {
                            var publicationStr = publicationParsed.GetString();
                            if (!string.IsNullOrWhiteSpace(publicationStr))
                                publications.Add(publicationStr);
                        }
                    }
                }

                // NEW: Extract hobbies
                if (data.TryGetProperty("hobby", out var hobbyArray) && hobbyArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var hobbyItem in hobbyArray.EnumerateArray())
                    {
                        if (hobbyItem.TryGetProperty("parsed", out var hobbyParsed))
                        {
                            var hobbyStr = hobbyParsed.GetString();
                            if (!string.IsNullOrWhiteSpace(hobbyStr))
                                hobbies.Add(hobbyStr);
                        }
                    }
                }

                // NEW: Extract referees
                if (data.TryGetProperty("referee", out var refereeArray) && refereeArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var refereeItem in refereeArray.EnumerateArray())
                    {
                        if (refereeItem.TryGetProperty("parsed", out var refereeParsed))
                        {
                            var refereeStr = refereeParsed.GetString();
                            if (!string.IsNullOrWhiteSpace(refereeStr))
                                referees.Add(refereeStr);
                        }
                    }
                }

                // NEW: Extract languages
                if (data.TryGetProperty("language", out var languageArray) && languageArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var languageItem in languageArray.EnumerateArray())
                    {
                        try
                        {
                            if (languageItem.TryGetProperty("parsed", out var languageParsed))
                            {
                                var langDetail = new LanguageDetail();

                                if (languageParsed.TryGetProperty("languageName", out var langNameElement))
                                {
                                    if (langNameElement.TryGetProperty("parsed", out var langNameParsed))
                                        langDetail.Name = langNameParsed.GetString();
                                }

                                if (languageParsed.TryGetProperty("languageProficiency", out var proficiencyElement))
                                {
                                    if (proficiencyElement.TryGetProperty("parsed", out var proficiencyParsed))
                                    {
                                        if (proficiencyParsed.ValueKind == JsonValueKind.Object && proficiencyParsed.TryGetProperty("value", out var proficiencyValue))
                                            langDetail.Proficiency = proficiencyValue.GetString();
                                        else if (proficiencyParsed.ValueKind == JsonValueKind.String)
                                            langDetail.Proficiency = proficiencyParsed.GetString();
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(langDetail.Name))
                                    languages.Add(langDetail);
                            }
                        }
                        catch (Exception langEx)
                        {
                            _logger.LogWarning(langEx, "Error extracting language detail");
                        }
                    }
                }

            }
            catch (Exception additionalFieldsEx)
            {
                _logger.LogWarning(additionalFieldsEx, "Error extracting additional fields, using basic extraction");
                // Continue with whatever we've extracted so far
            }

            // Variables are initialized above and populated in the try blocks

            // Create candidate metadata with all fields
            _logger.LogDebug("Creating candidate metadata. Email: {Email}, Phone: {Phone}", email, phone);
            var candidateMetaData = new CandidateMetaData
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Phone = phone,
                Location = location,
                LinkedInUrl = linkedInUrl,
                DateOfBirth = dateOfBirth,
                Headshot = headshot,
                Nationality = nationality,
                Availability = availability,
                PreferredWorkLocation = preferredWorkLocation,
                WillingToRelocate = willingToRelocate,
                RightToWork = rightToWork
            };

            // Create professional data with all fields
            _logger.LogDebug("Creating professional data. JobTitle: {JobTitle}, SkillsCount: {SkillsCount}", jobTitle, skills?.Count ?? 0);
            var professionalData = new ProfessionalData
            {
                DetectedJobTitle = jobTitle,
                TotalYearsExperience = totalYearsExperience,
                Skills = skills,
                EducationLevel = educationLevel,
                Objective = objective,
                Summary = summary,
                ExpectedSalary = expectedSalary,
                Education = educationDetails,
                WorkExperience = workExperienceDetails,
                Projects = projects,
                Achievements = achievements,
                Associations = associations,
                Patents = patents,
                Publications = publications,
                Hobbies = hobbies,
                Referees = referees,
                Languages = languages
            };

            _logger.LogDebug("Successfully created response objects");
            return new ParseResumeResponse
            {
                CandidateMetaData = candidateMetaData,
                ProfessionalData = professionalData,
                AffindaStructuredData = data,  // What Affinda parsed/structured
                RawAffindaResponse = rawJson   // Complete raw response
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping Affinda response: {Message}. StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
            
            // Return raw response on error
            try
            {
                var rawJson = JsonSerializer.Deserialize<JsonElement>(rawResponseContent);
                return new ParseResumeResponse
                {
                    RawAffindaResponse = rawJson
                };
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Failed to deserialize raw response in error handler");
                return new ParseResumeResponse
                {
                    RawAffindaResponse = null
                };
            }
        }
    }

    private int GetEducationLevelOrder(string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
            return 0;

            return level.ToLowerInvariant() switch
            {
                "phd" or "doctorate" => 5,
                "master" or "masters" => 4,
                "bachelor" or "bachelors" => 3,
                "associate" => 2,
                "diploma" or "certificate" => 1,
                _ => 0
            };
        }

    public async Task<List<AffindaWorkspace>> GetWorkspacesAsync()
    {
        try
        {
            _logger.LogInformation("Fetching workspaces from Affinda API");
            
            var response = await _httpClient.GetAsync($"{_baseUrl}/workspaces");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch workspaces: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new HttpRequestException($"Failed to fetch workspaces: {response.StatusCode} - {responseContent}");
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var workspaces = JsonSerializer.Deserialize<List<AffindaWorkspace>>(responseContent, jsonOptions);
            return workspaces ?? new List<AffindaWorkspace>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching workspaces from Affinda API");
            throw;
        }
    }
}

