using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

/// <summary>
/// Institute-level squad definition that can be linked by templates.
/// </summary>
public class InstituteSquad
{
    public int Id { get; set; }

    public int InstituteId { get; set; }

    public Institute Institute { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When true, the Task Builder / student experience enforces the developer (bundle) role rule for this template's squad.
    /// </summary>
    public bool RequireDeveloperRule { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<InstituteSquadRole> Roles { get; set; } = new List<InstituteSquadRole>();

    public ICollection<InstituteTemplate> InstituteTemplates { get; set; } = new List<InstituteTemplate>();
}
