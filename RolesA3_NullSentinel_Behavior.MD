# Roles Table — Null Sentinel Behavior (Post-A3)

## Scoping Rules

| InstituteId | SquadId | Meaning |
|---|---|---|
| `null` | `null` | Global / B2C role — platform catalog, **never modified** |
| `X` | `null` | Institute base role — customised copy for institute X |
| `X` | `Y` | Squad-scoped role — tied to a specific squad within institute X |

`InstituteId = null` is the sentinel for global roles. `InstituteId = 1` is a **normal institute** like any other — it can have its own base and squad roles.

---

## Key Invariants

### Global roles are never modified
When an institute admin saves Roles Config and no institute-specific rows exist, the backend creates **new institute copies** (`InstituteId = X, SquadId = null`). The original global rows (`InstituteId = null`) are untouched.

### Fork-on-save
`SaveInstituteRoles` (base path) looks up rows by ID first, then by **name** as fallback. This prevents duplicate creation when the frontend sends a stale or global role ID — it updates the existing institute row instead of inserting a new one.

### BaseInstituteRoleId validation
The `baseInstituteRoleIdSet` used to validate `dto.BaseInstituteRoleId` accepts **both** institute-specific rows (`InstituteId = X`) and global rows (`InstituteId = null`). When no institute rows exist the frontend falls back to global catalog IDs, which must be accepted.

---

## Query Patterns by Use Case

### Return global catalog only (B2C, registration, employer)
```csharp
.Where(r => r.InstituteId == null && r.IsActive)
```
Used by: `GET /api/Roles/use`, `GET /api/Roles/use/all`, `GET /api/Roles/by-category/{category}`, `GetRolesByCoupon` fallback.

### Return institute-specific base roles
```csharp
.Where(r => r.InstituteId == instituteId && r.SquadId == null)
```
Used by: `GET /api/Roles/use/institute/{id}` (no templateId), Roles Config save path.

### Return institute squad roles
```csharp
.Where(r => r.InstituteId == instituteId && r.SquadId == squadId)
```
Used by: `GET /api/Roles/use/institute/{id}?templateId=X`, squad save path.

### Return base roles including global fallback (course builder)
```csharp
.Where(r => effectiveRoleIds.Contains(r.Id)
         && (r.InstituteId == instituteTemplate.InstituteId || r.InstituteId == null)
         && r.SquadId == null)
```
Used by: `CourseBoardBuilderService` base role path. Needed because `effectiveRoleIds` may contain global role IDs when no institute rows exist.

### Return all roles for a filter combo (Staff Dashboard)
Combine squad roles + global catalog, dedup by name (squad takes priority):
```csharp
// 1. Squad-scoped roles for this institute's active squads
// 2. Global roles (InstituteId=null) where name not already in squad set
```
Used by: `GetInstituteSquadRoles` (Staff Dashboard Roles combo).

---

## Frontend Fallback Pattern

When `fetchInstituteRoles(instituteId)` returns empty (no institute-specific rows exist yet), frontends fall back to the global catalog:

| Screen | Fallback |
|---|---|
| Roles Config | `fetchAllRoles()` → global drafts with negative IDs |
| Create Course → Role tab | `fetchAllRolesForTaskBuilder()` |
| Staff Dashboard Roles combo | Always merges global catalog into combo |
| `GetRolesByCoupon` (backend) | `WHERE InstituteId IS NULL` |

---

## Role Filter by ID vs Name

The Staff Dashboard filters students by **role ID** (not name). Each student's `StudentRole.RoleId` is compared against the selected combo value.

- Institute course students → have squad-scoped role IDs → combo returns those IDs → filter works ✅
- B2C students → have global role IDs (e.g. id=6) → if a same-name squad role exists, the global entry is deduped out of the combo → B2C students are **missed** by that filter ⚠️

This is a known limitation. Fix (future): filter by role name when overlap exists between squad and global roles.

---

## EF Core Gotcha — HasDefaultValue(0) on Type column

`Role.Type`, `InstituteRole.Type`, `InstituteSquadRole.Type` all had `HasDefaultValue(0)` in the DbContext. EF Core treats `0` as the sentinel "use DB default" and **omits the column from INSERT**. If the DB column has no actual `DEFAULT 0` constraint, this inserts NULL → NOT NULL violation.

**Fix**: Remove `HasDefaultValue(0)` from all three entities so EF always sends the explicit value.

---

## InstituteId Nullable — C# Type Impact

`Role.InstituteId` is `int?`. Any code mapping a `Role` into a DTO/model with a non-nullable `int InstituteId` must use:
```csharp
InstituteId = role.InstituteId!.Value        // when guaranteed non-null (squad/institute rows)
InstituteId = role.InstituteId ?? fallback   // when global rows may be included
```
Known locations fixed: `CourseBoardBuilderService.cs` lines 197, 233.
