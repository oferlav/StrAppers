using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InstitutesController : ControllerBase
{
    private const int DefaultMeetingDurationMinutes = 60;

    private readonly ApplicationDbContext _context;
    private readonly ILogger<InstitutesController> _logger;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ISmtpEmailService _smtpEmailService;
    private readonly IConfiguration _configuration;

    public InstitutesController(
        ApplicationDbContext context,
        ILogger<InstitutesController> logger,
        IPasswordHasherService passwordHasher,
        ISmtpEmailService smtpEmailService,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _passwordHasher = passwordHasher;
        _smtpEmailService = smtpEmailService;
        _configuration = configuration;
    }

    /// <summary>
    /// Send the same SMTP meeting invitation email (HTML + .ics) as BoardRoom scheduling, using an existing
    /// <see cref="ProjectBoard"/> next meeting. Validates the institute contact email and that URL/time match the board row.
    /// </summary>
    [HttpPost("use/join-meeting")]
    public async Task<ActionResult<object>> JoinMeetingEmail(InstituteJoinMeetingRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(request.BoardId) ||
                string.IsNullOrWhiteSpace(request.NextMeetingUrl) ||
                string.IsNullOrWhiteSpace(request.NextMeetingTime) ||
                string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { Success = false, Message = "boardId, nextMeetingUrl, nextMeetingTime, and email are required" });
            }

            var email = request.Email.Trim();
            var teacher = await _context.Teachers
                .AsNoTracking()
                .Include(t => t.Institute)
                .FirstOrDefaultAsync(t => t.Email.ToLower() == email.ToLower());

            if (teacher == null || teacher.Institute == null)
            {
                _logger.LogWarning("join-meeting: teacher not found for email {Email}", email);
                return Unauthorized(new { Success = false, Message = "Teacher account not found for this email" });
            }

            if (!teacher.Institute.IsActive)
            {
                return Unauthorized(new { Success = false, Message = "This institute account is inactive" });
            }

            var projectBoard = await _context.ProjectBoards.FindAsync(request.BoardId);
            if (projectBoard == null)
            {
                return NotFound(new { Success = false, Message = "Board not found" });
            }

            if (string.IsNullOrWhiteSpace(projectBoard.NextMeetingUrl) || !projectBoard.NextMeetingTime.HasValue)
            {
                return BadRequest(new { Success = false, Message = "No upcoming meeting is stored for this board" });
            }

            var urlRequest = request.NextMeetingUrl.Trim();
            var urlDb = projectBoard.NextMeetingUrl.Trim();
            if (!string.Equals(urlRequest, urlDb, StringComparison.Ordinal))
            {
                _logger.LogWarning("join-meeting: URL mismatch for board {BoardId}", request.BoardId);
                return BadRequest(new { Success = false, Message = "Meeting URL does not match the current board meeting" });
            }

            if (!DateTime.TryParse(request.NextMeetingTime, out var parsedStart))
            {
                return BadRequest(new { Success = false, Message = "Invalid nextMeetingTime format" });
            }

            var startTime = NormalizeMeetingTimeToUtc(parsedStart);
            var expectedTime = projectBoard.NextMeetingTime!.Value;
            if (expectedTime.Kind == DateTimeKind.Unspecified)
                expectedTime = DateTime.SpecifyKind(expectedTime, DateTimeKind.Utc);
            var delta = Math.Abs((startTime - expectedTime.ToUniversalTime()).TotalMinutes);
            if (delta > 5)
            {
                _logger.LogWarning("join-meeting: time mismatch for board {BoardId}, delta {Delta} min", request.BoardId, delta);
                return BadRequest(new { Success = false, Message = "Meeting time does not match the current board meeting" });
            }

            var title = string.IsNullOrWhiteSpace(projectBoard.NextMeetingTitle)
                ? "Team Meeting"
                : projectBoard.NextMeetingTitle.Trim();
            var meetingDescription =
                $"Join this Teams meeting to discuss: {title}. Only invited attendees can join this meeting.";
            var endTime = startTime.AddMinutes(DefaultMeetingDurationMinutes);

            var sent = await _smtpEmailService.SendMeetingEmailAsync(
                email,
                title,
                startTime,
                endTime,
                urlDb,
                meetingDescription);

            if (!sent)
            {
                _logger.LogError("join-meeting: SMTP failed for {Email}, board {BoardId}", email, request.BoardId);
                return StatusCode(500, new { Success = false, Message = "Failed to send email. Check SMTP configuration." });
            }

            projectBoard.NextMeetingTeacherAttendance = true;
            projectBoard.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("join-meeting: invitation email sent to {Email} for board {BoardId}", email, request.BoardId);
            return Ok(new
            {
                Success = true,
                Message = "Meeting invitation sent to your email",
                BoardId = request.BoardId,
                Email = email,
                NextMeetingTeacherAttendance = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "join-meeting error: {Message}", ex.Message);
            return StatusCode(500, new { Success = false, Message = "An error occurred while sending the invitation" });
        }
    }

    /// <summary>
    /// Match <see cref="TeamsController"/> normalization for client-supplied meeting times.
    /// </summary>
    private DateTime NormalizeMeetingTimeToUtc(DateTime parsed)
    {
        if (parsed.Kind == DateTimeKind.Utc)
            return parsed;
        if (parsed.Kind == DateTimeKind.Local)
            return parsed.ToUniversalTime();
        var localTimeStr = _configuration["Trello:LocalTime"] ?? "GMT+2";
        var offset = TrelloBoardScheduleHelper.ParseLocalTimeOffset(localTimeStr);
        var utc = parsed.Subtract(offset);
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
    }

    /// <summary>
    /// Get an institute by contact email (safe shape; no password hash).
    /// </summary>
    [HttpGet("use/{email}")]
    public async Task<ActionResult<object>> GetInstituteByEmail(string email)
    {
        try
        {
            _logger.LogInformation("Getting institute by email {Email}", email);

            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new { Success = false, Message = "Email parameter is required" });
            }

            var normalized = email.Trim();
            var teacherRow = await _context.Teachers
                .AsNoTracking()
                .Include(t => t.Institute)
                .FirstOrDefaultAsync(t => t.Email.ToLower() == normalized.ToLower());

            if (teacherRow?.Institute == null)
            {
                _logger.LogWarning("Teacher with email {Email} not found", email);
                return NotFound(new
                {
                    Success = false,
                    Message = $"Teacher with email '{email}' not found"
                });
            }

            var institute = teacherRow.Institute;
            var studentCount = await _context.Students.CountAsync(s => s.InstituteId == institute.Id);

            return Ok(new
            {
                Success = true,
                institute.Id,
                institute.Name,
                institute.Description,
                institute.Website,
                ContactEmail = institute.ContactEmail,
                institute.Phone,
                institute.Address,
                institute.Type,
                institute.State,
                institute.Country,
                institute.IsActive,
                institute.Logo,
                institute.CreatedAt,
                institute.UpdatedAt,
                StudentCount = studentCount,
                Teacher = new
                {
                    teacherRow.Id,
                    teacherRow.FirstName,
                    teacherRow.LastName,
                    teacherRow.Email,
                    teacherRow.InstituteId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving institute with email {Email}: {Message}", email, ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = "An error occurred while retrieving the institute"
            });
        }
    }

    /// <summary>
    /// Login — verifies teacher email and password against <see cref="Teacher.PasswordHash"/>.
    /// </summary>
    [HttpPost("use/login")]
    public async Task<ActionResult<object>> LoginInstitute(InstituteLoginRequest request)
    {
        try
        {
            _logger.LogInformation("Login attempt for teacher with email {Email}", request.Email);

            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                _logger.LogWarning("Login attempt with missing email or password");
                return BadRequest(new { Success = false, Message = "Email and password are required" });
            }

            var email = request.Email.Trim();
            var teacher = await _context.Teachers
                .Include(t => t.Institute)
                .FirstOrDefaultAsync(t => t.Email.ToLower() == email.ToLower());

            if (teacher == null || teacher.Institute == null)
            {
                _logger.LogWarning("Login attempt failed: Teacher with email {Email} not found", request.Email);
                return Unauthorized(new { Success = false, Message = "Invalid email or password" });
            }

            if (!teacher.Institute.IsActive)
            {
                _logger.LogWarning("Login attempt failed: Institute {Id} is inactive", teacher.InstituteId);
                return Unauthorized(new { Success = false, Message = "This account is inactive" });
            }

            if (string.IsNullOrWhiteSpace(teacher.PasswordHash))
            {
                _logger.LogWarning("Login attempt failed: Teacher with email {Email} has no password set", request.Email);
                return Unauthorized(new { Success = false, Message = "Password not set for this account" });
            }

            if (!_passwordHasher.VerifyPassword(teacher.PasswordHash, request.Password))
            {
                _logger.LogWarning("Login attempt failed: Invalid password for teacher with email {Email}", request.Email);
                return Unauthorized(new { Success = false, Message = "Invalid email or password" });
            }

            _logger.LogInformation("Login successful for teacher with email {Email}", request.Email);

            var institute = teacher.Institute;
            return Ok(new
            {
                Success = true,
                Message = "Login successful",
                Institute = new
                {
                    institute.Id,
                    institute.Name,
                    ContactEmail = institute.ContactEmail
                },
                Teacher = new
                {
                    teacher.Id,
                    teacher.FirstName,
                    teacher.LastName,
                    teacher.Email,
                    teacher.InstituteId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during teacher login for email {Email}", request.Email);
            return StatusCode(500, new { Success = false, Message = "An error occurred during login" });
        }
    }
}

public class InstituteJoinMeetingRequest
{
    [Required]
    public string BoardId { get; set; } = string.Empty;

    [Required]
    public string NextMeetingUrl { get; set; } = string.Empty;

    /// <summary>ISO 8601 (e.g. from JavaScript <c>Date.prototype.toISOString()</c>).</summary>
    [Required]
    public string NextMeetingTime { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class InstituteLoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;
}
