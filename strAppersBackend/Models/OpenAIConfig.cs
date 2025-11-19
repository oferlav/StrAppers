using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace strAppersBackend.Models;

public class OpenAIConfig
{
    public string ApiKey { get; set; } = string.Empty;
}

public class SystemDesignRequest
{
    [Required]
    public int ProjectId { get; set; }
    
    [Required]
    [JsonConverter(typeof(CleanTextJsonConverter))]
    public string ExtendedDescription { get; set; } = string.Empty;
    
    [Required]
    public List<RoleInfo> TeamRoles { get; set; } = new List<RoleInfo>();
    
    [MaxLength(255)]
    public string? CreatedBy { get; set; }
}

public class RoleInfo
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int StudentCount { get; set; }
}

public class SystemDesignResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int? DesignVersionId { get; set; }
    public string? DesignDocument { get; set; }
    public string? DesignDocumentFormatted { get; set; }
    public byte[]? DesignDocumentPdf { get; set; }
}

public class SprintPlanningRequest
{
    [Required]
    public int ProjectId { get; set; }
    
    [Required]
    public int SprintLengthWeeks { get; set; }
    
    [Required]
    public int ProjectLengthWeeks { get; set; }
    
    [Required]
    public DateTime StartDate { get; set; }
    
    [Required]
    public List<RoleInfo> TeamRoles { get; set; } = new List<RoleInfo>();
    
    public List<StudentInfo> Students { get; set; } = new List<StudentInfo>();
    
    [MaxLength(255)]
    public string? CreatedBy { get; set; }
    
    public string? SystemDesign { get; set; }
}

public class SprintPlanningResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public SprintPlan? SprintPlan { get; set; }
}

public class SprintPlan
{
    public List<Epic> Epics { get; set; } = new List<Epic>();
    public List<Sprint> Sprints { get; set; } = new List<Sprint>();
    public int TotalSprints { get; set; }
    public int TotalTasks { get; set; }
    public int EstimatedWeeks { get; set; }
}

public class Epic
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<UserStory> UserStories { get; set; } = new List<UserStory>();
    public int Priority { get; set; }
}

public class UserStory
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AcceptanceCriteria { get; set; } = string.Empty;
    public List<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
    public int StoryPoints { get; set; }
    public int Priority { get; set; }
}

public class ProjectTask
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int EstimatedHours { get; set; }
    public int Priority { get; set; }
    public List<string> Dependencies { get; set; } = new List<string>();
    public List<string> ChecklistItems { get; set; } = new List<string>();
}

public class Sprint
{
    public int SprintNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
    public int TotalStoryPoints { get; set; }
    public Dictionary<int, int> RoleWorkload { get; set; } = new Dictionary<int, int>();
}

public class StudentInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

// New response models for ProjectModules AI methods
public class InitiateModulesResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<ModuleInfo>? Modules { get; set; }
}

public class ModuleInfo
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Inputs { get; set; } = string.Empty;
    public string Outputs { get; set; } = string.Empty;
}

public class CreateDataModelResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? SqlScript { get; set; }
}

public class UpdateModuleResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? UpdatedDescription { get; set; }
}

public class UpdateDataModelResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? UpdatedSqlScript { get; set; }
}
