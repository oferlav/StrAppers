using strAppersBackend.Controllers;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests for the institute Metric.Skill integration in the three legacy hardcoded-prompt metrics
/// (see MetricsController.SkillRubricPrompts.cs for the policy write-up):
/// - CustomerEngagement / MeetingsCommunication use <see cref="MetricsController.ResolveSystemPrompt"/>:
///   OVERRIDE — a non-blank institute Skill rubric fully replaces the hardcoded prompt.
/// - GapAnalysis uses <see cref="MetricsController.AppendInstituteSkillRubric"/>: ADD — a non-blank
///   institute Skill rubric is appended after the hardcoded domain rules, which are always kept intact.
/// </summary>
public class MetricSkillRubricOverrideTests
{
    private const string HardcodedPrompt = "HARDCODED-PROMPT-MARKER: score communication, pick your own category names.";

    // ── OVERRIDE: ResolveSystemPrompt (CustomerEngagement / MeetingsCommunication) ─────────────

    [Fact]
    public void Resolve_UsesHardcodedPrompt_WhenSkillIsNull()
    {
        var result = MetricsController.ResolveSystemPrompt(
            skillRubric: null, hardcodedPrompt: HardcodedPrompt, aiExpertise: null, defaultExpertise: "default expert");

        Assert.Equal(HardcodedPrompt, result);
    }

    [Fact]
    public void Resolve_UsesHardcodedPrompt_WhenSkillIsEmpty()
    {
        var result = MetricsController.ResolveSystemPrompt(
            skillRubric: "", hardcodedPrompt: HardcodedPrompt, aiExpertise: null, defaultExpertise: "default expert");

        Assert.Equal(HardcodedPrompt, result);
    }

    [Fact]
    public void Resolve_UsesHardcodedPrompt_WhenSkillIsWhitespaceOnly()
    {
        var result = MetricsController.ResolveSystemPrompt(
            skillRubric: "   \n  ", hardcodedPrompt: HardcodedPrompt, aiExpertise: null, defaultExpertise: "default expert");

        Assert.Equal(HardcodedPrompt, result);
    }

    [Fact]
    public void Resolve_BypassesHardcodedPrompt_WhenSkillIsDefined()
    {
        const string rubric = "- **Communication Clarity (0-100)**\n  - 0-25: unclear.";

        var result = MetricsController.ResolveSystemPrompt(
            skillRubric: rubric, hardcodedPrompt: HardcodedPrompt, aiExpertise: null, defaultExpertise: "default expert");

        Assert.DoesNotContain("HARDCODED-PROMPT-MARKER", result);
        Assert.Contains(rubric, result);
    }

    [Fact]
    public void Resolve_UsesDefaultExpertise_WhenAIExpertiseIsNull()
    {
        var result = MetricsController.ResolveSystemPrompt(
            skillRubric: "Score 0-100 on clarity.", hardcodedPrompt: HardcodedPrompt,
            aiExpertise: null, defaultExpertise: "expert communication coach");

        Assert.Contains("You are a expert communication coach.", result);
    }

    [Fact]
    public void Resolve_UsesMetricAIExpertise_WhenSet()
    {
        var result = MetricsController.ResolveSystemPrompt(
            skillRubric: "Score 0-100 on clarity.", hardcodedPrompt: HardcodedPrompt,
            aiExpertise: "20th century History professor", defaultExpertise: "expert communication coach");

        Assert.Contains("You are a 20th century History professor.", result);
        Assert.DoesNotContain("expert communication coach", result);
    }

    // ── OVERRIDE: BuildSkillOverrideSystemPrompt content shape ─────────────────────────────────

    [Fact]
    public void BuildOverride_EmbedsRubricVerbatim()
    {
        const string rubric = "- **Stakeholder Engagement (0-100)**\n  - 76-100: exceptional engagement.";

        var result = MetricsController.BuildSkillOverrideSystemPrompt(null, "default expert", rubric);

        Assert.Contains(rubric, result);
        Assert.Contains("=== ASSESSMENT RUBRIC ===", result);
        Assert.Contains("=== END RUBRIC ===", result);
    }

    [Fact]
    public void BuildOverride_IncludesJsonContract()
    {
        var result = MetricsController.BuildSkillOverrideSystemPrompt(null, "default expert", "Some rubric.");

        Assert.Contains("\"categories\"", result);
        Assert.Contains("\"narrative\"", result);
    }

    [Fact]
    public void BuildOverride_TrimsAIExpertise()
    {
        var result = MetricsController.BuildSkillOverrideSystemPrompt("  Senior PM  ", "default expert", "Some rubric.");

        Assert.Contains("You are a Senior PM.", result);
    }

    // ── ADD: AppendInstituteSkillRubric (GapAnalysis) ───────────────────────────────────────────

    private const string GapAnalysisBaseTemplate =
        "You are an expert in the student's role: Backend Developer.\n\nRules:\n- Do not conclude that no work was delivered after a merge.\n" +
        "{\"categories\":[{\"name\":\"string\",\"score\":0,\"rationale\":\"string\"}],\"narrative\":\"markdown\"}";

    [Fact]
    public void Append_ReturnsBaseUnchanged_WhenSkillIsNull()
    {
        var result = MetricsController.AppendInstituteSkillRubric(GapAnalysisBaseTemplate, null);

        Assert.Equal(GapAnalysisBaseTemplate, result);
    }

    [Fact]
    public void Append_ReturnsBaseUnchanged_WhenSkillIsWhitespaceOnly()
    {
        var result = MetricsController.AppendInstituteSkillRubric(GapAnalysisBaseTemplate, "   ");

        Assert.Equal(GapAnalysisBaseTemplate, result);
    }

    [Fact]
    public void Append_KeepsHardcodedRulesIntact_WhenSkillIsDefined()
    {
        var result = MetricsController.AppendInstituteSkillRubric(GapAnalysisBaseTemplate, "Score 0-100 on database design quality.");

        // The ~20 domain rules baked into the hardcoded template must survive verbatim — this is
        // what distinguishes ADD (GapAnalysis) from OVERRIDE (CustomerEngagement/MeetingsCommunication).
        Assert.Contains("Do not conclude that no work was delivered after a merge.", result);
        Assert.Contains("You are an expert in the student's role: Backend Developer.", result);
    }

    [Fact]
    public void Append_AddsInstituteRubric_AfterBaseTemplate()
    {
        const string rubric = "Score 0-100 on database design quality.";

        var result = MetricsController.AppendInstituteSkillRubric(GapAnalysisBaseTemplate, rubric);

        Assert.Contains(rubric, result);
        Assert.Contains("INSTITUTE-SPECIFIC ADDITIONAL RUBRIC", result);
        // The addition must come after the base template, not replace or precede it.
        Assert.True(result.IndexOf(GapAnalysisBaseTemplate, StringComparison.Ordinal) == 0);
        Assert.True(result.IndexOf(rubric, StringComparison.Ordinal) > result.IndexOf(GapAnalysisBaseTemplate, StringComparison.Ordinal) + GapAnalysisBaseTemplate.Length - 1);
    }

    [Fact]
    public void Append_TrimsInstituteRubric()
    {
        var result = MetricsController.AppendInstituteSkillRubric(GapAnalysisBaseTemplate, "  Score on clarity.  \n");

        Assert.Contains("Score on clarity.", result);
        Assert.DoesNotContain("  Score on clarity.  ", result); // leading/trailing whitespace must be trimmed
    }

    [Fact]
    public void Append_NotesBaseRulesTakePrecedenceOnConflict()
    {
        var result = MetricsController.AppendInstituteSkillRubric(GapAnalysisBaseTemplate, "Some institute rubric.");

        Assert.Contains("the rule above takes precedence", result);
    }

    // ── Policy divergence: same non-blank Skill text, opposite outcome for hardcoded content ────

    [Fact]
    public void SameSkillRubric_IsOverrideForCommunication_ButAddForGapAnalysis()
    {
        const string rubric = "- **Clarity (0-100)**\n  - 76-100: excellent.";

        var overrideResult = MetricsController.ResolveSystemPrompt(
            rubric, HardcodedPrompt, aiExpertise: null, defaultExpertise: "default expert");
        var addResult = MetricsController.AppendInstituteSkillRubric(GapAnalysisBaseTemplate, rubric);

        // OVERRIDE: hardcoded text is gone.
        Assert.DoesNotContain("HARDCODED-PROMPT-MARKER", overrideResult);
        // ADD: hardcoded text (domain rules) survives alongside the rubric.
        Assert.Contains("Do not conclude that no work was delivered after a merge.", addResult);
        Assert.Contains(rubric, addResult);
    }
}
