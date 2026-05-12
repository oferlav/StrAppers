using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;
using strAppersBackend.Utilities;

namespace strAppersBackend.Controllers;

public partial class MetricsController
{
    private const int MeetingsCommunicationMetricId = 8;

    private const string NoMeetingTranscriptReviewMessage =
        "No meeting transcript was available for this sprint, so a meeting communication review was not produced.";

    public class MeetingsCommunicationRequest
    {
        public string BoardId { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public int SprintNumber { get; set; }
        /// <summary>When true, returns generated prompts only (no LLM, no CacheMetrics write).</summary>
        public bool Test { get; set; }
    }

    /// <summary>
    /// Evaluates student communication quality in team meetings based on Teams transcript (VTT).
    /// Fetches/caches transcript per meeting URL, parses speaker segments, and calls LLM for per-dimension scores.
    /// Persists to <see cref="CacheMetrics"/> (MetricId 8).
    /// </summary>
    [HttpPost("use/MeetingsCommunication")]
    public async Task<ActionResult<object>> MeetingsCommunication(
        [FromBody] MeetingsCommunicationRequest? request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { success = false, message = "Request body is required." });
        if (string.IsNullOrWhiteSpace(request.BoardId))
            return BadRequest(new { success = false, message = "BoardId is required." });
        if (request.StudentId <= 0)
            return BadRequest(new { success = false, message = "StudentId is required." });
        if (request.SprintNumber <= 0)
            return BadRequest(new { success = false, message = "SprintNumber must be >= 1 (Sprint 0 has no meetings)." });

        var boardId = request.BoardId.Trim();

        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentRoles)
            .ThenInclude(sr => sr.Role)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);
        if (student == null)
            return NotFound(new { success = false, message = $"Student {request.StudentId} not found." });
        if (!string.Equals(student.BoardId?.Trim(), boardId, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, message = "Student is not assigned to this BoardId." });

        var board = await _context.ProjectBoards.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == boardId, cancellationToken);
        if (board == null)
            return NotFound(new { success = false, message = $"Board {boardId} not found." });

        var studentEmail = student.Email?.Trim();
        if (string.IsNullOrEmpty(studentEmail))
        {
            await UpsertCacheMetricsAsync(boardId, request.StudentId, request.SprintNumber,
                MeetingsCommunicationMetricId, NoMeetingTranscriptReviewMessage, null, cancellationToken);
            return Ok(new { success = true, metricId = MeetingsCommunicationMetricId, skippedLlm = true, reviewContent = NoMeetingTranscriptReviewMessage });
        }

        // Resolve sprint window
        var sprintLengthWeeks = _configuration.GetValue("BusinessLogicConfig:SprintLengthInWeeks", 1);
        var sprintMerge = await _context.ProjectBoardSprintMerges.AsNoTracking()
            .FirstOrDefaultAsync(m => m.ProjectBoardId == boardId && m.SprintNumber == request.SprintNumber, cancellationToken);

        var haveWindow =
            SprintPlanDateResolver.TryGetInclusiveUtcRangeFromSprintMerge(
                sprintMerge, request.SprintNumber, sprintLengthWeeks, out var windowStartUtc, out var windowEndUtc)
            || SprintPlanDateResolver.TryGetSprintInclusiveUtcRange(
                board.SprintPlan, board.StartDate, request.SprintNumber, out windowStartUtc, out windowEndUtc);

        if (!haveWindow)
        {
            const string msg = "Could not resolve this sprint's date range; meeting communication review skipped.";
            await UpsertCacheMetricsAsync(boardId, request.StudentId, request.SprintNumber,
                MeetingsCommunicationMetricId, msg, null, cancellationToken);
            return Ok(new { success = true, metricId = MeetingsCommunicationMetricId, skippedLlm = true, reviewContent = msg });
        }

        // Get meetings for this student in sprint window
        var emailLower = studentEmail.ToLowerInvariant();
        var meetings = await _context.BoardMeetings
            .Where(bm =>
                bm.BoardId == boardId &&
                bm.StudentEmail != null &&
                bm.StudentEmail.ToLower() == emailLower &&
                bm.MeetingTime >= windowStartUtc &&
                bm.MeetingTime <= windowEndUtc &&
                bm.ActualMeetingUrl != null)
            .OrderByDescending(bm => bm.MeetingTime)
            .ToListAsync(cancellationToken);

        if (meetings.Count == 0)
        {
            await UpsertCacheMetricsAsync(boardId, request.StudentId, request.SprintNumber,
                MeetingsCommunicationMetricId, NoMeetingTranscriptReviewMessage, null, cancellationToken);
            return Ok(new { success = true, metricId = MeetingsCommunicationMetricId, skippedLlm = true, reviewContent = NoMeetingTranscriptReviewMessage });
        }

        // Try to get a transcript — use cached VTT if available, otherwise fetch from Graph API
        string? vttContent = null;
        string? transcriptSource = null;
        var latestMeeting = meetings.First();

        foreach (var meeting in meetings)
        {
            if (!string.IsNullOrEmpty(meeting.TranscriptVtt))
            {
                vttContent = meeting.TranscriptVtt;
                transcriptSource = $"cached (BoardMeeting #{meeting.Id}, {meeting.MeetingTime:yyyy-MM-dd})";
                break;
            }
        }

        if (vttContent == null)
        {
            _logger.LogInformation("MeetingsCommunication: fetching transcript for meeting {Id} ({Url})",
                latestMeeting.Id, latestMeeting.ActualMeetingUrl);

            var (transcriptId, fetchedVtt, fetchError) =
                await _graphService.FetchLatestTranscriptVttAsync(latestMeeting.ActualMeetingUrl!);

            if (!string.IsNullOrEmpty(fetchedVtt) && !string.IsNullOrEmpty(transcriptId))
            {
                vttContent = fetchedVtt;
                transcriptSource = $"fetched from Graph API (meeting {latestMeeting.MeetingTime:yyyy-MM-dd})";

                // Fetch attendance report to resolve email → displayName for all attendees
                var attendeeNames = await _graphService.GetMeetingAttendeeDisplayNamesAsync(latestMeeting.ActualMeetingUrl!);

                // Cache VTT + SpeakerName on all BoardMeeting rows sharing the same ActualMeetingUrl
                var now = DateTime.UtcNow;
                var siblingMeetings = await _context.BoardMeetings
                    .Where(bm => bm.ActualMeetingUrl == latestMeeting.ActualMeetingUrl)
                    .ToListAsync(cancellationToken);

                foreach (var sibling in siblingMeetings)
                {
                    sibling.TranscriptId = transcriptId;
                    sibling.TranscriptVtt = fetchedVtt;
                    sibling.TranscriptFetchedAt = now;
                    // Resolve this student's exact VTT speaker name from the attendance report
                    if (!string.IsNullOrEmpty(sibling.StudentEmail) &&
                        attendeeNames.TryGetValue(sibling.StudentEmail, out var speakerName))
                        sibling.SpeakerName = speakerName;
                }
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                _logger.LogWarning("MeetingsCommunication: transcript not available — {Error}", fetchError);
                var msg = $"Meeting transcript is not yet available for this sprint ({fetchError ?? "no transcripts found"}). Try again after the meeting ends and transcription has processed.";
                await UpsertCacheMetricsAsync(boardId, request.StudentId, request.SprintNumber,
                    MeetingsCommunicationMetricId, msg, null, cancellationToken);
                return Ok(new { success = true, metricId = MeetingsCommunicationMetricId, skippedLlm = true, reviewContent = msg });
            }
        }

        // Use stored SpeakerName (resolved from attendance report) — no fuzzy matching needed
        // Re-query to get the updated SpeakerName after the save above
        var thisStudentMeeting = meetings.FirstOrDefault(m => !string.IsNullOrEmpty(m.SpeakerName))
            ?? await _context.BoardMeetings.AsNoTracking()
                .Where(bm => bm.ActualMeetingUrl == latestMeeting.ActualMeetingUrl &&
                             bm.StudentEmail != null &&
                             bm.StudentEmail.ToLower() == emailLower)
                .OrderByDescending(bm => bm.MeetingTime)
                .FirstOrDefaultAsync(cancellationToken);

        var studentDisplayName = thisStudentMeeting?.SpeakerName?.Trim();
        if (string.IsNullOrEmpty(studentDisplayName))
        {
            // Fallback: FirstName + LastName if attendance report had no match
            studentDisplayName = $"{student.FirstName} {student.LastName}".Trim();
            _logger.LogWarning("MeetingsCommunication: no SpeakerName resolved for {Email}, falling back to '{Name}'",
                studentEmail, studentDisplayName);
        }

        var transcriptMd = BuildTranscriptMarkdown(vttContent, studentDisplayName);

        // Build sprint context (Trello card + module + customer narrative)
        var activeRole = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive);
        var sprintContextMd = await BuildMeetingsCommunicationContextAsync(
            boardId, board, request.SprintNumber, activeRole?.Role, cancellationToken);

        var systemPrompt = LoadMeetingsCommunicationSystemPrompt();
        var userPrompt = new StringBuilder()
            .AppendLine("## MEETING TRANSCRIPT")
            .AppendLine($"Student being evaluated: **{studentDisplayName}**")
            .AppendLine($"Transcript source: {transcriptSource}")
            .AppendLine()
            .AppendLine(transcriptMd)
            .AppendLine()
            .AppendLine("## SPRINT CONTEXT")
            .AppendLine(sprintContextMd)
            .AppendLine()
            .AppendLine("Respond with JSON only as specified in your instructions.")
            .ToString();

        if (request.Test)
        {
            return Ok(new
            {
                success = true,
                test = true,
                message = "Test mode: LLM not called; CacheMetrics not updated.",
                systemPrompt,
                userPrompt,
                transcriptSource,
                meetingCount = meetings.Count,
                studentDisplayName,
            });
        }

        try
        {
            var cheapName = _configuration["OpenAI:CheapModel"] ?? "gpt-4o-mini";
            var aiModel = new AIModel
            {
                Name = cheapName,
                Provider = "OpenAI",
                BaseUrl = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1",
                MaxTokens = 16384,
                DefaultTemperature = 0.2
            };

            var (llmText, _, _) = await _chatCompletionService.GetChatCompletionAsync(aiModel, systemPrompt, userPrompt, null);
            var parsed = TryParseGapAnalysisJson(llmText, out var dto);
            if (!parsed || dto == null)
            {
                return UnprocessableEntity(new
                {
                    success = false,
                    message = "MeetingsCommunication LLM did not return valid JSON. Nothing saved to CacheMetrics.",
                    preview = Truncate(llmText.Trim(), 4000),
                });
            }

            var rows = dto.Categories
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => (c.Name.Trim(), Math.Clamp(c.Score, 0, 100)))
                .ToList();
            if (rows.Count == 0)
                rows.Add(("Overall communication", 0));

            var graphB64 = GapAnalysisBarChartRenderer.ToBase64Png(
                GapAnalysisBarChartRenderer.RenderSingleChart(rows, "Meeting communication"));

            var reviewContent = FormatMeetingsCommunicationReviewContent(dto);
            await UpsertCacheMetricsAsync(boardId, request.StudentId, request.SprintNumber,
                MeetingsCommunicationMetricId, reviewContent, graphB64, cancellationToken);

            return Ok(new
            {
                success = true,
                metricId = MeetingsCommunicationMetricId,
                skippedLlm = false,
                reviewContent,
                graphBase64 = graphB64,
                model = dto,
                transcriptSource,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MeetingsCommunication failed for board {BoardId}", boardId);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Parses VTT content into per-speaker segment markdown.
    /// Highlights the target student's lines; summarises all others.
    /// </summary>
    private static string BuildTranscriptMarkdown(string vttContent, string studentDisplayName)
    {
        // Parse cues: "<v Speaker Name>line text"
        var speakerLines = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var cueTextPattern = new Regex(@"<v\s+([^>]+)>(.*?)(?:</v>)?$", RegexOptions.Compiled);

        foreach (var rawLine in vttContent.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;
            var match = cueTextPattern.Match(line);
            if (!match.Success) continue;

            var speaker = match.Groups[1].Value.Trim();
            var text = match.Groups[2].Value.Trim();
            if (string.IsNullOrEmpty(text)) continue;

            if (!speakerLines.TryGetValue(speaker, out var list))
            {
                list = new List<string>();
                speakerLines[speaker] = list;
            }
            list.Add(text);
        }

        if (speakerLines.Count == 0)
            return "(Transcript content could not be parsed — no speaker cues found.)";

        var sb = new StringBuilder();
        var totalWords = speakerLines.Values.SelectMany(l => l).Sum(t => t.Split(' ').Length);

        foreach (var kvp in speakerLines.OrderByDescending(k => k.Value.Count))
        {
            var speaker = kvp.Key;
            var lines = kvp.Value;
            var wordCount = lines.Sum(t => t.Split(' ').Length);
            var pct = totalWords > 0 ? (int)Math.Round(100.0 * wordCount / totalWords) : 0;
            var isStudent = string.Equals(speaker, studentDisplayName, StringComparison.OrdinalIgnoreCase);

            sb.AppendLine(isStudent
                ? $"### ★ {speaker} [STUDENT BEING EVALUATED] ({lines.Count} turns, ~{wordCount} words, {pct}% of meeting)"
                : $"### {speaker} ({lines.Count} turns, ~{wordCount} words, {pct}% of meeting)");

            // Include all lines for the target student; cap others at 5 for context
            var toShow = isStudent ? lines : lines.Take(5).ToList();
            foreach (var l in toShow)
                sb.AppendLine($"- \"{l}\"");
            if (!isStudent && lines.Count > 5)
                sb.AppendLine($"- *(+{lines.Count - 5} more lines)*");
            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    private async Task<string> BuildMeetingsCommunicationContextAsync(
        string boardId,
        ProjectBoard board,
        int sprintNumber,
        Role? role,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        // Sprint role card from Trello
        var trelloLabel = ResolveTrelloSprintCardLabel(role, fullStackTrackLabel: null);
        var snapshot = await _trelloService.GetSprintRoleCardSnapshotAsync(boardId, sprintNumber, trelloLabel);
        if (snapshot != null)
        {
            sb.AppendLine($"### Sprint {sprintNumber} role card ({trelloLabel})");
            if (!string.IsNullOrWhiteSpace(snapshot.CardName))
                sb.AppendLine($"**{snapshot.CardName.Trim()}**");
            if (!string.IsNullOrWhiteSpace(snapshot.Description))
                sb.AppendLine(snapshot.Description.Trim());
            sb.AppendLine();
        }

        // Project module
        var moduleIdStr = await _trelloService.GetModuleIdFromSprintCardAsync(boardId, sprintNumber, trelloLabel);
        if (!string.IsNullOrWhiteSpace(moduleIdStr) && int.TryParse(moduleIdStr.Trim(), out var moduleId))
        {
            await AppendGapAnalysisProjectModuleSectionFromModuleIdAsync(
                sb, board, moduleId,
                "### Project module (sprint scope — context for communication relevance)",
                cancellationToken);
        }

        return sb.Length == 0 ? "(No sprint context available.)" : sb.ToString().Trim();
    }

    private static string FormatMeetingsCommunicationReviewContent(GapAnalysisLlmResult dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine(dto.Narrative.Trim());
        if (dto.Categories.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Categories");
            foreach (var c in dto.Categories)
            {
                if (string.IsNullOrWhiteSpace(c.Name)) continue;
                sb.Append("- **").Append(c.Name.Trim()).Append("** (").Append(Math.Clamp(c.Score, 0, 100)).Append("): ");
                sb.AppendLine(string.IsNullOrWhiteSpace(c.Rationale) ? "(no rationale)" : c.Rationale.Trim());
            }
        }
        return sb.ToString().Trim();
    }

    private static string LoadMeetingsCommunicationSystemPrompt()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", "Metrics", "MeetingsCommunicationSystem.txt");
        if (System.IO.File.Exists(path))
        {
            var t = System.IO.File.ReadAllText(path).Trim();
            if (!string.IsNullOrEmpty(t)) return t;
        }

        return "You evaluate a student's communication quality in a team meeting based on their transcript. " +
               "Output JSON with: categories (array of {name, score 0-100, rationale}) and narrative (markdown summary). " +
               "Categories: Participation, Communication Clarity, Technical Depth, Engagement & Responsiveness, Blockers Communicated. " +
               "Focus on the student marked [STUDENT BEING EVALUATED].";
    }
}
