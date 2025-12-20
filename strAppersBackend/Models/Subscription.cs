using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

public class Subscription
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public decimal Price { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<Employer> Employers { get; set; } = new List<Employer>();
    public ICollection<Student> Students { get; set; } = new List<Student>();
}




