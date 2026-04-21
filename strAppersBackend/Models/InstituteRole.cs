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

    [MaxLength(50)]
    public string? Category { get; set; }

    /// <summary>
    /// FK to <see cref="RoleType"/> (<see cref="RoleType.Id"/>).
    /// </summary>
    public int Type { get; set; } = 0;

    public virtual RoleType? RoleType { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
