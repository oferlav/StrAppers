using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]/use")]
public class GoogleWorkspaceController : ControllerBase
{
    private readonly IGoogleCalendarService _calendarService;
    private readonly IGmailService _gmailService;
    private readonly ILogger<GoogleWorkspaceController> _logger;
    private readonly GoogleWorkspaceConfig _config;

    public GoogleWorkspaceController(
        IGoogleCalendarService calendarService,
        IGmailService gmailService,
        ILogger<GoogleWorkspaceController> logger,
        IOptions<GoogleWorkspaceConfig> config)
    {
        _calendarService = calendarService;
        _gmailService = gmailService;
        _logger = logger;
        _config = config.Value;
    }

    /// <summary>
    /// Create a Google Meet meeting and send calendar invitations to attendees
    /// </summary>
    [HttpPost("create-meeting")]
    public async Task<ActionResult<object>> CreateGoogleMeeting(CreateGoogleWorkspaceMeetingRequest request)
    {
        try
        {
            _logger.LogInformation("Creating Google Meet meeting: {Title} at {StartTime} for {AttendeeCount} attendees", 
                request.Title, request.StartTime, request.Attendees.Count);

            // Validate request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Convert to service request
            var serviceRequest = new CreateGoogleMeetingRequest
            {
                Title = request.Title,
                Description = request.Description,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Attendees = request.Attendees
            };

            // Create the meeting using the calendar service
            var result = await _calendarService.CreateMeetingAsync(serviceRequest);

            if (result.Success)
            {
                _logger.LogInformation("Google Meet meeting created successfully: {MeetingId}", result.MeetingId);

                // Send email notifications to all attendees
                if (result.Attendees != null && result.Attendees.Any())
                {
                    var emailSuccess = await _gmailService.SendBulkMeetingEmailsAsync(
                        result.Attendees,
                        result.MeetingTitle ?? request.Title,
                        result.StartTime ?? request.StartTime,
                        result.EndTime ?? request.EndTime,
                        result.JoinUrl ?? "",
                        result.Details ?? request.Description
                    );

                    if (emailSuccess)
                    {
                        _logger.LogInformation("Meeting emails sent successfully to all attendees");
                    }
                    else
                    {
                        _logger.LogWarning("Some meeting emails may not have been sent successfully");
                    }
                }

                return Ok(new
                {
                    Success = true,
                    Message = "Google Meet meeting created and emails sent successfully",
                    MeetingId = result.MeetingId,
                    MeetingTitle = result.MeetingTitle,
                    StartTime = result.StartTime,
                    EndTime = result.EndTime,
                    DurationMinutes = result.DurationMinutes,
                    AttendeeCount = result.AttendeeCount,
                    JoinUrl = result.JoinUrl,
                    Attendees = result.Attendees
                });
            }
            else
            {
                _logger.LogError("Failed to create Google Meet meeting: {Message}", result.Message);
                return BadRequest(new
                {
                    Success = false,
                    Message = result.Message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Google Meet meeting: {Message}", ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = "An error occurred while creating the meeting",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Send meeting email to specific attendees
    /// </summary>
    [HttpPost("send-meeting-email")]
    public async Task<ActionResult<object>> SendMeetingEmail(SendMeetingEmailRequest request)
    {
        try
        {
            _logger.LogInformation("Sending meeting email to {Email} for meeting: {Title}", 
                request.RecipientEmail, request.MeetingTitle);

            var success = await _gmailService.SendMeetingEmailAsync(
                request.RecipientEmail,
                request.MeetingTitle,
                request.StartTime,
                request.EndTime,
                request.MeetingLink,
                request.MeetingDescription
            );

            if (success)
            {
                return Ok(new
                {
                    Success = true,
                    Message = "Meeting email sent successfully"
                });
            }
            else
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = "Failed to send meeting email"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending meeting email: {Message}", ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = "An error occurred while sending the email",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Test Google Workspace configuration
    /// </summary>
    [HttpGet("test-config")]
    public ActionResult<object> TestConfig()
    {
        try
        {
            var config = new
            {
                ServiceAccountEmail = _config.ServiceAccountEmail,
                Scopes = _config.Scopes,
                HasClientId = !string.IsNullOrEmpty(_config.ClientId),
                HasClientSecret = !string.IsNullOrEmpty(_config.ClientSecret),
                HasServiceAccountKey = !string.IsNullOrEmpty(_config.ServiceAccountKeyPath)
            };

            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting configuration: {Message}", ex.Message);
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }
}

public class CreateGoogleWorkspaceMeetingRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<string> Attendees { get; set; } = new List<string>();
}

public class SendMeetingEmailRequest
{
    public string RecipientEmail { get; set; } = string.Empty;
    public string MeetingTitle { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string MeetingLink { get; set; } = string.Empty;
    public string MeetingDescription { get; set; } = string.Empty;
}
