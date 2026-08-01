using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers;

/// <summary>AI assessment metric catalog — backed by the Metrics table, scoped per institute.</summary>
public partial class MetricsController
{
    public record MetricSensorFlagsDto(
        bool CustomerChat,
        bool MentorChat,
        bool CodebaseQuality,
        bool Resources,
        bool Stakeholders,
        bool ProjectModule,
        bool MeetingTranscripts,
        bool GroupChat,
        bool PrivateChat,
        bool TrelloTasks,
        bool TrelloUserStory,
        bool FigmaDesign);

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
        string? ExplicitRules,
        string? AIExpertise,
        bool IsBaseInstitute,
        string Source,
        MetricSensorFlagsDto Sensors);

    /// <summary>
    /// Returns the institute's own metrics, seeding from the base templates (InstituteId=1)
    /// on first access if the institute has no metrics yet.
    /// </summary>
    [HttpGet("use/assessment-config")]
    public async Task<ActionResult<IEnumerable<MetricAssessmentConfigDto>>> GetAssessmentMetricConfiguration(
        [FromQuery] int instituteId = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (instituteId > 1)
                await SeedMetricsForInstituteIfEmptyAsync(instituteId, cancellationToken);

            var metrics = await _context.Metrics
                .AsNoTracking()
                // Id > 0 keeps the sentinel rows (SprintSummary=0, HardSkills=-1) out no matter how
                // they are scoped in the database — they are not configurable metrics. Hard Skills
                // is appended below as a dedicated entry instead.
                .Where(m => m.InstituteId == instituteId && m.Id > 0)
                .OrderBy(m => m.Id)
                .ToListAsync(cancellationToken);

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
                m.ExplicitRules,
                m.AIExpertise,
                false,
                "db",
                new MetricSensorFlagsDto(
                    m.UseCustomerChat, m.UseMentorChat, m.UseCodebaseQuality,
                    m.UseResources, m.UseStakeholders, m.UseProjectModule,
                    m.UseMeetingTranscripts, m.UseGroupChat, m.UsePrivateChat,
                    m.UseTrelloTasks, m.UseTrelloUserStory, m.UseFigmaDesign))).ToList();

            // Hard skills is a sentinel (Id=-1, InstituteId=null), so the query above never returns
            // it. Append it so the staff dashboard's metric combo can select it like any other
            // metric. It is NOT configurable here — its rubric is each role's Hard Skills text and
            // its sensors come from the role's Main Tool — so the Soft Skills definition screen
            // filters it out by id. Sensor flags are all false: nothing reads them, and leaving them
            // true would imply a configuration that does not exist.
            dtos.Add(new MetricAssessmentConfigDto(
                HardSkillsMetricId,
                HardSkillsMetricSlug,
                HardSkillsDisplayName,
                "Hard Skills",
                "Scored against each role's Hard Skills definition, using the role's Main Tool as the evidence source.",
                null,
                true,
                3,
                null,
                null,
                null,
                false,
                "sentinel",
                new MetricSensorFlagsDto(false, false, false, false, false, false, false, false, false, false, false, false)));

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building assessment metric configuration list for institute {InstituteId}", instituteId);
            return StatusCode(500, "An error occurred while loading metric configuration.");
        }
    }

    /// <summary>
    /// Seeds the institute's own metric rows by copying from the base templates (InstituteId=1).
    /// Uses a Serializable transaction so concurrent first-accesses don't produce duplicates.
    /// Swallows serialization conflicts — they mean another request already seeded successfully.
    /// </summary>
    private async Task SeedMetricsForInstituteIfEmptyAsync(int instituteId, CancellationToken ct)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var hasOwn = await _context.Metrics
                .AnyAsync(m => m.InstituteId == instituteId, ct);

            if (!hasOwn)
            {
                var templates = await _context.Metrics
                    .AsNoTracking()
                    .Where(m => m.InstituteId == 1)
                    .ToListAsync(ct);

                var copies = templates.Select(t => new Metric
                {
                    InstituteId          = instituteId,
                    Name                 = t.Name,
                    Endpoint             = t.Endpoint,
                    Description          = t.Description,
                    Category             = t.Category,
                    Required             = t.Required,
                    Influence            = t.Influence,
                    Skill                = t.Skill,
                    ExplicitRules        = t.ExplicitRules,
                    AIExpertise          = t.AIExpertise,
                    UseCustomerChat      = t.UseCustomerChat,
                    UseMentorChat        = t.UseMentorChat,
                    UseCodebaseQuality   = t.UseCodebaseQuality,
                    UseResources         = t.UseResources,
                    UseStakeholders      = t.UseStakeholders,
                    UseProjectModule     = t.UseProjectModule,
                    UseMeetingTranscripts = t.UseMeetingTranscripts,
                    UseGroupChat         = t.UseGroupChat,
                    UsePrivateChat       = t.UsePrivateChat,
                    UseTrelloTasks       = t.UseTrelloTasks,
                    UseTrelloUserStory   = t.UseTrelloUserStory,
                    UseFigmaDesign       = t.UseFigmaDesign,
                }).ToList();

                _context.Metrics.AddRange(copies);
                await _context.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            // Serialization conflict = concurrent request already seeded. Log and continue.
            _logger.LogWarning(ex, "Metric seed transaction rolled back for institute {InstituteId} — likely a concurrent seed (safe to ignore).", instituteId);
        }
    }

    internal static string ToSlugKey(string name)
    {
        var s = name.Trim().ToLowerInvariant().Replace("&", "and").Replace(' ', '_');
        return System.Text.RegularExpressions.Regex.Replace(s, "[^a-z0-9_]", "");
    }
}
