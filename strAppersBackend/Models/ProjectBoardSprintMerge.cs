using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models
{
    /// <summary>
    /// Tracks merge state per sprint for a project board (e.g. when a sprint was merged with SystemBoard).
    /// One row per (board, sprint number). DueDate stores the sprint's due date.
    /// </summary>
    public class ProjectBoardSprintMerge
    {
        /// <summary>
        /// Foreign key to ProjectBoards (Trello board ID).
        /// </summary>
        [MaxLength(50)]
        public string ProjectBoardId { get; set; } = string.Empty;

        /// <summary>
        /// Sprint number (1, 2, 3, ...).
        /// </summary>
        public int SprintNumber { get; set; }

        /// <summary>
        /// When the sprint was last merged (overwrite or AI merge).
        /// </summary>
        [Column("MergedAt")]
        public DateTime? MergedAt { get; set; }

        /// <summary>
        /// Trello list ID for this sprint (optional).
        /// </summary>
        [Column("ListId")]
        [MaxLength(50)]
        public string? ListId { get; set; }

        /// <summary>
        /// Due date of the sprint.
        /// </summary>
        [Column("DueDate")]
        public DateTime? DueDate { get; set; }

        // Navigation
        [ForeignKey(nameof(ProjectBoardId))]
        public virtual ProjectBoard? ProjectBoard { get; set; }
    }
}
