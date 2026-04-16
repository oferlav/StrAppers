namespace strAppersBackend.Models;

/// <summary>Trello sprint role card content used for Gap Analysis context (description + checklists).</summary>
public class SprintRoleCardSnapshot
{
    public string TrelloCardId { get; set; } = string.Empty;
    public string? CardName { get; set; }
    public string? Description { get; set; }
    /// <summary>Human-readable checklist dump for LLM context.</summary>
    public string ChecklistsText { get; set; } = string.Empty;
}
