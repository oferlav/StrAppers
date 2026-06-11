using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Per-candidate infrastructure for a QuestMode board.
/// One row per student per board — mirrors the squad-level fields in ProjectBoards
/// but scoped to an individual candidate.
/// </summary>
public class QuestBoard
{
    public int Id { get; set; }

    /// <summary>FK → Students.Id</summary>
    [Required]
    public int StudentId { get; set; }

    /// <summary>FK → ProjectBoards.BoardId (Trello board ID of the squad)</summary>
    [Required]
    [MaxLength(50)]
    public string BoardId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? PublishUrl { get; set; }

    [MaxLength(1000)]
    public string? GithubFrontendUrl { get; set; }

    [MaxLength(1000)]
    public string? GithubBackendUrl { get; set; }

    [MaxLength(1000)]
    public string? WebApiUrl { get; set; }

    [MaxLength(200)]
    public string? DBPassword { get; set; }

    [MaxLength(100)]
    public string? NeonBranchId { get; set; }

    [MaxLength(100)]
    public string? NeonProjectId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Student? Student { get; set; }
    public ProjectBoard? ProjectBoard { get; set; }
}
