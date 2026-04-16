using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>Catalog of mentor metric types (adherence, gap analysis, etc.).</summary>
public class Metric
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional API route key; seeded empty until wired.</summary>
    [MaxLength(100)]
    public string? Endpoint { get; set; }
}
