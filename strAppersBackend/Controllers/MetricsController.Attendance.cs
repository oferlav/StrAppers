using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Utilities;

namespace strAppersBackend.Controllers;

public partial class MetricsController
{
    /// <summary>Matches <see cref="Metric"/> seed Id for Attendance.</summary>
    private const int AttendanceMetricId = 5;

    /// <summary>
    /// Sprint team-meeting participation from <see cref="BoardMeeting"/> rows for this board + student email,
    /// scoped to the sprint calendar window (same resolution as CRM gap analysis).
    /// Persists summary + chart to <see cref="CacheMetrics"/> (MetricId 5).
    /// </summary>
    [HttpGet("use/Attendance")]
    public async Task<ActionResult<object>> Attendance(
        [FromQuery] string boardId,
        [FromQuery] int sprintNumber,
        [FromQuery] int studentId,
        CancellationToken cancellationToken,
        [FromQuery] int? metricIdOverride = null)
    {
        if (string.IsNullOrWhiteSpace(boardId))
            return BadRequest(new { success = false, message = "BoardId is required." });
        if (studentId <= 0)
            return BadRequest(new { success = false, message = "StudentId is required." });
        if (sprintNumber < 0)
            return BadRequest(new { success = false, message = "SprintNumber must be >= 0." });

        var boardIdTrim = boardId.Trim();
        var metricId = metricIdOverride ?? AttendanceMetricId;

        var student = await _context.Students.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);
        if (student == null)
            return NotFound(new { success = false, message = $"Student {studentId} not found." });
        if (!string.Equals(student.BoardId?.Trim(), boardIdTrim, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, message = "Student is not assigned to this BoardId." });

        var board = await _context.ProjectBoards.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == boardIdTrim, cancellationToken);
        if (board == null)
            return NotFound(new { success = false, message = $"Board {boardIdTrim} not found." });

        var email = student.Email?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            const string summary = "Attendance cannot be evaluated: the student has no email on file (meetings are tracked by email).";
            var graphNoEmail = AttendanceChartRenderer.ToBase64Png(AttendanceChartRenderer.RenderPng(0, 0));
            await UpsertCacheMetricsAsync(boardIdTrim, studentId, sprintNumber, metricId, summary, graphNoEmail, cancellationToken);
            return Ok(new
            {
                success = true,
                boardId = boardIdTrim,
                sprintNumber,
                studentId,
                metricId = metricId,
                scheduleResolved = false,
                attendedCount = 0,
                totalScheduled = 0,
                participationPercent = (int?)null,
                summary,
                graphBase64 = graphNoEmail,
            });
        }

        if (sprintNumber == 0)
        {
            const string summary = "Sprint 0 is not used for team meeting attendance metrics.";
            var graphS0 = AttendanceChartRenderer.ToBase64Png(AttendanceChartRenderer.RenderPng(0, 0));
            await UpsertCacheMetricsAsync(boardIdTrim, studentId, sprintNumber, metricId, summary, graphS0, cancellationToken);
            return Ok(new
            {
                success = true,
                boardId = boardIdTrim,
                sprintNumber,
                studentId,
                metricId = metricId,
                scheduleResolved = false,
                attendedCount = 0,
                totalScheduled = 0,
                participationPercent = (int?)null,
                summary,
                graphBase64 = graphS0,
            });
        }

        var sprintLengthWeeks = _configuration.GetValue("BusinessLogicConfig:SprintLengthInWeeks", 1);
        var sprintMerge = await _context.ProjectBoardSprintMerges.AsNoTracking()
            .FirstOrDefaultAsync(m => m.ProjectBoardId == boardIdTrim && m.SprintNumber == sprintNumber, cancellationToken);

        DateTime windowStartUtc;
        DateTime windowEndInclusiveUtc;
        var haveWindow =
            SprintPlanDateResolver.TryGetInclusiveUtcRangeFromSprintMerge(
                sprintMerge, sprintNumber, sprintLengthWeeks, out windowStartUtc, out windowEndInclusiveUtc)
            || SprintPlanDateResolver.TryGetSprintInclusiveUtcRange(
                board.SprintPlan, board.StartDate, sprintNumber, out windowStartUtc, out windowEndInclusiveUtc);

        if (!haveWindow)
        {
            const string summary = "Could not resolve this sprint’s date range from the board schedule; attendance vs sprint is unknown.";
            var graphNoWindow = AttendanceChartRenderer.ToBase64Png(AttendanceChartRenderer.RenderPng(0, 0));
            await UpsertCacheMetricsAsync(boardIdTrim, studentId, sprintNumber, metricId, summary, graphNoWindow, cancellationToken);
            return Ok(new
            {
                success = true,
                boardId = boardIdTrim,
                sprintNumber,
                studentId,
                metricId = metricId,
                scheduleResolved = false,
                attendedCount = 0,
                totalScheduled = 0,
                participationPercent = (int?)null,
                summary,
                graphBase64 = graphNoWindow,
            });
        }

        var emailLower = email.ToLowerInvariant();
        var meetings = await _context.BoardMeetings.AsNoTracking()
            .Where(bm =>
                bm.BoardId == boardIdTrim &&
                bm.StudentEmail != null &&
                bm.StudentEmail.ToLower() == emailLower &&
                bm.MeetingTime >= windowStartUtc &&
                bm.MeetingTime <= windowEndInclusiveUtc)
            .ToListAsync(cancellationToken);

        var total = meetings.Count;
        var attended = meetings.Count(m => m.Attended);
        var pct = total > 0 ? (int)Math.Round(100.0 * attended / total) : 0;
        var summaryOk = total == 0
            ? "No team meetings were scheduled for this student in the sprint window."
            : $"The student participated in {attended} out of {total} team meetings set for the sprint.";

        var png = AttendanceChartRenderer.RenderPng(attended, total);
        var graphB64 = AttendanceChartRenderer.ToBase64Png(png);
        await UpsertCacheMetricsAsync(boardIdTrim, studentId, sprintNumber, metricId, summaryOk, graphB64, cancellationToken);

        return Ok(new
        {
            success = true,
            boardId = boardIdTrim,
            sprintNumber,
            studentId,
            metricId = metricId,
            scheduleResolved = true,
            windowStartUtc,
            windowEndUtc = windowEndInclusiveUtc,
            attendedCount = attended,
            totalScheduled = total,
            participationPercent = pct,
            summary = summaryOk,
            graphBase64 = graphB64,
        });
    }
}
