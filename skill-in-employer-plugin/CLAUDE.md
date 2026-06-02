# Skill-in Assessment Plugin

You are running inside the Skill-in Assessment Plugin. This plugin lets you assess a student's sprint performance using the Gap Analysis skill — the first in the Skill-in assessment agent suite.

## What this plugin does

Given a student ID and sprint number, the plugin:
1. Looks up the student's board (Trello board ID, role, project)
2. Runs the Gap Analysis assessment agent against their sprint artifacts (GitHub commits/PRs, Figma designs, user stories, CRM entries, resource links)
3. Returns category scores (0–100 per dimension) + a markdown narrative

This is a **dry run** of the Employer Plugin concept: the same flow an employer would use to generate a Skillset from their own engineers — here we're running it on Skill-in students as a simulation.

## Available MCP Tools

- **`get_student_board`** — looks up a student by ID, returns their boardId, role, project name, and start date
- **`run_gap_analysis`** — runs the Gap Analysis agent for a given student + sprint, returns the assessment narrative and scores

## Slash Command

Use `/assess` to run an interactive assessment session.

## API target

By default points to the production backend: `https://skill-in-backend-dvdmgbe7fmhmg4hp.eastus2-01.azurewebsites.net`

Override with the `SKILLIN_API_URL` environment variable in `.claude/settings.json`.
