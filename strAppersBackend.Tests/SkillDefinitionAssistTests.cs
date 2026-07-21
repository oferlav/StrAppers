using strAppersBackend.Controllers;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests for the Skill Definition dialog's AI-assist prompt builders
/// (<see cref="MetricsController.BuildSkillDefinitionAssistSystemPrompt"/>,
/// <see cref="MetricsController.BuildSkillDefinitionAssistUserPrompt"/>). Rubric mode (default) writes a
/// scored 0-100 rubric grounded in the raw rubric text. Rules mode ("kind": "rules") writes non-scored,
/// imperative rules grounded in the metric's name + description instead.
/// </summary>
public class SkillDefinitionAssistTests
{
    // ── System prompt: mode selection ───────────────────────────────────────────

    [Fact]
    public void SystemPrompt_RubricMode_AsksForScoredDimensions()
    {
        var result = MetricsController.BuildSkillDefinitionAssistSystemPrompt("expert", isRulesMode: false);

        Assert.Contains("3–5 scoring dimensions", result);
        Assert.Contains("0–100 scale", result);
    }

    [Fact]
    public void SystemPrompt_RulesMode_ForbidsScoredDimensions()
    {
        var result = MetricsController.BuildSkillDefinitionAssistSystemPrompt("expert", isRulesMode: true);

        Assert.Contains("NOT a scored rubric", result);
        Assert.Contains("Do not invent 0–100 scales or scored dimensions", result);
    }

    [Fact]
    public void SystemPrompt_RulesMode_AsksForImperativeRules()
    {
        var result = MetricsController.BuildSkillDefinitionAssistSystemPrompt("expert", isRulesMode: true);

        Assert.Contains("concrete, imperative instructions", result);
    }

    [Fact]
    public void SystemPrompt_EmbedsExpertisePersona_InBothModes()
    {
        var rubricPrompt = MetricsController.BuildSkillDefinitionAssistSystemPrompt("20th century History professor", isRulesMode: false);
        var rulesPrompt = MetricsController.BuildSkillDefinitionAssistSystemPrompt("20th century History professor", isRulesMode: true);

        Assert.Contains("You are a 20th century History professor.", rubricPrompt);
        Assert.Contains("You are a 20th century History professor.", rulesPrompt);
    }

    // ── User prompt: rubric mode (grounded in raw rubric text) ──────────────────

    [Fact]
    public void UserPrompt_RubricMode_UsesRawSkillAsRubricToRefine()
    {
        var request = new MetricsController.SkillDefinitionAssistRequest(
            MetricName: "Research Depth", RawSkill: "Score based on citation quality.",
            AiInstruction: null, AiExpertise: null);

        var result = MetricsController.BuildSkillDefinitionAssistUserPrompt(request, isRulesMode: false);

        Assert.Contains("Raw skill description to refine:", result);
        Assert.Contains("Score based on citation quality.", result);
        Assert.DoesNotContain("Metric description:", result);
    }

    [Fact]
    public void UserPrompt_RubricMode_PlaceholderWhenRawSkillEmpty()
    {
        var request = new MetricsController.SkillDefinitionAssistRequest(
            MetricName: "Research Depth", RawSkill: null, AiInstruction: null, AiExpertise: null);

        var result = MetricsController.BuildSkillDefinitionAssistUserPrompt(request, isRulesMode: false);

        Assert.Contains("(no description provided yet)", result);
    }

    // ── User prompt: rules mode (grounded in metric name + description) ─────────

    [Fact]
    public void UserPrompt_RulesMode_IncludesMetricDescription()
    {
        var request = new MetricsController.SkillDefinitionAssistRequest(
            MetricName: "Customer Requirements Fidelity",
            RawSkill: null,
            AiInstruction: null,
            AiExpertise: null,
            Kind: "rules",
            Description: "How well the student's work reflects the customer's stated requirements.");

        var result = MetricsController.BuildSkillDefinitionAssistUserPrompt(request, isRulesMode: true);

        Assert.Contains("Metric description: How well the student's work reflects the customer's stated requirements.", result);
        Assert.Contains("Metric name: Customer Requirements Fidelity", result);
    }

    [Fact]
    public void UserPrompt_RulesMode_UsesRawSkillAsExplicitRulesToRefine_NotRubric()
    {
        var request = new MetricsController.SkillDefinitionAssistRequest(
            MetricName: "Customer Requirements Fidelity",
            RawSkill: "Never penalize a missing PRD extraction.",
            AiInstruction: null,
            AiExpertise: null,
            Kind: "rules",
            Description: "Some description.");

        var result = MetricsController.BuildSkillDefinitionAssistUserPrompt(request, isRulesMode: true);

        Assert.Contains("Raw explicit rules to refine:", result);
        Assert.Contains("Never penalize a missing PRD extraction.", result);
        Assert.DoesNotContain("Raw skill description to refine:", result);
    }

    [Fact]
    public void UserPrompt_RulesMode_PlaceholderWhenDescriptionMissing()
    {
        var request = new MetricsController.SkillDefinitionAssistRequest(
            MetricName: "Custom Metric", RawSkill: null, AiInstruction: null, AiExpertise: null, Kind: "rules");

        var result = MetricsController.BuildSkillDefinitionAssistUserPrompt(request, isRulesMode: true);

        Assert.Contains("Metric description: (no description provided)", result);
        Assert.Contains("(no explicit rules provided yet)", result);
    }

    [Fact]
    public void UserPrompt_IncludesAiInstruction_WhenProvided_InBothModes()
    {
        var rubricRequest = new MetricsController.SkillDefinitionAssistRequest(
            MetricName: "M", RawSkill: "x", AiInstruction: "Focus on primary sources.", AiExpertise: null);
        var rulesRequest = new MetricsController.SkillDefinitionAssistRequest(
            MetricName: "M", RawSkill: "x", AiInstruction: "Focus on primary sources.", AiExpertise: null, Kind: "rules");

        Assert.Contains("Additional context: Focus on primary sources.",
            MetricsController.BuildSkillDefinitionAssistUserPrompt(rubricRequest, isRulesMode: false));
        Assert.Contains("Additional context: Focus on primary sources.",
            MetricsController.BuildSkillDefinitionAssistUserPrompt(rulesRequest, isRulesMode: true));
    }

    [Fact]
    public void UserPrompt_OmitsAiInstructionLine_WhenNotProvided()
    {
        var request = new MetricsController.SkillDefinitionAssistRequest(
            MetricName: "M", RawSkill: "x", AiInstruction: null, AiExpertise: null);

        var result = MetricsController.BuildSkillDefinitionAssistUserPrompt(request, isRulesMode: false);

        Assert.DoesNotContain("Additional context:", result);
    }
}
