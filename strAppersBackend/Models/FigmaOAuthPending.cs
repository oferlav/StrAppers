using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models
{
    /// <summary>
    /// Figma OAuth tokens captured during student registration (before a board exists).
    /// Merged into <see cref="Figma"/> when the student gets a boardId.
    /// </summary>
    [Table("FigmaOAuthPending")]
    public class FigmaOAuthPending
    {
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("Email")]
        public string Email { get; set; } = string.Empty;

        [MaxLength(512)]
        [Column("FigmaAccessToken")]
        public string? FigmaAccessToken { get; set; }

        [MaxLength(512)]
        [Column("FigmaRefreshToken")]
        public string? FigmaRefreshToken { get; set; }

        [Column("FigmaTokenExpiry")]
        public DateTime? FigmaTokenExpiry { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
