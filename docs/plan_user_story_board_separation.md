# Plan: Separate User Story List into Dedicated Trello Board

## Goal

Split the Trello "User Story" list off the main project board onto its own dedicated Trello board.  
The PM receives a Trello invitation email to this board only. Students and sprint operations remain on the main board, unaffected.  
User Story cards are generated on-the-fly from `ProjectModules` (one card per module) — always in sync with the project design.

---

## Key Decisions (from analysis)

| Decision | Rationale |
|---|---|
| New flag `CreateUserStoryBoard` (not reusing `MergeType` or `CreatePMEmptyBoard`) | `MergeType="Add"` governs sprint cadence; `CreatePMEmptyBoard` is currently dormant. Mixing concerns would break sprint logic. |
| New DB columns `UserStoryBoardId`, `UserStoryBoardUrl` on `ProjectBoard` | 11 other entities FK to `ProjectBoard.Id` — making the User Story board the primary record is not viable. |
| Generate cards on-the-fly from `ProjectModules`, not from `TrelloBoardJson` | Existing `TrelloBoardJson` User Story cards are per-sprint with empty `ModuleId` — not per-module. On-the-fly ensures permanent sync with project design. |
| Backend resolves `get-user-stories` to the correct board (server-side routing) | `SprintDetailsModal`, `SprintProgressPanel`, `UserStoryModal` need zero changes. |
| `SendInvitationToPMOnly` filter reused as-is | Already filters to PM role names; applied to User Story board invitations unchanged. |
| Sprint merge unchanged | `TrelloSprintMergeService` never reads or writes the User Story list. Confirmed safe. |
| `TrelloBoardJson` source unchanged | Board creation reads and filters `TrelloBoardJson` normally; User Story list is excluded from the main board at creation time. `StoreTrelloBoardJson` is not modified. |

---

## Architecture Overview

```
Board Creation (when CreateUserStoryBoard=true)
│
├── Main Board  (ProjectBoard.Id)
│   ├── Sprint 1..N lists  (from TrelloBoardJson, unchanged)
│   ├── Bugs list
│   └── NO User Story list
│   └── sendEmails: false  (no PM invite here)
│
└── User Story Board  (ProjectBoard.UserStoryBoardId)
    ├── "User Stories" list
    │   ├── Card: "User Story: [Module 1 Title]"  ModuleId=1  Checklist: Acceptance Criteria
    │   ├── Card: "User Story: [Module 2 Title]"  ModuleId=2  Checklist: Acceptance Criteria
    │   └── ...one card per ProjectModule (ModuleType != 3)
    └── sendEmails: true  → PM-filtered Trello invitation email
```

---

## 1. Database

### 1a. New columns on `ProjectBoard`

**File:** `strAppersBackend/Models/ProjectBoard.cs`

Add after `SystemBoardId` (~line 294):

```csharp
/// <summary>
/// Trello board ID for the dedicated User Story board (created when CreateUserStoryBoard=true).
/// Null for legacy boards created before this feature.
/// </summary>
[Column("UserStoryBoardId")]
public string? UserStoryBoardId { get; set; }

/// <summary>
/// Trello board URL for the dedicated User Story board.
/// </summary>
[Column("UserStoryBoardUrl")]
public string? UserStoryBoardUrl { get; set; }
```

### 1b. EF Migration

Create a new migration:

```
Add-Migration AddUserStoryBoardToProjectBoards
```

Migration up:
```csharp
migrationBuilder.AddColumn<string>(
    name: "UserStoryBoardId",
    table: "ProjectBoards",
    type: "nvarchar(max)",
    nullable: true);

migrationBuilder.AddColumn<string>(
    name: "UserStoryBoardUrl",
    table: "ProjectBoards",
    type: "nvarchar(max)",
    nullable: true);
```

No backfill needed. `null` = legacy board (fall back to main board for user story queries).

---

## 2. Configuration

### 2a. New flag in `TrelloConfig`

**File:** `strAppersBackend/Models/TrelloModels.cs`

Add to `TrelloConfig` class:

```csharp
/// <summary>
/// When true, board creation splits the User Story list onto a dedicated Trello board.
/// PM members (filtered by SendInvitationToPMOnly) are invited to the User Story board only.
/// Main board receives no User Story list and no PM invitation.
/// </summary>
public bool CreateUserStoryBoard { get; set; } = false;
```

### 2b. `appsettings.json`

```json
"Trello": {
  "MergeType": "Add",
  "CreatePMEmptyBoard": true,
  "SendInvitationToPMOnly": true,
  "CreateUserStoryBoard": false
}
```

Default is `false` — all existing boards are unaffected until explicitly enabled.

---

## 3. `TrelloProjectCreationResponse` — add new fields

**File:** `strAppersBackend/Models/TrelloModels.cs`

Add to `TrelloProjectCreationResponse`:

```csharp
/// <summary>Trello board ID of the dedicated User Story board (null if not created).</summary>
public string? UserStoryBoardId { get; set; }

/// <summary>Trello board URL of the dedicated User Story board (null if not created).</summary>
public string? UserStoryBoardUrl { get; set; }
```

---

## 4. Backend — `TrelloService`

**File:** `strAppersBackend/Services/TrelloService.cs`

### 4a. New method: `CreateUserStoryBoardAsync`

```csharp
/// <summary>
/// Creates a dedicated Trello board containing only a "User Stories" list,
/// with one card per ProjectModule. Invites PM members via Trello API.
/// </summary>
private async Task<(string? boardId, string? boardUrl, List<string> errors)>
    CreateUserStoryBoardAsync(
        string boardName,
        string? organizationId,
        List<ProjectModuleInfo> modules,
        List<TrelloMember> members)
{
    // 1. Create Trello board
    var url = $"https://api.trello.com/1/boards/" +
              $"?name={Uri.EscapeDataString(boardName)}" +
              $"&desc=User+Stories+board" +
              $"&defaultLists=false" +
              $"&prefs_permissionLevel=public" +
              $"&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";

    var boardResponse = await _httpClient.PostAsync(url, null);
    // ... parse boardId, boardUrl

    // 2. Create "User Stories" list on the new board
    var listUrl = $"https://api.trello.com/1/lists" +
                  $"?name=User+Stories&idBoard={boardId}" +
                  $"&key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
    var listResponse = await _httpClient.PostAsync(listUrl, null);
    // ... parse listId

    // 3. Create one card per module
    foreach (var module in modules)
    {
        var card = new TrelloCard
        {
            Name            = $"User Story: {module.Title}",
            Description     = string.Empty,
            ListName        = "User Stories",
            ModuleId        = module.Id.ToString(),
            ChecklistName   = "Acceptance Criteria",
            ChecklistItems  = new List<string> { "Add Acceptance Criteria here" },
            RequiredSkillData    = false,
            RequiredResourceData = false,
        };
        await CreateCardOnListAsync(listId, card);
    }

    // 4. Invite PM members (reuse existing PM filter + invite logic)
    var pmMembers = _trelloConfig.SendInvitationToPMOnly
        ? members.Where(m =>
            m.RoleName.Contains("Product Manager", StringComparison.OrdinalIgnoreCase) ||
            m.RoleName.Contains("PM", StringComparison.OrdinalIgnoreCase)).ToList()
        : members;

    foreach (var member in pmMembers)
    {
        await InviteMemberToBoardByEmailAsync(boardId, member.Email);
    }

    return (boardId, boardUrl, errors);
}
```

### 4b. `CreateProjectWithSprintsAsync` — wire in new board

After the existing single-board creation block, add:

```csharp
// User Story board (gated by CreateUserStoryBoard flag)
if (_trelloConfig.CreateUserStoryBoard && modules?.Count > 0)
{
    var userStoryBoardName = $"{request.BoardName} — User Stories";
    var (usBoardId, usBoardUrl, usErrors) =
        await CreateUserStoryBoardAsync(userStoryBoardName, organizationId, modules, members);

    response.UserStoryBoardId  = usBoardId;
    response.UserStoryBoardUrl = usBoardUrl;
    errors.AddRange(usErrors);
}
```

Also: when `CreateUserStoryBoard=true`, strip the `"User Stories"` list (and its cards) from `request.SprintPlan.Lists` / `request.SprintPlan.Cards` **before** creating the main board, so it doesn't appear there:

```csharp
if (_trelloConfig.CreateUserStoryBoard)
{
    request.SprintPlan.Lists?.RemoveAll(l =>
        string.Equals(l.Name, "User Stories", StringComparison.OrdinalIgnoreCase));
    request.SprintPlan.Cards?.RemoveAll(c =>
        string.Equals(c.ListName, "User Stories", StringComparison.OrdinalIgnoreCase));
}
```

> **Note:** `modules` (the `List<ProjectModuleInfo>`) must be passed into `CreateProjectWithSprintsAsync`. Currently `StoreTrelloBoardJson` fetches them from DB — the same fetch must happen in `BoardsController` before calling this method, and passed through. See §5b.

---

## 5. Backend — `BoardsController`

**File:** `strAppersBackend/Controllers/BoardsController.cs`

### 5a. Board creation endpoint (~line 3119) — save new fields

```csharp
var projectBoard = new ProjectBoard
{
    Id                 = trelloBoardId,
    // ... existing fields unchanged ...
    UserStoryBoardId   = trelloResponse.UserStoryBoardId,   // NEW
    UserStoryBoardUrl  = trelloResponse.UserStoryBoardUrl,  // NEW
};
```

### 5b. Pass `ProjectModules` into board creation

Before calling `CreateProjectWithSprintsAsync`, fetch modules:

```csharp
var modules = await _context.ProjectModules
    .Where(pm => pm.ProjectId == request.ProjectId && pm.ModuleType != 3)
    .OrderBy(pm => pm.Sequence)
    .Select(pm => new ProjectModuleInfo
    {
        Id    = pm.Id,
        Title = pm.Title,
    })
    .ToListAsync();

var trelloResponse = await _trelloService.CreateProjectWithSprintsAsync(request, modules);
```

### 5c. `GET /api/Boards/use/stats/get-user-stories` — route to correct board

**Current:** queries `boardId` (main board) directly.

**Change:** look up `ProjectBoard` by the incoming `boardId`, use `UserStoryBoardId` if set:

```csharp
[HttpGet("use/stats/get-user-stories")]
public async Task<IActionResult> GetUserStories(
    [FromQuery] string boardId,
    [FromQuery] int? studentId,
    [FromQuery] int? sprintNumber)
{
    // Resolve to User Story board if one exists for this project board
    var board = await _context.ProjectBoards
        .AsNoTracking()
        .FirstOrDefaultAsync(b => b.Id == boardId);

    var effectiveBoardId = board?.UserStoryBoardId ?? boardId;  // fall back for legacy boards

    // Existing query logic unchanged, using effectiveBoardId
    var result = studentId.HasValue && sprintNumber.HasValue
        ? await _trelloService.GetUserStoryCardByModuleIdAsync(effectiveBoardId, studentId.Value, sprintNumber.Value)
        : await _trelloService.GetUserStoriesListAsync(effectiveBoardId);

    return Ok(result);
}
```

---

## 6. Backend — Board Stats DTO

Find the DTO returned by the board stats / boardroom endpoint (the object the frontend reads `boardUrl` from).

Add:

```csharp
/// <summary>URL of the dedicated User Story Trello board. Null for legacy boards.</summary>
public string? UserStoryBoardUrl { get; set; }
```

Map in the query/service that builds this DTO:

```csharp
UserStoryBoardUrl = projectBoard.UserStoryBoardUrl,
```

---

## 7. Frontend — `TrelloButton`

**File:** `skill-in-Frontend/src/.../TrelloButton.jsx`

**Current (~line 20):**
```jsx
<button onClick={() => window.open(boardStats.boardUrl, '_blank')}>
  Open in Trello
</button>
```

**Change:** add a second button when `userStoryBoardUrl` is present:

```jsx
<div className="flex items-center gap-2">
  <button
    onClick={() => window.open(boardStats.boardUrl, '_blank')}
    className="..."
  >
    Open in Trello
  </button>

  {boardStats.userStoryBoardUrl && (
    <button
      onClick={() => window.open(boardStats.userStoryBoardUrl, '_blank')}
      className="..."
      title="User Stories board"
    >
      User Stories Board
    </button>
  )}
</div>
```

The second button only renders when the field is populated — zero visual change for legacy boards.

---

## 8. Components with NO changes required

| Component | Why unchanged |
|---|---|
| `SprintDetailsModal.jsx` | Calls `invokeGetUserStories(boardId, ...)` — backend resolves to correct board (§5c) |
| `SprintProgressPanel.jsx` | Same — backend routing transparent |
| `UserStoryModal.jsx` | Same |
| `TrelloSprintMergeService.cs` | Sprint merge never touches the User Story list |
| `StoreTrelloBoardJson` endpoint | Source JSON unchanged; User Story list stripped at board-creation time only |
| `AddUserStoryList` endpoint | Existing per-sprint flow unchanged; new feature bypasses it |
| All 11 FK entities referencing `ProjectBoard.Id` | `ProjectBoard.Id` remains the main board — no impact |

---

## 9. Unit Tests

### 9a. `TrelloService` tests

**File:** `StrAppersWebApi.Tests/TrelloServiceUserStoryBoardTests.cs`

```csharp
// T1 — CreateUserStoryBoardAsync creates board, list, and one card per module
[Fact]
public async Task CreateUserStoryBoardAsync_CreatesOneBoardOneListOneCardPerModule()

// T2 — Card names follow "User Story: {module.Title}" format
[Fact]
public async Task CreateUserStoryBoardAsync_CardNamesAreCorrectlyFormatted()

// T3 — Each card has ModuleId set to the module's Id
[Fact]
public async Task CreateUserStoryBoardAsync_SetsModuleIdOnEachCard()

// T4 — Each card has ChecklistName="Acceptance Criteria"
[Fact]
public async Task CreateUserStoryBoardAsync_SetsAcceptanceCriteriaChecklist()

// T5 — RequiredSkillData and RequiredResourceData are false on all cards
[Fact]
public async Task CreateUserStoryBoardAsync_CustomFieldsFalseOnAllCards()

// T6 — When SendInvitationToPMOnly=true, only PM-role members are invited
[Fact]
public async Task CreateUserStoryBoardAsync_InvitesOnlyPMsWhenFlagTrue()

// T7 — When SendInvitationToPMOnly=false, all members are invited
[Fact]
public async Task CreateUserStoryBoardAsync_InvitesAllMembersWhenFlagFalse()

// T8 — When CreateUserStoryBoard=true, User Story list is stripped from main board request
[Fact]
public async Task CreateProjectWithSprintsAsync_StripsUserStoryListFromMainBoard_WhenFlagTrue()

// T9 — When CreateUserStoryBoard=false, User Story list remains on main board (legacy)
[Fact]
public async Task CreateProjectWithSprintsAsync_LeavesUserStoryListOnMainBoard_WhenFlagFalse()

// T10 — Response contains UserStoryBoardId and UserStoryBoardUrl when created
[Fact]
public async Task CreateProjectWithSprintsAsync_PopulatesUserStoryBoardFieldsInResponse()

// T11 — Empty modules list: User Story board is still created, just has no cards
[Fact]
public async Task CreateUserStoryBoardAsync_HandlesEmptyModuleList_CreatesEmptyList()

// T12 — Modules with ModuleType=3 are excluded from card generation
[Fact]
public async Task CreateProjectWithSprintsAsync_ExcludesModuleType3FromUserStoryCards()
```

### 9b. `BoardsController` tests

**File:** `StrAppersWebApi.Tests/BoardsControllerUserStoryBoardTests.cs`

```csharp
// T13 — After board creation, ProjectBoard.UserStoryBoardId and UserStoryBoardUrl are persisted
[Fact]
public async Task CreateBoard_PersistsUserStoryBoardFieldsOnProjectBoard()

// T14 — get-user-stories uses UserStoryBoardId when set on ProjectBoard
[Fact]
public async Task GetUserStories_UsesUserStoryBoardId_WhenFieldIsSet()

// T15 — get-user-stories falls back to main boardId when UserStoryBoardId is null (legacy)
[Fact]
public async Task GetUserStories_FallsBackToMainBoardId_WhenUserStoryBoardIdIsNull()

// T16 — get-user-stories: board not found in DB → uses incoming boardId directly
[Fact]
public async Task GetUserStories_UsesIncomingBoardId_WhenProjectBoardNotFound()
```

### 9c. Board Stats DTO tests

```csharp
// T17 — Board stats DTO includes UserStoryBoardUrl when ProjectBoard has it set
[Fact]
public async Task BoardStatsDto_IncludesUserStoryBoardUrl_WhenFieldIsSet()

// T18 — Board stats DTO UserStoryBoardUrl is null for legacy boards
[Fact]
public async Task BoardStatsDto_UserStoryBoardUrlIsNull_ForLegacyBoards()
```

### 9d. Configuration / flag tests

```csharp
// T19 — CreateUserStoryBoard=false: no second board created, response fields null
[Fact]
public async Task CreateProjectWithSprintsAsync_NoUserStoryBoard_WhenFlagFalse()

// T20 — CreateUserStoryBoard=true with null modules: logs warning, still creates board
[Fact]
public async Task CreateProjectWithSprintsAsync_HandlesNullModules_Gracefully()
```

---

## 10. Implementation Order

1. **DB migration** — add two nullable columns, run migration, verify schema.
2. **`TrelloModels.cs`** — add `CreateUserStoryBoard` to `TrelloConfig`; add two fields to `TrelloProjectCreationResponse`.
3. **`ProjectBoard.cs`** — add `UserStoryBoardId` + `UserStoryBoardUrl` properties.
4. **`TrelloService.cs`** — implement `CreateUserStoryBoardAsync`; update `CreateProjectWithSprintsAsync` to strip list and call new method; pass `modules` parameter through.
5. **`BoardsController.cs`** — fetch modules before board creation; save new response fields to `ProjectBoard`; update `get-user-stories` routing.
6. **Board stats DTO** — add `UserStoryBoardUrl`; map from `ProjectBoard`.
7. **`appsettings.json`** — add `"CreateUserStoryBoard": false` (leave off for now, enable when ready to test).
8. **Unit tests** — write and pass all T1–T20.
9. **Frontend: `TrelloButton.jsx`** — add conditional second button using `boardStats.userStoryBoardUrl`.
10. **Enable flag** — set `"CreateUserStoryBoard": true` in staging; create a test board and verify end-to-end.

---

## 11. Rollback / Backward Compatibility

- Flag defaults to `false` — existing boards and all existing behavior are entirely unchanged.
- `get-user-stories` null-coalesces: `board?.UserStoryBoardId ?? boardId` — legacy boards with no `UserStoryBoardId` continue to query the main board.
- The second Trello button only renders when `userStoryBoardUrl` is non-null — no UI change for legacy boards.
- DB columns are nullable — no migration backfill, no downtime risk.

---

## 12. Open Questions (resolve before implementation)

| # | Question |
|---|---|
| 1 | Should the User Story board name follow a specific convention? Proposed: `"{ProjectTitle} — User Stories"` |
| 2 | Should the User Story board be added to the same Trello workspace/organization as the main board? (Assumed yes — pass same `organizationId`) |
| 3 | What happens if `CreateUserStoryBoard=true` but the Trello API fails to create the User Story board — should main board creation abort, or succeed with a warning? |
| 4 | Should there be an admin utility endpoint to retroactively create a User Story board for an existing `ProjectBoard` that has `UserStoryBoardId=null`? |
| 5 | When a project is re-boarded (board recreation), should the User Story board be deleted and recreated, or updated in place? |
