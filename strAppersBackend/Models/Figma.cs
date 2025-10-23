using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models
{
    /// <summary>
    /// Represents Figma integration data for a project board
    /// </summary>
    [Table("Figma")]
    public class Figma
    {
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("BoardId")]
        public string BoardId { get; set; } = string.Empty;

        [MaxLength(512)]
        [Column("FigmaAccessToken")]
        public string? FigmaAccessToken { get; set; }

        [MaxLength(512)]
        [Column("FigmaRefreshToken")]
        public string? FigmaRefreshToken { get; set; }

        [Column("FigmaTokenExpiry")]
        public DateTime? FigmaTokenExpiry { get; set; }

        [MaxLength(64)]
        [Column("FigmaUserId")]
        public string? FigmaUserId { get; set; }

        [MaxLength(1024)]
        [Column("FigmaFileUrl")]
        public string? FigmaFileUrl { get; set; }

        [MaxLength(64)]
        [Column("FigmaFileKey")]
        public string? FigmaFileKey { get; set; }

        [Column("FigmaLastSync")]
        public DateTime? FigmaLastSync { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("BoardId")]
        public virtual ProjectBoard? ProjectBoard { get; set; }
    }

    /// <summary>
    /// Request model for creating Figma integration
    /// </summary>
    public class CreateFigmaRequest
    {
        [Required]
        [MaxLength(50)]
        public string BoardId { get; set; } = string.Empty;

        [MaxLength(512)]
        public string? FigmaAccessToken { get; set; }

        [MaxLength(512)]
        public string? FigmaRefreshToken { get; set; }

        public DateTime? FigmaTokenExpiry { get; set; }

        [MaxLength(64)]
        public string? FigmaUserId { get; set; }

        [MaxLength(1024)]
        public string? FigmaFileUrl { get; set; }

        [MaxLength(64)]
        public string? FigmaFileKey { get; set; }

        public DateTime? FigmaLastSync { get; set; }
    }

    /// <summary>
    /// Request model for updating Figma integration
    /// </summary>
    public class UpdateFigmaRequest
    {
        [MaxLength(512)]
        public string? FigmaAccessToken { get; set; }

        [MaxLength(512)]
        public string? FigmaRefreshToken { get; set; }

        public DateTime? FigmaTokenExpiry { get; set; }

        [MaxLength(64)]
        public string? FigmaUserId { get; set; }

        [MaxLength(1024)]
        public string? FigmaFileUrl { get; set; }

        [MaxLength(64)]
        public string? FigmaFileKey { get; set; }

        public DateTime? FigmaLastSync { get; set; }
    }

    /// <summary>
    /// Response model for Figma integration
    /// </summary>
    public class FigmaResponse
    {
        public int Id { get; set; }
        public string BoardId { get; set; } = string.Empty;
        public string? FigmaAccessToken { get; set; }
        public string? FigmaRefreshToken { get; set; }
        public DateTime? FigmaTokenExpiry { get; set; }
        public string? FigmaUserId { get; set; }
        public string? FigmaFileUrl { get; set; }
        public string? FigmaFileKey { get; set; }
        public DateTime? FigmaLastSync { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}


