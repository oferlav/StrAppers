using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>Cached metric review text and optional graph (base64) per board, student, sprint, and metric.</summary>
public class CacheMetrics
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

    [Column("MetricId")]
    public int MetricId { get; set; }

    [Column("ReviewContent", TypeName = "text")]
    public string ReviewContent { get; set; } = string.Empty;

    /// <summary>Optional chart or image payload (base64).</summary>
    [Column(TypeName = "text")]
    public string? Graph { get; set; }

    /// <summary>Second chart (e.g. Full Stack frontend track) when <see cref="Graph"/> holds the backend chart.</summary>
    [Column(TypeName = "text")]
    public string? Graph2 { get; set; }

    [ForeignKey(nameof(BoardId))]
    public virtual ProjectBoard? ProjectBoard { get; set; }

    [ForeignKey(nameof(StudentId))]
    public virtual Student? Student { get; set; }

    [ForeignKey(nameof(MetricId))]
    public virtual Metric? Metric { get; set; }
}
