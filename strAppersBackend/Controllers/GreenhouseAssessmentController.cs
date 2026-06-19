using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Models.Greenhouse;
using System.Text;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/greenhouse/assessment")]
public class GreenhouseAssessmentController : ControllerBase
{
    private readonly ILogger<GreenhouseAssessmentController> _logger;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public GreenhouseAssessmentController(
        ILogger<GreenhouseAssessmentController> logger,
        ApplicationDbContext db,
        IConfiguration configuration)
    {
        _logger = logger;
        _db = db;
        _configuration = configuration;
    }

    // GET /api/greenhouse/assessment/list-tests
    // Greenhouse calls this to populate the test picker when a recruiter sets up an interview stage.
    [HttpGet("list-tests")]
    public IActionResult ListTests()
    {
        if (!ValidateGreenhouseAuth(out var deny)) return deny!;

        try
        {
            // TODO: replace with a real query to the assessment catalog once it exists.
            // partner_test_id values must be stable — Greenhouse stores them against job stages.
            var tests = new List<ListTestsResponseItem>
            {
                new() { PartnerTestId = "skill-in-fullstack", PartnerTestName = "Full-Stack Challenge" },
                new() { PartnerTestId = "skill-in-frontend",  PartnerTestName = "Frontend Challenge"   },
                new() { PartnerTestId = "skill-in-backend",   PartnerTestName = "Backend Challenge"    },
            };

            return Ok(tests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning test list to Greenhouse");
            return StatusCode(500);
        }
    }

    // POST /api/greenhouse/assessment/send-test
    // Greenhouse calls this when a recruiter sends a test to a candidate.
    // We create an AtsAssessmentInstance (token = ExternalInterviewId) and queue an invitation email.
    [HttpPost("send-test")]
    public async Task<IActionResult> SendTest([FromBody] SendTestRequest request)
    {
        if (!ValidateGreenhouseAuth(out var deny)) return deny!;

        if (string.IsNullOrWhiteSpace(request.Candidate?.Email))
            return BadRequest(new { error = "candidate.email is required" });

        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { error = "url (Greenhouse callback URL) is required" });

        try
        {
            // The token doubles as the registration coupon (Students.Coupon = ExternalInterviewId).
            var token = Guid.NewGuid().ToString("N");

            _db.AtsAssessmentInstances.Add(new AtsAssessmentInstance
            {
                Provider             = "greenhouse",
                ExternalInterviewId  = token,
                ExternalTestId       = request.PartnerTestId,
                CandidateEmail       = request.Candidate.Email.Trim().ToLower(),
                CandidateFirstName   = request.Candidate.FirstName,
                CandidateLastName    = request.Candidate.LastName,
                ExternalProfileUrl   = request.Candidate.GreenhouseProfileUrl,
                CallbackUrl          = request.Url,
                SentBy               = request.SentBy,
                Status               = "not_started",
                CreatedAt            = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync();

            // TODO: send invitation email to request.Candidate.Email with the registration link:
            //   https://skill-in.com/register?coupon={token}

            _logger.LogInformation(
                "Assessment instance created — token: {Token}, test: {TestId}, candidate: {Email}",
                token, request.PartnerTestId, request.Candidate.Email);

            return Ok(new SendTestResponse { PartnerInterviewId = token });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating assessment instance for {Email}", request.Candidate?.Email);
            return StatusCode(500);
        }
    }

    // GET /api/greenhouse/assessment/test-status?partner_interview_id=...
    // Greenhouse polls this hourly, or immediately after we PATCH its callback URL.
    [HttpGet("test-status")]
    public async Task<IActionResult> TestStatus(
        [FromQuery(Name = "partner_interview_id")] string partnerInterviewId)
    {
        if (!ValidateGreenhouseAuth(out var deny)) return deny!;

        if (string.IsNullOrWhiteSpace(partnerInterviewId))
            return BadRequest(new { error = "partner_interview_id is required" });

        try
        {
            var instance = await _db.AtsAssessmentInstances
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.ExternalInterviewId == partnerInterviewId
                                       && i.Provider == "greenhouse");

            if (instance is null)
                return NotFound();

            return Ok(new TestStatusResponse
            {
                PartnerStatus     = instance.Status,
                PartnerScore      = instance.Score,
                PartnerProfileUrl = instance.ProfileUrl,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching test status for {Id}", partnerInterviewId);
            return StatusCode(500);
        }
    }

    // POST /api/greenhouse/assessment/response-error
    // Greenhouse calls this to report errors it encountered calling our endpoints.
    [HttpPost("response-error")]
    public IActionResult ResponseError([FromBody] ResponseErrorRequest request)
    {
        if (!ValidateGreenhouseAuth(out var deny)) return deny!;

        _logger.LogError(
            "Greenhouse API error — call: {ApiCall}, errors: [{Errors}], interview: {InterviewId}, candidate: {Email}",
            request.ApiCall,
            string.Join("; ", request.Errors),
            request.PartnerInterviewId ?? "n/a",
            request.CandidateEmail ?? "n/a");

        return Ok(new { status = 200 });
    }

    // Validates the Basic Auth header Greenhouse sends on every inbound request.
    // Greenhouse encodes: Base64(api_key + ":") — username is the key, password is blank.
    private bool ValidateGreenhouseAuth(out IActionResult? result)
    {
        var expectedKey = _configuration["Greenhouse:AssessmentApiKey"];
        if (string.IsNullOrEmpty(expectedKey))
        {
            _logger.LogError("Greenhouse:AssessmentApiKey is not configured");
            result = StatusCode(503, new { error = "Assessment API not configured" });
            return false;
        }

        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            result = Unauthorized();
            return false;
        }

        try
        {
            var encoded = authHeader["Basic ".Length..].Trim();
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var apiKey  = decoded.TrimEnd(':');

            if (!string.Equals(apiKey, expectedKey, StringComparison.Ordinal))
            {
                _logger.LogWarning("Greenhouse Basic Auth rejected — key mismatch");
                result = Unauthorized();
                return false;
            }
        }
        catch
        {
            result = Unauthorized();
            return false;
        }

        result = null;
        return true;
    }
}
