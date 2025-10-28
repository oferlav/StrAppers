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
    
    [Column(TypeName = "TEXT")]
    public string? DataSchema { get; set; }
    
    public byte[]? SystemDesignDoc { get; set; }
    
    [MaxLength(2000)]
    [Column("SystemDesignFormatted")]
    public string? SystemDesignFormatted { get; set; }
    
    [MaxLength(50)]
    public string Priority { get; set; } = "Medium"; // Low, Medium, High, Critical
    
    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    
    // Project availability
    [Column("isAvailable")]
    public bool IsAvailable { get; set; } = true;
    
    [Column("Kickoff")]
    public bool? Kickoff { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<DesignVersion> DesignVersions { get; set; } = new List<DesignVersion>();
}

public class CreateProjectRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [Column(TypeName = "TEXT")]
    public string? ExtendedDescription { get; set; }
    
    [MaxLength(50)]
    public string Priority { get; set; } = "Medium";
    
    public bool IsAvailable { get; set; } = true;
}

public class CreateProjectSimpleRequest
{
    [Required]
    public int OrganizationId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [Column(TypeName = "TEXT")]
    public string? ExtendedDescription { get; set; }
}

public class UpdateProjectRequest
{
    [MaxLength(200)]
    public string? Title { get; set; }
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [Column(TypeName = "TEXT")]
    public string? ExtendedDescription { get; set; }
    
    [MaxLength(50)]
    public string? Priority { get; set; }
    
    public bool? IsAvailable { get; set; }
}
