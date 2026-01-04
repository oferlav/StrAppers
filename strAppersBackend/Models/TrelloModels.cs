using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models
{
    public class TrelloConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
        public string ApiToken { get; set; } = string.Empty;
    }

    public class TrelloUserRegistrationRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        public string? FullName { get; set; }
    }

    public class TrelloUserRegistrationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? Username { get; set; }
        public string? FullName { get; set; }
    }

    public class TrelloUserCheckRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class TrelloUserCheckResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsRegistered { get; set; }
        public string? UserId { get; set; }
        public string? Username { get; set; }
        public string? FullName { get; set; }
    }

    public class TrelloProjectCreationRequest
    {
        [Required]
        public int ProjectId { get; set; }
        
        public string? ProjectTitle { get; set; }
        public string? ProjectDescription { get; set; }
        public List<string> StudentEmails { get; set; } = new List<string>();
        public int ProjectLengthWeeks { get; set; }
        public int SprintLengthWeeks { get; set; }
        
        public DateTime? DueDate { get; set; }
        
        [Required]
        public List<TrelloTeamMember> TeamMembers { get; set; } = new List<TrelloTeamMember>();
        
        [Required]
        public TrelloSprintPlan SprintPlan { get; set; } = new TrelloSprintPlan();
    }

    public class TrelloTeamMember
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }

    public class TrelloSprintPlan
    {
        public List<TrelloBoard> Boards { get; set; } = new List<TrelloBoard>();
        public List<TrelloList> Lists { get; set; } = new List<TrelloList>();
        public List<TrelloCard> Cards { get; set; } = new List<TrelloCard>();
        public int TotalSprints { get; set; }
        public int TotalTasks { get; set; }
        public int EstimatedWeeks { get; set; }
    }

    public class TrelloBoard
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public List<string> MemberEmails { get; set; } = new List<string>();
    }

    public class TrelloList
    {
        public string Name { get; set; } = string.Empty;
        public string BoardName { get; set; } = string.Empty;
        public int Position { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class TrelloCard
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ListName { get; set; } = string.Empty;
        public string AssignedToEmail { get; set; } = string.Empty;
        public string AssignedToName { get; set; } = string.Empty;
        public List<string> Labels { get; set; } = new List<string>();
        public DateTime? DueDate { get; set; }
        public int Priority { get; set; }
        public int EstimatedHours { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string Status { get; set; } = "To Do";
        public string Risk { get; set; } = "Medium";
        public string ModuleId { get; set; } = string.Empty;
        public string CardId { get; set; } = string.Empty;
        public List<string> Dependencies { get; set; } = new List<string>();
        public bool? Branched { get; set; }
        public List<string> ChecklistItems { get; set; } = new List<string>();
    }

    public class TrelloProjectCreationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? BoardUrl { get; set; }
        public string? BoardId { get; set; }  // This will store the Trello-generated board ID
        public string? BoardName { get; set; }  // This will store the board name
        public List<TrelloCreatedCard> CreatedCards { get; set; } = new List<TrelloCreatedCard>();
        public List<TrelloInvitedUser> InvitedUsers { get; set; } = new List<TrelloInvitedUser>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class TrelloCreatedCard
    {
        public string CardId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AssignedToEmail { get; set; } = string.Empty;
        public string ListName { get; set; } = string.Empty;
        public string CardUrl { get; set; } = string.Empty;
    }

    public class TrelloInvitedUser
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
