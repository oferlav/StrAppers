using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using strAppersBackend.Services;
using strAppersBackend.Data;
using strAppersBackend.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;

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

            // Parse and normalize to UTC (Unspecified = local per Trello:LocalTime)
            if (!System.DateTime.TryParse(request.DateTime, out var startTime))
            {
                _logger.LogError("Invalid DateTime format: {DateTime}", request.DateTime);
                return BadRequest(new { Success = false, Message = "Invalid DateTime format" });
            }
            startTime = NormalizeMeetingTimeToUtc(startTime);
            var endTime = startTime.AddMinutes(request.DurationMinutes);

            // Pass UTC to Graph so the meeting is created at the correct time
            var dateTimeUtcString = startTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var serviceRequest = new Services.CreateTeamsMeetingRequest
            {
                Title = request.Title,
                DateTime = dateTimeUtcString,
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

    /// <summary>
    /// Track student access to meeting and redirect to actual Teams meeting URL
    /// Updates database to track attendance and redirects to the Teams meeting
    /// Supports both GET (query parameters for email links) and POST (body for API calls)
    /// </summary>
    [HttpPost("track-and-redirect")]
    [HttpGet("track-and-redirect")]
    public async Task<IActionResult> TrackAndRedirect(
        [FromQuery] string? boardId, 
        [FromQuery] string? studentEmail, 
        [FromBody] TrackAndRedirectRequest? request)
    {
        try
        {
            // Support both GET (query params) and POST (body)
            string actualBoardId = boardId ?? request?.BoardId ?? string.Empty;
            string actualStudentEmail = studentEmail ?? request?.StudentEmail ?? string.Empty;

            _logger.LogInformation("Track and redirect request received for BoardId: {BoardId}, StudentEmail: {StudentEmail}", 
                actualBoardId, actualStudentEmail);

            // Validate request
            if (string.IsNullOrEmpty(actualBoardId) || string.IsNullOrEmpty(actualStudentEmail))
            {
                _logger.LogWarning("Invalid track-and-redirect request: BoardId or StudentEmail is missing");
                return BadRequest(new { Success = false, Message = "BoardId and StudentEmail are required" });
            }

            // Find the BoardMeeting record
            // Priority: 1) Future meetings (soonest first), 2) Most recent past meeting
            var currentTime = DateTime.UtcNow;
            
            // First try to find a future meeting (prefer soonest)
            var boardMeeting = await _context.BoardMeetings
                .Where(bm => 
                    bm.BoardId == actualBoardId && 
                    bm.StudentEmail == actualStudentEmail &&
                    bm.CustomMeetingUrl != null &&
                    bm.MeetingTime >= currentTime)
                .OrderBy(bm => bm.MeetingTime) // Soonest future meeting first
                .FirstOrDefaultAsync();
            
            // If no future meeting found, get the most recent past meeting
            if (boardMeeting == null)
            {
                boardMeeting = await _context.BoardMeetings
                    .Where(bm => 
                        bm.BoardId == actualBoardId && 
                        bm.StudentEmail == actualStudentEmail &&
                        bm.CustomMeetingUrl != null &&
                        bm.MeetingTime < currentTime)
                    .OrderByDescending(bm => bm.MeetingTime) // Most recent past meeting first
                    .FirstOrDefaultAsync();
            }

            if (boardMeeting == null)
            {
                _logger.LogWarning("BoardMeeting not found for BoardId: {BoardId}, StudentEmail: {StudentEmail}", 
                    actualBoardId, actualStudentEmail);
                return NotFound(new { Success = false, Message = "Meeting link not found" });
            }

            _logger.LogInformation("Selected BoardMeeting Id: {Id} for BoardId: {BoardId}, StudentEmail: {StudentEmail}, MeetingTime: {MeetingTime}, Attended: {Attended}, IsFuture: {IsFuture}", 
                boardMeeting.Id, actualBoardId, actualStudentEmail, boardMeeting.MeetingTime, boardMeeting.Attended, boardMeeting.MeetingTime >= currentTime);

            // Check if ActualMeetingUrl exists
            if (string.IsNullOrEmpty(boardMeeting.ActualMeetingUrl))
            {
                _logger.LogWarning("ActualMeetingUrl is missing for BoardMeeting Id: {Id}", boardMeeting.Id);
                return BadRequest(new { Success = false, Message = "Meeting URL not available" });
            }

            // Validate join time - prevent recording if join time is before meeting time
            // EXCEPT if they're on the same date (then it's OK to record)
            var meetingTime = boardMeeting.MeetingTime;
            
            // Check if join time is before meeting time
            if (currentTime < meetingTime)
            {
                // Check if they're on the same date
                var joinDate = currentTime.Date;
                var meetingDate = meetingTime.Date;
                
                if (joinDate != meetingDate)
                {
                    // Join time is before meeting time AND not on the same date - don't record
                    _logger.LogWarning("Join time {JoinTime} is before meeting time {MeetingTime} and not on the same date - skipping attendance recording for BoardMeeting Id: {Id}, StudentEmail: {StudentEmail}", 
                        currentTime, meetingTime, boardMeeting.Id, actualStudentEmail);
                    
                    // Still redirect to the meeting, but don't record attendance
                    if (Request.Method == "GET")
                    {
                        return Redirect(boardMeeting.ActualMeetingUrl);
                    }
                    else
                    {
                        return Ok(new
                        {
                            Success = true,
                            Message = "Join time is before meeting time - attendance not recorded",
                            RedirectUrl = boardMeeting.ActualMeetingUrl,
                            BoardId = actualBoardId,
                            StudentEmail = actualStudentEmail,
                            JoinTime = (DateTime?)null,
                            AttendanceRecorded = false,
                            Reason = "Join time is before meeting time and not on the same date"
                        });
                    }
                }
                else
                {
                    // Same date but before meeting time - OK to record
                    _logger.LogInformation("Join time {JoinTime} is before meeting time {MeetingTime} but on the same date - recording attendance for BoardMeeting Id: {Id}", 
                        currentTime, meetingTime, boardMeeting.Id);
                }
            }

            // Update tracking information
            boardMeeting.Attended = true;
            boardMeeting.JoinTime = currentTime;
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tracking updated for BoardMeeting Id: {Id}, StudentEmail: {StudentEmail}, JoinTime: {JoinTime}, MeetingTime: {MeetingTime}", 
                boardMeeting.Id, actualStudentEmail, boardMeeting.JoinTime, boardMeeting.MeetingTime);

            // For GET requests (email links), redirect directly
            // For POST requests (API calls), return JSON with URL so frontend can handle redirect
            if (Request.Method == "GET")
            {
                return Redirect(boardMeeting.ActualMeetingUrl);
            }
            else
            {
                // POST request - return JSON with redirect URL
                return Ok(new
                {
                    Success = true,
                    Message = "Tracking updated successfully",
                    RedirectUrl = boardMeeting.ActualMeetingUrl,
                    BoardId = actualBoardId,
                    StudentEmail = actualStudentEmail,
                    JoinTime = boardMeeting.JoinTime
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in track-and-redirect: {Message}", ex.Message);
            return StatusCode(500, new { Success = false, Message = "Error processing meeting link" });
        }
    }

    /// <summary>
    /// Get custom meeting URL for a specific student email and boardId
    /// Returns the custom meeting URL without tracking access
    /// </summary>
    [HttpGet("get-custom-url")]
    public async Task<ActionResult<object>> GetCustomMeetingUrl([FromQuery] string boardId, [FromQuery] string studentEmail)
    {
        try
        {
            _logger.LogInformation("Get custom meeting URL request for BoardId: {BoardId}, StudentEmail: {StudentEmail}", 
                boardId, studentEmail);

            // Validate request
            if (string.IsNullOrEmpty(boardId) || string.IsNullOrEmpty(studentEmail))
            {
                _logger.LogWarning("Invalid get-custom-url request: BoardId or StudentEmail is missing");
                return BadRequest(new { Success = false, Message = "BoardId and StudentEmail are required" });
            }

            // Find the BoardMeeting record
            var boardMeeting = await _context.BoardMeetings
                .FirstOrDefaultAsync(bm => 
                    bm.BoardId == boardId && 
                    bm.StudentEmail == studentEmail &&
                    bm.CustomMeetingUrl != null);

            if (boardMeeting == null)
            {
                _logger.LogWarning("BoardMeeting not found for BoardId: {BoardId}, StudentEmail: {StudentEmail}", 
                    boardId, studentEmail);
                return NotFound(new { Success = false, Message = "Meeting link not found for this student and board" });
            }

            // Return the custom meeting URL
            return Ok(new
            {
                Success = true,
                BoardId = boardId,
                StudentEmail = studentEmail,
                CustomMeetingUrl = boardMeeting.CustomMeetingUrl,
                ActualMeetingUrl = boardMeeting.ActualMeetingUrl,
                MeetingTime = boardMeeting.MeetingTime,
                Attended = boardMeeting.Attended,
                JoinTime = boardMeeting.JoinTime
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting custom meeting URL: {Message}", ex.Message);
            return StatusCode(500, new { Success = false, Message = "Error retrieving meeting link" });
        }
    }

    /// <summary>
    /// Create a restricted Teams meeting for an existing board (only invited attendees can join)
    /// Creates custom tracking URLs for each student and saves BoardMeetings records
    /// </summary>
    [HttpPost("create-meeting-smtp-for-board-auth")]
    public async Task<ActionResult<object>> CreateRestrictedTeamsMeetingForBoard(CreateTeamsMeetingForBoardRequest request)
    {
        try
        {
            _logger.LogInformation("Creating restricted Teams meeting for board {BoardId}: {Title} at {DateTime} for {AttendeeCount} attendees", 
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

            // Parse and normalize to UTC (Unspecified = local per Trello:LocalTime)
            if (!System.DateTime.TryParse(request.DateTime, out var startTime))
            {
                _logger.LogError("Invalid DateTime format: {DateTime}", request.DateTime);
                return BadRequest(new { Success = false, Message = "Invalid DateTime format" });
            }
            startTime = NormalizeMeetingTimeToUtc(startTime);
            var endTime = startTime.AddMinutes(request.DurationMinutes);

            // Pass UTC to Graph so the meeting is created at the correct time
            var dateTimeUtcString = startTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var serviceRequest = new Services.CreateTeamsMeetingRequest
            {
                Title = request.Title,
                DateTime = dateTimeUtcString,
                DurationMinutes = request.DurationMinutes,
                Attendees = new List<string>() // Empty list to prevent Exchange from sending invites
            };

            // Create the meeting WITHOUT attendees (we'll send custom URLs via SMTP)
            // This prevents Exchange from automatically sending calendar invites
            var result = await _graphService.CreateTeamsMeetingWithoutAttendeesAsync(serviceRequest);

            if (!result.Success)
            {
                _logger.LogError("Failed to create restricted Teams meeting: {Message}", result.Message);
                return StatusCode(500, result);
            }

            if (string.IsNullOrEmpty(result.JoinUrl))
            {
                _logger.LogError("Teams meeting created but no join URL returned");
                return StatusCode(500, new { Success = false, Message = "Teams meeting created but join URL not available" });
            }

            _logger.LogInformation("Restricted Teams meeting created successfully. Meeting ID: {MeetingId}, JoinUrl: {JoinUrl}", 
                result.MeetingId, result.JoinUrl);

            // Check if we should update ProjectBoard
            // Update if: existing meeting has passed OR new meeting is in the future
            bool shouldUpdateProjectBoard = true;
            var currentTime = DateTime.UtcNow;
            if (projectBoard.NextMeetingTime.HasValue)
            {
                // Only skip if existing meeting is in the future AND new meeting is in the past
                // This prevents accidentally scheduling past meetings when a future meeting already exists
                // But allows rescheduling to an earlier future date
                if (currentTime <= projectBoard.NextMeetingTime.Value && startTime < currentTime)
                {
                    shouldUpdateProjectBoard = false;
                    _logger.LogInformation("Skipping ProjectBoard update: existing NextMeetingTime {ExistingTime} is in the future and new meeting time {NewTime} is in the past", 
                        projectBoard.NextMeetingTime.Value, startTime);
                }
                else if (currentTime > projectBoard.NextMeetingTime.Value)
                {
                    // Existing meeting has passed - always update
                    _logger.LogInformation("Existing NextMeetingTime {ExistingTime} has passed, updating to new meeting time {NewTime}", 
                        projectBoard.NextMeetingTime.Value, startTime);
                }
                else if (startTime >= currentTime)
                {
                    // New meeting is in the future - allow update (even if earlier than existing)
                    _logger.LogInformation("New meeting time {NewTime} is in the future, updating NextMeetingTime from {ExistingTime}", 
                        startTime, projectBoard.NextMeetingTime.Value);
                }
            }

            // Get base URL for constructing custom redirect URLs
            // ALWAYS use configured ApiBaseUrl - never use Request.Scheme://Request.Host because
            // when called internally via HttpClient, Request.Host may reflect the frontend's address
            var configuredBaseUrl = _configuration["ApiBaseUrl"];
            _logger.LogInformation("ApiBaseUrl from configuration: '{ConfiguredUrl}', Request.Host: '{RequestHost}', Request.Scheme: '{RequestScheme}'", 
                configuredBaseUrl ?? "(null)", Request.Host, Request.Scheme);
            
            if (string.IsNullOrEmpty(configuredBaseUrl))
            {
                _logger.LogWarning("ApiBaseUrl is not configured! Custom meeting URLs will not work correctly. Please set ApiBaseUrl in appsettings.json");
                // Fallback to a default, but this should be configured properly
                configuredBaseUrl = "http://localhost:9002"; // Default fallback
            }
            
            // Remove trailing slash if present
            var baseUrl = configuredBaseUrl.TrimEnd('/');
            _logger.LogInformation("Using base URL for custom meeting URLs: {BaseUrl}", baseUrl);
            var trackAndRedirectEndpoint = $"{baseUrl}/api/Teams/use/track-and-redirect";

            // Create or update BoardMeetings records and custom URLs for each student email
            var createdMeetings = new List<object>();
            var updatedMeetings = new List<object>();
            var skippedMeetings = new List<object>();
            var emailResults = new List<(string Email, bool Sent)>();

            foreach (var studentEmail in request.Attendees)
            {
                try
                {
                    // Check if a BoardMeeting record already exists for this BoardId + StudentEmail
                    var existingMeeting = await _context.BoardMeetings
                        .FirstOrDefaultAsync(bm => 
                            bm.BoardId == request.BoardId && 
                            bm.StudentEmail == studentEmail);

                    // Construct custom URL with BoardId and StudentEmail as query parameters (used in both cases)
                    var customUrl = $"{trackAndRedirectEndpoint}?boardId={WebUtility.UrlEncode(request.BoardId)}&studentEmail={WebUtility.UrlEncode(studentEmail)}";
                    var meetingDescription = $"Join this Teams meeting to discuss: {request.Title}. Only invited attendees can join this meeting.";

                    // Only skip if existing meeting is in the future AND new meeting is in the past
                    // This prevents accidentally scheduling past meetings when a future meeting already exists
                    // But allows rescheduling to an earlier future date
                    // If existing meeting has passed, always allow update
                    if (existingMeeting != null && currentTime <= existingMeeting.MeetingTime && startTime < currentTime)
                    {
                        _logger.LogInformation("Skipping BoardMeeting update for {Email}: existing MeetingTime {ExistingTime} is in the future and new meeting time {NewTime} is in the past", 
                            studentEmail, existingMeeting.MeetingTime, startTime);
                        skippedMeetings.Add(new
                        {
                            StudentEmail = studentEmail,
                            ExistingMeetingTime = existingMeeting.MeetingTime,
                            NewMeetingTime = startTime,
                            Reason = "Existing meeting is in the future and new meeting time is in the past"
                        });
                        
                        // Still send email even if we skip the database update
                        var skippedEmailSent = await _smtpEmailService.SendMeetingEmailAsync(
                            studentEmail,
                            request.Title, 
                            startTime, 
                            endTime, 
                            customUrl,
                            meetingDescription
                        );
                        emailResults.Add((studentEmail, skippedEmailSent));
                        continue; // Skip this attendee's BoardMeetings update
                    }

                    BoardMeeting boardMeeting;
                    bool isNewRecord = false;

                    // Determine if we should create new record or update existing
                    // Create NEW record if: (current time > MeetingTime) OR (Attended = true)
                    // Otherwise: UPDATE existing record
                    if (existingMeeting == null)
                    {
                        // No existing record - create new one
                        boardMeeting = new BoardMeeting
                        {
                            BoardId = request.BoardId,
                            MeetingTime = startTime,
                            StudentEmail = studentEmail,
                            CustomMeetingUrl = customUrl,
                            ActualMeetingUrl = result.JoinUrl,
                            Attended = false,
                            JoinTime = null
                        };
                        _context.BoardMeetings.Add(boardMeeting);
                        isNewRecord = true;
                        _logger.LogInformation("Creating NEW BoardMeeting record for StudentEmail: {Email}, CustomUrl: {CustomUrl}", 
                            studentEmail, customUrl);
                    }
                    else if (currentTime > existingMeeting.MeetingTime || existingMeeting.Attended)
                    {
                        // Meeting time has passed OR student already attended - create new record
                        boardMeeting = new BoardMeeting
                        {
                            BoardId = request.BoardId,
                            MeetingTime = startTime,
                            StudentEmail = studentEmail,
                            CustomMeetingUrl = customUrl,
                            ActualMeetingUrl = result.JoinUrl,
                            Attended = false,
                            JoinTime = null
                        };
                        _context.BoardMeetings.Add(boardMeeting);
                        isNewRecord = true;
                        _logger.LogInformation("Creating NEW BoardMeeting record (meeting passed or attended) for StudentEmail: {Email}, CustomUrl: {CustomUrl}", 
                            studentEmail, customUrl);
                    }
                    else
                    {
                        // Update existing record with new meeting details
                        boardMeeting = existingMeeting;
                        boardMeeting.MeetingTime = startTime;
                        boardMeeting.CustomMeetingUrl = customUrl;
                        boardMeeting.ActualMeetingUrl = result.JoinUrl;
                        // Keep existing Attended and JoinTime values
                        _context.BoardMeetings.Update(boardMeeting);
                        isNewRecord = false;
                        _logger.LogInformation("UPDATING existing BoardMeeting record Id: {Id} for StudentEmail: {Email}, CustomUrl: {CustomUrl}", 
                            boardMeeting.Id, studentEmail, customUrl);
                    }

                    await _context.SaveChangesAsync();

                    if (isNewRecord)
                    {
                        _logger.LogInformation("Created BoardMeeting record Id: {Id} for StudentEmail: {Email}, CustomUrl: {CustomUrl}", 
                            boardMeeting.Id, studentEmail, customUrl);
                        createdMeetings.Add(new
                        {
                            BoardMeetingId = boardMeeting.Id,
                            StudentEmail = studentEmail,
                            CustomUrl = customUrl,
                            Action = "Created"
                        });
                    }
                    else
                    {
                        _logger.LogInformation("Updated BoardMeeting record Id: {Id} for StudentEmail: {Email}, CustomUrl: {CustomUrl}", 
                            boardMeeting.Id, studentEmail, customUrl);
                        updatedMeetings.Add(new
                        {
                            BoardMeetingId = boardMeeting.Id,
                            StudentEmail = studentEmail,
                            CustomUrl = customUrl,
                            Action = "Updated"
                        });
                    }

                    // Send individual email with custom URL
                    var emailSent = await _smtpEmailService.SendMeetingEmailAsync(
                        studentEmail,
                        request.Title, 
                        startTime, 
                        endTime, 
                        customUrl, // Use custom URL instead of actual URL
                        meetingDescription
                    );

                    emailResults.Add((studentEmail, emailSent));

                    if (emailSent)
            {
                        _logger.LogInformation("SMTP email sent successfully to {Email} with custom URL", studentEmail);
            }
            else
            {
                        _logger.LogWarning("Failed to send SMTP email to {Email}", studentEmail);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing student email {Email}: {Message}", studentEmail, ex.Message);
                    emailResults.Add((studentEmail, false));
                }
            }

            // Update the NextMeetingTime and NextMeetingUrl fields in the ProjectBoard (only if conditions met)
            if (shouldUpdateProjectBoard)
            {
                projectBoard.NextMeetingTime = startTime;
                projectBoard.NextMeetingUrl = result.JoinUrl;
                projectBoard.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated NextMeetingTime and NextMeetingUrl for board {BoardId} to {MeetingTime} and {MeetingUrl}", 
                    request.BoardId, startTime, result.JoinUrl);
            }
            else
            {
                _logger.LogInformation("Skipped updating ProjectBoard NextMeetingTime - new meeting time is after existing NextMeetingTime");
            }

            var emailsSentCount = emailResults.Count(r => r.Sent);
            var emailsFailedCount = emailResults.Count(r => !r.Sent);

            var message = emailsSentCount == request.Attendees.Count
                ? $"Restricted Teams meeting created, invitations sent via SMTP" + 
                  (shouldUpdateProjectBoard ? ", and NextMeetingTime updated successfully" : " (NextMeetingTime not updated - new meeting is after existing)") +
                  $". BoardMeetings: {createdMeetings.Count} created, {updatedMeetings.Count} updated, {skippedMeetings.Count} skipped. Only invited attendees can join." 
                : $"Restricted Teams meeting created. {emailsSentCount}/{request.Attendees.Count} email invitations sent successfully." +
                  (shouldUpdateProjectBoard ? " NextMeetingTime updated." : " NextMeetingTime not updated - new meeting is after existing.") +
                  $" BoardMeetings: {createdMeetings.Count} created, {updatedMeetings.Count} updated, {skippedMeetings.Count} skipped.";

            return Ok(new
            {
                result.Success,
                Message = message,
                result.MeetingId,
                result.MeetingTitle,
                result.StartTime,
                result.EndTime,
                result.DurationMinutes,
                ActualMeetingUrl = result.JoinUrl,
                BoardId = request.BoardId,
                NextMeetingTime = shouldUpdateProjectBoard ? startTime : projectBoard.NextMeetingTime,
                NextMeetingTimeUpdated = shouldUpdateProjectBoard,
                BoardMeetingsCreated = createdMeetings.Count,
                BoardMeetingsUpdated = updatedMeetings.Count,
                BoardMeetingsSkipped = skippedMeetings.Count,
                EmailsSent = emailsSentCount,
                EmailsFailed = emailsFailedCount,
                EmailResults = emailResults.Select(r => new { Email = r.Email, Sent = r.Sent }).ToList(),
                CreatedMeetings = createdMeetings,
                UpdatedMeetings = updatedMeetings,
                SkippedMeetings = skippedMeetings,
                EmailMethod = "SMTP",
                SmtpEnabled = true,
                SmtpServer = _configuration["Smtp:Host"],
                FromEmail = _configuration["Smtp:FromEmail"],
                RestrictedAccess = true,
                AccessNote = "Only invited attendees can join this meeting. Others will be placed in the lobby."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating restricted Teams meeting for board: {Message}", ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = $"An error occurred while creating the restricted Teams meeting for board: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Create a Teams meeting for an employer/board with custom sender, message, and organization
    /// Similar to create-meeting-smtp-for-board-auth but includes sender information and custom message
    /// </summary>
    [HttpPost("create-meeting-smtp-for-board-employer")]
    public async Task<ActionResult<object>> CreateTeamsMeetingForBoardEmployer(CreateTeamsMeetingForBoardEmployerRequest request)
    {
        try
        {
            _logger.LogInformation("Creating Teams meeting for board {BoardId} from employer: {Title} at {DateTime} for {AttendeeCount} attendees, Sender: {SenderName} <{SenderEmail}>, Organization: {Organization}", 
                request.BoardId, request.Title, request.DateTime, request.Attendees.Count, request.SenderName, request.SenderEmail, request.Organization ?? "N/A");

            // Validate request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate sender email
            if (string.IsNullOrWhiteSpace(request.SenderEmail) || string.IsNullOrWhiteSpace(request.SenderName))
            {
                _logger.LogError("Sender email and name are required");
                return BadRequest(new { Success = false, Message = "Sender email and name are required" });
            }

            // Find the ProjectBoard
            var projectBoard = await _context.ProjectBoards.FindAsync(request.BoardId);
            if (projectBoard == null)
            {
                _logger.LogError("ProjectBoard with ID {BoardId} not found", request.BoardId);
                return NotFound(new { Success = false, Message = $"ProjectBoard with ID {request.BoardId} not found" });
            }

            // Parse and normalize to UTC (Unspecified = local per Trello:LocalTime)
            if (!System.DateTime.TryParse(request.DateTime, out var startTime))
            {
                _logger.LogError("Invalid DateTime format: {DateTime}", request.DateTime);
                return BadRequest(new { Success = false, Message = "Invalid DateTime format" });
            }
            startTime = NormalizeMeetingTimeToUtc(startTime);
            var endTime = startTime.AddMinutes(request.DurationMinutes);

            // Pass UTC to Graph so the meeting is created at the correct time
            var dateTimeUtcString = startTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var serviceRequest = new Services.CreateTeamsMeetingRequest
            {
                Title = request.Title,
                DateTime = dateTimeUtcString,
                DurationMinutes = request.DurationMinutes,
                Attendees = new List<string>() // Empty list to prevent Exchange from sending invites
            };

            // Create the meeting WITHOUT attendees (we'll send custom URLs via SMTP)
            var result = await _graphService.CreateTeamsMeetingWithoutAttendeesAsync(serviceRequest);

            if (!result.Success)
            {
                _logger.LogError("Failed to create Teams meeting: {Message}", result.Message);
                return StatusCode(500, result);
            }

            if (string.IsNullOrEmpty(result.JoinUrl))
            {
                _logger.LogError("Teams meeting created but no join URL returned");
                return StatusCode(500, new { Success = false, Message = "Teams meeting created but join URL not available" });
            }

            _logger.LogInformation("Teams meeting created successfully. Meeting ID: {MeetingId}, JoinUrl: {JoinUrl}", 
                result.MeetingId, result.JoinUrl);

            // Check configuration to determine if we should update database tables
            var updateTeamMeetingsWhenSendInvite = _configuration.GetValue<bool>("EmployerConfig:UpdateTeamMeetingsWhenSendInvite", false);
            _logger.LogInformation("EmployerConfig:UpdateTeamMeetingsWhenSendInvite = {UpdateTeamMeetingsWhenSendInvite}", updateTeamMeetingsWhenSendInvite);

            // Check if we should update ProjectBoard
            // Update if: existing meeting has passed OR new meeting is in the future
            bool shouldUpdateProjectBoard = true;
            var currentTime = DateTime.UtcNow;
            
            // Only check update logic if configuration allows DB updates
            if (updateTeamMeetingsWhenSendInvite && projectBoard.NextMeetingTime.HasValue)
            {
                // Only skip if existing meeting is in the future AND new meeting is also in the future but before existing
                // This prevents accidentally scheduling earlier meetings when a future meeting already exists
                if (currentTime <= projectBoard.NextMeetingTime.Value && startTime < projectBoard.NextMeetingTime.Value)
                {
                    shouldUpdateProjectBoard = false;
                    _logger.LogInformation("Skipping ProjectBoard update: existing NextMeetingTime {ExistingTime} is in the future and new meeting time {NewTime} is before it", 
                        projectBoard.NextMeetingTime.Value, startTime);
                }
                else if (currentTime > projectBoard.NextMeetingTime.Value)
                {
                    // Existing meeting has passed - always update
                    _logger.LogInformation("Existing NextMeetingTime {ExistingTime} has passed, updating to new meeting time {NewTime}", 
                        projectBoard.NextMeetingTime.Value, startTime);
                }
            }
            else if (!updateTeamMeetingsWhenSendInvite)
            {
                // Configuration disables DB updates
                shouldUpdateProjectBoard = false;
                _logger.LogInformation("Skipping ProjectBoard update: UpdateTeamMeetingsWhenSendInvite is false");
            }

            // Combine attendees with sender (sender should also receive invitation)
            var allRecipients = new List<string>(request.Attendees);
            if (!allRecipients.Contains(request.SenderEmail, StringComparer.OrdinalIgnoreCase))
            {
                allRecipients.Add(request.SenderEmail);
                _logger.LogInformation("Added sender {SenderEmail} to recipients list", request.SenderEmail);
            }

            // Create or update BoardMeetings records for each attendee (only if meeting time is valid and config allows)
            var createdMeetings = new List<object>();
            var updatedMeetings = new List<object>();
            var skippedMeetings = new List<object>();
            var emailResults = new List<(string Email, bool Sent)>();

            // Only process BoardMeetings updates if configuration allows
            if (updateTeamMeetingsWhenSendInvite)
            {
                foreach (var attendeeEmail in request.Attendees) // Only process attendees, not sender
                {
                    try
                    {
                        // Check if a BoardMeeting record already exists for this BoardId + attendeeEmail
                        var existingMeeting = await _context.BoardMeetings
                            .FirstOrDefaultAsync(bm => 
                                bm.BoardId == request.BoardId && 
                                bm.StudentEmail == attendeeEmail);

                        // Only skip if existing meeting is in the future AND new meeting is also in the future but before existing
                        // This prevents accidentally scheduling earlier meetings when a future meeting already exists
                        // But if existing meeting has passed, always allow update
                        if (existingMeeting != null && currentTime <= existingMeeting.MeetingTime && startTime < existingMeeting.MeetingTime)
                        {
                            _logger.LogInformation("Skipping BoardMeeting update for {Email}: existing MeetingTime {ExistingTime} is in the future and new meeting time {NewTime} is before it", 
                                attendeeEmail, existingMeeting.MeetingTime, startTime);
                            skippedMeetings.Add(new
                            {
                                StudentEmail = attendeeEmail,
                                ExistingMeetingTime = existingMeeting.MeetingTime,
                                NewMeetingTime = startTime,
                                Reason = "Existing meeting is in the future and new meeting time is before it"
                            });
                            continue; // Skip this attendee's BoardMeetings update
                        }

                        BoardMeeting boardMeeting;
                        bool isNewRecord = false;

                        // Determine if we should create new record or update existing
                        // Create NEW record if: no existing record OR (current time > MeetingTime) OR (Attended = true)
                        // Otherwise: UPDATE existing record
                        if (existingMeeting == null)
                        {
                            // No existing record - create new one
                            boardMeeting = new BoardMeeting
                            {
                                BoardId = request.BoardId,
                                MeetingTime = startTime,
                                StudentEmail = attendeeEmail,
                                CustomMeetingUrl = null, // Employer endpoint doesn't use custom tracking URLs
                                ActualMeetingUrl = result.JoinUrl,
                                Attended = false,
                                JoinTime = null
                            };
                            _context.BoardMeetings.Add(boardMeeting);
                            isNewRecord = true;
                            _logger.LogInformation("Creating NEW BoardMeeting record for StudentEmail: {Email}", attendeeEmail);
                        }
                        else if (currentTime > existingMeeting.MeetingTime || existingMeeting.Attended)
                        {
                            // Meeting time has passed OR student already attended - create new record
                            boardMeeting = new BoardMeeting
                            {
                                BoardId = request.BoardId,
                                MeetingTime = startTime,
                                StudentEmail = attendeeEmail,
                                CustomMeetingUrl = null,
                                ActualMeetingUrl = result.JoinUrl,
                                Attended = false,
                                JoinTime = null
                            };
                            _context.BoardMeetings.Add(boardMeeting);
                            isNewRecord = true;
                            _logger.LogInformation("Creating NEW BoardMeeting record (meeting passed or attended) for StudentEmail: {Email}", attendeeEmail);
                        }
                        else
                        {
                            // Update existing record with new meeting details
                            boardMeeting = existingMeeting;
                            boardMeeting.MeetingTime = startTime;
                            boardMeeting.ActualMeetingUrl = result.JoinUrl;
                            // Keep existing Attended and JoinTime values
                            _context.BoardMeetings.Update(boardMeeting);
                            isNewRecord = false;
                            _logger.LogInformation("UPDATING existing BoardMeeting record Id: {Id} for StudentEmail: {Email}", 
                                boardMeeting.Id, attendeeEmail);
                        }

                        await _context.SaveChangesAsync();

                        if (isNewRecord)
                        {
                            createdMeetings.Add(new
                            {
                                BoardMeetingId = boardMeeting.Id,
                                StudentEmail = attendeeEmail,
                                Action = "Created"
                            });
                        }
                        else
                        {
                            updatedMeetings.Add(new
                            {
                                BoardMeetingId = boardMeeting.Id,
                                StudentEmail = attendeeEmail,
                                Action = "Updated"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing BoardMeeting for attendee {Email}: {Message}", attendeeEmail, ex.Message);
                    }
                }
            }
            else
            {
                _logger.LogInformation("Skipping BoardMeetings updates: UpdateTeamMeetingsWhenSendInvite is false");
            }

            // Send SMTP email invitations to all recipients (including sender)
            foreach (var recipientEmail in allRecipients)
            {
                try
                {
                    var emailSent = await _smtpEmailService.SendMeetingEmailWithSenderAsync(
                        recipientEmail,
                        request.Title,
                        startTime,
                        endTime,
                        result.JoinUrl,
                        request.SenderEmail,
                        request.SenderName,
                        request.Message,
                        request.Organization
                    );

                    emailResults.Add((recipientEmail, emailSent));

                    if (emailSent)
                    {
                        _logger.LogInformation("SMTP email sent successfully to {Email} from {SenderName}", recipientEmail, request.SenderName);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to send SMTP email to {Email}", recipientEmail);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending email to {Email}: {Message}", recipientEmail, ex.Message);
                    emailResults.Add((recipientEmail, false));
                }
            }

            // Update the NextMeetingTime and NextMeetingUrl fields in the ProjectBoard (only if conditions met and config allows)
            if (shouldUpdateProjectBoard && updateTeamMeetingsWhenSendInvite)
            {
                projectBoard.NextMeetingTime = startTime;
                projectBoard.NextMeetingUrl = result.JoinUrl;
                projectBoard.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated NextMeetingTime and NextMeetingUrl for board {BoardId} to {MeetingTime} and {MeetingUrl}", 
                    request.BoardId, startTime, result.JoinUrl);
            }
            else if (!updateTeamMeetingsWhenSendInvite)
            {
                _logger.LogInformation("Skipped updating ProjectBoard NextMeetingTime - UpdateTeamMeetingsWhenSendInvite is false");
            }
            else
            {
                _logger.LogInformation("Skipped updating ProjectBoard NextMeetingTime - new meeting time is after existing NextMeetingTime");
            }

            var emailsSentCount = emailResults.Count(r => r.Sent);
            var emailsFailedCount = emailResults.Count(r => !r.Sent);

            var dbUpdateStatus = updateTeamMeetingsWhenSendInvite 
                ? (shouldUpdateProjectBoard ? ", and NextMeetingTime updated successfully" : " (NextMeetingTime not updated - new meeting is after existing)") +
                  $". BoardMeetings: {createdMeetings.Count} created, {updatedMeetings.Count} updated, {skippedMeetings.Count} skipped."
                : " (Database updates disabled by configuration).";
            
            var message = emailsSentCount == allRecipients.Count
                ? $"Teams meeting created, invitations sent via SMTP from {request.SenderName}" + dbUpdateStatus
                : $"Teams meeting created. {emailsSentCount}/{allRecipients.Count} email invitations sent successfully." + 
                  (updateTeamMeetingsWhenSendInvite 
                    ? (shouldUpdateProjectBoard ? " NextMeetingTime updated." : " NextMeetingTime not updated - new meeting is after existing.") +
                      $" BoardMeetings: {createdMeetings.Count} created, {updatedMeetings.Count} updated, {skippedMeetings.Count} skipped."
                    : " Database updates disabled by configuration.");

            return Ok(new
            {
                result.Success,
                Message = message,
                result.MeetingId,
                result.MeetingTitle,
                result.StartTime,
                result.EndTime,
                result.DurationMinutes,
                ActualMeetingUrl = result.JoinUrl,
                BoardId = request.BoardId,
                NextMeetingTime = shouldUpdateProjectBoard ? startTime : projectBoard.NextMeetingTime,
                NextMeetingTimeUpdated = shouldUpdateProjectBoard,
                SenderEmail = request.SenderEmail,
                SenderName = request.SenderName,
                Organization = request.Organization,
                EmailsSent = emailsSentCount,
                EmailsFailed = emailsFailedCount,
                TotalRecipients = allRecipients.Count,
                EmailResults = emailResults.Select(r => new { Email = r.Email, Sent = r.Sent }).ToList(),
                BoardMeetingsCreated = createdMeetings.Count,
                BoardMeetingsUpdated = updatedMeetings.Count,
                BoardMeetingsSkipped = skippedMeetings.Count,
                CreatedMeetings = createdMeetings,
                UpdatedMeetings = updatedMeetings,
                SkippedMeetings = skippedMeetings,
                EmailMethod = "SMTP",
                SmtpEnabled = true,
                SmtpServer = _configuration["Smtp:Host"],
                FromEmail = _configuration["Smtp:FromEmail"]
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Teams meeting for board employer: {Message}", ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = $"An error occurred while creating the Teams meeting: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Normalize a parsed meeting DateTime to UTC. When Kind is Unspecified, treat as local time
    /// per Trello:LocalTime and convert to UTC (avoids 2-hour shift when client sends local time without Z).
    /// </summary>
    private DateTime NormalizeMeetingTimeToUtc(DateTime parsed)
    {
        if (parsed.Kind == DateTimeKind.Utc)
            return parsed;
        if (parsed.Kind == DateTimeKind.Local)
            return parsed.ToUniversalTime();
        // Unspecified: treat as local time in Trello:LocalTime and convert to UTC
        var localTimeStr = _configuration["Trello:LocalTime"] ?? "GMT+2";
        var offset = strAppersBackend.Services.TrelloBoardScheduleHelper.ParseLocalTimeOffset(localTimeStr);
        var utc = parsed.Subtract(offset);
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
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
/// Request model for tracking and redirecting to meeting
/// </summary>
public class TrackAndRedirectRequest
{
    public string BoardId { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
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

/// <summary>
/// Request model for creating Teams meetings for boards with employer/sender information
/// </summary>
public class CreateTeamsMeetingForBoardEmployerRequest
{
    public string BoardId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DateTime { get; set; } = string.Empty; // Format: "2024-01-15T14:30:00Z"
    public int DurationMinutes { get; set; }
    public List<string> Attendees { get; set; } = new List<string>();
    public string SenderEmail { get; set; } = string.Empty; // Email address of the sender
    public string SenderName { get; set; } = string.Empty; // Display name of the sender
    public string? Message { get; set; } // Optional message from the sender
    public string? Organization { get; set; } // Optional organization name
}
