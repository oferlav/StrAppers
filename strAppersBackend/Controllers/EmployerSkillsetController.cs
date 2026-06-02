using Microsoft.AspNetCore.Mvc;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployerSkillsetController : ControllerBase
{
    private readonly ILogger<EmployerSkillsetController> _logger;

    public EmployerSkillsetController(ILogger<EmployerSkillsetController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Receive a generated Skillset from the employer plugin.
    /// Only aggregated, anonymised metric scores arrive here — no raw employee data, no credentials.
    /// Phase 1: validate + echo. Phase 2: persist to DB. Phase 3: trigger matching against student pipeline.
    /// </summary>
    [HttpPost("use/submit")]
    public ActionResult<object> SubmitSkillset([FromBody] EmployerSkillsetSubmitRequest? request)
    {
        if (request is null)
            return BadRequest(new { success = false, message = "Request body is required." });
        if (string.IsNullOrWhiteSpace(request.CompanyIdentifier))
            return BadRequest(new { success = false, message = "companyIdentifier is required." });
        if (string.IsNullOrWhiteSpace(request.RoleName))
            return BadRequest(new { success = false, message = "roleName is required." });
        if (request.Skills is null || request.Skills.Count == 0)
            return BadRequest(new { success = false, message = "skills array must not be empty." });

        var skillsetId = $"sks_{Guid.NewGuid():N}"[..20];

        // Weighted score: sum(score * weight) / sum(weight)
        var totalWeight = request.Skills.Sum(s => s.Weight);
        var weightedScore = totalWeight > 0
            ? request.Skills.Sum(s => s.AggregatedScore * s.Weight) / totalWeight
            : request.Skills.Average(s => s.AggregatedScore);

        _logger.LogInformation(
            "EmployerSkillset received: id={SkillsetId} company={Company} role={Role} skills={SkillCount} sources={Sources} period={From}→{To}",
            skillsetId,
            request.CompanyIdentifier,
            request.RoleName,
            request.Skills.Count,
            string.Join(",", request.DataSourcesUsed),
            request.AssessedPeriod?.From,
            request.AssessedPeriod?.To);

        // TODO Phase 2: persist to EmployerSkillsets table
        // TODO Phase 3: enqueue matching job against student pipeline

        return Ok(new
        {
            success = true,
            skillsetId,
            message = "Skillset received. It will be matched against the student pipeline once human rating review is complete.",
            summary = new
            {
                companyIdentifier = request.CompanyIdentifier,
                roleName = request.RoleName,
                skillCount = request.Skills.Count,
                dataSourcesUsed = request.DataSourcesUsed,
                employeeSampleSize = request.EmployeeSampleSize,
                weightedSkillsetScore = Math.Round(weightedScore, 1),
                assessedPeriod = request.AssessedPeriod,
                skillBreakdown = request.Skills.Select(s => new
                {
                    s.Name,
                    s.MetricCategory,
                    s.DataSources,
                    aggregatedScore = Math.Round(s.AggregatedScore, 1),
                    humanRatingAnchor = s.HumanRatingAnchor,
                    weight = s.Weight,
                    confidence = Math.Round(s.Confidence, 2),
                }),
                generatedAt = request.Metadata?.GeneratedAt,
                pluginVersion = request.Metadata?.PluginVersion,
            },
        });
    }
}
