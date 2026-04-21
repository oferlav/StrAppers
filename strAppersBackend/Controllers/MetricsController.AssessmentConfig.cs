using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace strAppersBackend.Controllers;

/// <summary>AI assessment metric catalog + mock configuration knobs for staff UI.</summary>
public partial class MetricsController
{
    public record MetricAssessmentConfigDto(
        int Id,
        string Key,
        string DisplayName,
        string Category,
        string Description,
        string? Endpoint,
        bool Required,
        int InfluenceScale,
        string AssessmentPhase,
        string Source);

    /// <summary>
    /// Catalog of metrics used in AI-assisted assessments, merged with editable-style defaults for the staff configuration UI.
    /// </summary>
    [HttpGet("use/assessment-config")]
    public async Task<ActionResult<IEnumerable<MetricAssessmentConfigDto>>> GetAssessmentMetricConfiguration()
    {
        try
        {
            var dbMetrics = await _context.Metrics.AsNoTracking().OrderBy(m => m.Id).ToListAsync();

            var list = new List<MetricAssessmentConfigDto>(capacity: dbMetrics.Count + ExtendedMetricDefinitions.Length);

            foreach (var m in dbMetrics)
            {
                var preset = ResolvePresetForCatalogMetric(m.Id, m.Name);
                list.Add(new MetricAssessmentConfigDto(
                    m.Id,
                    preset.Key,
                    preset.DisplayName,
                    preset.Category,
                    preset.Description,
                    m.Endpoint,
                    preset.Required,
                    preset.InfluenceScale,
                    preset.AssessmentPhase,
                    "catalog"));
            }

            list.AddRange(ExtendedMetricDefinitions);

            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building assessment metric configuration list");
            return StatusCode(500, "An error occurred while loading metric configuration.");
        }
    }

    private static readonly MetricAssessmentConfigDto[] ExtendedMetricDefinitions =
    {
        NewExtended(101, "github_velocity", "GitHub velocity & consistency", "Delivery",
            "Commits, PR rhythm, and branch hygiene vs sprint expectations for the active dev role.", true, 4, "Per sprint"),
        NewExtended(102, "figma_alignment", "Figma frame alignment", "Quality",
            "Frames linked to sprint scope; review feedback loop closure for UI/UX.", false, 4, "Per sprint"),
        NewExtended(103, "story_backlog_health", "User story clarity", "Quality",
            "Trello story titles, acceptance hints, and sprint list placement vs Definition of Ready.", true, 3, "Per sprint"),
        NewExtended(104, "crm_touchpoints", "CRM touchpoint depth", "Engagement",
            "Stakeholder notes, meeting cadence, and opportunity hygiene for PM/Marketing roles.", false, 3, "Continuous"),
        NewExtended(105, "resource_depth", "Learning resources usage", "Engagement",
            "Attached resources opened/applied; mentor-facing evidence on the Resources card.", false, 2, "Per sprint"),
        NewExtended(106, "board_state_freshness", "Board state freshness", "Delivery",
            "BoardStates / automation signals kept in sync with actual sprint progression.", false, 3, "Continuous"),
        NewExtended(107, "cross_role_handoff", "Cross-role handoff quality", "Collaboration",
            "Evidence of brief handoffs between Frontend, Backend, PM, and Design within the sprint window.", false, 2, "Per sprint"),
        NewExtended(108, "documentation_signals", "Documentation & runbook signals", "Quality",
            "README updates, deployment notes, or inline docs tied to shipped work.", false, 2, "Milestone"),
        NewExtended(109, "security_hygiene", "Dependency & security hygiene", "Risk",
            "High-level signals from dependency updates and obvious vulnerability backlog (mock weight).", false, 2, "Milestone"),
        NewExtended(110, "mentor_chat_engagement", "Mentor chat engagement", "Engagement",
            "Structured use of mentor channels for story, code, CRM, or resource guidance.", false, 2, "Continuous"),
        NewExtended(111, "squad_collaboration", "Squad collaboration index", "Collaboration",
            "Composite mock: peer mentions, shared labels, and squad-wide visibility on the board.", false, 2, "Per sprint"),
        NewExtended(112, "kickoff_alignment", "Kickoff / scope alignment", "Delivery",
            "Early-sprint alignment between brief, modules, and sprint plan structure.", false, 3, "Milestone"),
    };

    private static MetricAssessmentConfigDto NewExtended(
        int id,
        string key,
        string displayName,
        string category,
        string description,
        bool required,
        int influenceScale,
        string phase) =>
        new MetricAssessmentConfigDto(
            id,
            key,
            displayName,
            category,
            description,
            null,
            required,
            influenceScale,
            phase,
            "extended");

    private static (string Key, string DisplayName, string Category, string Description, bool Required, int InfluenceScale, string AssessmentPhase)
        ResolvePresetForCatalogMetric(int id, string name)
    {
        var n = name.Trim();
        return id switch
        {
            1 => ("adherence", "Adherence", "Delivery",
                "Checkbox-driven alignment with Required Skill Data / Required Resource Data on sprint role cards.", true, 5, "Per sprint"),
            2 => ("gap_analysis", "Gap analysis", "Quality",
                "Structured gap identification vs expectations for the role and sprint.", true, 4, "Per sprint"),
            3 => ("improvement", "Improvement trajectory", "Quality",
                "Trend of actionable improvements across recent sprints.", false, 3, "Continuous"),
            4 => ("communication", "Communication quality", "Collaboration",
                "Clarity and timeliness of updates visible on cards and mentor threads.", false, 3, "Continuous"),
            5 => ("attendance", "Attendance / participation", "Engagement",
                "Scheduled touchpoints and participation signals relevant to the institute.", false, 4, "Continuous"),
            6 => ("strengths_weaknesses", "Strengths & weaknesses", "Quality",
                "Balanced view of standout strengths versus recurring gaps.", false, 3, "Milestone"),
            7 => ("customer_engagement", "Customer engagement", "Engagement",
                "Customer-facing activity quality where the project includes stakeholder interaction.", false, 3, "Continuous"),
            _ => (ToSlugKey(n), n, "Quality", $"Assessment metric: {n}.", false, 3, "Per sprint")
        };
    }

    private static string ToSlugKey(string name)
    {
        var s = name.Trim().ToLowerInvariant().Replace("&", "and").Replace(' ', '_');
        return System.Text.RegularExpressions.Regex.Replace(s, "[^a-z0-9_]", "");
    }
}
