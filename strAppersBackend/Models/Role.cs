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
    public string Category { get; set; } = "General"; // Academic, Leadership, Technical, Administrative, etc.
    
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
    
    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    
    [MaxLength(200)]
    public string? Notes { get; set; }
    
    public bool IsActive { get; set; } = true;
}

public class CreateRoleRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [MaxLength(50)]
    public string Category { get; set; } = "General";
    
    public bool IsActive { get; set; } = true;
}

public class UpdateRoleRequest
{
    [MaxLength(100)]
    public string? Name { get; set; }
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [MaxLength(50)]
    public string? Category { get; set; }
    
    public bool? IsActive { get; set; }
}

public class AssignRoleRequest
{
    [Required]
    public int StudentId { get; set; }
    
    [Required]
    public int RoleId { get; set; }
    
    public DateTime? EndDate { get; set; }
    
    [MaxLength(200)]
    public string? Notes { get; set; }
}

