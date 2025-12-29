using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

/// <summary>
/// Represents an AI model available in the system (only models with API keys)
/// </summary>
public class AIModel
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("Name")]
    public string Name { get; set; } = string.Empty; // e.g., "gpt-4o-mini", "claude-sonnet-4-5-20250929"

    [MaxLength(50)]
    [Column("Provider")]
    public string Provider { get; set; } = string.Empty; // e.g., "OpenAI", "Anthropic"

    [MaxLength(500)]
    [Column("BaseUrl")]
    public string? BaseUrl { get; set; } // e.g., "https://api.openai.com/v1", "https://api.anthropic.com/v1"

    [MaxLength(50)]
    [Column("ApiVersion")]
    public string? ApiVersion { get; set; } // e.g., "2023-06-01" for Anthropic

    [Column("MaxTokens")]
    public int? MaxTokens { get; set; } // Default max tokens for this model

    [Column("DefaultTemperature")]
    public double? DefaultTemperature { get; set; } // Default temperature for this model

    [MaxLength(1000)]
    [Column("Description")]
    public string? Description { get; set; } // Description of the model

    [Column("IsActive")]
    public bool IsActive { get; set; } = true; // Whether this model is currently available

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("UpdatedAt")]
    public DateTime? UpdatedAt { get; set; }
}

