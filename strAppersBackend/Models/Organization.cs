using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

public class Organization
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [MaxLength(200)]
    public string? Website { get; set; }
    
    [MaxLength(255)]
    public string? ContactEmail { get; set; }
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    [MaxLength(50)]
    public string? Type { get; set; }
    
    [MaxLength(200)]
    public string? Address { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Logo field (base64 encoded image or URL)
    [Column("Logo")]
    public string? Logo { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public string? TermsUse { get; set; }
    public bool TermsAccepted { get; set; } = false;
    public DateTimeOffset? TermsAcceptedAt { get; set; }
    
    // Password hash field
    [MaxLength(256)]
    public string? PasswordHash { get; set; }
    
    // Navigation properties
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}

public class CreateOrganizationRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [MaxLength(200)]
    public string? Website { get; set; }
    
    [MaxLength(255)]
    public string? ContactEmail { get; set; }
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    [MaxLength(50)]
    public string? Type { get; set; }
    
    [MaxLength(200)]
    public string? Address { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public string? Logo { get; set; }  // Base64 encoded image or URL

    public string? TermsUse { get; set; }
    public bool? TermsAccepted { get; set; }
    public DateTimeOffset? TermsAcceptedAt { get; set; }
    
    [MaxLength(100)]
    public string? Password { get; set; }  // Plain password (will be hashed before storing)
}

public class UpdateOrganizationRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [MaxLength(200)]
    public string? Website { get; set; }
    
    [MaxLength(255)]
    public string? ContactEmail { get; set; }
    
    [MaxLength(200)]
    public string? Address { get; set; }
    
    public string? Logo { get; set; }  // Base64 encoded image or URL

    public string? TermsUse { get; set; }
    public bool? TermsAccepted { get; set; }
    public DateTimeOffset? TermsAcceptedAt { get; set; }
}
