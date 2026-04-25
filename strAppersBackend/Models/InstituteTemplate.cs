using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>POST body for <c>/api/Projects/use/add-template</c>.</summary>
public class AddInstituteTemplateRequest
{
    [Required]
    public string TrelloBoardJson { get; set; } = string.Empty;

    /// <summary>Required for new rows; for updates, send the template display name (or omit to keep the existing name).</summary>
    [MaxLength(100)]
    public string? Name { get; set; }

    /// <summary>When set, updates this <c>InstituteTemplates</c> row (must belong to query project + institute). When omitted, inserts a new row.</summary>
    public int? InstituteTemplateId { get; set; }
}

/// <summary>
/// Institute-scoped copy of a project Trello board template JSON (Task Builder saves). Table: <c>InstituteTemplates</c>.
/// </summary>
public class InstituteTemplate
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int InstituteId { get; set; }

    public Institute? Institute { get; set; }

    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    public ICollection<InstituteRole> InstituteRoles { get; set; } = new List<InstituteRole>();

    [Column(TypeName = "TEXT")]
    public string TrelloBoardJson { get; set; } = string.Empty;

    public bool IsActive { get; set; } = false;
}
