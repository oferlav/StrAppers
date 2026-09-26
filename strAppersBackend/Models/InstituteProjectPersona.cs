namespace strAppersBackend.Models;

/// <summary>
/// A side persona on an institute project (for example an "IT Manager" next to the main "Consumer").
/// Side personas support the students but are not assessed: their chats go to <see cref="PersonaChatHistory"/>,
/// never to <see cref="CustomerChatHistory"/>, so metrics and mentor reviews only see the main persona.
/// The chat prompt is <see cref="Persona.Prompt"/> (generic behavior) plus <see cref="ContextText"/> (project knowledge).
/// </summary>
public class InstituteProjectPersona
{
    public int Id { get; set; }

    public int InstituteProjectId { get; set; }
    public InstituteProject InstituteProject { get; set; } = null!;

    public int PersonaId { get; set; }
    public Persona Persona { get; set; } = null!;

    /// <summary>Project-specific knowledge of this persona (for the IT Manager: the Integration Sheet).</summary>
    public string? ContextText { get; set; }

    /// <summary>Tab order in the chat sidebar, after the main persona.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
