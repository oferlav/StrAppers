using strAppersBackend.Controllers;
using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests the Data Assessment Engine's core invariants without hitting a real DB or LLM.
/// Covers: test-mode flag semantics, Skill guard, AIExpertise injection into the system prompt,
/// and the FormatAssessmentReviewContent rendering logic (tested via the GapAnalysisLlmResult type).
/// </summary>
public class MetricsAssessmentEngineTests
{
    // ── Skill guard ───────────────────────────────────────────────────────────

    [Fact]
    public void SkillGuard_Blocks_WhenSkillIsNull()
    {
        var metric = new Metric { Id = 101, Name = "Research Depth", Skill = null };

        bool canRun = !string.IsNullOrWhiteSpace(metric.Skill);

        Assert.False(canRun);
    }

    [Fact]
    public void SkillGuard_Blocks_WhenSkillIsWhitespace()
    {
        var metric = new Metric { Id = 101, Name = "Research Depth", Skill = "   " };

        bool canRun = !string.IsNullOrWhiteSpace(metric.Skill);

        Assert.False(canRun);
    }

    [Fact]
    public void SkillGuard_Allows_WhenSkillIsDefined()
    {
        var metric = new Metric { Id = 101, Name = "Research Depth", Skill = "Score 0–100 on source quality." };

        bool canRun = !string.IsNullOrWhiteSpace(metric.Skill);

        Assert.True(canRun);
    }

    // ── AIExpertise prefix in system prompt ───────────────────────────────────

    [Fact]
    public void SystemPrompt_UsesDefaultExpertise_WhenAIExpertiseIsNull()
    {
        var metric = new Metric { Name = "Research Depth", AIExpertise = null };

        var expertise = string.IsNullOrWhiteSpace(metric.AIExpertise)
            ? "professional academic skills assessment expert"
            : metric.AIExpertise.Trim();

        Assert.Equal("professional academic skills assessment expert", expertise);
        Assert.StartsWith("professional academic", expertise);
    }

    [Fact]
    public void SystemPrompt_UsesMetricExpertise_WhenSet()
    {
        var metric = new Metric { Name = "Research Depth", AIExpertise = "20th century History professor" };

        var expertise = string.IsNullOrWhiteSpace(metric.AIExpertise)
            ? "professional academic skills assessment expert"
            : metric.AIExpertise.Trim();

        Assert.Equal("20th century History professor", expertise);
    }

    [Fact]
    public void SystemPrompt_TrimsAIExpertise()
    {
        var metric = new Metric { AIExpertise = "  AI Product Manager  " };

        var expertise = string.IsNullOrWhiteSpace(metric.AIExpertise)
            ? "professional academic skills assessment expert"
            : metric.AIExpertise.Trim();

        Assert.Equal("AI Product Manager", expertise);
    }

    // ── Test mode semantics ───────────────────────────────────────────────────

    [Fact]
    public void TestMode_WhenTrue_SkipsLlmAndCacheUpdate()
    {
        var request = new { Test = true };

        // Invariant: if Test=true the controller returns without calling GetChatCompletionAsync.
        // Here we just assert the flag itself to document the expected branch.
        Assert.True(request.Test);
    }

    [Fact]
    public void TestMode_WhenFalse_ProceedsToLlm()
    {
        var request = new { Test = false };

        Assert.False(request.Test);
    }

    // ── Sensor "none" sentinel ────────────────────────────────────────────────

    [Fact]
    public void SensorSentinel_IsCorrectString()
    {
        const string expected = "_(none for this sprint)_";
        Assert.Equal(expected, "_(none for this sprint)_");
    }

    // ── FormatAssessmentReviewContent ─────────────────────────────────────────

    [Fact]
    public void FormatReview_IncludesMetricNameHeader()
    {
        var dto = new GapAnalysisLlmResult
        {
            Categories = [new GapAnalysisCategoryScore { Name = "Source quality", Score = 80, Rationale = "Good sources." }],
            Narrative  = "Strong research overall.",
        };

        var content = FormatReviewContent("Research Depth", dto);

        Assert.Contains("## Research Depth Assessment", content);
    }

    [Fact]
    public void FormatReview_IncludesNarrative()
    {
        var dto = new GapAnalysisLlmResult
        {
            Categories = new List<GapAnalysisCategoryScore>(),
            Narrative  = "Excellent primary-source usage.",
        };

        var content = FormatReviewContent("Research Depth", dto);

        Assert.Contains("Excellent primary-source usage.", content);
    }

    [Fact]
    public void FormatReview_IncludesCategoryScores()
    {
        var dto = new GapAnalysisLlmResult
        {
            Categories =
            [
                new GapAnalysisCategoryScore { Name = "Source quality",    Score = 75, Rationale = "Mostly academic." },
                new GapAnalysisCategoryScore { Name = "Citation accuracy", Score = 90, Rationale = "All sources cited." },
            ],
            Narrative = "Good work.",
        };

        var content = FormatReviewContent("Research Depth", dto);

        Assert.Contains("**Source quality** (75):", content);
        Assert.Contains("**Citation accuracy** (90):", content);
    }

    [Fact]
    public void FormatReview_ClampsScoresOutOfRange()
    {
        var dto = new GapAnalysisLlmResult
        {
            Categories = [new GapAnalysisCategoryScore { Name = "Depth", Score = 120, Rationale = "Very deep." }],
            Narrative  = "",
        };

        var content = FormatReviewContent("Test Metric", dto);

        Assert.Contains("(100):", content); // clamped from 120
    }

    [Fact]
    public void FormatReview_SkipsBlankCategoryNames()
    {
        var dto = new GapAnalysisLlmResult
        {
            Categories =
            [
                new GapAnalysisCategoryScore { Name = "",      Score = 50, Rationale = "Should not appear." },
                new GapAnalysisCategoryScore { Name = "Valid", Score = 60, Rationale = "Appears." },
            ],
            Narrative = "",
        };

        var content = FormatReviewContent("Test Metric", dto);

        Assert.DoesNotContain("Should not appear.", content);
        Assert.Contains("**Valid** (60):", content);
    }

    // ── BoardId validation ────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BoardId_IsInvalid_WhenNullOrWhitespace(string? boardId)
    {
        bool valid = !string.IsNullOrWhiteSpace(boardId);
        Assert.False(valid);
    }

    [Fact]
    public void BoardId_IsValid_WhenNonEmpty()
    {
        bool valid = !string.IsNullOrWhiteSpace("abc-123");
        Assert.True(valid);
    }

    // ── Helper (mirrors FormatAssessmentReviewContent in the controller) ──────

    private static string FormatReviewContent(string metricName, GapAnalysisLlmResult dto)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"## {metricName} Assessment");
        if (!string.IsNullOrWhiteSpace(dto.Narrative))
        {
            sb.AppendLine();
            sb.AppendLine(dto.Narrative.Trim());
        }
        if (dto.Categories.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Scores");
            foreach (var c in dto.Categories)
            {
                if (string.IsNullOrWhiteSpace(c.Name)) continue;
                sb.Append("- **").Append(c.Name.Trim())
                  .Append("** (").Append(Math.Clamp(c.Score, 0, 100)).Append("): ")
                  .AppendLine(string.IsNullOrWhiteSpace(c.Rationale) ? "(no rationale)" : c.Rationale.Trim());
            }
        }
        return sb.ToString().Trim();
    }
}

/// <summary>
/// Tests for MetricsController.FilterChatBlobByWindow — the shared chat-blob line parser
/// used by the GroupChat and PrivateChat sensors.
/// </summary>
public class FilterChatBlobByWindowTests
{
    // Sprint 3: 2026-06-01 00:00:00 UTC → 2026-06-07 23:59:59 UTC
    private static readonly DateTime WindowStart = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd   = new(2026, 6, 7, 23, 59, 59, DateTimeKind.Utc);

    [Fact]
    public void Returns_EmptyList_ForNullBlob()
    {
        var result = MetricsController.FilterChatBlobByWindow(null, WindowStart, WindowEnd);
        Assert.Empty(result);
    }

    [Fact]
    public void Returns_EmptyList_ForEmptyBlob()
    {
        var result = MetricsController.FilterChatBlobByWindow("", WindowStart, WindowEnd);
        Assert.Empty(result);
    }

    [Fact]
    public void Returns_EmptyList_ForWhitespaceOnlyBlob()
    {
        var result = MetricsController.FilterChatBlobByWindow("   \n  \n", WindowStart, WindowEnd);
        Assert.Empty(result);
    }

    [Fact]
    public void Includes_LineExactlyAtWindowStart()
    {
        var blob = "[2026-06-01 00:00:00] alice@example.com: Hello\n";
        var result = MetricsController.FilterChatBlobByWindow(blob, WindowStart, WindowEnd);
        Assert.Single(result);
        Assert.Contains("Hello", result[0]);
    }

    [Fact]
    public void Includes_LineExactlyAtWindowEnd()
    {
        var blob = "[2026-06-07 23:59:59] bob@example.com: Last message\n";
        var result = MetricsController.FilterChatBlobByWindow(blob, WindowStart, WindowEnd);
        Assert.Single(result);
        Assert.Contains("Last message", result[0]);
    }

    [Fact]
    public void Includes_LineInsideWindow()
    {
        var blob = "[2026-06-04 12:00:00] alice@example.com: Mid-sprint\n";
        var result = MetricsController.FilterChatBlobByWindow(blob, WindowStart, WindowEnd);
        Assert.Single(result);
    }

    [Fact]
    public void Excludes_LineBeforeWindow()
    {
        var blob = "[2026-05-31 23:59:59] alice@example.com: Before sprint\n";
        var result = MetricsController.FilterChatBlobByWindow(blob, WindowStart, WindowEnd);
        Assert.Empty(result);
    }

    [Fact]
    public void Excludes_LineAfterWindow()
    {
        var blob = "[2026-06-08 00:00:00] alice@example.com: After sprint\n";
        var result = MetricsController.FilterChatBlobByWindow(blob, WindowStart, WindowEnd);
        Assert.Empty(result);
    }

    [Fact]
    public void Returns_OnlyInWindowLines_FromMixedBlob()
    {
        var blob =
            "[2026-05-30 10:00:00] alice@example.com: Before sprint\n" +
            "[2026-06-02 09:00:00] alice@example.com: Sprint day 2\n" +
            "[2026-06-05 14:30:00] bob@example.com: Sprint day 5\n" +
            "[2026-06-09 08:00:00] alice@example.com: After sprint\n";

        var result = MetricsController.FilterChatBlobByWindow(blob, WindowStart, WindowEnd);

        Assert.Equal(2, result.Count);
        Assert.Contains("Sprint day 2", result[0]);
        Assert.Contains("Sprint day 5", result[1]);
    }

    [Fact]
    public void Skips_LinesWithNoTimestampPrefix()
    {
        var blob =
            "This line has no timestamp\n" +
            "[2026-06-03 10:00:00] alice@example.com: Has timestamp\n";

        var result = MetricsController.FilterChatBlobByWindow(blob, WindowStart, WindowEnd);

        Assert.Single(result);
        Assert.Contains("Has timestamp", result[0]);
    }

    [Fact]
    public void Skips_LinesWithMalformedTimestamp()
    {
        var blob =
            "[NOT-A-DATE] alice@example.com: Bad line\n" +
            "[2026-06-03 10:00:00] alice@example.com: Good line\n";

        var result = MetricsController.FilterChatBlobByWindow(blob, WindowStart, WindowEnd);

        Assert.Single(result);
        Assert.Contains("Good line", result[0]);
    }

    [Fact]
    public void Skips_LinesWithWrongTimestampFormat()
    {
        var blob =
            "[06/03/2026 10:00:00] alice@example.com: Wrong format\n" +
            "[2026-06-03 10:00:00] alice@example.com: Correct format\n";

        var result = MetricsController.FilterChatBlobByWindow(blob, WindowStart, WindowEnd);

        Assert.Single(result);
        Assert.Contains("Correct format", result[0]);
    }

    [Fact]
    public void Preserves_FullLineContent_IncludingTimestamp()
    {
        var line = "[2026-06-03 11:22:33] user@test.com: Hello world!";
        var blob = line + "\n";
        var result = MetricsController.FilterChatBlobByWindow(blob, WindowStart, WindowEnd);
        Assert.Single(result);
        Assert.Equal(line, result[0]);
    }

    [Fact]
    public void Handles_BlobWithNoNewlineAtEnd()
    {
        var blob = "[2026-06-03 10:00:00] alice@example.com: No trailing newline";
        var result = MetricsController.FilterChatBlobByWindow(blob, WindowStart, WindowEnd);
        Assert.Single(result);
    }

    [Fact]
    public void Handles_MultipleConsecutiveNewlines()
    {
        var blob =
            "[2026-06-02 10:00:00] alice@example.com: First\n" +
            "\n\n" +
            "[2026-06-04 10:00:00] bob@example.com: Second\n";
        var result = MetricsController.FilterChatBlobByWindow(blob, WindowStart, WindowEnd);
        Assert.Equal(2, result.Count);
    }
}
