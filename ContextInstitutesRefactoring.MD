# Context Institutes Refactoring Plan

## Problem

All AI features (Customer, Mentor, Assessment) resolve project context using
`ProjectBoards.ProjectId`, which always points to the **base catalog project**
(`Projects` table). For institute boards (`InstituteProjectId` is set on
`ProjectBoard`), the correct content lives in `InstituteProjects` and
`InstituteProjectModules` instead.

Two broken lookup patterns:

**Pattern A — scalar fields** (Description, CustomerPastStory):
Every controller does `Projects WHERE Id = board.ProjectId` — always hits the
base catalog row, never the institute-specific content.

**Pattern B — module rows** (Title, Description by ID or by Sequence):
`ProjectModuleLookup.FindByBoardScopeAsync` tries both tables but uses
`board.ProjectId` (= BaseProjectId) as the key for
`InstituteProjectModules.InstituteProjectId` — which never matches.

---

## Root Cause

`ProjectBoards.InstituteProjectId` was only added in migration
`20260527120000_AddInstituteProjectIdToProjectBoards`. Before this, there was
no direct FK from a board to its institute project. All content lookups used
`ProjectBoards.ProjectId` (the base catalog project).

---

## Fix Plan

### Step 1 — Update `Utilities/ProjectModuleLookup.cs`

- Add `int? boardInstituteProjectId` parameter to `FindByBoardScopeAsync`.
  When set, try `InstituteProjectModules WHERE InstituteProjectId = boardInstituteProjectId`
  first, then fall back to `ProjectModules WHERE ProjectId = boardProjectId`.
- Add `FindBySequenceAsync(context, sequence, boardProjectId, boardInstituteProjectId?)`
  for the CustomerController sequence-based lookup.

### Step 2 — Add `Utilities/ProjectContextHelper.cs` (new file)

```
GetEffectiveProjectDataAsync(context, boardProjectId, boardInstituteProjectId?)
  → returns (Description, CustomerPastStory, DataSource)
     from InstituteProjects when InstituteProjectId is set, else Projects
```

DataSource string = `"InstituteProjects.Id={id}"` or `"Projects.Id={id}"`.
Avoids repeating the same if/else across 7 callers.

### Step 3 — Update 8 callers

In each: load `board.InstituteProjectId`, pass it to the helpers above.
When `Debug:AiContext` is true, call `AiContextDebugLogger` after resolving
context (see Debug Switches below).

### Step 4 — Add `Utilities/AiContextDebugLogger.cs` (new file)

Static helper called from each of the 8 controllers when `Debug:AiContext = true`.
Builds a log entry and emails it to `ofer@skill-in.com`:

```
=== AI Context Debug ===
Timestamp:  2026-05-28 10:00:00Z
Endpoint:   POST /api/Customer/use/respond/{model}
BoardId:    6a1728ca735d1cd243931642
StudentId:  73
Sprint:     1
BoardInstituteProjectId: 57

--- Project Context (Pattern A) ---
Source:     InstituteProjects.Id=57
Description[0..200]:       This initiative establishes...
CustomerPastStory[0..200]: The customer has been...

--- Module Context (Pattern B) ---
Source:     InstituteProjectModules (InstituteProjectId=57)
ModuleId:   12
ModuleTitle: Sprint 1 — Donor Submission System
```

Also includes a `contextDataSource` object in the API response so the
FE debug panel can display it when the panel is open.

---

## Debug Switches

### appsettings.json — add to your local file (never committed):

```json
"Debug": {
  "BoardCreation": false,
  "AiContext": false
}
```

Azure App Service env vars: `Debug__BoardCreation`, `Debug__AiContext`  
Default for both: `false`

### BE switch (`Debug:AiContext`)

Controlled by `private bool DebugAiContext => _configuration.GetValue<bool>("Debug:AiContext", false);`
in each affected controller.

When `true`:
- After resolving context, calls `AiContextDebugLogger.LogAndEmailAsync(...)` 
- Includes `contextDataSource` object in the AI response JSON

### FE switch

A hardcoded constant in the frontend code. To enable/disable, tell Claude and it will
flip the value and push. No appsettings or UI toggle needed.

---

## Affected Files

| File | Pattern A (scalar) | Pattern B (modules by ID) | Modules by Sequence |
|---|---|---|---|
| `Controllers/CustomerController.cs` | Description + CustomerPastStory | — | ✓ |
| `Controllers/MentorController.cs` | Description | ✓ | — |
| `Controllers/MentorController.StoryReview.cs` | CustomerPastStory | ✓ | — |
| `Controllers/MentorController.CrmReview.cs` | CustomerPastStory | ✓ | — |
| `Controllers/MentorController.FigmaFrameReview.cs` | — | ✓ | — |
| `Controllers/MentorController.ResourceReview.cs` | — | ✓ | — |
| `Controllers/MetricsController.GapAnalysis.cs` | CustomerPastStory | ✓ | — |
| `Controllers/MetricsController.CustomerEngagement.cs` | CustomerPastStory | — | — |

---

## Verification (replaces Swagger testing)

1. Set `"Debug": { "AiContext": true }` in `appsettings.json` and deploy (or set env var)
2. Open BoardRoom for an **institute board** (one with `InstituteProjectId` set)
3. Send a few prompts to each AI — Mentor, Customer, Assessment
4. Check email — each call should produce a log showing `InstituteProjects.Id=X`
   and `InstituteProjectModules` as the source
5. Repeat with a **catalog board** (`InstituteProjectId = null`) — should show
   `Projects.Id=X` and `ProjectModules`
6. If FE debug is on (tell Claude to enable it), the UI displays the
   `contextDataSource` from each AI response inline

The fix is confirmed when institute boards consistently log `InstituteProjects`
and `InstituteProjectModules` sources, and the content snippets differ from
the catalog board equivalents.

---

## Testing Checklist

- [ ] Customer (institute board): `projectSource = InstituteProjects.Id=X`
- [ ] Customer (catalog board): `projectSource = Projects.Id=X`
- [ ] Mentor main context (institute): both `projectSource` and `moduleSource` show institute tables
- [ ] Mentor story review (institute): `moduleSource = InstituteProjectModules`
- [ ] Mentor CRM review (institute): both sources show institute tables
- [ ] Mentor Figma review (institute): `moduleSource = InstituteProjectModules`
- [ ] Mentor resource review (institute): `moduleSource = InstituteProjectModules`
- [ ] Gap Analysis (institute): both sources show institute tables
- [ ] Customer Engagement (institute): `projectSource = InstituteProjects.Id=X`
- [ ] Description/CustomerPastStory snippets in email differ between institute and catalog boards

---

## Notes

- `ProjectModuleLookup.FindByBoardScopeAsync` already exists in
  `Utilities/ProjectModuleLookup.cs` — just needs the correct key passed in.
- `InstituteProject` has all the same scalar fields as `Project`:
  `Description`, `CustomerPastStory`, `SystemDesign`, `Mission`, `OneLiner`,
  `TrelloBoardJson`.
- `InstituteProjectModule` implements `IProjectModuleRow` (same as
  `ProjectModule`) — no interface changes needed.
- `ProjectBoards.InstituteProjectId` was added in migration
  `20260527120000_AddInstituteProjectIdToProjectBoards` and is set during
  board creation in `BoardsController.cs` when `InstituteProject` is provided.
- `AiContextDebugLogger` follows the same pattern as `FlushDebugLog` in
  `BoardsController` — failures are silently ignored, never interrupts AI flow.
