# Tester Agent Briefing — StrAppers Platform (Pre-Go-Live QA)

## Your role
You are a QA/testing agent for the StrAppers platform. Your job is to:
1. Write integration tests (xUnit) for the backend changes listed below
2. Tell the user exactly what manual steps they need to take

Do not modify application source code. Only create test files.

---

## Stack

- **Backend**: ASP.NET Core, C#, EF Core, PostgreSQL (Neon). Project root: `C:\ClaudeCode\StrAppers\strAppersBackend`
- **Frontend**: React + Vite. Project root: `C:\ClaudeCode\StrAppers\skill-in-Frontend`
- **Test framework to use**: xUnit + Moq + EF Core InMemory (`Microsoft.EntityFrameworkCore.InMemory`)
- **Do not create a full WebApplicationFactory integration test project** — the DB requires real credentials we don't have in CI. Use InMemory EF + mocked `IConfiguration` instead.
- Create the test project at: `C:\ClaudeCode\StrAppers\strAppersBackend.Tests\`
- Target framework: match `strAppersBackend.csproj` (check `<TargetFramework>` in that file)
- Reference the main project: `<ProjectReference Include="..\strAppersBackend\strAppersBackend.csproj" />`

---

## What was changed — what to test

### 1. BOARD STATES branch filter (`MentorController.cs` ~line 5815)

**Logic**: When `student.ProjectBoard.IsSingleRole == true` AND `student.RoleIndex > 0`, the board states query filters to keep only records where `GithubBranch` is empty/null (Railway, GitHub Pages — shared deployment) OR ends with `-{student.RoleIndex}`.

**What to test**:
- Student with `IsSingleRole=true, RoleIndex=2`: board states for branches `2-B-2`, `2-F-2` are returned; states for `2-B-1`, `2-F-1` (another developer's) are excluded; Railway records with empty `GithubBranch` are included.
- Student with `IsSingleRole=false` (squad course): all board states returned regardless of branch.
- Student with `IsSingleRole=true, RoleIndex=0` (unassigned): all board states returned (filter only applies when `RoleIndex > 0`).

**Key models**:
- `Student.RoleIndex` (int, default 0) — `Models/Student.cs`
- `ProjectBoard.IsSingleRole` (bool, default false) — `Models/ProjectBoard.cs`
- `BoardState.GithubBranch` (string?) — `Models/BoardState.cs`

**How to test**: Extract the filter logic into a pure helper or test it directly via an InMemory DbContext. The simplest approach: seed the InMemory DB, run the same LINQ query the controller uses, assert result set.

---

### 2. Mentor model resolution (`MentorController.cs` ~line 4095)

**Logic**: `POST /api/Mentor/use/respond/{aiModelName}` — when `aiModelName` is `"default"` (or empty), the controller reads `_configuration["Mentor:AiModel"]` and uses that to look up the model in the DB. Falls back to first active DB model if config is also empty.

**What to test**:
- `aiModelName = "default"`, config has `Mentor:AiModel = "claude-sonnet-4-5-20250929"`, DB has that model active → resolves to claude model.
- `aiModelName = "default"`, config has `Mentor:AiModel = "claude-sonnet-4-5-20250929"`, DB does NOT have that model → falls back to first active DB model.
- `aiModelName = "default"`, config key missing, DB has one active model → resolves to that DB model.
- `aiModelName = "gpt-4o-mini"` (explicit, not "default") → resolves to gpt-4o-mini from DB, ignores config.

**How to test**: The resolution block is at the top of `GetMentorResponse`. Extract it or test via a thin wrapper. Use `Mock<IConfiguration>` (Moq) for config, InMemory DbContext for DB.

---

### 3. Customer model resolution (`CustomerController.cs` ~line 94)

Same pattern as #2 but reads `Customer:AiModel` from config. Write the same 4 test cases.

---

### 4. AI provider routing in `AIService.cs` (`GenerateTextResponseAsync`)

**Logic**: If model name starts with `"claude-"` (case-insensitive) → calls Anthropic API. Otherwise → calls OpenAI API.

**What to test**:
- Model `"claude-sonnet-4-5-20250929"` → routes to Anthropic (mock HttpClient, assert the request URL contains `api.anthropic.com` or the request headers contain `x-api-key`).
- Model `"gpt-4o-mini"` → routes to OpenAI (assert different URL/path).
- Model `null` → falls back to `_aiConfig.Model` and routes accordingly.

**How to test**: `AIService` takes `IHttpClientFactory` (injectable). Use `Mock<IHttpClientFactory>` returning a `MockHttpMessageHandler`. Assert which URL was called.

---

## What to tell the user to do manually

After writing the tests, tell the user:

### A. Run the tests
```
cd C:\ClaudeCode\StrAppers\strAppersBackend.Tests
dotnet test
```

### B. Run `/ultrareview` in Claude Code
Type `/ultrareview` in their Claude Code session (no arguments — it reviews the current branch automatically).
Focus areas to mention to the reviewer: `MentorController.cs` model resolution block, `AIService.cs` Anthropic routing, `ChatSidebar.jsx` model state removal.

### C. Smoke test (no log-checking needed — check response body)
Send any message through the mentor chat panel. In the browser network tab (or Postman), inspect the response body:
```json
{
  "success": true,
  "model": { "name": "claude-sonnet-4-5-20250929", "provider": "Anthropic" },
  "response": "..."
}
```
If `model.name` matches the configured `Mentor:AiModel` value in appsettings — the chain is working.

For code review: trigger a code review and check the response body for `"modelUsed": "claude-sonnet-4-5-20250929"`.

### D. DB INSERT (if not yet done)
```sql
INSERT INTO "AIModels" ("Name", "Provider", "BaseUrl", "ApiVersion", "MaxTokens", "DefaultTemperature", "Description", "IsActive", "CreatedAt")
SELECT 'claude-sonnet-4-5-20250929', 'Anthropic', 'https://api.anthropic.com/v1', '2023-06-01', 200000, 0.3, 'Anthropic Claude Sonnet 4.5', TRUE, CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM "AIModels" WHERE "Name" = 'claude-sonnet-4-5-20250929');
```

### E. appsettings.json on the server — add these keys
```json
"Mentor": { "AiModel": "claude-sonnet-4-5-20250929" },
"Customer": { "AiModel": "gpt-4o-mini" },
"Assessment": { "AiModel": "claude-sonnet-4-5-20250929" }
```
The value must exactly match the `Name` column in the `AIModels` table.

---

## Files most relevant to read before writing tests

- `Controllers/MentorController.cs` — lines 4082–4123 (respond endpoint + model resolution)
- `Controllers/CustomerController.cs` — lines 71–100 (respond endpoint + model resolution)
- `Services/AIService.cs` — `GenerateTextResponseAsync` and `CallAnthropicTextAsync`
- `Models/AIModel.cs`, `Models/Student.cs`, `Models/ProjectBoard.cs`, `Models/BoardState.cs`
- `Data/ApplicationDbContext.cs` — to understand DbContext shape for InMemory setup

## Out of scope for this task
- Frontend tests
- Prompt content / AI response quality
- Testing the `/ultrareview` results (that's for the human to review)
