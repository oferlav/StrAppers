namespace strAppersBackend.Controllers;

/// <summary>
/// Institute-level <see cref="Models.Metric.Skill"/> integration for the three legacy metrics that use a
/// hardcoded system prompt file (CustomerEngagement, MeetingsCommunication, GapAnalysis) instead of the
/// generic Data Assessment Engine (<see cref="MetricsController.RunAssessmentEngine"/>), which already
/// reads <see cref="Models.Metric.Skill"/> directly.
///
/// Policy:
/// - CustomerEngagement / MeetingsCommunication (<see cref="ResolveSystemPrompt"/>): OVERRIDE. Their
///   hardcoded prompts are a short generic rubric with example category names — when the institute has
///   defined its own <see cref="Models.Metric.Skill"/> rubric, it fully replaces the hardcoded prompt.
/// - GapAnalysis (<see cref="AppendInstituteSkillRubric"/>): ADD. Its hardcoded prompt encodes ~20
///   domain-specific evidence-interpretation rules (PR/branch semantics, Figma-failure handling, the
///   REQUIRED DELIVERABLE NOT FOUND marker, PM-vs-developer scoping, etc.) that a rubric cannot replicate.
///   The institute's <see cref="Models.Metric.Skill"/> text is appended as supplementary guidance; the
///   hardcoded rules are kept in full and take precedence on conflict.
/// </summary>
public partial class MetricsController
{
    /// <summary>
    /// OVERRIDE: returns a rubric-driven system prompt built from <paramref name="skillRubric"/> when it
    /// is non-blank; otherwise returns <paramref name="hardcodedPrompt"/> unchanged.
    /// </summary>
    internal static string ResolveSystemPrompt(
        string? skillRubric, string hardcodedPrompt, string? aiExpertise, string defaultExpertise)
    {
        var trimmedRubric = skillRubric?.Trim();
        return string.IsNullOrEmpty(trimmedRubric)
            ? hardcodedPrompt
            : BuildSkillOverrideSystemPrompt(aiExpertise, defaultExpertise, trimmedRubric);
    }

    /// <summary>Builds a system prompt whose scoring authority is the institute's own rubric text, verbatim.</summary>
    internal static string BuildSkillOverrideSystemPrompt(string? aiExpertise, string defaultExpertise, string skillRubric)
    {
        var expertise = string.IsNullOrWhiteSpace(aiExpertise) ? defaultExpertise : aiExpertise.Trim();
        return $$"""
            You are a {{expertise}}.

            Your task: score the student's performance for this sprint according to the ASSESSMENT RUBRIC below, using only the evidence provided in the CONTEXT section of the user message.

            === ASSESSMENT RUBRIC ===
            {{skillRubric}}
            === END RUBRIC ===

            Rules:
            - Ground every score and rationale in verbatim evidence from the CONTEXT. Do not invent conversations, meetings, or activity not shown.
            - Name your output categories after the scoring dimensions defined in the rubric above.
            - Scores are integers on a 0-100 scale.
            - Output valid JSON only (no markdown fences), exactly:
            {"categories":[{"name":"string","score":0,"rationale":"string"}],"narrative":"markdown"}
            - narrative: a brief markdown summary of strengths, gaps, and 1-3 concrete follow-ups.
            """;
    }

    /// <summary>
    /// ADD: appends <paramref name="skillRubric"/> to <paramref name="baseSystemPrompt"/> as supplementary
    /// guidance when non-blank; returns <paramref name="baseSystemPrompt"/> unchanged otherwise.
    /// </summary>
    internal static string AppendInstituteSkillRubric(string baseSystemPrompt, string? skillRubric)
    {
        var trimmedRubric = skillRubric?.Trim();
        if (string.IsNullOrEmpty(trimmedRubric))
            return baseSystemPrompt;

        return baseSystemPrompt + "\n\n" + $$"""
            === INSTITUTE-SPECIFIC ADDITIONAL RUBRIC ===
            The institute running this course has defined additional scoring guidance for this metric. Apply it together with all the rules above. If it conflicts with a rule above, the rule above takes precedence.

            {{trimmedRubric}}
            === END ADDITIONAL RUBRIC ===
            """;
    }
}
