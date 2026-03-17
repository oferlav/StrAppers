using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using strAppersBackend.Data;
using strAppersBackend.Services;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]/use")]
public class EmailController : ControllerBase
{
    private const string DefaultNotifyApplicantTitle = "Thanks for applying to join a Skill-in project";
    private const string EmailLogoFileName = "logo.png";
    private const string EmailLogoPath = "assets/logo.png";

    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ISmtpEmailService _smtpEmailService;
    private readonly ILogger<EmailController> _logger;

    public EmailController(ApplicationDbContext context, IWebHostEnvironment env, IConfiguration configuration, ISmtpEmailService smtpEmailService, ILogger<EmailController> logger)
    {
        _context = context;
        _env = env;
        _configuration = configuration;
        _smtpEmailService = smtpEmailService;
        _logger = logger;
    }

    /// <summary>Fallback logo URL: backend's ApiBaseUrl/assets/logo.png (dev and prod use their own API URL).</summary>
    private string GetFallbackLogoUrl()
    {
        var baseUrl = _configuration["ApiBaseUrl"]?.TrimEnd('/');
        if (!string.IsNullOrEmpty(baseUrl))
            return baseUrl + "/" + EmailLogoPath;
        return "https://www.skill-in.com/logo.png"; // legacy fallback when ApiBaseUrl not set
    }

    /// <summary>
    /// Sends a notification email to an applicant using the same SMTP credentials as meeting emails.
    /// emailTitle and emailBody are optional; when omitted, a default thanks-for-applying message is used.
    /// When using the default body, the student's first name is taken from the Students table by studentEmail.
    /// </summary>
    [HttpPost("notify-applicant")]
    public async Task<ActionResult<object>> NotifyApplicant([FromBody] NotifyApplicantRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required.");

        if (string.IsNullOrWhiteSpace(request.StudentEmail))
            return BadRequest("studentEmail is required.");

        var title = string.IsNullOrWhiteSpace(request.EmailTitle) ? DefaultNotifyApplicantTitle : request.EmailTitle.Trim();
        string body;
        if (!string.IsNullOrWhiteSpace(request.EmailBody))
        {
            body = request.EmailBody.Trim();
        }
        else
        {
            var student = await _context.Students
                .AsNoTracking()
                .Where(s => s.Email == request.StudentEmail.Trim())
                .Select(s => new { s.FirstName })
                .FirstOrDefaultAsync();
            var firstName = student?.FirstName?.Trim() ?? "there";
            // Always use URL for logo (Gmail and many clients strip data URI images; backend serves at /assets/logo.png)
            var logoUrl = GetFallbackLogoUrl();
            _logger.LogInformation("Notify-applicant email logo URL: {Url}", logoUrl);
            body = GetDefaultNotifyApplicantBody(firstName, logoUrl);
        }

        var sent = await _smtpEmailService.SendPlainEmailAsync(
            request.StudentEmail.Trim(),
            title,
            body);

        if (!sent)
        {
            _logger.LogWarning("Failed to send notify-applicant email to {Email}", request.StudentEmail);
            return StatusCode(500, new { message = "Failed to send email." });
        }

        return Ok(new { message = "Email sent successfully.", to = request.StudentEmail });
    }

    /// <summary>Reads Assets/logo.png and returns a data URI for inline embedding, or null if file is missing.</summary>
    private string? GetInlineLogoDataUri()
    {
        var path = Path.Combine(_env.ContentRootPath, "Assets", EmailLogoFileName);
        _logger.LogInformation(
            "Notify-applicant default body: looking for logo at path (ContentRootPath={ContentRoot}, path={Path})",
            _env.ContentRootPath, path);
        if (!System.IO.File.Exists(path))
        {
            _logger.LogWarning("Email logo file not found at {Path}, email will be sent without inline logo.", path);
            return null;
        }
        try
        {
            var bytes = System.IO.File.ReadAllBytes(path);
            var base64 = Convert.ToBase64String(bytes);
            _logger.LogInformation(
                "Inline logo embedded successfully from {Path}, size {SizeBytes} bytes (data URI length {DataUriLength}).",
                path, bytes.Length, "data:image/png;base64,".Length + base64.Length);
            return "data:image/png;base64," + base64;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read email logo from {Path}.", path);
            return null;
        }
    }

    /// <summary>Default body uses celebration email template with Skill-in thanks-for-applying copy. logoSource is either inline data URI or fallback URL.</summary>
    private static string GetDefaultNotifyApplicantBody(string firstName, string? logoSource)
    {
        var safeName = WebUtility.HtmlEncode(firstName);
        var logoImg = string.IsNullOrEmpty(logoSource)
            ? ""
            : $@"<p style=""text-align: center; margin: 20px 0 0;""><img src=""{logoSource}"" alt=""Skill-in"" style=""max-width: 180px; height: auto; display: inline-block;"" /></p>";
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Thanks for applying</title>
    <style>
        body {{ margin: 0; padding: 0; font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; background-color: #f4f4f9; }}
        .email-container {{ max-width: 600px; margin: 20px auto; background: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.1); }}
        .header {{ padding: 40px 20px; text-align: center; background: #6c5ce7; color: white; position: relative; overflow: hidden; }}
        .content {{ padding: 40px; text-align: left; color: #2d3436; line-height: 1.6; }}
        .button {{ display: inline-block; padding: 15px 30px; margin-top: 20px; background-color: #00b894; color: white; text-decoration: none; border-radius: 25px; font-weight: bold; }}
        .confetti {{ position: absolute; width: 8px; height: 8px; top: -10px; z-index: 1; opacity: 0.8; animation: fall linear infinite; }}
        @keyframes fall {{ 0% {{ transform: translateY(0) rotate(0deg); opacity: 1; }} 100% {{ transform: translateY(200px) rotate(360deg); opacity: 0; }} }}
        .c1 {{ left: 10%; background: #fab1a0; animation-duration: 3s; }}
        .c2 {{ left: 30%; background: #55efc4; animation-duration: 4s; animation-delay: 1s; }}
        .c3 {{ left: 50%; background: #ffeaa7; animation-duration: 2.5s; animation-delay: 0.5s; }}
        .c4 {{ left: 70%; background: #81ecec; animation-duration: 5s; animation-delay: 2s; }}
        .c5 {{ left: 90%; background: #a29bfe; animation-duration: 3.5s; animation-delay: 1.5s; }}
    </style>
</head>
<body>
    <div class=""email-container"">
        <div class=""header"">
            <div class=""confetti c1""></div>
            <div class=""confetti c2""></div>
            <div class=""confetti c3""></div>
            <div class=""confetti c4""></div>
            <div class=""confetti c5""></div>
            <h1 style=""margin: 0; position: relative; z-index: 2;"">Thanks for applying!</h1>
            <p style=""margin: 10px 0 0; font-size: 1.1em; opacity: 0.9;"">We're glad you're here.</p>
        </div>
        <div class=""content"">
            <p>Hi {safeName},</p>
            <p>Thank you for applying to participate in a project on Skill-in.</p>
            <p>First of all, <strong>congratulations</strong> on taking this step. Choosing to join a project through Skill-in means you're stepping into a journey of learning, building, and collaborating with others who are excited to create something meaningful.</p>
            <p>Our team is currently working on forming project teams by bringing together members with different roles and strengths. We carefully match participants so each team has the right mix of skills to make the project successful.</p>
            <p>As soon as your team is ready, we'll notify you and share all the next steps.</p>
            <p>In the meantime, we appreciate your initiative and your decision to be part of the Skill-in community. We're excited to have you with us and look forward to the journey ahead.</p>
            <p>Best regards,<br><strong>The Skill-in Team</strong></p>
            {logoImg}
            <p style=""text-align: center; margin-top: 24px;""><a href=""https://skill-in.com"" class=""button"">Visit Skill-in</a></p>
        </div>
        <div style=""background: #dfe6e9; padding: 20px; text-align: center; font-size: 12px; color: #636e72;"">
            &copy; 2026 Skill-in<br>
            Where talent grows into experience.
        </div>
    </div>
</body>
</html>";
    }
}

public class NotifyApplicantRequest
{
    public string StudentEmail { get; set; } = string.Empty;
    public string? EmailTitle { get; set; }
    public string? EmailBody { get; set; }
}
