using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

public class Student
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string? StudentId { get; set; } // University student ID (updated to 255 chars)
    
    [Required]
    public int MajorId { get; set; }
    public Major Major { get; set; } = null!;
    
    [Required]
    public int YearId { get; set; }
    public Year Year { get; set; } = null!;
    
    [Required]
    [MaxLength(200)]
    [Url]
    public string LinkedInUrl { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string GithubUser { get; set; } = string.Empty; // GitHub username (required)
    
    // Project allocation fields
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public bool IsAdmin { get; set; } = false;
    
    // Trello board relationship
    [MaxLength(50)]
    public string? BoardId { get; set; }
    public ProjectBoard? ProjectBoard { get; set; }
    
    // Availability status
    public bool IsAvailable { get; set; } = true;
    
    // Photo field (base64 encoded image or URL)
    [Column("Photo")]
    public string? Photo { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<StudentRole> StudentRoles { get; set; } = new List<StudentRole>();
}

public class CreateStudentRequest
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public int MajorId { get; set; }
    
    [Required]
    public int YearId { get; set; }
    
    [Required]
    [MaxLength(200)]
    [Url]
    public string LinkedInUrl { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string GithubUser { get; set; } = string.Empty; // GitHub username (required)
    
    [Required]
    public int RoleId { get; set; }
    
    public string? Photo { get; set; }  // Base64 encoded image or URL
}

public class UpdateStudentRequest
{
    [MaxLength(100)]
    public string? FirstName { get; set; }
    
    [MaxLength(100)]
    public string? LastName { get; set; }
    
    [MaxLength(255)]
    [EmailAddress]
    public string? Email { get; set; }
    
    public int? MajorId { get; set; }
    
    public int? YearId { get; set; }
    
    [MaxLength(200)]
    [Url]
    public string? LinkedInUrl { get; set; }
    
    [MaxLength(255)]
    public string? GithubUser { get; set; } // GitHub username
    
    public int? RoleId { get; set; }
    
    public string? Photo { get; set; }  // Base64 encoded image or URL
}
