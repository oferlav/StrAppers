using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace strAppersBackend.Models;

public class Employer
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [Column("Logo", TypeName = "text")]
    public string? Logo { get; set; } // Base64 string or URL
    
    [MaxLength(500)]
    [Url]
    public string? Website { get; set; }
    
    [Required]
    [MaxLength(255)]
    [EmailAddress]
    public string ContactEmail { get; set; } = string.Empty;
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    [MaxLength(500)]
    public string? Address { get; set; }
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [Required]
    public int SubscriptionTypeId { get; set; }
    
    [MaxLength(256)]
    [Column("PasswordHash")]
    public string? PasswordHash { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    [ForeignKey("SubscriptionTypeId")]
    public Subscription SubscriptionType { get; set; } = null!;
    
    public ICollection<EmployerBoard> EmployerBoards { get; set; } = new List<EmployerBoard>();
    public ICollection<EmployerAdd> EmployerAdds { get; set; } = new List<EmployerAdd>();
    public ICollection<EmployerCandidate> EmployerCandidates { get; set; } = new List<EmployerCandidate>();
}

