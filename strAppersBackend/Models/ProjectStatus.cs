using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

public class ProjectStatus
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string? Description { get; set; }
    
    [MaxLength(7)]
    public string Color { get; set; } = "#6B7280"; // Default gray color (hex)
    
    public int SortOrder { get; set; } = 0;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}

public class CreateProjectStatusRequest
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string? Description { get; set; }
    
    [MaxLength(7)]
    public string Color { get; set; } = "#6B7280";
    
    public int SortOrder { get; set; } = 0;
    
    public bool IsActive { get; set; } = true;
}

public class UpdateProjectStatusRequest
{
    [MaxLength(50)]
    public string? Name { get; set; }
    
    [MaxLength(200)]
    public string? Description { get; set; }
    
    [MaxLength(7)]
    public string? Color { get; set; }
    
    public int? SortOrder { get; set; }
    
    public bool? IsActive { get; set; }
}

