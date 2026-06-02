# Skill-in Platform Connector Plugin

This plugin lets you assess Skill-in students using the platform's own data — no external tools (Jira/Slack/GitHub) required. It reads directly from the Skill-in backend: Trello sprint data, AI chat history, GitHub artifacts, and project modules.

## Available MCP Servers

### skill-in-assessment
Assessment and submission tools that talk to the Skill-in backend.
- `get_student_board(studentId)` — look up a student's board, project, role
- `run_gap_analysis(boardId, studentId, sprintNumber)` — run full sprint gap analysis
- `submit_skillset(...)` — submit a completed Skillset to the platform

### skill-in-platform
Platform data connector — reads the 4 normalized buckets directly from the Skill-in backend.
- `read_platform_config()` — load company + student config
- `fetch_student_info(studentId)` — board ID, project ID, role, team members
- `fetch_tasks(studentId, sprintNumber)` — Trello sprint cards, checklists, completion (tasks_and_sla)
- `fetch_communication(studentId, sprintNumber)` — mentor chat, customer chat, group chat (communication)
- `fetch_artifacts(studentId, sprintNumber)` — GitHub branch/PR/commit status (artifacts)
- `fetch_design_content(studentId)` — project modules / PRD specs (design_content)

## Slash Commands

- `/assess` — run a gap analysis on a student sprint, collect your rating + weight, submit Skillset
- `/generate-skillset` — full multi-student Skillset generation using platform data

## Config

Edit `connector-config.json` to set your company identifier, role, and the student IDs to assess.

## Backend

All calls go to: https://skill-in-backend-dvdmgbe7fmhmg4hp.eastus2-01.azurewebsites.net
