using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

// One row per employer per ATS provider.
// Provider-specific config (board token, API keys, OAuth tokens) lives in ConnectionConfigJson.
public class AtsConnection
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Required]
    [Column("EmployerId")]
    public int EmployerId { get; set; }

    // 'greenhouse' | 'lever' | 'workday' | 'manual'
    [Required]
    [Column("Provider")]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    // 'greenhouse-push' | 'greenhouse-pull' | 'manual-token' | 'open'
    [Required]
    [Column("Mode")]
    [MaxLength(50)]
    public string Mode { get; set; } = string.Empty;

    // Provider-specific config stored as JSON (boardToken, apiKey, clientId, etc.)
    // Sensitive values should be encrypted at the application layer before storage.
    [Column("ConnectionConfigJson")]
    public string? ConnectionConfigJson { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("UpdatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey(nameof(EmployerId))]
    public virtual Employer Employer { get; set; } = null!;
}
