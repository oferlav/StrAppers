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

    /// <summary>
    /// Invite a new teacher to the institute: creates the Teacher row (no password yet)
    /// and sends them a one-time invite link by email.
    /// </summary>
    [HttpPost("{id}/teachers/invite")]
    public async Task<ActionResult<object>> InviteTeacher(int id, [FromBody] InviteTeacherRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.FirstName) ||
                string.IsNullOrWhiteSpace(request.LastName))
                return BadRequest(new { Success = false, Message = "FirstName, LastName and Email are required" });

            var institute = await _context.Institutes.FindAsync(id);
            if (institute == null || !institute.IsActive)
                return NotFound(new { Success = false, Message = "Institute not found" });

            var email = request.Email.Trim().ToLower();

            // Prevent duplicate teacher emails
            var exists = await _context.Teachers.AnyAsync(t => t.Email.ToLower() == email);
            if (exists)
                return Conflict(new { Success = false, Message = "A teacher with this email already exists" });

            // Create teacher without a password — invite will complete registration
            var teacher = new Teacher
            {
                InstituteId = id,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = email,
                CreatedAt = DateTime.UtcNow
            };
            _context.Teachers.Add(teacher);
            await _context.SaveChangesAsync();

            // Generate a secure one-time token (48h expiry)
            var rawToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64))
                           .Replace("+", "-").Replace("/", "_").TrimEnd('=');

            var inviteToken = new TeacherInviteToken
            {
                TeacherId = teacher.Id,
                Token = rawToken,
                ExpiresAt = DateTime.UtcNow.AddHours(48),
                CreatedAt = DateTime.UtcNow
            };
            _context.TeacherInviteTokens.Add(inviteToken);
            await _context.SaveChangesAsync();

            // Build invite link pointing to frontend
            var frontendBase = (_configuration["GoogleAuth:FrontendBaseUrl"]
                                ?? _configuration["GitHub:FrontendBaseUrl"]
                                ?? "https://skill-in.com").TrimEnd('/');
            var inviteLink = $"{frontendBase}/AcceptTeacherInvite?token={rawToken}";

            var emailBody =
                $"Hi {teacher.FirstName},\n\n" +
                $"You've been invited to join {institute.Name} on Skill-in as a staff member.\n\n" +
                $"Click the link below to set your password and activate your account (link expires in 48 hours):\n\n" +
                $"{inviteLink}\n\n" +
                $"If you did not expect this invitation, you can ignore this email.\n\n" +
                $"The Skill-in Team";

            var sent = await _smtpEmailService.SendPlainEmailAsync(
                teacher.Email,
                $"You're invited to join {institute.Name} on Skill-in",
                emailBody);

            _logger.LogInformation(
                "Teacher invite sent: teacherId={TeacherId}, email={Email}, sent={Sent}",
                teacher.Id, teacher.Email, sent);

            return Ok(new
            {
                Success = true,
                Message = sent
                    ? "Invitation sent successfully"
                    : "Teacher created but email delivery failed — check SMTP config",
                EmailSent = sent,
                Teacher = new { teacher.Id, teacher.FirstName, teacher.LastName, teacher.Email }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inviting teacher to institute {InstituteId}", id);
            return StatusCode(500, new { Success = false, Message = "An error occurred while inviting the teacher" });
        }
    }

    /// <summary>
    /// Accept an invite token and set the teacher's password, completing registration.
    /// </summary>
    [HttpPost("use/accept-invite")]
    public async Task<ActionResult<object>> AcceptInvite([FromBody] AcceptInviteRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { Success = false, Message = "Token and password are required" });

            if (request.Password.Length < 8)
                return BadRequest(new { Success = false, Message = "Password must be at least 8 characters" });

            var inviteToken = await _context.TeacherInviteTokens
                .Include(t => t.Teacher)
                    .ThenInclude(t => t.Institute)
                .FirstOrDefaultAsync(t => t.Token == request.Token);

            if (inviteToken == null)
                return NotFound(new { Success = false, Message = "Invalid or expired invite link" });

            if (inviteToken.UsedAt != null)
                return BadRequest(new { Success = false, Message = "This invite link has already been used" });

            if (inviteToken.ExpiresAt < DateTime.UtcNow)
                return BadRequest(new { Success = false, Message = "This invite link has expired. Please ask your admin to resend the invitation." });

            var teacher = inviteToken.Teacher;
            if (teacher?.Institute == null || !teacher.Institute.IsActive)
                return BadRequest(new { Success = false, Message = "Account no longer active" });

            // Set password and mark token used
            teacher.PasswordHash = _passwordHasher.HashPassword(request.Password);
            teacher.UpdatedAt = DateTime.UtcNow;
            inviteToken.UsedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Teacher {TeacherId} accepted invite and set password", teacher.Id);

            return Ok(new
            {
                Success = true,
                Message = "Password set successfully. You can now log in.",
                Institute = new { teacher.Institute.Id, teacher.Institute.Name },
                Teacher = new { teacher.Id, teacher.FirstName, teacher.LastName, teacher.Email, teacher.InstituteId }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting teacher invite");
            return StatusCode(500, new { Success = false, Message = "An error occurred while activating your account" });
        }
    }

    /// <summary>
    /// List all teachers for an institute (safe shape, no password hash).
    /// </summary>
    [HttpGet("{id}/teachers")]
    public async Task<ActionResult<object>> GetTeachers(int id)
    {
        try
        {
            var institute = await _context.Institutes.FindAsync(id);
            if (institute == null) return NotFound(new { Success = false, Message = "Institute not found" });

            var teachers = await _context.Teachers
                .Where(t => t.InstituteId == id)
                .OrderBy(t => t.FirstName)
                .Select(t => new
                {
                    t.Id,
                    t.FirstName,
                    t.LastName,
                    t.Email,
                    t.InstituteId,
                    t.CreatedAt,
                    HasPassword = t.PasswordHash != null
                })
                .ToListAsync();

            return Ok(new { Success = true, Teachers = teachers });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching teachers for institute {InstituteId}", id);
            return StatusCode(500, new { Success = false, Message = "An error occurred" });
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

public class InviteTeacherRequest
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
}

public class AcceptInviteRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;
}
