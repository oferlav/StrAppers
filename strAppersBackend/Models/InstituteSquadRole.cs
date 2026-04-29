using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

/// <summary>
/// Role snapshot/override inside a specific squad.
/// Keeps full role properties so squad can diverge from base institute roles.
/// </summary>
public class InstituteSquadRole
{
    public int Id { get; set; }

    public int SquadId { get; set; }

    public InstituteSquad Squad { get; set; } = null!;

    /// <summary>
    /// Optional traceability to the base institute role used as source.
    /// </summary>
    public int? BaseInstituteRoleId { get; set; }

    public InstituteRole? BaseInstituteRole { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public string? Competencies { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    public int Type { get; set; } = 0;

    public virtual RoleType? RoleType { get; set; }

    public int? SkillId { get; set; }

    public virtual Skill? Skill { get; set; }

    public bool CustomerEngagement { get; set; } = false;

    public bool IsTechnical { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
