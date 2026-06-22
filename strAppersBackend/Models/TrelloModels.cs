using System.ComponentModel.DataAnnotations;
using System.Linq;

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
        /// <summary>
        /// "Merge" (default): use System Board and merge/override sprints. "Add": no System Board; create only VisibleSprints lists + Bugs at board creation; each time a sprint ends, add one new sprint list.
        /// </summary>
        public string MergeType { get; set; } = "Merge";
        /// <summary>
        /// First sprint number (1-based) for which to add a "User Story" card in the User Stories list. Default 3.
        /// </summary>
        public int UserStoryFirstSprint { get; set; } = 3;
        /// <summary>
        /// When true (default), add the "User Stories" list to the board template (Projects.TrelloBoardJson) when using the add-user-story-list utility.
        /// </summary>
        public bool UserStoryList { get; set; } = true;
        /// <summary>
        /// When true, board creation splits the "User Stories" list onto a dedicated Trello board separate from the main sprint board.
        /// One card is generated per ProjectModule (ModuleType != 3) with name "User Story: {module.Title}", ModuleId set, and an "Acceptance Criteria" checklist.
        /// PM members (filtered by SendInvitationToPMOnly) are invited to the User Story board only; the main board receives no User Story list and no PM invitation.
        /// Null UserStoryBoardId on ProjectBoard = legacy board; get-user-stories falls back to the main board.
        /// </summary>
        public bool CreateUserStoryBoard { get; set; } = false;
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
        /// <summary>Optional checklist name (e.g. "Acceptance Criteria") when different from default "Checklist".</summary>
        public string? ChecklistName { get; set; }
        /// <summary>Optional sprint number for user-story cards in the User Stories list (custom field in template).</summary>
        public int? SprintNumber { get; set; }
        /// <summary>Checkbox custom field "Required Skill Data" (template JSON; applied when creating cards on a board).</summary>
        public bool? RequiredSkillData { get; set; }
        /// <summary>Checkbox custom field "Required Resource Data" (template JSON; applied when creating cards on a board).</summary>
        public bool? RequiredResourceData { get; set; }

        /// <summary>Optional text custom field <c>BranchContext</c> (developer cards; gap analysis Git branch override).</summary>
        public string? BranchContext { get; set; }
    }

    /// <summary>Result of <see cref="Services.ITrelloService.UpdateExistingBoardWithBranchContextAsync"/>.</summary>
    public class BranchContextUtilityResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public List<string> Log { get; set; } = new();
    }

    /// <summary>Sets <see cref="TrelloCard.BranchContext"/> on Backend/Frontend developer cards for a specific sprint list in template JSON.</summary>
    public static class TrelloBranchContextTemplateHelper
    {
        public const string BackendDeveloperLabel = "Backend Developer";
        public const string FrontendDeveloperLabel = "Frontend Developer";
        public const string DefaultBackendBranchContext = "Bugs-B";
        public const string DefaultFrontendBranchContext = "Bugs-F";

        public static bool IsCardInSprintList(string? listName, int sprintNumber)
        {
            var ln = listName?.Trim() ?? "";
            return string.Equals(ln, $"Sprint {sprintNumber}", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ln, $"Sprint{sprintNumber}", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Sets BranchContext on cards in <paramref name="sprintNumber"/> list that have Backend or Frontend developer labels.</summary>
        /// <returns>Number of cards that received a BranchContext value.</returns>
        public static int ApplyToDeveloperCardsInSprint(
            TrelloProjectCreationRequest request,
            int sprintNumber,
            string backendValue = DefaultBackendBranchContext,
            string frontendValue = DefaultFrontendBranchContext)
        {
            if (request.SprintPlan?.Cards == null || request.SprintPlan.Cards.Count == 0)
                return 0;
            var n = 0;
            foreach (var card in request.SprintPlan.Cards)
            {
                if (!IsCardInSprintList(card.ListName, sprintNumber))
                    continue;
                if (card.Labels == null || card.Labels.Count == 0)
                    continue;
                if (card.Labels.Any(l => string.Equals(l.Trim(), BackendDeveloperLabel, StringComparison.OrdinalIgnoreCase)))
                {
                    card.BranchContext = backendValue;
                    n++;
                }
                else if (card.Labels.Any(l => string.Equals(l.Trim(), FrontendDeveloperLabel, StringComparison.OrdinalIgnoreCase)))
                {
                    card.BranchContext = frontendValue;
                    n++;
                }
            }

            return n;
        }
    }

    /// <summary>Names and per-list defaults for the Adherence checkbox custom fields on Trello cards.</summary>
    public static class TrelloRequiredDataFieldRules
    {
        public const string RequiredSkillDataFieldName = "Required Skill Data";
        public const string RequiredResourceDataFieldName = "Required Resource Data";

        /// <summary>Bugs: skill true, resource false. User Stories: both false. All other lists (e.g. Sprint N): both true.</summary>
        public static (bool RequiredSkillData, bool RequiredResourceData) ValuesForListName(string? listName)
        {
            if (string.IsNullOrWhiteSpace(listName))
                return (true, true);
            var n = listName.Trim();
            if (string.Equals(n, "Bugs", StringComparison.OrdinalIgnoreCase))
                return (true, false);
            if (string.Equals(n, "User Stories", StringComparison.OrdinalIgnoreCase))
                return (false, false);
            return (true, true);
        }
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
        public string? UserStoryBoardId { get; set; }   // Dedicated User Story board ID (when CreateUserStoryBoard=true)
        public string? UserStoryBoardUrl { get; set; }  // Dedicated User Story board URL (when CreateUserStoryBoard=true)
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

    /// <summary>Raw list info for Trello dashboard stats.</summary>
    public class TrelloDashboardListInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Check item state for dashboard stats.</summary>
    public class TrelloDashboardCheckItem
    {
        public string Id { get; set; } = string.Empty;
        public string State { get; set; } = "incomplete";
    }

    /// <summary>Checklist with items for dashboard stats.</summary>
    public class TrelloDashboardChecklist
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<TrelloDashboardCheckItem> CheckItems { get; set; } = new List<TrelloDashboardCheckItem>();
    }

    /// <summary>Card with checklists for Trello dashboard stats (excludes User Stories list by filter).</summary>
    public class TrelloDashboardCardInfo
    {
        public string Id { get; set; } = string.Empty;
        public string IdList { get; set; } = string.Empty;
        public bool Closed { get; set; }
        /// <summary>True when the card is marked complete (checkmark) in Trello; card may still be open (not archived).</summary>
        public bool DueComplete { get; set; }
        public DateTime? Due { get; set; }
        public List<string> IdMembers { get; set; } = new List<string>();
        public List<string> LabelNames { get; set; } = new List<string>();
        public List<TrelloDashboardChecklist> Checklists { get; set; } = new List<TrelloDashboardChecklist>();
    }

    /// <summary>Board member for matching students to Trello assignees.</summary>
    public class TrelloDashboardMember
    {
        public string Id { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? FullName { get; set; }
    }

    /// <summary>Lists and cards (with checklists) for computing dashboard stats. Cards exclude User Stories list.</summary>
    public class TrelloDashboardData
    {
        public List<TrelloDashboardListInfo> Lists { get; set; } = new List<TrelloDashboardListInfo>();
        public List<TrelloDashboardCardInfo> Cards { get; set; } = new List<TrelloDashboardCardInfo>();
        public List<TrelloDashboardMember> Members { get; set; } = new List<TrelloDashboardMember>();
    }

    /// <summary>Bug summary for get-all-open-bugs: single bug with CardId, Title, Priority, Status. Overdue is set only for Open bugs.</summary>
    public class OpenBugInfo
    {
        /// <summary>CardId custom field value (e.g. B-6fe6f647) used to identify the bug card.</summary>
        public string CardId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        /// <summary>Priority as integer (e.g. 1=Low, 2=Medium, 3=High, 4=Critical). 0 when missing or unparseable.</summary>
        public int Priority { get; set; }
        public string Status { get; set; } = string.Empty;
        /// <summary>True when the bug is Open and the due date has passed. Always false for Done bugs.</summary>
        public bool Overdue { get; set; }
    }

    /// <summary>Response for get-all-open-bugs: two sections, Open (not done) and Done.</summary>
    public class BoardBugsResponse
    {
        public IReadOnlyList<OpenBugInfo> Open { get; set; } = Array.Empty<OpenBugInfo>();
        public IReadOnlyList<OpenBugInfo> Done { get; set; } = Array.Empty<OpenBugInfo>();
    }

    /// <summary>Full bug details for get-bug by boardId and CardId.</summary>
    public class BugDetail
    {
        /// <summary>CardId custom field value (e.g. B-6fe6f647) used to identify the bug card.</summary>
        public string CardId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        /// <summary>Priority as integer (e.g. 1=Low, 2=Medium, 3=High, 4=Critical). 0 when missing or unparseable.</summary>
        public int Priority { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
