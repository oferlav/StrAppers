using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;

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
            .Where(c => c.BoardId == boardIdTrim);

        if (roleId.HasValue)
            query = query.Where(c => c.Student!.StudentRoles.Any(sr => sr.RoleId == roleId.Value && sr.IsActive));
        if (studentId.HasValue)
            query = query.Where(c => c.StudentId == studentId.Value);
        if (sprintNumber.HasValue)
            query = query.Where(c => c.SprintNumber == sprintNumber.Value);

        var rows = await query
            .OrderBy(c => c.SprintNumber)
            .ThenBy(c => c.StudentId)
            .ThenBy(c => c.MetricId)
            .ToListAsync(cancellationToken);

        var sprints = BuildAssessmentSprints(rows, includeSquadInStudentName: false);

        return Ok(new AssessmentReportDto
        {
            Headline = headline,
            BoardId = boardIdTrim,
            SquadName = resolvedSquadName,
            Sprints = sprints
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
            var hasRole = await _context.StudentRoles.AsNoTracking()
                .AnyAsync(sr => sr.StudentId == studentId.Value && sr.RoleId == roleId && sr.IsActive, cancellationToken);
            if (!hasRole)
                return BadRequest(new { success = false, message = "Student does not have this active role." });

            var name = $"{student.FirstName} {student.LastName}".Trim();
            headline = $"Assessment Report for {name} ({roleName})";
        }
        else
        {
            headline = $"Assessment Report — {roleName} (all squads)";
        }

        var query = _context.CacheMetrics
            .AsNoTracking()
            .Include(c => c.Metric)
            .Include(c => c.Student)
            .Include(c => c.ProjectBoard)
            .Where(c => c.Student!.StudentRoles.Any(sr => sr.RoleId == roleId && sr.IsActive));

        if (studentId.HasValue)
            query = query.Where(c => c.StudentId == studentId.Value);
        if (sprintNumber.HasValue)
            query = query.Where(c => c.SprintNumber == sprintNumber.Value);

        var rows = await query
            .OrderBy(c => c.SprintNumber)
            .ThenBy(c => c.StudentId)
            .ThenBy(c => c.MetricId)
            .ToListAsync(cancellationToken);

        var sprints = BuildAssessmentSprints(rows, includeSquadInStudentName: true);

        return Ok(new AssessmentReportDto
        {
            Headline = headline,
            BoardId = null,
            SquadName = null,
            Sprints = sprints
        });
    }

    private static IReadOnlyList<AssessmentReportSprintDto> BuildAssessmentSprints(
        IReadOnlyList<CacheMetrics> rows,
        bool includeSquadInStudentName = false)
    {
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
                    Metrics = studentGroup.OrderBy(r => r.MetricId).Select(MapMetricDto).ToList()
                });
            }

            sprints.Add(new AssessmentReportSprintDto
            {
                SprintNumber = sprintGroup.Key,
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

    private static AssessmentReportMetricDto MapMetricDto(CacheMetrics row)
    {
        var name = row.Metric?.Name?.Trim();
        if (string.IsNullOrEmpty(name))
            name = $"Metric {row.MetricId}";

        return new AssessmentReportMetricDto
        {
            MetricId = row.MetricId,
            MetricName = name,
            ReviewContent = row.ReviewContent ?? "",
            Graph = string.IsNullOrWhiteSpace(row.Graph) ? null : row.Graph.Trim(),
            Graph2 = string.IsNullOrWhiteSpace(row.Graph2) ? null : row.Graph2.Trim()
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

        var metrics = await _context.Metrics.AsNoTracking()
            .Where(m => m.InstituteId == request.InstituteId && m.Required)
            .ToListAsync(cancellationToken);

        if (!metrics.Any())
            return Ok(new { success = false, message = "No active metrics configured for this institute." });

        var errors = new List<string>();
        foreach (var metric in metrics)
        {
            try
            {
                switch (metric.Endpoint?.Trim().ToLowerInvariant())
                {
                    case "adherence":
                        await Adherence(new AdherenceRequest { BoardId = boardId, StudentId = request.StudentId, SprintNumber = request.SprintNumber }, cancellationToken);
                        break;
                    case "gapanalysis":
                        await GapAnalysis(new GapAnalysisRequest { BoardId = boardId, StudentId = request.StudentId, SprintNumber = request.SprintNumber }, cancellationToken);
                        break;
                    case "attendance":
                        await Attendance(boardId, request.SprintNumber, request.StudentId, cancellationToken);
                        break;
                    case "customerengagement":
                        await CustomerEngagement(new CustomerEngagementRequest { BoardId = boardId, StudentId = request.StudentId, SprintNumber = request.SprintNumber }, cancellationToken);
                        break;
                    case "meetingscommunication":
                        await MeetingsCommunication(new MeetingsCommunicationRequest { BoardId = boardId, StudentId = request.StudentId, SprintNumber = request.SprintNumber }, cancellationToken);
                        break;
                    default:
                        await RunAssessmentEngine(new AssessmentEngineRequest(
                            MetricId: metric.Id,
                            BoardId: boardId,
                            StudentId: request.StudentId,
                            SprintNumber: request.SprintNumber), cancellationToken);
                        break;
                }
                _logger.LogInformation("[RUN-STUDENT-SPRINT] Metric {MetricId} ({MetricName}) OK for student {StudentId}, sprint {Sprint}.", metric.Id, metric.Name, request.StudentId, request.SprintNumber);
            }
            catch (Exception ex)
            {
                errors.Add($"Metric {metric.Id} ({metric.Name}): {ex.Message}");
                _logger.LogWarning(ex, "[RUN-STUDENT-SPRINT] Metric {MetricId} failed for student {StudentId}, sprint {Sprint}: {Error}", metric.Id, request.StudentId, request.SprintNumber, ex.Message);
            }
        }

        return Ok(new { success = true, errors });
    }

    public sealed class RunStudentSprintRequest
    {
        public string BoardId { get; set; } = "";
        public int StudentId { get; set; }
        public int SprintNumber { get; set; }
        public int InstituteId { get; set; }
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
    }

    public sealed class AssessmentReportSprintDto
    {
        public int SprintNumber { get; set; }
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
    }
}
