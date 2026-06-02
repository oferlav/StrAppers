using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

public class Role
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;
    
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
    /// InstituteId = 1 → global/B2C role. InstituteId = X → institute-specific role.
    /// </summary>
    public int InstituteId { get; set; } = 1;

    public Institute? Institute { get; set; }

    /// <summary>
    /// When set, this role is scoped to a specific squad (replaces InstituteSquadRole).
    /// Null = base institute role (or global when InstituteId = 1).
    /// </summary>
    public int? SquadId { get; set; }

    public InstituteSquad? Squad { get; set; }

    public bool IsTechnical { get; set; } = false;

    public string? Competencies { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<StudentRole> StudentRoles { get; set; } = new List<StudentRole>();
}

public class StudentRole
{
    public int Id { get; set; }
    
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
    
    public bool IsActive { get; set; } = true;
}
