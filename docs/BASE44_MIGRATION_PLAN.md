# Base44 Migration Plan (No Auth Yet)

**Purpose:** When you want to start the migration, say: *"Let's do the base44 migration from the plan"* and reference this file. This is the plan only — no hands-on until you ask to execute.

**Goal:** Detach from base44 by moving all base44 function behavior into a new StrAppers backend branch and a new frontend project, with frontend hosted on Azure App Service. Google/auth is explicitly **out of scope** for this phase (do later).

---

## 1. Scope for This Phase

### In scope
- **New backend branch** from `strAppersBackend` with any new/updated endpoints so the frontend can call your API directly instead of `base44.functions.invoke(...)`.
- **New frontend project** that calls the new backend (no base44 SDK for functions). Auth can remain base44 for this phase or be a minimal placeholder (e.g. “logged in as dev” for testing).
- **Azure App Service** setup for hosting the new frontend (static site / SPA).
- **Mapping:** Every `base44.functions.invoke('X', payload)` becomes a direct HTTP call to StrAppersBackend (e.g. `GET/POST /api/...`). Logic currently in `strAppersFrontend/base44/functions/*/entry.ts` is either already in StrAppersBackend or is ported there.

### Out of scope (for later)
- Google (or other) authentication — replace base44 auth in a separate phase.
- base44 entities (e.g. `base44.entities.StudentProjectAllocation`, `Project`, `Employer` in Checkout) — those need backend endpoints or existing ones mapped; can be part of this plan’s “API surface” but not full auth flow.

---

## 2. Backend Work (New Branch from strAppersBackend)

1. **Create branch**  
   e.g. `feature/detach-base44` or `feature/api-for-frontend-no-base44` from current `strAppersBackend` main.

2. **Ensure one backend endpoint per base44 function**  
   For each base44 function used by the frontend, ensure StrAppersBackend exposes an equivalent route (same or similar request/response shape). Many already exist (e.g. `is-open-branch`, `pr-status`, `github-branch`, `github-pr`, `askMentor`, etc.). Add any that are missing.

3. **Add missing route (known gap)**  
   - **`branch-status`** — base44 calls `GET /api/Mentor/use/branch-status?...`. Backend does not have this route. Either:
     - Add `GET /api/Mentor/use/branch-status` that returns `branchExists`, `branchMerged` (e.g. by reusing logic from `is-open-branch`), or
     - Change the (new) frontend to use only `is-open-branch` + `pr-status` and drop the `branch-status` call.

4. **Optional: API version or prefix**  
   If you want a clear “new frontend” surface, consider a route prefix (e.g. `/api/v2/...`) or a small doc of “endpoints used by the new frontend.”

5. **CORS**  
   Ensure CORS allows the new frontend origin (e.g. Azure App Service URL and any custom domain).

6. **Auth for this phase**  
   For “plan only” you can document: “Endpoints will accept a placeholder header or no auth until Google auth is implemented.” No implementation of Google auth in this phase.

---

## 3. Frontend Work (New Frontend Project)

1. **New project**  
   Create a new frontend app (e.g. clone/copy of `strAppersFrontend` or a new Vite/React app) so the existing Hostinger + base44 app stays untouched until cutover.

2. **Single API client**  
   - One module (e.g. `api/strAppersBackend.js` or `services/backendApi.js`) that holds the base URL of StrAppersBackend and exposes methods for each former base44 invoke (e.g. `getMentorChatHistory(boardId, ...)`, `checkIsOpenBranch(boardId, branchName, isBackend)`, `getBoardStats(boardId)`, etc.).
   - All former `base44.functions.invoke('X', payload)` become calls to this client (e.g. `backendApi.getBoardStats(boardId)` → `fetch(`${baseUrl}/api/...`)`).

3. **Remove base44 SDK for functions**  
   - Remove or stub `base44.functions.invoke` usage everywhere in the new frontend.
   - Keep base44 auth for this phase if needed (or a minimal “mock logged in” for dev).

4. **Replace base44.entities (Checkout, etc.)**  
   - Replace `base44.entities.StudentProjectAllocation.filter(...)`, `Project.filter(...)`, `Employer.filter(...)` with backend API calls (new or existing endpoints that return the same data).

5. **Config**  
   - Backend base URL (e.g. `VITE_STRAPPERS_BACKEND_URL` or `REACT_APP_STRAPPERS_BACKEND_URL`) so the new frontend points to the new backend branch’s URL.

6. **No Deno**  
   - The new frontend does not call base44; it only talks to StrAppersBackend (and optionally base44 for auth until auth is migrated).

---

## 4. List of base44 Functions to Map (Reference)

Use this list to ensure every invoke has a backend endpoint. Frontend locations are approximate.

| base44 function name | Frontend usage (main files) | Backend endpoint to use or add |
|----------------------|----------------------------|---------------------------------|
| `getMentorChatHistory` | ChatSidebar | Mentor/use/chat-history (verify route) |
| `getCustomerChatHistory` | ChatSidebar | Backend chat endpoint |
| `getPrivateChat` | ChatSidebar | Backend private chat |
| `getBoardGitStatuses` | ChatSidebar | Bulk: multiple Mentor/use calls or one new bulk endpoint |
| `checkBranchStatus` | ChatSidebar | Add or use Mentor/use/branch-status or is-open-branch |
| `checkPRStatus` | ChatSidebar | Mentor/use/pr-status |
| `checkIsOpenBranch` | ChatSidebar | Mentor/use/is-open-branch |
| `pushMentorSystemMessage` | ChatSidebar | Mentor/use/push-mentor-system-message |
| `getMentorModels` | ChatSidebar, MentorChatModal | Mentor/use/get-models |
| `createGithubBranch` | ChatSidebar | Mentor/use/github-branch |
| `createGithubPR` | ChatSidebar | Mentor/use/github-pr |
| `mergeGithubBranch` | ChatSidebar | Mentor/use/github-merge |
| `reviewCode` | ChatSidebar | Mentor/use/code-review |
| `askMentor` | ChatSidebar, MentorChatModal | Mentor/use/respond or equivalent |
| `askCustomer` | ChatSidebar | Backend customer chat endpoint |
| `getBoardStats` | BoardRoom | Backend board stats endpoint |
| `getMeetingUrl` | BoardRoom | Backend meeting URL endpoint |
| `getProjectEngagementRules` | BoardRoom | Backend project rules endpoint |
| `getMyTasks` | BoardRoom | Backend tasks endpoint |
| `getSprintSchedule` | BoardRoom | Backend sprint schedule endpoint |
| `getDashboardStats` | useDashboardStats | Backend dashboard stats |
| `getBugDetails` | BugDetailsModal | Backend bug details |
| `modifyBug` | BugDetailsModal | Backend modify bug |
| `getOpenBugs` | SprintProgressPanel, SprintDetailsModal | Backend open bugs |
| `getUserStories` | SprintProgressPanel, UserStoryModal, SprintDetailsModal | Backend user stories |
| `setCheckDone` | SprintProgressPanel, SprintDetailsModal | Backend set check done |
| `resourceLinks` (list/list-by-student/delete etc.) | useResourceLinks | Backend resource links API |
| `figmaLinks` (list/delete etc.) | useFigmaLinks | Backend figma links API |
| `getBoardMedia` / `saveBoardMedia` | MediaModal | Backend media API |
| `addPrivateChatMessage` | PrivateChat | Backend private chat message |
| `getRoles` | RegistrationModal | Backend roles |
| `registerUser` | RegistrationModal | Backend register user |
| `checkEmployerByEmail` / `loginEmployer` / `changeEmployerPassword` | AuthDialog | Backend employer auth (or keep base44 for this phase) |
| `getStripImage` | LandingNew | Backend strip image |
| `logBugToBackend` / `logBugHandler` | logBugToBackend, useBugHandler | Backend log bug |
| `submitSupportRequest` | SupportModal | Backend support request |
| `getProjectStudents` | ProjectDetailsV2, ProjectsStudentV2 | Backend project students |
| `getStudentByEmail` | ProjectsStudentV2 | Backend student by email |
| `getAvailableProjects` | ProjectsStudentV2 | Backend available projects |
| `getAllOrganizations` | ProjectsStudentV2 | Backend organizations |
| `getProjectCriteria` | ProjectsStudentV2 | Backend project criteria |
| `getAllocatedProjectsByEmail` | ProjectsStudentV2 | Backend allocated projects |
| `getHotProjects` | ProjectsStudentV2 | Backend hot projects |
| `checkStudentAllocatable` | ProjectsStudentV2 | Backend check allocatable |
| `allocateStudentToProject` | ProjectsStudentV2 | Backend allocate |
| `deallocateStudentFromProject` | ProjectsStudentV2 | Backend deallocate |
| `allocateWithPriority` / `notifyApplicant` | Checkout | Backend allocation + notify |
| (Checkout entities) | Checkout | Backend endpoints for allocations, projects, employers |

Auth-related (out of scope for this phase, keep base44 or placeholder):  
`base44.auth.me`, `base44.auth.logout`, `base44.auth.isAuthenticated`, `base44.auth.redirectToLogin` (Landing, Pending, ProjectsStudentV2, Checkout).

---

## 5. Azure App Service for Frontend

1. **Create App Service** (e.g. “Static Web App” or “Web App” for static files).
2. **Build** the new frontend (e.g. `npm run build`).
3. **Deploy** the build output (e.g. `build/` or `dist/`) to the App Service (e.g. via Azure CLI, GitHub Actions, or VS Code Azure extension).
4. **Configure** the app to use the new backend URL (env or config).
5. **Optional:** Custom domain and HTTPS (can be later).

No hands-on in this phase — just document that “frontend will be deployed to Azure App Service” and the steps above.

---

## 6. Execution Order (When You Start)

1. Create backend branch and add any missing endpoints (including `branch-status` or equivalent).
2. Create new frontend project and API client; replace all base44.functions.invoke and base44.entities with backend calls.
3. Keep auth on base44 (or minimal mock) for this phase.
4. Set up Azure App Service and deploy the new frontend.
5. Test end-to-end (new frontend → new backend); fix CORS and config as needed.
6. Later phase: replace base44 auth with your own (e.g. Google) and remove base44 dependency entirely.

---

## 7. Reminder for Later

When you want to start: *“Let’s do the base44 migration from the plan”* or *“Execute the base44 migration plan in docs/BASE44_MIGRATION_PLAN.md”* — then we proceed step by step with hands-on, using this document as the source of truth.
