using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

public class ProjectCriteria
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// When false, criteria are excluded from GET /api/Projects/use/ProjectCriteria and from classification.
    /// </summary>
    public bool Active { get; set; } = false;
}








