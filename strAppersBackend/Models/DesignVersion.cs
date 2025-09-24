using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

public class DesignVersion
{
    public int Id { get; set; }
    
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    
    public int VersionNumber { get; set; }
    
    [Column(TypeName = "TEXT")]
    public string DesignDocument { get; set; } = string.Empty;
    
    public byte[]? DesignDocumentPdf { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [MaxLength(255)]
    public string? CreatedBy { get; set; }
    
    public bool IsActive { get; set; } = true;
}

public class CreateDesignVersionRequest
{
    [Required]
    public int ProjectId { get; set; }
    
    [Required]
    public int VersionNumber { get; set; }
    
    [Required]
    public string DesignDocument { get; set; } = string.Empty;
    
    public byte[]? DesignDocumentPdf { get; set; }
    
    [MaxLength(255)]
    public string? CreatedBy { get; set; }
    
    public bool IsActive { get; set; } = true;
}

public class UpdateDesignVersionRequest
{
    public int? VersionNumber { get; set; }
    
    public string? DesignDocument { get; set; }
    
    public byte[]? DesignDocumentPdf { get; set; }
    
    [MaxLength(255)]
    public string? CreatedBy { get; set; }
    
    public bool? IsActive { get; set; }
}



