# Project Selection Refactor Plan

Covers all gaps in the project selection UI that exist for institute students and the future B2C-via-Institute=1 path. This is a follow-on to A3 (Roles table consolidation) and depends on A3 being shipped first.

---

## Background

`ProjectsStudentV2` + `ProjectDetailsV2` was built for B2C students. It has hardcoded assumptions about role names and developer rules that break for institute students with custom role configurations. Four distinct gaps are identified below.

---

## Gap 1 — Role slots are hardcoded, not from the squad

**Current state:** `ProjectDetailsV2.jsx` defines role slots as hardcoded name-match patterns:
```js
{ roleLabel: 'Full Stack Developer', match: (r) => r.toLowerCase().includes('full stack') }
{ roleLabel: 'UI/UX Designer',       match: (r) => r.toLowerCase().includes('ui/ux') }
// etc.
```
These are fixed. If an institute's squad has "Senior Backend Engineer" or "Lead Designer", they never match and appear as empty slots.

**Fix (already in A3 Phase 8 Fix 4):**

Backend — `GetAvailableInstituteProjectsForStudent` includes squad roles per project:
```csharp
SquadRoles = template.Squad.Roles
    .Where(r => r.IsActive)
    .Select(r => new { r.Name, r.Type })
    .ToList()
```

Frontend — `ProjectDetailsV2.jsx` uses `project.squadRoles` when present; falls back to hardcoded patterns for B2C:
```js
const requiredSlots = project.squadRoles?.length > 0
  ? project.squadRoles.map(r => ({
      slotKey: r.name.toLowerCase().replace(/\s+/g, '_'),
      roleLabel: r.name,
      match: (roleName) => roleName?.toLowerCase() === r.name.toLowerCase(),
    }))
  : hardcodedFallback(applicantsCap);
```

**Tests (`InstituteProjectAvailableRolesTests.cs`):**
```
GetAvailableInstituteProjects_IncludesSquadRoles_ForEachProject
// Setup: institute with 2 projects, each linked to an active template → squad with 3 active roles
// Act:   GET /api/Projects/use/institute/available/{studentId}
// Assert: response contains 2 projects; each has squadRoles array with name + type for all 3 roles

GetAvailableInstituteProjects_SquadRoles_ExcludesInactiveRoles
// Setup: squad has 3 roles — 2 active, 1 inactive (IsActive=false)
// Assert: squadRoles contains only the 2 active roles

GetAvailableInstituteProjects_SquadRoles_EmptyArray_WhenNoActiveTemplate
// Setup: project exists but has no active InstituteTemplate
// Assert: squadRoles = []  (not null, not missing — empty array)

GetAvailableInstituteProjects_SquadRoles_EmptyArray_WhenTemplateHasNoSquad
// Setup: active template exists but Squad is null / SquadId is null
// Assert: squadRoles = []

GetAvailableInstituteProjects_SquadRoles_IncludesNameAndType_NotOtherFields
// Setup: squad role with Name="Senior Backend Engineer", Type=2, CustomerEngagement=true
// Assert: squadRoles[0] = { name: "Senior Backend Engineer", type: 2 }
//         response does NOT include CustomerEngagement, IsTechnical, SkillId (not needed by FE here)

// Frontend (manual checklist — no FE test framework yet):
// Institute project: detail panel shows slot labels matching squad role names exactly ✓
// Institute project: slot match is exact name equality, not substring (no false positives) ✓
// Institute project with "Senior Backend Engineer": slot is filled when applicant has that exact role ✓
// Institute project with "Senior Backend Engineer": slot is NOT filled for a "Backend Developer" applicant ✓
// B2C project (squadRoles absent): falls back to hardcoded cap-4 / cap-5 patterns ✓
```

---

## Gap 2 — Cap computation is name-based, breaks for custom institute role names

**Current state:** `computeApplicantsCap.js` determines cap (4 or 5 slots) by string-matching `studentData.roleName`:
```js
if (roleName.includes('full stack')) return 4;
if (roleName.includes('frontend') && roleName.includes('developer')) return 5;
```

Breaks the moment an institute names their role "Full Stack Dev", "Senior FS Engineer", etc.

**Fix:** Use `role.type` (integer, stable regardless of role name):
- `Type = 1` → Full Stack → cap 4
- `Type = 2` → Bundle (FE+BE) → cap 5
- Other types → roster-based fallback (unchanged)

```js
export function computeApplicantsCap(studentData, members) {
  const viewerType = studentData?.roleType ?? studentData?.RoleType ?? null;
  if (viewerType === 1) return 4;
  if (viewerType === 2) return 5;
  // roster-based fallback unchanged
}
```

**Requires:** `GetStudentByEmail` returns `RoleType = roleInfo?.Role?.Type` (one extra field alongside `RoleSkillId` already planned in A3 Phase 8).

**Tests — backend (`GetStudentByEmailRoleTypeTests.cs`):**
```
GetStudentByEmail_ReturnsRoleType_WhenRoleExists
// Setup: student with active StudentRole → Role.Type = 1
// Act:   GET /api/Students/use/by-email/{email}
// Assert: response.roleType == 1

GetStudentByEmail_ReturnsNullRoleType_WhenNoActiveRole
// Setup: student with no StudentRole rows (or all inactive)
// Assert: response.roleType == null

GetStudentByEmail_ReturnsRoleType_ForInstituteStudent_AfterA3
// Setup: institute student; StudentRole.RoleId → Roles row with InstituteId=X, Type=2
// Assert: response.roleType == 2  (type resolves through the new Roles row correctly)

GetStudentByEmail_RoleType_IndependentOfRoleName
// Setup: student with Role.Name="Senior FS Engineer", Role.Type=1
// Assert: response.roleType == 1  (type returned, not a name-derived value)
```

**Tests — frontend JS unit tests (`computeApplicantsCap.test.js`):**
```
computeApplicantsCap_viewerType1_returns4
// Input:  studentData = { roleType: 1 }, members = []
// Assert: returns 4

computeApplicantsCap_viewerType2_returns5
// Input:  studentData = { roleType: 2 }, members = []
// Assert: returns 5

computeApplicantsCap_viewerType3_fallsBackToRosterLogic
// Input:  studentData = { roleType: 3 } (UI/UX), members = []
// Assert: returns 5 (no developer on roster → default)

computeApplicantsCap_viewerTypeNull_fallsBackToRosterLogic
// Input:  studentData = { roleType: null }, members = [{ roleName: "Full Stack Developer" }]
// Assert: returns 4 (roster-based: fullstack present, no FE/BE)

computeApplicantsCap_instituteStudent_customRoleName_usesTypeNotName
// Input:  studentData = { roleType: 1, roleName: "Senior FS Engineer" }
// Assert: returns 4  — proves fix works
// Without fix (name-based): would return 5 (name doesn't include "full stack")

computeApplicantsCap_instituteStudent_type2_customRoleName_returns5
// Input:  studentData = { roleType: 2, roleName: "Fullstack Bundle Dev" }
// Assert: returns 5
```

---

## Gap 3 — Developer rule shown is global, not per-squad

**Current state:** The project detail panel reads developer rule status from `GET /api/Roles/use/configuration` (global `KickoffConfig.RequireDeveloperRule`). For institute students, each squad has its own `squad.RequireDeveloperRule` which may differ.

**Fix:** Include `requireDeveloperRule` per project in `GetAvailableInstituteProjectsForStudent` (from the active template's squad). Frontend uses `project.requireDeveloperRule` when present; falls back to global config for B2C.

Backend addition:
```csharp
RequireDeveloperRule = _context.InstituteTemplates
    .Where(t => t.InstituteProjectId == p.Id && t.IsActive)
    .Select(t => t.Squad.RequireDeveloperRule)
    .FirstOrDefault()
```

**Tests (`InstituteProjectDeveloperRuleTests.cs`):**
```
GetAvailableInstituteProjects_RequireDeveloperRule_True_WhenSquadEnforces
// Setup: project → active template → squad with RequireDeveloperRule = true
// Assert: response project.requireDeveloperRule == true

GetAvailableInstituteProjects_RequireDeveloperRule_False_WhenSquadDoesNot
// Setup: squad with RequireDeveloperRule = false
// Assert: response project.requireDeveloperRule == false

GetAvailableInstituteProjects_RequireDeveloperRule_False_WhenNoActiveTemplate
// Setup: project with no active InstituteTemplate
// Assert: response project.requireDeveloperRule == false  (safe default)

GetAvailableInstituteProjects_RequireDeveloperRule_IndependentOfGlobalKickoffConfig
// Setup: global KickoffConfig.RequireDeveloperRule = true
//        squad.RequireDeveloperRule = false
// Assert: response project.requireDeveloperRule == false  (squad value wins, not global)

GetAvailableInstituteProjects_RequireDeveloperRule_PerProject_CanDiffer
// Setup: 2 projects — project A squad has RequireDeveloperRule=true,
//                      project B squad has RequireDeveloperRule=false
// Assert: projectA.requireDeveloperRule=true, projectB.requireDeveloperRule=false

// Frontend (manual checklist):
// Institute project with requireDeveloperRule=true: panel shows developer rule enforced ✓
// Institute project with requireDeveloperRule=false: panel shows developer rule not required ✓
// B2C project (no requireDeveloperRule in response): panel reads global config as before ✓
```

---

## Gap 4 — Role-based course cards not supported

**Current state:** When a project uses `CourseType = "Role"` (one role, N students — e.g. 3 Full Stack Developers), the card should show indexed slots: "Full Stack Developer 1", "Full Stack Developer 2", "Full Stack Developer 3". This concept does not exist anywhere in the project selection UI.

`IsSingleRole` lives on `ProjectBoard` — which doesn't exist yet at selection time. But `InstituteTemplate.CourseType` and `InstituteTemplate.RoleCount` are available earlier.

**Fix:**

Backend — add to `GetAvailableInstituteProjectsForStudent` response:
```csharp
CourseType = template.CourseType,   // "Role" | "Squad" | null
RoleCount  = template.RoleCount,    // e.g. 3
```

Frontend — `ProjectDetailsV2.jsx` when `project.courseType === 'Role'`:
```js
const baseRole = project.squadRoles[0];
const requiredSlots = Array.from({ length: project.roleCount ?? 1 }, (_, i) => ({
  slotKey: `${baseRole.name.toLowerCase().replace(/\s+/g, '_')}_${i + 1}`,
  roleLabel: `${baseRole.name} ${i + 1}`,
  match: (roleName) => roleName?.toLowerCase() === baseRole.name.toLowerCase(),
}));
```

`computeApplicantsCap` override: `project.courseType === 'Role'` → return `project.roleCount` directly, bypassing cap logic.

**Tests — backend (`InstituteProjectCourseTypeTests.cs`):**
```
GetAvailableInstituteProjects_RoleCourse_ReturnsCourseTypeRole_AndRoleCount
// Setup: project → active template with CourseType="Role", RoleCount=3
// Assert: response project.courseType == "Role", project.roleCount == 3

GetAvailableInstituteProjects_SquadCourse_ReturnsCourseTypeSquad_AndNullRoleCount
// Setup: template with CourseType="Squad"
// Assert: response project.courseType == "Squad", project.roleCount == null

GetAvailableInstituteProjects_NoTemplate_CourseTypeIsNull
// Setup: no active template for project
// Assert: response project.courseType == null, project.roleCount == null

GetAvailableInstituteProjects_RoleCourse_SquadRoles_ContainsExactlyOneRole
// Setup: role-based course template → squad with 1 role (by definition)
// Assert: squadRoles.length == 1
```

**Tests — frontend JS unit tests (`computeApplicantsCap.test.js`):**
```
computeApplicantsCap_roleCourse_returnsRoleCount_ignoresViewerType
// Input:  project = { courseType: "Role", roleCount: 3 },
//         studentData = { roleType: 1 }   ← Full Stack, would normally return 4
// Assert: returns 3  — roleCount overrides cap logic

computeApplicantsCap_roleCourse_roleCount2_returns2
// Input:  project = { courseType: "Role", roleCount: 2 }, studentData = { roleType: 2 }
// Assert: returns 2
```

**Frontend (manual checklist):**
```
// Role course project: detail panel shows 3 slots — "Full Stack Developer 1", "Full Stack Dev 2", "Full Stack Dev 3" ✓
// Role course: each slot is filled individually as students apply ✓
// Role course: applicant count shows X/3, not X/4 or X/5 ✓
// Squad course: normal slot behaviour unchanged ✓
```

---

## Gap 5 — B2C platform growth path (Institute=1 generalization)

**Current state:** B2C students have `Student.InstituteId = null`. `ProjectsStudentV2` checks `instituteId > 1` to route them to the `Projects` table. Creating a project as platform admin (`InstituteId=1`) puts it in `InstituteProjects` — invisible to B2C students.

**Desired state:** Platform admin copies a project → `InstituteProject(InstituteId=1, Coupon=null)` → visible to all B2C students. `Coupon=X` → gated to a specific programme.

**Changes required:**

| # | Change | File |
|---|--------|------|
| 1 | Data migration: B2C students get `Student.InstituteId = 1` | SQL on `Students` table |
| 2 | Accept InstituteId=1 in available projects endpoint | `ProjectsController.cs:1287` — `<= 1` → `<= 0` |
| 3 | Route all students through institute path | `ProjectsStudentV2.jsx:121,318` — `> 1` → `> 0` |
| 4 | Coupon filter already works correctly | `ProjectsController.cs:1293` — no change |

**Prerequisite:** A3 shipped + Gaps 1–4 complete (institute path must be fully working before routing B2C through it).

**Tests (`B2cInstitute1ProjectsTests.cs`):**
```
GetAvailableInstituteProjects_Institute1Student_Returns200_WithProjects
// Setup: student with InstituteId=1; InstituteProject with InstituteId=1, IsAvailable=true, Coupon=null
// Act:   GET /api/Projects/use/institute/available/{studentId}
// Assert: 200 OK, project appears in response

GetAvailableInstituteProjects_NullInstituteStudent_Returns400
// Setup: student with InstituteId=null (pre-migration B2C student)
// Assert: 400 BadRequest  (guard: InstituteId <= 0)

GetAvailableInstituteProjects_ZeroInstituteStudent_Returns400
// Setup: student with InstituteId=0
// Assert: 400 BadRequest

GetAvailableInstituteProjects_Institute1_NoCouponProject_VisibleToAllInstitute1Students
// Setup: 3 students with InstituteId=1 — one with Coupon=null, one with Coupon="X", one with Coupon="Y"
//        InstituteProject with InstituteId=1, Coupon=null
// Assert: all 3 students receive the project in their response

GetAvailableInstituteProjects_Institute1_CouponProject_VisibleOnlyToMatchingCouponStudent
// Setup: 2 students — Student A has Coupon="PROG-1", Student B has Coupon="PROG-2"
//        InstituteProject with InstituteId=1, Coupon="PROG-1"
// Assert: Student A receives the project; Student B does not

GetAvailableInstituteProjects_Institute1_CouponProject_NotVisibleToNoCouponStudent
// Setup: student with InstituteId=1, Coupon=null
//        InstituteProject with InstituteId=1, Coupon="PROG-1"
// Assert: student does NOT receive the project
//         (student has no coupon → coupon filter excludes it)

GetAvailableInstituteProjects_Institute2Student_DoesNotSeeInstitute1Projects
// Setup: student with InstituteId=2; project with InstituteId=1
// Assert: response is empty  (InstituteId filter scopes correctly)
```

---

## API Response Shape (target for `GetAvailableInstituteProjectsForStudent`)

After all fixes, each project in the response includes:

```json
{
  "id": 12,
  "title": "FinTech Dashboard",
  "oneLiner": "...",
  "applicantsCount": 2,
  "squadRoles": [
    { "name": "Full Stack Developer", "type": 1 },
    { "name": "UI/UX Designer",       "type": 3 },
    { "name": "Product Manager",      "type": 4 }
  ],
  "requireDeveloperRule": true,
  "courseType": "Squad",
  "roleCount": null
}
```

For a role-based project:
```json
{
  "squadRoles": [{ "name": "Full Stack Developer", "type": 1 }],
  "courseType": "Role",
  "roleCount": 3
}
```

---

## `GetStudentByEmail` additions

Two fields to add alongside `RoleSkillId` (already planned in A3 Phase 8):

```csharp
RoleSkillId = roleInfo?.Role?.SkillId,
RoleType    = roleInfo?.Role?.Type,   // used by computeApplicantsCap
```

---

## Test file summary

| Test file | Gap | Key invariant |
|-----------|-----|---------------|
| `InstituteProjectAvailableRolesTests.cs` | 1 | Squad roles included per project; inactive excluded |
| `GetStudentByEmailRoleTypeTests.cs` | 2 | RoleType field returned; institute roles resolve correctly |
| `computeApplicantsCap.test.js` | 2 + 4 | Type-based cap; role course uses roleCount |
| `InstituteProjectDeveloperRuleTests.cs` | 3 | Per-squad rule returned; independent of global config |
| `InstituteProjectCourseTypeTests.cs` | 4 | courseType and roleCount returned correctly |
| `B2cInstitute1ProjectsTests.cs` | 5 | Institute=1 routing; coupon scoping |

---

## Implementation Order

```
1. A3 ships (prerequisite)
2. Gap 1 — dynamic slots             backend + frontend (bounded)
3. Gap 2 — type-based cap            add RoleType to GetStudentByEmail; update computeApplicantsCap
4. Gap 3 — per-squad developer rule  one field added to API response; frontend reads it
5. Gap 4 — role-based course cards   add courseType + roleCount to API response; update slot logic
6. Gap 5 — B2C Institute=1           data migration + 2 guard changes
```

Gaps 1–4 are independent of each other and can be done in any order after A3.
Gap 5 depends on Gaps 1–4 being complete (institute path must be fully working before routing B2C through it).
