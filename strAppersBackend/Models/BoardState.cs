using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models
{
    /// <summary>
    /// Represents the state of a board from various sources (GitHub, Railway)
    /// </summary>
    public class BoardState
    {
        /// <summary>
        /// Primary key
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to ProjectBoards table (BoardId)
        /// </summary>
        [Required]
        [MaxLength(50)]
        [Column("BoardId")]
        public string BoardId { get; set; } = string.Empty;

        /// <summary>
        /// Source of the state information (Github, Railway)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if this state was set by a webhook (true) or API call (false)
        /// </summary>
        [Column("Webhook")]
        public bool? Webhook { get; set; }

        // Railway-specific fields (nullable)
        /// <summary>
        /// Railway service name (e.g., webapi-{boardId})
        /// </summary>
        [MaxLength(255)]
        public string? ServiceName { get; set; }

        /// <summary>
        /// Error message from Railway service
        /// </summary>
        [Column(TypeName = "text")]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// File where error occurred
        /// </summary>
        [MaxLength(500)]
        public string? File { get; set; }

        /// <summary>
        /// Line number where error occurred
        /// </summary>
        public int? Line { get; set; }

        /// <summary>
        /// Stack trace of the error
        /// </summary>
        [Column(TypeName = "text")]
        public string? StackTrace { get; set; }

        /// <summary>
        /// Request URL when error occurred
        /// </summary>
        [MaxLength(500)]
        public string? RequestUrl { get; set; }

        /// <summary>
        /// HTTP request method when error occurred
        /// </summary>
        [MaxLength(10)]
        public string? RequestMethod { get; set; }

        /// <summary>
        /// Timestamp when error occurred
        /// </summary>
        [Column(TypeName = "timestamp with time zone")]
        public DateTime? Timestamp { get; set; }

        /// <summary>
        /// Last build status (SUCCESS, FAILED)
        /// </summary>
        [MaxLength(50)]
        public string? LastBuildStatus { get; set; }

        /// <summary>
        /// Last build output/logs
        /// </summary>
        [Column(TypeName = "text")]
        public string? LastBuildOutput { get; set; }

        /// <summary>
        /// Latest error summary (LastBuildOutput + AI analysis)
        /// </summary>
        [Column(TypeName = "text")]
        public string? LatestErrorSummary { get; set; }

        // GitHub-specific fields (nullable)
        /// <summary>
        /// Sprint number (set by branch action)
        /// </summary>
        public int? SprintNumber { get; set; }

        /// <summary>
        /// Branch name (set by branch action)
        /// </summary>
        [MaxLength(255)]
        public string? BranchName { get; set; }

        /// <summary>
        /// Branch URL (set by branch action)
        /// </summary>
        [MaxLength(500)]
        public string? BranchUrl { get; set; }

        /// <summary>
        /// GitHub branch name (for validations, webhooks, runtime errors, etc.)
        /// </summary>
        [MaxLength(255)]
        [Column("GithubBranch")]
        public string? GithubBranch { get; set; }

        /// <summary>
        /// Latest commit ID (set by webhook or API)
        /// </summary>
        [MaxLength(100)]
        public string? LatestCommitId { get; set; }

        /// <summary>
        /// Latest commit description/message (set by webhook or API)
        /// </summary>
        [Column(TypeName = "text")]
        public string? LatestCommitDescription { get; set; }

        /// <summary>
        /// Latest commit date (set by webhook or API)
        /// </summary>
        [Column(TypeName = "timestamp with time zone")]
        public DateTime? LatestCommitDate { get; set; }

        /// <summary>
        /// Last merge date (set by webhook or API)
        /// </summary>
        [Column(TypeName = "timestamp with time zone")]
        public DateTime? LastMergeDate { get; set; }

        /// <summary>
        /// Latest event (e.g., PUSH_REJECTED_MAIN) (set by webhook or API)
        /// </summary>
        [MaxLength(100)]
        public string? LatestEvent { get; set; }

        /// <summary>
        /// Pull request status (None, Requested, Approved)
        /// </summary>
        [MaxLength(50)]
        public string? PRStatus { get; set; }

        /// <summary>
        /// Branch status (Active, Merged)
        /// </summary>
        [MaxLength(50)]
        public string? BranchStatus { get; set; }

        /// <summary>
        /// Record creation timestamp
        /// </summary>
        [Required]
        [Column(TypeName = "timestamp with time zone")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Record update timestamp
        /// </summary>
        [Required]
        [Column(TypeName = "timestamp with time zone")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        /// <summary>
        /// Navigation property to the associated project board
        /// </summary>
        [ForeignKey(nameof(BoardId))]
        public virtual ProjectBoard? ProjectBoard { get; set; }
    }
}
