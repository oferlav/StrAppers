using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models
{
    /// <summary>
    /// Represents a Trello board associated with a project
    /// </summary>
    public class ProjectBoard
    {
        /// <summary>
        /// Trello board ID (primary key)
        /// </summary>
        [Key]
        [Column("BoardId")]
        [MaxLength(50)]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Foreign key to Projects table
        /// </summary>
        [Required]
        [Column("ProjectId")]
        public int ProjectId { get; set; }

        /// <summary>
        /// Project start date
        /// </summary>
        [Column("StartDate")]
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Project end date
        /// </summary>
        [Column("EndDate")]
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Project due date
        /// </summary>
        [Column("DueDate")]
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// Record creation timestamp
        /// </summary>
        [Required]
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Record update timestamp
        /// </summary>
        [Required]
        [Column("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Project status ID - references ProjectStatuses table
        /// </summary>
        [Column("StatusId")]
        public int? StatusId { get; set; }

        /// <summary>
        /// Admin student ID - references Students table
        /// </summary>
        [Column("AdminId")]
        public int? AdminId { get; set; }

        /// <summary>
        /// Sprint plan JSON data
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string? SprintPlan { get; set; }

        /// <summary>
        /// Trello board URL
        /// </summary>
        [MaxLength(500)]
        [Column("BoardURL")]
        public string? BoardUrl { get; set; }

        // Navigation properties
        /// <summary>
        /// Navigation property to the associated project
        /// </summary>
        [ForeignKey(nameof(ProjectId))]
        public virtual Project Project { get; set; } = null!;

        /// <summary>
        /// Navigation property to the project status
        /// </summary>
        [ForeignKey(nameof(StatusId))]
        public virtual ProjectStatus? Status { get; set; }

        /// <summary>
        /// Navigation property to the admin student
        /// </summary>
        [ForeignKey(nameof(AdminId))]
        public virtual Student? Admin { get; set; }
    }
}
