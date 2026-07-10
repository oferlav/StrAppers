# Add StudentMapsApiKey To All Provisions

## Why

Google now enforces (since 2026-06-19) that Gemini API keys are restricted to the Gemini API **only** — they cannot be combined with other APIs on the same key. Our provisioned student backends use a single `GOOGLE_API_KEY` for Gemini **and** Geocoding, Maps JS, Directions, Places (New), and Speech-to-Text. Result: Gemini works, the other five return `REQUEST_DENIED` / `403 PERMISSION_DENIED ("requests ... are blocked")`.

Fix: two keys.
- **Gemini key** (existing `GoogleApis:StudentBackendApiKey`) → stays Gemini-only. Env var: `GOOGLE_API_KEY` (unchanged).
- **Maps key** (new `GoogleApis:StudentMapsApiKey`) → Geocoding, Maps JavaScript, Directions, Places (New), Cloud Speech-to-Text. Env var: `GOOGLE_MAPS_API_KEY` (new).

## Scope

Only boards provisioned **after** this change get the new env var automatically. Already-provisioned Railway services need `GOOGLE_MAPS_API_KEY` added manually (or via a one-off backfill, out of scope here). Generated-code fallback (below) means old boards keep exactly their current behavior.

## Changes

### 1. Config — `strAppersBackend/appsettings.json`
Add under existing `GoogleApis` section:
```json
"StudentMapsApiKey": ""
```
**Manual follow-ups (owner):**
- Paste the multi-API key value locally and in Azure App Service configuration (`GoogleApis__StudentMapsApiKey`).
- In Google Console, that key must have API restrictions: Geocoding API, Maps JavaScript API, Directions API, Places API (New), Cloud Speech-to-Text API; Application restrictions: None; billing linked.

### 2. Provisioning — `strAppersBackend/Controllers/BoardsController.cs` (3 push sites + 1 helper)
Everywhere `GOOGLE_API_KEY` is pushed to Railway, also push `GOOGLE_MAPS_API_KEY` from `GoogleApis:StudentMapsApiKey` (env fallback `GOOGLE_MAPS_API_KEY`):
1. **Early block** (~line 2372, after DATABASE_URL is set): add second `variableUpsert` mutation. `googleApiKeySet` flag becomes true only when the Gemini-key push succeeded and the maps-key push succeeded (or maps key isn't configured).
2. **Late fallback block** (~line 3483) + helper `UpdateRailwayGoogleApiKey` (~line 7440): helper pushes both variables.
3. **Quest board block** (~line 8926): add second `variableUpsert` for `GOOGLE_MAPS_API_KEY`.
Skip the push (with a warning log) when `StudentMapsApiKey` is empty — never push an empty value.

### 3. Generated templates — `strAppersBackend/Services/GitHubService.cs` (7 languages)
Same mechanical edit per language block:
- Add a second key getter reading `GOOGLE_MAPS_API_KEY`, **falling back to** `GOOGLE_API_KEY` when unset (old boards keep working exactly as today).
- 5 endpoints switch to the new getter: `geocoding`, `maps`, `directions`, `places`, `speech-to-text`. Their `not_configured` message becomes "GOOGLE_MAPS_API_KEY is not set."
- `gemini`, `health` (aliases gemini) unchanged — keep `GOOGLE_API_KEY`.
- `status` endpoint: keep `configured` (Gemini key) as-is; add `mapsConfigured` boolean; message updated to mention both keys.

| # | Language | Template file | Location (approx.) |
|---|----------|---------------|--------------------|
| 1 | C# | `backend/Services/GoogleApiKeyHelper.cs` + `backend/Controllers/GoogleApiController.cs` | 4732–4970 |
| 2 | Python | `backend/Controllers/GoogleApiController.py` | 6521–6674 |
| 3 | Node.js | `backend/Controllers/GoogleApiController.js` | 7214–7340 |
| 4 | Java | `.../Controllers/GoogleApiController.java` | 7877–8019 |
| 5 | PHP | inline router block | 8958–9033 |
| 6 | Ruby | Sinatra routes | 9586–9735 |
| 7 | Go | `GoogleApi*` handlers | 10379–10494 |

### 4. Docs in generated repos — `GitHubService.cs`
- README line ~4205: mention both env vars (`GOOGLE_API_KEY` for Gemini, `GOOGLE_MAPS_API_KEY` for Maps/Places/Directions/Geocoding/Speech).

### 5. Mentor prompt — `strAppersBackend/Controllers/MentorController.cs` (~line 1690)
Update the sentence about platform-managed Google keys to name both env vars.

## Not changing
- Dashboard HTML test buttons (they call the backend endpoints; endpoints and response shapes stay compatible).
- DB, frontend, Windows service.
- Existing Railway services (manual/backfill later).

## Verification
1. `dotnet build` strAppersBackend — clean.
2. Run existing test suite for the backend project.
3. Spot-check each of the 7 template blocks' diff (getter added, 5 endpoints switched, gemini untouched).
4. After deploy + key configured: create a test board, confirm Railway env has both vars, hit `/api/google/gemini` and `/api/google/geocoding` on the student backend → both `status: ok`.

## Risk
Mechanical edit repeated across 7 near-duplicate string-literal templates — main risk is a missed swap in one language (that language's Maps endpoints would silently keep using the Gemini key and stay blocked). Mitigated by per-language diff review and a final grep asserting each block references `GOOGLE_MAPS_API_KEY`.
