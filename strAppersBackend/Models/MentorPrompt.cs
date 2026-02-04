using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Mentor system prompt fragments. Scoped by role (null = all roles) and category.
/// </summary>
[Table("MentorPrompt")]
public class MentorPrompt
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    /// <summary>
    /// FK to Roles. When null, this fragment applies to ALL user roles when building the Mentor prompt.
    /// </summary>
    [Column("RoleId")]
    public int? RoleId { get; set; }

    [Required]
    [Column("CategoryId")]
    public int CategoryId { get; set; }

    [Required]
    [Column("PromptString", TypeName = "text")]
    public string PromptString { get; set; } = string.Empty;

    [Column("SortOrder")]
    public int SortOrder { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("UpdatedAt")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(RoleId))]
    public Role? Role { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public PromptCategory Category { get; set; } = null!;
}
