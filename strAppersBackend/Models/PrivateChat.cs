using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Private chat between two participants (by email) on a board.
/// Email1 and Email2 are always stored in alphabetical order to ensure a single record per pair.
/// </summary>
public class PrivateChat
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    /// <summary>
    /// Board ID (FK to ProjectBoards.Id / BoardId).
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("BoardId")]
    public string BoardId { get; set; } = string.Empty;

    /// <summary>
    /// First participant email (alphabetically first). References Students.Email.
    /// </summary>
    [Required]
    [MaxLength(255)]
    [Column("Email1")]
    public string Email1 { get; set; } = string.Empty;

    /// <summary>
    /// Second participant email (alphabetically second). References Students.Email.
    /// </summary>
    [Required]
    [MaxLength(255)]
    [Column("Email2")]
    public string Email2 { get; set; } = string.Empty;

    [Required]
    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("ChatHistory", TypeName = "text")]
    public string? ChatHistory { get; set; }

    [ForeignKey(nameof(BoardId))]
    public virtual ProjectBoard? ProjectBoard { get; set; }
}
