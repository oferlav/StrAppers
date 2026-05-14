# Institute Migration Plan

> **Status:** NOT STARTED — planning only  
> **Scope:** Close all blind spots created by the introduction of `InstituteProject`, `InstituteProjectModule`, `InstituteTemplate`, `InstituteRole`, and `InstituteSquad` tables.

---

## Background

The Institute* tables were added to allow institutes to own custom content alongside the built-in catalog:

| New Table | Replaces / extends |
|---|---|
| `InstituteProject` | `Project` — institute-owned project/course; `IsBuiltIn` flag marks a catalog copy |
| `InstituteProjectModule` | `ProjectModule` — modules for an institute project |
| `InstituteTemplate` | Saved Trello board JSON per institute per project (`CourseType`, `RoleCount`, `VisableModuleDesign`) |
| `InstituteRole` | Role definitions on an institute template |
| `InstituteSquad` | Squad grouping inside an institute |

### Root-cause ambiguity

`ProjectBoard.ProjectId` can now contain **either** a `Project.Id` (catalog) **or** an `InstituteProject.Id` (custom). There is no separate FK column and no discriminator — this is the root cause of most blind spots below.

---

## Severity legend
- 🔴 **Critical** — wrong data or silent failure in production flows
- 🟠 **Important** — feature works for catalog boards, breaks for institute boards
- 🟡 **Minor** — data integrity / edge case / UX gap

---

## Module 1 — Board Creation

> Covers: `BoardsController.CreateBoard`, `CourseBoardBuilderService`, `TrelloSprintMergeService`, frontend `TaskBuilder.jsx` / `taskBuilderStrAppers.js`.

### 🔴 1.1 — `CreateBoard` ignores `InstituteTemplate.TrelloBoardJson`
**File:** `Controllers/BoardsController.cs` (CreateBoard endpoint)  
**Gap:** Board creation reads only `Project.TrelloBoardJson`. There is no `InstituteTemplateId` parameter. Boards for institute-owned courses are either created from the wrong catalog JSON or fail entirely — the institute-saved template is never used.  
**Fix:** Add optional `InstituteTemplateId` to the create-board request; load `TrelloBoardJson` from `InstituteTemplate`; copy `VisableModuleDesign`, `CourseType`, `RoleCount` from that row to the new `ProjectBoard`.

### 🔴 1.2 — `CourseBoardBuilderService` always reads `Project.TrelloBoardJson`
**File:** `Services/CourseBoardBuilderService.cs`  
**Gap:** `BuildAsync` resolves the Trello board JSON from `project.TrelloBoardJson`. Even though it conditionally queries `InstituteProjectModules` (via the `request.InstituteProject` flag), it never reads an `InstituteTemplate` as the source JSON, so module cards are built from the wrong template.  
**Fix:** Accept `InstituteTemplateId` in the build request; load JSON from `InstituteTemplate`; propagate `VisableModuleDesign` from the template row to the created `ProjectBoard`.

### 🟠 1.3 — `TrelloSprintMergeService` hardcoded to `Projects` table
**File:** `Services/TrelloSprintMergeService.cs` (≈ line 56)  
**Gap:** `_context.Projects.FirstOrDefaultAsync(p => p.Id == projectId)` returns null for institute boards — sprint add/merge silently fails.  
**Fix:** Check `ProjectBoard`; fall back to `InstituteProject` when no catalog `Project` matches `projectId`.

### 🟠 1.4 — Frontend `postCourseBoardBuild` has no `InstituteTemplateId` parameter
**File:** `src/api/taskBuilderStrAppers.js`; `src/pages/TaskBuilder.jsx`  
**Gap:** The API call that triggers board creation never passes `InstituteTemplateId`, so even once the backend supports it the frontend cannot drive the flow.  
**Fix:** Add `instituteTemplateId` to the build payload in `TaskBuilder.jsx`.

### 🟡 1.5 — `ProjectBoard.ProjectId` carries dual cardinality with no DB constraint
**File:** `Models/ProjectBoard.cs`; database schema  
**Gap:** `ProjectId INT` stores either a `Project.Id` or an `InstituteProject.Id` with no FK constraint to either table. Any EF query that eagerly loads `.Project` on a board backed by an `InstituteProject` returns null; any raw SQL join silently drops those rows.  
**Fix (long-term):** Add explicit `InstituteProjectId INT NULL` FK column; enforce exactly one of (`ProjectId`, `InstituteProjectId`) is non-null via a DB CHECK constraint. Add `public virtual InstituteProject? InstituteProject { get; set; }` navigation and make `Project` nullable. Migration required.

---

## Module 2 — Team Builder

> Covers: `StudentTeamBuilderService`, `StudentTeamBuilderService` role-variant logic, and the `InstituteSquad` / `InstituteRole` tables that should drive it.

### 🔴 2.1 — `StudentTeamBuilderService` has no InstituteId awareness
**File:** `Services/StudentTeamBuilderService.cs`  
**Gap:** Service resolves project via `projectBoard.ProjectId` treated as a catalog `projectId`. No logic distinguishes institute vs catalog boards. Squad formation, role assignment, and team composition all assume catalog `Role` rows — `InstituteRole` and `InstituteSquad` are never consulted.  
**Fix:** Pass `board.InstituteId`; when forming teams for institute boards, read roles from `InstituteRole` / `InstituteSquad` instead of `Role`.

### 🔴 2.2 — No role-based (`CourseType = "Role"`) teaming support
**File:** `Services/StudentTeamBuilderService.cs`  
**Gap:** Role-type courses assign `RoleCount` students per team, each playing the **same role** across all sprints but differentiated by a variant index (Role1, Role2, …RoleN). The service has no concept of this — it applies standard squad-merge logic and would randomly mix students into mismatched label slots.  
**Fix:** Add a role-variant assignment phase: for Role-type boards, assign each student a stable variant index (1…`RoleCount`); this index drives which Trello label suffix (`RoleName1`, `RoleName2`, …) they use per sprint.

### 🟡 2.3 — No persistence for role-variant assignment
**File:** `Models/` (missing table)  
**Gap:** Even once 2.2 is implemented, there is nowhere to store which variant index a student is assigned. `StudentRole` has no `VariantIndex` column, and no `StudentRoleVariant` join table exists.  
**Fix:** Create `StudentRoleVariant (StudentId, BoardId, VariantIndex)` join table, or add `VariantIndex INT NULL` to `StudentRole`.

---

## Module 3 — Student Project Selection

> Covers: `ProjectsStudentV2.jsx`, `projectsStudentStrAppers.js`, `ProjectsController.GetAvailableProjects`, `StudentsController.AllocateWithPriority` / `IsAllocatable` / `GetAllocatedProjects`.

> **Context:** `ProjectsStudentV2.jsx` calls five backend endpoints on load. All five are catalog-only and none understand `InstituteProject`, `InstituteId`, or `CourseType`. The correct union logic already exists in `GET /api/Projects/use/by-institute` (institute admin endpoint, line 1291) — none of it reaches the student view.

### 🔴 3.1 — `GET /api/Projects/use/available` returns only catalog `Projects`
**File:** `Controllers/ProjectsController.cs` (line 1241); `src/api/projectsStudentStrAppers.js` (line 22)  
**Gap:** Query is `_context.Projects.Where(p => p.InUse)` — zero `InstituteProject` rows ever returned. A student at an institute sees only the global catalog, not the custom courses their institute has configured.  
**Fix:** Student-facing endpoint must union `Projects (InUse=true)` with `InstituteProjects (InUse=true, InstituteId = session institute)`. Use `GET /api/Projects/use/by-institute` logic (line 1291) as the reference implementation.

### 🔴 3.2 — Entire allocation flow is catalog-only
**Files:** `Controllers/StudentsController.cs` (lines 216–293, 1285, 1807)  
**Gap:** `AllocateWithPriority` writes `Student.ProjectId = projectId1` and `Student.ProjectPriority1–4`, all FKs to the `Projects` table. Passing an `InstituteProject.Id` here produces a FK violation or silent mismapping. `IsStudentAllocatable` and `GetAllocatedProjects` are likewise catalog-only.  
**Fix:** Extend allocation endpoints to accept an `isInstituteProject` flag; route storage to `Student.InstituteProjectId` / `Student.InstituteProjectPriority1–4` (requires model migration — see 3.3).

### 🔴 3.3 — `Student.ProjectPriority1–4` and `Student.ProjectId` FK only to `Project`
**File:** `Models/Student.cs`  
**Gap:** Five student fields reference only the `Projects` table. Students cannot express or store preferences for institute-owned projects. This is the data-model root cause behind 3.2.  
**Fix:** Add `int? InstituteProjectId` + `int? InstituteProjectPriority1–4` columns to `Student`; enforce exactly one of (`ProjectId`, `InstituteProjectId`) is set for allocated students. Migration required.

### 🟠 3.4 — No institute-scoped filtering — all students see the same catalog
**File:** `Controllers/ProjectsController.cs` (line 1241)  
**Gap:** `GetAvailableProjects` has no `InstituteId` context. A student at Institute A sees the same list as a student at Institute B, including projects their institute has not activated.  
**Fix:** Resolve `InstituteId` from the student's session; only show catalog projects the institute has set `InUse = true` for, plus the institute's own `InstituteProjects`.

### 🟠 3.5 — No squad composition or role requirements shown in project cards
**File:** `src/pages/ProjectsStudentV2.jsx`; `Controllers/ProjectsController.cs` (line 1241)  
**Gap:** Project cards show only a headcount (`teamSize`). There is no display of what roles the project needs, how many spots are open per role, or — for Role-type courses — how many variant slots are still available. Students cannot make an informed choice.  
**Fix:** Add `requiredRoles` array and per-role vacancy count to the projects list API response; render role chips / vacancy badges in the project card UI.

### 🟠 3.6 — Role-type course selection: no role-variant slot UI
**File:** `src/pages/ProjectsStudentV2.jsx`  
**Gap:** When a student selects a Role-type project (`CourseType = 'Role'`, `RoleCount > 1`), the UI has no concept of "which variant slot am I taking?" (e.g., Frontend Developer 1 vs Frontend Developer 2). The student allocates blind; which label suffix they end up with is undefined.  
**Fix:** After selecting a Role-type project, present the available variant slots; the student picks one; allocation stores the chosen index (requires `StudentRoleVariant` table — see 2.3).

### 🟡 3.7 — `InstituteProject.CriteriaIds` never reaches the section-grouping logic
**File:** `Controllers/ProjectsController.cs` (line 1241); `src/pages/ProjectsStudentV2.jsx` (lines 74–101)  
**Gap:** `ProjectsStudentV2` groups projects into labelled sections via `p.CriteriaIds`. `InstituteProject` has a `CriteriaIds` column, but since 3.1 means institute rows never appear in the list, criteria grouping never applies to them. When 3.1 is fixed the response shape must align.  
**Fix:** Ensure the unified endpoint returns `CriteriaIds` for both catalog and institute projects under the same field name so the existing frontend grouping logic works unchanged.

---

## Module 4 — AI Entities (Customer & Mentor)

> Covers: `CustomerController.cs`, `MentorController.cs` (and sub-controllers: `StoryReview.cs`, `CrmReview.cs`, `FigmaFrameReview.cs`, `ResourceReview.cs`), `MetricsController.GapAnalysis.cs`.  
> Note: `ProjectModuleLookup.FindByBoardScopeAsync()` already exists in `Utilities/ProjectModuleLookup.cs` and correctly falls back from `ProjectModules` to `InstituteProjectModules`. It is currently used in ~4 places. Every gap below is a call site that should use it but doesn't.

### 🔴 4.1 — AI Customer: module context blind to `InstituteProjectModules`
**File:** `Controllers/CustomerController.cs` (lines 111–120)  
**Gap:**
```csharp
_context.ProjectModules
    .Where(m => m.ProjectId == projectId && m.Sequence == sequenceForSprint)
```
When `projectId` is an `InstituteProject.Id`, this returns zero rows → the AI Customer receives `"(No module context for this project/sprint.)"` and plays a customer who knows nothing about the project.  
**Fix:** Replace with `ProjectModuleLookup`-based lookup; load all modules for the board scope and filter by `Sequence`.

### 🔴 4.2 — AI Mentor: module description lookup blind to `InstituteProjectModules`
**File:** `Controllers/MentorController.cs` (≈ lines 462–489) and review sub-controllers (`StoryReview.cs`, `CrmReview.cs`, `FigmaFrameReview.cs`, `ResourceReview.cs`, `MetricsController.GapAnalysis.cs`)  
**Gap:** After reading `ModuleId` from a Trello card's custom field:
```csharp
_context.ProjectModules
    .FirstOrDefaultAsync(pm => pm.Id == moduleId && pm.ProjectId == project.Id)
```
When the board is an institute project, `project.Id` is an `InstituteProject.Id` — this returns null → module descriptions are absent from every review prompt.  
**Fix:** Replace every occurrence with `ProjectModuleLookup.FindByBoardScopeAsync(context, moduleId, board.ProjectId)`.

### 🟠 4.3 — AI Mentor: context query loads only `Project` navigation, not `InstituteProject`
**File:** `Controllers/MentorController.cs` (GetMentorContext, ≈ line 262)  
**Gap:** The context query includes `.ThenInclude(pb => pb.Project)`. For institute boards, `projectBoard.Project` is null → project title and description are missing from the mentor prompt.  
**Fix:** Add a parallel include for `InstituteProject`; resolve which one applies via `board.ProjectId`; use whichever is non-null for title/description.

### 🟡 4.4 — AI Mentor: UC3 boards have no Trello User Stories list → user story card always missing
**Gap:** `GetLinkedUserStoryCardAsync` fetches the user story card from the Trello "User Stories" list matched by `ModuleId`. UC3 boards (`VisableModuleDesign = true`) have no such list — the call returns null gracefully, but the mentor prompt lacks the user story context that UC1/UC2 boards have.  
**Fix / Mitigation:** When `board.VisableModuleDesign = true`, substitute the module description from `ProjectModuleLookup` as the user-story block in the prompt (the same data now shown in the Sprint Details modal).

### 🟡 4.5 — AI Customer: no `VisableModuleDesign` product decision (UC3)
**Gap:** UC3 boards have no Customer Engagement role — "who is the AI Customer?" is undefined. The button currently appears and works, but the context it gives the AI is for a customer role that doesn't exist in the course.  
**Fix:** Make a product decision; likely: hide the AI Customer button when `board.VisableModuleDesign = true`, or replace it with a different AI interaction suited to UC3.

---

## Cross-cutting: things outside the 4 modules

These gaps don't belong to any single module above but must be addressed as part of the migration:

### 🟠 X.1 — Metrics & Reporting: module lookups query `ProjectModules` only
**File:** `Controllers/MetricsController.cs` (GapAnalysis and related endpoints)  
**Gap:** Gap analysis, customer engagement metrics, and attendance metrics resolve module descriptions via `_context.ProjectModules`. Institute project modules are invisible — metrics are wrong for institute boards.  
**Fix:** Apply `ProjectModuleLookup.FindByBoardScopeAsync()` throughout `MetricsController`.

### 🟡 X.2 — `StudentMeetingsController`: meeting records lose institute context
**File:** `Controllers/StudentMeetingsController.cs`  
**Gap:** Meeting records are linked via `Student.ProjectId` (catalog FK). When a student is on an institute project, meeting context (which module/sprint the 1:1 covers) is not resolvable.  
**Fix:** Resolve institute context from `ProjectBoard.InstituteId` when recording and querying meetings.

### 🟡 X.3 — No cohort/instance support for `InstituteProject`
**Gap:** `ProjectInstances` only links to `Project`. Institute projects cannot run multiple cohorts simultaneously.  
**Fix:** Create `InstituteProjectInstances` or extend `ProjectInstances` with a nullable `InstituteProjectId` FK.

---

## Gap Summary

| Module | 🔴 Critical | 🟠 Important | 🟡 Minor | Total |
|---|---|---|---|---|
| 1. Board Creation | 2 (1.1, 1.2) | 2 (1.3, 1.4) | 1 (1.5) | **5** |
| 2. Team Builder | 2 (2.1, 2.2) | — | 1 (2.3) | **3** |
| 3. Student Project Selection | 3 (3.1, 3.2, 3.3) | 2 (3.4, 3.5, 3.6) | 1 (3.7) | **7** |
| 4. AI Entities | 2 (4.1, 4.2) | 1 (4.3) | 2 (4.4, 4.5) | **5** |
| Cross-cutting | — | 1 (X.1) | 2 (X.2, X.3) | **3** |
| **Total** | **9** | **9** | **7** | **25** |

---

## Suggested Implementation Order

1. **Model / DB migration** — add `ProjectBoard.InstituteProjectId` FK + nullable `Project` nav; add `Student.InstituteProjectId` + `Student.InstituteProjectPriority1–4`; add `StudentRoleVariant` table. Everything else builds on this foundation.
2. **`ProjectModuleLookup` sweep** — replace every bare `_context.ProjectModules.Where(m => m.ProjectId == ...)` with the existing utility. Closes AI Customer 4.1, AI Mentor 4.2, Metrics X.1 in one pass.
3. **Board creation from `InstituteTemplate`** — backend `CreateBoard` + `CourseBoardBuilderService` accept `InstituteTemplateId`; frontend `postCourseBoardBuild` passes it (gaps 1.1–1.4).
4. **Project selection — unified endpoint** — extend `GET /api/Projects/use/available` to union catalog + institute projects with institute-scoped filtering; add role requirement / vacancy data (gaps 3.1, 3.4, 3.5, 3.7).
5. **Allocation flow** — extend `AllocateWithPriority` + `IsAllocatable` + `AllocatedProjects` to handle `InstituteProject.Id`; add role-variant slot selection UI for Role-type courses (gaps 3.2, 3.3, 3.6).
6. **Team builder** — InstituteId / `InstituteRole` / `InstituteSquad` awareness; role-variant assignment for Role-type courses (gaps 2.1–2.3).
7. **AI Mentor context & UC3 fallback** — load `InstituteProject` nav; substitute module description for missing user story card (gaps 4.3, 4.4).
8. **Data integrity & cross-cutting** — XOR constraint on `ProjectBoard.ProjectId`; meetings institute context; cohort instances for institute projects (gaps 1.5, X.2, X.3).

---

*Last updated: 2026-05-14*
