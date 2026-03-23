# After a crash — how to resume (for you + the AI)

## What to paste in a new chat

Copy **one** of these:

**Short**

> Read `docs/AGENT_RESUME.md` (checkpoint **2026-03-22**), `docs/BASE44_FINAL_DETACH_CHECKLIST.md`, and `docs/BASE44_MIGRATION_PLAN.md`, then continue from **Next (lead order)** in `AGENT_RESUME` (or execute the final-detach checklist if I ask for cutover).

**If you remember the last task**

> We were doing [e.g. “migrate BoardRoom to strAppersBackend”]. Read `docs/AGENT_RESUME.md` and continue from there.

**Fast handoff (recommended now)**

> Read `docs/AGENT_RESUME.md`, `docs/BASE44_MIGRATION_PLAN.md`, `docs/BASE44_FINAL_DETACH_CHECKLIST.md`, and `docs/STRAPPERS_BACKEND_CLIENT.md`. Continue from the **Checkpoint** and **Next step** below.

---

## Project facts (do not re-argue)

| Topic | Fact |
|-------|------|
| Frontend root | `C:\StrAppers\strAppersFrontend` = **base44 GitHub clone** (Vite + `src/` + `base44/functions` ~126 Deno functions). |
| Backend | `strAppersBackend` in **StrAppers** repo. |
| Policy | **Happy with current frontend.** **Do not** add backend routes just to fill audit gaps. Mark calls **obsolete** only when **100% sure** no matching route exists. |
| Cursor vs disk | IDE may show fewer files than Explorer; audits used **disk** scans. **Also:** Cursor’s **file search / glob** for `strAppersFrontend/src` has returned only a **subset** of `.jsx` files while PowerShell on the same machine sees **159** — use **`docs/FRONTEND_SRC_FILE_INDEX.md`** (regenerate: `.\scripts\GenerateFrontendSrcIndex.ps1`) as the **source of truth** for “what exists on disk.” |

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

## Not done yet (unless you finished it later)

- Replacing **`base44.functions.invoke`** with **`strAppersBackendJson`** screen-by-screen.
- Azure deploy (plan §5).
- Auth migration off base44.

---

## Suggested next step (when continuing implementation)

1. Pick **one** screen or hook.  
2. Map invokes → paths via **`base44/functions/<name>/entry.ts`** + audit (skip obsolete URLs).  
3. Wire **`strAppersBackendJson`**; confirm **CORS** on the API.  

Update this file’s **Next step** line when you change direction.

### Checkpoint — 2026-03-21 (break — **current**)

**User decision:** Final Base44 detach is planned **ASAP**; **hold new development on Base44** (Deno / Base44-only) until cutover. Until then, continue migrating **other modules/pages** using the **dual-path** pattern — Board Room is **StrAppers-first** when `VITE_STRAPPERS_BACKEND_URL` is set; remaining Base44 ties are tracked in **`docs/BASE44_FINAL_DETACH_CHECKLIST.md`** (SDK removal, invoke fallbacks, static assets, `CRMDashboardModal` → `crmStrAppers`, etc.).

**Board Room (recap):** Team Performance uses **`fetchDashboardStats`** + **`useDashboardStats`** (~60s poll). No merged board/detailed-task overlay in board stats (reverted per user). **`base44Client` intercepts** still route many invokes to StrAppers when env is set.

**Next session:** Pick the next **page/module** from the order below (or user’s priority). Do **not** block on final detach checklist until user starts cutover.

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

**Next (lead order) — superseded by checkpoint 2026-03-22:** see **Checkpoint — 2026-03-22** table above. Historical items: landing smoke-test, auth noise, Checkout entities fallback, employer upload, final checklist.

**Detach readiness:** Dual-path coverage is wider; **final detach** still requires checklist items (remove SDK fallbacks, static CDN URLs, auth replacement, etc.) — say when when you want that pass.

**Still optional:** `npm run build` / `ChangePassword.jsx`; Azure; auth off Base44.

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

### Progress snapshot (excludes Azure deploy + auth migration)

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

> Read **`docs/AGENT_RESUME.md`** (start at **Checkpoint — 2026-03-22**), **`docs/STRAPPERS_BACKEND_CLIENT.md`**, **`docs/BASE44_FINAL_DETACH_CHECKLIST.md`**, and **`docs/BASE44_MIGRATION_PLAN.md`** (intro + policy §0.0.2).  
> **Context:** Dual-path migration: when **`VITE_STRAPPERS_BACKEND_URL`** is set, prefer StrAppers HTTP; else Base44 invoke. **Org project wizard** (ProjectsOrg, CreateProjectModal, ProjectWizardModal) is migrated including **`bindModules`** and **`downloadProjectDesign`**; backend needs **`POST /api/SystemDesign/use/download-project-design`** deployed if ZIP download should work on dev/prod. **`Boards/use/boards`** returns **200 + []** when project exists but has no boards (redeploy if still 404).  
> **Continue from:** **Next (lead order)** in checkpoint **2026-03-22** — **[pick: remaining invoke grep / employer integrations upload / final detach checklist / user-specific screen]**.  
> Policy: avoid **new** backend routes **only** to fill audit gaps (`docs/BASE44_BACKEND_ENDPOINT_AUDIT.md`); map real routes from `docs/BASE44_AUDIT_API_PATHS_FROM_DISK.txt` / `BASE44_FRONTEND_INVOKE_INDEX.md`. Full file index: `docs/FRONTEND_SRC_FILE_INDEX.md` if glob is incomplete in IDE.

---

## Optional: git as checkpoint

After a good stopping point: `git add` / `git commit` with a message like `docs: base44 migration checkpoint` so the next session can use `git log -1` / diff to see state.
