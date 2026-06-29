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
        string? AIExpertise,
        MetricSensorFlagsDto? Sensors = null);

    public record CreateMetricRequest(
        string Name,
        string? Description,
        string? Category,
        bool Required,
        int Influence,
        string? Skill,
        string? AIExpertise,
        string? Endpoint,
        MetricSensorFlagsDto? Sensors = null);

    public record SkillDefinitionAssistRequest(
        string MetricName,
        string? RawSkill,
        string? AiInstruction,
        string? AiExpertise);

    /// <summary>Update editable fields on an institute-owned metric. Returns 403 for base-institute (Id=1) rows.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateMetric(int id, [FromQuery] int instituteId, [FromBody] UpdateMetricRequest request)
    {
        if (instituteId <= 0)
            return BadRequest(new { message = "instituteId query param is required." });

        var metric = await _context.Metrics.FirstOrDefaultAsync(m => m.Id == id);
        if (metric == null)
            return NotFound(new { message = $"Metric {id} not found." });

        if (metric.InstituteId != instituteId)
            return StatusCode(403, new { message = "You can only edit metrics belonging to your institute." });

        if (metric.InstituteId == 1)
            return StatusCode(403, new { message = "Base-institute metrics are read-only." });

        metric.Description = request.Description;
        metric.Category    = request.Category;
        metric.Required    = request.Required;
        metric.Influence   = Math.Clamp(request.Influence, 1, 5);
        metric.Skill       = request.Skill;
        metric.AIExpertise = request.AIExpertise;

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
            Influence   = Math.Clamp(request.Influence, 1, 5),
            Skill       = request.Skill,
            AIExpertise = request.AIExpertise,
            Endpoint    = request.Endpoint,
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

    /// <summary>Delete an institute-owned metric. Returns 403 for base-institute (Id=1) rows.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteMetric(int id, [FromQuery] int instituteId)
    {
        if (instituteId <= 0)
            return BadRequest(new { message = "instituteId query param is required." });

        var metric = await _context.Metrics.FirstOrDefaultAsync(m => m.Id == id);
        if (metric == null)
            return NotFound(new { message = $"Metric {id} not found." });

        if (metric.InstituteId == 1)
            return StatusCode(403, new { message = "Base-institute metrics cannot be deleted." });

        if (metric.InstituteId != instituteId)
            return StatusCode(403, new { message = "You can only delete metrics belonging to your institute." });

        _context.Metrics.Remove(metric);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// AI assistant for the Skill Definition dialog. Expands and refines the professor's raw skill
    /// text into a structured assessment rubric with 3–5 scoring dimensions.
    /// </summary>
    [HttpPost("use/skill-definition-assist")]
    public async Task<ActionResult<object>> SkillDefinitionAssist([FromBody] SkillDefinitionAssistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MetricName))
            return BadRequest(new { message = "MetricName is required." });

        var expertise = string.IsNullOrWhiteSpace(request.AiExpertise)
            ? "professional skills assessment expert"
            : request.AiExpertise.Trim();

        var systemPrompt = $"""
            You are a {expertise}.
            Your job is to help an academic professor write a clear, structured assessment rubric for one skill metric.

            Rules:
            - Define 3–5 scoring dimensions. Each must have a name, a 0–100 scale description, and concrete rationale guidelines.
            - Write in markdown bullet format. Use **bold** for dimension names.
            - Be precise and academic in tone. No JSON, no code fences.
            - Base the rubric strictly on the raw description and any instruction provided. Do not invent new objectives.
            - The rubric will be fed verbatim into an AI assessment agent that will evaluate student sprint work.
            """;

        var rawSkill = string.IsNullOrWhiteSpace(request.RawSkill)
            ? "(no description provided yet)"
            : request.RawSkill.Trim();

        var userPrompt = $"""
            Metric name: {request.MetricName.Trim()}
            {(string.IsNullOrWhiteSpace(request.AiInstruction) ? "" : $"Additional context: {request.AiInstruction.Trim()}")}

            Raw skill description to refine:
            {rawSkill}

            Rewrite this as a structured assessment rubric following your instructions.
            """;

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
}
