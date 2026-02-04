# Prompt refactor list: places that can be refactored to DB (or config)

Only **non-fragmented** and **non-variant** prompt text is safe to move to DB. This list contains **only** places that can be refactored (in code or in config), sorted by PromptType and category.

---

## General

| Location | Category | Refactor To DB | Notes |
|----------|----------|----------------|-------|
| **MentorController** | | | |
| `GetPlatformContextAndLimitations()` | Platform Context and Vision | ✅ Yes | Single block; currently hardcoded. Read from MentorPrompt category "Platform Context and Vision". |
| `GetSystemPrompt()` | Base + Platform | ✅ Yes | Base prompt from config → read from MentorPrompt categories "Base Mentor Persona and General Instructions" + "Trello Knowledge". Platform from DB (above). |
| `GetSystemPromptRefactor()` | — | ✅ Yes | Already reads from MentorPrompt (non-fragmented, non-variant only when using InsertMentorPrompt_RefactorSafeOnly). |
| **Config** | | | |
| `PromptConfig:Mentor:SystemPrompt` (appsettings) | Base Mentor Persona + Trello Knowledge | ✅ Yes | Replace with DB read from categories "Base Mentor Persona and General Instructions" and "Trello Knowledge". |
| `PromptConfig:Mentor:BoardStatesAwareness` | Board States awareness (full JSON) | ✅ Referenced | Config overrides when set. Else loaded from `Prompts/Mentor/BoardStatesAwareness.txt` (no large inline strings in code). |
| `PromptConfig:Mentor:BoardStatesAwarenessWithoutJson` | Board States awareness (FULL STACK only) | ✅ Referenced | Config overrides when set. Else `Prompts/Mentor/BoardStatesAwarenessWithoutJson.txt`. |
| `PromptConfig:Mentor:BoardHealthFirstLastInstruction` | Last instruction (board health first) | ✅ Referenced | Config overrides when set. Else `Prompts/Mentor/BoardHealthFirstLastInstruction.txt`. |
| **Script** | | | |
| `InsertMentorPrompt_RefactorSafeOnly_PostgreSQL.sql` | — | — | Inserts only refactor-safe rows: Platform Context (1), Base Mentor Persona (1), Trello Knowledge (2). |

---

## Developers

| Location | Category | Refactor To DB | Notes |
|----------|----------|----------------|-------|
| **MentorController** | | | |
| Code review flow: `_promptConfig.Mentor.CodeReview.ReviewSystemPrompt` | Code Review | ⚠️ Possible | Single block from config; dynamic context injected by agent. Could move to DB as one row (no variants). |
| **Config** | | | |
| `PromptConfig:Mentor:CodeReview:ReviewSystemPrompt` (appsettings) | Code Review | ⚠️ Possible | Single block; refactor to DB optional. |

---

## SprintPlanning

| Location | Category | Refactor To DB | Notes |
|----------|----------|----------------|-------|
| **Config** | | | |
| `PromptConfig:ProjectModules:InitiateModules:SystemPrompt` | ProjectModules | ✅ Yes | Single block; no variants. |
| `PromptConfig:ProjectModules:CreateDataModel:SystemPrompt` | ProjectModules | ✅ Yes | Single block; no variants. |
| `PromptConfig:ProjectModules:UpdateModule:SystemPrompt` | ProjectModules | ✅ Yes | Single block; no variants. |
| `PromptConfig:SprintPlanning:SystemPrompt` | SprintPlanning | ✅ Yes | Template with placeholders; single block. |
| **TrelloConfig** (appsettings) | | | |
| `Trello:SprintMergePrompt` | SprintPlanning | ✅ Yes | Single template with `{{LiveSprintJson}}`, `{{SystemSprintJson}}`. |
| **Code** | | | |
| UtilitiesController: `ParseChecklistWithAiAsync` / `NormalizeChecklistWithAiAsync` | SprintPlanning | ⚠️ Possible | Small inline prompts; could move to config or DB. |
| BoardsController: `DebugAIPrompt` | SprintPlanning | Keep | Debug-only; no refactor needed. |

---

## Customer

| Location | Category | Refactor To DB | Notes |
|----------|----------|----------------|-------|
| **Config** | | | |
| `PromptConfig:Customer:SystemPrompt` (appsettings) | Customer | ✅ Yes | Single block with placeholders `[INSERT PROJECT DESCRIPTION HERE]`, `[INSERT PROJECT-SPECIFIC DESIGN/LOGIC DATA HERE]`. |

---

## Categories (PromptCategories / MentorPrompt)

| Category | Refactor-safe? | Notes |
|----------|----------------|-------|
| Platform Context and Vision | ✅ Yes | Single fragment; no variants. |
| Base Mentor Persona and General Instructions | ✅ Yes | Single fragment; no variants. |
| Trello Knowledge | ✅ Yes | Two fragments (custom fields, dependency rules); both single-block, no variants. |
| Current Mentor Context | ❌ No | Variants (new/existing conversation, meeting state); keep in code. |
| User Message Templates | ❌ No | User message template; excluded from system prompt; keep in code/config or separate category. |

---

## Summary

- **General:** Refactor to DB: Platform Context, Base Mentor Persona, Trello Knowledge (use `InsertMentorPrompt_RefactorSafeOnly_PostgreSQL.sql`). Then wire `GetPlatformContextAndLimitations()` and `GetSystemPrompt()` to read from MentorPrompt (filter by category, exclude User Message Templates and conditional variants).
- **Developers:** Code review prompt can optionally move to DB (single block).
- **SprintPlanning:** ProjectModules and SprintPlanning config prompts are single-block; can move to DB or keep in config.
- **Customer:** SystemPrompt is single-block with placeholders; can move to DB or keep in config.
