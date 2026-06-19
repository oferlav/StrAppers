# ATS & Employer Portal Integration Plan — Skill-in

## 1. Scope

A unified employer portal that supports job posting and candidate assessment across four modes — two Greenhouse-connected modes and two standalone modes. All data models use neutral names (no `Ats` prefix) so they work equally for ATS and non-ATS employers.

---

## 2. Table Naming

All three new tables share the `Ats` prefix to distinguish them from the existing `Employer*` tables, even though they also cover non-ATS (manual) usage. The `Provider` column distinguishes the source.

| Table | What it stores |
|-------|---------------|
| `AtsConnections` | Per-employer external connection config (ATS credentials, or `Provider='manual'` for no ATS) |
| `AtsJobPostings` | Job postings — from any source: ATS sync, or employer-entered manually |
| `AtsAssessmentInstances` | One row per candidate per job — created by ATS webhook OR employer clicking "Invite" OR self-apply |

---

## 3. The Four Modes

### Mode 1 — `greenhouse-push` (Phase 1)
Greenhouse initiates everything. Recruiter sends test from Greenhouse → we receive candidate → candidate registers on Skill-in → assessment → results pushed back to Greenhouse.

### Mode 2 — `greenhouse-pull` (Phase 2)
Employer syncs job postings from Greenhouse (Job Board API). Job is listed on Skill-in. **Already-registered Skill-in users** browse listings and self-apply — no coupon. Employer sees results on Skill-in portal. Optional: results pushed to Greenhouse via Assessment Partner API if employer has that stage configured.

### Mode 3 — `manual-token` (Phase 1)
No ATS. Employer fills job form manually on Skill-in. Employer invites specific candidates by name + email → system generates coupon → sends invitation email. Same token/registration flow as `greenhouse-push` but we initiate instead of Greenhouse.

### Mode 4 — `open` (Phase 2)
No ATS. Employer fills job form. Job is publicly listed on Skill-in. Any registered user can browse and apply. No coupon needed. Employer dashboard shows all applicants and scores.

---

## 4. How the Employer Portal Form Works

**One form, not separate forms per mode.** The connection type (set in Connection Settings) determines how sections render.

### 4.1 Connection Settings (separate page, set once per employer)

| Field | Shown when |
|-------|-----------|
| ATS Provider (Greenhouse / None) | Always |
| Mode (push / pull / manual-token / open) | Always |
| Board Token | Provider = Greenhouse |
| Job Board API Key | Provider = Greenhouse |
| Assessment API Key | Provider = Greenhouse (push mode) |

### 4.2 Job Posting Form

```
┌─────────────────────────────────────────────────────────────────┐
│  ATS Integration  [Toggle: ON / OFF]                             │
│  ┌── ATS Settings (visible when ON) ───────────────────────────┐│
│  │  Board Token *    [__________]                               ││
│  │  Job              [Dropdown: fetch from Greenhouse ▼]        ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                  │
│  ── Job Details ─────────────────────────────────────────────── │
│  Title *          [__________]   locked when ATS ON             │
│  Location         [__________]   locked when ATS ON             │
│  Department       [__________]   locked when ATS ON             │
│  Employment Type  [Dropdown ▼]   locked when ATS ON             │
│  Description      [Rich Text]    locked when ATS ON             │
│                                                                  │
│  ── Visibility ──────────────────────────────────────────────── │
│  [ ] List publicly on Skill-in   (enables Pull / Open modes)    │
│                                                                  │
│  ── Skill-in Custom Fields (always editable) ─────────────────  │
│  Challenge        [Text Area]                                    │
│  Resource         [Text Field]  [🔗 Connect GitHub Repo]        │
│  Expectations     [Text Area]                                    │
│  Q&A              [Text Area]                                    │
│                                                                  │
│                          [ Save Draft ]  [ Submit Job ]          │
└─────────────────────────────────────────────────────────────────┘
```

**Field locking rules:**

| Field | ATS ON (push/pull) | ATS OFF (manual) |
|-------|--------------------|------------------|
| Title, Location, Department, Type, Description | Locked — from ATS | Free text |
| Challenge, Resource, Expectations, Q&A | Always editable | Always editable |
| Board Token, External Job ID | Shown | Hidden |
| "List publicly" toggle | Shown (enables pull/open) | Shown |

### 4.3 Candidates Section (below the job form, after job is saved)

#### Mode: `greenhouse-push`
Read-only table — rows appear automatically when Greenhouse calls `/send-test`.
No "Invite" button — Greenhouse is the initiator.

```
Candidates invited by Greenhouse:
┌──────────────┬────────────────────┬─────────────┬───────┬──────────────┐
│ Name         │ Email              │ Status      │ Score │ Actions      │
├──────────────┼────────────────────┼─────────────┼───────┼──────────────┤
│ John Doe     │ john@example.com   │ complete    │ 87    │ View Results │
│ Jane Smith   │ jane@example.com   │ started     │ —     │ —            │
│ Mike Brown   │ mike@example.com   │ not_started │ —     │ Resend Email │
└──────────────┴────────────────────┴─────────────┴───────┴──────────────┘
```

#### Mode: `manual-token`
"Invite Candidate" button → modal: first name, last name, email → generates coupon → sends email.
Same table as above but employer controls who gets added.

```
[ + Invite Candidate ]

┌──────────────┬────────────────────┬─────────────┬───────┬──────────────┐
│ Name         │ Email              │ Status      │ Score │ Actions      │
├──────────────┼────────────────────┼─────────────┼───────┼──────────────┤
│ John Doe     │ john@example.com   │ complete    │ 91    │ View Results │
│ Jane Smith   │ jane@example.com   │ not_started │ —     │ Resend Email │
└──────────────┴────────────────────┴─────────────┴───────┴──────────────┘
```

#### Mode: `greenhouse-pull` or `open`
No invitation flow — users self-apply. Table shows everyone who applied.
Employer can see applicants and filter by score.

```
Applicants (self-registered):
┌──────────────┬────────────────────┬──────────┬───────┬──────────────┐
│ Name         │ Email              │ Applied  │ Score │ Actions      │
├──────────────┼────────────────────┼──────────┼───────┼──────────────┤
│ Alice Chen   │ alice@example.com  │ 2026-06  │ 93    │ View Results │
│ Bob Nir      │ bob@example.com    │ 2026-06  │ 78    │ View Results │
└──────────────┴────────────────────┴──────────┴───────┴──────────────┘
```

---

## 5. Coupon / Token Flow Per Mode

### `greenhouse-push` (ATS initiates)
1. Greenhouse calls `/send-test` with candidate email
2. We generate a GUID token → save `AtsAssessmentInstances` row
3. Send invitation email: `skill-in.com/register?coupon={token}`
4. Candidate registers → `Students.Coupon = token`, `Students.InstituteId = EmployerId`
5. `AtsAssessmentInstances.StudentId` backfilled, `Status = started`
6. Assessment → `CacheMetrics` → `CompleteAssessmentAsync(token, score, url)` → PATCH Greenhouse

### `manual-token` (employer initiates)
1. Employer clicks "Invite Candidate" → enters name + email
2. We generate a GUID token → save `AtsAssessmentInstances` row (Provider='manual', CallbackUrl=null)
3. Send invitation email: `skill-in.com/register?coupon={token}`
4. Same registration flow as above
5. Assessment → `CacheMetrics` → we update `AtsAssessmentInstances` (no Greenhouse notification)

### `greenhouse-pull` or `open` (user self-applies)
1. Registered user browses job listings → clicks "Apply" / "Take Challenge"
2. We create a `AtsAssessmentInstances` row on the spot with `StudentId` already set, `Status='started'`
3. **No coupon, no email** — user is already registered
4. Assessment → `CacheMetrics` → update `AtsAssessmentInstances` (Status=complete, Score)
5. For `greenhouse-pull`: optionally push to Greenhouse if employer has Assessment Partner stage configured

---

## 6. DB Schema

See `Greenhouse_DB_Migration.sql` for the full PostgreSQL script.

### `AtsConnections`
| Column | Type | Notes |
|--------|------|-------|
| Id | SERIAL PK | |
| EmployerId | INT FK → Employers | |
| Provider | VARCHAR(50) | 'greenhouse' \| 'manual' |
| Mode | VARCHAR(50) | 'greenhouse-push' \| 'greenhouse-pull' \| 'manual-token' \| 'open' |
| ConnectionConfigJson | TEXT | Encrypted JSON: boardToken, apiKey, etc. |
| IsActive | BOOLEAN | |

### `AtsJobPostings`
| Column | Type | Notes |
|--------|------|-------|
| Id | SERIAL PK | |
| EmployerId | INT FK | |
| AtsConnectionId | INT FK (nullable) | null = no ATS |
| Provider | VARCHAR(50) | 'greenhouse' \| 'manual' |
| ExternalJobId | VARCHAR(100) | ATS job ID; null = manual |
| IsAtsSynced | BOOLEAN | true = populated from ATS |
| IsPublic | BOOLEAN | true = listed for self-apply (pull/open modes) |
| Title, Location, Department, EmploymentType, Description | standard fields | |
| Challenge, Resource, ResourceGithubUrl, Expectations, QA | custom fields | always editable |
| RawMetadataJson | TEXT | |

### `AtsAssessmentInstances`
| Column | Type | Notes |
|--------|------|-------|
| Id | SERIAL PK | |
| JobPostingId | INT FK (nullable) | |
| Provider | VARCHAR(50) | 'greenhouse' \| 'manual' |
| ExternalInterviewId | VARCHAR(100) UNIQUE | **= coupon token** (also used as Greenhouse partner_interview_id) |
| ExternalTestId | VARCHAR(100) | assessment type slug |
| CandidateEmail | VARCHAR(255) | from ATS payload or employer entry |
| CandidateFirstName, CandidateLastName | VARCHAR(100) | |
| ExternalProfileUrl | VARCHAR(1000) | ATS candidate profile URL |
| CallbackUrl | VARCHAR(1000) | Greenhouse PATCH URL; null in non-ATS modes |
| Status | VARCHAR(50) | not_started \| started \| complete |
| Score | DECIMAL(10,2) | from CacheMetrics |
| ProfileUrl | VARCHAR(1000) | our results page; sent to ATS |
| StudentId | INT (nullable) | backfilled on registration; set immediately for self-apply |
| InvitationSentAt | TIMESTAMPTZ | null for self-apply (no email sent) |
| SentBy | VARCHAR(255) | recruiter email or employer user |

---

## 7. Greenhouse Assessment Partner — How the "Skill-in Test" Option Appears

For the Greenhouse push mode to work, the employer's Greenhouse account must show Skill-in in their assessment provider dropdown. This requires two things:

**Our side (one-time, platform-level):**
- Email `partners@greenhouse.io` with our four endpoint URLs + a sample API key
- Greenhouse approves us as an Assessment Partner
- We appear globally in all Greenhouse customers' assessment provider lists

**Employer side (per employer, after we're a partner):**
- In Greenhouse: go to a Job → Interview Plan → Add Stage → "Take Home Test"
- Select **"Skill-in"** from the provider dropdown
- Choose which assessment (maps to our `/list-tests` response)
- From that point, any candidate who reaches that stage gets a "Send Test" button

See `Greenhouse_Employer_Instructions.md` for the step-by-step employer guide.

---

## 8. Backend API Endpoints

### Greenhouse Assessment Partner (Greenhouse calls us)
| Method | Route | Auth |
|--------|-------|------|
| GET | `/api/greenhouse/assessment/list-tests` | Basic Auth |
| POST | `/api/greenhouse/assessment/send-test` | Basic Auth |
| GET | `/api/greenhouse/assessment/test-status` | Basic Auth |
| POST | `/api/greenhouse/assessment/response-error` | Basic Auth |

### Job Board Proxy (we call Greenhouse)
| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/api/jobs/greenhouse?boardToken=...` | List published GH jobs |
| GET | `/api/jobs/greenhouse/{jobId}?boardToken=...` | GH job detail + custom fields |

### Employer Portal
| Method | Route | Purpose |
|--------|-------|---------|
| POST/PUT | `/api/employer/connection` | Save/update EmployerConnection |
| POST | `/api/employer/job-postings` | Create job posting |
| PUT | `/api/employer/job-postings/{id}` | Update job posting |
| GET | `/api/employer/job-postings` | List postings |
| GET | `/api/employer/job-postings/{id}/candidates` | List AtsAssessmentInstances for a job |
| POST | `/api/employer/job-postings/{id}/candidates` | Invite a candidate (manual-token mode) |
| POST | `/api/employer/job-postings/{id}/apply` | Self-apply (pull/open mode; student must be logged in) |

---

## 9. Configuration

```json
"Greenhouse": {
  "AssessmentApiKey": "your-generated-key-here"
}
```

`AssessmentApiKey` is generated by us, given to Greenhouse during partner onboarding. Per-employer credentials (board token, job board API key) live in `AtsConnections.ConnectionConfigJson`, encrypted before storage.

---

## 10. Implementation Phases

### Phase 1
1. Run `Greenhouse_DB_Migration.sql` (tables: `AtsConnections`, `AtsJobPostings`, `AtsAssessmentInstances`)
2. Deploy Greenhouse Assessment endpoints (needs live URL before emailing Greenhouse)
3. Job Board proxy endpoints
4. Employer portal: Connection Settings + Job Posting form + Candidates dashboard
5. `greenhouse-push` and `manual-token` candidate flows
6. Wire student registration coupon → `AtsAssessmentInstances` lookup + `LinkStudentAsync`
7. Wire `CacheMetrics` completion → `CompleteAssessmentAsync(coupon, score, profileUrl)`
8. Invitation email on `send-test` and manual invite

### Phase 2
- `greenhouse-pull` mode: Job Board API sync + self-apply flow
- `open` mode: public job listings + self-apply
- Greenhouse partner agreement (formal, for Assessment Partner directory listing)
- Harvest API OAuth (full Greenhouse Pull with write-back)

---

## 11. Open Questions

- Is Q&A a plain text blob or structured `[{ question, answer }]`?
- What is the score formula from `CacheMetrics`? (Needed for Phase 1 wire-up)
- Should invitation emails be sent immediately or queued?
- Table prefix confirmed as `Ats*` (`AtsConnections`, `AtsJobPostings`, `AtsAssessmentInstances`)
- GitHub OAuth app: Skill-in org app or new app?
