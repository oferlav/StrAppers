# After a crash — how to resume (for you + the AI)

## What to paste in a new chat

Copy **one** of these:

**Short**

> Read `docs/AGENT_RESUME.md` (checkpoint **2026-03-24** — migration **complete**), `docs/BASE44_FINAL_DETACH_CHECKLIST.md`, and `docs/BASE44_MIGRATION_PLAN.md` for reference; continue from **Current status** (or optional final-detach cleanup if I ask).

**If you remember the last task**

> We were doing [e.g. “migrate BoardRoom to strAppersBackend”]. Read `docs/AGENT_RESUME.md` and continue from there.

**Fast handoff (recommended now)**

> Read `docs/AGENT_RESUME.md`, `docs/BASE44_MIGRATION_PLAN.md`, `docs/BASE44_FINAL_DETACH_CHECKLIST.md`, and `docs/STRAPPERS_BACKEND_CLIENT.md`. **Migration is complete** — see **Checkpoint — 2026-03-24**; use checklist only for optional SDK/static cleanup.

---

## Project facts (do not re-argue)

| Topic | Fact |
|-------|------|
| Frontend root | `C:\StrAppers\strAppersFrontend` = **base44 GitHub clone** (Vite + `src/` + `base44/functions` ~126 Deno functions). |
| Backend | `strAppersBackend` in **StrAppers** repo. |
| Policy | **Happy with current frontend.** **Do not** add backend routes just to fill audit gaps. Mark calls **obsolete** only when **100% sure** no matching route exists. |
| Cursor vs disk | IDE may show fewer files than Explorer; audits used **disk** scans. **Also:** Cursor’s **file search / glob** for `strAppersFrontend/src` has returned only a **subset** of `.jsx` files while PowerShell on the same machine sees **159** — use **`docs/FRONTEND_SRC_FILE_INDEX.md`** (regenerate: `.\scripts\GenerateFrontendSrcIndex.ps1`) as the **source of truth** for “what exists on disk.” |
| Curated frontend paths | **`docs/FRONTEND_PATH_INDEX.md`** — append-only list of pages/components the user (or agent) has pointed at; agents should extend it “as you go.” Rule: `.cursor/rules/strappers-frontend-layout.mdc`. |

---

## What’s already done (documentation + scaffold)

- **`docs/BASE44_MIGRATION_PLAN.md`** — plan, policy §0.0.2, execution order §6.
- **`docs/BASE44_BACKEND_ENDPOINT_AUDIT.md`** — obsolete patterns (`branch-status`, `/api/StudentProjects`, board count typo, etc.).
- **`docs/BASE44_AUDIT_API_PATHS_FROM_DISK.txt`** — all `entry.ts` → `/api/...` (regenerate via PowerShell in audit doc).
- **`docs/BASE44_FRONTEND_INVOKE_INDEX.md`** — 83 `base44.functions.invoke` names under `src/`.
- **`docs/STRAPPERS_BACKEND_CLIENT.md`** — how to use the direct client.
- **`strAppersFrontend/src/api/strAppersBackend.js`** — `strAppersBackendFetch` / `strAppersBackendJson`, env `VITE_STRAPPERS_BACKEND_URL`.
- **`strAppersFrontend/.env.example`** — env template; `.gitignore` has `!.env.example`.
- **`docs/BASE44_FINAL_DETACH_CHECKLIST.md`** — **single checklist** for the last mile when removing Base44 (SDK, fallbacks, CDN URLs, CRM direct invokes, verification greps). Updated 2026-03-21.

## Migration status — **complete** (2026-03-24)

- **Base44 → StrAppers (dual-path) work** and **cutover-related tasks** are treated as **done** for this project (frontend uses `VITE_STRAPPERS_BACKEND_URL` + StrAppers APIs; remaining Base44 references are fallbacks or optional cleanup per checklist).
- **Azure:** API deployed; **Google OAuth** — `GoogleAuth:*` configured in **Azure App Service Configuration** (overrides JSON); redirect URI aligned with Google Cloud Console.
- **Optional later:** `docs/BASE44_FINAL_DETACH_CHECKLIST.md` — remove SDK fallbacks, static CDN URLs, etc., only if you want a “hard” detach with no Base44 code paths.

---

## Suggested next step (post-migration)

Only if you open a new task: pick a concrete bug/feature, or run **optional** final-detach items from **`docs/BASE44_FINAL_DETACH_CHECKLIST.md`**. Update this file when direction changes.

### Checkpoint — 2026-03-24 (**current** — migration complete)

**Status:** Migration **complete** per user. StrAppers backend on Azure; Google sign-in works with app settings for **`GoogleAuth`** (redirect URI, client id/secret, etc.). Use **`docs/BASE44_FINAL_DETACH_CHECKLIST.md`** only for **optional** cleanup (remove Base44 SDK / fallbacks / CDN references), not as a blocking migration step.

**Historical:** Prior checkpoints (2026-03-21, 2026-03-22) described dual-path rollout and detach planning; that phase is **closed**.

---

### Checkpoint — 2026-03-21 (archived — pre-complete)

**User decision:** Final Base44 detach was planned **ASAP**; dual-path pattern until cutover. Superseded by **2026-03-24** (migration complete).

**Board Room (recap):** Team Performance uses **`fetchDashboardStats`** + **`useDashboardStats`** (~60s poll). **`base44Client` intercepts** still route many invokes to StrAppers when env is set.

**Next session:** Superseded — see **2026-03-24**.

---

### Checkpoint — 2026-03-20 (archived)

**Dual-path:** If `VITE_STRAPPERS_BACKEND_URL` is set → direct StrAppers HTTP; else → `base44.functions.invoke`. Policy: **no new backend routes** just to fill audit gaps (`docs/BASE44_BACKEND_ENDPOINT_AUDIT.md`).

**Backend (deploy if not live yet):** `BoardsController.GetBoardStats` now includes **`nextMeetingTime`**, **`nextMeetingUrl`**, **`nextMeetingTitle`** from `ProjectBoard` so Board Room “Next Meeting” matches what Base44 used to merge. **Redeploy `strAppersBackend`** for that fix in prod/dev API.

**Invoke audit (you pasted full grep):**

| Bucket | Meaning |
|--------|--------|
| **A** | `base44Client.js` — wrapper only; ignore. |
| **B** | `src/api/*StrAppers.js` (boardChat, boardCollab, boardRoom, dashboard, mentorChat, mentorGit) — `invoke` only in **fallback** when env unset; **no action** unless removing Base44 entirely. |
| **C** | **`components/`**, **`pages/`**, **`hooks/`** with direct `base44.functions.invoke` — **still hit Base44 even when StrAppers URL is set**; this is the **real remaining migration**. |

**Done recently (in addition to table below):** `BugDetailsModal` + bugs list → `get-bug` / `modify-bug` / `fetchOpenBugs`; `logBugToBackend.jsx` → `create-bug`; `SprintProgressPanel` → `get-user-stories`, `set-done`, flattened checklist index; `mentorGitStrAppers` `*Dual` + `ChatSidebar` refactor + `base44Client` POST intercepts for github-branch/pr/merge/review; `base44Client` intercepts for several board/bug/trello invokes.

---

### What is next — **exact order (after break)**

**Done (2026-03-20):** `MyTasksModal.jsx` (**`invokeGetMyTasksForModal`** + **`invokeSetCheckDone`** + flat check index), `SprintDetailsModal.jsx` (**`invokeGetUserStories`**, **`fetchOpenBugs`**, **`invokeSetCheckDone`** + flat index + `customFields.CardId`), `UserStoryModal.jsx` (**`invokeGetUserStories`** + response shape handling).

**Done (2026-03-20b):** **`boardMediaStrAppers.js`** — `fetchBoardMedia` / `saveBoardMedia` → `GET /api/Media/use/get-media/{boardId}`, `POST /api/Media/use/update-media` (maps `linkedinUrl` → `linkedInUrl`); **`DiagramsModal.jsx`** + **`MediaModal.jsx`** wired.

**Done (2026-03-20c):** **`MediaModal.jsx`** load path uses **`fetchBoardMedia`** (fixes missing `base44` import / StrAppers path). **`MyTasksModal.jsx`** uses **`invokeGetMyTasksForModal`** for load (not raw `getMyTasks`). **`base44Client.js`** intercepts when StrAppers is set: **`getCRMMetadata`** (parallel `categories` + `statuses`), **`getStakeholders`**, **`createStakeholder`**, **`updateStakeholder`**, **`deleteStakeholder`**, **`submitSupportRequest`** → `POST /api/Support`. New **`crmStrAppers.js`** / **`supportStrAppers.js`** for optional direct imports (same routes).

**Done (2026-03-21):** **`SupportModal.jsx`** → **`invokeSubmitSupportRequest`** (`supportStrAppers.js`). **`useBugHandler.jsx` / `useBugLogging.jsx`** → **`createBugOnBoard`** (`dashboardStrAppers.js`) with **`creator: studentData?.email`** (API requires email, not numeric id). **`base44Client`** create-bug redirect extended to **`logBugHandler`** with **`creator || email || Email`**.

**Done (2026-03-21d):** **Employer slice** — new **`src/api/employerStrAppers.js`** (dual-path): roles, students-by-role, student board data, shortlisted candidates, invite check, candidate follow APIs, observe board, Teams `create-meeting-smtp-for-board-employer`, invite-to-board, get/create employer, login/change-password helpers, job ads **`POST /api/Employers/Adds/use/create`**. **`EmployerSearch.jsx`**, **`EmployersPortal.jsx`**, **`EmployerJobAd.jsx`** wired (job ad loads `employerId` via `getEmployerByEmail`; logo upload still Base44 `integrations` on portal).

**Done (2026-03-19e):** **Projects / student (browse + allocate)** — **`src/api/projectsStudentStrAppers.js`**: student-by-email (via **`boardEntityStrAppers`**), available projects, orgs, criteria, allocated-by-email, engagement rules (**re-export** from **`boardRoomStrAppers`**), hot projects (normalizes API comma-string → `{ ids }`), project students, is-allocatable, allocate/deallocate (JSON bodies per `AllocateStudentRequest` / `DeallocateStudentRequest`), allocate-with-priority (pads to 4 route segments, `0` = clear), **`notifyApplicant`** → **`POST /api/Email/use/notify-applicant`**. **`ProjectsStudentV2.jsx`**, **`CategoryProjects.jsx`**, **`Checkout.jsx`** wired; Checkout localStorage org list → id-map fix; Checkout DB fallback without localStorage uses StrAppers (**`getStudentByEmail`** + **`allocated-projects`** + **`getAllOrganizations`**) when env set, else Base44 entities. Avatar prefetch uses **`VITE_STRAPPERS_BACKEND_URL`** when set (no `Deno.env`). **`Pending.jsx`**: static confirmation page only — **`base44.auth.logout`** on logo click until auth migration.

**Done (2026-03-21e):** **Board CRM modals** — **`CRMDashboardModal.jsx`** + **`AddStakeholderModal.jsx`** use **`crmStrAppers.js`** (`invokeGetCRMMetadata`, `invokeGetStakeholders`, `invokeDeleteStakeholder`, `invokeCreateStakeholder`, `invokeUpdateStakeholder`) instead of raw **`base44.functions.invoke`**; stakeholder list normalizes **`stakeholders` / `Stakeholders`**.

**Done (2026-03-21f):** **Landing + early-bird (dual-path toward detach)** — new **`src/api/skillInWebsiteStrAppers.js`**: **`invokeGetStripImage`** → absolute URL **`GET /api/skill-in-website/render-image?id=`** (StrAppers serves bytes; `<img src>` works cross-origin); **`invokeRegisterUser`** → **`POST /api/skill-in-website/register`**. **`LandingNew.jsx`** uses **`invokeGetStripImage`**; **`RegistrationModal.jsx`** uses **`invokeGetRoles`** (`employerStrAppers.js`) + **`invokeRegisterUser`**. **`base44Client`** intercepts **`getStripImage`** + **`registerUser`** when StrAppers URL is set (safety net).

**Done (2026-03-19g):** **Student registration / edit profile, org registration / edit, ProjectsOrg** — **`studentRegistrationStrAppers.js`** (majors, years, roles, langs, create/update student, GitHub login URL), **`organizationStrAppers.js`** (terms, create, update org), **`projectsOrgStrAppers.js`** (projects by org, boards by project, member count, suspend/activate). Pages wired off **`@/functions/*`** for those flows when **`VITE_STRAPPERS_BACKEND_URL`** is set. **`boardEntityStrAppers.getStudentByEmail`** now normalizes API casing via **`normalizeStudentRecord`**. **Landing** footer `mailto:` uses **`VITE_SUPPORT_EMAIL`** or **`support@strappers.app`** (not skill-in.com). **`getProjectsByOrgId`** accepts **`orgId`** or **`organizationId`**.

**Next (lead order) — superseded:** **Checkpoint — 2026-03-24** marks migration **complete**; items below are historical.

**Detach readiness (optional):** **`docs/BASE44_FINAL_DETACH_CHECKLIST.md`** — SDK removal, CDN URLs, etc. — only if you want a hard Base44 detach.

**Still optional (non-blocking):** `npm run build` / `ChangePassword.jsx` issues; any leftover Base44 fallbacks.

---

### Checkpoint — 2026-03-19 (session end)

**Dual-path:** If `VITE_STRAPPERS_BACKEND_URL` is set → direct StrAppers HTTP; else → `base44.functions.invoke`. Policy: **no new backend routes** just to fill audit gaps (`docs/BASE44_BACKEND_ENDPOINT_AUDIT.md`).

| Done in `strAppersFrontend` | Notes |
|----------------------------|--------|
| `src/api/strAppersBackend.js` | `strAppersBackendJson` / Fetch; env normalization for `VITE_STRAPPERS_BACKEND_URL` |
| `src/api/mentorGitStrAppers.js` | Git status: **GET is-open-branch + pr-status** only (not obsolete `branch-status`); bulk + per-sprint |
| `src/api/mentorChatStrAppers.js` | Mentor/customer models/history/respond + system message centralized dual-path |
| `src/api/boardChatStrAppers.js` | Centralized dual-path for group/private chat (StrAppers + Base44 fallback) |
| `ChatSidebar.jsx` | Mentor/customer chat; dev git via **`mentorGitStrAppers` `*Dual`** (bulk status, row refresh, branch/PR/merge/review); private poll `fetchPrivateChatApi`; Base44 fallbacks inside dual helpers |
| `MentorChatModal.jsx` | Models + respond StrAppers-first |
| `GroupChat.jsx` / `PrivateChat.jsx` | Group/private chat StrAppers-first |
| `boardRoomStrAppers.js` + `BoardRoom.jsx` + `SprintProgressPanel.jsx` + **`MyTasksModal` / `SprintDetailsModal` / `UserStoryModal`** | Stats, meeting URL, engagement rules, sprint schedule, per-label tasks, **get-user-stories**, **set-done**, **`invokeGetMyTasksForModal`** (merged label GETs + lists/cards for My Tasks modal) |
| `boardEntityStrAppers.js` + `BoardRoom.jsx` | Student-by-email, project, org (by project / by id), team-by-board (dual-path) |
| `boardCollabStrAppers.js` + `useFigmaLinks` / `useResourceLinks` / `BoardRoom.jsx` | Figma + non-Figma resources list/add/modify/delete + single-url set + Teams schedule (dual-path) |
| `dashboardStrAppers.js` + `useDashboardStats` + `logBugToBackend.jsx` | Dashboard stats/open-bugs + **create bug** `POST /api/Boards/use/create-bug` dual-path |

**User-verified (dev.host):** `dev.skill-in.com` in Network for mentor + board chat + git actions when triggered.

**Follow-ups:** `npm run build` may still fail on unrelated `ChangePassword.jsx` import; Azure + auth migration later per plan.

### Progress snapshot (historical — pre-2026-03-24)

**Superseded:** Migration declared **complete** in **Checkpoint — 2026-03-24**. Table kept for archive.

Rough **frontend base44 → StrAppers (dual-path / cutover) progress:**

| Scope | Estimate | Notes |
|-------|-----------|--------|
| **Whole app vs `BASE44_FRONTEND_INVOKE_INDEX.md` (~83 names)** | **~50–55%** | Many indexed names are other screens/APIs not hit in the Board Room path; current `src/` still has **fallback** `invoke` lines only inside dual-path components (env unset). |
| **Board Room + sidebar/chat/git/tasks (your tested slice)** | **~80–85%** | With `VITE_STRAPPERS_BACKEND_URL` set, the main board surface is StrAppers-first; Base44 remains as fallback. |

**User-tested:** BoardRoom **`boardRoomStrAppers`** batch passed; **`boardEntityStrAppers`** (student / project / org / team loaders) added — **please verify** Network calls when opening BoardRoom with `VITE_STRAPPERS_BACKEND_URL` set.

**Current status:** **`BoardRoom.jsx`** now uses **`boardRoomStrAppers.js`** + **`boardEntityStrAppers.js`** + **`boardCollabStrAppers.js`** and `useFigmaLinks`/`useResourceLinks` dual-path; mentor/customer chat models/history/respond/system message are centralized via **`mentorChatStrAppers.js`**. Legacy fallbacks remain by design when backend URL is unset.

**Next step (superseded by §2026-03-21 checkpoint):** See **“Next (lead order)”** under archived **2026-03-20** block below, or user’s chosen page. Final SDK removal = **`docs/BASE44_FINAL_DETACH_CHECKLIST.md`**.

**Mentor git (#3):** `mentorGitStrAppers.js` exposes `fetchBoardGitStatusesDual`, `fetchGitRowDual`, `createGithubBranchDual`, `createGithubPRDual`, `mergeGithubBranchDual`, `reviewCodeDual` — `ChatSidebar.jsx` uses them only; `base44Client` intercepts the four POST `invoke` names when StrAppers is configured.

## Copy-paste prompt for new agent

Paste this into a **new chat** (edit the bracketed part if your priority changed):

> Read **`docs/AGENT_RESUME.md`** (start at **Checkpoint — 2026-03-24** — migration **complete**), **`docs/STRAPPERS_BACKEND_CLIENT.md`**, **`docs/BASE44_FINAL_DETACH_CHECKLIST.md`** (optional hard-detach), and **`docs/BASE44_MIGRATION_PLAN.md`** (reference).  
> **Context:** Base44 → StrAppers migration is **done**; API on Azure; Google OAuth via **`GoogleAuth`** in Azure App Service settings. Dual-path / Base44 fallbacks may still exist until optional checklist cleanup.  
> **Continue from:** New user task (bug/feature), or **optional** final-detach items in **`docs/BASE44_FINAL_DETACH_CHECKLIST.md`**.  
> Policy (unchanged): avoid **new** backend routes **only** to fill audit gaps (`docs/BASE44_BACKEND_ENDPOINT_AUDIT.md`); map real routes from `docs/BASE44_AUDIT_API_PATHS_FROM_DISK.txt` / `BASE44_FRONTEND_INVOKE_INDEX.md`. Full file index: `docs/FRONTEND_SRC_FILE_INDEX.md` if glob is incomplete in IDE.

---

## Optional: git as checkpoint

After a good stopping point: `git add` / `git commit` with a message like `docs: migration complete checkpoint` so the next session can use `git log -1` / diff to see state.
