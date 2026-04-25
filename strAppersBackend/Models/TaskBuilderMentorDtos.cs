using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

/// <summary>POST body for <c>POST /api/Projects/use/by-institute/ai-assistant/Templates/{id}</c> (project id in route, not in body).</summary>
public class ProjectInstituteTemplatesAssistantRequest
{
    /// <summary>Optional; 0 = no module selected.</summary>
    public int ModuleId { get; set; }

    /// <summary>When set, server loads Trello JSON from <c>InstituteTemplates</c> (avoids a large <see cref="TrelloBoardJson"/> in the POST). Must match institute + <c>ProjectId</c> from the route.</summary>
    public int? InstituteTemplateId { get; set; }

    /// <summary>Full Trello template JSON. Omit or send <c>{}</c> when <see cref="InstituteTemplateId"/> is set.</summary>
    public string TrelloBoardJson { get; set; } = string.Empty;

    public string SprintName { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    /// <summary>Validated in controller (non-empty).</summary>
    public string UserMessage { get; set; } = string.Empty;

    public string? CurrentDescription { get; set; }

    public List<string>? CurrentChecklistItems { get; set; }

    /// <summary>When true, returns prompts only and does not call the LLM.</summary>
    public bool Test { get; set; }
}

/// <summary>Legacy: POST body when using <c>/api/Mentor/use/task-builder</c> (deprecated; project id in body).</summary>
public class TaskBuilderMentorRequest
{
    [Required]
    public int ProjectId { get; set; }

    /// <summary>Optional; 0 = no module selected.</summary>
    public int ModuleId { get; set; }

    /// <summary>Full Trello template JSON string (may be large).</summary>
    [Required]
    public string TrelloBoardJson { get; set; } = string.Empty;

    public string SprintName { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    /// <summary>End-user message for the assistant.</summary>
    [Required]
    public string UserMessage { get; set; } = string.Empty;

    /// <summary>Current card description from the Task Builder UI (optional context).</summary>
    public string? CurrentDescription { get; set; }

    /// <summary>Current checklist lines from the Task Builder UI (optional context).</summary>
    public List<string>? CurrentChecklistItems { get; set; }

    /// <summary>When true, returns prompts only and does not call the LLM.</summary>
    public bool Test { get; set; }
}

/// <summary>LLM JSON shape (camelCase; deserialized case-insensitively).</summary>
public class TaskBuilderMentorLlmResult
{
    public string? AiReply { get; set; }

    public string? Description { get; set; }

    public List<string>? ChecklistItems { get; set; }
}
