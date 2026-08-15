using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// A student-facing AI conversation persona (the "AI Customer", an "AI Professor", …).
///
/// <see cref="Prompt"/> is the full system prompt for the persona's chatbot, replacing the single
/// global <c>PromptConfig:Customer:SystemPrompt</c> that used to hard-code the customer persona for
/// every institute. <see cref="Name"/> is what the UI labels the chat tab and its surrounding copy
/// with, so the wording follows the persona instead of saying "Customer" to a course whose persona
/// is a professor.
///
/// Selected per institute via <see cref="Institute.MainAIPersonaId"/>; institutes that select none
/// (and B2C students, who have no institute) keep the configured prompt and the "Customer" label.
/// </summary>
public class Persona
{
    public int Id { get; set; }

    /// <summary>Display name used for every user-facing label of this persona's chat (e.g. "Customer", "Professor").</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The persona's full system prompt. Supports the same placeholders the configured customer
    /// prompt does — <c>[INSERT PROJECT DESCRIPTION HERE]</c> and
    /// <c>[INSERT PROJECT-SPECIFIC DESIGN/LOGIC DATA HERE]</c> — and gets the same appended project
    /// context when it omits them.
    /// </summary>
    [Column(TypeName = "text")]
    public string? Prompt { get; set; }
}
