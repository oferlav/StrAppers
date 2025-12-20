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
    
    // Status and project priorities
    public int? Status { get; set; }
    public DateTime? StartPendingAt { get; set; }
    public int? ProjectPriority1 { get; set; }
    public int? ProjectPriority2 { get; set; }
    public int? ProjectPriority3 { get; set; }
    public int? ProjectPriority4 { get; set; }
    
    // Navigation to preferred projects
    [ForeignKey("ProjectPriority1")] public Project? ProjectPriority1Project { get; set; }
    [ForeignKey("ProjectPriority2")] public Project? ProjectPriority2Project { get; set; }
    [ForeignKey("ProjectPriority3")] public Project? ProjectPriority3Project { get; set; }
    [ForeignKey("ProjectPriority4")] public Project? ProjectPriority4Project { get; set; }
    
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
    
    // Programming language preference
    public int? ProgrammingLanguageId { get; set; }
    public ProgrammingLanguage? ProgrammingLanguage { get; set; }
    
    // Password hash field
    [MaxLength(256)]
    public string? PasswordHash { get; set; }
    
    // CV field (base64 encoded PDF/document)
    [Column(TypeName = "text")]
    public string? CV { get; set; }
    
    // Work preferences
    public int? MinutesToWork { get; set; }
    public bool HybridWork { get; set; } = false;
    public bool HomeWork { get; set; } = false;
    public bool FullTimeWork { get; set; } = false;
    public bool PartTimeWork { get; set; } = false;
    public bool FreelanceWork { get; set; } = false;
    public bool TravelWork { get; set; } = false;
    public bool NightShiftWork { get; set; } = false;
    public bool RelocationWork { get; set; } = false;
    public bool StudentWork { get; set; } = false;
    public bool MultilingualWork { get; set; } = false;
    
    // Subscription type
    public int? SubscriptionTypeId { get; set; }
    
    // Navigation properties
    public ICollection<StudentRole> StudentRoles { get; set; } = new List<StudentRole>();
    
    [ForeignKey("SubscriptionTypeId")]
    public Subscription? SubscriptionType { get; set; }
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
    
    [MaxLength(255)]
    public string? GithubUser { get; set; } // GitHub username (optional - not required for non-developer roles)
    
    [Required]
    public int RoleId { get; set; }
    
    public string? Photo { get; set; }  // Base64 encoded image or URL
    
    public int? ProgrammingLanguageId { get; set; }  // Optional programming language preference
    
    [MaxLength(100)]
    public string? Password { get; set; }  // Plain password (will be hashed before storing)
    
    // Work preferences
    public int? MinutesToWork { get; set; }
    public bool? HybridWork { get; set; }
    public bool? HomeWork { get; set; }
    public bool? FullTimeWork { get; set; }
    public bool? PartTimeWork { get; set; }
    public bool? FreelanceWork { get; set; }
    public bool? TravelWork { get; set; }
    public bool? NightShiftWork { get; set; }
    public bool? RelocationWork { get; set; }
    public bool? StudentWork { get; set; }
    public bool? MultilingualWork { get; set; }
    
    // CV field (base64 encoded PDF/document)
    public string? CV { get; set; }
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
    
    public int? ProgrammingLanguageId { get; set; }  // Optional programming language preference
    
    // Work preferences
    public int? MinutesToWork { get; set; }
    public bool? HybridWork { get; set; }
    public bool? HomeWork { get; set; }
    public bool? FullTimeWork { get; set; }
    public bool? PartTimeWork { get; set; }
    public bool? FreelanceWork { get; set; }
    public bool? TravelWork { get; set; }
    public bool? NightShiftWork { get; set; }
    public bool? RelocationWork { get; set; }
    public bool? StudentWork { get; set; }
    public bool? MultilingualWork { get; set; }
    
    // CV field (base64 encoded PDF/document)
    public string? CV { get; set; }
}
