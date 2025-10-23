using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

public class Role
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [MaxLength(50)]
    public string? Category { get; set; } // e.g., "Development", "Design", "Management"
    
    /// <summary>
    /// Role type: 0=Default, 1=Developer, 2=Junior Developer, 3=UI/UX Designer, 4=Leadership
    /// </summary>
    public int Type { get; set; } = 0;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<StudentRole> StudentRoles { get; set; } = new List<StudentRole>();
}

public class StudentRole
{
    public int Id { get; set; }
    
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
    
    public bool IsActive { get; set; } = true;
}
