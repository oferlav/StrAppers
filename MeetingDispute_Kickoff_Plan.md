# Kickoff Meeting Dispute — Planning Doc

Status: **Planning only — not implemented.** Source: `temp_meetingDispute_plan.txt`, refined against the current codebase and a Q&A round with the product owner (2026-07-24).

## 1. Goal

Force every member of a newly-created squad to explicitly agree on a first ("kickoff") meeting time within 3 days of board creation. If they can't agree in time, the squad is automatically dissolved and its members sent back to project selection.

## 2. State machine

`ProjectBoards.KickoffState` (nullable int):

| Value | Meaning | Band color |
|---|---|---|
| `NULL` | Board predates this feature — not part of the new flow at all. Behaves exactly as today. | n/a (normal `MeetingStrip`) |
| `0` | New board, nobody has proposed a kickoff time yet. | Yellow |
| `1` | Someone proposed a time; waiting on the rest of the squad to approve or counter-propose. | Orange |
| `2` | Everyone approved. Terminal state — real invite sent, band disappears, normal `MeetingStrip` takes over. | n/a (normal `MeetingStrip`) |

Decided: **there is no state 3.** The plan's `KickoffState < 3` checks become `KickoffState < 2`, and the reset job's guard is `KickoffState IS NOT NULL AND KickoffState < 2`.

```
        suggest(date)                 all approve
  0 ────────────────────► 1 ────────────────────────► 2  (terminal)
         │      ▲          │
         │      └──────────┘
         │   suggest(new date) — resets everyone
         │   else this student's ApprovedKickoff, keeps proposer's
         │
         │ 3 days pass, still <2
         ▼
   squad reset, ProjectBoards.IsStale=true
```

## 3. Current behavior this replaces

- Board creation (`BoardsController.cs:3193-3210`) currently sets `NextMeetingTime = nextMeetingTime` where `nextMeetingTime = kickoffUtc` (computed at line 3140 as "first day of next week, 10:00 local" or a day-based equivalent). **This still runs today** — there's a stale comment nearby (lines 3361-3365) claiming `NextMeetingTime` "now stays null," but the actual field assignment at line 3207 was never changed to match; only the separate block that calls the Teams/SMTP invite endpoint and sends the email was disabled, via `const bool autoScheduleKickoffMeetingOnBoardCreation = false;` (line 3366). Net effect today: every new board gets a computed `NextMeetingTime` immediately, with no real meeting URL and no invite email — `MeetingStrip` shows a date but a disabled "Go to Meeting" button.
- **Important**: `kickoffUtc` is also the anchor for sprint due-date math (`ComputeBoardDueDateUtc`, `GetSprintDueDateUtcForDays`, etc.) throughout board creation. That computation must **not** be touched — only the `NextMeetingTime = nextMeetingTime` assignment at line 3207 changes.
- `.jsx` band precedent: `MeetingStrip.jsx` (58px white strip, "Next Meeting" + Go to Meeting / Schedule a Meeting buttons) is the live component, rendered at `BoardRoom.jsx:2280`. There is a `MeetingCountdown.jsx` file in the same folder that looks like a ready-made countdown band — **it is dead code, never imported anywhere** — do not reuse it; build the new band fresh, styled to match `MeetingStrip`'s look (58px strip, same color/typography language) rather than that unused component.
- The "real invitation" (Teams meeting + `.ics` attachment via SMTP) is the same code block currently disabled at board creation (`BoardsController.cs:3358-3467`), which POSTs to `/api/Teams/use/create-meeting-smtp-for-board-auth`. This is the flow to reuse/re-trigger once `KickoffState` reaches 2 — not the separate Google Meet path in `StudentMeetingsController.cs`.

## 4. Data model changes

New columns — no EF migration will be generated. Raw SQL for you to run manually:

```sql
ALTER TABLE "ProjectBoards" ADD COLUMN "KickoffState" integer NULL;
ALTER TABLE "ProjectBoards" ADD COLUMN "LastDateStudentId" integer NULL;
ALTER TABLE "ProjectBoards" ADD CONSTRAINT "FK_ProjectBoards_Students_LastDateStudentId"
    FOREIGN KEY ("LastDateStudentId") REFERENCES "Students"("Id");
ALTER TABLE "ProjectBoards" ADD COLUMN "SuggestedKickoffDate" timestamp with time zone NULL;
ALTER TABLE "ProjectBoards" ADD COLUMN "IsStale" boolean NOT NULL DEFAULT false;

ALTER TABLE "Students" ADD COLUMN "ApprovedKickoff" boolean NOT NULL DEFAULT false;

-- Optional but recommended: speeds up the periodic reset-job scan
CREATE INDEX "IX_ProjectBoards_KickoffState_CreatedAt" ON "ProjectBoards" ("CreatedAt")
    WHERE "KickoffState" IS NOT NULL AND "KickoffState" < 2;
```

**Confirmed: no backfill.** `KickoffState`, `LastDateStudentId`, `SuggestedKickoffDate` are left `NULL` on every existing row and are never written by any migration/backfill script — existing boards are simply not part of the new flow, forever (their `KickoffState` stays `NULL`, which both the reset job and the frontend band treat as "legacy, do nothing new here"). `IsStale` (default `false`) and `ApprovedKickoff` (default `false`) get a blanket default because it's inert for rows that never enter the new flow.

Corresponding EF model additions (`ProjectBoard.cs`, `Student.cs`) still need C# properties even though no migration is generated — mark them nullable to match, and the app will treat the DB and model as already in sync once the SQL above has been run.

## 5. Backend design

### 5.1 Board creation (`BoardsController.CreateBoard`, ~line 3207)
- Change `NextMeetingTime = nextMeetingTime` → `NextMeetingTime = null`.
- Add `KickoffState = 0` on the new `ProjectBoard` object.
- `IsStale` defaults to `false` — no change needed.
- Leave the disabled Teams-invite block (3358-3467) as dead code for now, or better: extract it into a shared private method (e.g. `CreateTeamsMeetingForBoardAsync(boardId, title, dateTimeUtc, attendeeEmails)`) so the same ~110 lines can be called both here (still disabled) and from the new approve endpoint (§5.3) instead of duplicating it.

### 5.2 `POST /api/Boards/{boardId}/kickoff/suggest`
Body: `{ studentId, suggestedDateTimeUtc }`. Used both for the very first proposal (state 0→1) and any counter-proposal (state 1→1) — same endpoint, same logic.

- Reject (400) if `KickoffState` is `NULL` (legacy board) or `>= 2` (already resolved) or `IsStale`.
- Reject (403) if `studentId` isn't a member of this board (`Students.BoardId == boardId`).
- Set `SuggestedKickoffDate`, `LastDateStudentId = studentId`, `KickoffState = 1`.
- Set this student's `ApprovedKickoff = true`; set every *other* squad member's `ApprovedKickoff = false`.
- Email (via `ISmtpEmailService.SendPlainEmailAsync`, `SmtpEmailService.cs:17`):
  - To every other squad member: notify + link to approve/counter-propose.
  - To the proposer: confirmation.

### 5.3 `POST /api/Boards/{boardId}/kickoff/approve`
Body: `{ studentId }`.

- Reject under the same conditions as §5.2.
- Set this student's `ApprovedKickoff = true`.
- Inside a transaction (row-lock the `ProjectBoards` row, e.g. `SELECT ... FOR UPDATE`, to avoid a race with a concurrent approve or with the reset job), check: does every student with `BoardId == boardId` now have `ApprovedKickoff = true`?
  - If yes: set `KickoffState = 2`, `NextMeetingTime = SuggestedKickoffDate`, then call the shared Teams/SMTP invite method from §5.1 using `SuggestedKickoffDate` as the meeting time and the squad's emails as attendees — this is "the real invitation... like the one that is currently sent."
  - If no: just persist the approval; band/message stays in state 1.

### 5.4 3-day reset job (`StudentTeamBuilderService/Worker.cs`)
New method modeled directly on `ExpireOldPendingAsync` (`Worker.cs:348-374`), called from the same loop in `ExecuteAsync` (`Worker.cs:44-64`) alongside it — no new infrastructure, same 5-minute poll interval (`Worker:IntervalMinutes`).

```sql
WITH stale_boards AS (
  UPDATE "ProjectBoards"
  SET "IsStale" = true
  WHERE "KickoffState" IS NOT NULL
    AND "KickoffState" < 2
    AND "IsStale" = false
    AND "CreatedAt" < NOW() - INTERVAL '3 days'
  RETURNING "BoardId"
)
UPDATE "Students" s
SET "Status" = 0,
    "ProjectId" = NULL,
    "BoardId" = NULL,
    "InstitutePriority1" = NULL,
    "InstitutePriority2" = NULL,
    "InstitutePriority3" = NULL,
    "InstitutePriority4" = NULL,
    "ApprovedKickoff" = false,
    "UpdatedAt" = NOW()
FROM stale_boards
WHERE s."BoardId" = stale_boards."BoardId"
RETURNING s."Id", s."Email";
```

This is a single atomic statement (data-modifying CTE) — it can't race with an in-flight approve, because the `IsStale = false` guard combined with row locking means only one of "reset" or "approve" wins for a given board. After it runs, iterate the returned student emails and send the reset-notice email (§5.5). Decided: the 3-day window is **fixed from `ProjectBoards.CreatedAt`**, not extended by counter-proposals.

### 5.5 Email copy (draft — please review/edit before implementation)

| Trigger | To | Subject | Body |
|---|---|---|---|
| Suggest (new/counter) | Other squad members | "Approve your squad's kickoff time" | "{Proposer} suggested {date/time} for your squad's kickoff meeting. Log in to Skill-in to approve it, or suggest a different time if you can't make it." |
| Suggest (new/counter) | Proposer | "Kickoff time sent to your squad" | "Thanks for suggesting a kickoff time. We'll let you know as soon as everyone approves, or if someone needs a different time." |
| Auto-reset | All squad members | "Your squad needs to restart project selection" | "Your squad wasn't able to agree on a kickoff meeting time within 3 days, so it's been reset. Log back in to Skill-in and choose your projects again." |

## 6. Frontend design

New component, e.g. `skill-in-Frontend/src/components/boards/KickoffBand.jsx`, built from scratch, sized/styled like `MeetingStrip.jsx` (58px strip). Rendered at `BoardRoom.jsx:2280` **instead of** `MeetingStrip` whenever `board.kickoffState !== null && board.kickoffState < 2`; falls through to the existing `MeetingStrip` otherwise (covers both legacy boards and resolved ones).

Props: `kickoffState`, `suggestedKickoffDate`, `lastDateStudentName`, `boardCreatedAt` (for the deadline = `createdAt + 3 days`), `currentStudentApproved`, `onSuggest`, `onApprove`.

Copy (draft):

- **State 0 (yellow):** "No kickoff meeting yet — set one to get started." + button "Set Meeting Time" + d/h/m/s countdown to the 3-day deadline.
- **State 1 (orange):** "{Proposer} suggested {date, time} — waiting on your approval." + buttons "Approve" / "Suggest a Different Time" + same countdown. (Hide the login-time message/buttons for the proposer themself, since they're auto-approved.)

For the date-picker UI on "Set Meeting Time" / "Suggest a Different Time," check whether `KickoffConfirmationModal.jsx` (`components/projects/`) already fits (it's a date+time picker with `onConfirm`) before building a new modal.

## 7. Concurrency / edge cases

- Approve vs. reset race: handled by §5.4's atomicity — whichever transaction commits first wins; the loser's `WHERE` clause no longer matches.
- Two students approving simultaneously: handled by row-locking in §5.3.
- A student who never logs in during the 3 days: no special handling needed — the reset job catches this the same as active disagreement.
- `ApprovedKickoff` is cleared to `false` for everyone (including the resetting board) as part of §5.4's `UPDATE`.

## 8. Assumptions to confirm during implementation (not blocking this doc)

- "Squad" = every `Students` row with `BoardId == board.BoardId`. (`ProjectBoards.AdminId` and QuestMode are both unrelated/legacy concepts — not in scope here.)
- New endpoint auth/authorization should follow whatever pattern existing student-initiated `BoardsController` actions already use (e.g. checkout) — not yet inspected in detail.
- Whether to extract the Teams/SMTP invite block into a shared method (§5.1) vs. duplicate it for the approve endpoint — recommend extracting, but flagging since it touches code outside the literal feature scope.
- No feature flag — ships straight to production, safety comes from the test plan in §9.

## 9. Testing plan

Existing test projects: `strAppersBackend.Tests` (xUnit, e.g. `BoardCreationSprintDaysTests.cs`, `BoardStatesBranchFilterTests.cs`) and frontend `*.test.js` files colocated with components (e.g. `BoardRoom.progress.test.js`, Vitest). Follow these conventions.

**Backend unit tests** (new file, e.g. `KickoffDisputeStateTests.cs`):
- Board creation sets `KickoffState = 0`, `NextMeetingTime = null`.
- `suggest`: 0→1 transition; 1→1 counter-proposal correctly flips `ApprovedKickoff` (true for proposer, false for everyone else); rejects on `NULL`/`>=2`/stale board; rejects non-member student.
- `approve`: single approval doesn't flip state early; last approval flips `KickoffState` to 2, sets `NextMeetingTime`, triggers invite; verify invite call uses `SuggestedKickoffDate` not `kickoffUtc`.
- Reset job: board past 3 days with `KickoffState < 2` gets `IsStale = true` and all its students reset (`Status=0`, `ProjectId=NULL`, `BoardId=NULL`, `InstitutePriority1-4=NULL`, `ApprovedKickoff=false`); board within 3 days is untouched; board with `KickoffState = 2` is untouched; board with `KickoffState = NULL` (legacy) is untouched; already-`IsStale` board isn't reprocessed/re-emailed.
- Race simulation: concurrent approve + reset-job run against the same board — exactly one outcome wins, no partial/inconsistent state.
- Single-member-squad edge case (if that's even reachable given `KickoffConfig.MinimumStudents`).

**Backend integration tests**: hit the two new endpoints through the test host, asserting HTTP status codes for each rejection case in §5.2/5.3, and asserting emails were dispatched (mock `ISmtpEmailService`) with the right recipients on each transition.

**Frontend tests** (colocated `.test.js`):
- `KickoffBand` renders correct copy/buttons/colors for state 0 and state 1.
- Countdown ticks down correctly and stops/hides appropriately once the deadline or `KickoffState=2` is reached.
- `BoardRoom` renders `KickoffBand` vs `MeetingStrip` correctly based on `kickoffState` (null / 0 / 1 / 2).
- Approve/Suggest buttons call the right endpoints with the right payload; proposer sees state 1 without the approve prompt for their own suggestion.

**Manual QA checklist before prod deploy:**
1. Create a fresh squad in staging, confirm yellow band + countdown appears, `MeetingStrip` does not.
2. One member proposes a time — confirm orange band for others, emails land (check staging mailbox), proposer sees confirmation email + no approve prompt for themself.
3. Another member counter-proposes — confirm the first proposer's approval is cleared, new proposer is auto-approved, emails go out again.
4. All members approve — confirm `MeetingStrip` reappears with the agreed time, real Teams/.ics invite email arrives, no more login banner.
5. Let a test squad's 3-day window lapse (or fast-forward `CreatedAt` in staging DB) — confirm reset email arrives, students land back in "available" status, `IsStale=true` on the board, board itself untouched otherwise (not deleted).
6. Confirm a pre-existing (already-in-production) board is completely unaffected — no band, `MeetingStrip` behaves exactly as before.
7. Confirm sprint due-date math is unaffected by the `NextMeetingTime = null` change (spot-check a newly created board's sprint list due dates against `kickoffUtc` as before).
8. Load-test the reset job query against production-scale `ProjectBoards`/`Students` row counts to confirm the partial index keeps it cheap at 5-minute intervals.

Run the full existing `strAppersBackend.Tests` and frontend suites after implementation to catch regressions elsewhere (especially anything touching `BoardsController.CreateBoard`, `MeetingStrip`, `BoardRoom.jsx`).
