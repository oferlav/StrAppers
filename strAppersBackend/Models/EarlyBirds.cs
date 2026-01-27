using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Stores early bird registrations for the company website
/// </summary>
public class EarlyBirds
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("Type")]
    public string Type { get; set; } = string.Empty; // "Junior" or "Employer"

    [Required]
    [MaxLength(200)]
    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("Email")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("OrgName")]
    public string? OrgName { get; set; }

    [MaxLength(200)]
    [Column("FutureRole")]
    public string? FutureRole { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
