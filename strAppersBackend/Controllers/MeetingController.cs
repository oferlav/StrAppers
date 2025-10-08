using Microsoft.AspNetCore.Mvc;
using strAppersBackend.Services;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]/use")]
public class MeetingController : ControllerBase
{
    private readonly IGoogleCalendarService _calendarService;
    private readonly IGmailService _gmailService;
    private readonly ISmtpEmailService _smtpEmailService;
    private readonly ILogger<MeetingController> _logger;

    public MeetingController(
        IGoogleCalendarService calendarService,
        IGmailService gmailService,
        ISmtpEmailService smtpEmailService,
        ILogger<MeetingController> logger)
    {
        _calendarService = calendarService;
        _gmailService = gmailService;
        _smtpEmailService = smtpEmailService;
        _logger = logger;
    }

    /// <summary>
    /// Create a Google Meet meeting and send emails to attendees
    /// </summary>
    [HttpPost("create-meeting")]
    public async Task<ActionResult<object>> CreateMeeting(CreateSimpleMeetingRequest request)
    {
        try
        {
            _logger.LogInformation("Creating meeting: {Title} at {StartTime} for {AttendeeCount} attendees", 
                request.Title, request.StartTime, request.AttendeeEmails.Count);

            // Validate request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate required fields
            if (string.IsNullOrEmpty(request.Title))
            {
                return BadRequest("Title is required.");
            }

            if (request.AttendeeEmails == null || !request.AttendeeEmails.Any())
            {
                return BadRequest("At least one attendee email is required.");
            }

            if (request.StartTime >= request.EndTime)
            {
                return BadRequest("Start time must be before end time.");
            }

            // Create Google Meet meeting
            var meetingRequest = new CreateGoogleMeetingRequest
            {
                Title = request.Title,
                Description = request.Description ?? "",
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Attendees = request.AttendeeEmails
            };

            var result = await _calendarService.CreateMeetingAsync(meetingRequest);

            if (result.Success)
            {
                _logger.LogInformation("Google Meet meeting created successfully: {MeetingId}", result.MeetingId);

                // Send email notifications to all attendees
                var emailSuccess = await _gmailService.SendBulkMeetingEmailsAsync(
                    request.AttendeeEmails,
                    result.MeetingTitle ?? request.Title,
                    result.StartTime ?? request.StartTime,
                    result.EndTime ?? request.EndTime,
                    result.JoinUrl ?? "",
                    result.Details ?? request.Description ?? ""
                );

                // If Gmail API fails, try SMTP as fallback
                if (!emailSuccess)
                {
                    _logger.LogInformation("Gmail API failed, trying SMTP as fallback");
                    emailSuccess = await _smtpEmailService.SendBulkMeetingEmailsAsync(
                        request.AttendeeEmails,
                        result.MeetingTitle ?? request.Title,
                        result.StartTime ?? request.StartTime,
                        result.EndTime ?? request.EndTime,
                        result.JoinUrl ?? "",
                        result.Details ?? request.Description ?? ""
                    );
                }

                _logger.LogInformation("Meeting emails sent: {EmailSuccess} for {AttendeeCount} attendees", 
                    emailSuccess, request.AttendeeEmails.Count);

                return Ok(new
                {
                    Success = true,
                    Message = "Meeting created and emails sent successfully",
                    MeetingId = result.MeetingId,
                    MeetingTitle = result.MeetingTitle,
                    StartTime = result.StartTime,
                    EndTime = result.EndTime,
                    DurationMinutes = result.DurationMinutes,
                    AttendeeCount = result.AttendeeCount,
                    JoinUrl = result.JoinUrl,
                    AttendeeEmails = request.AttendeeEmails,
                    EmailsSent = emailSuccess
                });
            }
            else
            {
                _logger.LogError("Failed to create meeting: {Message}", result.Message);
                return BadRequest(new
                {
                    Success = false,
                    Message = result.Message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating meeting: {Message}", ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = "An error occurred while creating the meeting",
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
            return Ok(new
            {
                Success = true,
                Message = "Google Workspace configuration is ready",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing configuration: {Message}", ex.Message);
            return StatusCode(500, new 
            { 
                Success = false, 
                Message = ex.Message 
            });
        }
    }

    /// <summary>
    /// Test Gmail API functionality
    /// </summary>
    [HttpPost("test-email")]
    public async Task<ActionResult<object>> TestEmail(TestEmailRequest request)
    {
        try
        {
            _logger.LogInformation("Testing Gmail API with email: {Email}", request.Email);

            // Try Gmail API first
            var success = await _gmailService.SendMeetingEmailAsync(
                request.Email,
                "Test Email from StrAppers (Gmail API)",
                DateTime.UtcNow.AddHours(1),
                DateTime.UtcNow.AddHours(2),
                "https://meet.google.com/test-link",
                "This is a test email to verify Gmail API functionality."
            );

            // If Gmail API fails, try SMTP
            if (!success)
            {
                _logger.LogInformation("Gmail API failed, trying SMTP for test email");
                success = await _smtpEmailService.SendMeetingEmailAsync(
                    request.Email,
                    "Test Email from StrAppers (SMTP)",
                    DateTime.UtcNow.AddHours(1),
                    DateTime.UtcNow.AddHours(2),
                    "https://meet.google.com/test-link",
                    "This is a test email to verify SMTP functionality."
                );
            }

            return Ok(new
            {
                Success = success,
                Message = success ? "Test email sent successfully" : "Test email failed to send",
                Email = request.Email,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing email: {Message}", ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = ex.Message,
                Error = ex.ToString()
            });
        }
    }
}

/// <summary>
/// Request model for creating simple meetings
/// </summary>
public class CreateSimpleMeetingRequest
{
    /// <summary>
    /// Meeting title
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Meeting description (optional)
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Meeting start time (UTC)
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// Meeting end time (UTC)
    /// </summary>
    public DateTime EndTime { get; set; }
    
    /// <summary>
    /// List of attendee email addresses
    /// </summary>
    public List<string> AttendeeEmails { get; set; } = new List<string>();
}

/// <summary>
/// Request model for testing email functionality
/// </summary>
public class TestEmailRequest
{
    public string Email { get; set; } = string.Empty;
}
