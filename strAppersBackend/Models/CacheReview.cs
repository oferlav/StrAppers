using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>Kind of cached review row (resource, skill, or PR).</summary>
public enum CacheReviewType
{
    Resource = 0,
    Skill = 1,
    PR = 2
}

/// <summary>
/// Cached mentor review text per board, student, and sprint. <see cref="SequenceNumber"/> increments per
/// (BoardId, StudentId, SprintNumber) on insert (application responsibility).
/// </summary>
public class CacheReview
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

    [Column("SprintNumber")]
    public int SprintNumber { get; set; }

    /// <summary>1-based sequence within BoardId + StudentId + SprintNumber.</summary>
    [Column("SequenceNumber")]
    public int SequenceNumber { get; set; }

    [Required]
    [Column("Type")]
    public CacheReviewType Type { get; set; }

    [Required]
    [Column("ReviewContent", TypeName = "text")]
    public string ReviewContent { get; set; } = string.Empty;

    [ForeignKey(nameof(BoardId))]
    public virtual ProjectBoard? ProjectBoard { get; set; }

    [ForeignKey(nameof(StudentId))]
    public virtual Student? Student { get; set; }
}
