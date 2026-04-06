# Per-User Trello OAuth Refactor Plan

## Goal
Support private third-party Trello boards safely by using per-user OAuth tokens instead of one global token, while removing security debt (hardcoded secrets/config drift).

## 1) Target Architecture
- User connects Trello account via OAuth.
- Backend stores a TrelloConnection per user/org (encrypted token).
- Each ProjectBoard links to a specific TrelloConnection.
- Every Trello API call resolves token by board/user context.
- Global config keys remain only for OAuth app credentials (not runtime board access).

## 2) Security/Compliance Baseline (Do First)
1. Remove hardcoded credentials immediately:
   - Remove hardcoded Trello key/token from test endpoint(s).
   - Replace with config/env or delete endpoint if unnecessary.
2. Unify config namespace:
   - Standardize on one section (`Trello`), not mixed `Trello` and `TrelloConfig`.
3. Add secret management policy:
   - Tokens/secrets only via env/KeyVault/secret store.
   - Never log token values.
4. Add token redaction in logs/errors:
   - Ensure querystrings with key/token are never emitted.

## 3) Data Model Changes

### TrelloConnection
- Id (PK)
- OwnerStudentId (or org-level owner)
- TrelloMemberId
- TrelloUsername
- AccessTokenEncrypted
- Scopes
- ConnectedAt
- RevokedAt (nullable)
- LastValidatedAt (nullable)
- TokenVersion / metadata fields

### ProjectBoard updates
- Add nullable TrelloConnectionId FK
- Backfill existing boards to a legacy/system connection if needed

### Optional
- TrelloOAuthState table for anti-CSRF OAuth state/nonce.

## 4) OAuth Flow Endpoints
1. `GET /api/Trello/oauth/start`
   - Generates secure state, stores short-lived state record.
   - Redirects to Trello authorize URL with scopes.
2. `GET /api/Trello/oauth/callback`
   - Validates state
   - Exchanges/receives token
   - Fetches `members/me` to bind token identity
   - Saves encrypted token in TrelloConnection
3. `POST /api/Trello/oauth/disconnect`
   - Marks connection revoked and detaches/blocks boards.

Frontend:
- "Connect Trello" CTA
- Callback handler page
- Connected/disconnected status UI

## 5) Service Refactor (Core)
Current TrelloService uses global `_trelloConfig.ApiToken`. Refactor to token-aware calls:
1. Introduce `ITrelloCredentialProvider`:
   - `GetTokenForBoard(boardId, userContext)`
   - `GetTokenForConnection(connectionId)`
2. Replace direct global token usage in runtime operations with resolved token.
3. Keep config token only for migration fallback/feature flag period.
4. Add request helpers that avoid leaking credentials in logs.

## 6) Authorization Rules
- User can access board actions only if:
  - they are authorized in app DB, and
  - board has valid linked TrelloConnection.
- For admin/system jobs:
  - explicitly decide if system token remains allowed (temporary only).
- Reject arbitrary boardId access for unauthorized users.

## 7) API Contract Updates
Affected endpoints include:
- `/api/Boards/use/stats/{boardId}`
- `/api/Trello/use/board/{boardId}/label/{label}`
- checklist toggle, sprint merge, member fetch, bug routes, etc.

Behavior changes:
- Board exists but token missing/revoked: return clear 409/422 with reconnect required.
- Token lacks board access: 403 with action hint.
- Preserve response shape where possible to reduce frontend regressions.

## 8) Migration Strategy (Low Risk)

### Phase A (dual-mode)
- Add TrelloConnection + FK.
- Keep legacy token fallback with feature flags:
  - `Trello:EnablePerUserOAuth`
  - `Trello:AllowLegacyTokenFallback` (true temporarily)

### Phase B (board linking)
- Connect owner/admin Trello accounts and set TrelloConnectionId.
- Add admin report: boards missing connection.

### Phase C (cutover)
- Disable legacy fallback in staging, then production.
- Remove old global runtime token paths.

## 9) Test Plan

### Unit tests
- Credential provider resolves correct token by board/user.
- Missing/revoked token handling.
- OAuth state/nonce validation.

### Integration tests
- OAuth start/callback happy path.
- Access private board via linked connection.
- Unauthorized board/token cases.
- Existing board stats/task endpoints continue functioning.

### Regression focus
- BoardRoom load (`invokeGetBoardStats`)
- Label task fetch (`invokeGetMyTasks`)
- Checklist toggles
- Bugs, sprint schedule, user stories routes.

## 10) Operational Runbook
- Admin screen/report for:
  - connection health
  - stale/revoked tokens
  - boards missing connection
- Scheduled token validation job (`members/me` ping).
- Alert on Trello 401/403 spikes.

## 11) Cleanup Checklist (Must Include)
- Remove hardcoded key/token from test endpoint.
- Remove credential-bearing querystrings from logs.
- Normalize config keys (`Trello:*` only).
- Remove deprecated Trello test/debug endpoints from production.
- Document OAuth setup (callback URL, scopes, env vars, rotation process).

## 12) Suggested Timeline (Near Future)
- Week 1: security cleanup + schema + credential provider scaffolding
- Week 2: OAuth endpoints + frontend connect flow + token encryption
- Week 3: refactor priority endpoints (`stats`, `label`, `set-done`) + dual-mode rollout
- Week 4: migrate active boards, disable fallback in staging, then prod cutover

## 13) Risks & Mitigations
- Users disconnect Trello -> board sync fails
  - Mitigation: graceful errors + reconnect UX + health checks
- Mixed old/new config breaks endpoints
  - Mitigation: single config contract + startup validation
- Auth gaps on boardId routes
  - Mitigation: centralized board access guard middleware/service
