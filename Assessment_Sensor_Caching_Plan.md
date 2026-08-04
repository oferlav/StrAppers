# Assessment Sensor Caching — Plan

**Status:** Deferred. Written to capture the design and the open questions; not scheduled.

**Scope:** A caching layer for the *inputs* of the Data Assessment Engine — the evidence blocks each sensor builds — for all sensors, not only GitHub.

---

## 1. Why

Three separate problems, one mechanism.

**Cost / rate limits.** The assessment engine runs **per metric**, not per student. A student with six metrics that share a sensor rebuilds identical evidence six times. For live sensors (GitHub, Figma, Trello, meeting transcripts, document extraction) that means repeated external API calls. GitHub in particular runs on a single shared platform token with a fixed hourly budget, so a cohort-wide run multiplies quickly: students × metrics × tracks.

**Latency.** The same duplication is paid in wall-clock time on every assessment run.

**Reproducibility — the one that matters long term.** External evidence is not durable. Branches are deleted after merge, repos are archived or removed when a course ends, access is revoked, history is rewritten by force-push, Figma tokens expire, Trello cards are edited or deleted. Re-running an assessment on a past sprint therefore produces *different evidence* than the original run — and so a different score, with no way to explain the difference. If a grade is ever questioned by a student, a faculty member, or an accreditation review, the evidence it was based on must still exist.

A snapshot makes assessments **auditable and re-computable**: identical inputs go in, so any change in the score is attributable to the rubric or the model, never to drift in the outside world.

---

## 2. Shape

**Grain:** one row per `board + student + sprint + sensor`.

**Stored payload:** the rendered evidence block the sensor produces (the markdown that goes into the prompt), plus:
- captured-at timestamp
- format version stamp
- the sprint-window state at capture time (open / closed)
- a capture status (complete / partial / failed-to-observe)

**Why the rendered block rather than raw API responses:** it is exactly what the model saw, which is the whole point of an audit trail. Caching raw responses and re-rendering later is more flexible, but a renderer change would silently alter what a past assessment is shown to have used — defeating the purpose. The format version stamp is the escape hatch: bump it to deliberately invalidate old entries when the rendering changes.

---

## 3. Freshness rules

| Sprint window | Behaviour |
|---|---|
| **Open** | Short TTL. Long enough to absorb a burst of metrics running back to back, short enough that mid-sprint evidence is current. This is what removes the per-metric multiplication. |
| **Closed** | Frozen permanently. Never refetched. |
| **Any** | An explicit refresh overrides both. |

A "failed to observe" capture (token error, API outage, missing repo URL) must **not** be frozen — it should carry a short TTL regardless of window state, so a transient outage does not permanently poison a sprint's evidence.

---

## 4. Interaction with existing result caching

Assessment *results* are already cached. This adds a layer beneath that:

```
sensor evidence (new cache)  →  prompt  →  LLM  →  assessment result (existing cache)
```

Refreshing sensor evidence should not silently invalidate a stored result — the result stays until the assessment is re-run. Worth deciding whether a staff-triggered evidence refresh should also mark dependent results as stale in the UI.

---

## 5. Rollout

1. Table plus read/write path, no behaviour change (write-through, always read fresh) — verifies capture works.
2. Enable read-from-cache for the GitHub sensor only, under the freshness rules above.
3. Extend to the remaining live sensors (Figma, Trello, transcripts, document extraction).
4. Optionally extend to the cheap database-backed sensors for uniformity.

**Verification at each step:** run an assessment twice in a row; the second run must produce a byte-identical evidence block and make no external API calls.

---

## 6. Open questions

1. **TTL while the sprint is open.** Minutes rather than hours is the instinct. What is the tolerance for slightly stale evidence in a mid-sprint assessment?
2. **Where does explicit refresh live** — a flag on the assess call, a button in the staff metrics UI, or both?
3. **Freeze on sprint close — confirm the policy.** If a student pushes to a sprint branch after the sprint ends, a frozen snapshot means the assessment never sees it. Defensible (the sprint is over), but it is a policy call, not a technical one.
4. **Retention.** Diffs and transcripts are large text, and the table grows as board × student × sprint × sensor. Options: a size cap per entry, an expiry for non-final sprints, or keep everything indefinitely for audit. What is the storage tolerance?
5. **Scope on day one.** All sensors, or GitHub first and extend? The cheap database-backed sensors gain little from caching and add staleness risk, though the open-sprint TTL makes that safe.
6. **Should a sensor refresh mark dependent cached assessment results as stale** in the staff UI, or is that noise?
7. **Per-institute or global?** If different institutes have different retention or audit obligations, the freshness and retention rules may need to be configurable rather than fixed.
8. **Failure visibility.** When a capture is "failed to observe", should that surface to staff proactively (log, email, dashboard flag), or only appear inside the assessment prompt?

---

## 7. Explicitly out of scope

- Caching the LLM responses themselves (already handled by the existing result cache).
- Any change to how sensors *build* their evidence. This plan only stores what they already produce.
