using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models
{
    /// <summary>
    /// Represents a meeting associated with a project board
    /// </summary>
    public class BoardMeeting
    {
        /// <summary>
        /// Primary key
        /// </summary>
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to ProjectBoards table (BoardId)
        /// </summary>
        [Required]
        [MaxLength(50)]
        [Column("BoardId")]
        public string BoardId { get; set; } = string.Empty;

        /// <summary>
        /// Meeting time
        /// </summary>
        [Required]
        [Column("MeetingTime")]
        public DateTime MeetingTime { get; set; }

        /// <summary>
        /// Student email address for this meeting invitation
        /// </summary>
        [MaxLength(255)]
        [Column("StudentEmail")]
        public string? StudentEmail { get; set; }

        /// <summary>
        /// Custom redirect URL for tracking individual student access
        /// </summary>
        [Column("CustomMeetingUrl", TypeName = "TEXT")]
        public string? CustomMeetingUrl { get; set; }

        /// <summary>
        /// Actual Teams meeting URL (the real Microsoft Teams join link)
        /// </summary>
        [Column("ActualMeetingUrl", TypeName = "TEXT")]
        public string? ActualMeetingUrl { get; set; }

        /// <summary>
        /// Whether the student has attended the meeting (default: false)
        /// </summary>
        [Column("Attended")]
        public bool Attended { get; set; } = false;

        /// <summary>
        /// Timestamp when the student joined the meeting via their custom URL
        /// </summary>
        [Column("JoinTime", TypeName = "timestamp with time zone")]
        public DateTime? JoinTime { get; set; }

        /// <summary>
        /// Teams transcript ID (from Graph API) for the meeting this row belongs to
        /// </summary>
        [Column("TranscriptId", TypeName = "TEXT")]
        public string? TranscriptId { get; set; }

        /// <summary>
        /// When the transcript was fetched from Graph API and stored
        /// </summary>
        [Column("TranscriptFetchedAt", TypeName = "timestamp with time zone")]
        public DateTime? TranscriptFetchedAt { get; set; }

        /// <summary>
        /// Raw VTT transcript content for the meeting (shared across all students in same meeting)
        /// </summary>
        [Column("TranscriptVtt", TypeName = "TEXT")]
        public string? TranscriptVtt { get; set; }

        /// <summary>
        /// The exact speaker name used by this student in the VTT transcript (resolved via attendance report).
        /// Stored per-student so analysis never needs fuzzy matching.
        /// </summary>
        [MaxLength(255)]
        [Column("SpeakerName")]
        public string? SpeakerName { get; set; }

        // Navigation properties
        /// <summary>
        /// Navigation property to the associated project board
        /// </summary>
        [ForeignKey(nameof(BoardId))]
        public virtual ProjectBoard ProjectBoard { get; set; } = null!;
    }
}

