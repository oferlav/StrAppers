using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

public class ProjectCriteria
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}








