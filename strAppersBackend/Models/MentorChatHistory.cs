using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Stores chat history for mentor conversations
/// </summary>
public class MentorChatHistory
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Required]
    [Column("StudentId")]
    public int StudentId { get; set; }

    [Required]
    [Column("SprintId")]
    public int SprintId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("Role")]
    public string Role { get; set; } = "user"; // "user" or "assistant"

    [Required]
    [Column("Message", TypeName = "text")]
    public string Message { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("AIModelName")]
    public string? AIModelName { get; set; } // Which AI model was used for assistant messages

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Student? Student { get; set; }
}

