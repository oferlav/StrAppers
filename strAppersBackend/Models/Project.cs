using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

public class Project
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [Column(TypeName = "TEXT")]
    public string? ExtendedDescription { get; set; }
    
    [Column(TypeName = "TEXT")]
    public string? SystemDesign { get; set; }
    
    public byte[]? SystemDesignDoc { get; set; }
    
    public int StatusId { get; set; }
    public ProjectStatus Status { get; set; } = null!;
    
    [MaxLength(50)]
    public string Priority { get; set; } = "Medium"; // Low, Medium, High, Critical
    
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? DueDate { get; set; }
    
    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    
    // Project allocation fields
    public bool HasAdmin { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<DesignVersion> DesignVersions { get; set; } = new List<DesignVersion>();
}

public class CreateProjectRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    public int StatusId { get; set; }
    
    [MaxLength(50)]
    public string Priority { get; set; } = "Medium";
    
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? DueDate { get; set; }
    
    public int? OrganizationId { get; set; }
    public bool HasAdmin { get; set; } = false;
}

public class UpdateProjectRequest
{
    [MaxLength(200)]
    public string? Title { get; set; }
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    public int? StatusId { get; set; }
    
    [MaxLength(50)]
    public string? Priority { get; set; }
    
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? DueDate { get; set; }
    
    public int? OrganizationId { get; set; }
    public bool? HasAdmin { get; set; }
}
