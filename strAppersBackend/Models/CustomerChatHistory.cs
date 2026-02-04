using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Stores chat history for customer conversations.
/// Same structure as MentorChatHistory; StudentId is the student Id, SprintId is SprintNumber (no FK to Students).
/// </summary>
public class CustomerChatHistory
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Required]
    [Column("StudentId")]
    public int StudentId { get; set; } // Student Id (who is chatting with the AI Customer)

    [Required]
    [Column("SprintId")]
    public int SprintId { get; set; } // SprintNumber for customer context

    [Required]
    [MaxLength(50)]
    [Column("Role")]
    public string Role { get; set; } = "user"; // "user" or "assistant"

    [Required]
    [Column("Message", TypeName = "text")]
    public string Message { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("AIModelName")]
    public string? AIModelName { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
