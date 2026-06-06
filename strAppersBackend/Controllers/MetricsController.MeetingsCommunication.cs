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

        // Get this student's meetings in the sprint window
        var emailLower = studentEmail.ToLowerInvariant();
        _logger.LogInformation(
            "MeetingsCommunication window: studentId={StudentId} boardId={BoardId} sprint={Sprint} email={Email} windowStart={Start:O} windowEnd={End:O}",
            request.StudentId, boardId, request.SprintNumber, studentEmail, windowStartUtc, windowEndUtc);

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

        _logger.LogInformation(
            "MeetingsCommunication meetings found: studentId={StudentId} count={Count} urls={Urls}",
            request.StudentId, meetings.Count,
            string.Join(", ", meetings.Select(m => $"{m.MeetingTime:yyyy-MM-dd HH:mm}")));
        if (DebugAiContext)
        {
            var dbg = $"StudentId={request.StudentId} BoardId={boardId} Sprint={request.SprintNumber} Email={studentEmail}\n" +
                      $"WindowStart={windowStartUtc:O} WindowEnd={windowEndUtc:O}\n" +
                      $"MeetingsFound={meetings.Count}\n" +
                      string.Join("\n", meetings.Select(m => $"  Id={m.Id} MeetingTime={m.MeetingTime:O} Attended={m.Attended} HasVtt={!string.IsNullOrEmpty(m.TranscriptVtt)} Url={m.ActualMeetingUrl}"));
            try { await _smtpEmailService.SendPlainEmailAsync("ofer@skill-in.com", $"[Metrics Debug] MeetingsCommunication student={request.StudentId} sprint={request.SprintNumber}", dbg); } catch { /* ignore */ }
        }

        if (meetings.Count == 0)
        {
            await UpsertCacheMetricsAsync(boardId, request.StudentId, request.SprintNumber,
                MeetingsCommunicationMetricId, NoMeetingTranscriptReviewMessage, null, cancellationToken);
            return Ok(new { success = true, metricId = MeetingsCommunicationMetricId, skippedLlm = true, reviewContent = NoMeetingTranscriptReviewMessage });
        }

        var latestMeeting = meetings.First();

        // ── Step 1: Get VTT (from this student's cached row, any sibling's cached row, or Graph API) ──
        string? vttContent = null;
        string? vttTranscriptId = null;
        string? transcriptSource = null;

        // Check this student's own rows first
        foreach (var m in meetings)
        {
            if (!string.IsNullOrEmpty(m.TranscriptVtt))
            {
                vttContent = m.TranscriptVtt;
                vttTranscriptId = m.TranscriptId;
                transcriptSource = $"cached (BoardMeeting #{m.Id}, {m.MeetingTime:yyyy-MM-dd})";
                break;
            }
        }

        // Then check any other student's row for the same meeting URL
        if (vttContent == null)
        {
            var siblingsWithVtt = await _context.BoardMeetings
                .AsNoTracking()
                .Where(bm => bm.ActualMeetingUrl == latestMeeting.ActualMeetingUrl &&
                             bm.TranscriptVtt != null)
                .FirstOrDefaultAsync(cancellationToken);
            if (siblingsWithVtt != null)
            {
                vttContent = siblingsWithVtt.TranscriptVtt;
                vttTranscriptId = siblingsWithVtt.TranscriptId;
                transcriptSource = $"cached from sibling (BoardMeeting #{siblingsWithVtt.Id})";
            }
        }

        // If still nothing, fetch from Graph API
        if (vttContent == null)
        {
            _logger.LogInformation("MeetingsCommunication: fetching transcript for meeting {Id} ({Url})",
                latestMeeting.Id, latestMeeting.ActualMeetingUrl);
            try
            {
                var (transcriptId, fetchedVtt, fetchError) =
                    await _graphService.FetchLatestTranscriptVttAsync(latestMeeting.ActualMeetingUrl!);

                if (!string.IsNullOrEmpty(fetchedVtt) && !string.IsNullOrEmpty(transcriptId))
                {
                    vttContent = fetchedVtt;
                    vttTranscriptId = transcriptId;
                    transcriptSource = $"fetched from Graph API (meeting {latestMeeting.MeetingTime:yyyy-MM-dd})";
                    // VTT will be stored below only on confirmed-attendee rows
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MeetingsCommunication: Graph API fetch threw for meeting {Id}", latestMeeting.Id);
                var msg = $"Meeting transcript could not be retrieved for this sprint (Graph API error: {ex.Message}). Try again later.";
                await UpsertCacheMetricsAsync(boardId, request.StudentId, request.SprintNumber,
                    MeetingsCommunicationMetricId, msg, null, cancellationToken);
                return Ok(new { success = true, metricId = MeetingsCommunicationMetricId, skippedLlm = true, reviewContent = msg });
            }
        }

        // ── Step 2: Resolve SpeakerName for all siblings via attendance report + name matching ──
        // Load all sibling rows (all students scheduled for this meeting URL)
        var allSiblings = await _context.BoardMeetings
            .Where(bm => bm.ActualMeetingUrl == latestMeeting.ActualMeetingUrl)
            .ToListAsync(cancellationToken);

        // Load student records for all siblings (name + cached TeamsDisplayName + id)
        var siblingEmailKeys = allSiblings
            .Where(s => !string.IsNullOrEmpty(s.StudentEmail))
            .Select(s => s.StudentEmail!.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
        var siblingStudents = await _context.Students
            .Where(s => s.Email != null && siblingEmailKeys.Contains(s.Email.Trim().ToLower()))
            .ToDictionaryAsync(
                s => s.Email!.Trim().ToLowerInvariant(),
                s => new { s.Id, FullName = $"{s.FirstName} {s.LastName}".Trim(), s.TeamsDisplayName },
                cancellationToken);

        // Pass 1: apply already-known TeamsDisplayName (no API call needed)
        var now1 = DateTime.UtcNow;
        bool savedPass1 = false;
        foreach (var sibling in allSiblings.Where(s => string.IsNullOrEmpty(s.SpeakerName)))
        {
            if (string.IsNullOrEmpty(sibling.StudentEmail)) continue;
            var key = sibling.StudentEmail.Trim().ToLowerInvariant();
            if (!siblingStudents.TryGetValue(key, out var ss)) continue;
            if (string.IsNullOrEmpty(ss.TeamsDisplayName)) continue;

            sibling.SpeakerName = ss.TeamsDisplayName;
            if (string.IsNullOrEmpty(sibling.TranscriptVtt))
            {
                sibling.TranscriptVtt = vttContent;
                sibling.TranscriptId = vttTranscriptId;
                sibling.TranscriptFetchedAt = now1;
            }
            savedPass1 = true;
        }
        if (savedPass1) await _context.SaveChangesAsync(cancellationToken);

        // Pass 2: fetch attendance report for siblings still missing SpeakerName
        var needsSpeakerNames = allSiblings.Any(s => string.IsNullOrEmpty(s.SpeakerName));
        Dictionary<string, string> attendeeNamesForLog = new(StringComparer.OrdinalIgnoreCase);

        if (needsSpeakerNames)
        {
            attendeeNamesForLog = await _graphService.GetMeetingAttendeeDisplayNamesAsync(latestMeeting.ActualMeetingUrl!);
            _logger.LogInformation("MeetingsCommunication: attendance report returned {Count} display names: {Names}",
                attendeeNamesForLog.Count,
                string.Join(", ", attendeeNamesForLog.Values));

            var attendeeDisplayNames = attendeeNamesForLog.Values.ToList();
            var now2 = DateTime.UtcNow;
            bool anyUpdated = false;

            foreach (var sibling in allSiblings.Where(s => string.IsNullOrEmpty(s.SpeakerName)))
            {
                if (string.IsNullOrEmpty(sibling.StudentEmail)) continue;
                var key = sibling.StudentEmail.Trim().ToLowerInvariant();
                if (!siblingStudents.TryGetValue(key, out var ss)) continue;

                // Exact name match first
                var resolvedName = attendeeDisplayNames.FirstOrDefault(dn =>
                    string.Equals(dn.Trim(), ss.FullName, StringComparison.OrdinalIgnoreCase));

                // AI fuzzy fallback
                if (resolvedName == null && attendeeDisplayNames.Count > 0)
                {
                    resolvedName = await ResolveDisplayNameWithAiAsync(ss.FullName, attendeeDisplayNames, cancellationToken);
                    if (resolvedName != null)
                        _logger.LogInformation("MeetingsCommunication: AI matched '{StudentName}' → '{DisplayName}'",
                            ss.FullName, resolvedName);
                }

                if (resolvedName != null)
                {
                    sibling.SpeakerName = resolvedName;
                    if (string.IsNullOrEmpty(sibling.TranscriptVtt))
                    {
                        sibling.TranscriptVtt = vttContent;
                        sibling.TranscriptId = vttTranscriptId;
                        sibling.TranscriptFetchedAt = now2;
                    }
                    anyUpdated = true;
                    _logger.LogInformation("MeetingsCommunication: resolved SpeakerName='{Name}' for {Email}",
                        resolvedName, sibling.StudentEmail);

                    // Persist to Students.TeamsDisplayName so future calls skip the API entirely
                    var studentEntity = await _context.Students.FindAsync(new object[] { ss.Id }, cancellationToken);
                    if (studentEntity != null && string.IsNullOrEmpty(studentEntity.TeamsDisplayName))
                        studentEntity.TeamsDisplayName = resolvedName;
                }
            }

            if (anyUpdated)
                await _context.SaveChangesAsync(cancellationToken);
        }

        // ── Step 3: Determine this student's display name ──
        var thisStudentSibling = allSiblings
            .FirstOrDefault(m => m.StudentEmail?.Trim().ToLowerInvariant() == emailLower &&
                                 !string.IsNullOrEmpty(m.SpeakerName));

        var studentDisplayName = thisStudentSibling?.SpeakerName?.Trim();

        if (string.IsNullOrEmpty(studentDisplayName))
        {
            if (attendeeNamesForLog.Count > 0)
            {
                // Report had data but this student wasn't in it → they were absent
                _logger.LogInformation(
                    "MeetingsCommunication: {Email} not in attendance report ({Count} attendees) — absent",
                    studentEmail, attendeeNamesForLog.Count);
                var absentMsg = "The student did not attend the meeting for this sprint, so a meeting communication review was not produced.";
                if (!request.Test)
                    await UpsertCacheMetricsAsync(boardId, request.StudentId, request.SprintNumber,
                        MeetingsCommunicationMetricId, absentMsg, null, cancellationToken);
                return Ok(new
                {
                    success = true,
                    metricId = MeetingsCommunicationMetricId,
                    skippedLlm = true,
                    reviewContent = absentMsg,
                    studentEmailChecked = studentEmail,
                    attendeeNamesFromReport = attendeeNamesForLog,
                });
            }

            // Attendance report was empty (API failure) — fall back to DB name
            studentDisplayName = $"{student.FirstName} {student.LastName}".Trim();
            _logger.LogWarning(
                "MeetingsCommunication: attendance report empty for {Email} — falling back to '{Name}' (possible permissions issue)",
                studentEmail, studentDisplayName);
        }

        // ── Step 4: Build prompts ──
        var transcriptMd = BuildTranscriptMarkdown(vttContent!, studentDisplayName);

        var activeRole = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive);
        var roleName2 = activeRole?.Role?.Name?.Trim() ?? string.Empty;
        IReadOnlyList<string> resolvedLabels;
        if (board.IsSingleRole && student.RoleIndex > 0)
            resolvedLabels = new[] { $"{roleName2} {student.RoleIndex}" };
        else
            resolvedLabels = IsFullStackRole(roleName2)
                ? await _trelloService.ResolveSprintLabelsAsync(boardId, request.SprintNumber, roleName2)
                : new[] { ResolveTrelloSprintCardLabel(activeRole?.Role, fullStackTrackLabel: null) };
        var trelloLabelUsed = string.Join(" + ", resolvedLabels);
        var sprintContextMd = await BuildMeetingsCommunicationContextAsync(
            boardId, board, request.SprintNumber, activeRole?.Role, student.RoleIndex, cancellationToken);

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
                speakerNameResolved = !string.IsNullOrEmpty(thisStudentSibling?.SpeakerName),
                trelloLabelUsed,
                sprintContextAvailable = sprintContextMd != "(No sprint context available.)",
                attendeeNamesFromReport = attendeeNamesForLog,
            });
        }

        // ── Step 5: LLM evaluation ──
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
    /// Uses GPT-4o-mini to fuzzy-match a student's full name against a list of Teams display names.
    /// Returns the matched display name exactly as it appears in the list, or null if no match.
    /// </summary>
    private async Task<string?> ResolveDisplayNameWithAiAsync(
        string studentName,
        List<string> attendeeDisplayNames,
        CancellationToken cancellationToken)
    {
        if (attendeeDisplayNames.Count == 0) return null;
        try
        {
            var nameList = string.Join("\n", attendeeDisplayNames.Select(n => $"- {n}"));
            var userPrompt =
                $"Student name: \"{studentName}\"\n\nTeams meeting display names:\n{nameList}\n\n" +
                "Which display name belongs to this student? Consider name variations, partial names, and Hebrew/English equivalents. " +
                "Reply with ONLY the exact display name from the list above, or NONE if no reasonable match exists.";

            var model = new AIModel
            {
                Name = _configuration["OpenAI:CheapModel"] ?? "gpt-4o-mini",
                Provider = "OpenAI",
                BaseUrl = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1",
                MaxTokens = 50,
                DefaultTemperature = 0
            };

            var (result, _, _) = await _chatCompletionService.GetChatCompletionAsync(
                model,
                "You match person names to their Teams meeting display names. Reply with ONLY the exact display name from the list, or NONE.",
                userPrompt,
                null);

            var trimmed = result.Trim().Trim('"').Trim();
            if (string.IsNullOrEmpty(trimmed) ||
                string.Equals(trimmed, "NONE", StringComparison.OrdinalIgnoreCase))
                return null;

            // Verify the returned value is actually in the list (guard against hallucination)
            return attendeeDisplayNames.FirstOrDefault(n =>
                string.Equals(n.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MeetingsCommunication: AI display name resolution failed for '{StudentName}'", studentName);
            return null;
        }
    }

    /// <summary>
    /// Parses VTT content into per-speaker segment markdown.
    /// Highlights the target student's lines; summarises all others.
    /// </summary>
    private static string BuildTranscriptMarkdown(string vttContent, string studentDisplayName)
    {
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
        int roleIndex,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var roleName = role?.Name?.Trim() ?? string.Empty;

        IReadOnlyList<string> trelloLabels;
        if (board.IsSingleRole && roleIndex > 0)
            trelloLabels = new[] { $"{roleName} {roleIndex}" };
        else
            trelloLabels = IsFullStackRole(roleName)
                ? await _trelloService.ResolveSprintLabelsAsync(boardId, sprintNumber, roleName)
                : new[] { ResolveTrelloSprintCardLabel(role, fullStackTrackLabel: null) };

        foreach (var trelloLabel in trelloLabels)
        {
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

            // Only append module once (both tracks share the same module)
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
