# QuestMode — Full Feature Plan

## Overview

QuestMode transforms the boardroom from a **collaborative squad experience** into a **competitive individual hiring challenge**. Instead of juniors building an app together, each student in a squad independently completes an employer-provided quest, competing for the same job. They can see each other's progress and LinkedIn profiles but cannot communicate or access each other's repos/artifacts.

The feature is a **non-breaking switch** — all existing squad/course functionality remains intact. QuestMode is toggled per-institute via `Institutes.QuestMode`.

---

## Status Legend
- ✅ Done
- 🔄 In Progress
- ⬜ Planned

---

## Phase 1 — QuestMode Switch + Boardroom UI ✅

### 1.1 Infrastructure Switch ✅
- `Institutes.QuestMode` boolean column (migration + SQL script)
- `GET /api/Institutes/{id}/teachers` returns `QuestMode`
- `PATCH /api/Institutes/{id}/settings` endpoint to toggle it
- `GET /api/Students/use/email` returns `QuestMode` via Institute include
- `InstituteConfig.jsx` — admin toggle UI (on/off switch)

### 1.2 Boardroom UI (QuestMode = true) ✅
| Change | Status |
|---|---|
| Hide "Next Meeting" panel (MeetingStrip) | ✅ |
| "Project Brief" → "Job Description" | ✅ |
| Hide Trello button | ✅ |
| Disable Figma button (unless role contains "Designer") | ✅ |
| Hide CRM button | ✅ |
| "Team Performance" → "Candidates Progress" | ✅ |
| Hide overall progress, overdue, active tasks, bugs metrics | ✅ |
| Sprint X → "TO-DO", remove date range | ✅ |
| Show only current user's own tasks in TO-DO | ✅ |
| Remove Sprint Progress panel (incl. Bugs tiles) | ✅ |
| Remove Team Chat tab | ✅ |
| Remove Individual (private) chat tabs | ✅ |
| Customer chat visible only if CustomerEngagement flag | ✅ |
| Remove Sprint Context selector (AI mentor sidebar) | ✅ |
| Remove Human button | ✅ |

---

## Phase 2 — Board Creation Infra 🔄

Currently each squad shares one board (one Trello board, one GitHub repo, one Railway instance, one Neon DB).
In QuestMode, each student gets their **own** isolated GitHub/Railway/Neon environment, all linked to a single shared Trello board.

### 2.0 Data Model ✅
- `QuestBoards` table: `Id`, `StudentId` (FK→Students), `BoardId` (FK→ProjectBoards), `PublishUrl`, `GithubFrontendUrl`, `GithubBackendUrl`, `WebApiUrl`, `DBPassword`, `NeonBranchId`, `NeonProjectId`, `CreatedAt`, `UpdatedAt`
- Unique constraint on `(StudentId, BoardId)`
- SQL script: `Scripts/AddQuestBoardsTable_PostgreSQL.sql`

---

### 2.1 Board Creation Changes ⬜

**Context (from codebase analysis):**
- Single endpoint: `POST /api/Boards/use/create` in `BoardsController.cs`
- Called by `StudentTeamBuilderService/Worker.cs` → `TryCreateBoardsAsync()` after group selection
- Currently provisions: 1× Trello board, 1× GitHub BE repo, 1× GitHub FE repo, 1× Railway service+domain, 1× Neon project+branch — all stored in `ProjectBoards`
- Worker already knows the institute from the students; it can check `Institute.QuestMode`

**Plan:**

**Step A — Worker.cs:** When building the `CreateBoardRequest`, include `IsQuestMode = institute.QuestMode` (load institute via `Students[0].InstituteId`).

**Step B — CreateBoardRequest:** Add `bool IsQuestMode` field.

**Step C — BoardsController.cs `CreateBoard()`:**
When `IsQuestMode = true`:
1. **Trello** — create a **single shared** Trello board as today (all students see the same cards). No change here.
2. **Per-student loop** — after Trello creation, loop over each student in `StudentIds` and for each:
   - **GitHub:** Create two repos:
     - Backend: `backend_{boardId}_{studentId}` (e.g. `backend_abc123_42`)
     - Frontend: `{boardId}_{studentId}`
     - Add only that student as collaborator (not the whole squad)
     - Call `CreateBackendOnlyCommitAsync()` / `CreateFrontendOnlyCommitAsync()` with per-student Neon connection string
   - **Neon:** Create one project + branch per student (same flow as today, just N times)
     - Project name: `Quest-{boardId}-{studentId}`
   - **Railway:** Create one service + domain per student
     - Service name: `{boardId}-{studentId}`
     - Deploy from the student's own GitHub repos
   - **Write `QuestBoards` row** for this student with all the resulting URLs
3. **ProjectBoards** — still created as today for the squad-level record (Trello board, SquadName, etc.) but GitHub/Railway/Neon fields left null (they live in QuestBoards now).

**Naming conventions:**
| Resource | Name pattern |
|---|---|
| GitHub BE repo | `backend_{boardId}_{studentId}` |
| GitHub FE repo | `{boardId}_{studentId}` |
| Neon project | `Quest-{boardId}-{studentId}` |
| Railway service | `{boardId}-{studentId}` |

---

### 2.2 Boardroom URL Retrieval ⬜

**Context (from codebase analysis):**
- `BoardRoom.jsx` calls `GET /api/Boards/use/stats/{boardId}` → `boardStats`
- Lines 1499-1540: extracts `githubBackendUrl`, `githubFrontendUrl`, `webApiUrl`, `publishUrl` from `boardStats`
- These come from `ProjectBoards` fields in the existing stats endpoint
- These are passed to `BoardActionButtons` (Live Project, GitHub BE/FE, API buttons)

**Plan:**

**New API endpoint:** `GET /api/QuestBoards/{boardId}/me` (authenticated by `?studentId=` query param or student email header — same auth pattern used elsewhere)
- Looks up `QuestBoards` WHERE `BoardId = boardId AND StudentId = studentId`
- Returns: `PublishUrl`, `GithubFrontendUrl`, `GithubBackendUrl`, `WebApiUrl`, `DBPassword`

**BoardRoom.jsx changes (QuestMode only):**
- After loading `boardStats`, if `questMode = true`, call the new `GET /api/QuestBoards/{boardId}/me` endpoint
- Override the URL state variables (`publishUrl`, `repositoryBackendUrl`, `repositoryFrontendUrl`, `webApiUrl`) with the QuestBoards values
- Non-QuestMode flow unchanged — still reads from `boardStats` as today

**New `api/questBoardStrAppers.js` file** with `getMyQuestBoard(boardId, studentId)` function.

---

### 2.3 AI Mentor Context — GitHub URL Changes ⬜

**Context (from codebase analysis):**
- `ChatSidebar.jsx` imports: `fetchBoardGitStatusesDual()`, `fetchGitRowDual()`, `createGithubBranchDual()`, `createGithubPRDual()`, `mergeGithubBranchDual()`, `reviewCodeDual()`
- These Git operations use the board-level GitHub URLs (from `ProjectBoards`)
- The mentor backend (`MentorController`) receives repo URLs and uses them for code review, gap analysis, commit fetching via `GitHubService`
- `SprintAssessmentService` fetches commits/PRs per student using the board's GitHub repos

**Plan:**

**Backend — `MentorController`:** Each mentor endpoint that accepts a `boardId` should also accept an optional `questBoardId` or `studentId`. When provided (and `QuestMode = true` for that board's institute), look up `QuestBoards` and use the student's personal `GithubBackendUrl` / `GithubFrontendUrl` instead of `ProjectBoards` fields.

**Backend — `SprintAssessmentService`:** When fetching commits for assessment, check if a `QuestBoards` row exists for the student. If yes, use those GitHub URLs; otherwise fall back to `ProjectBoards`.

**Frontend — `ChatSidebar.jsx`:**
- In QuestMode, pass the student's own QuestBoard GitHub URLs (loaded via `getMyQuestBoard()`) into the Git operation calls
- Git branch creation, PR creation, merge, code review — all target the student's personal repo, not the squad repo
- The `devPanel` (branch status, PR status) should query the student's personal repo

---

### 2.4 Build Order for Phase 2 ⬜

| Step | What | File(s) |
|---|---|---|
| 2.1a | Add `IsQuestMode` to `CreateBoardRequest` | `BoardsController.cs` |
| 2.1b | Worker passes `IsQuestMode` from institute | `Worker.cs` |
| 2.1c | Per-student provisioning loop in `CreateBoard()` | `BoardsController.cs` |
| 2.1d | Write `QuestBoards` rows after provisioning | `BoardsController.cs` |
| 2.2a | New `GET /api/QuestBoards/{boardId}/me` endpoint | New `QuestBoardsController.cs` |
| 2.2b | `getMyQuestBoard()` API function | `api/questBoardStrAppers.js` |
| 2.2c | BoardRoom fetches + overrides URLs in QuestMode | `BoardRoom.jsx` |
| 2.3a | Mentor endpoints accept per-student repo override | `MentorController.cs` |
| 2.3b | Assessment uses QuestBoards GitHub URLs | `SprintAssessmentService.cs` |
| 2.3c | ChatSidebar passes QuestBoard URLs to Git ops | `ChatSidebar.jsx` |

---

## Phase 3 — Employer Portal ⬜

A new web portal (separate login flow) for employers to submit quests and view results.

### 3.1 Employer Input
- **Job Title** (text)
- **Job Description** (rich text / markdown)
- **Task / Quest** (free text — the actual challenge students will complete)
- **Reference Repo** (GitHub URL — employer grants read-only OAuth access)
- **Questions** (list of free-text questions the AI will ask candidates at sprint completion)

### 3.2 Employer Output (Results View)
- List of candidates who completed the quest (linked to their squad/board)
- Per-candidate AI assessment summary
- **Comparison layer** — ranked view across all candidates (Phase 6)
- LinkedIn profile links per candidate

### 3.3 Auth
- Separate employer login (email + password, similar to institute teacher login)
- New `Employers` table: `Id`, `Name`, `Email`, `PasswordHash`, `CompanyName`, `CreatedAt`
- New `EmployerPortalController`

### 3.4 Data Model
```
Employers
  Id, CompanyName, ContactName, Email, PasswordHash, IsActive, CreatedAt

EmployerQuests
  Id, EmployerId, JobTitle, JobDescription, QuestText, ReferenceRepoUrl,
  ReferenceRepoToken (encrypted), Status (Draft/Active/Closed), CreatedAt

EmployerQuestQuestions
  Id, QuestId, QuestionText, OrderIndex

EmployerQuestBoards  (links a quest to a ProjectBoard / squad run)
  Id, QuestId, BoardId, InstituteId, CreatedAt
```

---

## Phase 4 — Employer Quest Generator ⬜

Triggered from the Employer Portal after the employer submits their input. Generates the quest content in the DB and seeds the Trello boards.

### 4.1 Pipeline
1. Employer submits quest via portal → creates `EmployerQuests` row
2. Generator reads: job description + quest text + reference repo content (via OAuth token)
3. AI generates:
   - Sprint1 Trello card list (task breakdown matching the quest)
   - Design brief / project brief content (shown as "Job Description" in boardroom)
   - Acceptance criteria per card
4. Outputs stored as `TrelloBoardJson` (same format as existing Course Builder output)
5. `EmployerQuestBoards` row links quest → board

### 4.2 Reference Repo Read
- OAuth flow: employer authorizes read-only GitHub access
- Backend fetches repo file tree + key files (README, main source files)
- Feeds into AI context for quest generation and mentor context

### 4.3 Endpoint
`POST /api/EmployerQuests/{questId}/generate`

---

## Phase 5 — Course Builder (QuestMode Integration) ⬜

The existing Course Builder generates `TrelloBoardJson`, role assignments, and module content for institute courses. It needs a QuestMode-aware path.

### 5.1 Changes
- New `QuestMode` flag on the Course Builder generation request
- When `QuestMode = true`:
  - Skip role-based task assignment (all cards go to "unassigned" — student picks them up)
  - Generate a single `Sprint1` only (no future sprints)
  - Use employer quest text + job description as the primary AI prompt context
  - Include employer questions as a "completion checklist" card in Sprint1
- Output `TrelloBoardJson` is reused by Phase 2 board creation (one copy per student)

### 5.2 ⚠️ Note — Identical Tasks Across All Candidates
In QuestMode the Course Builder must generate **identical tasks for every candidate**, regardless of role index (Full Stack Developer 1, Full Stack Developer 2, etc. all get the same task set). This is the opposite of the normal squad behaviour where tasks are distributed across roles.
- The same `TrelloBoardJson` is seeded into every student's Trello board unchanged
- No per-role card filtering or distribution
- Ensure the board creation loop (Phase 2.1c) does **not** apply the existing role-based card filtering logic (`OverrideSprintOnBoardAsync` role filter) when `IsQuestMode = true`

---

## Phase 6 — Mentor Context (QuestMode Adjustment) ⬜

The AI mentor currently operates in squad/collaborative context. In QuestMode it needs to shift to a competitive/hiring context.

### 6.1 System Prompt Changes
- Replace squad framing with: "You are mentoring a candidate completing a job quest for [Company]. They are competing individually for the role of [Job Title]."
- Include job description and quest text in system context
- Reference the employer's reference repo (summarized) as context for tech stack expectations
- Mentor should **not** reveal other candidates' progress or solutions

### 6.2 Completion Trigger
- When student marks all Sprint1 cards complete → mentor automatically pushes the employer's questions
- Questions presented one at a time in the mentor chat
- Answers recorded in `EmployerQuestCandidateAnswers` table

### 6.3 Data Model Addition
```
EmployerQuestCandidateAnswers
  Id, QuestId, StudentId, QuestionId, AnswerText, AnsweredAt
```

---

## Phase 7 — Assessment Comparison Layer ⬜

An additional AI assessment pass that runs **across all candidates** for the same quest, enabling the employer to compare them.

### 7.1 Individual Assessment (existing + enhanced)
- Existing AI assessment runs per student as today
- Enhanced with QuestMode context: evaluate against job description, quest requirements, employer questions

### 7.2 Cross-Candidate Comparison
- Triggered when employer views results (or manually from portal)
- AI receives all candidates' assessment summaries + question answers for the same quest
- Outputs:
  - Ranked shortlist with justification
  - Strengths/weaknesses per candidate relative to the role
  - Notable differences (e.g. "Candidate A completed 100% of tasks; Candidate B had cleaner code structure")

### 7.3 Endpoint
`POST /api/EmployerQuests/{questId}/compare-candidates`

### 7.4 Data Model
```
EmployerQuestCandidateAssessments
  Id, QuestId, StudentId, AssessmentText, Score (0-100), GeneratedAt

EmployerQuestComparisonReport
  Id, QuestId, ReportJson, GeneratedAt
```

---

## Phase 8 — Project Selection UI (Cosmetic) ⬜

The student-facing project selection page needs minor adjustments for QuestMode.

### 8.1 Changes
- When `InstituteQuestMode = true`, project cards show "Quest" badge instead of "Project"
- Project description replaced with job title + company name (from `EmployerQuests`)
- Hide squad/role selection step — in QuestMode students join individually, not as a squad
- CTA button: "Apply for Quest" instead of "Join Squad"

---

## Phase 9 — CV-Based Quest Matching Layer ⬜

After a student registers, instead of browsing all available quests manually, an AI/CV-matching layer filters and ranks the relevant job quests for that candidate — replacing the generic "Project Selection" browse experience with a personalised job-match feed.

### 9.1 Flow
1. Student completes registration (existing flow)
2. Student uploads or provides their CV / LinkedIn profile URL
3. CV parsing API extracts structured profile: skills, experience, education, technologies
4. Matching engine scores each active `EmployerQuest` against the student's profile
5. Student lands on a ranked "Job Matches" page instead of the generic project browser

### 9.2 CV Input Options
- **File upload** — PDF/DOCX parsed server-side
- **LinkedIn URL** — scrape or OAuth profile import
- **Manual profile** — structured form as fallback (skills, years of experience, tech stack)
- CV/profile stored on the `Students` table (new `CvText` / `CvParsedJson` columns)

### 9.3 Matching Logic
- CV parsing: third-party API (e.g. Affinda, Sovren, or OpenAI with PDF extraction)
- Match scoring per quest: AI compares student profile against `JobTitle` + `JobDescription` + `QuestText`
- Score dimensions: tech stack overlap, seniority fit, domain relevance
- Output: ranked list of `EmployerQuestId` with match score (0–100) and short match rationale

### 9.4 Quest Match Feed UI (replaces Project Selection in QuestMode)
- Card per matched quest: company name, job title, match score badge, 1-line rationale
- "Why you match" expandable section per card
- Sorted by match score descending
- CTA: "Start Quest" → triggers board provisioning (Phase 2)

### 9.5 Backend
- `POST /api/Students/{id}/cv` — upload and parse CV, store parsed JSON
- `GET /api/Students/{id}/quest-matches` — returns ranked quest list for that student
- `EmployerQuestMatchScores` table caches scores (re-computed when CV or quests change)

### 9.6 Data Model
```
Students (additions)
  CvText         text nullable       -- raw extracted text from CV
  CvParsedJson   text nullable       -- structured JSON from parsing API
  CvUpdatedAt    timestamp nullable

EmployerQuestMatchScores
  Id, QuestId, StudentId, Score (0-100), RationaleText, ComputedAt
```

---

## Open Questions / To Clarify

- **Phase 2**: Should Railway/Neon provisioning be fully automated or semi-manual (teacher triggers it)?
- **Phase 3**: Is the Employer Portal a separate app/subdomain or part of the existing teacher dashboard?
- **Phase 4**: Should the reference repo reader be a one-time snapshot at quest creation, or re-fetched on each mentor session?
- **Phase 6**: Should the employer's questions be hidden from the student until they complete the sprint, or visible upfront?
- **Phase 8**: Does the student know they're competing against others, or is the quest presented as a solo challenge?

---

## Build Order

| # | Phase | Depends On |
|---|---|---|
| 1 | ✅ QuestMode Switch + Boardroom UI | — |
| 2 | ⬜ Board Creation Infra | Phase 1 |
| 3 | ⬜ Employer Portal | Phase 1 |
| 4 | ⬜ Quest Generator | Phase 3 |
| 5 | ⬜ Course Builder Integration | Phase 4 |
| 6 | ⬜ Mentor Context | Phase 4, 5 |
| 7 | ⬜ Assessment Comparison | Phase 6 |
| 8 | ⬜ Project Selection UI | Phase 3, 4 |
| 9 | ⬜ CV Matching Layer | Phase 3, 8 |
