# Skill-in Employment Plan

## The Core Insight

Skill-in students already work exactly like real engineering teams — they write PRDs, do sprints, commit code, communicate, get reviewed. That generates 4 types of structured data: design docs, task/sprint data, communication, and code artifacts.

The observation is: **real companies' best engineers generate the exact same 4 data types** — just in Jira instead of Trello, Slack instead of a chat sidebar, GitHub the same, Confluence instead of a PRD doc.

If you run **identical AI assessment agents** on both sides against a **normalized schema**, you get mathematically comparable profiles. That's the entire foundation of the matching engine.

---

## What a Skillset Is

A Skillset is a company's AI-generated definition of excellence for a specific role — not generic, not a rubric someone wrote, but reverse-engineered from their actual top performers.

It's a collection of individual Skills, each produced by one dedicated agent measuring one metric (Communication Quality, Task Delivery, Code Review Thoroughness, etc.). Each agent pulls from whichever data sources are relevant — it's metric-first, not source-first.

The critical differentiator: **human ratings anchor it**. The employer scores each of their top performers on each metric, giving the agents a calibrated ground truth. Then they add weightings. The result is their unique hiring fingerprint — their definition of "excellent", not a generic industry standard.

---

## How the Skillset Gets Generated (The Plugin)

You deliver a Claude Code Plugin the employer installs in their own environment. It bundles MCP connectors to their existing tools (Jira, GitHub, Slack), per-metric assessment workflows, an orchestration sub-agent, and a `/generate-skillset` slash command.

They point it at 3–5 top performers + a date range, run the command, review the output, add their human ratings and weights. Done. The Skillset is built.

The privacy design is elegant: **all raw data stays inside their Claude Code environment**. The only thing that leaves is the anonymized, aggregated Skillset — sent via a write-only endpoint. They can inspect the exact JSON payload before it goes anywhere.

---

## The Matching Engine

The same agents that built the Skillset from the employer's data run on Skill-in student project data. Students are profiled on the same schema. The matching agent does a weighted comparison against the employer's Skillset. When a student crosses the employer's threshold — the **Skills Hook fires**: an automatic alert with no CVs, no searching, just "candidate found — matches your Skillset."

The employer never sees the student's underlying analysis. The student never sees the employer's Skillset breakdown. The match result is the only thing that crosses.

---

## Two Paths

**Path 1: Retroactive** — run the Skillset against the existing student database immediately. Hidden matches surface right now without any course changes.

**Path 2: Prospective (Course Shaping)** — the Skillset drives what tasks and requirements students get assigned in their course. You don't just find who fits — you grow people who will fit, to your exact standard.

Same Skillset. Two revenue streams. Maximum employer flexibility.

---

## The First Design Partner Plan

The immediate goal is to generate the **first real Skillset** with a single employer:
- They identify 3–5 top performers in one role
- Connect their tools via MCP config (Jira, GitHub, Slack)
- Install the plugin, run `/generate-skillset`
- Do 2–3 human rating sessions to anchor the scores
- That produces the first real, validated Skillset — which immediately runs against the existing student DB

This first partner is both a proof of concept and a sales asset — the output validates the entire matching thesis.

---

## Summary

Skill-in is a two-sided talent intelligence marketplace. Supply side: Skill-in students (already instrumented). Demand side: employers who define their standard via a Skillset generated from their own people. The bridge: shared schema + shared agents + a matching engine. Distribution: a Claude Code plugin that runs entirely in the employer's environment, so privacy is solved by architecture, not policy.
