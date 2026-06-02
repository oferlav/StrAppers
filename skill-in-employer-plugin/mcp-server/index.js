import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { CallToolRequestSchema, ListToolsRequestSchema } from '@modelcontextprotocol/sdk/types.js';

const BASE_URL = (process.env.SKILLIN_API_URL ?? 'https://skill-in-backend-dvdmgbe7fmhmg4hp.eastus2-01.azurewebsites.net').replace(/\/$/, '');

const server = new Server(
  { name: 'skill-in-assessment', version: '1.0.0' },
  { capabilities: { tools: {} } }
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [
    {
      name: 'get_student_board',
      description: "Look up a student's board information by student ID. Returns boardId (Trello), student name, role, project name, organisation, and start date.",
      inputSchema: {
        type: 'object',
        properties: {
          studentId: { type: 'number', description: 'Numeric student ID' },
        },
        required: ['studentId'],
      },
    },
    {
      name: 'run_gap_analysis',
      description: 'Run the Gap Analysis assessment agent for a student sprint. Compares sprint requirements to delivered artifacts (GitHub commits/PRs, Figma, user stories, CRM). Returns a markdown narrative with category scores (0–100 per dimension).',
      inputSchema: {
        type: 'object',
        properties: {
          boardId: { type: 'string', description: 'Trello board ID for the student project' },
          studentId: { type: 'number', description: 'Numeric student ID' },
          sprintNumber: { type: 'number', description: 'Sprint number to analyze (1–8; use 0 for bugs sprint)' },
        },
        required: ['boardId', 'studentId', 'sprintNumber'],
      },
    },
    {
      name: 'submit_skillset',
      description: 'Send a completed Skillset to the Skill-in platform. Only aggregated, anonymised scores are submitted — no raw data, no employee names, no credentials. Returns a skillsetId for tracking.',
      inputSchema: {
        type: 'object',
        properties: {
          companyIdentifier: { type: 'string', description: 'Anonymised company slug, e.g. acme-corp' },
          roleName: { type: 'string', description: 'Role this Skillset covers, e.g. Senior Backend Engineer' },
          assessedPeriod: {
            type: 'object',
            properties: {
              from: { type: 'string', description: 'ISO date, e.g. 2025-01-01' },
              to:   { type: 'string', description: 'ISO date, e.g. 2025-06-01' },
            },
            required: ['from', 'to'],
          },
          dataSourcesUsed: { type: 'array', items: { type: 'string' }, description: 'e.g. ["jira","slack","github"]' },
          employeeSampleSize: { type: 'number', description: 'Number of employees assessed (no names/IDs)' },
          skills: {
            type: 'array',
            description: 'One entry per assessed metric dimension',
            items: {
              type: 'object',
              properties: {
                name:               { type: 'string' },
                metricCategory:     { type: 'string', description: 'tasks_and_sla | communication | artifacts | design_content' },
                dataSources:        { type: 'array', items: { type: 'string' } },
                aggregatedScore:    { type: 'number', description: '0–100' },
                humanRatingAnchor:  { type: 'number', description: 'Employer manual score for calibration' },
                weight:             { type: 'number', description: 'Relative importance, default 1.0' },
                confidence:         { type: 'number', description: '0–1 agent confidence level' },
                rationale:          { type: 'string' },
              },
              required: ['name', 'metricCategory', 'dataSources', 'aggregatedScore', 'weight', 'confidence'],
            },
          },
          metadata: {
            type: 'object',
            properties: {
              pluginVersion:  { type: 'string' },
              generatedAt:    { type: 'string' },
              connectorType:  { type: 'string' },
            },
          },
        },
        required: ['companyIdentifier', 'roleName', 'skills'],
      },
    },
  ],
}));

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  try {
    if (name === 'get_student_board') {
      const resp = await fetch(`${BASE_URL}/api/Boards/use/student/${args.studentId}`);
      const text = await resp.text();
      if (!resp.ok) {
        return { content: [{ type: 'text', text: `API error ${resp.status}: ${text}` }], isError: true };
      }

      const data = JSON.parse(text);
      // Find the student's own entry in teamMembers to get their name + role
      const me = (data.teamMembers ?? []).find(m => m.id === args.studentId);
      const studentName = me ? `${me.firstName ?? ''} ${me.lastName ?? ''}`.trim() : `Student #${args.studentId}`;
      const roleName = me?.roles?.[0]?.roleName ?? 'Unknown role';

      return {
        content: [{
          type: 'text',
          text: JSON.stringify({
            boardId: data.boardId,
            studentName,
            roleName,
            projectName: data.projectName,
            organisationName: data.organizationName,
            startDate: data.startDate,
            weeksLeft: data.weeksLeft,
          }, null, 2),
        }],
      };
    }

    if (name === 'run_gap_analysis') {
      const resp = await fetch(`${BASE_URL}/api/Metrics/use/GapAnalysis`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          boardId: args.boardId,
          studentId: Number(args.studentId),
          sprintNumber: Number(args.sprintNumber),
        }),
      });
      const text = await resp.text();
      if (!resp.ok) {
        return { content: [{ type: 'text', text: `API error ${resp.status}: ${text}` }], isError: true };
      }

      const data = JSON.parse(text);
      return {
        content: [{
          type: 'text',
          text: [
            `**Sprint ${args.sprintNumber} — Gap Analysis**`,
            `MetricId: ${data.metricId} | Tokens: ${data.inputTokens ?? '?'} in / ${data.outputTokens ?? '?'} out`,
            `Chart: ${data.graphBase64 ? 'available — open http://localhost:3456 to view' : 'not returned'}`,
            '',
            data.reviewContent ?? '(no narrative returned)',
          ].join('\n'),
        }],
      };
    }

    if (name === 'submit_skillset') {
      const resp = await fetch(`${BASE_URL}/api/employer-skillset/use/submit`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          companyIdentifier: args.companyIdentifier,
          roleName: args.roleName,
          assessedPeriod: args.assessedPeriod,
          dataSourcesUsed: args.dataSourcesUsed ?? [],
          employeeSampleSize: Number(args.employeeSampleSize ?? 0),
          skills: args.skills,
          metadata: {
            ...(args.metadata ?? {}),
            generatedAt: args.metadata?.generatedAt ?? new Date().toISOString(),
            pluginVersion: args.metadata?.pluginVersion ?? '1.0.0',
          },
        }),
      });
      const text = await resp.text();
      if (!resp.ok) {
        return { content: [{ type: 'text', text: `API error ${resp.status}: ${text}` }], isError: true };
      }
      const data = JSON.parse(text);
      return {
        content: [{
          type: 'text',
          text: [
            `✓ Skillset submitted successfully`,
            `Skillset ID: ${data.skillsetId}`,
            `${data.message}`,
            '',
            `Summary: ${data.summary?.skillCount} skills, weighted score ${data.summary?.weightedSkillsetScore}, ${data.summary?.dataSourcesUsed?.join(', ')}`,
          ].join('\n'),
        }],
      };
    }

    return { content: [{ type: 'text', text: `Unknown tool: ${name}` }], isError: true };
  } catch (err) {
    return { content: [{ type: 'text', text: `Error: ${err.message}` }], isError: true };
  }
});

const transport = new StdioServerTransport();
await server.connect(transport);
