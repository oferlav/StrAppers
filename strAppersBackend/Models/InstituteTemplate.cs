using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>POST body for <c>/api/Projects/use/by-institute/add-template</c>.</summary>
public class AddInstituteTemplateRequest
{
    /// <summary>Board JSON; may be empty when the course is created without a Trello template yet.</summary>
    public string TrelloBoardJson { get; set; } = string.Empty;

    /// <summary>Required for new rows; for updates, send the course/template display name (or omit to keep the existing).</summary>
    [MaxLength(100)]
    public string? CourseName { get; set; }

    /// <summary>Legacy JSON property; prefer <see cref="CourseName"/>.</summary>
    [MaxLength(100)]
    public string? Name { get; set; }

    /// <summary>When set, updates this <c>InstituteTemplates</c> row (must belong to query project + institute). When omitted, inserts a new row.</summary>
    public int? InstituteTemplateId { get; set; }

    /// <summary>Course structure type: "Squad" (default) or "Role".</summary>
    [MaxLength(20)]
    public string? CourseType { get; set; }

    /// <summary>For Role-type courses: number of parallel role-students per team (1–5).</summary>
    [Range(1, 5)]
    public int? RoleCount { get; set; }
}

public class DeleteInstituteTemplateRequest
{
    [Required]
    public int InstituteTemplateId { get; set; }

    [Required]
    public int ProjectId { get; set; }

    [Required]
    public int InstituteId { get; set; }

    /// <summary>When true, <see cref="ProjectId"/> is an <see cref="InstituteProject"/> id (not <see cref="Project"/>).</summary>
    public bool InstituteProject { get; set; }
}

/// <summary>
/// Institute-scoped copy of a project Trello board template JSON (Task Builder saves). Table: <c>InstituteTemplates</c>.
/// </summary>
public class InstituteTemplate
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string CourseName { get; set; } = string.Empty;

    public int InstituteId { get; set; }

    public Institute? Institute { get; set; }

    /// <summary>Built-in / legacy catalog link to <see cref="Project"/>. Null when <see cref="InstituteProjectId"/> is set.</summary>
    public int? ProjectId { get; set; }

    public Project? Project { get; set; }

    /// <summary>Institute project row (custom or activated built-in). Null when <see cref="ProjectId"/> is set.</summary>
    public int? InstituteProjectId { get; set; }

    public InstituteProject? InstituteProject { get; set; }

    /// <summary>
    /// Optional squad linked to this template.
    /// </summary>
    public int? SquadId { get; set; }

    public InstituteSquad? Squad { get; set; }

    public ICollection<InstituteRole> InstituteRoles { get; set; } = new List<InstituteRole>();

    [Column(TypeName = "TEXT")]
    public string TrelloBoardJson { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("BoardURL")]
    public string? BoardUrl { get; set; }

    public bool IsActive { get; set; } = false;

    /// <summary>
    /// Course structure type. "Squad" = multiple roles per sprint (default/existing behaviour).
    /// "Role" = single repeated role per student, differentiated by <see cref="RoleIndex"/> suffix on Trello card labels.
    /// </summary>
    [MaxLength(20)]
    public string CourseType { get; set; } = "Squad";

    /// <summary>
    /// Number of parallel role-students per team. Used only when <see cref="CourseType"/> = "Role".
    /// Drives label generation (RoleName1…RoleNameN) and module-count validation.
    /// </summary>
    public int? RoleCount { get; set; }

    /// <summary>
    /// UC3 flag: true when no Customer Engagement role exists in this course template.
    /// When set, boards created from this template will use module descriptions (DB) as the
    /// User Story data source in BoardRoom instead of the Trello User Stories list.
    /// </summary>
    public bool VisableModuleDesign { get; set; } = false;
}
