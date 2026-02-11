using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Models
{
    public class TrelloConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
        public string ApiToken { get; set; } = string.Empty;
        public bool SendInvitationToPMOnly { get; set; } = true;
        public bool CreatePMEmptyBoard { get; set; } = true;
        /// <summary>
        /// When true and Project.TrelloBoardJson has data, board creation uses the saved JSON instead of calling AI.
        /// </summary>
        public bool UseDBProjectBoard { get; set; } = true;
        /// <summary>
        /// First day of the week for kickoff and sprint boundaries (e.g. "Sunday", "Monday"). Default "Sunday".
        /// </summary>
        public string FirstDayOfWeek { get; set; } = "Sunday";
        /// <summary>
        /// Local timezone for meetings and sprint dates (e.g. "GMT+2", "UTC"). Default "GMT+2".
        /// Used in email invitations and NextMeetingTime.
        /// </summary>
        public string LocalTime { get; set; } = "GMT+2";
        /// <summary>
        /// AI prompt for merging a live sprint with the SystemBoard sprint (POST /api/Trello/use/merge-sprint).
        /// Placeholders: {{LiveSprintJson}} and {{SystemSprintJson}} are replaced with the live and system sprint card JSON.
        /// </summary>
        public string? SprintMergePrompt { get; set; }
        /// <summary>
        /// When true (default), board creation creates only the visible sprints (see VisibleSprints) plus one empty sprint and Bugs; after a sprint merge, the next empty sprint is created from TrelloBoardJson.
        /// </summary>
        public bool NextSprintOnlyVisability { get; set; } = true;
        /// <summary>
        /// Number of "system" sprints to create with full content. Board gets Sprint 1..VisibleSprints (system), Sprint (VisibleSprints+1) (empty), and Bugs. Default 2 = Sprint1, Sprint2 (system), Sprint3 (empty), Bugs.
        /// </summary>
        public int VisibleSprints { get; set; } = 2;
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
        /// <summary>Sprint/list-level description (stored in JSON; not sent to Trello list API).</summary>
        public string? Description { get; set; }
        /// <summary>Sprint-level checklist items (stored in JSON; can be used when creating a sprint card or for display).</summary>
        public List<string> ChecklistItems { get; set; } = new List<string>();
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
        public string? BoardId { get; set; }  // This will store the Trello-generated board ID (empty board if CreatePMEmptyBoard is true)
        public string? BoardName { get; set; }  // This will store the board name
        public string? SystemBoardId { get; set; }  // This will store the SystemBoard ID (full board) when CreatePMEmptyBoard is true
        public string? SystemBoardUrl { get; set; }  // This will store the SystemBoard URL when CreatePMEmptyBoard is true
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

    /// <summary>
    /// Request for POST /api/Trello/use/set-done — toggle a checklist item at checkIndex.
    /// </summary>
    public class TrelloSetDoneRequest
    {
        [Required]
        public string BoardId { get; set; } = string.Empty;

        [Required]
        public string CardId { get; set; } = string.Empty;

        /// <summary>
        /// 0-based index of the check item within the card's checklists (flattened: first checklist, then second, etc.).
        /// </summary>
        public int CheckIndex { get; set; }
    }

    /// <summary>
    /// Request for POST /api/Trello/use/merge-sprint. Override live sprint with SystemBoard sprint; optionally merge via AI.
    /// </summary>
    public class MergeSprintRequest
    {
        public int ProjectId { get; set; }
        [Required]
        public string BoardId { get; set; } = string.Empty;
        public int SprintNumber { get; set; }
        /// <summary>If true (default), live and system sprints are merged via AI then override; if false, live sprint is completely overwritten with system sprint (no AI).</summary>
        public bool Merge { get; set; } = true;
    }

    /// <summary>One card in a sprint snapshot (for get/override/merge).</summary>
    public class SprintSnapshotCard
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public List<string> ChecklistItems { get; set; } = new List<string>();
        public string? CardId { get; set; }
    }

    /// <summary>Snapshot of a sprint list on a board (list id/name + cards).</summary>
    public class SprintSnapshot
    {
        public string ListId { get; set; } = string.Empty;
        public string ListName { get; set; } = string.Empty;
        public List<SprintSnapshotCard> Cards { get; set; } = new List<SprintSnapshotCard>();
    }
}
