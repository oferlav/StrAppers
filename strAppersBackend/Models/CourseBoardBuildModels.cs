using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

public class CourseBoardBuildRequest
{
    [Required]
    public int ProjectId { get; set; }

    /// <summary>When true, <see cref="ProjectId"/> refers to <see cref="InstituteProject"/> (not <see cref="Project"/>).</summary>
    public bool InstituteProject { get; set; }

    [Range(1, 30)]
    public int SprintLengthInDays { get; set; } = 7;

    [Range(1, 200)]
    public int? NumberOfSprints { get; set; }

    /// <summary>
    /// Number of project modules to include in the course. Defaults to all modules found on the project.
    /// Cannot exceed the actual number of modules in the project.
    /// </summary>
    [Range(2, 20)]
    public int? NumberOfModules { get; set; }

    /// <summary>
    /// Tier 1 — uniform module length: how many sprints each module spans (default 1).
    /// Ignored when <see cref="ModuleLengths"/> is provided.
    /// </summary>
    [Range(1, 10)]
    public int ModuleLengthInSprints { get; set; } = 1;

    /// <summary>
    /// Tier 2 — per-module sprint lengths. When provided, takes precedence over
    /// <see cref="ModuleLengthInSprints"/>. The list length must equal the resolved module count.
    /// Example: [1, 2, 2, 3, 1] means module 1 spans 1 sprint, module 2 spans 2 sprints, etc.
    /// </summary>
    public List<int>? ModuleLengths { get; set; }

    /// <summary>
    /// When true, returns the computed sprint plan per role without making any AI calls,
    /// creating Trello boards, or writing to the database. Use this to validate the course
    /// structure and module distribution before running the full generation.
    /// </summary>
    public bool DryRun { get; set; } = false;

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
    /// Populated when <see cref="CourseBoardBuildRequest.DryRun"/> is true.
    /// Contains the computed sprint plan per role — no AI calls made.
    /// </summary>
    public List<DryRunRolePlan>? DryRunPlans { get; set; }

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

public class DryRunRolePlan
{
    public string RoleName { get; set; } = string.Empty;
    public string Track { get; set; } = string.Empty;
    public List<DryRunSprintSlot> SprintPlan { get; set; } = new();
}

public class DryRunSprintSlot
{
    public int SprintNumber { get; set; }

    /// <summary>Null when the sprint has no module (setup / gap / GTM / stabilization sprint).</summary>
    public int? ModuleId { get; set; }

    public string? ModuleTitle { get; set; }

    /// <summary>"1 of 2" style string when the module spans multiple sprints. Null for single-sprint modules.</summary>
    public string? Part { get; set; }

    /// <summary>Human-readable label: module title (with part) or special sprint description.</summary>
    public string Label { get; set; } = string.Empty;
}
