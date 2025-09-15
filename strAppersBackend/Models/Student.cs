using System.ComponentModel.DataAnnotations;

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
    
    [MaxLength(20)]
    public string? StudentId { get; set; } // University student ID
    
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
    
    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    
    // Project allocation fields
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public bool IsAdmin { get; set; } = false;
    
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
    
    [MaxLength(20)]
    public string? StudentId { get; set; }
    
    [Required]
    public int MajorId { get; set; }
    
    [Required]
    public int YearId { get; set; }
    
    [Required]
    [MaxLength(200)]
    [Url]
    public string LinkedInUrl { get; set; } = string.Empty;
    
    public int? OrganizationId { get; set; }
    public int? ProjectId { get; set; }
    public bool IsAdmin { get; set; } = false;
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
    
    [MaxLength(20)]
    public string? StudentId { get; set; }
    
    public int? MajorId { get; set; }
    
    public int? YearId { get; set; }
    
    [MaxLength(200)]
    [Url]
    public string? LinkedInUrl { get; set; }
    
    public int? OrganizationId { get; set; }
    public int? ProjectId { get; set; }
    public bool? IsAdmin { get; set; }
}
