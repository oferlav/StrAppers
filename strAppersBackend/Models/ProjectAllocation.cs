using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models;

public class ProjectAllocationRequest
{
    [Required]
    public int StudentId { get; set; }
    
    [Required]
    public int ProjectId { get; set; }
    
    public bool IsAdmin { get; set; } = false;
}

public class ProjectAllocationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Student? Student { get; set; }
    public Project? Project { get; set; }
}

public class AvailableProject
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public bool HasAdmin { get; set; }
    public bool IsAvailable { get; set; }
}

public class ChangeProjectStatusRequest
{
    [Required]
    public int StudentId { get; set; }
    
    [Required]
    public int ProjectId { get; set; }
}

public class ChangeProjectStatusResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Project? Project { get; set; }
}
