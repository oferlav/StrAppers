using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Stores marketing images for the company website
/// </summary>
public class MarketingImages
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Required]
    [Column("Base64", TypeName = "text")]
    public string Base64 { get; set; } = string.Empty;

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
