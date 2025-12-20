using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

public class EmployerAdd
{
    public int Id { get; set; }
    
    [Required]
    public int EmployerId { get; set; }
    
    [Required]
    public int RoleId { get; set; }
    
    [Column(TypeName = "text")]
    public string? Tags { get; set; }
    
    [Column(TypeName = "text")]
    public string? JobDescription { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    [ForeignKey("EmployerId")]
    public Employer Employer { get; set; } = null!;
    
    [ForeignKey("RoleId")]
    public Role Role { get; set; } = null!;
}

