using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using strAppersBackend.Services;
using strAppersBackend.Data;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]/use")]
public class TeamsController : ControllerBase
{
    private readonly IMicrosoftGraphService _graphService;
    private readonly ISmtpEmailService _smtpEmailService;
    private readonly ILogger<TeamsController> _logger;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;

    public TeamsController(
        IMicrosoftGraphService graphService,
        ISmtpEmailService smtpEmailService,
        ILogger<TeamsController> logger,
        IConfiguration configuration,
        ApplicationDbContext context)
    {
        _graphService = graphService;
        _smtpEmailService = smtpEmailService;
        _logger = logger;
        _configuration = configuration;
        _context = context;
    }

    /// <summary>
    /// Create a Teams meeting and send calendar invitations to attendees
    /// </summary>
    [HttpPost("create-meeting")]
    public async Task<ActionResult<object>> CreateTeamsMeeting(CreateTeamsMeetingControllerRequest request)
    {
        try
        {
            _logger.LogInformation("Creating Teams meeting: {Title} at {DateTime} for {AttendeeCount} attendees", 
                request.Title, request.DateTime, request.Attendees.Count);

            // Validate request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Convert to service request
            var serviceRequest = new Services.CreateTeamsMeetingRequest
            {
                Title = request.Title,
                DateTime = request.DateTime,
                DurationMinutes = request.DurationMinutes,
                Attendees = request.Attendees
            };

            // Create the meeting using the service
            var result = await _graphService.CreateTeamsMeetingAsync(serviceRequest);

            if (result.Success)
            {
                _logger.LogInformation("Teams meeting created successfully. Meeting ID: {MeetingId}", result.MeetingId);
                
                // Step 1: Verify the event was created with correct properties
                var validation = await _graphService.VerifyEventCreationAsync(result.MeetingId);
                _logger.LogInformation("Event validation result: {ValidationMessage}", validation.Message);
                
                // Step 2: Force Exchange to send meeting requests using forward action
                var forwardSuccess = await _graphService.ForwardMeetingInviteAsync(result.MeetingId, request.Attendees);
                
                _logger.LogInformation("Meeting request forwarded to {AttendeeCount} attendees: {ForwardSuccess}", 
                    result.AttendeeCount, forwardSuccess);

                return Ok(new
                {
                    result.Success,
                    Message = forwardSuccess 
                        ? "Teams meeting created, validated, and invites sent via Exchange" 
                        : "Teams meeting created and validated (Exchange may send invites automatically)",
                    result.MeetingId,
                    result.MeetingTitle,
                    result.StartTime,
                    result.EndTime,
                    result.DurationMinutes,
                    result.AttendeeCount,
                    result.JoinUrl,
                    result.Attendees,
                    result.Details,
                    Validation = new
                    {
                        validation.IsOrganizer,
                        validation.ResponseRequested,
                        validation.IsOnlineMeeting,
                        validation.AttendeesValid
                    },
                    InvitesSent = forwardSuccess
                });
            }
            else
            {
                _logger.LogError("Failed to create Teams meeting: {Message}", result.Message);
                return StatusCode(500, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Teams meeting: {Message}", ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = $"An error occurred while creating the Teams meeting: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Create a Teams meeting and send invitations via SMTP email
    /// </summary>
    [HttpPost("create-meeting-smtp")]
    public async Task<ActionResult<object>> CreateTeamsMeetingWithSmtp(CreateTeamsMeetingControllerRequest request)
    {
        try
        {
            _logger.LogInformation("Creating Teams meeting with SMTP: {Title} at {DateTime} for {AttendeeCount} attendees", 
                request.Title, request.DateTime, request.Attendees.Count);

            // Validate request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Convert to service request
            var serviceRequest = new Services.CreateTeamsMeetingRequest
            {
                Title = request.Title,
                DateTime = request.DateTime,
                DurationMinutes = request.DurationMinutes,
                Attendees = request.Attendees
            };

            // Create the meeting using Microsoft Graph WITHOUT attendees (prevents Exchange from sending invites)
            var result = await _graphService.CreateTeamsMeetingWithoutAttendeesAsync(serviceRequest);

            if (!result.Success)
            {
                _logger.LogError("Failed to create Teams meeting: {Message}", result.Message);
                return StatusCode(500, result);
            }

            _logger.LogInformation("Teams meeting created successfully. Meeting ID: {MeetingId}", result.MeetingId);

            // Parse start and end times for email
            if (!System.DateTime.TryParse(request.DateTime, out var startTime))
            {
                _logger.LogError("Invalid DateTime format: {DateTime}", request.DateTime);
                return BadRequest(new { Success = false, Message = "Invalid DateTime format" });
            }

            var endTime = startTime.AddMinutes(request.DurationMinutes);

            // Send SMTP email invitations to all attendees
            _logger.LogInformation("Sending SMTP email invitations to {Count} attendees", request.Attendees.Count);
            
            var emailsSent = await _smtpEmailService.SendBulkMeetingEmailsAsync(
                request.Attendees, 
                request.Title, 
                startTime, 
                endTime, 
                result.JoinUrl ?? "Meeting link not available",
                $"Join this Teams meeting to discuss: {request.Title}"
            );

            if (emailsSent)
            {
                _logger.LogInformation("All SMTP email invitations sent successfully");
            }
            else
            {
                _logger.LogWarning("Some SMTP email invitations failed to send");
            }

            return Ok(new
            {
                result.Success,
                Message = emailsSent 
                    ? "Teams meeting created and invitations sent via SMTP successfully" 
                    : "Teams meeting created but some email invitations failed to send",
                result.MeetingId,
                result.MeetingTitle,
                result.StartTime,
                result.EndTime,
                result.DurationMinutes,
                result.AttendeeCount,
                result.JoinUrl,
                result.Attendees,
                EmailsSent = emailsSent,
                EmailMethod = "SMTP",
                SmtpServer = _configuration["Smtp:Host"],
                FromEmail = _configuration["Smtp:FromEmail"]
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Teams meeting with SMTP: {Message}", ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = $"An error occurred while creating the Teams meeting with SMTP: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Test endpoint to verify Microsoft Graph connectivity
    /// </summary>
    [HttpGet("test-connection")]
    public async Task<ActionResult<object>> TestConnection()
    {
        try
        {
            var isConnected = await _graphService.TestConnectionAsync();
            
            return Ok(new
            {
                Success = isConnected,
                Message = isConnected 
                    ? "Microsoft Graph connection successful" 
                    : "Microsoft Graph connection failed",
                Configured = true,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Microsoft Graph connection: {Message}", ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = $"Error testing connection: {ex.Message}",
                Configured = false
            });
        }
    }

    /// <summary>
    /// Debug endpoint to check configuration values
    /// </summary>
    [HttpGet("debug-config")]
    public ActionResult<object> DebugConfig()
    {
        try
        {
            var config = new
            {
                TenantId = _configuration["MicrosoftGraph:TenantId"],
                ClientId = _configuration["MicrosoftGraph:ClientId"],
                ServiceAccountEmail = _configuration["MicrosoftGraph:ServiceAccountEmail"],
                ServiceAccountUserId = _configuration["MicrosoftGraph:ServiceAccountUserId"],
                AllMicrosoftGraphKeys = _configuration.GetSection("MicrosoftGraph").GetChildren()
                    .ToDictionary(x => x.Key, x => x.Value)
            };
            
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting configuration: {Message}", ex.Message);
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Create a Teams meeting for an existing board and update the NextMeetingTime field
    /// </summary>
    [HttpPost("create-meeting-smtp-for-board")]
    public async Task<ActionResult<object>> CreateTeamsMeetingForBoard(CreateTeamsMeetingForBoardRequest request)
    {
        try
        {
            _logger.LogInformation("Creating Teams meeting for board {BoardId}: {Title} at {DateTime} for {AttendeeCount} attendees", 
                request.BoardId, request.Title, request.DateTime, request.Attendees.Count);

            // Validate request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Find the ProjectBoard
            var projectBoard = await _context.ProjectBoards.FindAsync(request.BoardId);
            if (projectBoard == null)
            {
                _logger.LogError("ProjectBoard with ID {BoardId} not found", request.BoardId);
                return NotFound(new { Success = false, Message = $"ProjectBoard with ID {request.BoardId} not found" });
            }

            // Convert to service request
            var serviceRequest = new Services.CreateTeamsMeetingRequest
            {
                Title = request.Title,
                DateTime = request.DateTime,
                DurationMinutes = request.DurationMinutes,
                Attendees = request.Attendees
            };

            // Create the meeting using Microsoft Graph WITHOUT attendees (prevents Exchange from sending invites)
            var result = await _graphService.CreateTeamsMeetingWithoutAttendeesAsync(serviceRequest);

            if (!result.Success)
            {
                _logger.LogError("Failed to create Teams meeting: {Message}", result.Message);
                return StatusCode(500, result);
            }

            _logger.LogInformation("Teams meeting created successfully. Meeting ID: {MeetingId}", result.MeetingId);

            // Parse start and end times for email
            if (!System.DateTime.TryParse(request.DateTime, out var startTime))
            {
                _logger.LogError("Invalid DateTime format: {DateTime}", request.DateTime);
                return BadRequest(new { Success = false, Message = "Invalid DateTime format" });
            }

            // Ensure the DateTime is UTC for PostgreSQL compatibility
            if (startTime.Kind == DateTimeKind.Unspecified)
            {
                startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            }
            else if (startTime.Kind == DateTimeKind.Local)
            {
                startTime = startTime.ToUniversalTime();
            }

            var endTime = startTime.AddMinutes(request.DurationMinutes);

            // Send SMTP email invitations to all attendees
            _logger.LogInformation("Sending SMTP email invitations to {Count} attendees", request.Attendees.Count);
            
            var emailsSent = await _smtpEmailService.SendBulkMeetingEmailsAsync(
                request.Attendees, 
                request.Title, 
                startTime, 
                endTime, 
                result.JoinUrl ?? "Meeting link not available",
                $"Join this Teams meeting to discuss: {request.Title}"
            );

            if (emailsSent)
            {
                _logger.LogInformation("All SMTP email invitations sent successfully");
            }
            else
            {
                _logger.LogWarning("Some SMTP email invitations failed to send");
            }

            // Update the NextMeetingTime and NextMeetingUrl fields in the ProjectBoard
            projectBoard.NextMeetingTime = startTime;
            projectBoard.NextMeetingUrl = result.JoinUrl;
            projectBoard.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated NextMeetingTime and NextMeetingUrl for board {BoardId} to {MeetingTime} and {MeetingUrl}", 
                request.BoardId, startTime, result.JoinUrl);

            return Ok(new
            {
                result.Success,
                Message = emailsSent 
                    ? "Teams meeting created, invitations sent via SMTP, and NextMeetingTime updated successfully" 
                    : "Teams meeting created and NextMeetingTime updated, but some email invitations failed to send",
                result.MeetingId,
                result.MeetingTitle,
                result.StartTime,
                result.EndTime,
                result.DurationMinutes,
                result.AttendeeCount,
                result.JoinUrl,
                result.Attendees,
                BoardId = request.BoardId,
                NextMeetingTime = startTime,
                EmailsSent = emailsSent,
                EmailMethod = "SMTP",
                SmtpServer = _configuration["Smtp:Host"],
                FromEmail = _configuration["Smtp:FromEmail"]
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Teams meeting for board: {Message}", ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = $"An error occurred while creating the Teams meeting for board: {ex.Message}"
            });
        }
    }
}

/// <summary>
/// Request model for creating Teams meetings via controller
/// </summary>
public class CreateTeamsMeetingControllerRequest
{
    public string Title { get; set; } = string.Empty;
    public string DateTime { get; set; } = string.Empty; // Format: "2024-01-15T14:30:00Z"
    public int DurationMinutes { get; set; }
    public List<string> Attendees { get; set; } = new List<string>();
}

/// <summary>
/// Request model for creating Teams meetings for existing boards
/// </summary>
public class CreateTeamsMeetingForBoardRequest
{
    public string BoardId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DateTime { get; set; } = string.Empty; // Format: "2024-01-15T14:30:00Z"
    public int DurationMinutes { get; set; }
    public List<string> Attendees { get; set; } = new List<string>();
}
