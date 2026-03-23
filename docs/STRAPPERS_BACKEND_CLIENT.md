# StrAppersBackend client (frontend scaffold)

**Location:** `strAppersFrontend/src/api/strAppersBackend.js`  
**Env:** `VITE_STRAPPERS_BACKEND_URL` (see `strAppersFrontend/.env.example`)

## Purpose

Thin `fetch` wrapper for calling **StrAppersBackend** directly from the Vite app. This is the foundation for **§3** in `BASE44_MIGRATION_PLAN.md`: over time, `base44.functions.invoke('X')` can be replaced by `strAppersBackendJson('/api/...')` (or small wrapper functions) **without** adding new backend routes unless you choose to.

## Setup

**Beginner walkthrough:** [`docs/STEP_BY_STEP_VITE_BACKEND_URL.md`](STEP_BY_STEP_VITE_BACKEND_URL.md)

1. In `strAppersFrontend`: copy `.env.example` → `.env`
2. Set `VITE_STRAPPERS_BACKEND_URL` to your API origin (no trailing slash), e.g. `https://api.example.com`
3. Restart `npm run dev` after changing `.env`

### Dev vs prod env split (recommended)

- `.env.development` → dev values (e.g. `VITE_STRAPPERS_BACKEND_URL=https://dev.skill-in.com`)
- `.env.production` → prod values (e.g. `VITE_STRAPPERS_BACKEND_URL=https://api.skill-in.com`)
- Keep Base44 auth/function values separate:
  - `VITE_BASE44_APP_ID=<base44 app id>`
  - `VITE_BASE44_BACKEND_URL=<Base44 app subdomain/custom domain>`
  - Do **not** use `https://app.base44.com` for function calls; use your app domain.
- For local legacy imports (`@/functions/*`), ensure `BASE44_LEGACY_SDK_IMPORTS=true` is set before `npm run dev` (or wired in scripts).

## API

| Export | Description |
|--------|-------------|
| `getStrAppersBackendBaseUrl()` | Returns normalized base or `''` |
| `isStrAppersBackendConfigured()` | `true` if env is set |
| `strAppersBackendFetch(path, init)` | Same as `fetch` to `base + path` |
| `strAppersBackendJson(path, init)` | Parses JSON; throws on non-OK |

`path` must start with `/` (e.g. `/api/Mentor/use/get-models`).

### Auth / `User/me`

When **`VITE_STRAPPERS_BACKEND_URL`** is set:

1. **`base44Client`** wraps **`base44.auth.me()`** (no Base44 network call); returns **`{ email }`** from **`localStorage.userEmail`** or **`null`**.
2. **`AuthContext`** skips **`checkUserAuth`** → **`me()`** in that mode.
3. **`strAppersBackend.js`** installs a **`window.fetch`** shim (see **`main.jsx`** side-effect import) so any SDK **`fetch`** to **`/entities/User/me`** is answered locally with the same JSON shape — avoids 401s the wrapper might miss.

Employer flows should set **`userEmail`** in localStorage when not using Base44 session.

**Login / create-account dialog (`AuthDialog.jsx`) — migration status**

- **Dual-path when `VITE_STRAPPERS_BACKEND_URL` is set:** email/password login (`authLoginStrAppers.js`), employer login/check (`employerStrAppers.js`), password change (`changePasswordStrAppers.js`), signup + forgot-password email checks (`authPrecheckStrAppers.js` → `GET /api/Students/use/by-email/...`, `GET /api/Organizations/use/{email}`, employer existence). Standalone **`pages/ChangePassword.jsx`** uses `changePasswordStrAppers` only.
- **Leftover (intentional for now):** **Google OAuth** still uses **`@/functions/googleAuth`** (Base44/Deno). Replacing it is a separate auth phase; do not block HTML/email login migration on it.
- **Where Base44 functions live on disk:** e.g. `checkUserByEmail` and **`checkEmailExists`** are under **`strAppersFrontend/base44/functions/<name>/`**. The Vite alias **`@/functions/*`** may not mirror those folders in your tree—prefer **`src/api/*StrAppers.js`** for new code.
- **`authSessionStrAppers.js`:** `performAppLogout` / `clearAppSessionLocalKeys` — when **`VITE_STRAPPERS_BACKEND_URL`** is set, logout skips **`base44.auth.logout`** (full `localStorage.clear()` still used where pages request it). **`AuthContext`** skips Base44 **public-settings** bootstrap in StrAppers mode so the shell loads without that network call.

## CORS

The backend must allow your frontend origin. Configure in StrAppers ASP.NET when you host the new SPA origin.

## Already wired (dual path)

- **`ChatSidebar.jsx` / `MentorChatModal.jsx`:** mentor + customer `get-models`, `chat-history`, `respond/{model}` when configured; else Base44.
- **`mentorGitStrAppers.js` + `ChatSidebar.jsx`:** dual-path `fetchBoardGitStatusesDual` / `fetchGitRowDual` (StrAppers `is-open-branch` + `pr-status`, or legacy triple invoke); `createGithubBranchDual`, `createGithubPRDual`, `mergeGithubBranchDual`, `reviewCodeDual` → `POST` `github-branch`, `github-pr`, `github-merge`, `code-review`. `base44Client` intercepts those four `invoke` names when StrAppers is configured.
- **`mentorChatStrAppers.js` + `ChatSidebar` / `MentorChatModal`:** centralized dual-path for mentor/customer chat models/history/respond and `push-mentor-system-message`; StrAppers endpoints when configured, Base44 fallback otherwise.
- **`ChatSidebar.jsx`:** `push-mentor-system-message`, `github-branch`, `github-pr`, `github-merge`, `code-review` when configured; else matching `base44.functions.invoke`.
- **`boardChatStrAppers.js` + `GroupChat` / `PrivateChat` / `ChatSidebar` (private):** centralized dual-path in one API: when configured uses `GET /api/Boards/use/chat`, `POST /api/Boards/use/chat-add`; else falls back to `getBoardChat`, `getPrivateChat`, `addBoardChatMessage`, `addPrivateChatMessage`.
- **`boardRoomStrAppers.js` + `BoardRoom.jsx` / `SprintProgressPanel.jsx` / `MyTasksModal` / `SprintDetailsModal` / `UserStoryModal`:** when configured — `GET /api/Boards/use/stats/{boardId}`, `GET /api/Teams/use/get-custom-url`, `GET /api/Projects/use/engagement-rules`, `GET /api/Boards/use/sprint-schedule`, multi-label `GET /api/Trello/use/board/{boardId}/label/{label}` (including **`invokeGetMyTasksForModal`**: merge lists+cards per role + optional Bugs label), **`GET /api/Boards/use/stats/get-user-stories`**, **`POST /api/Trello/use/set-done`**; else matching `base44.functions.invoke`.
- **`boardEntityStrAppers.js` + `BoardRoom.jsx`:** when configured — `GET /api/Students/use/by-email/{email}`, `GET /api/Projects/use/{id}`, `GET /api/Organizations/use/by-project/{projectId}`, `GET /api/Organizations/use/by-id/{id}`, `GET /api/Students/use/by-board/{boardId}` (team); else legacy `@/functions/*` loaders.
- **`boardCollabStrAppers.js` + `useFigmaLinks` / `useResourceLinks` / `BoardRoom.jsx`:** when configured — Figma via `GET /api/Resources/use/figma`, non-Figma resource links via `GET /api/Resources/use/resources-all` and `GET /api/Resources/use/resources-by-student`, writes via `POST /api/Resources/use/add|modify|delete`, and meeting scheduling via `POST /api/Teams/use/create-meeting-smtp-for-board`; else legacy `figmaLinks` / `resourceLinks` / `setFigmaUrl` / `scheduleTeamsMeeting`.
- **`boardMediaStrAppers.js` + `DiagramsModal.jsx` / `MediaModal.jsx`:** `GET /api/Media/use/get-media/{boardId}`, `POST /api/Media/use/update-media`; else `getBoardMedia` / `saveBoardMedia` invoke.
- **`dashboardStrAppers.js` + `useDashboardStats` / `SprintProgressPanel` / `BugDetailsModal` / `logBugToBackend.jsx`:** dashboard stats via `GET /api/Boards/use/trello-dashboard-stats`; open bugs via `GET /api/Boards/use/get-all-open-bugs`; bug detail modal via `GET /api/Boards/use/get-bug` and save via `POST /api/Boards/use/modify-bug`; **new bug** via `POST /api/Boards/use/create-bug` (`createBugOnBoard`); else legacy `getDashboardStats` / `getOpenBugs` / `getBugDetails` / `modifyBug` / `logBugToBackend`. `base44Client` also intercepts those invokes when StrAppers is configured.
- **`crmStrAppers.js` / `supportStrAppers.js`:** optional explicit imports — CRM `GET /api/CRM/use/categories|statuses|stakeholder`, `POST add|update|delete-stakeholder`; support `POST /api/Support`. **`base44Client`** intercepts matching **`getCRMMetadata`**, **`getStakeholders`**, **`createStakeholder`**, **`updateStakeholder`**, **`deleteStakeholder`**, **`submitSupportRequest`** when StrAppers URL is set.
- **`skillInWebsiteStrAppers.js` + `LandingNew.jsx` / `RegistrationModal.jsx`:** `GET /api/skill-in-website/render-image?id=` (image URL for `<img src>`), `POST /api/skill-in-website/register` (early-bird); roles in modal via **`employerStrAppers.invokeGetRoles`**. **`base44Client`** intercepts **`getStripImage`** and **`registerUser`** when StrAppers URL is set.
- **`studentRegistrationStrAppers.js` + `StudentRegistration.jsx` / `EditStudentProfile.jsx`:** majors/years/roles/programming languages, **`POST /api/Students/use/create`**, **`POST /api/Students/use/update/{id}`** (+ refetch by email), **`GET /auth/github/login-url`**; roles reuse **`employerStrAppers.invokeGetRoles`**. **`EditStudentProfile`** loads student via **`boardEntityStrAppers.getStudentByEmail`** (normalized fields).
- **`organizationStrAppers.js` + `OrganizationRegistration.jsx` / `EditOrganizationProfile.jsx`:** **`GET /api/Organizations/use/get-terms/{language}`**, **`POST .../use/create`**, **`POST .../use/update/{id}`**; org load on edit still **`boardEntityStrAppers.getOrganizationById`**.
- **`projectsOrgStrAppers.js` + `ProjectsOrg.jsx`:** **`GET /api/Projects/use/by-organization/{id}`**, **`GET /api/Boards/use/boards?projectId=`** (404 → empty boards), member count via **`GET /api/Students/use/by-board/{boardId}`**, suspend/activate **`POST /api/Projects/use/suspend|activate/{id}`**; org header load via **`boardEntityStrAppers`**.
- **`systemDesignStrAppers.js` + `projectWizardStrAppersHttp.js` + `CreateProjectModal.jsx` / `ProjectWizardModal.jsx`:** when StrAppers URL is set — **`POST /api/Projects/use/create`**, **`POST /api/Projects/use/update/{id}`**, **`POST /api/SystemDesign/use/initiate-modules`**, **`POST /api/SystemDesign/use/create-data-model`**, **`GET /api/SystemDesign/use/data-schema/{projectId}`** (HTML via **`strAppersBackendText`** → `{ html }` for the UI), **`GET /api/SystemDesign/use/refine-module`**, **`POST /api/SystemDesign/use/update-data-model/{projectId}`**, **`POST /api/SystemDesign/use/update-module`**, **`POST /api/SystemDesign/use/bind-modules`**, **`POST /api/SystemDesign/use/download-project-design`** (JSON with base64 ZIP: markdown + optional PDF + optional `DataSchema.png`), plus ModuleCount / ModuleDescription; else matching Base44 invokes. **`base44Client`** also routes these function names when StrAppers is configured.

**Landing support email:** optional **`VITE_SUPPORT_EMAIL`** in `.env` (must use the **`VITE_`** prefix or Vite will not expose it). Restart **`npm run dev`** after changing env. Footer uses **`src/config/supportEmail.js`**; default `support@strappers.app`.

## Next steps (when you migrate a screen)

1. Pick an invoke from `BASE44_FRONTEND_INVOKE_INDEX.md`
2. Open matching `base44/functions/<name>/entry.ts` for method, path, body
3. Skip URLs flagged obsolete in `BASE44_BACKEND_ENDPOINT_AUDIT.md`
4. Call `strAppersBackendJson` from a hook or service; keep `base44` until the screen is fully switched

---

*Part of base44 → StrAppers migration docs.*
