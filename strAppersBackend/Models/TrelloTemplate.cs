using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>POST body for <c>/api/Projects/use/add-template</c>.</summary>
public class AddTrelloTemplateRequest
{
    [Required]
    public string TrelloBoardJson { get; set; } = string.Empty;
}

/// <summary>
/// Institute-scoped copy of a project Trello board template JSON (Task Builder saves).
/// </summary>
public class TrelloTemplate
{
    public int Id { get; set; }

    public int InstituteId { get; set; }

    public Institute? Institute { get; set; }

    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    [Column(TypeName = "TEXT")]
    public string TrelloBoardJson { get; set; } = string.Empty;
}
