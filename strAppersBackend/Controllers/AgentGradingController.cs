using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services.GoogleProxy;

namespace strAppersBackend.Controllers;

/// <summary>
/// Starts and reads API-level gradings of a student's AI agent backend (AgentGrader).
///   POST /api/agent-grading/runs          header X-Grader-Key   body AgentGradingRequest  -> 202 { gradingId }
///   GET  /api/agent-grading/runs/{id}     header X-Grader-Key                             -> the report (status running | completed | failed)
/// What gets graded comes from the DB: board -> institute project -> its graded (or practice) scenario set.
/// Protected by GoogleProxy:GraderKey because a grading calls the student backend and spends Gemini quota.
/// </summary>
[ApiController]
[Route("api/agent-grading")]
[ApiExplorerSettings(IgnoreApi = true)]
public class AgentGradingController : ControllerBase
{
    private readonly GoogleProxyConfig _config;
    private readonly AgentGrader _grader;
    private readonly ApplicationDbContext _context;

    public AgentGradingController(IOptions<GoogleProxyConfig> config, AgentGrader grader, ApplicationDbContext context)
    {
        _config = config.Value;
        _grader = grader;
        _context = context;
    }

    public class AgentGradingRequest
    {
        public string BoardId { get; set; } = "";
        /// <summary>"graded" (default) or "practice": which of the project's scenario sets to run.</summary>
        public string Purpose { get; set; } = ProjectAgentScenarioSet.PurposeGraded;
        public int Repetitions { get; set; } = 3;
        /// <summary>Optional subset of the set's scenario keys; all scenarios when empty.</summary>
        public List<string>? ScenarioKeys { get; set; }
        /// <summary>Optional override of the board's WebApiUrl (for example a local backend while testing).</summary>
        public string? BackendUrl { get; set; }
    }

    [HttpPost("runs")]
    public async Task<IActionResult> Start([FromBody] AgentGradingRequest request, CancellationToken cancellationToken)
    {
        if (!TryAuthorize(out var failure)) return failure!;
        if (string.IsNullOrWhiteSpace(request.BoardId)) return BadRequest(new { error = "boardId is required." });
        if (request.Purpose != ProjectAgentScenarioSet.PurposeGraded && request.Purpose != ProjectAgentScenarioSet.PurposePractice)
            return BadRequest(new { error = "purpose must be 'graded' or 'practice'." });

        var rawUrl = request.BackendUrl;
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            rawUrl = await _context.ProjectBoards.AsNoTracking()
                .Where(b => b.Id == request.BoardId)
                .Select(b => b.WebApiUrl)
                .FirstOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(rawUrl)) return BadRequest(new { error = "The board has no WebApiUrl; pass backendUrl." });
        }

        // WebApiUrl may point at /swagger; the grader needs only scheme and host.
        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return BadRequest(new { error = $"Invalid backend URL '{rawUrl}'." });
        var backendUrl = uri.GetLeftPart(UriPartial.Authority);

        try
        {
            var plan = await AgentGrader.ResolvePlanAsync(_context, request.BoardId, request.Purpose, request.ScenarioKeys, cancellationToken);
            var report = await _grader.StartAsync(plan, backendUrl, request.Repetitions, cancellationToken);
            return StatusCode(202, new
            {
                gradingId = report.GradingId, status = "running", backendUrl,
                scenarioSet = $"{plan.ScenarioSetName} v{plan.ScenarioSetVersion}", scenarios = plan.Scenarios.Count,
                statusUrl = $"/api/agent-grading/runs/{report.GradingId}"
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("runs/{gradingId}")]
    public async Task<IActionResult> Get(string gradingId, CancellationToken cancellationToken)
    {
        if (!TryAuthorize(out var failure)) return failure!;
        var json = await _grader.GetReportJsonAsync(gradingId, cancellationToken);
        return json == null ? NotFound(new { error = "Unknown or expired grading id." }) : Content(json, "application/json");
    }

    private bool TryAuthorize(out IActionResult? failure)
    {
        failure = null;
        if (!_config.Enabled || string.IsNullOrWhiteSpace(_config.TokenSecret) || string.IsNullOrWhiteSpace(_config.GraderKey))
        {
            failure = StatusCode(503, new { error = "Agent grading is not enabled." });
            return false;
        }
        var expected = Encoding.UTF8.GetBytes(_config.GraderKey);
        var actual = Encoding.UTF8.GetBytes(Request.Headers["X-Grader-Key"].ToString());
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            failure = Unauthorized(new { error = "Missing or invalid X-Grader-Key." });
            return false;
        }
        return true;
    }
}
