using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace strAppersBackend.Controllers;

/// <summary>AI assessment metric catalog — backed by the Metrics table, scoped per institute.</summary>
public partial class MetricsController
{
    public record MetricAssessmentConfigDto(
        int Id,
        string Key,
        string DisplayName,
        string? Category,
        string? Description,
        string? Endpoint,
        bool Required,
        int InfluenceScale,
        string? Skill,
        string? AIExpertise,
        bool IsBaseInstitute,
        string Source);

    /// <summary>
    /// Returns base-institute metrics (InstituteId=1) plus the requesting institute's own metrics.
    /// instituteId defaults to 1 when omitted (base institute view).
    /// </summary>
    [HttpGet("use/assessment-config")]
    public async Task<ActionResult<IEnumerable<MetricAssessmentConfigDto>>> GetAssessmentMetricConfiguration(
        [FromQuery] int instituteId = 1)
    {
        try
        {
            var metrics = await _context.Metrics
                .AsNoTracking()
                .Where(m => m.InstituteId == 1 || m.InstituteId == instituteId)
                .OrderBy(m => m.InstituteId)
                .ThenBy(m => m.Id)
                .ToListAsync();

            var dtos = metrics.Select(m => new MetricAssessmentConfigDto(
                m.Id,
                ToSlugKey(m.Name),
                m.Name,
                m.Category,
                m.Description,
                m.Endpoint,
                m.Required,
                m.Influence,
                m.Skill,
                m.AIExpertise,
                m.InstituteId == 1,
                "db")).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building assessment metric configuration list for institute {InstituteId}", instituteId);
            return StatusCode(500, "An error occurred while loading metric configuration.");
        }
    }

    private static string ToSlugKey(string name)
    {
        var s = name.Trim().ToLowerInvariant().Replace("&", "and").Replace(' ', '_');
        return System.Text.RegularExpressions.Regex.Replace(s, "[^a-z0-9_]", "");
    }
}
