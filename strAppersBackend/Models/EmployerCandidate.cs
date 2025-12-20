using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

public class EmployerCandidate
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }
    
    [Required]
    [Column("EmployerId")]
    public int EmployerId { get; set; }
    
    [Required]
    [Column("StudentId")]
    public int StudentId { get; set; }
    
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    [ForeignKey(nameof(EmployerId))]
    public virtual Employer Employer { get; set; } = null!;
    
    [ForeignKey(nameof(StudentId))]
    public virtual Student Student { get; set; } = null!;
}




