Generate a Skillset from the Skill-in platform's own student data and submit it to the Skill-in backend.

$ARGUMENTS

This command uses the platform connector — no external Jira/Slack/GitHub credentials needed.
Data is read directly from the Skill-in backend: Trello sprints, AI chat history, GitHub state, and project modules.

## Steps

### 1. Load config
Call `skill-in-platform.read_platform_config`.
Show: company identifier, role, platform URL, student IDs, sprint numbers to assess.

### 2. For each student, fetch all 4 data buckets in parallel
For each student ID in the config, call all fetch tools simultaneously:
- `skill-in-platform.fetch_tasks(studentId, sprintNumber)`          → tasks_and_sla
- `skill-in-platform.fetch_communication(studentId, sprintNumber)`  → communication
- `skill-in-platform.fetch_artifacts(studentId, sprintNumber)`      → artifacts
- `skill-in-platform.fetch_design_content(studentId)`               → design_content

Show a one-line progress update per student as data comes in.

### 3. Score each dimension

**Tasks & SLA** (from Trello data):
- Primary signal: `summary.avgChecklistCompletion` (0–100 directly)
- Adjust ±10 for card count relative to typical sprint load
- Score = weighted checklist completion rate

**Communication** (from chat data):
- Mentor engagement: `summary.mentorEngagement` messages (benchmark: 5+ = good)
- Customer engagement: `summary.customerEngagement` messages (benchmark: 3+ = good)
- Group participation: `summary.groupParticipation` lines (benchmark: 10+ = active)
- Score = composite of all three channels, each 0–100

**Artifacts** (from GitHub data):
- Active branch: +30 points
- PR opened: +30 points
- PR merged: +40 points
- If no branch at all: 10 points
- Score = sum of above

**Design Content** (from ProjectModules):
- `summary.modulesWithContent / summary.totalModules × 100`
- Adjust for word count depth (shallow docs < 100 words penalised)
- Score = content coverage rate

Aggregate each dimension score **across all students using the median**.

### 4. Ask for human rating anchors
Present the scored dimensions table:

| Dimension | AI Score | Your Rating (0–100) |
|-----------|----------|---------------------|

Ask: "For each dimension, enter your own score — how well do you think a strong candidate should perform on this?"
Accept their ratings.

### 5. Ask for weights
Ask: "Rate the relative importance of each dimension for this role (1 = normal, 2 = twice as important, 0.5 = half)."

### 6. Submit to Skill-in
Call `skill-in-assessment.submit_skillset` with:
- `companyIdentifier`, `roleName` from config
- `assessedPeriod`: from sprint start (use today minus 90 days) to today
- `dataSourcesUsed`: ["trello", "skill-in-chat", "github", "project-modules"]
- `employeeSampleSize`: number of students assessed
- `skills`: one entry per dimension with aggregatedScore, humanRatingAnchor, weight, confidence, rationale

Show the returned `skillsetId`.

### 7. Summary
Display:
- Skillset ID
- Role and company
- Skills table with AI scores, human anchors, weights
- Data sources used
- "Skill-in will now match this Skillset against all active students. You will receive a Skills Hook alert when a student meets your threshold."
