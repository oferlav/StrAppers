using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

public class EmployerBoard
{
    public int Id { get; set; }
    
    [Required]
    public int EmployerId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string BoardId { get; set; } = string.Empty;
    
    public bool Observed { get; set; } = false;
    
    public bool Approved { get; set; } = false;
    
    public DateTime? MeetRequest { get; set; }
    
    [Column(TypeName = "text")]
    public string? Message { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    [ForeignKey("EmployerId")]
    public Employer Employer { get; set; } = null!;
    
    [ForeignKey("BoardId")]
    public ProjectBoard ProjectBoard { get; set; } = null!;
}




