using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

// One row per job posting — either synced from an ATS or entered manually.
// Holds standard job fields + Skill-in custom fields.
public class AtsJobPosting
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Required]
    [Column("EmployerId")]
    public int EmployerId { get; set; }

    // Nullable — null when Mode is 'manual-token' or 'open'
    [Column("AtsConnectionId")]
    public int? AtsConnectionId { get; set; }

    // Mirrors AtsConnection.Provider; denormalised for query convenience
    [Required]
    [Column("Provider")]
    [MaxLength(50)]
    public string Provider { get; set; } = "manual";

    // External job ID from the ATS (e.g. Greenhouse job_id); null for manual entries
    [Column("ExternalJobId")]
    [MaxLength(100)]
    public string? ExternalJobId { get; set; }

    // True when job data was last populated from the ATS
    [Column("IsAtsSynced")]
    public bool IsAtsSynced { get; set; } = false;

    // ── Standard job fields ──────────────────────────────────────────────────────
    // Locked in UI when IsAtsSynced = true; free-text when false.

    [Required]
    [Column("Title")]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Column("Location")]
    [MaxLength(500)]
    public string? Location { get; set; }

    [Column("Department")]
    [MaxLength(200)]
    public string? Department { get; set; }

    [Column("EmploymentType")]
    [MaxLength(100)]
    public string? EmploymentType { get; set; }

    [Column("Description")]
    public string? Description { get; set; }

    // ── Skill-in custom fields ───────────────────────────────────────────────────
    // Always editable regardless of ATS toggle.
    // When ATS is connected these map to Greenhouse custom fields of the same name.

    [Column("Challenge")]
    public string? Challenge { get; set; }

    [Column("Resource")]
    [MaxLength(1000)]
    public string? Resource { get; set; }

    [Column("ResourceGithubUrl")]
    [MaxLength(1000)]
    public string? ResourceGithubUrl { get; set; }

    [Column("Expectations")]
    public string? Expectations { get; set; }

    [Column("QA")]
    public string? QA { get; set; }

    // Full ATS metadata snapshot (audit / future field support)
    [Column("RawMetadataJson")]
    public string? RawMetadataJson { get; set; }

    // true = job is publicly listed on Skill-in for self-apply (pull / open modes)
    [Column("IsPublic")]
    public bool IsPublic { get; set; } = false;

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("LastSyncedAt")]
    public DateTime? LastSyncedAt { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("UpdatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey(nameof(EmployerId))]
    public virtual Employer Employer { get; set; } = null!;

    [ForeignKey(nameof(AtsConnectionId))]
    public virtual AtsConnection? AtsConnection { get; set; }

    public virtual ICollection<AtsAssessmentInstance> AssessmentInstances { get; set; } = new List<AtsAssessmentInstance>();
}
