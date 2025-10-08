using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]/use")]
public class StudentMeetingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IGoogleCalendarService _calendarService;
    private readonly IGmailService _gmailService;
    private readonly ILogger<StudentMeetingsController> _logger;

    public StudentMeetingsController(
        ApplicationDbContext context,
        IGoogleCalendarService calendarService,
        IGmailService gmailService,
        ILogger<StudentMeetingsController> logger)
    {
        _context = context;
        _calendarService = calendarService;
        _gmailService = gmailService;
        _logger = logger;
    }

    /// <summary>
    /// Create a meeting for a specific project team
    /// </summary>
    [HttpPost("create-project-meeting")]
    public async Task<ActionResult<object>> CreateProjectMeeting(CreateProjectMeetingRequest request)
    {
        try
        {
            _logger.LogInformation("Creating project meeting for Project {ProjectId}: {Title}", 
                request.ProjectId, request.Title);

            // Get project
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

            if (project == null)
            {
                return NotFound($"Project with ID {request.ProjectId} not found.");
            }

            // Get students assigned to this project
            var students = await _context.Students
                .Where(s => s.ProjectId == request.ProjectId)
                .ToListAsync();

            var studentEmails = students.Select(s => s.Email).ToList();
            
            if (!studentEmails.Any())
            {
                return BadRequest("No students found for this project.");
            }

            // Create Google Meet meeting
            var meetingRequest = new CreateGoogleMeetingRequest
            {
                Title = $"{project.Title} - {request.Title}",
                Description = $"{request.Description}\n\nProject: {project.Title}\nProject Description: {project.Description}",
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Attendees = studentEmails
            };

            var result = await _calendarService.CreateMeetingAsync(meetingRequest);

            if (result.Success)
            {
                // Send email notifications
                var emailSuccess = await _gmailService.SendBulkMeetingEmailsAsync(
                    studentEmails,
                    result.MeetingTitle ?? meetingRequest.Title,
                    result.StartTime ?? request.StartTime,
                    result.EndTime ?? request.EndTime,
                    result.JoinUrl ?? "",
                    result.Details ?? request.Description
                );

                _logger.LogInformation("Project meeting created successfully: {MeetingId} for {StudentCount} students", 
                    result.MeetingId, studentEmails.Count);

                return Ok(new
                {
                    Success = true,
                    Message = "Project meeting created and emails sent successfully",
                    ProjectId = request.ProjectId,
                    ProjectTitle = project.Title,
                    MeetingId = result.MeetingId,
                    MeetingTitle = result.MeetingTitle,
                    StartTime = result.StartTime,
                    EndTime = result.EndTime,
                    DurationMinutes = result.DurationMinutes,
                    AttendeeCount = result.AttendeeCount,
                    JoinUrl = result.JoinUrl,
                    StudentEmails = studentEmails,
                    EmailSent = emailSuccess
                });
            }
            else
            {
                _logger.LogError("Failed to create project meeting: {Message}", result.Message);
                return BadRequest(new
                {
                    Success = false,
                    Message = result.Message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project meeting: {Message}", ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = "An error occurred while creating the project meeting",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Create a meeting for specific students by their IDs
    /// </summary>
    [HttpPost("create-student-meeting")]
    public async Task<ActionResult<object>> CreateStudentMeeting(CreateStudentMeetingRequest request)
    {
        try
        {
            _logger.LogInformation("Creating student meeting: {Title} for {StudentCount} students", 
                request.Title, request.StudentIds.Count);

            // Get students by IDs
            var students = await _context.Students
                .Where(s => request.StudentIds.Contains(s.Id))
                .ToListAsync();

            if (!students.Any())
            {
                return NotFound("No students found with the provided IDs.");
            }

            var studentEmails = students.Select(s => s.Email).ToList();

            // Create Google Meet meeting
            var meetingRequest = new CreateGoogleMeetingRequest
            {
                Title = request.Title,
                Description = request.Description,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Attendees = studentEmails
            };

            var result = await _calendarService.CreateMeetingAsync(meetingRequest);

            if (result.Success)
            {
                // Send email notifications
                var emailSuccess = await _gmailService.SendBulkMeetingEmailsAsync(
                    studentEmails,
                    result.MeetingTitle ?? request.Title,
                    result.StartTime ?? request.StartTime,
                    result.EndTime ?? request.EndTime,
                    result.JoinUrl ?? "",
                    result.Details ?? request.Description
                );

                _logger.LogInformation("Student meeting created successfully: {MeetingId} for {StudentCount} students", 
                    result.MeetingId, studentEmails.Count);

                return Ok(new
                {
                    Success = true,
                    Message = "Student meeting created and emails sent successfully",
                    MeetingId = result.MeetingId,
                    MeetingTitle = result.MeetingTitle,
                    StartTime = result.StartTime,
                    EndTime = result.EndTime,
                    DurationMinutes = result.DurationMinutes,
                    AttendeeCount = result.AttendeeCount,
                    JoinUrl = result.JoinUrl,
                    StudentEmails = studentEmails,
                    EmailSent = emailSuccess
                });
            }
            else
            {
                _logger.LogError("Failed to create student meeting: {Message}", result.Message);
                return BadRequest(new
                {
                    Success = false,
                    Message = result.Message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating student meeting: {Message}", ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = "An error occurred while creating the student meeting",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get all students for a project (for meeting creation)
    /// </summary>
    [HttpGet("project/{projectId}/students")]
    public async Task<ActionResult<object>> GetProjectStudents(int projectId)
    {
        try
        {
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found.");
            }

            // Get students assigned to this project
            var projectStudents = await _context.Students
                .Where(s => s.ProjectId == projectId)
                .ToListAsync();

            var students = projectStudents.Select(s => new
            {
                Id = s.Id,
                FirstName = s.FirstName,
                LastName = s.LastName,
                Email = s.Email,
                IsAdmin = s.IsAdmin
            }).ToList();

            return Ok(new
            {
                ProjectId = projectId,
                ProjectTitle = project.Title,
                StudentCount = students.Count,
                Students = students
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project students: {Message}", ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = "An error occurred while getting project students",
                Error = ex.Message
            });
        }
    }
}

public class CreateProjectMeetingRequest
{
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class CreateStudentMeetingRequest
{
    public List<int> StudentIds { get; set; } = new List<int>();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}