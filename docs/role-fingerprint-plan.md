# Role Fingerprint: Company-Calibrated Hiring via Claude Skills
## StrAppers Product Plan — May 2026

---

## Executive Summary

The system analyzes how exemplary employees work inside a company over a few sprints, synthesizes that into a structured **Claude Skill** (a JSON object that compiles to a system prompt), then uses that skill in three ways inside StrAppers: direct assessment injection, post-assessment scoring, and course shaping. No personal data leaves the pipeline. The output is a portable, inspectable "Role Fingerprint" — company-calibrated, not generic.

**The core insight:** Both students and employees are evaluated against the same four signal domains, using standardized assessment inputs regardless of which specific tools are in use. This "apples-to-apples" framework is what makes the comparison valid and the matching accurate.

---

## 1. The Apples-to-Apples Standardization Framework

This is the architectural foundation. The platform normalizes inputs from any combination of tools into the same four signal domains and the same assessment markdown format. The LLM sees identical structure on both sides — making student scores and employee baseline patterns directly comparable.

### The Four Signal Domains

| Domain | What it measures | Student side (Skill-in) | Employee side (tool-agnostic) |
|---|---|---|---|
| **Project Design** | Quality of design thinking, requirements, specs | Module cards, sprint role cards, PRDs, design documents | PRDs, architecture docs, specs — in whatever tool the org uses |
| **Tasks & User Stories** | Planning quality, ticket hygiene, story clarity, estimation | Trello cards, PM user stories, acceptance criteria | Jira, Linear, Asana, Trello, or equivalent |
| **Skilled Output** | Actual delivery artifacts — code, design, documents | GitHub commits/PRs, Figma files, uploaded resources | GitHub, design tools, deliverable files — equivalent per role |
| **Communication** | Async updates, proactive communication, context-sharing | Mentor chat, AI customer sessions, team channels | Slack, Microsoft Teams, email — whatever channel the org uses |

### The Standardized Assessment Format

Regardless of source tool, every assessment — both student and employee — is structured as:

```
## CONTEXT (what was expected)
[Normalized from the project design + task/story signal for this sprint/period]

## ARTIFACTS (what was delivered)
[Normalized from the skilled output + communication signal for this sprint/period]
```

The LLM prompt (`GapAnalysisSystem.txt`) already uses this format for students. The same format is used when analyzing employee patterns. The synthesis LLM then compares exemplary employee patterns to baseline — producing signal weights and competency indicators that are grounded in the same dimensional framework that students are assessed against.

### Tool Agnosticism

> **The specific tools are determined together with each design partner.** We do not dictate which tools to integrate. What we define upfront are the four signal domains. The partner then maps their existing tooling onto those domains. The platform builds an adapter for their specific tools during the design partnership.

This means:
- A company using Jira + GitHub + Slack maps identically onto the framework as one using Linear + GitLab + Teams
- A company with no design tool simply leaves that signal domain at low weight
- Custom signals (e.g., "we use a proprietary ticketing system") are accommodated via the rating map configuration

---

## 2. The Claude Skill Schema

The output is a structured JSON object — Option B (not a raw `.txt` prompt). It is both the technical artifact and the marketing artifact. Orgs can read it, understand it, and appreciate its value. It compiles to system prompts at runtime.

```json
{
  "schemaVersion": "1.0",
  "meta": {
    "roleName": "Senior Full-Stack Engineer",
    "roleTrack": "technical",
    "orgHash": "tc_a3f9",
    "generatedAt": "2026-05-09",
    "analysisWindow": "3 sprints / 6 weeks",
    "employeeSampleSize": "4–6 (anonymized)",
    "calibrationMethod": "exemplary-baseline"
  },

  "signalWeights": {
    "delivery":       { "weight": 5, "rationale": "Org prioritizes closing what is opened; incomplete sprints are the most frequent failure pattern observed" },
    "quality":        { "weight": 4, "rationale": "Code review depth and PRD alignment are strong predictors of exemplary performance" },
    "collaboration":  { "weight": 3, "rationale": "Cross-role handoff quality correlates with exemplary pattern; not the primary differentiator" },
    "communication":  { "weight": 4, "rationale": "Proactive async updates distinguish top performers" },
    "engagement":     { "weight": 2, "rationale": "Meeting participation signals are weak predictors for this role at this org" }
  },

  "competencies": [
    {
      "name": "Delivery consistency",
      "weight": 5,
      "positiveIndicators": [
        "Commits distributed across sprint window, not deadline-clustered",
        "PRs opened before sprint end in >80% of sprints",
        "Ticket status updated within 24h of state change"
      ],
      "negativeIndicators": [
        "Commit bursts only in final 48h of sprint",
        "More than 1 ticket carried forward per sprint on average",
        "PRs merged without peer review comments"
      ]
    },
    {
      "name": "Technical depth",
      "weight": 4,
      "positiveIndicators": [
        "PR descriptions reference acceptance criteria explicitly",
        "Code review comments left on at least 2 PRs per sprint",
        "Branch naming follows feature/module convention consistently"
      ],
      "negativeIndicators": [
        "Empty or one-line PR descriptions",
        "No evidence of self-review or testing notes",
        "Direct commits to main/integration branch"
      ]
    },
    {
      "name": "Proactive communication",
      "weight": 4,
      "positiveIndicators": [
        "Async updates posted before team check-ins",
        "Blockers surfaced in writing with a proposed path forward",
        "Customer or stakeholder context referenced in delivery notes"
      ],
      "negativeIndicators": [
        "Ticket updates only after manager/team prompts",
        "No written trace of blockers until sprint review"
      ]
    },
    {
      "name": "Documentation",
      "weight": 2,
      "positiveIndicators": [
        "PRD or technical spec updated within sprint window",
        "README or inline comments present in new modules"
      ],
      "negativeIndicators": [
        "No documentation commits across multiple sprints"
      ]
    }
  ],

  "matchingCriteria": {
    "minimumMatchScore": 65,
    "dealBreakers": [
      "No output artifacts across any sprint",
      "Zero async communication signals over 4+ weeks"
    ],
    "bonusSignals": [
      "Customer-facing communication with substantive exploration",
      "Evidence of unblocking teammates"
    ]
  },

  "assessmentSystemPrompt": "You are an expert evaluator for the role of Senior Full-Stack Engineer as defined by a specific technology organization. This organization's exemplary engineers are distinguished by delivery consistency (commits distributed across sprint, not deadline-clustered), proactive written communication (updates before prompts), and technical depth evidenced by meaningful PR descriptions and peer review participation. Weight delivery at 5/5, communication at 4/5, quality at 4/5, collaboration at 3/5. Evaluate against: Delivery Consistency, Technical Depth, Proactive Communication, Documentation. Score 0–100 per category grounded in artifact evidence.",

  "courseCompetencies": "Deliver working features iteratively — avoid deadline clustering. Write PR descriptions that reference acceptance criteria. Update ticket state within 24h of any change. Surface blockers in writing with a proposed resolution before check-ins. Leave or respond to code review comments each sprint. Reference customer or stakeholder context in delivery notes. Document new modules inline or in the project spec."
}
```

---

## 3. The Three Use Modes (Existing System Integration)

### Mode 1 — Direct Assessment Injection
The `assessmentSystemPrompt` field replaces `{{ROLE_DESCRIPTION}}` in `GapAnalysisSystem.txt`. One line change in `MetricsController.GapAnalysis.cs` — swap the role description source from `InstituteSquadRole.Description` to the published `OrgSkill.assessmentSystemPrompt`.

Student is now assessed not against a generic role description but against this company's actual performance fingerprint.

### Mode 2 — Post-Assessment Scoring
After standard `CacheMetrics` are computed, a matching endpoint takes the student's existing category scores and re-weights them using `signalWeights` and `competencies[].weight` from the `OrgSkill`. No re-analysis needed. Works immediately with existing student data.

This is the low-risk MVP path — works even before a full design partner org is live.

### Mode 3 — Course Shaping
The `courseCompetencies` field is injected directly into `InstituteRole.Competencies` in `CourseBoardBuilderService.BuildUserPrompt()` (line 580). Zero architecture change. Courses built on this role will train students toward patterns this specific company values.

---

## 4. Technical Architecture

### New Data Models

```
OrgProject
  Id, OrgHash (anonymized), InternalLabel
  RoleName, RoleTrack
  AnalysisStartDate, AnalysisEndDate
  SprintCount, EmployeeCount (aggregate only, never individual)
  Status: pending | analyzing | complete | approved
  OrgSkillId (FK → OrgSkill)

OrgEmployee  ← EPHEMERAL: deleted after synthesis
  Id (internal UUID), OrgProjectId
  RoleName
  IsExemplary (bool — marked by org admin)
  GitHubHandle (encrypted)
  ProjectToolId (Jira/Trello/Linear ID — encrypted)
  CommunicationHandle (Slack/Teams ID — encrypted)
  ConsentTimestamp
  — All PII fields deleted post-synthesis —

OrgSkill  ← PERMANENT: the output
  Id, OrgProjectId
  RoleName, RoleTrack
  SkillJson (the full Claude Skill JSON)
  GeneratedAt
  IsApproved (org admin approves before use)
  LinkedInstituteRoleId (optional FK → InstituteRole for course injection)
```

### New Services

**OrgAnalysisPipeline**
Mirrors the student assessment pipeline. For each `OrgEmployee`:
1. Pull data via tool adapters for the analysis window
2. Compute per-employee signal scores per domain (same 0–100 scale as `CacheMetrics`)
3. Separate exemplary (`IsExemplary=true`) from baseline employees
4. LLM synthesis: "Given these patterns from exemplary vs baseline, what signals distinguish great performance here?"
5. Delete all `OrgEmployee` PII fields
6. Pass synthesis output to `OrgSkillCompiler`

**OrgSkillCompiler**
Deterministic compilation step (not an LLM call). Takes LLM synthesis output and builds the structured `OrgSkill` JSON — signal weights, competency indicators, compiled `assessmentSystemPrompt`, compiled `courseCompetencies`.

**Integration Adapters** (adapter pattern — one per tool, all output normalized signal data)

| Adapter | Phase | Reuse existing? | Notes |
|---|---|---|---|
| GitHub | Phase 1 | Yes — `GitHubService` | Already used for student assessment |
| Trello | Phase 1 | Yes — `TrelloService` | Already used for student assessment |
| Jira | Phase 1 | New | Well-documented REST API |
| Slack | Phase 2 | New | Requires Slack app with read scopes |
| Microsoft Teams | Phase 2 | New | Graph API |
| Google Drive / Confluence | Phase 2 | New | Doc-level analysis |
| Email (Gmail / Outlook) | Phase 3 | New | Complex, highest friction |

> The specific adapters for each design partner are determined by their tool stack. We do not require any specific tool — we build adapters for whatever 2–3 tools the partner uses.

---

## 5. Privacy Architecture

### What is NEVER stored in output
- Employee names, emails, profile photos, any PII
- Individual commit messages (aggregate pattern signals only)
- Raw chat content (aggregate communication pattern scores only)
- Any identifier that could be reverse-engineered to a person

### What IS stored during analysis (ephemeral)
- Anonymized employee UUID (internal only, no link to real identity)
- Integration handles (GitHub username, Jira ID) — **encrypted at rest**
- Per-employee signal scores (0–100 per domain, per sprint)
- `IsExemplary` flag (set by org admin)
- Consent timestamp

**All PII fields on `OrgEmployee` are deleted after synthesis runs.**

### What IS stored permanently (the output)
- The `OrgSkill` JSON — pure behavioral patterns, zero individual identifiers
- Aggregate metadata: N employees analyzed, N sprints, date range

### Technical Privacy Guarantees
- **Minimum 3 exemplary employees per role** — platform refuses synthesis below this threshold. Not a policy, a hard code guard.
- **No individual scores in output** — all competency indicators are aggregate pattern descriptions, not per-person data
- **Ephemeral PII deletion** — `OrgEmployee` integration handles are deleted immediately after synthesis completes

### Employee Consent Flow
1. Org admin configures an analysis project
2. Platform generates a consent notice (templated) for the org to send
3. Each employee gets a unique consent link — platform records consent timestamp
4. Only consented employees are included in analysis
5. Employees see a transparency dashboard: "These signal categories are being analyzed. No personal data is stored in the output."
6. Employees can withdraw consent — their data is excluded and existing patterns recomputed

---

## 6. The Rating Map / Org Calibration

Two layers, shaped together with each design partner:

### Layer 1 — Exemplary employee marking
The org admin marks which employees are exemplary performers for this role. This is the training signal. The LLM synthesis separates their patterns from baseline.

### Layer 2 — Signal configuration
Mirrors the existing `MetricsAssessment` configuration UI (same UI pattern, new backend):
- **Adjust signal weights** per domain (e.g., "we don't use GitHub — weight delivery via Jira only")
- **Toggle signal domains on/off** based on available tool integrations
- **Add custom signals** with text descriptions (e.g., "Engineers who update the internal wiki show higher long-term value")
- **Mark deal-breakers** (signals that disqualify regardless of overall score)

---

## 7. Phased Roadmap

### Phase 0 — Design Partnership Prep (now, 2–3 weeks)
- Finalize `OrgSkill` JSON schema
- Build employer pitch
- Define minimum viable integration list (GitHub + Jira/Trello for Phase 1)
- Draft employee consent template
- Identify 1–2 design partner candidates

**Deliverable:** Pitch deck, skill schema, consent template

### Phase 1 — Single Design Partner MVP (6–8 weeks)
- `OrgProject` + `OrgEmployee` + `OrgSkill` models and migrations
- GitHub adapter (reuse) + Jira adapter (new) — or Trello if org uses it
- Org admin UI: create project, configure signals, mark exemplary employees
- LLM synthesis pipeline + `OrgSkillCompiler`
- **Mode 3 only** (course competencies injection) — lowest risk, immediate value
- Employee consent flow (basic version)
- Privacy: ephemeral PII storage, anonymized output, minimum sample size guard

**Deliverable:** One org, one role, one generated skill, injected into course builder

### Phase 2 — Expand + Matching (8–10 weeks)
- Slack / Teams adapters
- Mode 2 (post-assessment scoring matching dashboard)
- Multiple roles per org
- Org admin skill review and approval UI (inspect + edit JSON before publishing)
- Employee transparency dashboard
- GDPR-compliant data deletion flow

**Deliverable:** Full matching MVP, multiple orgs

### Phase 3 — Direct Assessment Injection (6 weeks)
- Mode 1: `assessmentSystemPrompt` injection into `GapAnalysisSystem.txt`
- Side-by-side: generic vs company-calibrated assessment scores
- Student-facing match percentage
- Org-facing candidate ranking dashboard

**Deliverable:** Full loop — org analysis → student assessment → ranked match list

---

## 8. What's Already Built vs. What's New

| Component | Status |
|---|---|
| Sprint-windowed LLM assessment | **Built** — `MetricsController.GapAnalysis.cs` |
| GitHub signal extraction | **Built** — `GitHubService` |
| Trello signal extraction | **Built** — `TrelloService` |
| Metrics configuration UI (signal weights) | **Built** — `MetricsAssessment.jsx` |
| Role + Competencies model | **Built** — `InstituteRole.Competencies` |
| Course competencies injection | **Built** — `CourseBoardBuilderService.cs:580` |
| `{{ROLE_DESCRIPTION}}` injection point | **Built** — `GapAnalysisSystem.txt` |
| Standardized assessment markdown format | **Built** — same CONTEXT/ARTIFACTS structure |
| `OrgProject` / `OrgEmployee` / `OrgSkill` models | **New** |
| Jira / Slack / Teams adapters | **New** |
| OrgAnalysisPipeline service | **New** |
| OrgSkillCompiler service | **New** |
| Employee consent flow | **New** |
| Org admin UI | **New** |
| Post-assessment matching engine (Mode 2) | **New** |
| Direct prompt injection (Mode 1) | **New — small, one swap** |

---

## 9. Challenges & Mitigations

| Challenge | Risk | Mitigation |
|---|---|---|
| Employment monitoring laws (EU especially) | High | Consent-first architecture, zero-PII output, legal review before EU pilots. Start with US/Israel partners where regulation is lighter. |
| Employee trust ("are you spying on us?") | High | Transparent consent flow, employee transparency dashboard, frame as "documenting what makes this role successful" not monitoring individuals. |
| IT/security approval for API access | Medium | OAuth scopes are read-only and minimal. Provide a security whitepaper. Start with GitHub (lowest friction) only for MVP. |
| Data quality (org marks wrong employees as exemplary) | Medium | The system reflects the org's own definition of good — that's by design. Flag statistical outliers. Minimum 3 employees prevents single-person bias. |
| Bias amplification | Medium | LLM synthesis instructions explicitly exclude demographic inference. Signal categories are purely behavioral/artifact-based. Org admin reviews output before approval. |
| Integration complexity varies per employer | Medium | Adapter pattern. Build adapters for whatever tools the design partner uses. Don't build all adapters upfront. |
| Minimum sample size (small teams) | Medium | Floor of 3. For small teams, allow role families with weighting. Communicate limitation clearly. |
| Skill drift (profile goes stale) | Low-Medium | Version `OrgSkill` with `generatedAt`. Recommend re-synthesis every 6 months or after major team changes. |
| LLM synthesis quality | Low | Synthesis grounded in 0–100 metric scores (not raw data). Human review: org admin reads and approves JSON before publishing. |

---

## 10. Employer Pitch Summary

**One sentence:** *"We watch how your best people actually work, distill it into an AI-native role profile with no personal data, then find students who match the way your team thinks — not the way their CV reads."*

**Three value propositions:**
1. **Hiring** — Company-calibrated candidate matching, not generic skills screening
2. **L&D** — Understand skill gaps across your existing team using the same framework
3. **The Role Fingerprint artifact** — A tangible, readable, shareable standard you own and reuse

**The design partnership ask:**
- Read-only API access to 2 existing tools (guided setup)
- Mark 3–5 exemplary employees per role
- 30 minutes to configure signal weights
- Send consent notice to involved employees
- 3 × 30-min check-ins over 6 weeks

**Timeline:** 6 weeks to first Role Fingerprint. 7+ weeks to first matched candidates.

---

*Last updated: 2026-05-09*
