using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

public class Institute
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    [MaxLength(255)]
    public string? ContactEmail { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(50)]
    public string? Type { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    // Additional location fields requested for institutes
    [MaxLength(100)]
    public string? State { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    public bool IsActive { get; set; } = true;

    [Column("Logo")]
    public string? Logo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public string? TermsUse { get; set; }
    public bool TermsAccepted { get; set; } = false;
    public DateTimeOffset? TermsAcceptedAt { get; set; }

    [MaxLength(256)]
    public string? PasswordHash { get; set; }

    public ICollection<Student> Students { get; set; } = new List<Student>();

    public ICollection<Teacher> Teachers { get; set; } = new List<Teacher>();

    public ICollection<Project> Projects { get; set; } = new List<Project>();

    public ICollection<InstituteTemplate> InstituteTemplates { get; set; } = new List<InstituteTemplate>();

    public ICollection<InstituteRole> InstituteRoles { get; set; } = new List<InstituteRole>();
}
