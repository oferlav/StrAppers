# Option A3: Roles Table Consolidation — Implementation Plan

## What We're Doing

Add `InstituteId` (**non-nullable** FK, default `1`), `SquadId` (nullable FK), and `IsTechnical` (bool) to the `Roles` table so that all role variants live in one table.

### The rule after A3:
| InstituteId | SquadId | Meaning |
|---|---|---|
| `1` | `null` | Global / B2C role (platform institute — Id=1 always exists) |
| `X` | `null` | Institute base role (replaces `InstituteRole`) |
| `X` | `Y` | Squad-scoped role (replaces `InstituteSquadRole`) |

`InstituteId` is **non-nullable with default = 1**. No null checks anywhere — global = `InstituteId = 1`, institute-specific = `InstituteId = X`. Requires an `Institutes` row with `Id = 1` (the platform institute).

`StudentRole.RoleId` continues pointing to `Roles.Id` — no change. Institute students now get a real FK to a row with their data, no name-matching needed.

---

## Biggest Risk ⚠️

**`StudentRegistration.jsx:31` and `FigmaViewerModal.jsx:19` both hardcode `roleId === 4`.**

After A3 institute UI/UX students have a new `Role.Id` (not 4). Both checks silently fail — no error, no log. The student registers without Figma OAuth and can't edit their Figma URL in the board room.

**Fix:** Use `Role.SkillId === 3` (Figma skill; Skills table: 1=GitHub, 2=Trello, 3=Figma, 4=CRM). `StudentRegistration.jsx` already has `skillId` in the roles API response. `FigmaViewerModal` needs one field (`RoleSkillId`) added to `GetStudentByEmail` — the data is already loaded via `.ThenInclude(sr => sr.Role)`, just not projected.

**Secondary risk:** Missing a `WHERE InstituteId = 1` filter on any of the ~8 global-role query sites causes institute roles to bleed into every role dropdown seen by all users and employers.

---

## RolesConfig Screen Invariant

**A new institute admin opens the Roles Config screen:**
1. `GET /api/Roles/use/institute/{X}` returns empty (no institute roles yet)
2. The frontend `available` panel falls back to `fetchAllRoles()` → global roles (`InstituteId = 1`) ✓
3. Admin picks roles, customises CE/IsTechnical/SkillId, and saves

**On save — the safety rule:**
- Backend creates a **new** `Role` row with `InstituteId = X`
- The original global row (`InstituteId = null`) is **never modified**

This is already enforced today: in the squad path (`RolesController.cs:294`) the backend only treats a DTO's `Id` as "update existing" when it matches an `existingSquadIds` set (IDs of existing `InstituteSquadRole` rows for that squad). A global role's ID won't be there → always creates new. After A3 the equivalent check must be: **never update a `Roles` row where `InstituteId = 1`** — guard this explicitly in the save logic.

---

## Phases

---

### Phase 1 — Schema + Model

**Migration — 4 new columns on `Roles`:**

| Column | Type | Default | Constraint |
|--------|------|---------|------------|
| `InstituteId` | `int` | `1` | NOT NULL, FK → `Institutes.Id` ON DELETE RESTRICT |
| `SquadId` | `int?` | `null` | FK → `InstituteSquads.Id` ON DELETE SET NULL |
| `IsTechnical` | `bool` | `false` | — |
| `Competencies` | `text?` | `null` | — |

`InstituteId` is **non-nullable**. All existing rows get `InstituteId = 1` (platform/B2C) as part of the migration. Requires `Institutes` row with `Id = 1` to exist before migration runs.

Note: `Role` already has `Description` (varchar 500). `Competencies` is a separate free-text field used by `InstituteRole` and `InstituteSquadRole` for technical competency expectations. Both fields are kept — they serve different purposes.

`CustomerEngagement` already exists on `Role.cs:33` — no change.

**`Role.cs` additions:**
```csharp
public int InstituteId { get; set; } = 1;
public Institute Institute { get; set; } = null!;
public int? SquadId { get; set; }
public InstituteSquad? Squad { get; set; }
public bool IsTechnical { get; set; } = false;
public string? Competencies { get; set; }
```

**`ApplicationDbContext.cs`:** configure non-nullable FK for `InstituteId` (default value `1`), optional FK for `SquadId`, add `InstituteSquad.Roles` as `ICollection<Role>` nav property via `SquadId` FK.

**Drop `InstituteSquadRoles` table** — after data migration (Phase 9).

**Tests (`InstituteRoleSchemaTests.cs`):**
```
Role_WithInstituteId1_AndNullSquadId_IsGlobalRole
Role_WithInstituteIdX_AndNullSquadId_IsBaseScopedRole
Role_WithInstituteIdX_AndSquadId_IsSquadScopedRole
Role_DefaultInstituteId_Is1
Squad_RolesNavigation_ReturnsOnlyRowsWithMatchingSquadId
Migration_AllExistingRoles_HaveInstituteId1AfterMigration
Role_Competencies_CanBeNullForGlobalRoles_AndPopulatedForInstituteRoles
```

---

### Phase 2 — Update `InstituteSquad.Roles` Navigation

`InstituteSquad.Roles` changes from `ICollection<InstituteSquadRole>` to `ICollection<Role>`.

**Files to update:**

| File | Line | What changes |
|------|------|-------------|
| `StudentsController.cs` | 1104 | `.SelectMany(t => t.Squad!.Roles.Where(r => r.IsActive))` → now returns `Role` objects directly |
| `StudentTeamBuilderService.cs` | 160 | Squad includes — same query, `squadRole.Name` still works (Name is on Role) |
| `CourseBoardBuilderService.cs` | 195–213 | Squad role load maps `InstituteSquadRole → InstituteRole` DTO; after A3 squad roles ARE `Role` objects — mapping simplifies or disappears |

**Access pattern change:** Any `squadRole.Role.Name` / `squadRole.Role.Type` (navigation from `InstituteSquadRole` through to `Role`) becomes `squadRole.Name` / `squadRole.Type` directly. Grep: `\.Role\.Name`, `\.Role\.Type`, `\.Role\.CustomerEngagement`, `\.Role\.IsTechnical` in files that deal with `InstituteSquadRole`.

**Tests (`InstituteSquadNavTests.cs`):**
```
Squad_Roles_ReturnsRoleObjects_NotSquadRoleObjects
Squad_Roles_FiltersByIsActive
StudentTeamBuilder_SquadRoleLoad_WorksWithRoleNavProperty
```

---

### Phase 3 — Global Role Query Filtering

Every endpoint that returns the role catalog to non-institute contexts must add `WHERE InstituteId IS NULL`.

| File | Line | Endpoint | Filter to add |
|------|------|----------|--------------|
| `RolesController.cs` | ~641 | `GET /api/Roles/use` | `r.InstituteId == 1 && r.IsActive` |
| `RolesController.cs` | ~663 | `GET /api/Roles/use/all` | `r.InstituteId == 1` |
| `RolesController.cs` | ~708 | `GET /api/Roles/by-category/{category}` | `r.InstituteId == 1` |
| `RolesController.cs` | ~510 | `IsGlobalCatalogRole` check | `r.InstituteId == 1` |
| `StudentsController.cs` | ~1115 | `GetRolesByCoupon` name-match fallback | `r.InstituteId == 1` |
| `StudentsController.cs` | ~1134 | `GetRolesByCoupon` default fallback | `r.InstituteId == 1` |
| `TestController.cs` | ~1081, 2002 | Role count / list | add filter or note as test-only |
| `BoardsController.GitHubStats.cs` | ~295 | Role lookup | inspect — add filter if catalog query |

**No filter needed (direct FK lookup by a role ID already scoped to the caller):**
- `MetricsController.AssessmentReport.cs:104, 169` — `Roles WHERE Id = student.RoleId`
- `MentorController.CacheReviewInsert.cs:98` — joins via `sr.RoleId`
- `StudentsController.cs:373` — `FindAsync(roleId)`
- `RolesController.cs:688, 734` — `FindAsync(id)` admin lookup

**Tests (`GlobalRoleFilterTests.cs`):**
```
GetActiveRoles_ReturnsOnlyInstituteId1Roles
GetAllRoles_ReturnsOnlyInstituteId1Roles
GetRolesByCategory_ReturnsOnlyInstituteId1Roles
GetActiveRoles_WhenInstituteRoleHasSameNameAsGlobal_ReturnsOnlyOneResult
```

---

### Phase 4 — RolesController: Institute Role CRUD

`SaveInstituteRoles` currently writes to `InstituteRoles` (base path) and `InstituteSquadRoles` (squad path). After A3 both write to `Roles` with `InstituteId` (and `SquadId` for squad scope).

**Safety rule to enforce in save logic:**
```csharp
// Before any update: assert the row being updated belongs to this institute
if (existingRow.InstituteId != request.InstituteId)
    return BadRequest("Cannot modify a role belonging to another scope.");
// Never touch InstituteId == 1 rows — they are global/B2C
if (existingRow.InstituteId == 1)
    return BadRequest("Cannot modify a global role via institute endpoint.");
```

**Endpoints affected:**

| Lines | Endpoint | Change |
|-------|----------|--------|
| ~86–111 | `GET /api/Roles/use/institute/{id}?templateId=X` | Query `Roles WHERE InstituteId=@id AND SquadId=@squadId` |
| ~118–129 | `GET /api/Roles/use/institute/{id}` (no templateId) | Query `Roles WHERE InstituteId=@id AND SquadId=null` |
| ~144–158 | `GET .../assigned-role-names` | `Roles WHERE InstituteId=@id` distinct names |
| ~253–361 | `POST SaveInstituteRoles` (squad path) | Write to `Roles` with `InstituteId`+`SquadId` |
| ~398–479 | `POST SaveInstituteRoles` (base path) | Write to `Roles` with `InstituteId`, `SquadId=null` |
| ~502–555 | `DELETE .../role/{roleId}` | Delete `Roles WHERE InstituteId=@id AND Id=@roleId` |

`IsTechnical` is already in DTOs (`InstituteRolesSaveDtos.cs:63, 80`) — no DTO changes.

**Tests (`InstituteRoleCrudTests.cs`):**
```
SaveInstituteRoles_Base_CreatesNewRolesRow_WithCorrectInstituteId
SaveInstituteRoles_Base_SetsIsTechnical_AndCustomerEngagement_AndSkillId
SaveInstituteRoles_Base_WhenGlobalRoleIdSentInPayload_CreatesNewRow_DoesNotModifyGlobal
SaveInstituteRoles_Squad_CreatesNewRolesRow_WithInstituteIdAndSquadId
SaveInstituteRoles_Squad_WhenGlobalRoleIdSentInPayload_CreatesNewRow_DoesNotModifyGlobal
SaveInstituteRoles_Update_OnlyUpdatesRowsOwnedByThatInstitute
GetInstituteRoles_Base_ReturnsOnlyRowsWithMatchingInstituteId_AndNullSquadId
GetInstituteRoles_Squad_ReturnsOnlyRowsWithMatchingInstituteIdAndSquadId
DeleteInstituteRole_RemovesOnlyInstituteRow_LeavesGlobalRowIntact
```

---

### Phase 5 — GetRolesByCoupon Simplification

**File:** `StudentsController.cs ~1080–1146`

**Current flow (3 steps):**
1. Resolve coupon → squads → `InstituteSquadRoles` (names only)
2. Name-match to global `Roles` to get IDs ← **the broken step**
3. Return mapped IDs

**After A3 (2 steps):**
1. Resolve coupon → squads → `squad.Roles` (returns `Role` objects directly via `SquadId` FK)
2. Return `Role.Id` — no mapping

**Fallback** (no squad roles): return `Roles WHERE InstituteId = 1` (global/B2C only).

**Tests (`GetRolesByCouponTests.cs`):**
```
GetRolesByCoupon_ReturnsSqaudRoleIds_NotGlobalIds
GetRolesByCoupon_ReturnedRoleIds_AreOwnedByThatInstitute
GetRolesByCoupon_IncludesSkillId_IsTechnical_CustomerEngagement_InResponse
GetRolesByCoupon_WhenNoSquadRoles_FallsBackToInstituteId1RolesOnly
GetRolesByCoupon_Fallback_DoesNotIncludeNonPlatformInstituteRoles
CreateStudent_WithInstituteCoupon_StudentRoleIdPointsToInstituteRoleNotGlobal
```

---

### Phase 6 — MetricsController: Simplify ResolveCustomerEngagementAsync

**File:** `MetricsController.cs:617–658`

Current logic checks `role.CustomerEngagement`, then falls back to a name-match against `InstituteSquadRoles` for institute students. After A3, each institute student's `sr.Role` IS the institute-scoped `Role` row with `CustomerEngagement` already set:

```csharp
// Before (complex):
private async Task<bool> ResolveCustomerEngagementAsync(Role? role, string roleName,
    int? studentInstituteId, CancellationToken ct)
{
    if (role?.CustomerEngagement == true) return true;
    if (studentInstituteId > 1) { /* name-match query to InstituteSquadRoles */ }
    return false;
}

// After (simple):
private Task<bool> ResolveCustomerEngagementAsync(Role? role, ...)
    => Task.FromResult(role?.CustomerEngagement == true);
```

**Tests (`ResolveCustomerEngagementTests.cs`):**
```
ResolveCustomerEngagement_InstituteRole_CeTrue_ReturnsTrue
ResolveCustomerEngagement_InstituteRole_CeFalse_ReturnsFalse
ResolveCustomerEngagement_B2cRole_CeTrue_ReturnsTrue
ResolveCustomerEngagement_B2cRole_CeFalse_ReturnsFalse
ResolveCustomerEngagement_NullRole_ReturnsFalse
CustomerEngagement_Endpoint_InstituteDevRole_CeFalse_SkipsLlm
CustomerEngagement_Endpoint_InstituteDevRole_CeTrue_RunsLlm
```

---

### Phase 7 — CourseBoardBuilderService

**File:** `CourseBoardBuilderService.cs`

Squad roles now come from `squad.Roles` as `ICollection<Role>`. All flag reads (`IsTechnical`, `CustomerEngagement`, `SkillId`) already exist on `Role` — no new properties. The mapping from `InstituteSquadRole → InstituteRole` DTO at lines 195–213 simplifies or disappears.

Lines to review:
- `195–213`: squad role load — mapping simplifies
- `207`: **Remove Competencies fallback** — current code: `Competencies = sr.Competencies ?? sr.BaseInstituteRole?.Competencies`. After A3 the squad role IS a `Role` row with `Competencies` already populated from migration. The `BaseInstituteRole` navigation is gone. Change to: `Competencies = role.Competencies`.
- `221–230`: base institute role load → `Roles WHERE InstituteId=@id AND SquadId=null`
- `224`: **Fix latent bug** — `.Where(r => ... && r.TemplateId == request.TemplateId)` always returns empty (all `InstituteRole.TemplateId` are null in DB, confirmed). Remove the `TemplateId` condition; `effectiveRoleIds.Contains(r.Id)` is the correct filter.
- `364`: `role.IsTechnical ? "Technical" : ...` — works once column exists
- `80–94`: CE detection already reads from `Role` — no change

**Tests (`CourseBoardBuilderInstituteRoleTests.cs`):**
```
BuildCourseBoard_InstituteRole_IsTechnicalTrue_UsesTechnicalTrack
BuildCourseBoard_InstituteRole_IsTechnicalFalse_UsesNonTechnicalTrack
BuildCourseBoard_InstituteRole_CustomerEngagementTrue_IncludesCeTasks
BuildCourseBoard_InstituteRole_CustomerEngagementFalse_ExcludesCeTasks
BuildCourseBoard_InstituteRole_SkillId3_IncludesFigmaContext
BuildCourseBoard_GlobalRole_B2c_BehaviorUnchanged
CourseBoardBuilder_InstituteRolesWithNullTemplateId_AreFoundByEffectiveRoleIds
// Setup: InstituteRole rows with TemplateId=null, Ids in effectiveRoleIds
// Act:   call builder with request.TemplateId = <some integer>
// Assert: roles are loaded successfully, no "not found for template" error

BuildCourseBoard_SquadRole_Competencies_LoadedDirectlyFromRole_NotFromBaseInstituteRole
// Setup: squad Role row with Competencies="React, Node.js"; no BaseInstituteRole navigation (gone after A3)
// Act:   build course board
// Assert: role context includes "React, Node.js" — direct property, no null fallback needed
```

---

### Phase 8 — Frontend Fixes + One Backend Change

**The right check for "this role uses Figma" is `Role.SkillId === 3`** — set by the admin, stable across any name or ID changes.

---

**Fix 1 — `StudentRegistration.jsx:31, 467`** (Figma OAuth skipped during registration)

`selectedRole` is already computed on line 466. `RolesController:103` already returns `SkillId` — no API change needed.

```js
// Remove: const UI_UX_DESIGNER_ROLE_ID = '4';
const FIGMA_SKILL_ID = 3; // Skills: 1=GitHub 2=Trello 3=Figma 4=CRM
const isUiUxDesigner = selectedRole?.skillId === FIGMA_SKILL_ID;
```

---

**Fix 2 — `FigmaViewerModal.jsx:19`** (student can't edit Figma URL in board room)

`studentData` comes from `GetStudentByEmail` (`StudentsController.cs:1426`). Role data is already loaded via `.ThenInclude(sr => sr.Role)` (line 1410) — `SkillId` just isn't projected.

**Backend — add one field to the anonymous object at `StudentsController.cs:1448`:**
```csharp
RoleSkillId = roleInfo?.Role?.SkillId,
```

**Frontend — `FigmaViewerModal.jsx:19`:**
```js
const FIGMA_SKILL_ID = 3;
const canEdit = studentData?.roleSkillId === FIGMA_SKILL_ID
  || (studentData?.roleName || '').toLowerCase().includes('marketing')
  || (studentData?.roleName || '').toLowerCase().includes('bizdev');
```

The name-based fallbacks for marketing/bizdev are kept — those roles may not have `SkillId=3`. The `roleId === 4` check is removed entirely.

---

**Fix 3 — `ProjectCard.jsx:68-79`** (cosmetic — role color mapping)

`getRoleColor(roleId)` maps IDs 1–8 to colors. Institute role IDs fall back to default blue. Not functional — no action required.

---

**Fix 4 — `ProjectDetailsV2.jsx` role slots hardcoded** (functional gap for institute students)

The detail panel currently defines role slots as hardcoded name-match patterns (Full Stack Developer, UI/UX Designer, etc.). For institute students whose squads have custom role names, these slots are wrong or missing.

**Backend change** — `GetAvailableInstituteProjectsForStudent` (`ProjectsController.cs:1279`) adds squad roles to each project in the response:
```csharp
// In the project SELECT, resolve: InstituteProject → InstituteTemplate → Squad → Roles
SquadRoles = _context.InstituteTemplates
    .Where(t => t.InstituteProjectId == p.Id && t.IsActive)
    .SelectMany(t => t.Squad.Roles.Where(r => r.IsActive))
    .Select(r => new { r.Name, r.Type })
    .ToList()
```

**Frontend change** — `ProjectDetailsV2.jsx`: when `project.squadRoles` is present, build `requiredSlots` from the API data instead of the hardcoded patterns. Keep hardcoded patterns as fallback for B2C projects (which have no squad).

```js
const requiredSlots = useMemo(() => {
  if (project.squadRoles?.length > 0) {
    return project.squadRoles.map(r => ({
      slotKey: r.name.toLowerCase().replace(/\s+/g, '_'),
      roleLabel: r.name,
      match: (roleName) => roleName?.toLowerCase() === r.name.toLowerCase(),
    }));
  }
  // fallback: existing hardcoded patterns for B2C
  return applicantsCap === 4 ? [ /* existing */ ] : [ /* existing */ ];
}, [project.squadRoles, applicantsCap]);
```

---

**Tests (`RoleSkillIdTests.cs` — backend; checklist for FE):**
```
// Backend (C#):
GetStudentByEmail_WhenRoleHasSkillId3_ReturnsRoleSkillId3
GetStudentByEmail_WhenRoleHasNullSkillId_ReturnsNullRoleSkillId
GetStudentByEmail_InstituteStudentWithFigmaRole_ReturnsCorrectSkillId
GetAvailableInstituteProjects_IncludesSquadRoles_ForEachProject
GetAvailableInstituteProjects_SquadRoles_MatchActiveSquadRolesForTemplate

// Frontend (manual checklist):
// B2C UI/UX student (skillId=3): Figma OAuth shown ✓
// Institute UI/UX student (new roleId, skillId=3): Figma OAuth shown ✓
// Institute Developer student (skillId=1): no Figma OAuth ✓
// Institute UI/UX student in board room: canEdit=true ✓
// Institute project detail panel: shows squad role names not hardcoded defaults ✓
// B2C project detail panel: still shows hardcoded role patterns (fallback) ✓
```

---

### Phase 9 — Data Migration (user-handled) + Table Drop

1. User copies `InstituteRoles` rows into `Roles` with `InstituteId` set, `SquadId = null`
2. User copies `InstituteSquadRoles` rows into `Roles` with `InstituteId` and `SquadId` set
3. After verification: drop tables in this exact order — **`InstituteSquadRoles` first, then `InstituteRoles`**. `InstituteSquadRoles.BaseInstituteRoleId` is a FK to `InstituteRoles.Id`; reversing the order causes a FK constraint violation.
4. Update `GET /api/Boards/use/institute-squad-roles` (`BoardsController`, used by `StaffDashboard.jsx:399`) to query `Roles WHERE InstituteId=@id AND SquadId IS NOT NULL` instead of the dropped `InstituteSquadRoles` table.

---

## File Impact Summary

### Backend — must change:

| File | Change |
|------|--------|
| `Models/Role.cs` | Add `InstituteId`, `SquadId`, `IsTechnical` |
| `Models/InstituteSquad.cs` | `Roles` nav: `ICollection<InstituteSquadRole>` → `ICollection<Role>` |
| `Data/ApplicationDbContext.cs` | Configure new columns, FKs, nav property |
| `Migrations/` | New migration |
| `Controllers/RolesController.cs` | Institute CRUD + global query filters + global-row safety guard |
| `Controllers/StudentsController.cs` | GetRolesByCoupon simplification + fallback filter + add `RoleSkillId` to `GetStudentByEmail` |
| `Controllers/MetricsController.cs` | Simplify `ResolveCustomerEngagementAsync` |
| `Controllers/BoardsController.GitHubStats.cs` | Verify/add `InstituteId IS NULL` filter |
| `Services/CourseBoardBuilderService.cs` | Squad/institute role load paths |
| `Services/StudentTeamBuilderService.cs` | Squad role load (nav property type change) |

### Frontend — must change:

| File | Change |
|------|--------|
| `src/pages/StudentRegistration.jsx` | Replace `roleId === '4'` with `skillId === 3` |
| `src/components/boards/FigmaViewerModal.jsx` | Replace `roleId === 4` with `roleSkillId === 3` |
| `src/components/projects/v2/ProjectDetailsV2.jsx` | Dynamic role slots from `project.squadRoles`; hardcoded fallback for B2C |

### Tests — new files:

| File | Phase | Key invariant covered |
|------|-------|-----------------------|
| `InstituteRoleSchemaTests.cs` | 1 | Role scoping rules |
| `InstituteSquadNavTests.cs` | 2 | `squad.Roles` returns `Role` objects |
| `GlobalRoleFilterTests.cs` | 3 | Global endpoints never return institute roles |
| `InstituteRoleCrudTests.cs` | 4 | Save never touches global rows; reads scoped correctly |
| `GetRolesByCouponTests.cs` | 5 | Returns institute role IDs; fallback is global only |
| `ResolveCustomerEngagementTests.cs` | 6 | CE flag comes from Role directly |
| `CourseBoardBuilderInstituteRoleTests.cs` | 7 | IsTechnical / CE track selection |
| `RoleSkillIdTests.cs` | 8 | `GetStudentByEmail` returns `RoleSkillId`; `GetAvailableInstituteProjects` includes squad roles |

---

## Post-A3: Platform Growth Path (separate refactor)

After A3 ships, the natural next step to enable B2C project catalog management through the same admin UI:

1. Set `Student.InstituteId = 1` for all existing B2C students (data migration on `Students` table)
2. Change `ProjectsController.cs:1287` from `InstituteId <= 1 → BadRequest` to `InstituteId <= 0 → BadRequest`
3. Change `ProjectsStudentV2.jsx:121,318` from `> 1` to `> 0`
4. `ProjectsStudentV2` then calls `invokeGetAvailableInstituteProjects` for Institute=1 students too

**Result:** Platform admin copies a built-in project → `InstituteProject(InstituteId=1, Coupon=null)` → visible to all B2C students. `Coupon=X` → gated to a specific programme. The existing coupon filter at `ProjectsController.cs:1293` already handles this correctly with no further changes.

---

## Implementation Order

```
Phase 1  → Schema + model                 → tests: InstituteRoleSchemaTests pass
Phase 2  → InstituteSquad nav change      → tests: InstituteSquadNavTests pass
Phase 3  → Global query filters           → tests: GlobalRoleFilterTests pass
Phase 4  → RolesController CRUD           → tests: InstituteRoleCrudTests pass
Phase 5  → GetRolesByCoupon               → tests: GetRolesByCouponTests pass
Phase 6  → ResolveCustomerEngagement      → tests: ResolveCustomerEngagementTests pass
Phase 7  → CourseBoardBuilderService      → tests: CourseBoardBuilderInstituteRoleTests pass
Phase 8  → Frontend + GetStudentByEmail   → tests: RoleSkillIdTests pass + FE checklist
Phase 9  → Data migration + drop tables   → verify: no data loss, all tests still pass
```

## Testing Strategy

**Test-first** for every critical safety invariant — write the test, watch it fail (proves it catches the right thing), then write the code to make it pass. Applies to:
- Phase 3: global filter leakage (institute roles must not appear in global endpoints)
- Phase 4: SaveInstituteRoles safety guard (InstituteId=1 rows must never be touched)
- Phase 5: GetRolesByCoupon returns correct institute Role.Ids

**Alongside** for mechanical plumbing changes where the logic is obvious (nav property type change, query rewrites with clear intent).

**After every phase**: run the full existing test suite to catch regressions before moving to the next phase.

**Frontend (Phase 8)**: no test framework in place — these are manual checklist items, called out explicitly when reached.

**Script timing** (user runs these, not Claude):
- `migrate_roles_a3.sql` → run when Phase 3 code is ready to deploy
- `migrate_roles_a3_squads.sql` → run after Phase 6 is verified, before Phase 7 deploys

Write the tests for a phase first. Do not mark a phase complete until its tests pass.
