using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

public class CourseBoardBuildRequest
{
    [Required]
    public int ProjectId { get; set; }

    [Range(1, 30)]
    public int SprintLengthInDays { get; set; } = 7;

    [Range(1, 200)]
    public int? NumberOfSprints { get; set; }

    /// <summary>
    /// Number of project modules to include in the course. Defaults to all modules found on the project.
    /// Cannot exceed the actual number of modules in the project.
    /// Must satisfy: NumberOfSprints >= NumberOfModules + 3.
    /// </summary>
    [Range(2, 20)]
    public int? NumberOfModules { get; set; }

    /// <summary>
    /// Single-role mode (legacy / backward compat). Ignored when <see cref="InstituteRoleIds"/> is provided.
    /// </summary>
    public int? InstituteRoleId { get; set; }

    /// <summary>
    /// Squad mode — list of InstituteSquadRole IDs (or InstituteRole IDs for legacy templates)
    /// to generate cards for. Takes precedence over <see cref="InstituteRoleId"/>.
    /// At least one of InstituteRoleId or InstituteRoleIds must be provided.
    /// </summary>
    public List<int>? InstituteRoleIds { get; set; }

    [Required]
    public int TemplateId { get; set; }

    /// <summary>
    /// When true, the response includes the number of prompt and completion tokens consumed.
    /// Defaults to false to keep the response lightweight.
    /// </summary>
    public bool IncludeTokenUsage { get; set; } = false;

    /// <summary>
    /// When true, creates a real Trello board from the generated course template and stores the URL on the institute template row.
    /// </summary>
    public bool GenerateTrelloBoard { get; set; } = false;

    /// <summary>Resolved effective role IDs — InstituteRoleIds if provided, else InstituteRoleId as a single-item list.</summary>
    public List<int> EffectiveRoleIds =>
        InstituteRoleIds is { Count: > 0 }
            ? InstituteRoleIds
            : InstituteRoleId is > 0
                ? new List<int> { InstituteRoleId.Value }
                : new List<int>();
}

public class CourseBoardBuildResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public TrelloProjectCreationRequest? BoardTemplate { get; set; }
    public string? BoardUrl { get; set; }

    /// <summary>
    /// Token usage across all AI generation calls (summed). Only populated when
    /// <see cref="CourseBoardBuildRequest.IncludeTokenUsage"/> is true.
    /// </summary>
    public CourseBuildTokenUsage? TokenUsage { get; set; }
}

public class CourseBuildTokenUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens => PromptTokens + CompletionTokens;
}
