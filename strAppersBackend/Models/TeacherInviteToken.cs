using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

/// <summary>
/// One-time token emailed to a new teacher so they can set their password and complete registration.
/// </summary>
public class TeacherInviteToken
{
    public int Id { get; set; }

    [Required]
    public int TeacherId { get; set; }

    public Teacher Teacher { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string Token { get; set; } = string.Empty;

    [Required]
    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
