using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

/// <summary>
/// Lookup for <see cref="Role.Type"/> (FK). Ids 0–4 are seeded.
/// </summary>
public class RoleType
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public ICollection<Role> Roles { get; set; } = new List<Role>();

    public ICollection<InstituteRole> InstituteRoles { get; set; } = new List<InstituteRole>();
}
