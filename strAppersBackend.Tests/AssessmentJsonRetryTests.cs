using strAppersBackend.Controllers;

namespace strAppersBackend.Tests;

/// <summary>
/// Regression coverage for the 422 seen on Metric 225 (Critical thinking), sprint 1.
///
/// The model returned a complete and correct assessment but omitted the closing brace of the
/// FOURTH category object, so the array closed while that object was still open. Five opening
/// braces, four closing. The whole call was discarded and nothing reached CacheMetrics.
///
/// These tests pin two things:
///   1. That payload must still FAIL to parse. The existing repair layer
///      (RepairUnescapedQuotesInJsonStrings) targets unescaped prose quotes and must not be
///      extended into brace-balancing — guessing where a structural character belongs would
///      cache a plausible but wrong assessment. Failing loudly is the intended behaviour, and
///      the controller now resamples once instead of repairing.
///   2. The same payload with the brace restored must parse, with all four categories and the
///      narrative intact — i.e. the content was never the problem.
/// </summary>
public class AssessmentJsonRetryTests
{
    /// <summary>The real payload, abridged in the rationale text but structurally identical:
    /// the fourth object is never closed before the array closes.</summary>
    private const string MalformedFourthCategory = """
    {"categories":[
      {"name":"Locating, processing, analyzing, and interpreting relevant and reliable information to address complex issues and problems","score":76,"rationale":"Locates and processes relevant information."},
      {"name":"Questioning and evaluating ideas and solutions","score":76,"rationale":"Consistently questions the validity of the information provided."},
      {"name":"Recognizing and Evaluating Multiple Perspectives","score":51,"rationale":"Identifies some alternative perspectives but the depth could be improved."},
      {"name":"Evaluating future consequences of present actions for self and others","score":76,"rationale":"Evaluates the future consequences of design choices."],
      "narrative":"Strong performance in locating and processing relevant information."}
    """;

    /// <summary>Identical, with the single missing brace restored.</summary>
    private const string WellFormed = """
    {"categories":[
      {"name":"Locating, processing, analyzing, and interpreting relevant and reliable information to address complex issues and problems","score":76,"rationale":"Locates and processes relevant information."},
      {"name":"Questioning and evaluating ideas and solutions","score":76,"rationale":"Consistently questions the validity of the information provided."},
      {"name":"Recognizing and Evaluating Multiple Perspectives","score":51,"rationale":"Identifies some alternative perspectives but the depth could be improved."},
      {"name":"Evaluating future consequences of present actions for self and others","score":76,"rationale":"Evaluates the future consequences of design choices."}],
      "narrative":"Strong performance in locating and processing relevant information."}
    """;

    [Fact]
    public void MissingClosingBrace_DoesNotParse_SoTheControllerResamplesRatherThanGuessing()
    {
        var parsed = MetricsController.TryParseGapAnalysisJson(MalformedFourthCategory, out var dto);

        Assert.False(parsed);
        Assert.Null(dto);
    }

    [Fact]
    public void TheQuoteRepairLayerMustNotBeExtendedIntoBraceBalancing()
    {
        // Same assertion from the other direction, stated as intent: the repair helper is for
        // unescaped prose quotes only. If someone teaches it to close unbalanced objects, this
        // test fails and they have to justify caching a guessed structure.
        var repaired = MetricsController.RepairUnescapedQuotesInJsonStrings(MalformedFourthCategory);

        Assert.False(MetricsController.TryParseGapAnalysisJson(repaired, out _));
    }

    [Fact]
    public void RestoringTheBrace_ParsesWithAllFourCategoriesIntact()
    {
        var parsed = MetricsController.TryParseGapAnalysisJson(WellFormed, out var dto);

        Assert.True(parsed);
        Assert.NotNull(dto);
        Assert.Equal(4, dto!.Categories.Count);
        Assert.False(string.IsNullOrWhiteSpace(dto.Narrative));
    }

    [Fact]
    public void RestoringTheBrace_KeepsTheScoresThatMakeTheAssessmentUseful()
    {
        MetricsController.TryParseGapAnalysisJson(WellFormed, out var dto);

        // The low score is the whole signal: three categories at 76, one at 51. If a repair
        // heuristic ever silently reshaped this payload, that contrast is what would be lost.
        Assert.Equal(51, dto!.Categories.Single(c => c.Name.StartsWith("Recognizing")).Score);
        Assert.Equal(3, dto.Categories.Count(c => c.Score == 76));
    }
}
