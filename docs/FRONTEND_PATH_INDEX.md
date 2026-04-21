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
| `strAppersFrontend/src/pages/SchoolsAndUniversities.jsx` | Marketing `/SchoolsAndUniversities`: Solutions → schools/universities audience; hero + 5 feature cards (3+2 grid) + CTA; `activeNav="universities"` |
| `strAppersFrontend/src/pages/BootcampsTrainingCenters.jsx` | Marketing `/BootcampsTrainingCenters`: Solutions → bootcamps audience; hero + 4-card `#EDEEF3` features + dark CTA; same header/footer strip pattern as Features; `LandingMarketingHeader` `activeNav="bootcamps"` |
| `strAppersFrontend/src/pages/Features.jsx` | Marketing `/Features`: shared `LandingMarketingHeader` (`activeNav="features"`), hero, grid, mockup band, dark CTA, landing-style footer + `CookieConsentBar`; Privacy/Terms `returnTo=Features` |
| `strAppersFrontend/src/pages/Pricing.jsx` | Marketing `/Pricing`: `#EDEEF3` hero + equal-height plan cards, CTAs match header “Join a Squad” button; landing-style footer (Privacy / Terms / Cookie + LinkedIn + mail); `activeNav="pricing"`; `CookieConsentBar` |
| `strAppersFrontend/src/components/landing/LandingMarketingHeader.jsx` | Sticky marketing nav: desktop nav + mobile full-screen menu (hamburger); Solutions links → Bootcamps & Training Centers, Universities & Schools; Join a Squad matches teal pill style; `activeNav` `landing` / `features` / `pricing` / `bootcamps` / `universities` |
| `strAppersFrontend/public/features-board-mockup.png` | Features page board room UI screenshot |
| `strAppersFrontend/src/pages/LandingNew.jsx` | Marketing landing; header: Features / Pricing / Solutions (dropdown TBD), For Employers, Join the Pilot; `#features` / `#pricing` sections; footer: Privacy / Terms / Cookie, social + mail |
| `strAppersFrontend/src/components/landing/RegistrationModal.jsx` | Employer/Junior signup; optional `title` overrides dialog heading (e.g. Features demo: `Register for a Demo`) |
| `strAppersFrontend/src/components/landing/CookieConsentBar.jsx` | Landing cookie banner + settings modal + FAB; footer uses `openCookieSettings()` from `LandingNew.jsx` |
| `strAppersFrontend/src/components/SkillInDiamondLogo.jsx` | Shared Checkout hero diamond (`bb0d13df2` on media.base44.com); staff nav/headers + Checkout header/hero |
| `strAppersFrontend/src/pages/StaffDashboard.jsx` | Staff squad grid: `GET /api/Boards/use/staff-squads`, roles `GET /api/Roles/use`, GitHub stats `GET /api/Boards/use/github/stats` (sprint + role filters); CRM read-only modal → `GET /api/CRM/use/stakeholder?boardId=`; **Assessment Report** button → `fetchAssessmentReport` (board from Squad filter or single filtered squad); route **`/staffdashboard`**; institute users redirect to **`InstituteStaff`**; optional **`embedInInstituteShell`** hides top bar when nested in that shell |
| `strAppersFrontend/src/pages/InstituteStaff.jsx` | Institute staff home after login: navy sidebar + white content card; default embedded Staff Dashboard; **`?view=taskbuilder`** → Task Builder; **`?view=roles`** → **Roles configuration**; **`?view=metrics`** → Metrics; **`?view=projectdesigns`** → **Project Designs** (module notes + design assistant panel); uses **`localStorage.instituteId`** for template save; route **`/institutestaff`** |
| `strAppersFrontend/src/pages/RolesConfig.jsx` | Institute shell: **Roles configuration** — **`GET /api/Roles/use/all`**; defaults: all roles in Desired, Req off for Marketing/BizDev-style names only; UI/UX Designer required by default; FE/BE/Full Stack bundled developer coverage panel; Req column only on **Desired**; Add Role modal; seeded Available-only extras (QA Engineer, Data Analyst/Scientist); prop **`embedInInstituteShell`** |
| `strAppersFrontend/src/api/rolesStrAppers.js` | `fetchAllRoles` → **`GET /api/Roles/use/all`** |
| `strAppersFrontend/src/pages/ProjectDesigns.jsx` | Institute shell: **Project Designs** — left **`GET /api/Projects/use/available`** project list + **Available** checkbox (default on) + **Brief** expand + **+** opens module composer (Add module / upload / module blocks); center empty until **+**; right **Design assistant**; prop **`embedInInstituteShell`** |
| `strAppersFrontend/src/api/projectDesignsStrAppers.js` | `fetchAvailableProjects` → **`GET /api/Projects/use/available`** |
| `strAppersFrontend/src/pages/MetricsAssessment.jsx` | Institute shell: staff **Metrics configuration** table (Required, influence 1–5, category, phase); loads **`GET /api/Metrics/use/assessment-config`**; local edits preview-only; prop **`embedInInstituteShell`** |
| `strAppersFrontend/src/api/metricsStrAppers.js` | `fetchMetricsAssessmentConfig` → **`GET /api/Metrics/use/assessment-config`**; also `fetchAssessmentReport`, `fetchSquadNamesSearch` |
| `strAppersFrontend/src/components/metrics/AssessmentReportBody.jsx` | Shared printable assessment report HTML (Print / Save as PDF); used by Management Dashboard and Staff Dashboard modal |
| `strAppersFrontend/src/api/staffDashboardStrAppers.js` | `fetchStaffSquads`, `fetchActiveRoles`, `fetchBoardGitHubStats`, `fetchStaffBoardTrelloBundle`, `fetchCrmStakeholders` |
| `strAppersFrontend/src/components/staff/ClassroomCard.jsx` | Squad card actions: Live/API/GitHub/Trello/Figma/Resources/**CRM** (stakeholders modal) |
| `strAppersFrontend/src/components/staff/StaffStakeholdersReadOnlyModal.jsx` | Read-only CRM stakeholders table for staff (board-scoped; not driven by sprint/role filters) |
| `strAppersFrontend/src/pages/ManagementDashboard.jsx` | Management dashboard: assessment report filters (board / optional student & sprint), `fetchAssessmentReport` → `GET /api/Metrics/use/assessment-report`, printable preview window; route `/ManagementDashboard` |
| `strAppersFrontend/src/api/metricsStrAppers.js` | `fetchSquadNamesSearch` → `GET /api/Metrics/use/squad-names-search`; `fetchAssessmentReport` → `GET /api/Metrics/use/assessment-report` (`squadName` or `boardId`) |
| `strAppersFrontend/src/pages/ManagementDashboardMock.jsx` | Staff management dashboard **UI mockup** (classes/boards, stats, URLs, resources, assistance flags); static data only, route `/ManagementDashboardMock` |
| `strAppersFrontend/src/api/figmaStrAppers.js` | OAuth: `getFigmaOAuthStart`, `storeFigmaOAuthTokens`, `beginBoardFigmaOAuth`, `beginRegistrationFigmaOAuth`, `applyPendingFigmaToBoard`, `getFigmaConnection`; mentor UI → `postMentorFigmaFrameReview` (`POST /api/Mentor/use/figma-frame-review`) |
| `strAppersFrontend/src/api/resourceReviewStrAppers.js` | Mentor reviews: `postMentorResourceReview`, `postMentorStoryReview`, `postMentorCrmReview`; resources list + `pickResourceForMentorReview` |
| `strAppersFrontend/src/components/boards/ChatSidebar.jsx` | Mentor tab: role panels (dev GitHub/code + Resources; designer Figma + Resources; PM Trello story + Resources; Marketing/BizDev **CRM** + Resources) under “Review my work” |
| `strAppersFrontend/src/components/boards/FigmaLinksModal.jsx` | Figma links list + “Connect Figma account” (API) for designers when StrAppers backend is configured |
| `strAppersFrontend/src/pages/StudentRegistration.jsx` | **UI/UX Designer (role id 4):** optional “Connect Figma account” during registration; OAuth redirect `…/StudentRegistration` stores tokens by email until a board exists (`apply-pending` from BoardRoom) |
| `strAppersFrontend/src/pages/BoardRoom.jsx` | Figma OAuth redirect target (`…/BoardRoom`); exchanges `code`; calls `apply-pending` when `boardId` + email to merge registration-stored Figma tokens |
| `strAppersFrontend/src/pages/TaskBuilder.jsx` | Staff maintenance page for task templates; optional **Templates** column (checkbox enables institute template combo → **`GET /api/Projects/use/templates/list`** + **`GET /api/Projects/use/templates`** by id); default loads board JSON from **`GET /api/Projects/use/templates?projectId=`** only (project row); with **`instituteId`**, role combo loads **`GET /api/Roles/use/institute/{id}`** active (Desired) rows; otherwise **`GET /api/Roles/use`**; optional **`embedInInstituteShell`** when opened from **`InstituteStaff`**; Task assistant → `POST /api/Mentor/use/task-builder`; **Save to Template** → `POST /api/Projects/use/add-template` |
| `strAppersFrontend/src/api/taskBuilderStrAppers.js` | **`fetchInstituteTemplatesList`** → **`GET /api/Projects/use/templates/list`**; **`fetchProjectTemplate`** → **`GET /api/Projects/use/templates`** (optional `instituteId` + `instituteTemplateId`); `postAddInstituteTemplate` + **`postTaskBuilderMentor`** → `POST /api/Mentor/use/task-builder` |

### Preview ProjectsStudentV2 locally (before deploy)

1. **`strAppersFrontend/.env`** — set `VITE_STRAPPERS_BACKEND_URL` to your API base (e.g. dev IIS `http://localhost:9001` — no trailing slash). Copy from `.env.example` if needed. Restart Vite after changes.
2. From **`strAppersFrontend`**: `npm install` (once), then **`npm run dev`**.
3. Open **`http://localhost:5173/ProjectsStudentV2`** (Vite default port; check terminal if different).
4. You need a **student session**: the page reads `localStorage.userEmail` (set after your normal login / Google flow). Without it, load may fail or show errors.

---

*Add new rows below as the user points out paths — do not remove historical rows unless obsolete.*
