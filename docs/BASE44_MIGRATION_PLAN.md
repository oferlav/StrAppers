# Base44 Migration Plan (No Auth Yet)

**Purpose:** When you want to start the migration, say: *"Let's do the base44 migration from the plan"* and reference this file. This is the plan only — no hands-on until you ask to execute.

**Goal:** Detach from base44 by moving all base44 function behavior into a new StrAppers backend branch and a new frontend project. **Hosting:** Hostinger = domain + DNS; **dev** SPA can be served from **IIS** (`dist/`) on a subdomain; **prod** SPA on **Azure**; **prod API** may already run on **Azure App Service** (see **§5**). Google/auth is explicitly **out of scope** for this phase (do later).

### Final detach — tracked checklist (do at cutover, not per screen)

When you are ready to **remove Base44 entirely** (SDK, invokes, analytics), work from:

**`docs/BASE44_FINAL_DETACH_CHECKLIST.md`**

That file lists SDK removal, stripping `invoke` fallbacks from `*StrAppers.js`, direct-`base44` components, static CDN URLs, and verification greps. **Screen-by-screen migration** can continue without completing that list first; the checklist is the **promised** “leftovers” backlog for the last mile.

---

## 0. Repos and execution order (important)

Two separate Git repos are involved:

| Repo | Role |
|------|------|
| **StrAppers** | Backend (`strAppersBackend`), optional copy of frontend under `strAppersFrontend`, migration docs. Feature branch `feature/detach-base44` lives **here**. |
| **skill-in-Frontend** (or **`strAppersFrontend`** under StrAppers) | Source of truth for **base44** Deno functions (`base44/functions/*/entry.ts`) and the frontend. Endpoint audit for `C:\StrAppers\strAppersFrontend` is in `docs/BASE44_BACKEND_ENDPOINT_AUDIT.md`. |

### 0.0 Recorded facts (do not re-litigate)

- **`C:\StrAppers\strAppersFrontend`** is intentionally used as the **clone of the base44 GitHub repo** (Vite app at repo root + **`base44/functions`** with **~126** Deno function folders on disk).
- **IDE / parent `StrAppers` git** may list **fewer** files under `strAppersFrontend` than File Explorer if most of that tree is **not** committed in StrAppers — the **full** HTTP inventory is maintained by scanning **disk** and by **`docs/BASE44_AUDIT_API_PATHS_FROM_DISK.txt`** (see **`docs/BASE44_BACKEND_ENDPOINT_AUDIT.md`**).
- **Step 1 audit** for this project is **done enough to proceed**: invalid paths are flagged there (e.g. `branch-status`, `Boards/use/use/count`, `GET /api/StudentProjects`, `googleAuth` URL verify).

#### 0.0.1 `strAppersFrontend` layout (root + `src` + `base44`)

We treat this repo as **one** project with three important areas:

| Area | Role |
|------|------|
| **Repo root** | Vite/React app config: `package.json` (e.g. `@base44/sdk`, `@base44/vite-plugin`), **`vite.config.js`** (base44 plugin + React), **`jsconfig.json`** (`@/*` → `./src/*`), `tailwind.config.js`, `postcss.config.js`, `eslint.config.js`, `components.json` (shadcn-style UI), `index.html`, `README.md`. |
| **`src/`** | **Main SPA**: `main.jsx`, `App.jsx`, `Layout.jsx`, **`pages/`** (e.g. `BoardRoom`, `ProjectsStudentV2`, registration/checkout flows), **`components/boards/`** (e.g. `ChatSidebar.jsx` + modals), **`components/ui/`**, **`api/`** (`base44Client.js`, entities/integrations), **`lib/`**, hooks, styles — **150+ files** on a full disk clone. |
| **`base44/functions/`** | **Deno** edge functions (`entry.ts` per folder); each performs `fetch` to StrAppersBackend using `BACKEND_API_URL`. |

The **current** product frontend is **acceptable as-is**; migration work is about **knowing** what to retire or remap later, not rewriting this app unless you choose to.

#### 0.0.2 Policy: no backend work to “fill gaps” for the current app

- You are **happy with the current frontend** and do **not** want **new StrAppersBackend endpoints** added just to cover audit gaps (e.g. no adding `branch-status` unless you explicitly ask later).
- For **future** direct-to-backend or post-base44 clients: treat base44 functions as **obsolete** only when we are **100% sure** the **exact URL / pattern** they call has **no** matching route in `strAppersBackend` (controller scan). If unsure (e.g. auth hosted elsewhere), mark **verify**, not obsolete.
- See **`docs/BASE44_BACKEND_ENDPOINT_AUDIT.md`** → **“Obsolete vs client-fix (recorded policy)”** for the shortlist.

**Recommended order**

1. **StrAppers — create migration branch** (on `main` or after syncing `main`):  
   `git checkout -b feature/detach-base44`  
   *(You have already done this.)*

2. **skill-in-Frontend — pull latest** (in a **separate** clone of that repo, not inside StrAppers unless it is a submodule):  
   ```bash
   cd <path-to-your-skill-in-Frontend-clone>
   git fetch origin
   git pull origin main
   ```  
   Use `master` instead of `main` if that is your default branch. This ensures audits and the new frontend use the **current** base44 backend URLs (e.g. after fixes like replacing a missing `branch-status` call).

3. **Optional — sync base44 into StrAppers**  
   If you maintain `strAppersFrontend/base44` as a copy of skill-in-Frontend’s base44 folder, copy or merge those files after pulling skill-in-Frontend so StrAppers matches.

4. **Flag invalid / unused base44 → backend calls** (see §0.1 below) before building the new frontend API client.

5. When you **choose** to move off base44: follow §3 (new client / cutover) using the audit; **do not** assume §2 requires new routes unless you change the policy in §0.0.2.

### 0.1 Flag all unused or invalid base44 → backend API calls

**Task:** Identify and document **every** HTTP call from `base44/functions/*/entry.ts` to StrAppersBackend (or hardcoded backend URLs). For each, record:

- Base44 function name  
- Method + full path (e.g. `GET /api/Mentor/use/...`)  
- Status: **valid** (backend route exists) / **invalid** (404, wrong path, deprecated) / **replaced** (use endpoint X instead)  
- **New frontend:** which endpoint(s) to call instead (never call invalid paths)

Maintain the result in a small audit doc (e.g. `docs/BASE44_BACKEND_ENDPOINT_AUDIT.md`) or extend the table in §4. The **new frontend must not** call any path flagged as invalid or unused; align with whatever **latest** skill-in-Frontend base44 uses after step 2.

**Note:** You do **not** have to add backend routes for every mistake in old base44 if base44 was fixed upstream (e.g. `branch-status` removed in favor of `is-open-branch`). The flag list prevents the new frontend from repeating dead endpoints.

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

## 2. Backend Work (when you actually need it)

**Default (current policy):** Do **not** add backend routes solely to fix base44 audit gaps; keep today’s app behavior as-is.

1. **Branch / deploy**  
   Use your normal process (e.g. `feature/detach-base44` or `main`) when you have real features to ship.

2. **No “one endpoint per base44 function” requirement**  
   Most flows already map to existing controllers. Only add endpoints when **you** want a new capability — not because a Deno function called a dead URL.

3. **`branch-status`** — Still **not** on `MentorController`. **New** clients must not call it; use **`is-open-branch`** + **`pr-status`** (or keep using base44 until cutover). **No** obligation to implement `branch-status` on the backend.

4. **Optional: API version or prefix**  
   If you want a clear “new frontend” surface later, consider `/api/v2/...` or a small published list of supported routes.

5. **CORS**  
   When hosting a new frontend origin, allow it in CORS config.

6. **Auth for this phase**  
   Document expectations (e.g. placeholder header) until Google/auth phase.

---

## 3. Frontend Work (New Frontend Project)

1. **New project**  
   Create a new frontend app (e.g. clone/copy of `strAppersFrontend` or a new Vite/React app) so the existing Hostinger + base44 app stays untouched until cutover.

2. **Single API client**  
   - **Scaffolded:** `strAppersFrontend/src/api/strAppersBackend.js` + `.env.example` (`VITE_STRAPPERS_BACKEND_URL`). Details: **`docs/STRAPPERS_BACKEND_CLIENT.md`**.  
   - Over time, add thin wrappers per feature (e.g. `getMentorChatHistory` → `strAppersBackendJson('/api/Mentor/use/chat-history?...')`) or keep using `strAppersBackendJson` with paths from `base44/functions/*/entry.ts` (see §4).
   - All former `base44.functions.invoke('X', payload)` become calls to this client when you migrate each screen.

3. **Remove base44 SDK for functions**  
   - Remove or stub `base44.functions.invoke` usage everywhere in the new frontend.
   - Keep base44 auth for this phase if needed (or a minimal “mock logged in” for dev).

4. **Replace base44.entities (Checkout, etc.)**  
   - Replace `base44.entities.StudentProjectAllocation.filter(...)`, `Project.filter(...)`, `Employer.filter(...)` with backend API calls (new or existing endpoints that return the same data).

5. **Config**  
   - Backend base URL (e.g. `VITE_STRAPPERS_BACKEND_URL` or `REACT_APP_STRAPPERS_BACKEND_URL`) so the new frontend points to the new backend branch’s URL.
   - Keep **separate env files** for dev/prod in `strAppersFrontend`:
     - `.env.development` → `VITE_STRAPPERS_BACKEND_URL=https://dev.skill-in.com`
     - `.env.production` → `VITE_STRAPPERS_BACKEND_URL=https://api.skill-in.com` (example)
   - Base44 values are separate from StrAppers values:
     - `VITE_BASE44_APP_ID=<base44 app id>`
     - `VITE_BASE44_BACKEND_URL=<your Base44 app subdomain/custom domain, not app.base44.com>`
   - `BASE44_LEGACY_SDK_IMPORTS=true` must be present when local dev needs `@/functions/*` compatibility.

6. **No Deno**  
   - The new frontend does not call base44; it only talks to StrAppersBackend (and optionally base44 for auth until auth is migrated).

---

## 4. base44 → backend mapping (reference)

**Authoritative lists (regenerate on disk when the app changes):**

| Doc | Contents |
|-----|----------|
| **`docs/BASE44_FRONTEND_INVOKE_INDEX.md`** | All **`base44.functions.invoke('…')`** names used under **`strAppersFrontend/src`** (**83** unique as of last scan). |
| **`docs/BASE44_AUDIT_API_PATHS_FROM_DISK.txt`** | Each **`base44/functions/*/entry.ts`** → `/api/...` path(s) used in `fetch`. |
| **`docs/BASE44_BACKEND_ENDPOINT_AUDIT.md`** | Obsolete URL patterns (e.g. **`branch-status`**, **`/api/StudentProjects`**), policy, cheatsheet. |

**When building a direct backend client (§3):** for each invoke name, open the matching folder under **`base44/functions/<sameName>/entry.ts`** (if present) and copy method, path, and body/query shape — **except** paths flagged obsolete in the audit (replace with **`is-open-branch`** / **`pr-status`**, correct **`Boards/use/count`**, real allocation APIs, etc.).

**Note:** Some flows use **`@/functions/...`** imports (student profile); those map to the same Deno functions as in **`base44/functions`**.

Auth-related (later phase): `base44.auth.me`, `logout`, `isAuthenticated`, `redirectToLogin`, etc.

---

## 5. Hosting architecture (Hostinger DNS + dev IIS + prod Azure)

**Recorded decision (Skill-In style):** Use **Hostinger** for **domain registration and DNS** (names under `skill-in.com`). Run **production** frontend on **Azure** (App Service or Static Web Apps). Run **development** frontend from **IIS** on the server that already hosts StrAppers **dev** API, using a **separate subdomain** for the SPA.

**Current ops note (project):** **Production StrAppers API** is already deployed to **Azure App Service**. **Hostinger DNS** for that API hostname is **not configured yet** — see **§5.7** (you only *need* Hostinger DNS if you want a **custom** name like `api.skill-in.com`; the default `*.azurewebsites.net` URL works without Hostinger).

### 5.1 One-page picture

| Environment | What | Where it runs | How users / clients reach it |
|-------------|------|----------------|------------------------------|
| **Dev API** | StrAppers backend (existing) | Your server (IIS / Kestrel behind IIS, etc.) | **`https://dev.skill-in.com`** (DNS at Hostinger → dev API) |
| **Dev SPA** | Vite `dist/` (static) | **IIS** (site → folder of built files) | **`https://<dev-frontend-subdomain>.skill-in.com`** (DNS → server public IP / edge) |
| **Prod SPA** | Vite `npm run build` | **Azure App Service** (or Static Web Apps) | Custom domain or Azure default URL; DNS at Hostinger if using your domain |
| **Prod API** | StrAppers backend | **Azure App Service** *(already provisioned for this project)* | **`https://<your-app>.azurewebsites.net`** today; optional **`https://api.…skill-in.com`** after Hostinger DNS + Azure custom domain |

**Hostinger’s job:** Own **`skill-in.com`** and maintain **A / CNAME / TXT** so each chosen hostname resolves to the correct target (dev IIS, Azure App Service endpoints, etc.).

**Azure’s job (prod):** Host **production API** (existing App Service) and **production SPA** (deploy `dist/` to its own App Service or Static Web App). Build pipelines set **`VITE_STRAPPERS_BACKEND_URL`** to whichever **public API base URL** you actually use (custom domain or `azurewebsites.net`).

**IIS’s job (dev SPA):** Serve **`dist/`** as a static site; bind the dev SPA hostname; TLS cert for that hostname.

### 5.2 Why a frontend dev on IIS is OK

After `npm run build`, the app is **HTML + JS + CSS**. IIS can serve it from a physical path. A **second subdomain** (alongside `dev.skill-in.com` for the API) gives a real **HTTPS origin** for the browser (CORS / cookies later).

### 5.3 Build-time env (Vite) — per environment

Each environment needs a **build** with the correct **`VITE_STRAPPERS_BACKEND_URL`** (and other `VITE_*`):

- **Dev SPA** → typically **`https://dev.skill-in.com`** (or your dev API URL).
- **Prod SPA** → the **production API base URL** (see §5.7 — either Azure default hostname or future custom domain).

### 5.4 CORS (StrAppers backend)

Allow every **frontend origin** you use (dev SPA URL, prod SPA URL) in ASP.NET CORS configuration.

### 5.5 Prod SPA on Azure — short steps

1. Create **App Service** or **Static Web Apps** for the SPA (separate from the API app unless you intentionally combine — usually two apps).
2. **Build** with production `VITE_STRAPPERS_BACKEND_URL`.
3. **Deploy** `dist/`.
4. Optional **custom domain** + Hostinger DNS records per Azure portal instructions; enable **HTTPS** (managed cert).

### 5.6 Summary (stakeholders)

**Hostinger holds domain/DNS. Dev API and dev SPA use Hostinger subdomains pointing at your server (API + IIS static SPA). Production API and production SPA run on Azure; DNS at Hostinger is added when you want pretty hostnames under `skill-in.com` — otherwise Azure’s default URL is enough for the API until then.**

### 5.7 Do you *have* to put Hostinger DNS on the Azure backend API?

**No — not required** for the API to work. Azure App Service already gives you a URL like **`https://<app-name>.azurewebsites.net`**. Clients (including a SPA built with `VITE_STRAPPERS_BACKEND_URL=https://<app-name>.azurewebsites.net`) can call that **without** any change at Hostinger.

**You add Hostinger DNS when you want** a name under your brand, e.g. **`https://api.skill-in.com`**: create the record in Hostinger, add the same **custom domain** in Azure App Service, validate (TXT/CNAME), bind TLS. Same pattern for **`https://app.skill-in.com`** for the prod SPA.

**Takeaway:** Hostinger DNS for prod API is **optional until you want a custom subdomain**; dev (`dev.skill-in.com`) can stay as-is on Hostinger pointing at your dev server.

---

*§5 supersedes the old “Azure frontend only” bullet list: Hostinger is the DNS/registrar layer; IIS hosts **dev** SPA static builds; Azure hosts **prod** API (existing) and **prod** SPA.*

---

## 6. Execution Order (When You Start)

**Done (documentation only):** §0.1 audit, **`BASE44_AUDIT_API_PATHS_FROM_DISK.txt`**, **`BASE44_BACKEND_ENDPOINT_AUDIT.md`**, **`BASE44_FRONTEND_INVOKE_INDEX.md`**, policy §0.0.2 (no backend gap-filling for current app).

1. **StrAppers:** use your normal branch (`feature/detach-base44` or `main`) when you ship backend changes.
2. **strAppersFrontend:** `git pull` your base44 repo when you want latest functions/UI.
3. **Re-audit (optional):** Regenerate the two machine lists (§4 table) after major pulls.
4. **Backend:** only when **you** need new features — **not** to implement `branch-status` etc. unless you change policy. Configure CORS when a new origin exists.
5. **New frontend project** (when ready): API client per §3; map each of the **83** invokes using §4 docs; skip obsolete URLs from the audit.
6. Keep auth on base44 (or minimal mock) for this phase.
7. Hosting per **§5** (prod SPA on Azure; optional custom DNS on Hostinger; prod API on Azure may already exist).
8. Test end-to-end; fix CORS and config as needed.
9. Later phase: replace base44 auth and remove base44 dependency entirely.

---

## 7. Reminder for Later

When you want to start: *“Let’s do the base44 migration from the plan”* or *“Execute the base44 migration plan in docs/BASE44_MIGRATION_PLAN.md”* — then we proceed step by step with hands-on, using this document as the source of truth.
