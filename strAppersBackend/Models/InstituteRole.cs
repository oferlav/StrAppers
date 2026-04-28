using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

/// <summary>
/// Institute-scoped role catalog row (same shape as <see cref="Role"/> plus <see cref="InstituteId"/>).
/// </summary>
public class InstituteRole
{
    public int Id { get; set; }

    public int InstituteId { get; set; }

    public Institute Institute { get; set; } = null!;

    /// <summary>
    /// Optional link to an institute-specific template.
    /// </summary>
    public int? TemplateId { get; set; }

    public InstituteTemplate? Template { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Free-text technical competencies expected from this role.
    /// </summary>
    public string? Competencies { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    /// <summary>
    /// FK to <see cref="RoleType"/> (<see cref="RoleType.Id"/>).
    /// </summary>
    public int Type { get; set; } = 0;

    public virtual RoleType? RoleType { get; set; }

    /// <summary>
    /// Optional FK to <see cref="Skill"/>.
    /// </summary>
    public int? SkillId { get; set; }

    public virtual Skill? Skill { get; set; }

    public bool CustomerEngagement { get; set; } = false;

    /// <summary>
    /// When true, this role follows the technical track: modules are assigned one sprint later than non-technical roles
    /// (Sprint 3-7 for implementation, Sprints 1-2 are foundation/setup with no module).
    /// When false, the role follows either the leadership track (Type=4: Sprints 2-6) or the customer-facing track (Type≠4: Sprints 2-5 + special Sprint 6).
    /// </summary>
    public bool IsTechnical { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
