# Course Board Builder — Variability Extension Plan

## Goal
Make the builder fully parameterizable: variable sprint count, module count, sprint length, module length, and any role set. Currently hardcoded to 8 sprints / 5 modules / single sprint length.

---

## Phase 1 — Variable Sprint Count & Module Count (do together, small lift)

### What changes
- **Request model** (`CourseBoardBuildModels.cs`): add `SprintCount` (default 8, range 4–16) and `ModuleCount` (default 5, range 2–8).
- **Service** (`CourseBoardBuilderService.cs`): replace `const int SprintCount = 8` and `const int ModuleCount = 5` with values from the request.
- **`ComputeSprintModules`**: rewrite the three track tables to be derived algorithmically from `SprintCount` and `ModuleCount` instead of hardcoded offsets.
  - Track A (Technical): setup sprints = 2, implementation sprints = ModuleCount, stabilization = 1. Total = ModuleCount + 3.
  - Track B (Leadership): planning sprint = 1, module sprints = ModuleCount, GTM + QA = 2. Total = ModuleCount + 3. Same minimum as Track A.
  - Track C (Customer-facing): same as B but module coverage stops 1 short (no Module 5 equivalent) and Sprint 6-equivalent is "special".
  - Guard: if `SprintCount < ModuleCount + 3`, return a validation error.
- **Prompt** (`CoursBuilder.txt`): the track tables are hardcoded. Two options:
  - Option A (simpler): inject a pre-computed sprint plan table into the user message (already done for modules), and remove the hardcoded tables from the system prompt. The system prompt stays generic ("follow the pre-computed plan below").
  - Option B: keep system prompt tables but generate them dynamically and inject into the system prompt at runtime.
  - **Recommended**: Option A — cleanest, least prompt bloat.
- **`BuildUserPrompt`**: already injects the sprint plan — just ensure it uses the dynamic `SprintCount` / `ModuleCount`.

### Validation rules to add
- `SprintCount >= ModuleCount + 3` (need setup + all modules + stabilization at minimum).
- `ModuleCount >= 2`.

---

## Phase 2 — Variable Sprint Length (alongside Phase 1, trivial)

### What changes
- `SprintLengthInDays` already exists on the request model (range 1–30, default 7). Currently unused in generation.
- **Service**: pass `SprintLengthInDays` into `BuildUserPrompt` and inject it into the user message context ("Each sprint is N days long").
- **Prompt**: add a note in the system prompt that sprint length affects task density — shorter sprints = fewer checklist items, longer sprints = more.
- **Card Content Rules**: adjust the `~9 / ~8 / 12–13 / 7` item counts to be relative to a 7-day baseline, scaled proportionally.

### No model changes needed — field already exists.

---

## Phase 3 — Variable Module Length (larger, separate project)

### What changes
- **New concept**: each module can span a different number of sprints (currently always 1 sprint = 1 module).
- **Request model**: add `ModuleLengths: List<int>?` — per-module sprint count. If null, defaults to all-1s.
- **`ComputeSprintModules`**: instead of 1:1 sprint→module mapping, expand modules across multiple sprints. E.g. Module 2 with length 2 occupies two consecutive sprint slots.
- **Sprint count validation**: `SprintCount` must equal `sum(ModuleLengths) + setup sprints + stabilization sprints`.
- **Prompt**: inject a richer sprint plan that labels "Sprint 4 (Module 2 — Week 1 of 2)" so the AI knows it's a mid-module sprint vs. a closing sprint.
- **Card naming**: add a suffix convention for multi-sprint modules (e.g., "Feature Logic & API Execution — Part 1 of 2").

### Complexity note
This is the most structurally invasive change. `ComputeSprintModules`, the prompt injection, and card naming all need coordinated updates. Do this in isolation after Phase 1/2 are stable.

---

## Out of Scope (for now)
- Per-role sprint count (all roles share the same sprint count).
- Dynamic few-shot selection based on sprint/module count.
- UI changes — the endpoint contract is backward-compatible (all new fields have defaults).

---

## Backward Compatibility
All new fields default to current behavior:
- `SprintCount` defaults to 8
- `ModuleCount` defaults to 5
- `SprintLengthInDays` already defaults to 7
- `ModuleLengths` defaults to null (all modules = 1 sprint)

Existing callers that don't pass new fields get identical output.
