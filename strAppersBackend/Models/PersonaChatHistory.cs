using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Chat history with side personas (<see cref="InstituteProjectPersona"/>). Same shape as
/// <see cref="CustomerChatHistory"/> plus <see cref="PersonaId"/>; kept in its own table so the
/// main-persona chat, and every metric that reads it, stays untouched. No FK to Students, as in CustomerChatHistory.
/// </summary>
public class PersonaChatHistory
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    /// <summary>SprintNumber, as in CustomerChatHistory.</summary>
    public int SprintId { get; set; }

    public int PersonaId { get; set; }
    public Persona Persona { get; set; } = null!;

    [MaxLength(50)]
    public string Role { get; set; } = "user";

    [Column(TypeName = "text")]
    public string Message { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? AIModelName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
