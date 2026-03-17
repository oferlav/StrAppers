using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Support request / ticket log.
/// </summary>
public class Support
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("Email")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("Description")]
    public string? Description { get; set; }

    [Column("Priority")]
    public int Priority { get; set; } = 3;
}
