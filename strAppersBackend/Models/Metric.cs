using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>Catalog of assessment metric types. InstituteId=1 rows are base/system metrics; institute-owned rows carry the institute's own Id.</summary>
public class Metric
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Endpoint { get; set; }

    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    public bool Required { get; set; }

    public int Influence { get; set; } = 3;

    /// <summary>Full assessment rubric fed into the Data Assessment Engine prompt.</summary>
    public string? Skill { get; set; }

    /// <summary>
    /// Explicit, non-scored rules for the grading AI (edge cases, interpretation guidance) — appended
    /// after the rubric in the Data Assessment Engine prompt.
    /// </summary>
    public string? ExplicitRules { get; set; }

    /// <summary>Expert persona for the LLM system prompt, e.g. "senior software engineering lead".</summary>
    [MaxLength(500)]
    public string? AIExpertise { get; set; }

    public int? InstituteId { get; set; }
    public Institute? Institute { get; set; }

    // Per-sensor toggles — all default true so existing metrics keep all sources enabled.
    public bool UseCustomerChat { get; set; } = true;
    public bool UseMentorChat { get; set; } = true;
    public bool UseCodebaseQuality { get; set; } = true;
    public bool UseResources { get; set; } = true;
    public bool UseStakeholders { get; set; } = true;
    public bool UseProjectModule { get; set; } = true;
    public bool UseMeetingTranscripts { get; set; } = true;
    public bool UseGroupChat { get; set; } = true;
    public bool UsePrivateChat { get; set; } = true;
    public bool UseTrelloTasks { get; set; } = true;
    public bool UseTrelloUserStory { get; set; } = true;
    public bool UseFigmaDesign { get; set; } = true;
}
