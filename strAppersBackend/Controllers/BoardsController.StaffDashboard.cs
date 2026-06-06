using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;
using strAppersBackend.Utilities;

namespace strAppersBackend.Controllers;

/// <summary>Staff dashboard: squad cards backed by <see cref="ProjectBoard"/> + students.</summary>
public sealed class StaffSquadStudentDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Photo { get; set; }
    public List<int> RoleIds { get; set; } = new();
    public List<string> RoleNames { get; set; } = new();
    public bool AssistMe { get; set; }
    public DateTime? NextMeetingTime { get; set; }
    public string? NextMeetingUrl { get; set; }
}

public sealed class StaffSquadRowDto
{
    public string BoardId { get; set; } = string.Empty;
    public string? SquadName { get; set; }
    public int ProjectId { get; set; }
    public string ProjectTitle { get; set; } = string.Empty;
    public int? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public string? PublishUrl { get; set; }
    public string? WebApiUrl { get; set; }
    public string? GithubFrontendUrl { get; set; }
    public string? GithubBackendUrl { get; set; }
    public string? BoardUrl { get; set; }
    public DateTime? NextMeetingTime { get; set; }
    public string? NextMeetingTitle { get; set; }
    public string? NextMeetingUrl { get; set; }

    /// <summary>True after institute &quot;Get Invited&quot; email was sent for the current next meeting.</summary>
    public bool NextMeetingTeacherAttendance { get; set; }

    public int? CurrentSprintNumber { get; set; }
    public string? CurrentSprintLabel { get; set; }

    /// <summary>Sprint numbers that have a <see cref="ProjectBoardSprintMerge"/> row (for staff filters).</summary>
    public List<int> MergedSprintNumbers { get; set; } = new();

    /// <summary>UTC end (inclusive) of the final planned sprint for this board, or null if unknown.</summary>
    public DateTime? LastSprintEndUtc { get; set; }

    public List<StaffSquadStudentDto> Students { get; set; } = new();
}

public sealed class StaffSquadsResponseDto
{
    public List<StaffSquadRowDto> Squads { get; set; } = new();

    /// <summary>Distinct sprint numbers that appear in merge rows (for filter dropdowns).</summary>
    public List<int> ObservedSprintNumbers { get; set; } = new();
}

public sealed class StaffSquadAssistStudentDto
{
    public int Id { get; set; }
    public bool AssistMe { get; set; }

    /// <summary>Student-level next meeting (e.g. after staff schedules help) — keeps boardroom Human control in sync.</summary>
    public DateTime? NextMeetingTime { get; set; }

    public string? NextMeetingUrl { get; set; }
}

public sealed class StaffSquadAssistRowDto
{
    public string BoardId { get; set; } = string.Empty;
    public List<StaffSquadAssistStudentDto> Students { get; set; } = new();
}

public sealed class StaffSquadsAssistResponseDto
{
    public List<StaffSquadAssistRowDto> Squads { get; set; } = new();
}

public partial class BoardsController
{
    private static readonly JsonSerializerOptions StaffSprintJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Live project boards (non-system) with org/project links, sprint window hint, and onboard students for the staff dashboard.
    /// Route: GET /api/Boards/use/staff-squads?instituteId={id}
    /// </summary>
    [HttpGet("staff-squads")]
    public async Task<ActionResult<StaffSquadsResponseDto>> GetStaffSquads([FromQuery] int? instituteId, CancellationToken cancellationToken)
    {
        try
        {
            if (!instituteId.HasValue)
            {
                return Ok(new StaffSquadsResponseDto
                {
                    Squads = new List<StaffSquadRowDto>(),
                    ObservedSprintNumbers = new List<int>(),
                });
            }

            var sprintLengthWeeks = _configuration.GetValue<int>("BusinessLogicConfig:SprintLengthInWeeks", 1);
            var nowUtc = DateTime.UtcNow;

            var boards = await _context.ProjectBoards
                .AsNoTracking()
                .Include(pb => pb.Project)
                .ThenInclude(p => p.Organization)
                .Include(pb => pb.SprintMerges)
                .Where(pb => !pb.IsSystemBoard && pb.Project.IsAvailable && pb.InstituteId == instituteId.Value)
                .OrderBy(pb => pb.Project.Title)
                .ThenBy(pb => pb.SquadName)
                .ThenBy(pb => pb.Id)
                .ToListAsync(cancellationToken);

            if (boards.Count == 0)
            {
                return Ok(new StaffSquadsResponseDto
                {
                    Squads = new List<StaffSquadRowDto>(),
                    ObservedSprintNumbers = new List<int>(),
                });
            }

            var boardIds = boards.Select(b => b.Id).ToList();
            var students = await _context.Students
                .AsNoTracking()
                .Include(s => s.StudentRoles)
                .ThenInclude(sr => sr.Role)
                .Where(s => s.BoardId != null && boardIds.Contains(s.BoardId) && s.IsAvailable)
                .ToListAsync(cancellationToken);

            var byBoard = students
                .GroupBy(s => s.BoardId!)
                .ToDictionary(g => g.Key, g => g.ToList());

            var showAllStaffSquads = _testingConfig.Value.ShowAllStaffSquads;
            var boardsToProcess = showAllStaffSquads
                ? boards
                : boards.Where(b => byBoard.TryGetValue(b.Id, out var list) && list.Count > 0).ToList();

            var observedSprints = new HashSet<int>();
            foreach (var b in boardsToProcess)
            {
                foreach (var n in GetRealMergedSprintNumbers(b))
                    observedSprints.Add(n);
            }

            if (boardsToProcess.Count == 0)
            {
                return Ok(new StaffSquadsResponseDto
                {
                    Squads = new List<StaffSquadRowDto>(),
                    ObservedSprintNumbers = observedSprints.OrderBy(n => n).ToList(),
                });
            }

            var rows = new List<StaffSquadRowDto>(boardsToProcess.Count);
            foreach (var pb in boardsToProcess)
            {
                byBoard.TryGetValue(pb.Id, out var boardStudents);
                boardStudents ??= new List<Student>();

                var (currentN, label) = ResolveCurrentSprint(pb, sprintLengthWeeks, nowUtc);

                var studentDtos = boardStudents
                    .OrderBy(s => s.LastName)
                    .ThenBy(s => s.FirstName)
                    .Select(s =>
                    {
                        var active = s.StudentRoles.Where(sr => sr.IsActive && sr.Role != null).ToList();
                        return new StaffSquadStudentDto
                        {
                            Id = s.Id,
                            FirstName = s.FirstName,
                            LastName = s.LastName,
                            Photo = s.Photo,
                            RoleIds = active.Select(sr => sr.RoleId).ToList(),
                            RoleNames = active.Select(sr => sr.Role!.Name).ToList(),
                            AssistMe = s.AssistMe,
                            NextMeetingTime = s.NextMeetingTime,
                            NextMeetingUrl = s.NextMeetingUrl,
                        };
                    })
                    .ToList();

                rows.Add(new StaffSquadRowDto
                {
                    BoardId = pb.Id,
                    SquadName = pb.SquadName,
                    ProjectId = pb.ProjectId,
                    ProjectTitle = pb.Project.Title,
                    OrganizationId = pb.Project.OrganizationId,
                    OrganizationName = pb.Project.Organization?.Name,
                    PublishUrl = pb.PublishUrl,
                    WebApiUrl = pb.WebApiUrl,
                    GithubFrontendUrl = pb.GithubFrontendUrl,
                    GithubBackendUrl = pb.GithubBackendUrl,
                    BoardUrl = pb.BoardUrl,
                    NextMeetingTime = pb.NextMeetingTime,
                    NextMeetingTitle = pb.NextMeetingTitle,
                    NextMeetingUrl = pb.NextMeetingUrl,
                    NextMeetingTeacherAttendance = pb.NextMeetingTeacherAttendance,
                    CurrentSprintNumber = currentN,
                    CurrentSprintLabel = label,
                    MergedSprintNumbers = GetRealMergedSprintNumbers(pb),
                    LastSprintEndUtc = ComputeLastSprintEndUtc(pb, sprintLengthWeeks),
                    Students = studentDtos,
                });

                if (currentN.HasValue)
                    observedSprints.Add(currentN.Value);
            }

            return Ok(new StaffSquadsResponseDto
            {
                Squads = rows,
                ObservedSprintNumbers = observedSprints.OrderBy(n => n).ToList(),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building staff-squads payload");
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Staff dashboard: poll-only payload with <see cref="Student.AssistMe"/> per student (lightweight). GET /api/Boards/use/staff-squads-assist?instituteId={id} — no success-path logging (errors only in <c>catch</c>).
    /// </summary>
    [HttpGet("staff-squads-assist")]
    public async Task<ActionResult<StaffSquadsAssistResponseDto>> GetStaffSquadsAssist([FromQuery] int? instituteId, CancellationToken cancellationToken)
    {
        try
        {
            if (!instituteId.HasValue)
                return Ok(new StaffSquadsAssistResponseDto { Squads = new List<StaffSquadAssistRowDto>() });

            var boards = await _context.ProjectBoards
                .AsNoTracking()
                .Include(pb => pb.Project)
                .Where(pb => !pb.IsSystemBoard && pb.Project.IsAvailable && pb.InstituteId == instituteId.Value)
                .OrderBy(pb => pb.Project!.Title)
                .ThenBy(pb => pb.SquadName)
                .ThenBy(pb => pb.Id)
                .ToListAsync(cancellationToken);

            if (boards.Count == 0)
            {
                return Ok(new StaffSquadsAssistResponseDto { Squads = new List<StaffSquadAssistRowDto>() });
            }

            var boardIds = boards.Select(b => b.Id).ToList();
            var studentRows = await _context.Students
                .AsNoTracking()
                .Where(s => s.BoardId != null && boardIds.Contains(s.BoardId) && s.IsAvailable)
                .Select(s => new { s.BoardId, s.Id, s.AssistMe, s.NextMeetingTime, s.NextMeetingUrl })
                .ToListAsync(cancellationToken);

            var byBoard = studentRows
                .GroupBy(s => s.BoardId!)
                .ToDictionary(g => g.Key, g => g.ToList());

            var showAllStaffSquads = _testingConfig.Value.ShowAllStaffSquads;
            var boardsToProcess = showAllStaffSquads
                ? boards
                : boards.Where(b => byBoard.TryGetValue(b.Id, out var list) && list.Count > 0).ToList();

            var rows = new List<StaffSquadAssistRowDto>(boardsToProcess.Count);
            foreach (var pb in boardsToProcess)
            {
                if (!byBoard.TryGetValue(pb.Id, out var slist) || slist.Count == 0)
                {
                    rows.Add(new StaffSquadAssistRowDto
                    {
                        BoardId = pb.Id,
                        Students = new List<StaffSquadAssistStudentDto>(),
                    });
                    continue;
                }

                rows.Add(new StaffSquadAssistRowDto
                {
                    BoardId = pb.Id,
                    Students = slist
                        .OrderBy(x => x.Id)
                        .Select(x => new StaffSquadAssistStudentDto
                        {
                            Id = x.Id,
                            AssistMe = x.AssistMe,
                            NextMeetingTime = x.NextMeetingTime,
                            NextMeetingUrl = x.NextMeetingUrl,
                        })
                        .ToList(),
                });
            }

            return Ok(new StaffSquadsAssistResponseDto { Squads = rows });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building staff-squads-assist payload");
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Distinct active roles actually assigned to students in the institute's boards.
    /// Returns only roles in use — scoped to visible squads, correct IDs for filtering.
    /// Route: GET /api/Boards/use/institute-squad-roles?instituteId={id}
    /// </summary>
    [HttpGet("institute-squad-roles")]
    public async Task<ActionResult<List<object>>> GetInstituteSquadRoles([FromQuery] int? instituteId, CancellationToken cancellationToken)
    {
        try
        {
            if (!instituteId.HasValue || instituteId.Value <= 0)
                return Ok(Array.Empty<object>());

            var boardIds = await _context.ProjectBoards
                .AsNoTracking()
                .Where(pb => !pb.IsSystemBoard && pb.InstituteId == instituteId.Value && pb.Project.IsAvailable)
                .Select(pb => pb.Id)
                .ToListAsync(cancellationToken);

            if (boardIds.Count == 0)
                return Ok(Array.Empty<object>());

            var roles = await _context.StudentRoles
                .AsNoTracking()
                .Where(sr => sr.IsActive
                    && sr.Role != null
                    && sr.Student.IsAvailable
                    && sr.Student.BoardId != null
                    && boardIds.Contains(sr.Student.BoardId))
                .Select(sr => new { sr.Role!.Id, sr.Role!.Name })
                .Distinct()
                .OrderBy(r => r.Name)
                .ToListAsync(cancellationToken);

            var result = roles
                .DistinctBy(r => r.Id)
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Select(r => new { id = r.Id, name = r.Name })
                .Cast<object>()
                .ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building institute-squad-roles payload");
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Sprint numbers from all existing SprintMerge rows, ordered ascending.
    /// </summary>
    private static List<int> GetRealMergedSprintNumbers(ProjectBoard pb) =>
        pb.SprintMerges
            .Select(m => m.SprintNumber)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

    /// <summary>End of the highest-numbered sprint window (merge row preferred, else plan).</summary>
    private static DateTime? ComputeLastSprintEndUtc(ProjectBoard pb, int sprintLengthWeeks)
    {
        var mergesByN = pb.SprintMerges.ToDictionary(m => m.SprintNumber, m => m);
        var realNums = GetRealMergedSprintNumbers(pb);
        var maxFromMerges = realNums.Count > 0 ? realNums.Max() : 0;
        var maxFromPlan = TryReadTotalSprints(pb.SprintPlan);
        var maxS = Math.Max(Math.Max(maxFromMerges, maxFromPlan), 1);
        maxS = Math.Min(maxS, 36);

        mergesByN.TryGetValue(maxS, out var mergeForLast);
        if (mergeForLast != null &&
            SprintPlanDateResolver.TryGetInclusiveUtcRangeFromSprintMerge(mergeForLast, maxS, sprintLengthWeeks, out _, out var endM))
        {
            return endM;
        }

        if (SprintPlanDateResolver.TryGetSprintInclusiveUtcRange(pb.SprintPlan, pb.StartDate, maxS, out _, out var endP))
        {
            return endP;
        }

        return null;
    }

    private static (int? sprintNumber, string? label) ResolveCurrentSprint(ProjectBoard pb, int sprintLengthWeeks, DateTime nowUtc)
    {
        var mergesByN = pb.SprintMerges.ToDictionary(m => m.SprintNumber, m => m);
        var realNums = GetRealMergedSprintNumbers(pb);
        var maxFromMerges = realNums.Count > 0 ? realNums.Max() : 0;
        var maxFromPlan = TryReadTotalSprints(pb.SprintPlan);
        var maxS = Math.Max(Math.Max(maxFromMerges, maxFromPlan), 1);
        maxS = Math.Min(maxS, 36);

        for (var n = 1; n <= maxS; n++)
        {
            mergesByN.TryGetValue(n, out var merge);
            if (merge != null &&
                SprintPlanDateResolver.TryGetInclusiveUtcRangeFromSprintMerge(merge, n, sprintLengthWeeks, out var startM, out var endM))
            {
                if (nowUtc >= startM && nowUtc <= endM)
                {
                    return (n, $"Sprint {n}");
                }
            }

            if (SprintPlanDateResolver.TryGetSprintInclusiveUtcRange(pb.SprintPlan, pb.StartDate, n, out var startP, out var endP))
            {
                if (nowUtc >= startP && nowUtc <= endP)
                {
                    return (n, $"Sprint {n}");
                }
            }
        }

        // Next upcoming sprint (smallest n where start > now)
        for (var n = 1; n <= maxS; n++)
        {
            mergesByN.TryGetValue(n, out var merge);
            DateTime? startUtc = null;
            if (merge != null &&
                SprintPlanDateResolver.TryGetInclusiveUtcRangeFromSprintMerge(merge, n, sprintLengthWeeks, out var sm, out _))
            {
                startUtc = sm;
            }
            else if (SprintPlanDateResolver.TryGetSprintInclusiveUtcRange(pb.SprintPlan, pb.StartDate, n, out var sp, out _))
            {
                startUtc = sp;
            }

            if (startUtc.HasValue && nowUtc < startUtc.Value)
            {
                return (n, $"Sprint {n}");
            }
        }

        if (maxFromMerges > 0)
        {
            return (maxFromMerges, $"Sprint {maxFromMerges}");
        }

        return (null, null);
    }

    private static string FormatSprintLabel(int sprintNumber, DateTime startUtc, DateTime endInclusiveUtc)
    {
        var s = startUtc.ToUniversalTime();
        var e = endInclusiveUtc.ToUniversalTime();
        var c = CultureInfo.InvariantCulture;
        var range = s.Month == e.Month && s.Year == e.Year
            ? $"{s.ToString("MMM", c)} {s.Day}–{e.Day}, {e.Year}"
            : $"{s.ToString("MMM d", c)} – {e.ToString("MMM d, yyyy", c)}";
        return $"Sprint {sprintNumber} · {range}";
    }

    private static int TryReadTotalSprints(string? sprintPlanJson)
    {
        if (string.IsNullOrWhiteSpace(sprintPlanJson))
            return 0;
        try
        {
            var plan = JsonSerializer.Deserialize<TrelloSprintPlan>(sprintPlanJson, StaffSprintJsonOptions);
            if (plan == null)
                return 0;
            if (plan.TotalSprints > 0)
                return plan.TotalSprints;
            if (plan.Lists == null || plan.Lists.Count == 0)
                return 0;
            return plan.Lists.Count(l =>
                !string.IsNullOrWhiteSpace(l.Name) &&
                l.Name.Contains("Sprint", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return 0;
        }
    }
}
