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
            
            var joinUrl = meetingResponse.TryGetProperty("onlineMeeting", out var onlineMeetingElement) &&
                         onlineMeetingElement.TryGetProperty("joinUrl", out var joinUrlElement)
                ? joinUrlElement.GetString()
                : null;

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
