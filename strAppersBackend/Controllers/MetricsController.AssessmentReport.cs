using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;
using strAppersBackend.Utilities;

namespace strAppersBackend.Controllers;

public partial class MetricsController
{
    /// <summary>
    /// Autocomplete: search project boards by <see cref="ProjectBoard.SquadName"/> (substring, case-insensitive).
    /// </summary>
    [HttpGet("use/squad-names-search")]
    [ProducesResponseType(typeof(SquadNameSearchResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SquadNameSearchResponseDto>> SearchSquadNames(
        [FromQuery] string? q,
        CancellationToken cancellationToken = default)
    {
        var term = (q ?? "").Trim();
        if (term.Length == 0)
            return Ok(new SquadNameSearchResponseDto { Items = Array.Empty<SquadNameSearchItemDto>() });

        var pattern = "%" + term.Replace("%", "\\%").Replace("_", "\\_") + "%";
        var items = await _context.ProjectBoards.AsNoTracking()
            .Where(b => b.SquadName != null && b.SquadName != "" && EF.Functions.ILike(b.SquadName, pattern))
            .OrderBy(b => b.SquadName)
            .Take(30)
            .Select(b => new SquadNameSearchItemDto
            {
                BoardId = b.Id,
                SquadName = b.SquadName!
            })
            .ToListAsync(cancellationToken);

        return Ok(new SquadNameSearchResponseDto { Items = items });
    }

    /// <summary>
    /// Structured report from <see cref="CacheMetrics"/>. Provide <paramref name="squadName"/> (preferred), <paramref name="boardId"/>, or <paramref name="roleId"/> (all squads with that active role).
    /// </summary>
    [HttpGet("use/assessment-report")]
    [ProducesResponseType(typeof(AssessmentReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssessmentReportDto>> GetAssessmentReport(
        [FromQuery] string? boardId = null,
        [FromQuery] string? squadName = null,
        [FromQuery] int? studentId = null,
        [FromQuery] int? sprintNumber = null,
        [FromQuery] int? roleId = null,
        CancellationToken cancellationToken = default)
    {
        var hasBoard = !string.IsNullOrWhiteSpace(boardId) || !string.IsNullOrWhiteSpace(squadName);
        if (!hasBoard && !roleId.HasValue)
            return BadRequest(new { success = false, message = "Provide squadName, boardId, or roleId." });

        if (!hasBoard && roleId.HasValue)
            return await GetAssessmentReportForRoleAcrossSquads(roleId.Value, studentId, sprintNumber, cancellationToken);

        string boardIdTrim;
        string? resolvedSquadName = null;

        if (!string.IsNullOrWhiteSpace(squadName))
        {
            var sn = squadName.Trim();
            var matches = await _context.ProjectBoards.AsNoTracking()
                .Where(b => b.SquadName != null && EF.Functions.ILike(b.SquadName, sn))
                .Select(b => new { b.Id, b.SquadName })
                .ToListAsync(cancellationToken);

            if (matches.Count == 0)
                return NotFound(new { success = false, message = $"No board with squad name \"{sn}\"." });
            if (matches.Count > 1)
            {
                return Conflict(new
                {
                    success = false,
                    message = "Multiple boards use this squad name. Pass boardId or pick a unique name.",
                    candidates = matches.Take(10).Select(m => new { boardId = m.Id, squadName = m.SquadName }).ToList()
                });
            }

            boardIdTrim = matches[0].Id;
            resolvedSquadName = matches[0].SquadName;
        }
        else
        {
            boardIdTrim = boardId!.Trim();
            resolvedSquadName = await _context.ProjectBoards.AsNoTracking()
                .Where(b => b.Id == boardIdTrim)
                .Select(b => b.SquadName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var boardExists = await _context.ProjectBoards.AsNoTracking()
            .AnyAsync(b => b.Id == boardIdTrim, cancellationToken);
        if (!boardExists)
            return NotFound(new { success = false, message = $"Board {boardIdTrim} not found." });

        string? roleFilterName = null;
        if (roleId.HasValue)
        {
            roleFilterName = await _context.Roles.AsNoTracking()
                .Where(r => r.Id == roleId.Value)
                .Select(r => r.Name)
                .FirstOrDefaultAsync(cancellationToken);
            if (string.IsNullOrEmpty(roleFilterName))
                return NotFound(new { success = false, message = $"Role {roleId.Value} not found." });
        }

        string headline;
        if (studentId.HasValue)
        {
            var student = await _context.Students.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == studentId.Value, cancellationToken);
            if (student == null)
                return NotFound(new { success = false, message = $"Student {studentId.Value} not found." });
            if (!string.Equals(student.BoardId?.Trim(), boardIdTrim, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Student is not assigned to this board." });

            var name = $"{student.FirstName} {student.LastName}".Trim();
            headline = $"Assessment Report for {name}";
        }
        else
        {
            headline = "Squad Assessment Report";
        }

        if (!string.IsNullOrEmpty(roleFilterName))
            headline = $"{headline} — {roleFilterName}";

        var query = _context.CacheMetrics
            .AsNoTracking()
            .Include(c => c.Metric)
            .Include(c => c.Student)
            .Where(c => c.BoardId == boardIdTrim)
            // Hide cached rows for sprints that don't exist on the board (a pre-created next-sprint
            // merge row has MergedAt=null and ListId=null; assessments cached for it are phantom).
            .Where(c => c.SprintNumber < 1 || _context.ProjectBoardSprintMerges.Any(m =>
                m.ProjectBoardId == c.BoardId && m.SprintNumber == c.SprintNumber &&
                (m.MergedAt != null || m.ListId != null)));

        if (roleId.HasValue)
        {
            // Per-concept role filter: match any duplicate row of the same role name.
            var roleConceptIds = await strAppersBackend.Utilities.RoleConceptResolver.ResolveConceptRoleIdsAsync(_context, roleId.Value, cancellationToken);
            query = WhereStudentHasRoleConcept(query, roleConceptIds);
        }
        if (studentId.HasValue)
            query = query.Where(c => c.StudentId == studentId.Value);
        if (sprintNumber.HasValue)
            query = query.Where(c => c.SprintNumber == sprintNumber.Value);

        var rows = await query
            .OrderBy(c => c.SprintNumber)
            .ThenBy(c => c.StudentId)
            .ThenBy(c => c.MetricId)
            .ToListAsync(cancellationToken);

        var sprints = BuildAssessmentSprints(rows, includeSquadInStudentName: false,
            await BuildMainToolLookupAsync(rows, cancellationToken),
            await BuildSprintDateLookupAsync(boardIdTrim, rows, cancellationToken));

        return Ok(new AssessmentReportDto
        {
            Headline = headline,
            BoardId = boardIdTrim,
            SquadName = resolvedSquadName,
            Sprints = sprints,
            CourseSummaries = BuildCourseSummaries(rows, includeSquadInStudentName: false)
        });
    }

    private async Task<ActionResult<AssessmentReportDto>> GetAssessmentReportForRoleAcrossSquads(
        int roleId,
        int? studentId,
        int? sprintNumber,
        CancellationToken cancellationToken)
    {
        var roleName = await _context.Roles.AsNoTracking()
            .Where(r => r.Id == roleId)
            .Select(r => r.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrEmpty(roleName))
            return NotFound(new { success = false, message = $"Role {roleId} not found." });

        string headline;
        if (studentId.HasValue)
        {
            var student = await _context.Students.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == studentId.Value, cancellationToken);
            if (student == null)
                return NotFound(new { success = false, message = $"Student {studentId.Value} not found." });
            var conceptIdsForCheck = await strAppersBackend.Utilities.RoleConceptResolver.ResolveConceptRoleIdsAsync(_context, roleId, cancellationToken);
            var hasRole = await _context.StudentRoles.AsNoTracking()
                .AnyAsync(sr => sr.StudentId == studentId.Value && conceptIdsForCheck.Contains(sr.RoleId) && sr.IsActive, cancellationToken);
            if (!hasRole)
                return BadRequest(new { success = false, message = "Student does not have this active role." });

            var name = $"{student.FirstName} {student.LastName}".Trim();
            headline = $"Assessment Report for {name} ({roleName})";
        }
        else
        {
            headline = $"Assessment Report — {roleName} (all squads)";
        }

        var conceptRoleIds = await strAppersBackend.Utilities.RoleConceptResolver.ResolveConceptRoleIdsAsync(_context, roleId, cancellationToken);
        var query = WhereStudentHasRoleConcept(
                _context.CacheMetrics
                    .AsNoTracking()
                    .Include(c => c.Metric)
                    .Include(c => c.Student)
                    .Include(c => c.ProjectBoard),
                conceptRoleIds)
            // Same phantom-sprint filter as the board-scoped report (see GetAssessmentReport).
            .Where(c => c.SprintNumber < 1 || _context.ProjectBoardSprintMerges.Any(m =>
                m.ProjectBoardId == c.BoardId && m.SprintNumber == c.SprintNumber &&
                (m.MergedAt != null || m.ListId != null)));

        if (studentId.HasValue)
            query = query.Where(c => c.StudentId == studentId.Value);
        if (sprintNumber.HasValue)
            query = query.Where(c => c.SprintNumber == sprintNumber.Value);

        var rows = await query
            .OrderBy(c => c.SprintNumber)
            .ThenBy(c => c.StudentId)
            .ThenBy(c => c.MetricId)
            .ToListAsync(cancellationToken);

        var sprints = BuildAssessmentSprints(rows, includeSquadInStudentName: true,
            await BuildMainToolLookupAsync(rows, cancellationToken));

        return Ok(new AssessmentReportDto
        {
            Headline = headline,
            BoardId = null,
            SquadName = null,
            Sprints = sprints,
            CourseSummaries = BuildCourseSummaries(rows, includeSquadInStudentName: true)
        });
    }

    /// <summary>
    /// Start/due dates per sprint number for one board, resolved exactly the way the assessment
    /// engine resolves its evidence window: the sprint merge row first, then the board's sprint
    /// plan. Only meaningful for a single board — a sprint number means different dates on each.
    /// </summary>
    private async Task<Dictionary<int, (DateTime Start, DateTime End)>> BuildSprintDateLookupAsync(
        string boardId, IReadOnlyList<CacheMetrics> rows, CancellationToken ct)
    {
        var lookup = new Dictionary<int, (DateTime, DateTime)>();
        var board = await _context.ProjectBoards.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == boardId, ct);
        if (board == null) return lookup;

        var weeks = _configuration.GetValue("BusinessLogicConfig:SprintLengthInWeeks", 1);
        var days = await SprintLengthResolver.ResolveForBoardAsync(_context, boardId, weeks, ct);
        var merges = await _context.ProjectBoardSprintMerges.AsNoTracking()
            .Where(m => m.ProjectBoardId == boardId)
            .ToListAsync(ct);

        foreach (var n in rows.Select(r => r.SprintNumber).Distinct())
        {
            if (n < 0) continue; // course-summary sentinel, not a real sprint
            var merge = merges.FirstOrDefault(m => m.SprintNumber == n);
            if (SprintPlanDateResolver.TryGetInclusiveUtcRangeFromSprintMerge(merge, n, days, out var s, out var e)
                || SprintPlanDateResolver.TryGetSprintInclusiveUtcRange(board.SprintPlan, board.StartDate, n, out s, out e, days))
            {
                lookup[n] = (s, e);
            }
        }
        return lookup;
    }

    internal static IReadOnlyList<AssessmentReportSprintDto> BuildAssessmentSprints(
        IReadOnlyList<CacheMetrics> rows,
        bool includeSquadInStudentName = false,
        IReadOnlyDictionary<int, string?>? mainToolByStudent = null,
        IReadOnlyDictionary<int, (DateTime Start, DateTime End)>? sprintDates = null)
    {
        // Course summaries (SprintNumber = CourseSummarySprintNumber) are not a real sprint — they are
        // reported separately via BuildCourseSummaries, never as a sprint section.
        rows = rows.Where(r => r.SprintNumber != CourseSummarySprintNumber).ToList();
        if (rows.Count == 0)
            return Array.Empty<AssessmentReportSprintDto>();

        var sprints = new List<AssessmentReportSprintDto>();
        foreach (var sprintGroup in rows.GroupBy(r => r.SprintNumber).OrderBy(g => g.Key))
        {
            var students = new List<AssessmentReportStudentDto>();
            foreach (var studentGroup in sprintGroup.GroupBy(r => r.StudentId).OrderBy(g => StudentSortKey(g.First(), includeSquadInStudentName)))
            {
                var first = studentGroup.First();
                students.Add(new AssessmentReportStudentDto
                {
                    StudentId = first.StudentId,
                    StudentName = FormatStudentName(first, includeSquadInStudentName),
                    Metrics = studentGroup
                        .OrderBy(r => r.MetricId)
                        .Select(r => MapMetricDto(r, mainToolByStudent?.GetValueOrDefault(r.StudentId)))
                        .ToList()
                });
            }

            (DateTime Start, DateTime End) window = default;
            var hasDates = sprintDates != null && sprintDates.TryGetValue(sprintGroup.Key, out window);
            sprints.Add(new AssessmentReportSprintDto
            {
                SprintNumber = sprintGroup.Key,
                StartDate = hasDates ? window.Start : null,
                DueDate = hasDates ? window.End : null,
                Students = students
            });
        }

        return sprints;
    }

    private static string StudentSortKey(CacheMetrics row, bool includeSquadInStudentName) =>
        FormatStudentName(row, includeSquadInStudentName);

    private static string FormatStudentName(CacheMetrics row, bool includeSquadInStudentName = false)
    {
        var baseName = row.Student == null
            ? $"Student {row.StudentId}"
            : $"{row.Student.FirstName} {row.Student.LastName}".Trim();
        if (!includeSquadInStudentName)
            return baseName;
        var sn = row.ProjectBoard?.SquadName?.Trim();
        return string.IsNullOrEmpty(sn) ? baseName : $"{baseName} ({sn})";
    }

    /// <summary>Role filter for cached assessment rows — per role CONCEPT (all duplicate ids), never a single row id.</summary>
    internal static IQueryable<CacheMetrics> WhereStudentHasRoleConcept(IQueryable<CacheMetrics> query, List<int> conceptRoleIds)
        => query.Where(c => c.Student!.StudentRoles.Any(sr => conceptRoleIds.Contains(sr.RoleId) && sr.IsActive));

    /// <summary>Per-student course summaries (rows stored at SprintNumber = CourseSummarySprintNumber, MetricId = SummaryMetricId).</summary>
    internal static IReadOnlyList<AssessmentReportCourseSummaryDto> BuildCourseSummaries(
        IReadOnlyList<CacheMetrics> rows,
        bool includeSquadInStudentName = false)
    {
        // Course-level hard skills live in their own row (MetricId=-1) at the same sentinel sprint;
        // merge it into the student's course summary so the report has one entry per student.
        var hardByStudent = rows
            .Where(r => r.SprintNumber == CourseSummarySprintNumber && r.MetricId == HardSkillsMetricId)
            .GroupBy(r => r.StudentId)
            .ToDictionary(g => g.Key, g => g.First());

        return rows
            .Where(r => r.SprintNumber == CourseSummarySprintNumber && r.MetricId == SummaryMetricId)
            .OrderBy(r => StudentSortKey(r, includeSquadInStudentName))
            .Select(r =>
            {
                hardByStudent.TryGetValue(r.StudentId, out var hard);
                return new AssessmentReportCourseSummaryDto
                {
                    StudentId = r.StudentId,
                    StudentName = FormatStudentName(r, includeSquadInStudentName),
                    ReviewContent = r.ReviewContent ?? "",
                    Graph = string.IsNullOrWhiteSpace(r.Graph) ? null : r.Graph.Trim(),
                    HardSkillsReviewContent = string.IsNullOrWhiteSpace(hard?.ReviewContent) ? null : hard!.ReviewContent.Trim(),
                    HardSkillsGraph = string.IsNullOrWhiteSpace(hard?.Graph) ? null : hard!.Graph!.Trim(),
                };
            })
            .ToList();
    }

    /// <summary>
    /// Enabled sensors for a metric, as the labels the staff UI uses (kept in step with
    /// SENSOR_LABELS in MetricsAssessment.jsx). The MetricId=0 summary row has no metric of
    /// its own — its sources are those of the metrics it summarizes — so it returns none.
    /// </summary>
    internal static IReadOnlyList<string> DescribeSensors(Metric? metric)
    {
        if (metric is null) return Array.Empty<string>();

        var labels = new List<string>();
        if (metric.UseCustomerChat)       labels.Add("AI Customer Chat");
        if (metric.UseMentorChat)         labels.Add("AI Mentor Chat");
        if (metric.UseCodebaseQuality)    labels.Add("Codebase & Github");
        if (metric.UseResources)          labels.Add("Resources (Docs, Links, Images, etc.)");
        if (metric.UseStakeholders)       labels.Add("CRM / Stakeholders");
        if (metric.UseProjectModule)      labels.Add("Project Design");
        if (metric.UseMeetingTranscripts) labels.Add("Meeting Transcripts");
        if (metric.UseGroupChat)          labels.Add("Group Chat (Squad)");
        if (metric.UsePrivateChat)        labels.Add("Private Chat (1-on-1)");
        if (metric.UseTrelloTasks)        labels.Add("Sprint Tasks");
        if (metric.UseTrelloUserStory)    labels.Add("User Stories");
        if (metric.UseFigmaDesign)        labels.Add("Figma Design");
        return labels;
    }

    /// <summary>
    /// Sensors to list for a cached row.
    ///
    /// The two sentinel rows can't use their own Metrics row: both exist only to satisfy the FK on
    /// CacheMetrics.MetricId and carry every sensor flag defaulted to true, which is meaningless.
    /// Summary (0) reports none — it has no sensors of its own, and the metrics it summarizes list
    /// theirs already. Hard skills (-1) reports whatever its role's Main Tool selects, matching what
    /// BuildHardSkillsMetric actually collected.
    /// </summary>
    internal static IReadOnlyList<string> ResolveReportSensors(CacheMetrics row, string? mainTool)
    {
        if (row.MetricId == SummaryMetricId) return Array.Empty<string>();
        if (row.MetricId == HardSkillsMetricId) return DescribeSensors(BuildHardSkillsMetric(string.Empty, mainTool));
        return DescribeSensors(row.Metric);
    }

    /// <summary>
    /// Main Tool per student, used only for hard-skills rows' Data Sources. Mirrors
    /// <see cref="ResolveHardSkillsRoleAsync"/>: the assigned role wins when it names a tool,
    /// otherwise the institute's row of the same name does. Returns empty (no queries) when the
    /// report contains no hard-skills rows.
    /// </summary>
    private async Task<Dictionary<int, string?>> BuildMainToolLookupAsync(
        IReadOnlyList<CacheMetrics> rows, CancellationToken ct)
    {
        var studentIds = rows.Where(r => r.MetricId == HardSkillsMetricId)
            .Select(r => r.StudentId).Distinct().ToList();
        var lookup = new Dictionary<int, string?>();
        if (studentIds.Count == 0) return lookup;

        var students = await _context.Students
            .AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .Include(s => s.StudentRoles)
                .ThenInclude(sr => sr.Role)
                    .ThenInclude(r => r!.Skill)
            .ToListAsync(ct);

        foreach (var s in students)
        {
            var assigned = s.StudentRoles?.FirstOrDefault(sr => sr.IsActive)?.Role
                           ?? s.StudentRoles?.FirstOrDefault()?.Role;
            var tool = assigned?.Skill?.Name?.Trim();

            if (string.IsNullOrEmpty(tool) && assigned != null && s.InstituteId is > 0)
            {
                var name = (assigned.Name ?? string.Empty).Trim().ToLower();
                tool = await _context.Roles
                    .AsNoTracking()
                    .Where(r => r.InstituteId == s.InstituteId && r.IsActive
                                && r.Name.ToLower() == name && r.SkillId != null)
                    .OrderBy(r => r.SquadId == null ? 0 : 1)
                    .ThenBy(r => r.Id)
                    .Select(r => r.Skill!.Name)
                    .FirstOrDefaultAsync(ct);
            }

            lookup[s.Id] = string.IsNullOrWhiteSpace(tool) ? null : tool.Trim();
        }

        return lookup;
    }

    internal static AssessmentReportMetricDto MapMetricDto(CacheMetrics row, string? mainTool = null)
    {
        // The sentinel rows are named "SprintSummary" (Id=0) and "HardSkills" (Id=-1) in the Metrics
        // table (see the SeedSummaryMetricRow / SeedHardSkillsMetricRow migrations); the report shows
        // the friendlier display names.
        var name = row.MetricId switch
        {
            SummaryMetricId    => "Sprint Summary",
            HardSkillsMetricId => "Professional Skills",
            _                  => row.Metric?.Name?.Trim(),
        };
        if (string.IsNullOrEmpty(name))
            name = $"Metric {row.MetricId}";

        return new AssessmentReportMetricDto
        {
            MetricId = row.MetricId,
            MetricName = name,
            ReviewContent = row.ReviewContent ?? "",
            Graph = string.IsNullOrWhiteSpace(row.Graph) ? null : row.Graph.Trim(),
            Graph2 = string.IsNullOrWhiteSpace(row.Graph2) ? null : row.Graph2.Trim(),
            Sensors = ResolveReportSensors(row, mainTool)
        };
    }

    /// <summary>
    /// On-demand: run all configured metric endpoints for a single student + sprint.
    /// Used by the staff dashboard when a sprint has no cached assessment data yet.
    /// Route: POST /api/Metrics/use/run-student-sprint
    /// </summary>
    [HttpPost("use/run-student-sprint")]
    public async Task<ActionResult> RunStudentSprintAssessment([FromBody] RunStudentSprintRequest? request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.BoardId) || request.StudentId <= 0 || request.SprintNumber < 0)
            return BadRequest(new { success = false, message = "BoardId, StudentId (> 0), and SprintNumber (>= 0) are required." });

        var boardId = request.BoardId.Trim();

        var student = await _context.Students.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);
        if (student == null)
            return NotFound(new { success = false, message = $"Student {request.StudentId} not found." });
        if (!string.Equals(student.BoardId?.Trim(), boardId, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, message = "Student is not assigned to this board." });

        if (request.InstituteId <= 0)
            return BadRequest(new { success = false, message = "InstituteId is required." });

        // A merge row with MergedAt=null and ListId=null is the pre-created "next sprint" trigger row
        // (MergeType=Add) — that sprint has no Trello list yet, so running metrics for it would cache
        // phantom assessment rows the report then displays. Sprint 0 (Bugs) has no merge row by design.
        if (request.SprintNumber >= 1)
        {
            var sprintExists = await _context.ProjectBoardSprintMerges.AsNoTracking()
                .AnyAsync(m => m.ProjectBoardId == boardId && m.SprintNumber == request.SprintNumber &&
                               (m.MergedAt != null || m.ListId != null), cancellationToken);
            if (!sprintExists)
                return BadRequest(new { success = false, message = $"Sprint {request.SprintNumber} does not exist on this board (no merged sprint list). Assessment not run." });
        }

        var metrics = await _context.Metrics.AsNoTracking()
            .Where(m => m.InstituteId == request.InstituteId && m.Required)
            .ToListAsync(cancellationToken);

        if (!metrics.Any())
            return Ok(new { success = false, message = "No active metrics configured for this institute." });

        // Hard skills is a sentinel, not an institute-owned metric row, so the query above can never
        // return it. Append it so it runs like any other metric — the switch below dispatches it by
        // name to RunHardSkillsAssessment, which takes its rubric from the student's role.
        metrics.Add(HardSkillsBatchMetric());

        if (request.MetricId.HasValue)
        {
            metrics = metrics.Where(m => m.Id == request.MetricId.Value).ToList();
            if (!metrics.Any())
                return BadRequest(new { success = false, message = $"Metric {request.MetricId.Value} is not an active metric of institute {request.InstituteId}." });
        }

        if (request.ForceRefresh)
        {
            var staleQuery = _context.CacheMetrics
                .Where(c => c.BoardId == boardId && c.StudentId == request.StudentId && c.SprintNumber == request.SprintNumber);
            // Single-metric re-collect must not wipe the other metrics' cached rows — they would not
            // be regenerated by this run.
            if (request.MetricId.HasValue)
                staleQuery = staleQuery.Where(c => c.MetricId == request.MetricId.Value);
            var stale = await staleQuery.ToListAsync(cancellationToken);
            if (stale.Count > 0)
            {
                _context.CacheMetrics.RemoveRange(stale);
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("[RUN-STUDENT-SPRINT] ForceRefresh: deleted {Count} stale CacheMetrics rows for student {StudentId}, sprint {Sprint}, metric {MetricId}.", stale.Count, request.StudentId, request.SprintNumber, request.MetricId?.ToString() ?? "(all)");
            }
        }

        var errors = new List<string>();
        foreach (var metric in metrics)
        {
            try
            {
                IActionResult? result;
                switch (ToSlugKey(metric.Name))
                {
                    case "adherence":
                        result = (await Adherence(new AdherenceRequest { BoardId = boardId, StudentId = request.StudentId, SprintNumber = request.SprintNumber, MetricIdOverride = metric.Id }, cancellationToken)).Result;
                        break;
                    case "gapanalysis":
                        result = (await GapAnalysis(new GapAnalysisRequest { BoardId = boardId, StudentId = request.StudentId, SprintNumber = request.SprintNumber, MetricIdOverride = metric.Id }, cancellationToken)).Result;
                        break;
                    case "attendance":
                        result = (await Attendance(boardId, request.SprintNumber, request.StudentId, cancellationToken, metricIdOverride: metric.Id)).Result;
                        break;
                    case "customerengagement":
                        result = (await CustomerEngagement(new CustomerEngagementRequest { BoardId = boardId, StudentId = request.StudentId, SprintNumber = request.SprintNumber, MetricIdOverride = metric.Id }, cancellationToken)).Result;
                        break;
                    case "communication":
                        result = (await MeetingsCommunication(new MeetingsCommunicationRequest { BoardId = boardId, StudentId = request.StudentId, SprintNumber = request.SprintNumber, MetricIdOverride = metric.Id }, cancellationToken)).Result;
                        break;
                    case HardSkillsMetricSlug:
                        result = await RunHardSkillsAssessment(
                            new HardSkillsAssessmentRequest(boardId, request.StudentId, request.SprintNumber),
                            cancellationToken);
                        break;
                    default:
                        result = await RunAssessmentEngine(new AssessmentEngineRequest(
                            MetricId: metric.Id,
                            BoardId: boardId,
                            StudentId: request.StudentId,
                            SprintNumber: request.SprintNumber), cancellationToken);
                        break;
                }

                // Every handler above returns the outcome via its ActionResult (Ok/BadRequest/NotFound/
                // UnprocessableEntity/StatusCode) — previously this loop only awaited the call and
                // discarded that result, so a handler returning e.g. UnprocessableEntity (invalid LLM
                // JSON — see TryParseGapAnalysisJson) was silently treated as success: no CacheMetrics
                // row was written, nothing landed in `errors`, and the "OK" log line fired anyway.
                var statusCode = (result as IStatusCodeActionResult)?.StatusCode;
                var isSuccess = IsSuccessStatusCode(statusCode);
                if (isSuccess)
                {
                    _logger.LogInformation("[RUN-STUDENT-SPRINT] Metric {MetricId} ({MetricName}) OK for student {StudentId}, sprint {Sprint}.", metric.Id, metric.Name, request.StudentId, request.SprintNumber);
                }
                else
                {
                    var detail = DescribeFailedActionResult(result);
                    errors.Add($"Metric {metric.Id} ({metric.Name}): HTTP {statusCode?.ToString() ?? "?"} — {detail}");
                    _logger.LogWarning(
                        "[RUN-STUDENT-SPRINT] Metric {MetricId} ({MetricName}) failed for student {StudentId}, sprint {Sprint}: HTTP {StatusCode} {Detail}",
                        metric.Id, metric.Name, request.StudentId, request.SprintNumber, statusCode, detail);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Metric {metric.Id} ({metric.Name}): {ex.Message}");
                _logger.LogWarning(ex, "[RUN-STUDENT-SPRINT] Metric {MetricId} failed for student {StudentId}, sprint {Sprint}: {Error}", metric.Id, request.StudentId, request.SprintNumber, ex.Message);
            }
        }

        return Ok(new { success = true, errors });
    }

    /// <summary>True only for a determinable 2xx status code — null (no status code found on the result) and anything outside 200-299 count as failure.</summary>
    internal static bool IsSuccessStatusCode(int? statusCode) => statusCode is >= 200 and < 300;

    /// <summary>Best-effort JSON dump of a non-2xx ActionResult's payload, for the errors list above.</summary>
    internal static string DescribeFailedActionResult(IActionResult? result)
    {
        if (result is ObjectResult { Value: { } value })
        {
            try { return System.Text.Json.JsonSerializer.Serialize(value); }
            catch { return value.ToString() ?? "(no detail)"; }
        }
        return result?.GetType().Name ?? "(null result)";
    }

    public sealed class RunStudentSprintRequest
    {
        public string BoardId { get; set; } = "";
        public int StudentId { get; set; }
        public int SprintNumber { get; set; }
        public int InstituteId { get; set; }
        public bool ForceRefresh { get; set; }

        /// <summary>When set, only this metric is (re-)run — and ForceRefresh deletes only its row, not the whole student/sprint cache.</summary>
        public int? MetricId { get; set; }
    }

    public sealed class SquadNameSearchResponseDto
    {
        public IReadOnlyList<SquadNameSearchItemDto> Items { get; set; } = Array.Empty<SquadNameSearchItemDto>();
    }

    public sealed class SquadNameSearchItemDto
    {
        public string BoardId { get; set; } = "";
        public string SquadName { get; set; } = "";
    }

    public sealed class AssessmentReportDto
    {
        public string Headline { get; set; } = "";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BoardId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SquadName { get; set; }

        public IReadOnlyList<AssessmentReportSprintDto> Sprints { get; set; } = Array.Empty<AssessmentReportSprintDto>();

        /// <summary>Per-student course-level summaries (CacheMetrics rows at SprintNumber=-1, MetricId=0). Empty until "Course Summary" is generated.</summary>
        public IReadOnlyList<AssessmentReportCourseSummaryDto> CourseSummaries { get; set; } = Array.Empty<AssessmentReportCourseSummaryDto>();
    }

    public sealed class AssessmentReportCourseSummaryDto
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = "";
        public string ReviewContent { get; set; } = "";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Graph { get; set; }

        /// <summary>Course-level hard skills (MetricId=-1, SprintNumber=-1). Null until one is generated.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? HardSkillsReviewContent { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? HardSkillsGraph { get; set; }
    }

    public sealed class AssessmentReportSprintDto
    {
        public int SprintNumber { get; set; }

        /// <summary>Sprint window, when it resolves. Null for reports spanning several boards.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? StartDate { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? DueDate { get; set; }
        public IReadOnlyList<AssessmentReportStudentDto> Students { get; set; } = Array.Empty<AssessmentReportStudentDto>();
    }

    public sealed class AssessmentReportStudentDto
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = "";
        public IReadOnlyList<AssessmentReportMetricDto> Metrics { get; set; } = Array.Empty<AssessmentReportMetricDto>();
    }

    public sealed class AssessmentReportMetricDto
    {
        public int MetricId { get; set; }
        public string MetricName { get; set; } = "";
        public string ReviewContent { get; set; } = "";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Graph { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Graph2 { get; set; }

        /// <summary>Display names of the sensors enabled on this metric, for the report's Data Sources list.</summary>
        public IReadOnlyList<string> Sensors { get; set; } = Array.Empty<string>();
    }
}
