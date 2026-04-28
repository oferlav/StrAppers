using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

public class CourseBoardBuildRequest
{
    [Required]
    public int ProjectId { get; set; }

    [Range(1, 30)]
    public int SprintLengthInDays { get; set; } = 7;

    [Required]
    public int InstituteRoleId { get; set; }

    [Required]
    public int TemplateId { get; set; }
}

public class CourseBoardBuildResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public TrelloProjectCreationRequest? BoardTemplate { get; set; }
}
