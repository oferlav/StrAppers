using strAppersBackend.Controllers;

namespace strAppersBackend.Tests;

/// <summary>
/// Regression tests for the LLM JSON repair pass in TryParseGapAnalysisJson: models occasionally
/// emit unescaped double quotes inside string values (prose like: acknowledgment (e.g., "Got it")),
/// which invalidated the whole payload and failed the metric with 422 ("did not return valid JSON").
/// Real-world case: Collaboration Responsiveness (metric 114) during a "collect all metrics first"
/// Sprint Summary run. RepairUnescapedQuotesInJsonStrings escapes prose quotes so the payload parses.
/// </summary>
public class LlmJsonRepairTests
{
    // ── End-to-end: TryParseGapAnalysisJson with the repair fallback ────────────

    [Fact]
    public void Parses_TheRealWorldPayload_WithUnescapedProseQuotes()
    {
        // Condensed from the actual failing response: unescaped "Got it, I'll look into this" inside
        // the narrative string.
        const string llmText =
            "{\"categories\":[{\"name\":\"Response Timeliness\",\"score\":20,\"rationale\":\"No recorded response.\"}]," +
            "\"narrative\":\"## Summary\\n1. **Acknowledge messages promptly** — even a brief acknowledgment (e.g., \"Got it, I'll look into this\") prevents teammates from being blocked.\"}";

        var ok = MetricsController.TryParseGapAnalysisJson(llmText, out var dto);

        Assert.True(ok);
        Assert.NotNull(dto);
        Assert.Single(dto!.Categories);
        Assert.Equal("Response Timeliness", dto.Categories[0].Name);
        Assert.Contains("Got it, I'll look into this", dto.Narrative);
    }

    [Fact]
    public void Parses_ValidJson_WithoutNeedingRepair()
    {
        const string llmText = "{\"categories\":[{\"name\":\"A\",\"score\":50,\"rationale\":\"fine\"}],\"narrative\":\"ok\"}";

        var ok = MetricsController.TryParseGapAnalysisJson(llmText, out var dto);

        Assert.True(ok);
        Assert.Equal("ok", dto!.Narrative);
    }

    [Fact]
    public void Parses_ValidJson_WithProperlyEscapedQuotes()
    {
        const string llmText = "{\"categories\":[],\"narrative\":\"said \\\"stop\\\" twice\"}";

        var ok = MetricsController.TryParseGapAnalysisJson(llmText, out var dto);

        Assert.True(ok);
        Assert.Equal("said \"stop\" twice", dto!.Narrative);
    }

    [Fact]
    public void StillFails_ForTrulyBrokenPayloads()
    {
        var ok = MetricsController.TryParseGapAnalysisJson("not json at all", out var dto);

        Assert.False(ok);
        Assert.Null(dto);
    }

    // ── RepairUnescapedQuotesInJsonStrings ──────────────────────────────────────

    [Fact]
    public void Repair_EscapesProseQuotes_InsideStringValues()
    {
        const string broken = "{\"a\":\"he said \"hello\" loudly\"}";

        var repaired = MetricsController.RepairUnescapedQuotesInJsonStrings(broken);

        Assert.Equal("{\"a\":\"he said \\\"hello\\\" loudly\"}", repaired);
    }

    [Fact]
    public void Repair_LeavesValidJson_Unchanged()
    {
        const string valid = "{\"a\":\"plain\",\"b\":[{\"c\":1}],\"d\":\"already \\\"escaped\\\" quotes\"}";

        Assert.Equal(valid, MetricsController.RepairUnescapedQuotesInJsonStrings(valid));
    }

    [Fact]
    public void Repair_KeepsStructuralQuotes_BeforeCommaBraceBracketColon()
    {
        const string valid = "{\"key\":\"value\",\"arr\":[\"x\",\"y\"],\"n\":2}";

        Assert.Equal(valid, MetricsController.RepairUnescapedQuotesInJsonStrings(valid));
    }

    [Fact]
    public void Repair_HandlesQuoteFollowedByClosingParenthesis()
    {
        // The exact shape from the real payload: quote before ')' is prose, not structural.
        const string broken = "{\"n\":\"(e.g., \"Got it\") prevents\"}";

        var repaired = MetricsController.RepairUnescapedQuotesInJsonStrings(broken);

        Assert.Equal("{\"n\":\"(e.g., \\\"Got it\\\") prevents\"}", repaired);
    }
}
