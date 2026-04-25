using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

/// <summary>
/// Lookup catalog for role skill grouping.
/// </summary>
public class Skill
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public ICollection<Role> Roles { get; set; } = new List<Role>();

    public ICollection<InstituteRole> InstituteRoles { get; set; } = new List<InstituteRole>();
}

