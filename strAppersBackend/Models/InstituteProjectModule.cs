using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Module row for an <see cref="InstituteProject"/> design (mirrors <see cref="ProjectModule"/> columns, without catalog <see cref="Project"/> FK).
/// </summary>
public class InstituteProjectModule : IProjectModuleRow
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Column("InstituteProjectId")]
    public int InstituteProjectId { get; set; }

    public InstituteProject InstituteProject { get; set; } = null!;

    [Column("ModuleType")]
    public int? ModuleType { get; set; }

    [MaxLength(100)]
    [Column("Title")]
    public string? Title { get; set; }

    [Column("Description")]
    public string? Description { get; set; }

    [Column("Sequence")]
    public int? Sequence { get; set; }

    /// <summary>
    /// When created by copying, the source module <see cref="Id"/> (from <see cref="ProjectModule"/> or <see cref="InstituteProjectModule"/>).
    /// </summary>
    [Column("OriginalModuleId")]
    public int? OriginalModuleId { get; set; }

    public virtual ModuleType? ModuleTypeNavigation { get; set; }
}
