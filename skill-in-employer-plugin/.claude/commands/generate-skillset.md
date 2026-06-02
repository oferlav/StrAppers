Generate a Skillset from the employer's connected data sources and submit it to the Skill-in platform.

$ARGUMENTS

## Steps

### 1. Load config
Call `skill-in-connector.read_connector_config` to load the employer config.
- Show the user: company name, role being assessed, date range, number of employees, which connectors are enabled.
- If no connectors are enabled, tell the user to edit `connector-config.json` and enable at least one source.

### 2. Collect data per employee
For each employee listed in the config, call the enabled fetch tools in parallel if possible:
- If Jira enabled → `skill-in-connector.fetch_tasks(employeeEmail)`
- If Slack enabled → `skill-in-connector.fetch_communication(employeeEmail)`
- If GitHub enabled → `skill-in-connector.fetch_artifacts(employeeEmail)`

Show a one-line progress update per employee as data comes in.

### 3. Assess each metric dimension
For each data bucket that was fetched, run an assessment:

**Tasks & SLA** (from Jira data):
- Completion rate: completed / total tasks × 100
- On-time delivery: tasks delivered on or before due date
- Velocity trend: story points per period
- Score = weighted average of these sub-metrics

**Communication** (from Slack data):
- Message volume relative to team average
- Thread engagement ratio (thread replies / total messages)
- Channel diversity (breadth of participation)
- Average message depth (words per message)
- Score = weighted composite

**Artifacts** (from GitHub data):
- Commit frequency and consistency
- PR merge rate (merged PRs / opened PRs)
- Code review participation (avg review comments on own PRs)
- PR quality (time to merge, review cycles)
- Score = weighted composite

Aggregate each dimension score across all employees: use the median to reduce outlier distortion.

### 4. Ask for human rating anchors
Present the scored dimensions table and ask the employer to review:
"For each dimension, enter your own score (0–100) for how important this is and how well your top performers score on it. This anchors the Skillset to YOUR standard."

Display a simple table and accept their ratings.

### 5. Ask for weights
Ask: "Are any of these dimensions more important than others for this role? Rate their relative importance (1 = normal, 2 = twice as important, 0.5 = half)."

### 6. Submit to Skill-in
Call `skill-in-assessment.submit_skillset` with the full payload:
- companyIdentifier, roleName, assessedPeriod, dataSourcesUsed, employeeSampleSize
- skills array: one entry per dimension with aggregatedScore, humanRatingAnchor, weight, confidence, rationale

Show the returned `skillsetId` and confirmation message.

### 7. Summary
Display a clean summary:
- Skillset ID (for reference)
- Role assessed
- Skills and their weighted scores
- Which data sources contributed
- Next step: "Skill-in will now match this Skillset against the student pipeline. You will receive a Skills Hook alert when a candidate meets your threshold."
