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
    public bool IsAdmin { get; set; }
    public bool AdminAllocationFailed { get; set; }
}

public class AvailableProject
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string? OrganizationName { get; set; }
    public bool HasAdmin { get; set; }
    public int StudentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsAvailable { get; set; } = true;
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
