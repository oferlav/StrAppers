using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

public class CourseBoardBuildRequest
{
    [Required]
    public int ProjectId { get; set; }

    [Range(1, 30)]
    public int SprintLengthInDays { get; set; } = 7;

    [Required]
    public int InstituteRoleId { get; set; }

    [Required]
    public int TemplateId { get; set; }

    /// <summary>
    /// When true, the response includes the number of prompt and completion tokens consumed by the AI generation.
    /// Defaults to false to keep the response lightweight.
    /// </summary>
    public bool IncludeTokenUsage { get; set; } = false;
}

public class CourseBoardBuildResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public TrelloProjectCreationRequest? BoardTemplate { get; set; }

    /// <summary>
    /// Token usage for the AI generation call. Only populated when <see cref="CourseBoardBuildRequest.IncludeTokenUsage"/> is true.
    /// </summary>
    public CourseBuildTokenUsage? TokenUsage { get; set; }
}

public class CourseBuildTokenUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens => PromptTokens + CompletionTokens;
}
