using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Represents an instance of a project (e.g. multiple cohorts/runs of the same project).
/// </summary>
public class ProjectInstance
{
    public int Id { get; set; }

    /// <summary>
    /// FK to Projects.Id
    /// </summary>
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Instance number (unique across the table for FK from Students).
    /// </summary>
    public int InstanceId { get; set; }
}
