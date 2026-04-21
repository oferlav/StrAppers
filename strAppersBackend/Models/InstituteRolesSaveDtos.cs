using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

/// <summary>
/// POST /api/Roles/use/institute — replace institute-scoped role rows with upserts.
/// </summary>
public class SaveInstituteRolesRequest
{
    [Required]
    public int InstituteId { get; set; }

    /// <summary>Optional UI flag (session); not written to DB in this endpoint.</summary>
    public bool? RequireDeveloperRule { get; set; }

    /// <summary>Full set of institute role rows to keep (desired + available). Omitted existing Ids are removed.</summary>
    [Required]
    public List<InstituteRoleSaveDto> Roles { get; set; } = new();
}

public class InstituteRoleSaveDto
{
    /// <summary><see cref="InstituteRole.Id"/> when updating; omit, null, or ≤ 0 for new institute-created roles.</summary>
    public int? Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    /// <summary>Optional FK to InstituteTemplates.Id for this institute role.</summary>
    public int? TemplateId { get; set; }

    /// <summary>FK to RoleTypes.Id.</summary>
    public int Type { get; set; }

    public bool IsActive { get; set; } = true;
}
