using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

public class Stakeholder
{
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    [ForeignKey(nameof(CategoryId))]
    public StakeholderCategory? Category { get; set; }

    public int StatusId { get; set; }
    [ForeignKey(nameof(StatusId))]
    public StakeholderStatus? Status { get; set; }

    public int V1AlignmentScore { get; set; }

    [Column(TypeName = "TEXT")]
    public string? Delta { get; set; }

    [MaxLength(50)]
    public string? BoardId { get; set; }
    [ForeignKey(nameof(BoardId))]
    public ProjectBoard? ProjectBoard { get; set; }
}
