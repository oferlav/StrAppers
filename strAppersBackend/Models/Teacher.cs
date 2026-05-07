using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Institute staff user (login); belongs to one <see cref="Institute"/>.
/// </summary>
public class Teacher
{
    public int Id { get; set; }

    [Required]
    public int InstituteId { get; set; }

    public Institute Institute { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    [Column("FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("LastName")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? PasswordHash { get; set; }

    [Column("IsAdmin")]
    public bool IsAdmin { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
