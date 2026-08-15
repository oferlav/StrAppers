using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

public class Institute
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    [MaxLength(255)]
    public string? ContactEmail { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(50)]
    public string? Type { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    // Additional location fields requested for institutes
    [MaxLength(100)]
    public string? State { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    public bool IsActive { get; set; } = true;

    [Column("Logo")]
    public string? Logo { get; set; }

    /// <summary>
    /// Hero headline shown to this institute's students on Choose Your Squad, replacing the default
    /// marketing copy. Applies to InstituteId &gt; 1 only; null falls back to the default copy.
    /// Word limit enforced server-side (Institutes:HeadlineFields:PrimaryWords).
    /// </summary>
    [Column("PrimaryHeadline")]
    [MaxLength(200)]
    public string? PrimaryHeadline { get; set; }

    /// <summary>
    /// Sub-headline shown under <see cref="PrimaryHeadline"/>. Same scoping and fallback rules.
    /// Word limit enforced server-side (Institutes:HeadlineFields:SecondaryWords).
    /// </summary>
    [Column("SecondaryHeadline")]
    [MaxLength(400)]
    public string? SecondaryHeadline { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public string? TermsUse { get; set; }
    public bool TermsAccepted { get; set; } = false;
    public DateTimeOffset? TermsAcceptedAt { get; set; }

    [MaxLength(256)]
    public string? PasswordHash { get; set; }

    [MaxLength(100)]
    public string? Coupon { get; set; }

    public bool QuestMode { get; set; } = false;

    /// <summary>Only meaningful when QuestMode=true. When true, board creation kicks off for each eligible student individually (no team required).</summary>
    public bool SingleQuest { get; set; } = true;

    /// <summary>
    /// Institute-selected model for the generic Data Assessment Engine (use/assess). Null falls back
    /// to the OpenAI:CheapModel config default. See MetricsController.ResolveAssessmentEngineModelAsync.
    /// </summary>
    public int? AssessmentEngineAIModelId { get; set; }
    public AIModel? AssessmentEngineAIModel { get; set; }

    /// <summary>
    /// Institute-selected <see cref="Persona"/> for the student-facing AI chat. Its
    /// <see cref="Persona.Prompt"/> becomes the chatbot's system prompt and its
    /// <see cref="Persona.Name"/> labels the chat everywhere in the UI. Null keeps the configured
    /// <c>PromptConfig:Customer:SystemPrompt</c> and the default "Customer" wording.
    /// See CustomerController.ResolveCustomerSystemPrompt.
    /// </summary>
    public int? MainAIPersonaId { get; set; }
    public Persona? MainAIPersona { get; set; }

    public ICollection<Student> Students { get; set; } = new List<Student>();

    public ICollection<Teacher> Teachers { get; set; } = new List<Teacher>();

    public ICollection<Project> Projects { get; set; } = new List<Project>();

    public ICollection<InstituteTemplate> InstituteTemplates { get; set; } = new List<InstituteTemplate>();

    public ICollection<InstituteRole> InstituteRoles { get; set; } = new List<InstituteRole>();

    public ICollection<InstituteSquad> InstituteSquads { get; set; } = new List<InstituteSquad>();

    public ICollection<InstituteProject> InstituteProjects { get; set; } = new List<InstituteProject>();

    public ICollection<ProjectBoard> ProjectBoards { get; set; } = new List<ProjectBoard>();
}
