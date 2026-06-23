# Plan: Student Test Runner

**Branch:** `feat/student-test-runner`  
**Scope:** BE only (`strAppersBackend/`)  
**Date:** 2026-06-23

---

## Goal

After every student push → build cycle, automatically run the project's tests and record pass/fail + output in `BoardStates`, so the mentor LLM can surface test results in its context.

---

## High-Level Flow (after this feature)

```
student git push
  → GitHub Actions: build → run tests → POST results to /api/Mentor/test-results → deploy to Railway
  → BoardStates updated (Source="TestRunner", LastTestStatus, LastTestOutput, LastTestRunDate)
  → Mentor LLM reads BoardStates → surfaces test results in chat
```

---

## DB Change

**3 new columns on `BoardStates`:**

| Column | Type | Purpose |
|--------|------|---------|
| `LastTestStatus` | varchar(50) | `PASS` / `FAIL` / `NO_TESTS` |
| `LastTestOutput` | text | Raw test runner output (truncated to ~4000 chars) |
| `LastTestRunDate` | timestamp with time zone | When the test run completed |

**SQL script:** `sql_test_runner_migration.sql` — run manually on prod DB.  
**Migration file:** `20260623120000_AddTestResultsToBoardStates.cs` — kept in sync but not applied via `dotnet ef`.

---

## Files Changed

### 1. `strAppersBackend/Models/BoardState.cs`
Add 3 new nullable properties matching the new columns.

### 2. `strAppersBackend/Migrations/20260623120000_AddTestResultsToBoardStates.cs`
New migration file — adds the 3 columns. Not applied via CLI; SQL script used instead.

### 3. `strAppersBackend/Services/GitHubService.cs` — Starter test files (per language)

Each `Generate*Backend()` method gets new test file entries added to its file dictionary:

| Language | Method | New files |
|----------|--------|-----------|
| C# | `GenerateCSharpBackend()` | `Tests/Tests.csproj`, `Tests/CalculatorTests.cs` |
| Python | `GeneratePythonBackend()` | `tests/__init__.py`, `tests/test_calculator.py` |
| Node.js | `GenerateNodeJSBackend()` | `tests/calculator.test.js` (+ jest in package.json) |
| Java | `GenerateJavaBackend()` | `src/test/java/com/example/CalculatorTest.java` |
| PHP | `GeneratePhpBackend()` | `tests/CalculatorTest.php`, `phpunit.xml` |
| Ruby | `GenerateRubyBackend()` | `spec/calculator_spec.rb` (+ rspec in Gemfile) |
| Go | `GenerateGoBackend()` | `calculator_test.go` |

Each starter test covers one trivial `add(a, b)` helper — the point is the runner works, not real coverage.

### 4. `strAppersBackend/Services/GitHubService.cs` — Workflow changes

**Method signature change:**
```csharp
// Before
private string GenerateRailwayDeploymentWorkflowAtRoot(string programmingLanguage)

// After
private string GenerateRailwayDeploymentWorkflowAtRoot(
    string programmingLanguage,
    string boardId = "",
    string strAppersApiUrl = "")
```

**`CreateBackendOnlyCommitAsync` signature change:**
```csharp
// Add two optional params
public async Task<bool> CreateBackendOnlyCommitAsync(
    string owner, string repositoryName, string projectTitle,
    string accessToken, string programmingLanguage,
    string? databaseConnectionString = null,
    string? webApiUrl = null,
    string? swaggerUrl = null,
    string boardId = "",           // NEW
    string strAppersApiUrl = "")   // NEW
```

**New test step added after build, before deploy** — a separate switch on `programmingLanguage` for test commands (all 7 languages handled):

| Language | Test command |
|----------|-------------|
| C# | `dotnet test Tests/Tests.csproj --no-build -v minimal` |
| Python | `pytest tests/ -q --tb=short` |
| Node.js | `npm test -- --passWithNoTests` |
| Java | `mvn test -q` |
| PHP | `./vendor/bin/phpunit tests/` |
| Ruby | `bundle exec rspec spec/ --format progress` |
| Go | `go test ./...` |

When `boardId` is non-empty, a second `Report Test Results` step is added that POSTs to `{strAppersApiUrl}/api/Mentor/test-results`. When `boardId` is empty (UtilitiesController path), that step is omitted.

### 5. `strAppersBackend/Controllers/BoardsController.cs`
Call site at line 2771 — pass `trelloBoardId` and `_configuration["ApiBaseUrl"]` to `CreateBackendOnlyCommitAsync`.  
Same update for the QuestMode call site at line 8823.

### 6. `strAppersBackend/Controllers/UtilitiesController.cs`
Update its local copy of `GenerateRailwayDeploymentWorkflowAtRoot` to add the same test step (no board ID — report step is skipped).

### 7. `strAppersBackend/Controllers/MentorController.cs`
New endpoint:
```
POST /api/Mentor/test-results
Body: { boardId, devRole, status, output }
```
- Finds or creates a `BoardState` row with `Source="TestRunner"` for this `boardId` + `devRole`
- Sets `LastTestStatus`, `LastTestOutput` (truncated to 4000 chars), `LastTestRunDate = UtcNow`
- No auth token required (called from GitHub Actions; board ID acts as the identifier)

### 8. `strAppersBackend/Prompts/Mentor/BoardStatesAwareness.txt`
Add one instruction line for test results awareness.

---

## Implementation Order

1. `BoardState.cs` model + migration file
2. `POST /api/Mentor/test-results` endpoint
3. Starter test files in each `Generate*Backend()` method
4. Workflow test step + signature changes in `GitHubService.cs`
5. Call site updates in `BoardsController.cs` (both standard + QuestMode paths)
6. `UtilitiesController.cs` local copy update
7. `BoardStatesAwareness.txt` update

---

## Out of Scope

- Frontend test runner
- Test coverage reports
- Blocking deploy on test failure (intentional)
- Existing boards (only new boards get starter test files)

---

## Pre-existing Bug (noted, not fixed)

`GenerateRailwayDeploymentWorkflowAtRoot` build-commands switch only handles C#, Python, Node.js, Java. PHP, Ruby, Go fall to default C# build commands. Railway's own Nixpacks build detection masks this. Our new test-commands switch handles all 7 correctly; the build switch is left as-is.
