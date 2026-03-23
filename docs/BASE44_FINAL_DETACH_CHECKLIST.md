# Base44 final detach — leftovers checklist

**Purpose:** Single source of truth for work to do **when you cut Base44 off entirely** (no more `@base44/sdk`, no `skill-in.com` function/analytics traffic).  

**Not for:** Day-to-day screen migration. While `VITE_STRAPPERS_BACKEND_URL` is set, the app is already **StrAppers-first**; this list is the **last mile**.

**Related:** `docs/BASE44_MIGRATION_PLAN.md` (overall plan), `docs/BASE44_FRONTEND_INVOKE_INDEX.md` (invoke names), `docs/AGENT_RESUME.md` (current sprint / handoff).

---

## Policy (recorded 2026-03-21)

- **User intent:** Detach Base44 **ASAP**; **avoid new development on Base44** (Deno functions, Base44-only features) until final detach.
- **Until then:** Continue migrating **other modules/pages** using the existing **dual-path** pattern (`*StrAppers.js` + `strAppersBackend.js`); Board Room is treated as **good enough** for StrAppers-first behavior.
- **This document** is the promised **checklist** so “leftovers” are not forgotten at cutover time.

---

## 1. SDK and client removal

- [ ] Remove **`@base44/sdk`** from `package.json` and any **`@base44/vite-plugin`** (or equivalent) from `vite.config.js`.
- [ ] Delete or replace **`src/api/base44Client.js`** (`createClient`, wrapped `invoke`). Today it provides **StrAppers intercepts** + **Base44 fallback** — at detach, keep only what you need (e.g. a thin **`strAppersHttp.js`** without Base44).
- [ ] Remove **`src/lib/app-params.js`** (or equivalent) if it exists **only** for Base44 app id / server URL; replace with StrAppers-only env (`VITE_STRAPPERS_BACKEND_URL`, auth later).
- [ ] Grep **`base44`**, **`createClient`**, **`functions.invoke`** under `strAppersFrontend/src` and eliminate remaining call sites (see §4).

**Outcome:** No bundle load of Base44 → **analytics batch** (`…/analytics/track/batch` on `skill-in.com`) stops.

---

## 2. API modules: drop Base44 fallbacks

Every file matching **`src/api/*StrAppers.js`** (and similar) currently does: *if StrAppers configured → HTTP; else → `base44.functions.invoke`*.

- [ ] For each module, **remove the `else` branch** and require `VITE_STRAPPERS_BACKEND_URL` (or fail fast with a clear error in dev).
- [ ] Confirm list against **`docs/BASE44_FRONTEND_INVOKE_INDEX.md`** and **`base44Client.js`** intercept table so nothing still expects `invoke`.

**Typical files (non-exhaustive — re-grep before cutover):**

- `boardRoomStrAppers.js`, `boardEntityStrAppers.js`, `boardCollabStrAppers.js`
- `dashboardStrAppers.js`, `boardMediaStrAppers.js`, `boardChatStrAppers.js`
- `mentorChatStrAppers.js`, `mentorGitStrAppers.js`
- `crmStrAppers.js`, `supportStrAppers.js`

---

## 3. Components / pages: direct `base44` imports

Some UI still imports **`base44`** directly instead of `*StrAppers` helpers. With intercepts, StrAppers may still work, but **detach requires** these to use **HTTP-only** modules.

- [ ] Grep: `from '@/api/base44Client'` and `base44.functions.invoke` under **`src/pages`**, **`src/components`**, **`src/hooks`**.
- [ ] **`CRMDashboardModal.jsx`** — refactor to **`crmStrAppers.js`** (`invokeGetCRMMetadata`, `invokeGetStakeholders`, `invokeDeleteStakeholder`, etc.) instead of raw `invoke`.
- [ ] Re-run **`docs/FRONTEND_SRC_FILE_INDEX.md`** / index script if IDE search is incomplete.

---

## 4. Legacy `@/functions/*` (Base44 compatibility layer)

- [ ] If **`BASE44_LEGACY_SDK_IMPORTS`** or **`@/functions/*`** exists only for Base44-era loaders, either migrate callers to **`boardEntityStrAppers`** (or equivalent) or remove.
- [ ] **`boardEntityStrAppers.js`** fallbacks today call **`@/functions/getStudentByEmail`** etc. — replace with StrAppers-only or remove when Base44 is gone.

---

## 5. Static assets & CDN (not API, but “Base44 branding”)

- [ ] Replace **Supabase `base44-prod`** URLs (e.g. BoardRoom footer images, icons) with your **Azure / CDN / `public/`** assets.
- [ ] Replace **`media.base44.com/.../6921e483...`** (e.g. Support modal logo) with hosted copies.

---

## 6. Backend / infra (outside frontend repo)

- [ ] Stop routing production traffic through Base44 Deno functions when frontend no longer calls them.
- [ ] **CORS:** final SPA origin(s) allowed on StrAppersBackend.
- [ ] **Secrets:** rotate or retire Base44-only tokens if any server still referenced them.

---

## 7. Optional product follow-ups

- [ ] **Analytics:** If you still want usage metrics post-Base44, add **your** tool (App Insights, Plausible, PostHog, …).
- [ ] **Auth:** Per migration plan, Base44 auth replacement is a **later phase** — track separately unless you combine with this cutover.

---

## 8. Verification before “done”

- [ ] Production build with **only** `VITE_STRAPPERS_BACKEND_URL` (no Base44 app env required for data).
- [ ] Smoke: Board Room, CRM, Support, bug log, chat, mentor/git flows you care about.
- [ ] Network: **no** requests to Base44 function hosts / `skill-in.com` **analytics** (unless you intentionally keep something).

---

## 9. Quick grep commands (run from `strAppersFrontend`)

```bash
# Direct invokes in app code (should be empty after detach)
rg "base44\.functions\.invoke" src --glob "*.jsx" --glob "*.js"

# base44 client imports outside api/base44Client.js (should be empty or intentional)
rg "from ['\"]@/api/base44Client['\"]" src
```

---

*Last updated: 2026-03-21 — checklist created for post–Board Room migration break.*
