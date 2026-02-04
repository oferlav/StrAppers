using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Categories for organizing system prompt fragments (e.g. Platform Context, General Instructions, Database Rules).
/// </summary>
[Table("PromptCategories")]
public class PromptCategory
{
    [Key]
    [Column("CategoryId")]
    public int CategoryId { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("Description")]
    public string? Description { get; set; }

    [Column("SortOrder")]
    public int SortOrder { get; set; }

    // Navigation property
    public ICollection<MentorPrompt> MentorPrompts { get; set; } = new List<MentorPrompt>();
}
