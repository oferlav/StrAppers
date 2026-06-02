# Skill-in Assessment Plugin — Dry Run

A portable Claude Code plugin that runs the **Gap Analysis** assessment agent against a Skill-in student's sprint.

This is a **dry run** of the Employer Plugin concept from the Skill-in Employment Plan: the same flow an employer would use to generate a Skillset from their own engineers, here applied to Skill-in students as a simulation.

---

## What's inside

```
skill-in-employer-plugin/
├── CLAUDE.md                      ← plugin context loaded into Claude Code
├── .claude/
│   ├── settings.json              ← MCP server config (auto-loaded by Claude Code)
│   └── commands/
│       └── assess.md              ← /assess slash command
├── mcp-server/
│   ├── package.json
│   └── index.js                   ← MCP server: get_student_board + run_gap_analysis tools
└── workflow/
    └── index.html                 ← standalone web form (no build step)
```

---

## Setup

### 1. Install MCP server dependencies

```bash
cd mcp-server
npm install
cd ..
```

### 2. Open this directory in Claude Code

```bash
claude C:\ClaudeCode\StrAppers\skill-in-employer-plugin
```

Claude Code will automatically load `.claude/settings.json` and start the MCP server.
The `get_student_board` and `run_gap_analysis` tools will be available immediately.

### 3. (Optional) Open the web form

Serve the workflow form from localhost so CORS is satisfied:

```bash
# Option A — Python (no install needed)
python -m http.server 3456 --directory workflow

# Option B — Node
npx serve workflow -p 3456
```

Then open: **http://localhost:3456**

---

## Usage

### Via Claude Code (slash command)

```
/assess
```

Claude will ask for a student ID, look up their board, ask for a sprint number, run the assessment, and display the results.

You can also pass the student ID directly:

```
/assess 42
```

### Via the web form

1. Enter a **Student ID** → click **Look Up**
2. Confirm the student name, role, board
3. Enter a **Sprint Number** (1–8) → click **Run Assessment**
4. Results: category scores + narrative + bar chart

---

## MCP Tools

| Tool | Description |
|------|-------------|
| `get_student_board` | Looks up board ID, role, project name, and start date for a student |
| `run_gap_analysis` | Runs the Gap Analysis agent (Sprint requirements vs. delivered artifacts) |

---

## Configuration

Edit `.claude/settings.json` to point at a different backend:

```json
{
  "mcpServers": {
    "skill-in-assessment": {
      "command": "node",
      "args": ["./mcp-server/index.js"],
      "env": {
        "SKILLIN_API_URL": "http://localhost:5186"
      }
    }
  }
}
```

---

## What the Gap Analysis does

Given a student + sprint number, the agent:

1. Pulls sprint requirements from Trello (role card, module description, PM user story)
2. Pulls student artifacts (GitHub diffs/PRs, Figma files, resource links, CRM/stakeholder rows, customer chat)
3. Sends both to GPT-4o-mini with a structured prompt
4. Returns category scores (0–100 per dimension) + a markdown narrative + a bar chart PNG

---

## Next steps (not yet built)

- **Date range → sprint mapping**: accept a date range and auto-resolve which sprints it covers using the board's `startDate` and sprint length
- **Multi-sprint sweep**: run gap analysis across all sprints in a range and aggregate scores
- **Skillset generation**: compare student scores against employer human ratings to produce a match score
- **Skills Hook**: fire an alert when a student's profile exceeds an employer's threshold
