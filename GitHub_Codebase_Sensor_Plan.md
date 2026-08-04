# Codebase & GitHub Sensor — Rebuild Plan

**Goal:** make the existing "Codebase & GitHub" sensor of the Data Assessment Engine provide *direct evidence* — the actual diffs and branch metadata for the student's sprint branch — instead of a single CI status string.

**Decisions already taken (do not re-litigate):**
- Reuses the **existing** Codebase & GitHub toggle. No new sensor flag, no migration for a flag.
- **No caching.** Repeated fetching across metrics is accepted; usage is controlled by turning the sensor off on metrics that don't need code.
- **Secrets redaction in diffs is out of scope.** Raw patches are sent to the model as-is.
- **Bugs branches (sprint 0) are excluded** from numbered-sprint assessments. Revisit later.
- **One combined section** covering both repos for full-stack students.
- **Commit messages are included.**
- The sprint-number fix is **part of this plan**.

---

## 1. Today's behaviour (what is being replaced)

The sensor reads a board-state table filtered to rows whose sprint number matches the assessment's sprint. In practice only CI test rows carry a sprint number, so the section the model receives is effectively one line — a PASS/FAIL/NO_TESTS string — or nothing at all. It makes no GitHub calls, shows no code, and cannot distinguish "student delivered nothing" from "the platform never recorded anything."

---

## 2. What the sensor will emit

A single section, `### Code & GitHub`, containing three parts.

### 2.1 Combined summary line
One line spanning all tracks: total commits, total PRs, whether anything merged, total files changed and +/-. Gives the model the shape of the delivery before it reads detail.

### 2.2 Per-track sub-blocks
One per repository track the student owns (backend, frontend, or both). Each contains:

**Delivery metadata**
- Repository and branch inspected, and how the branch name was derived (default convention, or an override from the sprint card's branch-context field)
- Branch state: exists / deleted after merge / never existed
- Commits: count, first and last commit date, and the commit messages
- Pull requests: count, and per PR — number, state, merged yes/no, merge date, and review outcome where recorded
- Files changed, additions, deletions
- A note when commits fall outside the sprint window

**The diff** — per file: path, status, +/- counts, and the patch.

### 2.3 CI status sub-block
Labelled **"CI Status (build & tests)"**, built from the board-state table, scoped to this sprint and this student's branches. Reports build status, test status, and last run date. This is supplementary signal, clearly separated from the GitHub evidence above it.

### 2.4 Not-applicable line (non-developer roles)
When the student's role has no repository, the section contains only:

> This sensor measures code delivered to a sprint branch. This student's role is *{role}*, which has no repository or branch, so there is nothing here to measure. This is expected. Do not create a score category for it and do not reduce any score because this section is empty.

The final sentence is load-bearing — without it models routinely invent a "no code contribution" penalty for a Product Manager.

---

## 3. Evidence resolution order

This order is what makes assessment of **completed** sprints work. A naive implementation that compares the branch to main returns an empty diff for every merged sprint, and the model scores zero for work that was actually delivered.

1. **Resolve the repository.** Board-level backend/frontend URL; fall back to the per-student URL for QuestMode boards.
2. **Resolve the branch.** Default convention `{sprint}-B` / `{sprint}-F`, with the role-index suffix when the board is role-indexed (index read from the student record, never guessed). An override from the sprint card's branch-context field takes precedence; if the override yields no evidence, retry the default name.
3. **Look up pull requests first** — across open *and* merged, including the client-side scan of recent closed PRs. GitHub's head filter misses PRs whose branch was deleted.
4. **If a merged PR exists, take the diff from the PR's own file list.** After a squash merge the branch compare shows zero files and the branch may no longer exist. The PR file list is the only surviving evidence.
5. **Otherwise compare the branch against the base**, trying the configured base then the fallbacks.
6. **Count commits from the PR's commit list when the branch is gone**, from the branch otherwise. A squash merge collapses N commits into one on main — count the PR's, not main's.

**Student attribution** is by **repository + branch**, not branch name alone. Branch names are not unique across QuestMode repos. A full-stack student maps to a *set* of branches, which is why the section is combined.

---

## 4. Failure vocabulary

The section must never be silently empty. Each state below is a different score and must be named distinctly in the emitted text:

| State | Meaning for scoring |
|---|---|
| No branch, no PR | Nothing was delivered |
| Branch with commits, no PR | Work exists, never submitted for review |
| PR open, not merged | Submitted, not landed |
| PR merged | Delivered |
| Repo URL missing / token failure / API error | **Platform could not observe** — not a student outcome |
| Sprint window still open | In flight; absence of a merge is not a gap |

The last two are the ones that silently produce unfair grades. They are emitted as facts, and paired with a scoring rule instructing the model to withhold judgement rather than score zero.

---

## 5. Sprint-number fix (in scope)

Required for the CI sub-block, and independently valuable.

**At write time:** every board-state writer that has a branch name populates the sprint number from it, using the existing branch-name convention parser. This covers code review, PR validations, merge events, PR webhooks, and CI test results.

**Backfill:** a one-time pass over existing rows that have a branch name but no sprint number, deriving it the same way. Reversible and idempotent.

**Rows that legitimately stay null:** deployment/runtime-error and frontend-log rows have no branch by design. They remain out of per-sprint scope, which is correct.

**Bugs branches** parse to sprint 0. Per the decision above, they are excluded from numbered-sprint assessments.

**Two related defects to fix while here:**
- The sensor's existing line prints the wrong branch column — a column that these rows never populate — so branch always renders as blank. Read the column that is actually written.
- The existing role filter matches a role label against a derived Backend/Frontend field whose value comes from checking whether the branch name contains the letter "b". Replace it with scoping by the resolved branch names, which are now known exactly.

---

## 6. Budget and noise control

**Generated files are excluded before anything is selected** — lock files, build output directories, minified bundles, vendored dependencies, images. They are reported as a one-line summary (e.g. *"14 generated files excluded, +8,200/-3,100"*) so the model knows the diff is partial but does not grade machine output.

**Remaining files are ranked by change size**, then taken until the per-track file count or character budget is reached. The budget is **per track**, so a large frontend diff cannot crowd out the backend.

**GitHub omits the patch** for very large or binary files while still reporting counts. Emit *"patch omitted by GitHub (file too large or binary)"* rather than nothing — otherwise a large delivery reads as an empty one.

**Commit messages** are capped in count and truncated in length.

---

## 7. Configuration — paste into appSettings

```json
"GitHub": {
  "AssessmentSensor": {
    "Enabled": true,
    "MaxFilesPerTrack": 20,
    "DiffCharBudgetPerTrack": 12000,
    "MaxCommitMessages": 30,
    "CommitMessageMaxChars": 200,
    "MaxPullRequestsListed": 10,
    "BaseBranch": "main",
    "FallbackBaseBranches": [ "master", "develop" ],
    "ExcludedPathPatterns": [
      "**/package-lock.json",
      "**/yarn.lock",
      "**/pnpm-lock.yaml",
      "**/*.min.js",
      "**/*.min.css",
      "**/dist/**",
      "**/build/**",
      "**/node_modules/**",
      "**/vendor/**",
      "**/bin/**",
      "**/obj/**",
      "**/*.png",
      "**/*.jpg",
      "**/*.jpeg",
      "**/*.gif",
      "**/*.svg",
      "**/*.ico",
      "**/*.pdf"
    ]
  }
}
```

`Enabled: false` is a global kill switch — the sensor then emits a short "disabled by configuration" note and makes no API calls. Given there is no caching, this is the fastest lever if usage spikes.

Defaults are chosen to match the budget the existing gap-analysis GitHub evidence already uses in production, so prompt sizes stay in a range known to work.

---

## 8. Scoring rules added to the prompt

Appended after the section, phrased neutrally so they suit any rubric (the existing gap-analysis wording is specific to that metric and must not be reused verbatim):

- A merged pull request means the work landed. An empty branch comparison after a merge is normal and is **not** evidence of missing work — score from the merged PR's diff.
- Commit count and branch history are facts, not productivity measures; force-push and rebase rewrite them.
- Where the section states the platform could not observe the repository, treat the evidence as unavailable and do not score it as absent work.
- Where the sprint window is still open, do not treat an unmerged branch as a failure to deliver.
- Generated files excluded from the diff are not part of the student's authored work.

---

## 9. Implementation steps

1. **Sprint-number fix** — write at capture, then backfill.
   *Verify:* recent code-review and PR-validation rows carry the correct sprint; backfilled rows match their branch names; branch-less rows remain null.
2. **Track resolution** — determine which repositories the student owns from role, full-stack scoping, and role index.
   *Verify:* a backend student resolves one track, a full-stack student two, a Product Manager none.
3. **Evidence builder** — the resolution order in §3, producing metadata and diff per track.
   *Verify against a merged sprint first* — that is the case naive implementations get wrong.
4. **Rendering** — combined summary, per-track sub-blocks, CI sub-block, not-applicable line, budget and noise filtering.
5. **Wire into the engine's context builder** under the existing toggle, plus the scoring rules.
6. **Configuration** — all limits read from appSettings with the defaults above.

**Verification method throughout:** the engine's test mode returns the fully built prompts without calling the LLM. The acceptance criterion is concrete:

> For a student whose sprint PR is already merged, the returned prompt contains a non-empty per-track block with real patch text, a commit count greater than zero, and `merged=yes`.

Secondary checks: a student with no branch produces the "nothing delivered" wording, not an empty section; a Product Manager produces the not-applicable line; a board with a missing repository URL produces "could not observe".

---

## 10. Known limitations (accepted)

- **No caching.** The sensor runs per metric, so a student with several code-aware metrics refetches the same evidence each time. Controlled by configuration and by disabling the sensor on metrics that don't need code.
- **No secrets redaction.** Raw patches may contain hardcoded credentials and are sent to the model as-is. Explicitly accepted.
- **Force-push and rebase** make commit counts and dates unreliable as effort measures.
- **Commits pushed by a mentor** onto a student's branch are attributed to the student.
- **Late commits** on an old sprint branch count toward that sprint (branch scoping is by name, not date), but are flagged.
- **Bugs-branch work** is invisible to numbered-sprint assessments.

---

## 11. Deferred

- Sensor evidence caching and snapshotting — see `Assessment_Sensor_Caching_Plan.md`.
- Whether Bugs-branch work should count toward a sprint.
- Feeding stored code-review feedback into the assessment as a narrative layer over this factual base.
