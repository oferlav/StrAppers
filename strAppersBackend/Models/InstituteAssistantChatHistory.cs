using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Chat turns for institute staff assistants (Project Designs, Task Builder, etc.): per teacher, project, and source. Stores user and assistant text only (not structured field payloads).
/// </summary>
public class InstituteAssistantChatHistory
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Required]
    public int InstituteId { get; set; }

    public Institute Institute { get; set; } = null!;

    [Required]
    public int TeacherId { get; set; }

    public Teacher Teacher { get; set; } = null!;

    /// <summary>Built-in / legacy <see cref="Project"/> id when the assistant is scoped to <c>Projects</c>.</summary>
    public int? ProjectId { get; set; }

    public Project? Project { get; set; }

    /// <summary><see cref="InstituteProject"/> id when the assistant is scoped to institute project designs.</summary>
    public int? InstituteProjectId { get; set; }

    public InstituteProject? InstituteProject { get; set; }

    /// <summary>General, Templates, Brief, Modules, Customer.</summary>
    [Required]
    [MaxLength(32)]
    public string Source { get; set; } = string.Empty;

    /// <summary>False = staff user message, true = assistant reply text (e.g. assistantMessage or aiReply only, not JSON field blobs).</summary>
    public bool IsAssistant { get; set; }

    [Column(TypeName = "text")]
    public string Message { get; set; } = string.Empty;

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
