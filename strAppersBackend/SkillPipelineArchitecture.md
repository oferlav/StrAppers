# Skill Pipeline Architecture
## StrAppers — Data-Driven Job Matching via Structured Skills

---

## Problem with the Current State

| Today | Goal |
|---|---|
| `InstituteRole.Competencies` = free-text keyword list | Structured semantic rubric per role |
| Course cards reference generic technologies | Cards reference exact proficiency levels and sprint behaviors |
| Job matching = keyword overlap (cold) | Job matching = evidence vs. rubric (data-driven) |
| `EmployerCandidate` = binary flag | Ranked list with scored evidence and explanation |

---

## Core Architecture Principle — The Decoupling

**The course builder does not care where a skill comes from.**

It receives a resolved `CourseSkillContext` DTO at build time. That DTO is sourced from one of three places in priority order:

```
1. EmployerSkill    — when an employer is linked to the template (future)
2. InstituteRoleSkill — when a skill has been generated for the role (Phase 2+)
3. Raw Competencies — fallback, current behavior, always available
```

This means:
- Near-term: institutes generate `InstituteRoleSkill` from their role definitions → courses improve immediately
- Long-term: employers link their `EmployerSkill` to a template → course is shaped by real job requirements
- **No rearchitecting the course builder when the source changes** — only the resolution logic changes

---

## The Two Skill Layers

### First-Level Skill — `InstituteRoleSkill`
**Source (near-term):** Institute admin triggers generation. AI processes the role definition.
**Source (long-term):** Can be superseded by `EmployerSkill` when one is linked to the template.
**What it is:** A structured semantic rubric — technical domains, sprint behaviors, collaboration patterns, expected deliverables — plus two extracted fields used directly by other services.
**When it is used:**
1. **Course building** — `coursePromptContext` replaces raw `Competencies` in `BuildUserPrompt()`. Falls back to raw competencies if no skill generated.
2. **Candidate scoring baseline** — `evaluationPrompt` is the rubric passed to the scoring AI alongside student evidence.

### Second-Level Skill — `CandidateSkillScore`
**Source:** System-generated after course completion (or on-demand endpoint).
**What it is:** A scored evidence profile. AI evaluates the student's actual sprint output against the role skill rubric. Domain-level scores + evidence explanation.
**When it is used:**
1. **Employer matching** — compared against `EmployerSkill` to produce ranked score + explanation.
2. **Institute analytics** — cohort-level skill coverage and gap analysis.

### Employer Skill — `EmployerSkill` *(Phase 5)*
**Source:** Employer submits job ad text. AI generates using identical schema to `InstituteRoleSkill`.
**When linked to a template:** its `coursePromptContext` takes priority over `InstituteRoleSkill` in the resolver.
**When used for matching:** its `evaluationPrompt` scores all `CandidateSkillScore` records → ranked candidate list.

---

## Shared Skill JSON Schema

Both `InstituteRoleSkill` and `EmployerSkill` use this structure. The matching engine works because both sides speak the same language.

```json
{
  "roleName": "Backend Developer",
  "track": "Technical",
  "technicalDomains": [
    {
      "domain": "API Design & Implementation",
      "proficiency": "Builds REST APIs independently — auth, validation, error handling, response contracts",
      "sprintEvidence": [
        "POST/GET/PUT endpoints implemented per User Story spec",
        "Error codes documented and handled by frontend",
        "API contract reviewed with FE before sprint end"
      ]
    }
  ],
  "behavioralCompetencies": [
    "Commits working code incrementally — no sprint-end dumps",
    "Creates descriptive PRs referencing the User Story",
    "Flags blockers in NSM before they delay the sprint"
  ],
  "collaborationPatterns": [
    "Syncs with Frontend on API contracts before build sprint begins",
    "Clarifies data spec with PM once User Story is drafted"
  ],
  "deliverableTypes": [
    "Database migration files",
    "REST API endpoints (CRUD)",
    "Unit + integration tests",
    "Pull Requests merged to main via Mentor Panel"
  ],
  "coursePromptContext": "AI-synthesized paragraph — injected into course builder instead of raw Competencies",
  "evaluationPrompt": "Prompt used by scoring AI to evaluate candidate evidence against this skill"
}
```

---

## The Resolution Logic (course builder decoupling)

New private method in `CourseBoardBuilderService`:

```csharp
private async Task<CourseSkillContext> ResolveSkillAsync(
    InstituteRole role,
    InstituteTemplate template)
{
    // Priority 1: EmployerSkill linked to this template (Phase 5+)
    if (template.EmployerSkillId is > 0)
    {
        var empSkill = await _context.EmployerSkills
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == template.EmployerSkillId && s.IsActive);
        if (empSkill != null)
            return new CourseSkillContext(
                empSkill.CoursePromptContext,
                empSkill.EvaluationPrompt,
                SkillSource.Employer);
    }

    // Priority 2: InstituteRoleSkill generated for this role (Phase 2+)
    var roleSkill = await _context.InstituteRoleSkills
        .AsNoTracking()
        .FirstOrDefaultAsync(s => s.InstituteRoleId == role.Id && s.IsActive);
    if (roleSkill != null)
        return new CourseSkillContext(
            roleSkill.CoursePromptContext,
            roleSkill.EvaluationPrompt,
            SkillSource.InstituteRole);

    // Priority 3: Raw competencies (current behavior — always available)
    return new CourseSkillContext(
        role.Competencies,
        evaluationPrompt: null,
        SkillSource.RawCompetencies);
}

private record CourseSkillContext(
    string? CoursePromptContext,
    string? EvaluationPrompt,
    SkillSource Source);

private enum SkillSource { RawCompetencies, InstituteRole, Employer }
```

`BuildUserPrompt()` then uses `resolvedSkill.CoursePromptContext` instead of `role.Competencies` directly. The rest of the builder is untouched.

---

## Data Model Changes

### Phase 2 — InstituteRoleSkill

**New table: `InstituteRoleSkills`**

```sql
CREATE TABLE "InstituteRoleSkills" (
    "Id"                     SERIAL PRIMARY KEY,
    "InstituteRoleId"        INTEGER NOT NULL REFERENCES "InstituteRoles"("Id"),
    "GeneratedAt"            TIMESTAMP NOT NULL DEFAULT NOW(),
    "GeneratorPromptVersion" VARCHAR(50) NOT NULL,
    "SkillJson"              TEXT NOT NULL,
    "CoursePromptContext"    TEXT NOT NULL,
    "EvaluationPrompt"       TEXT NOT NULL,
    "IsActive"               BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE INDEX "IX_InstituteRoleSkills_RoleId_Active"
    ON "InstituteRoleSkills"("InstituteRoleId", "IsActive");
```

**C# model:** `Models/InstituteRoleSkill.cs`

### Phase 3 — Wire into course builder

**Changes to existing files:**
- `Services/CourseBoardBuilderService.cs`: add `ResolveSkillAsync`, update `BuildUserPrompt` to use resolved context
- No prompt file changes needed — `coursePromptContext` is already a synthesized paragraph ready to inject

### Phase 4 — CandidateSkillScore

**New table: `CandidateSkillScores`**

```sql
CREATE TABLE "CandidateSkillScores" (
    "Id"                   SERIAL PRIMARY KEY,
    "StudentId"            INTEGER NOT NULL REFERENCES "Students"("Id"),
    "InstituteRoleSkillId" INTEGER NOT NULL REFERENCES "InstituteRoleSkills"("Id"),
    "Score"                DECIMAL(4,3) NOT NULL,
    "EvidenceJson"         TEXT NOT NULL,
    "ScoredAt"             TIMESTAMP NOT NULL DEFAULT NOW(),
    "ModelVersion"         VARCHAR(100) NOT NULL
);

CREATE INDEX "IX_CandidateSkillScores_StudentId"
    ON "CandidateSkillScores"("StudentId");
```

**C# model:** `Models/CandidateSkillScore.cs`

**New endpoint:** `POST /api/Students/{studentId}/score-skill`

**New prompt:** `Prompts/Skills/CandidateSkillScorer.txt`

### Phase 5 — EmployerSkill

**New table: `EmployerSkills`**

```sql
CREATE TABLE "EmployerSkills" (
    "Id"                     SERIAL PRIMARY KEY,
    "EmployerId"             INTEGER NOT NULL REFERENCES "Employers"("Id"),
    "RawJobAd"               TEXT NOT NULL,
    "GeneratedAt"            TIMESTAMP NOT NULL DEFAULT NOW(),
    "GeneratorPromptVersion" VARCHAR(50) NOT NULL,
    "SkillJson"              TEXT NOT NULL,
    "CoursePromptContext"    TEXT NOT NULL,
    "EvaluationPrompt"       TEXT NOT NULL,
    "IsActive"               BOOLEAN NOT NULL DEFAULT TRUE
);
```

**Template linkage** — add column to `InstituteTemplates`:

```sql
ALTER TABLE "InstituteTemplates"
    ADD COLUMN "EmployerSkillId" INTEGER REFERENCES "EmployerSkills"("Id");
```

This is the hook that activates Priority 1 in `ResolveSkillAsync`.

**C# model:** `Models/EmployerSkill.cs`

**New endpoint:** `POST /api/Employers/{employerId}/build-skill`

### Phase 6 — Matching

**New table: `EmployerCandidateScores`** (replaces binary `EmployerCandidates`)

```sql
CREATE TABLE "EmployerCandidateScores" (
    "Id"              SERIAL PRIMARY KEY,
    "EmployerSkillId" INTEGER NOT NULL REFERENCES "EmployerSkills"("Id"),
    "StudentId"       INTEGER NOT NULL REFERENCES "Students"("Id"),
    "Score"           DECIMAL(4,3) NOT NULL,
    "EvidenceJson"    TEXT NOT NULL,
    "ScoredAt"        TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE ("EmployerSkillId", "StudentId")
);
```

**New endpoint:** `POST /api/Employers/{employerId}/match-candidates`

---

## New Files Summary

| File | Phase | Purpose |
|---|---|---|
| `Prompts/Skills/InstituteRoleSkillBuilder.txt` | 2 | System prompt for skill generation from role definition |
| `Prompts/Skills/EmployerSkillBuilder.txt` | 5 | System prompt for skill generation from job ad |
| `Prompts/Skills/CandidateSkillScorer.txt` | 4 | System prompt for scoring student output vs. skill rubric |
| `Models/InstituteRoleSkill.cs` | 2 | EF model |
| `Models/CandidateSkillScore.cs` | 4 | EF model |
| `Models/EmployerSkill.cs` | 5 | EF model |
| `Models/SkillModels.cs` | 2 | DTOs: skill generation request/response, `CourseSkillContext` |
| `Scripts/AddInstituteRoleSkills_PostgreSQL.sql` | 2 | DB migration script |
| `Scripts/AddCandidateSkillScores_PostgreSQL.sql` | 4 | DB migration script |
| `Scripts/AddEmployerSkills_PostgreSQL.sql` | 5 | DB migration script |

---

## Existing Files Changed

| File | Phase | Change |
|---|---|---|
| `Services/CourseBoardBuilderService.cs` | 3 | Add `ResolveSkillAsync`, update `BuildUserPrompt` |
| `Data/ApplicationDbContext.cs` | 2,4,5 | Add DbSets for new models |
| `Models/InstituteTemplate.cs` | 5 | Add `EmployerSkillId` nullable FK |

---

## Phased Roadmap

| Phase | Deliverable | Unblocked by | Value |
|---|---|---|---|
| 1 | Lock skill JSON schema (this doc) | — | Alignment |
| 2 | `InstituteRoleSkillBuilder` prompt + generator endpoint | Phase 1 | Skills exist |
| 3 | Wire `ResolveSkillAsync` into course builder | Phase 2 | Better courses immediately |
| 4 | `CandidateSkillScore` post-course scoring | Phase 2 | Candidate-side data |
| 5 | `EmployerSkillBuilder` prompt + generator endpoint | Phase 1 | Employer-side data |
| 6 | Matching engine | Phases 4 + 5 | Full pipeline live |

Phases 2–3 are self-contained and can be shipped without any employer or candidate scoring work.
Phase 5 can run in parallel with Phase 4.
Phase 6 requires both sides populated.

---

## Key Design Decisions

**Decoupling:** The course builder resolves a `CourseSkillContext` at build time. Source priority: `EmployerSkill` (if template-linked) → `InstituteRoleSkill` (if generated) → raw `Competencies`. Swapping sources requires only a change to `ResolveSkillAsync`, not to the builder or prompt.

**Versioning:** Skills are versioned via `GeneratedAt`, `GeneratorPromptVersion`, and `IsActive`. Regenerating a skill deactivates the old one (set `IsActive = false`), preserves history. `CandidateSkillScore` records `InstituteRoleSkillId` — scores from different versions are not comparable.

**Shared schema:** `InstituteRoleSkill` and `EmployerSkill` use identical JSON. The matching engine works because both sides speak the same language. This is the core bet of the architecture.

**Platform-awareness:** Skills are generated with StrAppers platform context baked in (sprint cadence, NSM, Mentor Panel, GitHub branching `N-B`/`N-F`). The `sprintEvidence` indicators reference what the platform actually captures — making scoring grounded and explainable.

**No EF migrations:** Follow existing project convention — provide SQL scripts in `Scripts/` folder, not `dotnet ef` migrations.
