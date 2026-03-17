using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// A resource link (Figma or other) associated with a board and optionally a student.
/// </summary>
public class Resource
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("BoardId")]
    public string BoardId { get; set; } = string.Empty;

    [Required]
    [Column("StudentId")]
    public int StudentId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    [Column("Url")]
    public string Url { get; set; } = string.Empty;

    [Column("IsFigma")]
    public bool IsFigma { get; set; } = false;

    [ForeignKey(nameof(BoardId))]
    public virtual ProjectBoard? ProjectBoard { get; set; }

    [ForeignKey(nameof(StudentId))]
    public virtual Student? Student { get; set; }
}
