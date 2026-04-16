# Plan: Figma metadata controls & mentor (UI/UX) alignment

This document captures agreed directions for **`POST /api/Figma/use/download-metadata`**, related **rate-limit / payload** protections, **caching**, and **mentor system prompt** updates for the **UI/UX Designer** role. Use it when implementing or revisiting the “Figma metadata issue.”

**Related code (backend)**

- `strAppersBackend/Controllers/FigmaController.cs` — `DownloadMetadata`, `GetFigmaFileMetadata`, `TryExtractNodeIdFromFigmaUrl`, `NormalizeFigmaNodeId`, `DownloadMetadataRequest`
- `strAppersBackend/Controllers/MentorController.FigmaFrameReview.cs` — calls download-metadata, size limits for LLM path
- `strAppersBackend/Controllers/TestController.cs` — optional pruning / metadata-LLM test path

**Related prompts**

- `strAppersBackend/Prompts/Mentor/FigmaFrameReviewSystem.txt` — main Figma frame review instructions
- `strAppersBackend/Prompts/Mentor/MentorUiUxRoleContext.txt` — UI/UX role context (Figma sharing, naming)

---

## 1. Goals

1. **Protect Figma REST API quota** (especially **Tier 1** `GET /v1/files/:key`) and reduce **429** / blocked usage.
2. **Bound payload size** so responses stay fast, cheap to store, and safe for downstream LLM steps.
3. **Avoid misleading reviews** when metadata is partial, truncated, or scoped to a subtree.
4. **Keep designer workflow sane**: one changed file should be reviewable again without stale cache traps.

Official reference: [Figma REST API rate limits](https://developers.figma.com/docs/rest-api/rate-limits/) (tiers depend on **seat**, **endpoint tier**, and **plan of the file**). `GET /v1/files` is **Tier 1** — see [file endpoints](https://developers.figma.com/docs/rest-api/file-endpoints/#get-file).

---

## 2. Constraints to enforce (backend)

### 2.1 Single root `node-id` (no multi-frame URLs)

**Problem:** Figma allows `ids` as a **comma-separated** list. Your URL parser can pass through `node-id=1-1,1-2` → multiple subtrees in **one** Tier‑1 call → large merged JSON and higher blast radius.

**Recommendation**

- After parsing `node-id` / `node_id`, **reject** if the normalized value contains **`,`** (or split and require **exactly one** id).
- Optionally reject URLs with **repeated** `node-id` query keys if you want stricter behavior.
- Return **400** with a short message: use **one** “Copy link to selection” for a **single** frame/section.

**Note:** This does **not** stop a **single** huge frame (one id, many layers). Combine with depth and size limits below.

### 2.2 Default and maximum `depth`

**Problem:** Unbounded depth returns enormous trees (design-system pages, whole files under one node).

**Recommendation**

- For **automated** mentor / review flows, **default `Depth`** to a conservative range (e.g. **6–10**) when the client omits it (product decision).
- **Cap** `Depth` at a maximum (e.g. 12–15) to prevent abuse.
- Document that lower depth shrinks payload; users who need deeper structure opt in explicitly.

### 2.3 Maximum response size (bytes or characters)

**Problem:** No official Figma “max MB” for free tier; practical issues are **timeouts**, **memory**, and **retry storms** after 429.

**Recommendation**

- After `GET /v1/files` succeeds, if body exceeds a configurable limit (e.g. **4 MiB** default, **2 MiB** stricter for education/Starter-heavy traffic), **do not** stream huge JSON to clients/LLM — return **400/413** with guidance: narrower `node-id`, lower `depth`, or split reviews.
- Align with any existing LLM path limits (e.g. `FigmaMetadataLlm:MaxMetadataChars` in mentor test flow).

### 2.4 Figma API behavior (document for implementers)

From Figma docs: with `ids`, the response can still include **extra** nodes (e.g. dependencies, historical quirk: top-level canvas nodes). **Payload can be larger than “one frame”** even with one id — size caps still matter.

### 2.5 Error handling

- Continue to respect **429**, surface **`Retry-After`**, **`X-Figma-Plan-Tier`**, **`X-Figma-Rate-Limit-Type`** where applicable (already partially present on failure payloads).

---

## 3. Caching strategy

### 3.1 What caching is for

- Dedupe **rapid duplicate** calls (retries, double-submit, mentor pipeline steps).
- **Not** a substitute for “user changed Figma” unless combined with invalidation or refresh.

### 3.2 Recommended building blocks

| Mechanism | Purpose |
|--------|---------|
| **Short TTL** (e.g. 2–10 minutes) | Cheap dedupe; natural expiry for casual “review again” after edits. |
| **`forceRefresh` (or equivalent)** | User or client explicitly **bypasses cache** for a new review after Figma changes. |
| **Version-aware invalidation** | Store Figma **`version`** / **`lastModified`** from file JSON (or compare via **`GET /v1/files/:key/meta`**, Tier 3) before serving cache — refetch Tier 1 when file changed. |
| **Session-scoped cache** | Optional: cache only within one review job id — minimal staleness, smaller win. |

### 3.2 Product copy

Tell users: we may reuse a **recent** snapshot to save API quota; after **material edits**, use **refresh** / new review with refresh so the mentor sees latest Figma.

---

## 4. Mentor system prompt updates (UI/UX Designer role)

Update **`FigmaFrameReviewSystem.txt`** and extend **`MentorUiUxRoleContext.txt`** (or equivalent assembly for UI/UX) so the model **understands scope and limits** and does not mislead users.

### 4.1 Scope of the Figma payload (what the LLM must assume)

Add a short section **“Metadata scope and limits”**:

1. **Single selection link:** The pipeline should use **one** linked node (one subtree). The model must **not** infer that many `nodeId` values in the tree mean “multiple frames were linked” — **many ids appear because every layer has an id**.
2. **Subtree only:** The JSON is **not** guaranteed to be the full file; it is scoped to the linked node (plus Figma’s documented extras). Do not assume unseen pages/frames are “missing from the design” if they were never in scope.
3. **Depth / pruning:** If the payload is projected, pruned, or depth-limited, **deeper children or sibling sections may be absent**. Call out “not visible in this export” instead of “absent from the design” when hierarchy is cut off.
4. **Heuristics unchanged:** Keep existing language that `role`, tokens, etc. are heuristic.

### 4.2 Designer-facing guidance (in `MentorUiUxRoleContext.txt` or sibling)

Add bullets:

- Link **one** frame or section relevant to the sprint (**“Copy link to selection”**), not a whole page or multiple selections in one URL (when the platform enforces single-id).
- After **significant edits**, trigger a **fresh** review / refresh so the mentor is not commenting on stale structure.
- Prefer **focused** selections for review quality and faster feedback.

### 4.3 Output behavior (optional tweak to `FigmaFrameReviewSystem.txt`)

- When the tree is large, **prioritize** sprint-aligned subtrees (already partially stated) — reinforce that **listing dozens of default-named layers** is less valuable than sprint-critical gaps; keep caps on list length (already partially there for hygiene).

---

## 5. Implementation phases (suggested order)

1. **Validation:** Single node id only; reject comma-separated / multi-param abuse; cap `depth`; configurable max response size with clear errors.
2. **Observability:** Log `fileKey`, normalized node id, `depth`, response byte size, 429s — no secrets.
3. **Caching:** In-memory or distributed cache key `(boardId or app id, fileKey, nodeId, depth)` + TTL; add **`forceRefresh`** on download-metadata (and mentor caller).
4. **Optional:** Version/meta check before returning cache.
5. **Prompts:** Patch `FigmaFrameReviewSystem.txt` and `MentorUiUxRoleContext.txt` per §4.
6. **Docs / UI:** User-visible strings for errors and “refresh after editing Figma.”

---

## 6. Open decisions (fill in when implementing)

- Default **`Depth`** for production mentor flow (if not sent by client).
- Exact **max JSON bytes** (e.g. 2 MiB vs 4 MiB) per environment.
- Whether **`GET /v1/files/:key/meta`** is acceptable on every cache lookup (Tier 3 vs Tier 1 tradeoff).
- Cache store: **memory** vs **Redis** vs DB blob.

---

## 7. Revision history

| Date | Notes |
|------|--------|
| 2026-04-13 | Initial plan: single-id validation, depth/size limits, caching, mentor prompt alignment. |
