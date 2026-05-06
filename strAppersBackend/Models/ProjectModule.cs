using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models
{
    /// <summary>
    /// Represents a project module (e.g., User Authentication, Payment System, etc.)
    /// </summary>
    public class ProjectModule : IProjectModuleRow
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("ProjectId")]
        public int? ProjectId { get; set; }

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
        /// When this row was created by copying another project, the source <see cref="Id"/> from that copy.
        /// Used to rewrite TrelloBoardJson module references to the new Ids. Null for modules created normally.
        /// </summary>
        [Column("OriginalModuleId")]
        public int? OriginalModuleId { get; set; }

        // Navigation properties
        public virtual Project? Project { get; set; }

        public virtual ModuleType? ModuleTypeNavigation { get; set; }
    }

    /// <summary>
    /// Request model for creating a project module
    /// </summary>
    public class CreateProjectModuleRequest
    {
        public int? ProjectId { get; set; }

        public int? InstituteProjectId { get; set; }
        public int ModuleType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? Sequence { get; set; }
    }

    /// <summary>
    /// Request model for updating a project module
    /// </summary>
    public class UpdateProjectModuleRequest
    {
        public int? ProjectId { get; set; }

        public int? InstituteProjectId { get; set; }
        public int? ModuleType { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int? Sequence { get; set; }
    }
}
