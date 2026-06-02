Run a Gap Analysis assessment for a Skill-in student, collect employer ratings, and submit a Skillset entry to the Skill-in platform.

$ARGUMENTS

Steps:
1. If a student ID was not supplied in the arguments, ask the user for one.
2. Call the `get_student_board` MCP tool with that student ID.
   - If the student has no board assigned, tell the user and stop.
   - Otherwise display: student name, role, project name, board ID, start date.
3. Ask the user which sprint number to assess (1–8). Remind them that sprint 0 = bugs sprint.
4. Call the `run_gap_analysis` MCP tool with the boardId, studentId, and sprint number.
5. Present the results clearly:
   - **Sprint assessed** and **student / role** as a header
   - The full **narrative** from the assessment (it is markdown — render it as-is)
   - Compute an **overall AI score** by averaging the category scores returned (0–100). Show it prominently.
   - A note that the bar chart is visible in the workflow form at http://localhost:3456
   - Token and model info at the bottom (collapsed / small)

6. **Collect employer rating and weight** — ask the user two questions (can be on the same prompt):
   - "How would you rate this student overall? (1–5, where 5 = exceptional)"
   - "How important is sprint delivery quality for the role you're hiring for? (1–5, where 1 = nice-to-have, 5 = critical)"
   - Map both answers to a 0–100 scale: multiply by 20. So a rating of 4 → 80, weight of 5 → 100 (store as 1.0–5.0 for weight, keep as 0–100 for humanRatingAnchor).

7. **Load company context** — call `skill-in-connector.read_connector_config` to get companyIdentifier and roleName.
   - If the connector config is not found or connectors are not configured, ask the user to enter companyIdentifier and roleName inline.

8. **Submit to Skill-in** — call `skill-in-assessment.submit_skillset` with:
   - `companyIdentifier` and `roleName` from step 7
   - `assessedPeriod`: use the student's startDate as `from` and today's date as `to`
   - `dataSourcesUsed`: ["skill-in-gap-analysis"]
   - `employeeSampleSize`: 1
   - `skills`: one entry:
     - `name`: "Sprint Gap Analysis"
     - `metricCategory`: "artifacts"
     - `dataSources`: ["trello", "github", "figma"]
     - `aggregatedScore`: the AI overall score from step 5
     - `humanRatingAnchor`: employer rating mapped to 0–100 from step 6
     - `weight`: employer importance rating (1.0–5.0) from step 6
     - `confidence`: 0.85
     - `rationale`: one sentence summary of the gap analysis outcome
   - `metadata`: `{ pluginVersion: "1.0.0", connectorType: "skill-in-gap-analysis" }`

   Show the returned `skillsetId` and confirmation message.

9. After submission, ask: "Would you like to assess another sprint for this student, or a different student?"
