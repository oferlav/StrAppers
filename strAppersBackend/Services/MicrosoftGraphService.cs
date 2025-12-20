using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace strAppersBackend.Services;

public interface IMicrosoftGraphService
{
    Task<string> GetAccessTokenAsync();
    Task<TeamsMeetingResponse> CreateTeamsMeetingAsync(CreateTeamsMeetingRequest request);
    Task<TeamsMeetingResponse> CreateTeamsMeetingWithoutAttendeesAsync(CreateTeamsMeetingRequest request);
    Task<bool> ForwardMeetingInviteAsync(string eventId, List<string> attendees);
    Task<bool> SendCustomMeetingInviteAsync(List<string> attendees, string subject, string meetingUrl, DateTime startTime, DateTime endTime, string customSenderName);
    Task<EventValidationResult> VerifyEventCreationAsync(string eventId);
    Task<bool> TestConnectionAsync();
    Task<MeetingTrackingResponse> TrackMeetingsByParticipantsAsync(DateTime startDate, DateTime endDate, List<string> emailAddresses, List<string> participantNames = null);
    Task<TeamsMeetingResponse> CreateRestrictedTeamsMeetingAsync(CreateTeamsMeetingRequest request);
}

public class MicrosoftGraphService : IMicrosoftGraphService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MicrosoftGraphService> _logger;
    private readonly string _tenantId;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _serviceAccountEmail;
    private readonly string _serviceAccountUserId;

    public MicrosoftGraphService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MicrosoftGraphService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        
        _tenantId = _configuration["MicrosoftGraph:TenantId"] ?? "";
        _clientId = _configuration["MicrosoftGraph:ClientId"] ?? "";
        _clientSecret = _configuration["MicrosoftGraph:ClientSecret"] ?? "";
        _serviceAccountEmail = _configuration["MicrosoftGraph:ServiceAccountEmail"] ?? "";
        _serviceAccountUserId = _configuration["MicrosoftGraph:ServiceAccountUserId"] ?? "";
        
        // Only log configuration if GetChat logs are not disabled (to reduce log noise)
        var disableGetChatLogs = _configuration.GetValue<bool>("Logging:DisableGetChatLogs", true);
        if (!disableGetChatLogs)
        {
        _logger.LogInformation("MicrosoftGraph Configuration loaded:");
        _logger.LogInformation("- TenantId: '{TenantId}' (Length: {TenantIdLength})", _tenantId, _tenantId?.Length ?? 0);
        _logger.LogInformation("- ClientId: '{ClientId}' (Length: {ClientIdLength})", _clientId, _clientId?.Length ?? 0);
        _logger.LogInformation("- ServiceAccountEmail: '{ServiceAccountEmail}' (Length: {EmailLength})", _serviceAccountEmail, _serviceAccountEmail?.Length ?? 0);
        _logger.LogInformation("- ServiceAccountUserId: '{ServiceAccountUserId}' (Length: {UserIdLength})", _serviceAccountUserId, _serviceAccountUserId?.Length ?? 0);
        
        // Debug: Check all MicrosoftGraph configuration keys
        var microsoftGraphSection = _configuration.GetSection("MicrosoftGraph");
        _logger.LogInformation("All MicrosoftGraph configuration keys:");
        foreach (var item in microsoftGraphSection.GetChildren())
        {
            _logger.LogInformation("- {Key}: '{Value}'", item.Key, item.Value);
            }
        }
    }

    public async Task<string> GetAccessTokenAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_tenantId) || string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
            {
                throw new InvalidOperationException("Microsoft Graph configuration is incomplete");
            }

            var tokenEndpoint = $"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token";
            
            var formData = new List<KeyValuePair<string, string>>
            {
                new("client_id", _clientId),
                new("client_secret", _clientSecret),
                new("scope", "https://graph.microsoft.com/.default"),
                new("grant_type", "client_credentials")
            };

            var formContent = new FormUrlEncodedContent(formData);
            
            _logger.LogInformation("Requesting access token from Microsoft Graph");
            
            var response = await _httpClient.PostAsync(tokenEndpoint, formContent);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get access token: {StatusCode} - {Content}", response.StatusCode, content);
                throw new HttpRequestException($"Failed to get access token: {response.StatusCode}");
            }

            var tokenResponse = JsonSerializer.Deserialize<JsonElement>(content);
            var accessToken = tokenResponse.GetProperty("access_token").GetString();
            
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new InvalidOperationException("Access token is null or empty");
            }

            // Decode token to verify roles (debugging)
            try
            {
                var tokenParts = accessToken.Split('.');
                if (tokenParts.Length >= 2)
                {
                    // Decode the payload (second part)
                    var payload = tokenParts[1];
                    // Add padding if needed
                    switch (payload.Length % 4)
                    {
                        case 2: payload += "=="; break;
                        case 3: payload += "="; break;
                    }
                    var payloadBytes = Convert.FromBase64String(payload);
                    var payloadJson = System.Text.Encoding.UTF8.GetString(payloadBytes);
                    var payloadElement = JsonSerializer.Deserialize<JsonElement>(payloadJson);
                    
                    // Log all claims for debugging
                    _logger.LogInformation("Token payload claims: {Claims}", string.Join(", ", payloadElement.EnumerateObject().Select(p => p.Name)));
                    
                    if (payloadElement.TryGetProperty("roles", out var roles))
                    {
                        var rolesList = new List<string>();
                        if (roles.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var role in roles.EnumerateArray())
                            {
                                if (role.ValueKind == JsonValueKind.String)
                                {
                                    rolesList.Add(role.GetString() ?? "");
                                }
                            }
                        }
                        
                        if (rolesList.Count > 0)
                        {
                            _logger.LogInformation("Token contains {RoleCount} role(s): {Roles}", rolesList.Count, string.Join(", ", rolesList));
                        }
                        else
                        {
                            _logger.LogError("⚠️ Token 'roles' claim exists but is EMPTY - no application permissions are included!");
                        }
                        
                        if (rolesList.Contains("CallRecords.Read.All", StringComparer.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("✓ CallRecords.Read.All role is present in token");
                        }
                        else
                        {
                            _logger.LogWarning("✗ CallRecords.Read.All role is NOT present in token");
                        }
                        
                        // Check other expected roles
                        var expectedRoles = new[] { "User.Read.All", "Calendars.ReadWrite", "OnlineMeetings.ReadWrite.All" };
                        foreach (var expectedRole in expectedRoles)
                        {
                            if (rolesList.Contains(expectedRole, StringComparer.OrdinalIgnoreCase))
                            {
                                _logger.LogInformation("✓ {Role} role is present in token", expectedRole);
                            }
                            else
                            {
                                _logger.LogWarning("✗ {Role} role is NOT present in token", expectedRole);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogError("⚠️ Token does not contain 'roles' claim at all - this indicates no application permissions are included!");
                    }
                    
                    // Log app ID and tenant for verification
                    if (payloadElement.TryGetProperty("appid", out var appId))
                    {
                        var tokenAppId = appId.GetString();
                        _logger.LogInformation("Token app ID: {AppId}", tokenAppId);
                        _logger.LogInformation("Configured ClientId: {ClientId}", _clientId);
                        if (tokenAppId != _clientId)
                        {
                            _logger.LogError("⚠️ MISMATCH: Token app ID does not match configured ClientId!");
                        }
                        else
                        {
                            _logger.LogInformation("✓ Token app ID matches configured ClientId");
                        }
                    }
                    if (payloadElement.TryGetProperty("tid", out var tid))
                    {
                        var tokenTenantId = tid.GetString();
                        _logger.LogInformation("Token tenant ID: {TenantId}", tokenTenantId);
                        _logger.LogInformation("Configured TenantId: {TenantId}", _tenantId);
                        if (tokenTenantId != _tenantId)
                        {
                            _logger.LogError("⚠️ MISMATCH: Token tenant ID does not match configured TenantId!");
                        }
                        else
                        {
                            _logger.LogInformation("✓ Token tenant ID matches configured TenantId");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decode token for debugging (non-critical)");
            }

            _logger.LogInformation("Successfully obtained access token");
            return accessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting access token: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<TeamsMeetingResponse> CreateTeamsMeetingAsync(CreateTeamsMeetingRequest request)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();
            
            // Use the configured service account to create the meeting
            var startTime = DateTime.Parse(request.DateTime);
            var endTime = startTime.AddMinutes(request.DurationMinutes);
            
            _logger.LogInformation("Checking service account email: '{ServiceAccountEmail}' (Length: {Length})", 
                _serviceAccountEmail, _serviceAccountEmail?.Length ?? 0);
            
            if (string.IsNullOrEmpty(_serviceAccountEmail) || _serviceAccountEmail == "your-service-account@yourdomain.com")
            {
                _logger.LogWarning("Service account email validation failed: '{ServiceAccountEmail}'", _serviceAccountEmail);
                return new TeamsMeetingResponse
                {
                    Success = false,
                    Message = "Service account email not configured. Please set MicrosoftGraph:ServiceAccountEmail in appsettings.json"
                };
            }
            
            _logger.LogInformation("Creating meeting on behalf of service account: {ServiceAccountEmail} (ID: {ServiceAccountUserId})", 
                _serviceAccountEmail, _serviceAccountUserId);
            
            _logger.LogInformation("DEBUG - Checking ServiceAccountUserId: '{ServiceAccountUserId}' (IsNullOrEmpty: {IsNullOrEmpty})", 
                _serviceAccountUserId, string.IsNullOrEmpty(_serviceAccountUserId));
            
            if (string.IsNullOrEmpty(_serviceAccountUserId))
            {
                _logger.LogError("ServiceAccountUserId is null or empty! Current value: '{ServiceAccountUserId}'", _serviceAccountUserId);
                return new TeamsMeetingResponse
                {
                    Success = false,
                    Message = "Service account user ID not configured. Please set MicrosoftGraph:ServiceAccountUserId in appsettings.json"
                };
            }
            
            // Use Calendar Events API to create event with attendees (auto-sends invites)
            var transactionId = $"strappers-{Guid.NewGuid()}";
            
            // Get custom sender name from configuration
            var customSenderName = _configuration["Smtp:FromName"] ?? "Skill-In Meetings";
            
            var meetingRequest = new
            {
                subject = request.Title,
                body = new
                {
                    contentType = "HTML",
                    content = $"<p>{request.Title}</p><p>Meeting details below.</p>"
                },
                start = new
                {
                    dateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    timeZone = "UTC"
                },
                end = new
                {
                    dateTime = endTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    timeZone = "UTC"
                },
                location = new
                {
                    displayName = "Microsoft Teams"
                },
                organizer = new
                {
                    emailAddress = new
                    {
                        name = customSenderName,  // Custom display name
                        address = _serviceAccountEmail
                    }
                },
                attendees = request.Attendees.Select(email => new
                {
                    emailAddress = new
                    {
                        address = email,
                        name = email
                    },
                    type = "required"
                }).ToArray(),
                isOnlineMeeting = true,
                onlineMeetingProvider = "teamsForBusiness",
                transactionId = transactionId
            };
            
            _logger.LogInformation("Creating event with transactionId: {TransactionId}", transactionId);

            // Call Microsoft Graph API to create calendar event
            var json = JsonSerializer.Serialize(meetingRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            _logger.LogInformation("Creating Teams meeting via Calendar API: {Title} for {AttendeeCount} attendees", 
                request.Title, request.Attendees.Count);
            _logger.LogInformation("Request JSON: {RequestJson}", json);
            
            // Set authorization header
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            // Use Calendar Events API - automatically sends email invites to attendees
            // Note: By default, POST /events sends notifications to attendees
            var response = await _httpClient.PostAsync(
                $"https://graph.microsoft.com/v1.0/users/{_serviceAccountEmail}/events", 
                content);
            
            _logger.LogInformation("Calendar event creation response status: {StatusCode}", response.StatusCode);

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Microsoft Graph API error: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return new TeamsMeetingResponse
                {
                    Success = false,
                    Message = $"Failed to create Teams meeting: {response.StatusCode}",
                    Details = responseContent
                };
            }

            // Parse the response to extract meeting details
            var meetingResponse = JsonSerializer.Deserialize<JsonElement>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Extract meeting details from Calendar Events API response
            var meetingId = meetingResponse.GetProperty("id").GetString();
            
            // Get the online meeting details from the event
            var joinUrl = meetingResponse.TryGetProperty("onlineMeeting", out var onlineMeetingElement) &&
                         onlineMeetingElement.TryGetProperty("joinUrl", out var joinUrlElement)
                ? joinUrlElement.GetString()
                : null;

            if (string.IsNullOrEmpty(joinUrl))
            {
                _logger.LogWarning("No Teams join URL found in calendar event response");
            }

            // Use the already calculated times for response

            _logger.LogInformation("Teams meeting created successfully. Meeting ID: {MeetingId}", meetingId);

            return new TeamsMeetingResponse
            {
                Success = true,
                Message = "Teams meeting created successfully",
                MeetingId = meetingId,
                MeetingTitle = request.Title,
                StartTime = startTime,
                EndTime = endTime,
                DurationMinutes = request.DurationMinutes,
                AttendeeCount = request.Attendees.Count,
                JoinUrl = joinUrl,
                Attendees = request.Attendees
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Teams meeting: {Message}", ex.Message);
            return new TeamsMeetingResponse
            {
                Success = false,
                Message = $"Error creating Teams meeting: {ex.Message}"
            };
        }
    }

    public async Task<TeamsMeetingResponse> CreateTeamsMeetingWithoutAttendeesAsync(CreateTeamsMeetingRequest request)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();
            
            var startTime = DateTime.Parse(request.DateTime);
            var endTime = startTime.AddMinutes(request.DurationMinutes);
            
            _logger.LogInformation("Creating Teams meeting WITHOUT attendees for SMTP delivery: {Title}", request.Title);
            
            if (string.IsNullOrEmpty(_serviceAccountEmail) || _serviceAccountEmail == "your-service-account@yourdomain.com")
            {
                return new TeamsMeetingResponse
                {
                    Success = false,
                    Message = "Service account email not configured. Please set MicrosoftGraph:ServiceAccountEmail in appsettings.json"
                };
            }
            
            // Create meeting WITHOUT attendees to prevent Exchange from sending invites
            var transactionId = $"strappers-smtp-{Guid.NewGuid()}";
            var meetingRequest = new
            {
                subject = request.Title,
                body = new
                {
                    contentType = "HTML",
                    content = $"<p>{request.Title}</p><p>Meeting details below.</p>"
                },
                start = new
                {
                    dateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    timeZone = "UTC"
                },
                end = new
                {
                    dateTime = endTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    timeZone = "UTC"
                },
                location = new
                {
                    displayName = "Microsoft Teams"
                },
                // NO ATTENDEES - this prevents Exchange from sending invites
                isOnlineMeeting = true,
                onlineMeetingProvider = "teamsForBusiness",
                transactionId = transactionId
            };
            
            _logger.LogInformation("Creating event without attendees, transactionId: {TransactionId}", transactionId);

            var json = JsonSerializer.Serialize(meetingRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await _httpClient.PostAsync(
                $"https://graph.microsoft.com/v1.0/users/{_serviceAccountEmail}/events", 
                content);
            
            _logger.LogInformation("Teams meeting (no attendees) creation response status: {StatusCode}", response.StatusCode);

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Microsoft Graph API error: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return new TeamsMeetingResponse
                {
                    Success = false,
                    Message = $"Failed to create Teams meeting: {response.StatusCode}",
                    Details = responseContent
                };
            }

            var meetingResponse = JsonSerializer.Deserialize<JsonElement>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var meetingId = meetingResponse.GetProperty("id").GetString();
            
            // Extract join URL from onlineMeeting, handling null case
            string? joinUrl = null;
            if (meetingResponse.TryGetProperty("onlineMeeting", out var onlineMeetingElement) &&
                onlineMeetingElement.ValueKind != JsonValueKind.Null &&
                onlineMeetingElement.ValueKind == JsonValueKind.Object)
            {
                if (onlineMeetingElement.TryGetProperty("joinUrl", out var joinUrlElement) &&
                    joinUrlElement.ValueKind != JsonValueKind.Null)
                {
                    joinUrl = joinUrlElement.GetString();
                }
            }

            if (string.IsNullOrEmpty(joinUrl))
            {
                _logger.LogWarning("No Teams join URL found in calendar event response");
            }

            _logger.LogInformation("Teams meeting (no attendees) created successfully. Meeting ID: {MeetingId}", meetingId);

            return new TeamsMeetingResponse
            {
                Success = true,
                Message = "Teams meeting created successfully (without Exchange invites)",
                MeetingId = meetingId,
                MeetingTitle = request.Title,
                StartTime = startTime,
                EndTime = endTime,
                DurationMinutes = request.DurationMinutes,
                AttendeeCount = request.Attendees.Count,
                JoinUrl = joinUrl,
                Attendees = request.Attendees
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Teams meeting without attendees: {Message}", ex.Message);
            return new TeamsMeetingResponse
            {
                Success = false,
                Message = $"Error creating Teams meeting: {ex.Message}"
            };
        }
    }

    public async Task<TeamsMeetingResponse> CreateRestrictedTeamsMeetingAsync(CreateTeamsMeetingRequest request)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();
            
            var startTime = DateTime.Parse(request.DateTime);
            var endTime = startTime.AddMinutes(request.DurationMinutes);
            
            _logger.LogInformation("Creating restricted Teams meeting WITH attendees for restricted access: {Title}", request.Title);
            
            if (string.IsNullOrEmpty(_serviceAccountEmail) || _serviceAccountEmail == "your-service-account@yourdomain.com")
            {
                return new TeamsMeetingResponse
                {
                    Success = false,
                    Message = "Service account email not configured. Please set MicrosoftGraph:ServiceAccountEmail in appsettings.json"
                };
            }

            // Get custom sender name from configuration
            var customSenderName = _configuration["Smtp:FromName"] ?? "Skill-In Meetings";
            
            // Create meeting WITH attendees (so they're invited and can join)
            var transactionId = $"strappers-restricted-{Guid.NewGuid()}";
            var meetingRequest = new
            {
                subject = request.Title,
                body = new
                {
                    contentType = "HTML",
                    content = $"<p>{request.Title}</p><p>Meeting details below.</p>"
                },
                start = new
                {
                    dateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    timeZone = "UTC"
                },
                end = new
                {
                    dateTime = endTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    timeZone = "UTC"
                },
                location = new
                {
                    displayName = "Microsoft Teams"
                },
                organizer = new
                {
                    emailAddress = new
                    {
                        name = customSenderName,
                        address = _serviceAccountEmail
                    }
                },
                attendees = request.Attendees.Select(email => new
                {
                    emailAddress = new
                    {
                        address = email,
                        name = email
                    },
                    type = "required"
                }).ToArray(),
                isOnlineMeeting = true,
                onlineMeetingProvider = "teamsForBusiness",
                transactionId = transactionId
            };
            
            _logger.LogInformation("Creating event with attendees for restricted meeting, transactionId: {TransactionId}", transactionId);
            _logger.LogInformation("Attendees to be invited: {Attendees}", string.Join(", ", request.Attendees));

            var json = JsonSerializer.Serialize(meetingRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogInformation("Meeting request JSON: {Json}", json);

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            // Create the calendar event with attendees
            // Note: By default, POST /events sends notifications to attendees automatically
            // Adding Prefer header to ensure invites are sent
            _httpClient.DefaultRequestHeaders.Remove("Prefer");
            _httpClient.DefaultRequestHeaders.Add("Prefer", "outlook.timezone=\"UTC\"");
            
            var response = await _httpClient.PostAsync(
                $"https://graph.microsoft.com/v1.0/users/{_serviceAccountEmail}/events", 
                content);
            
            _logger.LogInformation("Restricted Teams meeting creation response status: {StatusCode}", response.StatusCode);

            var responseContent = await response.Content.ReadAsStringAsync();
            
            // Log full response for debugging (truncated to avoid log spam)
            if (responseContent.Length > 1000)
            {
                _logger.LogInformation("Response content (truncated): {ResponseContent}...", responseContent.Substring(0, 1000));
            }
            else
            {
                _logger.LogInformation("Full response content: {ResponseContent}", responseContent);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Microsoft Graph API error: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return new TeamsMeetingResponse
                {
                    Success = false,
                    Message = $"Failed to create Teams meeting: {response.StatusCode}",
                    Details = responseContent
                };
            }

            var meetingResponse = JsonSerializer.Deserialize<JsonElement>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var meetingId = meetingResponse.GetProperty("id").GetString();
            
            // Verify attendees were included in the created event
            if (meetingResponse.TryGetProperty("attendees", out var attendeesElement) && 
                attendeesElement.ValueKind == JsonValueKind.Array)
            {
                var attendeeCount = attendeesElement.GetArrayLength();
                _logger.LogInformation("Event created with {AttendeeCount} attendees. Exchange should send calendar invites automatically.", attendeeCount);
                
                // Log attendee emails
                var attendeeEmails = new List<string>();
                foreach (var attendee in attendeesElement.EnumerateArray())
                {
                    if (attendee.TryGetProperty("emailAddress", out var emailAddress) &&
                        emailAddress.TryGetProperty("address", out var address))
                    {
                        attendeeEmails.Add(address.GetString() ?? "");
                    }
                }
                _logger.LogInformation("Attendee emails in created event: {Emails}", string.Join(", ", attendeeEmails));
            }
            else
            {
                _logger.LogWarning("No attendees found in created event response. Exchange may not send invites.");
            }
            
            // Log the response for debugging
            _logger.LogInformation("Meeting created successfully. Response contains onlineMeeting: {HasOnlineMeeting}", 
                meetingResponse.TryGetProperty("onlineMeeting", out var _));
            
            // Extract online meeting details
            string? joinUrl = null;
            string? onlineMeetingId = null;
            
            if (meetingResponse.TryGetProperty("onlineMeeting", out var onlineMeetingElement))
            {
                _logger.LogInformation("onlineMeeting element found. ValueKind: {ValueKind}", onlineMeetingElement.ValueKind);
                
                if (onlineMeetingElement.ValueKind == JsonValueKind.Null)
                {
                    _logger.LogWarning("onlineMeeting element is null in response");
                }
                else if (onlineMeetingElement.ValueKind == JsonValueKind.Object)
                {
                    if (onlineMeetingElement.TryGetProperty("joinUrl", out var joinUrlElement) && 
                        joinUrlElement.ValueKind != JsonValueKind.Null)
                    {
                        joinUrl = joinUrlElement.GetString();
                        _logger.LogInformation("Extracted joinUrl from onlineMeeting");
                    }
                    
                    // Get the onlineMeetingId to update meeting settings
                    if (onlineMeetingElement.TryGetProperty("id", out var onlineMeetingIdElement) && 
                        onlineMeetingIdElement.ValueKind != JsonValueKind.Null)
                    {
                        onlineMeetingId = onlineMeetingIdElement.GetString();
                        _logger.LogInformation("Extracted onlineMeetingId: {OnlineMeetingId}", onlineMeetingId);
                    }
                }
            }
            else
            {
                _logger.LogWarning("onlineMeeting property not found in meeting response");
            }

            // If onlineMeetingId is not in the response, try multiple approaches to get it
            if (string.IsNullOrEmpty(onlineMeetingId) || string.IsNullOrEmpty(joinUrl))
            {
                _logger.LogInformation("Online meeting details not found in initial response, attempting alternative methods");
                
                // Method 1: Wait a bit and fetch the event again (onlineMeeting should be included automatically, no expand needed)
                // Teams online meeting creation can be asynchronous and may take several seconds
                try
                {
                    // Try multiple times with increasing delays
                    for (int attempt = 1; attempt <= 3; attempt++)
                    {
                        var delayMs = attempt * 2000; // 2s, 4s, 6s
                        _logger.LogInformation("Waiting {DelayMs}ms before attempt {Attempt} to fetch online meeting details", delayMs, attempt);
                        await Task.Delay(delayMs);
                        
                        var eventResponse = await _httpClient.GetAsync(
                            $"https://graph.microsoft.com/v1.0/users/{_serviceAccountEmail}/events/{meetingId}");
                        
                        if (eventResponse.IsSuccessStatusCode)
                        {
                            var eventContent = await eventResponse.Content.ReadAsStringAsync();
                            _logger.LogInformation("Fetched event content (length: {Length}) on attempt {Attempt}", eventContent.Length, attempt);
                            
                            var eventData = JsonSerializer.Deserialize<JsonElement>(eventContent, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                            
                            // Check if onlineMeeting exists in the fetched event
                            if (eventData.TryGetProperty("onlineMeeting", out var onlineMeetingFromEvent))
                            {
                                _logger.LogInformation("onlineMeeting found in fetched event. ValueKind: {ValueKind}", onlineMeetingFromEvent.ValueKind);
                                
                                if (onlineMeetingFromEvent.ValueKind != JsonValueKind.Null && 
                                    onlineMeetingFromEvent.ValueKind == JsonValueKind.Object)
                                {
                                    if (onlineMeetingFromEvent.TryGetProperty("id", out var idElement) && 
                                        idElement.ValueKind != JsonValueKind.Null)
                                    {
                                        onlineMeetingId = idElement.GetString();
                                        _logger.LogInformation("Found online meeting ID from fetched event: {OnlineMeetingId}", onlineMeetingId);
                                    }
                                    if (string.IsNullOrEmpty(joinUrl) && 
                                        onlineMeetingFromEvent.TryGetProperty("joinUrl", out var joinUrlFromEvent) && 
                                        joinUrlFromEvent.ValueKind != JsonValueKind.Null)
                                    {
                                        joinUrl = joinUrlFromEvent.GetString();
                                        _logger.LogInformation("Found join URL from fetched event: {JoinUrl}", joinUrl);
                                        break; // Success, exit the retry loop
                                    }
                                }
                            }
                            
                            // If we found the joinUrl, break out of the retry loop
                            if (!string.IsNullOrEmpty(joinUrl))
                            {
                                break;
                            }
                        }
                        else
                        {
                            var errorContent = await eventResponse.Content.ReadAsStringAsync();
                            _logger.LogWarning("Failed to fetch event on attempt {Attempt}: {StatusCode} - {Error}", 
                                attempt, eventResponse.StatusCode, errorContent);
                        }
                    }
                    
                    // Log final status
                    if (string.IsNullOrEmpty(joinUrl))
                    {
                        _logger.LogWarning("After {Attempts} attempts, onlineMeeting joinUrl still not found. Teams meeting may not have been created automatically.", 3);
                        _logger.LogInformation("Note: Exchange should still send calendar invites automatically to all attendees.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch online meeting ID from event");
                }
                
                // Method 2: If still no joinUrl, try creating online meeting explicitly using /onlineMeetings endpoint
                if (string.IsNullOrEmpty(joinUrl))
                {
                    _logger.LogInformation("Join URL still not found, attempting to create online meeting explicitly");
                    try
                    {
                        var onlineMeetingRequest = new
                        {
                            startDateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            endDateTime = endTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            subject = request.Title,
                            participants = new
                            {
                                attendees = request.Attendees.Select(email => new
                                {
                                    upn = email,
                                    role = "attendee"
                                }).ToArray()
                            }
                        };
                        
                        var onlineMeetingJson = JsonSerializer.Serialize(onlineMeetingRequest, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
                        
                        var onlineMeetingContent = new StringContent(onlineMeetingJson, System.Text.Encoding.UTF8, "application/json");
                        
                        _logger.LogInformation("Attempting to create online meeting via /me/onlineMeetings");
                        
                        // Try /me/onlineMeetings first (works if service account is the authenticated user)
                        var onlineMeetingResponse = await _httpClient.PostAsync(
                            $"https://graph.microsoft.com/v1.0/me/onlineMeetings",
                            onlineMeetingContent);
                        
                        // If that fails, try /users/{id}/onlineMeetings
                        if (!onlineMeetingResponse.IsSuccessStatusCode)
                        {
                            var errorContent1 = await onlineMeetingResponse.Content.ReadAsStringAsync();
                            _logger.LogWarning("Failed with /me/onlineMeetings ({StatusCode}): {Error}. Trying /users/{ServiceAccountEmail}/onlineMeetings", 
                                onlineMeetingResponse.StatusCode, errorContent1, _serviceAccountEmail);
                            
                            onlineMeetingResponse = await _httpClient.PostAsync(
                                $"https://graph.microsoft.com/v1.0/users/{_serviceAccountEmail}/onlineMeetings",
                                onlineMeetingContent);
                        }
                        
                        if (onlineMeetingResponse.IsSuccessStatusCode)
                        {
                            var onlineMeetingResponseContent = await onlineMeetingResponse.Content.ReadAsStringAsync();
                            var onlineMeetingData = JsonSerializer.Deserialize<JsonElement>(onlineMeetingResponseContent, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                            
                            if (onlineMeetingData.TryGetProperty("joinUrl", out var joinUrlElement) && 
                                joinUrlElement.ValueKind != JsonValueKind.Null)
                            {
                                joinUrl = joinUrlElement.GetString();
                                _logger.LogInformation("Successfully created online meeting and obtained join URL");
                            }
                            
                            if (onlineMeetingData.TryGetProperty("id", out var idElement) && 
                                idElement.ValueKind != JsonValueKind.Null)
                            {
                                onlineMeetingId = idElement.GetString();
                                _logger.LogInformation("Successfully created online meeting with ID: {OnlineMeetingId}", onlineMeetingId);
                            }
                        }
                        else
                        {
                            var errorContent = await onlineMeetingResponse.Content.ReadAsStringAsync();
                            _logger.LogWarning("Failed to create online meeting explicitly: {StatusCode} - {Error}", 
                                onlineMeetingResponse.StatusCode, errorContent);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create online meeting explicitly: {Message}", ex.Message);
                    }
                }
            }

            // Update online meeting settings to restrict access (require lobby admission)
            if (!string.IsNullOrEmpty(onlineMeetingId))
            {
                try
                {
                    var meetingSettingsUpdate = new
                    {
                        lobbyBypassSettings = new
                        {
                            scope = "organizer", // Only organizer can bypass lobby
                            isLobbyBypassEnabled = false // Disable lobby bypass
                        },
                        allowAnonymousUsersToStartMeeting = false, // Don't allow anonymous users
                        allowedPresenters = "organizer" // Only organizer can present
                    };

                    var settingsJson = JsonSerializer.Serialize(meetingSettingsUpdate, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    var settingsContent = new StringContent(settingsJson, System.Text.Encoding.UTF8, "application/json");
                    
                    // Update online meeting settings
                    var settingsResponse = await _httpClient.PatchAsync(
                        $"https://graph.microsoft.com/v1.0/users/{_serviceAccountEmail}/onlineMeetings/{onlineMeetingId}",
                        settingsContent);

                    if (settingsResponse.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Successfully updated online meeting settings to restrict access (lobby required for non-invited users)");
                    }
                    else
                    {
                        var settingsError = await settingsResponse.Content.ReadAsStringAsync();
                        _logger.LogWarning("Failed to update online meeting settings: {StatusCode} - {Content}. Meeting created with attendees but may not be fully restricted.", 
                            settingsResponse.StatusCode, settingsError);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error updating online meeting settings. Meeting created with attendees but may not be fully restricted.");
                }
            }
            else
            {
                _logger.LogWarning("No online meeting ID found, cannot set restricted access settings. Meeting created with attendees but access restrictions may not be applied.");
            }

            _logger.LogInformation("Restricted Teams meeting created successfully. Meeting ID: {MeetingId}", meetingId);

            return new TeamsMeetingResponse
            {
                Success = true,
                Message = "Restricted Teams meeting created successfully (attendees invited, lobby required for others)",
                MeetingId = meetingId,
                MeetingTitle = request.Title,
                StartTime = startTime,
                EndTime = endTime,
                DurationMinutes = request.DurationMinutes,
                AttendeeCount = request.Attendees.Count,
                JoinUrl = joinUrl,
                Attendees = request.Attendees
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating restricted Teams meeting: {Message}", ex.Message);
            return new TeamsMeetingResponse
            {
                Success = false,
                Message = $"Error creating restricted Teams meeting: {ex.Message}"
            };
        }
    }

    public async Task<bool> ForwardMeetingInviteAsync(string eventId, List<string> attendees)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();
            
            _logger.LogInformation("Forwarding meeting invite for event {EventId} to {AttendeeCount} attendees", 
                eventId, attendees.Count);

            // Prepare the forward request
            var forwardRequest = new
            {
                comment = "Meeting invitation",
                toRecipients = attendees.Select(email => new
                {
                    emailAddress = new
                    {
                        address = email
                    }
                }).ToArray()
            };

            var json = JsonSerializer.Serialize(forwardRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            // Set authorization header
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            // Forward the event to attendees - this forces Exchange to send meeting requests
            var response = await _httpClient.PostAsync(
                $"https://graph.microsoft.com/v1.0/users/{_serviceAccountEmail}/events/{eventId}/forward", 
                content);

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to forward meeting invite: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return false;
            }

            _logger.LogInformation("Meeting invite forwarded successfully to {AttendeeCount} attendees", attendees.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding meeting invite: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<EventValidationResult> VerifyEventCreationAsync(string eventId)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();
            
            _logger.LogInformation("Verifying event creation for event {EventId}", eventId);

            // Set authorization header
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            // Get the event with specific fields to verify
            var response = await _httpClient.GetAsync(
                $"https://graph.microsoft.com/v1.0/users/{_serviceAccountEmail}/events/{eventId}?$select=id,subject,isOrganizer,responseRequested,attendees,start,end,isOnlineMeeting,onlineMeetingProvider");

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to verify event: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return new EventValidationResult { Success = false, Message = "Failed to retrieve event" };
            }

            var eventData = JsonSerializer.Deserialize<JsonElement>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Validate the event properties
            var isOrganizer = eventData.TryGetProperty("isOrganizer", out var isOrganizerElement) && isOrganizerElement.GetBoolean();
            var responseRequested = eventData.TryGetProperty("responseRequested", out var responseRequestedElement) && responseRequestedElement.GetBoolean();
            var isOnlineMeeting = eventData.TryGetProperty("isOnlineMeeting", out var isOnlineMeetingElement) && isOnlineMeetingElement.GetBoolean();
            
            var attendeesValid = true;
            if (eventData.TryGetProperty("attendees", out var attendeesElement) && attendeesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var attendee in attendeesElement.EnumerateArray())
                {
                    if (attendee.TryGetProperty("status", out var statusElement) &&
                        statusElement.TryGetProperty("response", out var responseElement))
                    {
                        var responseStatus = responseElement.GetString();
                        if (responseStatus != "none")
                        {
                            _logger.LogWarning("Attendee response is not 'none': {Response}", responseStatus);
                        }
                    }
                }
            }

            _logger.LogInformation("Event validation - isOrganizer: {IsOrganizer}, responseRequested: {ResponseRequested}, isOnlineMeeting: {IsOnlineMeeting}",
                isOrganizer, responseRequested, isOnlineMeeting);

            return new EventValidationResult
            {
                Success = true,
                IsOrganizer = isOrganizer,
                ResponseRequested = responseRequested,
                IsOnlineMeeting = isOnlineMeeting,
                AttendeesValid = attendeesValid,
                Message = $"Event validated - isOrganizer: {isOrganizer}, responseRequested: {responseRequested}, isOnlineMeeting: {isOnlineMeeting}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying event: {Message}", ex.Message);
            return new EventValidationResult { Success = false, Message = ex.Message };
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();
            
            // Test the connection by getting organization info (works with application permissions)
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.GetAsync("https://graph.microsoft.com/v1.0/organization");
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Microsoft Graph connection: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Track meetings by participants' email addresses and/or names within a date range
    /// Returns all meetings where any of the specified emails or names participated
    /// Supports both authenticated users (by email) and anonymous users (by name)
    /// </summary>
    public async Task<MeetingTrackingResponse> TrackMeetingsByParticipantsAsync(DateTime startDate, DateTime endDate, List<string> emailAddresses, List<string> participantNames = null)
    {
        var response = new MeetingTrackingResponse
        {
            Success = false,
            StartDate = startDate,
            EndDate = endDate,
            SearchedEmails = emailAddresses ?? new List<string>(),
            SearchedNames = participantNames ?? new List<string>()
        };

        try
        {
            // Validate that at least emails or names are provided
            var hasEmails = emailAddresses != null && emailAddresses.Count > 0;
            var hasNames = participantNames != null && participantNames.Count > 0;
            
            if (!hasEmails && !hasNames)
            {
                response.Message = "At least one email address or participant name must be provided";
                return response;
            }

            _logger.LogInformation("Tracking meetings for {EmailCount} emails and {NameCount} names between {StartDate} and {EndDate}", 
                emailAddresses?.Count ?? 0, participantNames?.Count ?? 0, startDate, endDate);

            var accessToken = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Test token with a simpler API call first
            _logger.LogInformation("Testing token with a simple Graph API call...");
            try
            {
                var testResponse = await _httpClient.GetAsync("https://graph.microsoft.com/v1.0/users?$top=1");
                _logger.LogInformation("Test API call status: {StatusCode}", testResponse.StatusCode);
                if (!testResponse.IsSuccessStatusCode)
                {
                    var testErrorContent = await testResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("Test API call failed: {StatusCode} - {Content}", testResponse.StatusCode, testErrorContent);
                }
                else
                {
                    _logger.LogInformation("✓ Token is valid and working with Graph API");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Test API call failed with exception");
            }

            // Normalize email addresses for comparison (lowercase, exact match)
            var normalizedEmails = (emailAddresses ?? new List<string>()).Select(e => e.ToLowerInvariant().Trim()).Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
            var emailSet = new HashSet<string>(normalizedEmails, StringComparer.OrdinalIgnoreCase);
            
            // Normalize participant names for comparison (trim whitespace, case-insensitive)
            // Store both original and normalized versions for partial matching
            var normalizedNames = (participantNames ?? new List<string>()).Select(n => n.Trim()).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            var nameSet = new HashSet<string>(normalizedNames.Select(n => n.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);

            // Format dates for API query (ISO 8601 format)
            var startDateStr = startDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            var endDateStr = endDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

            // Get call records filtered by date range (handle pagination)
            var allCallRecords = new List<JsonElement>();
            var callRecordsUrl = $"https://graph.microsoft.com/v1.0/communications/callRecords?$filter=startDateTime ge {startDateStr} and startDateTime le {endDateStr}";
            _logger.LogInformation("Fetching call records from: {Url}", callRecordsUrl);

            // Handle pagination
            var nextLink = callRecordsUrl;
            int pageCount = 0;
            while (!string.IsNullOrEmpty(nextLink))
            {
                pageCount++;
                _logger.LogInformation("Fetching call records page {PageCount}", pageCount);

                var callRecordsResponse = await _httpClient.GetAsync(nextLink);
                
                if (!callRecordsResponse.IsSuccessStatusCode)
                {
                    var errorContent = await callRecordsResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to fetch call records: {StatusCode} - {Content}", callRecordsResponse.StatusCode, errorContent);
                    
                    // Try to parse error details for better logging
                    try
                    {
                        var errorData = JsonSerializer.Deserialize<JsonElement>(errorContent);
                        if (errorData.TryGetProperty("error", out var errorObj))
                        {
                            var errorCode = errorObj.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : "Unknown";
                            var errorMessage = errorObj.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "No message";
                            _logger.LogError("Call Records API Error - Code: {ErrorCode}, Message: {ErrorMessage}", errorCode, errorMessage);
                            
                            if (errorObj.TryGetProperty("innerError", out var innerError))
                            {
                                var requestId = innerError.TryGetProperty("request-id", out var reqIdProp) ? reqIdProp.GetString() : "Unknown";
                                _logger.LogError("Request ID: {RequestId}", requestId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse error response");
                    }
                    
                    if (allCallRecords.Count == 0)
                    {
                        response.Message = $"Failed to fetch call records: {callRecordsResponse.StatusCode}";
                        return response;
                    }
                    // If we already have some records, continue with what we have
                    break;
                }

                var callRecordsContent = await callRecordsResponse.Content.ReadAsStringAsync();
                var callRecordsData = JsonSerializer.Deserialize<JsonElement>(callRecordsContent);
                
                if (callRecordsData.TryGetProperty("value", out var callRecordsArray) && callRecordsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var record in callRecordsArray.EnumerateArray())
                    {
                        allCallRecords.Add(record);
                    }
                    _logger.LogInformation("Fetched {Count} call records from page {PageCount}", callRecordsArray.GetArrayLength(), pageCount);
                }

                // Check for next page
                nextLink = callRecordsData.TryGetProperty("@odata.nextLink", out var nextLinkProp) ? nextLinkProp.GetString() : "";
            }

            if (allCallRecords.Count == 0)
            {
                _logger.LogInformation("No call records found in date range");
                response.Success = true;
                response.Message = "No call records found";
                return response;
            }

            _logger.LogInformation("Found {TotalCount} total call records in date range across {PageCount} page(s)", allCallRecords.Count, pageCount);

            var matchingMeetings = new List<MeetingRecord>();
            var processedCallRecordIds = new HashSet<string>();

            // Process each call record
            foreach (var callRecord in allCallRecords)
            {
                var callRecordId = callRecord.TryGetProperty("id", out var idProp) ? idProp.GetString() : "";
                if (string.IsNullOrEmpty(callRecordId) || processedCallRecordIds.Contains(callRecordId))
                    continue;

                processedCallRecordIds.Add(callRecordId);

                // Get sessions for this call record (handle pagination)
                var allSessions = new List<JsonElement>();
                var sessionsUrl = $"https://graph.microsoft.com/v1.0/communications/callRecords/{callRecordId}/sessions";
                var sessionsNextLink = sessionsUrl;
                int sessionsPageCount = 0;

                while (!string.IsNullOrEmpty(sessionsNextLink))
                {
                    sessionsPageCount++;
                    var sessionsResponse = await _httpClient.GetAsync(sessionsNextLink);

                    if (!sessionsResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to fetch sessions for call record {CallRecordId}: {StatusCode}", callRecordId, sessionsResponse.StatusCode);
                        break;
                    }

                    var sessionsContent = await sessionsResponse.Content.ReadAsStringAsync();
                    var sessionsData = JsonSerializer.Deserialize<JsonElement>(sessionsContent);

                    if (sessionsData.TryGetProperty("value", out var sessionsArray) && sessionsArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var session in sessionsArray.EnumerateArray())
                        {
                            allSessions.Add(session);
                        }
                    }

                    // Check for next page
                    sessionsNextLink = sessionsData.TryGetProperty("@odata.nextLink", out var sessionsNextLinkProp) ? sessionsNextLinkProp.GetString() : "";
                }

                if (allSessions.Count == 0)
                    continue;

                var matchingParticipants = new List<ParticipantRecord>();

                // Check each session for matching participants
                foreach (var session in allSessions)
                {
                    // Get participant information from session
                    var participantEmail = "";
                    var participantName = "";
                    var participantUserId = "";

                    if (session.TryGetProperty("caller", out var callerProp))
                    {
                        participantEmail = callerProp.TryGetProperty("userPrincipalName", out var upnProp) ? upnProp.GetString() : "";
                        participantName = callerProp.TryGetProperty("displayName", out var nameProp) ? nameProp.GetString() : "";
                        participantUserId = callerProp.TryGetProperty("id", out var userIdProp) ? userIdProp.GetString() : "";
                    }

                    // Also check callee
                    if (string.IsNullOrEmpty(participantEmail) && session.TryGetProperty("callee", out var calleeProp))
                    {
                        participantEmail = calleeProp.TryGetProperty("userPrincipalName", out var upnProp2) ? upnProp2.GetString() : "";
                        // Only update name if we don't already have one from caller
                        if (string.IsNullOrEmpty(participantName))
                        {
                            participantName = calleeProp.TryGetProperty("displayName", out var nameProp2) ? nameProp2.GetString() : "";
                        }
                        if (string.IsNullOrEmpty(participantUserId))
                        {
                            participantUserId = calleeProp.TryGetProperty("id", out var userIdProp2) ? userIdProp2.GetString() : "";
                        }
                    }

                    // Skip if participant has neither email nor name
                    if (string.IsNullOrEmpty(participantEmail) && string.IsNullOrEmpty(participantName))
                        continue;

                    // Check if this participant matches by email OR name
                    // Email matching: exact match (case-insensitive)
                    bool matchesEmail = !string.IsNullOrEmpty(participantEmail) && 
                        emailSet.Contains(participantEmail.ToLowerInvariant().Trim());
                    
                    // Name matching: case-insensitive partial match (e.g., "John" matches "John Doe")
                    bool matchesName = false;
                    if (!string.IsNullOrEmpty(participantName))
                    {
                        var normalizedParticipantName = participantName.Trim().ToLowerInvariant();
                        
                        // Check for exact match first
                        if (nameSet.Contains(normalizedParticipantName))
                        {
                            matchesName = true;
                        }
                        else
                        {
                            // Check for partial match: if any searched name is contained in participant name
                            // or if participant name is contained in any searched name
                            foreach (var searchedName in normalizedNames)
                            {
                                var normalizedSearchedName = searchedName.ToLowerInvariant();
                                if (normalizedParticipantName.Contains(normalizedSearchedName) || 
                                    normalizedSearchedName.Contains(normalizedParticipantName))
                                {
                                    matchesName = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (matchesEmail || matchesName)
                    {
                        var joinTime = session.TryGetProperty("startDateTime", out var startProp) && DateTime.TryParse(startProp.GetString(), out var startDt) ? startDt : (DateTime?)null;
                        var leaveTime = session.TryGetProperty("endDateTime", out var endProp) && DateTime.TryParse(endProp.GetString(), out var endDt) ? endDt : (DateTime?)null;
                        var platform = session.TryGetProperty("modalities", out var modProp) && modProp.ValueKind == JsonValueKind.Array 
                            ? string.Join(", ", modProp.EnumerateArray().Select(m => m.GetString())) 
                            : null;

                        var duration = joinTime.HasValue && leaveTime.HasValue ? leaveTime.Value - joinTime.Value : (TimeSpan?)null;

                        matchingParticipants.Add(new ParticipantRecord
                        {
                            Email = participantEmail,
                            Name = participantName,
                            JoinTime = joinTime,
                            LeaveTime = leaveTime,
                            Duration = duration,
                            Platform = platform,
                            UserId = participantUserId
                        });
                    }
                }

                // If we found matching participants, add this meeting
                if (matchingParticipants.Count > 0)
                {
                    var startDateTime = callRecord.TryGetProperty("startDateTime", out var callStartProp) && DateTime.TryParse(callStartProp.GetString(), out var callStartDt) 
                        ? callStartDt 
                        : DateTime.MinValue;
                    var endDateTime = callRecord.TryGetProperty("endDateTime", out var callEndProp) && DateTime.TryParse(callEndProp.GetString(), out var callEndDt) 
                        ? callEndDt 
                        : DateTime.MinValue;
                    var meetingType = callRecord.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

                    // Get organizer information
                    var organizerEmail = "";
                    var organizerName = "";
                    if (callRecord.TryGetProperty("organizer", out var organizerProp))
                    {
                        organizerEmail = organizerProp.TryGetProperty("userPrincipalName", out var orgUpnProp) ? orgUpnProp.GetString() : "";
                        organizerName = organizerProp.TryGetProperty("displayName", out var orgNameProp) ? orgNameProp.GetString() : "";
                    }

                    var duration = endDateTime != DateTime.MinValue && startDateTime != DateTime.MinValue 
                        ? endDateTime - startDateTime 
                        : TimeSpan.Zero;

                    matchingMeetings.Add(new MeetingRecord
                    {
                        CallRecordId = callRecordId,
                        StartDateTime = startDateTime,
                        EndDateTime = endDateTime,
                        OrganizerEmail = organizerEmail,
                        OrganizerName = organizerName,
                        MeetingType = meetingType,
                        Participants = matchingParticipants,
                        TotalParticipants = matchingParticipants.Count,
                        Duration = duration
                    });

                    _logger.LogInformation("Found matching meeting {CallRecordId} with {ParticipantCount} matching participants", 
                        callRecordId, matchingParticipants.Count);
                }
            }

            response.Success = true;
            response.Meetings = matchingMeetings.OrderByDescending(m => m.StartDateTime).ToList();
            response.TotalMeetings = matchingMeetings.Count;
            response.Message = $"Found {matchingMeetings.Count} meeting(s) with matching participants";

            _logger.LogInformation("Meeting tracking completed: Found {MeetingCount} meetings with matching participants", matchingMeetings.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking meetings by participants: {Message}", ex.Message);
            response.Message = $"Error tracking meetings: {ex.Message}";
            return response;
        }
    }

    public async Task<bool> SendCustomMeetingInviteAsync(List<string> attendees, string subject, string meetingUrl, DateTime startTime, DateTime endTime, string customSenderName)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();
            
            _logger.LogInformation("Sending custom meeting invites to {Count} attendees with sender name: {SenderName}", 
                attendees.Count, customSenderName);

            // Create iCalendar content
            var icsContent = GenerateIcsContent(subject, startTime, endTime, meetingUrl, _serviceAccountEmail ?? "noreply@skill-in.com");
            var icsBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(icsContent));

            foreach (var attendee in attendees)
            {
                // Create the email payload using a dictionary to handle @odata.type correctly
                var attachmentDict = new Dictionary<string, object>
                {
                    { "@odata.type", "#microsoft.graph.fileAttachment" },
                    { "name", "meeting.ics" },
                    { "contentType", "text/calendar" },
                    { "contentBytes", icsBase64 }
                };

                var messageDict = new Dictionary<string, object>
                {
                    { "subject", $"Meeting Invitation: {subject}" },
                    { "body", new Dictionary<string, string>
                        {
                            { "contentType", "HTML" },
                            { "content", GenerateMeetingEmailBody(subject, startTime, endTime, meetingUrl) }
                        }
                    },
                    { "from", new Dictionary<string, object>
                        {
                            { "emailAddress", new Dictionary<string, string>
                                {
                                    { "name", customSenderName },
                                    { "address", _serviceAccountEmail ?? "" }
                                }
                            }
                        }
                    },
                    { "toRecipients", new[]
                        {
                            new Dictionary<string, object>
                            {
                                { "emailAddress", new Dictionary<string, string>
                                    {
                                        { "address", attendee }
                                    }
                                }
                            }
                        }
                    },
                    { "attachments", new[] { attachmentDict } }
                };

                var emailPayload = new Dictionary<string, object>
                {
                    { "message", messageDict },
                    { "saveToSentItems", true }
                };

                var json = JsonSerializer.Serialize(emailPayload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.PostAsync(
                    $"https://graph.microsoft.com/v1.0/users/{_serviceAccountEmail}/sendMail",
                    content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to send custom invite to {Attendee}: {Error}", attendee, error);
                }
                else
                {
                    _logger.LogInformation("Custom meeting invite sent successfully to {Attendee}", attendee);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending custom meeting invites: {Message}", ex.Message);
            return false;
        }
    }

    private string GenerateIcsContent(string subject, DateTime startTime, DateTime endTime, string meetingUrl, string organizerEmail)
    {
        var uid = Guid.NewGuid().ToString();
        var startUtc = startTime.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
        var endUtc = endTime.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
        var nowUtc = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");

        return $@"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Skill-In//Meeting Invitation//EN
METHOD:REQUEST
BEGIN:VEVENT
UID:{uid}
DTSTAMP:{nowUtc}
DTSTART:{startUtc}
DTEND:{endUtc}
SUMMARY:{subject}
DESCRIPTION:Join the meeting: {meetingUrl}
LOCATION:{meetingUrl}
URL:{meetingUrl}
ORGANIZER:MAILTO:{organizerEmail}
STATUS:CONFIRMED
SEQUENCE:0
BEGIN:VALARM
TRIGGER:-PT15M
ACTION:DISPLAY
DESCRIPTION:Reminder
END:VALARM
END:VEVENT
END:VCALENDAR";
    }

    private string GenerateMeetingEmailBody(string subject, DateTime startTime, DateTime endTime, string meetingUrl)
    {
        var startFormatted = startTime.ToString("MMMM dd, yyyy 'at' h:mm tt");
        var endFormatted = endTime.ToString("h:mm tt");
        var duration = (endTime - startTime).TotalMinutes;

        // Generate Google Calendar link
        var googleCalendarLink = BuildGoogleCalendarLink(subject, startTime, endTime, $"Join Teams Meeting: {meetingUrl}", meetingUrl);

        return $@"
<html>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <h2 style='color: #2c3e50;'>📅 Meeting Invitation</h2>
        
        <h3 style='color: #667eea;'>{subject}</h3>
        
        <div style='background-color: #f0f4ff; padding: 15px; border-radius: 5px; margin: 20px 0;'>
            <p style='margin: 5px 0;'><strong>📅 Date & Time:</strong> {startFormatted}</p>
            <p style='margin: 5px 0;'><strong>⏱️ Duration:</strong> {duration} minutes</p>
        </div>
        
        <div style='margin: 20px 0;'>
            <a href='{meetingUrl}' style='display:inline-block;padding:12px 24px;background-color:#667eea;color:#fff;text-decoration:none;border-radius:5px;font-weight:bold;'>Join Teams Meeting</a>
        </div>

        <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #28a745;'>
            <h3 style='color: #28a745; margin-top: 0;'>📅 Add to Calendar</h3>
            <p style='margin: 10px 0;'><strong>Choose your preferred calendar app:</strong></p>
            <p style='margin: 10px 0;'>
                <a href='{googleCalendarLink}' target='_blank' style='display:inline-block;padding:10px 16px;border-radius:6px;background:#1a73e8;color:#fff;text-decoration:none;font-weight:600;margin-right:10px;'>Add to Google Calendar</a>
                <span style='color: #666; font-size: 14px;'>or download the .ics file attachment for Outlook, Apple Calendar, etc.</span>
            </p>
        </div>
        
        <div style='background-color: #e8f5e8; padding: 15px; border-radius: 5px; margin: 20px 0;'>
            <h3 style='color: #27ae60; margin-top: 0;'>📋 What to Expect</h3>
            <ul style='margin: 10px 0; padding-left: 20px;'>
                <li>Click the meeting link above to join</li>
                <li>Test your camera and microphone before the meeting</li>
                <li>Join a few minutes early to ensure everything works</li>
                <li>Have a stable internet connection</li>
            </ul>
        </div>
        
        <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee; color: #666; font-size: 14px;'>
            <p>This meeting invitation was sent by Skill-In Platform.</p>
            <p>If you have any questions, please contact your project administrator.</p>
        </div>
    </div>
</body>
</html>";
    }

    private string BuildGoogleCalendarLink(string title, DateTime startTime, DateTime endTime, string description, string location)
    {
        // Convert to UTC and format as YYYYMMDDTHHMMSSZ
        var startUtc = startTime.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");
        var endUtc = endTime.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");
        var dates = $"{startUtc}/{endUtc}";

        var baseUrl = "https://calendar.google.com/calendar/render?action=TEMPLATE";
        var url = baseUrl
            + "&text=" + System.Net.WebUtility.UrlEncode(title ?? "")
            + "&dates=" + dates
            + "&details=" + System.Net.WebUtility.UrlEncode(description ?? "")
            + "&location=" + System.Net.WebUtility.UrlEncode(location ?? "");

        _logger.LogInformation("Generated Google Calendar link for meeting: {Title}", title);
        return url;
    }
}

/// <summary>
/// Request model for creating Teams meetings
/// </summary>
public class CreateTeamsMeetingRequest
{
    public string Title { get; set; } = string.Empty;
    public string DateTime { get; set; } = string.Empty; // Format: "2024-01-15T14:30:00Z"
    public int DurationMinutes { get; set; }
    public List<string> Attendees { get; set; } = new List<string>();
}

/// <summary>
/// Response model for Teams meeting creation
/// </summary>
public class TeamsMeetingResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? MeetingId { get; set; }
    public string? MeetingTitle { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public int AttendeeCount { get; set; }
    public string? JoinUrl { get; set; }
    public List<string>? Attendees { get; set; }
    public string? Details { get; set; }
}

/// <summary>
/// Result model for event validation
/// </summary>
public class EventValidationResult
{
    public bool Success { get; set; }
    public bool IsOrganizer { get; set; }
    public bool ResponseRequested { get; set; }
    public bool IsOnlineMeeting { get; set; }
    public bool AttendeesValid { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response model for meeting tracking by participants
/// </summary>
public class MeetingTrackingResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<MeetingRecord> Meetings { get; set; } = new List<MeetingRecord>();
    public int TotalMeetings { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<string> SearchedEmails { get; set; } = new List<string>();
    public List<string> SearchedNames { get; set; } = new List<string>();
}

/// <summary>
/// Model representing a meeting record with participant details
/// </summary>
public class MeetingRecord
{
    public string CallRecordId { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string? OrganizerEmail { get; set; }
    public string? OrganizerName { get; set; }
    public string? MeetingType { get; set; }
    public List<ParticipantRecord> Participants { get; set; } = new List<ParticipantRecord>();
    public int TotalParticipants { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Model representing a participant in a meeting
/// </summary>
public class ParticipantRecord
{
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public DateTime? JoinTime { get; set; }
    public DateTime? LeaveTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? Platform { get; set; }
    public string? UserId { get; set; }
}
