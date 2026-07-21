using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers;

/// <summary>CRUD endpoints for institute-owned metrics and the Skill Definition AI assistant.</summary>
public partial class MetricsController
{
    public record UpdateMetricRequest(
        string? Description,
        string? Category,
        bool Required,
        int Influence,
        string? Skill,
        string? ExplicitRules,
        string? AIExpertise,
        MetricSensorFlagsDto? Sensors = null);

    public record CreateMetricRequest(
        string Name,
        string? Description,
        string? Category,
        bool Required,
        int Influence,
        string? Skill,
        string? ExplicitRules,
        string? AIExpertise,
        string? Endpoint,
        MetricSensorFlagsDto? Sensors = null);

    /// <summary>
    /// <paramref name="Kind"/>: "rubric" (default, null also means rubric) refines the scored Assessment
    /// Rubric from <paramref name="RawSkill"/>. "rules" refines the non-scored Explicit Rules box (passed
    /// in <paramref name="RawSkill"/> too — it's "whichever box's raw text this call is refining") and is
    /// grounded in <paramref name="Description"/> (the metric's description) instead of the rubric.
    /// </summary>
    public record SkillDefinitionAssistRequest(
        string MetricName,
        string? RawSkill,
        string? AiInstruction,
        string? AiExpertise,
        string? Kind = null,
        string? Description = null);

    /// <summary>Update editable fields on an institute-owned metric. Returns 403 for base-institute (Id=1) rows.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateMetric(int id, [FromQuery] int instituteId, [FromBody] UpdateMetricRequest request)
    {
        if (instituteId <= 0)
            return BadRequest(new { message = "instituteId query param is required." });

        var metric = await _context.Metrics.FirstOrDefaultAsync(m => m.Id == id);
        if (metric == null)
            return NotFound(new { message = $"Metric {id} not found." });

        if (metric.InstituteId != 1 && metric.InstituteId != instituteId)
            return StatusCode(403, new { message = "You can only edit metrics belonging to your institute." });

        metric.Description = request.Description;
        metric.Category    = request.Category;
        metric.Required    = request.Required;
        metric.Influence   = Math.Clamp(request.Influence, 1, 5);
        metric.Skill         = request.Skill;
        metric.ExplicitRules = request.ExplicitRules;
        metric.AIExpertise   = request.AIExpertise;

        if (request.Sensors is { } s)
        {
            metric.UseCustomerChat     = s.CustomerChat;
            metric.UseMentorChat       = s.MentorChat;
            metric.UseCodebaseQuality  = s.CodebaseQuality;
            metric.UseResources        = s.Resources;
            metric.UseStakeholders     = s.Stakeholders;
            metric.UseProjectModule    = s.ProjectModule;
            metric.UseMeetingTranscripts = s.MeetingTranscripts;
            metric.UseGroupChat        = s.GroupChat;
            metric.UsePrivateChat      = s.PrivateChat;
            metric.UseTrelloTasks      = s.TrelloTasks;
            metric.UseTrelloUserStory  = s.TrelloUserStory;
            metric.UseFigmaDesign      = s.FigmaDesign;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Create a new metric owned by the specified institute.</summary>
    [HttpPost("institute/{instituteId:int}")]
    public async Task<ActionResult<MetricAssessmentConfigDto>> CreateMetric(int instituteId, [FromBody] CreateMetricRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });

        var institute = await _context.Institutes.AsNoTracking().AnyAsync(i => i.Id == instituteId);
        if (!institute)
            return NotFound(new { message = $"Institute {instituteId} not found." });

        var metric = new Metric
        {
            Name        = request.Name.Trim(),
            InstituteId = instituteId,
            Description = request.Description,
            Category    = request.Category,
            Required    = request.Required,
            Influence     = Math.Clamp(request.Influence, 1, 5),
            Skill         = request.Skill,
            ExplicitRules = request.ExplicitRules,
            AIExpertise   = request.AIExpertise,
            Endpoint      = request.Endpoint,
        };
        if (request.Sensors is { } cs)
        {
            metric.UseCustomerChat     = cs.CustomerChat;
            metric.UseMentorChat       = cs.MentorChat;
            metric.UseCodebaseQuality  = cs.CodebaseQuality;
            metric.UseResources        = cs.Resources;
            metric.UseStakeholders     = cs.Stakeholders;
            metric.UseProjectModule    = cs.ProjectModule;
            metric.UseMeetingTranscripts = cs.MeetingTranscripts;
            metric.UseGroupChat        = cs.GroupChat;
            metric.UsePrivateChat      = cs.PrivateChat;
            metric.UseTrelloTasks      = cs.TrelloTasks;
            metric.UseTrelloUserStory  = cs.TrelloUserStory;
            metric.UseFigmaDesign      = cs.FigmaDesign;
        }

        _context.Metrics.Add(metric);
        await _context.SaveChangesAsync();

        var dto = new MetricAssessmentConfigDto(
            metric.Id,
            ToSlugKey(metric.Name),
            metric.Name,
            metric.Category,
            metric.Description,
            metric.Endpoint,
            metric.Required,
            metric.Influence,
            metric.Skill,
            metric.ExplicitRules,
            metric.AIExpertise,
            false,
            "db",
            new MetricSensorFlagsDto(
                metric.UseCustomerChat, metric.UseMentorChat, metric.UseCodebaseQuality,
                metric.UseResources, metric.UseStakeholders, metric.UseProjectModule,
                metric.UseMeetingTranscripts, metric.UseGroupChat, metric.UsePrivateChat,
                metric.UseTrelloTasks, metric.UseTrelloUserStory, metric.UseFigmaDesign));

        return CreatedAtAction(nameof(GetAssessmentMetricConfiguration), new { instituteId }, dto);
    }

    /// <summary>
    /// Core metrics backed by hardcoded BE logic (batch runner routes them by name slug).
    /// They cannot be deleted; their names must stay intact or the routing breaks.
    /// </summary>
    internal static readonly HashSet<string> CoreMetricSlugs = new(StringComparer.Ordinal)
        { "adherence", "gapanalysis", "attendance", "customerengagement", "communication" };

    /// <summary>Delete an institute-owned metric. Returns 403 for base-institute (Id=1) rows and core metrics.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteMetric(int id, [FromQuery] int instituteId)
    {
        if (instituteId <= 0)
            return BadRequest(new { message = "instituteId query param is required." });

        var metric = await _context.Metrics.FirstOrDefaultAsync(m => m.Id == id);
        if (metric == null)
            return NotFound(new { message = $"Metric {id} not found." });

        if (metric.InstituteId != 1 && metric.InstituteId != instituteId)
            return StatusCode(403, new { message = "You can only delete metrics belonging to your institute." });

        if (CoreMetricSlugs.Contains(ToSlugKey(metric.Name)))
            return StatusCode(403, new { message = $"'{metric.Name}' is a core metric and cannot be deleted. Disable it instead (Active toggle)." });

        _context.Metrics.Remove(metric);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// AI assistant for the Skill Definition dialog. In rubric mode (default), expands the professor's
    /// raw text into a structured, scored assessment rubric. In rules mode (<see cref="SkillDefinitionAssistRequest.Kind"/>
    /// == "rules"), expands it into non-scored explicit rules for the grading AI, grounded in the metric's
    /// name and description rather than the rubric.
    /// </summary>
    [HttpPost("use/skill-definition-assist")]
    public async Task<ActionResult<object>> SkillDefinitionAssist([FromBody] SkillDefinitionAssistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MetricName))
            return BadRequest(new { message = "MetricName is required." });

        var expertise = string.IsNullOrWhiteSpace(request.AiExpertise)
            ? "professional skills assessment expert"
            : request.AiExpertise.Trim();
        var isRulesMode = string.Equals(request.Kind, "rules", StringComparison.OrdinalIgnoreCase);

        var systemPrompt = BuildSkillDefinitionAssistSystemPrompt(expertise, isRulesMode);
        var userPrompt = BuildSkillDefinitionAssistUserPrompt(request, isRulesMode);

        try
        {
            var cheapName = _configuration["OpenAI:CheapModel"] ?? "gpt-4o-mini";
            var aiModel = new Models.AIModel
            {
                Name               = cheapName,
                Provider           = "OpenAI",
                BaseUrl            = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1",
                MaxTokens          = 4096,
                DefaultTemperature = 0.4f,
            };

            var (text, _, _) = await _chatCompletionService.GetChatCompletionAsync(
                aiModel, systemPrompt, userPrompt, null);

            return Ok(new { success = true, refinedSkill = text.Trim() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Skill definition assist failed for metric {MetricName}", request.MetricName);
            return StatusCode(500, new { message = "AI assistant call failed. Please try again." });
        }
    }

    /// <summary>Rubric mode: scored 0–100 dimensions. Rules mode: non-scored, imperative grading guidance.</summary>
    internal static string BuildSkillDefinitionAssistSystemPrompt(string expertise, bool isRulesMode)
    {
        if (isRulesMode)
        {
            return $"""
                You are a {expertise}.
                Your job is to help an academic professor write clear, explicit rules that guide an AI assessment agent when it evaluates student sprint work for one skill metric — NOT a scored rubric.

                Rules:
                - Write concrete, imperative instructions (e.g. "Do not penalize X", "Treat Y as evidence of Z", "Ignore missing W when V is present"). These are interpretation rules and edge cases for the grading AI, not scoring dimensions.
                - Do not invent 0–100 scales or scored dimensions — that belongs in the separate assessment rubric, not here.
                - Write in markdown bullet format, one rule per bullet.
                - Base the rules strictly on the metric's name, description, and any instruction provided. Do not invent objectives unrelated to them.
                - These rules will be fed verbatim into an AI assessment agent alongside the metric's rubric.
                """;
        }

        return $"""
            You are a {expertise}.
            Your job is to help an academic professor write a clear, structured assessment rubric for one skill metric.

            Rules:
            - Define 3–5 scoring dimensions. Each must have a name, a 0–100 scale description, and concrete rationale guidelines.
            - Write in markdown bullet format. Use **bold** for dimension names.
            - Be precise and academic in tone. No JSON, no code fences.
            - Base the rubric strictly on the raw description and any instruction provided. Do not invent new objectives.
            - The rubric will be fed verbatim into an AI assessment agent that will evaluate student sprint work.
            """;
    }

    /// <summary>
    /// Rubric mode grounds the rewrite in <see cref="SkillDefinitionAssistRequest.RawSkill"/> (the raw
    /// rubric text). Rules mode grounds it in <see cref="SkillDefinitionAssistRequest.Description"/> (the
    /// metric's description) instead — <see cref="SkillDefinitionAssistRequest.RawSkill"/> is still used,
    /// but as "the raw explicit-rules text to refine", not rubric content.
    /// </summary>
    internal static string BuildSkillDefinitionAssistUserPrompt(SkillDefinitionAssistRequest request, bool isRulesMode)
    {
        var additionalContext = string.IsNullOrWhiteSpace(request.AiInstruction)
            ? ""
            : $"Additional context: {request.AiInstruction.Trim()}";

        if (isRulesMode)
        {
            var description = string.IsNullOrWhiteSpace(request.Description)
                ? "(no description provided)"
                : request.Description.Trim();
            var rawRules = string.IsNullOrWhiteSpace(request.RawSkill)
                ? "(no explicit rules provided yet)"
                : request.RawSkill.Trim();

            return $"""
                Metric name: {request.MetricName.Trim()}
                Metric description: {description}
                {additionalContext}

                Raw explicit rules to refine:
                {rawRules}

                Rewrite this as a set of clear explicit rules following your instructions.
                """;
        }

        var rawSkill = string.IsNullOrWhiteSpace(request.RawSkill)
            ? "(no description provided yet)"
            : request.RawSkill.Trim();

        return $"""
            Metric name: {request.MetricName.Trim()}
            {additionalContext}

            Raw skill description to refine:
            {rawSkill}

            Rewrite this as a structured assessment rubric following your instructions.
            """;
    }
}
