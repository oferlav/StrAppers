# Student Team Builder Service

Windows service that creates Trello boards for projects and runs due sprint merges.

## Config files

- **Dev/Test:** `appsettings.json` — used when the service is started with first argument `Dev` or when running from a path that does not contain `publish-prod`.
- **Prod:** `appsettings.Prod.json` — used when the service is started with first argument `Prod` or when the executable path contains `publish-prod`.

Only one of these files is loaded per run (no layering). Both files are copied to the publish output by the project; the correct one is chosen at startup.

## Config structure (aligned with code)

| Section | Keys | Used by |
|--------|------|--------|
| `Service` | `Name` | Windows service display name |
| `ConnectionStrings` | `DefaultConnection` | PostgreSQL (boards, students, run-due-sprint-merges) |
| `Backend` | `BaseUrl` | API base URL for create-board and run-due-sprint-merges |
| `KickoffConfig` | `MinimumStudents`, `RequireAdmin`, `RequireUIUXDesigner`, `RequireProductManager`, `RequireDeveloperRule`, `MaxPendingTime` | Board creation eligibility |
| `Worker` | `IntervalMinutes`, `MaxBoardsPerIteration` | Polling interval; max boards per iteration (optional) |
| `ProjectCriteriaConfig` | `PopularProjectsRate`, `NewProjectsMaxDays` | Criteria update logic |

## Where the service runs from

You can run the service from the publish folders under the repo:

- **Dev:** `C:\StrAppers\StudentTeamBuilderService\publish-dev`  
  Config at runtime: `publish-dev\appsettings.json` (start with argument `Dev`).
- **Prod:** `C:\StrAppers\StudentTeamBuilderService\publish-prod`  
  Config at runtime: `publish-prod\appsettings.Prod.json` (start with argument `Prod` or path contains `publish-prod`).

Alternatively, use the deploy scripts in `Scripts\` to copy to `C:\Services\StudentTeamBuilderService-Dev` and `C:\Services\StudentTeamBuilderService-Prod` and install the Windows service there.

## Publish

- **Dev:** `dotnet publish -c Release -o publish-dev`  
  Then start with: `StudentTeamBuilderService.exe Dev`
- **Prod:** `dotnet publish -c Release -o publish-prod`  
  Then start with: `StudentTeamBuilderService.exe Prod`

Do not publish into a path that is already inside another publish folder (e.g. do not run `dotnet publish -o publish-prod` from inside `publish-dev`), to avoid nested `publish-dev` / `publish-prod` trees.

After cleaning nested folders, run `dotnet publish -c Release -o publish-dev` and `dotnet publish -c Release -o publish-prod` from the project root so each publish folder contains the exe plus `appsettings.json` and `appsettings.Prod.json`.
