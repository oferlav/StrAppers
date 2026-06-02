using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Institute-owned project definition (custom designs and activated copies of built-in catalog projects).
/// Mirrors scalar columns of <see cref="Project"/> plus <see cref="InstituteId"/> and optional <see cref="BaseProjectId"/>.
/// </summary>
public class InstituteProject
{
    public int Id { get; set; }

    public int InstituteId { get; set; }

    public Institute Institute { get; set; } = null!;

    /// <summary>When set, this row was created from <see cref="Project"/> (built-in catalog).</summary>
    public int? BaseProjectId { get; set; }

    public Project? BaseProject { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Built-in catalog course label mirrored from <see cref="Project.CourseName"/> when activating or copying from catalog.</summary>
    [MaxLength(100)]
    public string? BuiltInCourseName { get; set; }

    /// <summary>Catalog copies may exceed legacy varchar(2000); column is TEXT.</summary>
    [Column(TypeName = "TEXT")]
    public string? Mission { get; set; }

    [MaxLength(250)]
    public string? OneLiner { get; set; }

    /// <summary>May exceed 1000 chars when copied from catalog projects; stored as TEXT.</summary>
    [Column(TypeName = "TEXT")]
    public string? Description { get; set; }

    [Column(TypeName = "TEXT")]
    public string? ExtendedDescription { get; set; }

    [Column(TypeName = "TEXT")]
    public string? SystemDesign { get; set; }

    [Column(TypeName = "TEXT")]
    public string? DataSchema { get; set; }

    [Column(TypeName = "TEXT")]
    public string? Logo { get; set; }

    public byte[]? SystemDesignDoc { get; set; }

    [Column("SystemDesignFormatted", TypeName = "TEXT")]
    public string? SystemDesignFormatted { get; set; }

    [MaxLength(50)]
    public string Priority { get; set; } = "Medium";

    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    [Column("isAvailable")]
    public bool IsAvailable { get; set; } = true;

    [Column("InUse")]
    public bool InUse { get; set; } = true;

    [Column("IsBuiltIn")]
    public bool IsBuiltIn { get; set; } = false;

    [Column("Kickoff")]
    public bool? Kickoff { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [Column("TrelloBoardJson", TypeName = "TEXT")]
    public string? TrelloBoardJson { get; set; }

    [Column("CustomerPastStory", TypeName = "TEXT")]
    public string? CustomerPastStory { get; set; }

    [Column("ShortBrief", TypeName = "TEXT")]
    public string? ShortBrief { get; set; }

    [Column("deployment_manifest", TypeName = "TEXT")]
    public string? DeploymentManifest { get; set; }

    [MaxLength(50)]
    [Column("ide_generation_status")]
    public string IdeGenerationStatus { get; set; } = "not_started";

    [Column("total_chunks")]
    public int TotalChunks { get; set; } = 0;

    [Column("completed_chunks")]
    public int CompletedChunks { get; set; } = 0;

    [Column("mock_records_count")]
    public int MockRecordsCount { get; set; } = 10;

    [MaxLength(500)]
    [Column("CriteriaIds")]
    public string? CriteriaIds { get; set; }

    /// <summary>
    /// Format: {Institutes.Coupon}-{Integer}. Multiple projects in the same institute
    /// may share the same coupon. Uniqueness across institutes is enforced by the prefix.
    /// </summary>
    [MaxLength(100)]
    public string? Coupon { get; set; }

    public ICollection<InstituteTemplate> InstituteTemplates { get; set; } = new List<InstituteTemplate>();

    public ICollection<InstituteProjectModule> InstituteProjectModules { get; set; } = new List<InstituteProjectModule>();
}
