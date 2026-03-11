# Project Instance Allocation Plan (on hold)

This plan allows multiple "instances" of the same project (e.g. different cohorts/slots). Students get a place via `InstanceId`; teams are formed only among students with the same instance (or all unallocated).

## Done

### 1. AddProjectInstance endpoint
- **Endpoint:** `POST /api/Students/use/add-project-instance?studentId={id}`
- **Location:** `StudentsController.cs`
- **Behavior:**
  - Runs only when student has `InstanceId == null`; otherwise returns "Student already has a project instance."
  - Uses student's active role; compatible roles: same RoleId, or Fullstack (6) → 6, 2, 3.
  - **First pass (unallocated):** For each available project, require at least one other student (excluding request student) with `InstanceId == null`, that project in priorities, Status 0/1/2, compatible role.
  - **Second pass (instance loop):** For each `ProjectInstances` row, require at least one other student with that `InstanceId`, that project in priorities, Status 0/1/2, compatible role; return false if any row has no such students.
  - If all checks pass: `nextInstanceId = max(InstanceId) + 1` (or 1 if empty); choose project for even spread (prefer 0 instances, then fewest); insert one `ProjectInstance`, set only `student.InstanceId` (do **not** set `student.ProjectId`).
- DB: `ProjectInstances` table and `Students.InstanceId` exist (migration applied).

---

## Todo (when resuming)

### 2. Call AddProjectInstance on student login
- After successful student login (`POST /api/Students/use/login`), if student has `InstanceId == null` (and any other conditions), call AddProjectInstance logic or endpoint so they get an instance when they first log in.

### 3. Projects/use/available – return (ProjectId, InstanceId)
- **Current:** `GET /api/Projects/use/available` returns flat list of available projects (by `IsAvailable`); no join to `ProjectInstances`, no `(ProjectId, InstanceId)`.
- **Change:** Join to `ProjectInstances` and return unique `(ProjectId, InstanceId)` so frontend can show/select by instance.

### 4. Projects/use/get-students/{id} – InstanceId filter and in response
- **Current:** `GET /api/Projects/use/get-students/{id}` has no `InstanceId` parameter, does not filter by instance; response does not include `InstanceId`.
- **Change:** Add optional query (e.g. `instanceId`) and filter students by it; include `InstanceId` in response; frontend and Worker use it so teams are formed only within same InstanceId.

### 5. StudentTeamBuilderService (Worker) – same InstanceId (or all null)
- **Current:** Worker candidate SQL does not select `InstanceId`; grouping is only by `ProjectId`; teams can mix students with different InstanceIds.
- **Change:**
  1. Add `InstanceId` to candidate query and to `StudentCandidate`.
  2. Group by `(ProjectId, InstanceId)` so one team per (project, instance) slot.
  3. When selecting a group for a slot, only use candidates whose `InstanceId` matches that slot (or all null for unallocated slot). All team members must have same InstanceId (or all null).

### 6. ProjectInstances cleanup when status becomes 3
- **Current:** When a student's status is set to 3 (e.g. in BoardsController when board is created), there is no cleanup of `ProjectInstances`.
- **Change:** When a student's status becomes 3, find InstanceIds that have **no** students with status 0, 1, or 2 and `InstanceId` not null; delete those `ProjectInstances` rows (FK will set `Students.InstanceId` to null for those students).

---

## Summary table

| Item | Status |
|------|--------|
| AddProjectInstance endpoint | Done |
| StudentTeamBuilderService (Worker): same InstanceId or all null | Todo |
| Call AddProjectInstance on student login | Todo |
| Projects/use/available → (ProjectId, InstanceId) | Todo |
| get-students/{id} + optional InstanceId + include InstanceId | Todo |
| ProjectInstances cleanup when status = 3 | Todo |
