using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

public class Organization
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [MaxLength(100)]
    public string? Website { get; set; }
    
    [MaxLength(255)]
    [EmailAddress]
    public string? ContactEmail { get; set; }
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    [MaxLength(200)]
    public string? Address { get; set; }
    
    [MaxLength(50)]
    public string? Type { get; set; } // University, Company, Non-profit, etc.
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}

public class CreateOrganizationRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [MaxLength(100)]
    public string? Website { get; set; }
    
    [MaxLength(255)]
    [EmailAddress]
    public string? ContactEmail { get; set; }
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    [MaxLength(200)]
    public string? Address { get; set; }
    
    [MaxLength(50)]
    public string? Type { get; set; }
    
    public bool IsActive { get; set; } = true;
}

public class UpdateOrganizationRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [MaxLength(100)]
    public string? Website { get; set; }
    
    [MaxLength(255)]
    [EmailAddress]
    public string? ContactEmail { get; set; }
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    [MaxLength(200)]
    public string? Address { get; set; }
    
    [MaxLength(50)]
    public string? Type { get; set; }
    
    public bool? IsActive { get; set; }
}

