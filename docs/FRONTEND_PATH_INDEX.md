# Frontend path index (append-only)

**Purpose:** Running list of important UI/API locations. **When the user states where a page or file lives, append a row here** (and keep paths repo-relative from `C:\StrAppers`).

**Convention:** One line per discovery; short note in *Context*.

| Path | Context |
|------|---------|
| `strAppersFrontend/src/pages/ProjectsStudentV2.jsx` | Student project browsing v2; loads applicant counts via `invokeGetProjectStudents` |
| `strAppersFrontend/src/pages/CategoryProjects.jsx` | Category / my-projects views; same student-list API |
| `strAppersFrontend/src/components/projects/v2/ProjectDetailsV2.jsx` | Project detail panel; applicants fetch; attached-below-tabs uses full-bleed gradient + flex header/body (panel 16px under Applicants) |
| `strAppersFrontend/src/components/projects/v2/ProjectSection.jsx` | Student sections grid; desktop details panel with `attachedBelowTabs` |
| `strAppersFrontend/src/api/projectsStudentStrAppers.js` | `invokeGetProjectStudents` → `/api/Projects/use/get-students/{id}` |
| `strAppersFrontend/src/lib/cookieConsentStorage.js` | Landing cookie consent: `localStorage` key `skillin_cookie_consent_v1`, `openCookieSettings()`, `hasAnalyticsConsent()` / `hasMarketingConsent()` for gating tags |
| `strAppersFrontend/src/pages/LandingNew.jsx` | Marketing landing; footer: logo, Privacy Policy / Terms of Use / Cookie settings (`createPageUrl`), social + mail |
| `strAppersFrontend/src/components/landing/CookieConsentBar.jsx` | Landing cookie banner + settings modal + FAB; footer uses `openCookieSettings()` from `LandingNew.jsx` |

### Preview ProjectsStudentV2 locally (before deploy)

1. **`strAppersFrontend/.env`** — set `VITE_STRAPPERS_BACKEND_URL` to your API base (e.g. dev IIS `http://localhost:9001` — no trailing slash). Copy from `.env.example` if needed. Restart Vite after changes.
2. From **`strAppersFrontend`**: `npm install` (once), then **`npm run dev`**.
3. Open **`http://localhost:5173/ProjectsStudentV2`** (Vite default port; check terminal if different).
4. You need a **student session**: the page reads `localStorage.userEmail` (set after your normal login / Google flow). Without it, load may fail or show errors.

---

*Add new rows below as the user points out paths — do not remove historical rows unless obsolete.*
