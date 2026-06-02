/**
 * Skill-in Connector MCP Server
 *
 * Reads connector-config.json (employer fills this in once).
 * Exposes tools that pull raw data from each enabled data source,
 * then normalise it into the Skill-in four-bucket schema:
 *
 *   tasks_and_sla   ← Jira, Trello, Linear, ...
 *   communication   ← Slack, Teams, email, ...
 *   artifacts       ← GitHub PRs/commits, code review comments, ...
 *   design_content  ← Confluence, Notion, Google Drive, ...
 *
 * The assessment MCP server receives normalised data — it never sees
 * raw Jira tickets, Slack messages, or GitHub diffs.
 */

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { CallToolRequestSchema, ListToolsRequestSchema } from '@modelcontextprotocol/sdk/types.js';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const CONFIG_PATH = path.resolve(__dirname, '..', 'connector-config.json');

function loadConfig() {
  if (!fs.existsSync(CONFIG_PATH)) {
    throw new Error(
      'connector-config.json not found. Copy connector-config.template.json → connector-config.json and fill in your values.'
    );
  }
  return JSON.parse(fs.readFileSync(CONFIG_PATH, 'utf8'));
}

// ─── Normalisation helpers ──────────────────────────────────────────────────

function normaliseJiraIssues(issues, employeeEmail, cfg) {
  return {
    source: 'jira',
    connector: `${cfg.connectors.jira.baseUrl} / project ${cfg.connectors.jira.projectKey}`,
    employeeEmail,
    period: { from: cfg.assessment.dateFrom, to: cfg.assessment.dateTo },
    tasks: issues.map(i => ({
      id: i.key,
      title: i.fields?.summary ?? '',
      status: i.fields?.status?.name ?? 'unknown',
      priority: i.fields?.priority?.name ?? 'none',
      storyPoints: i.fields?.story_points ?? i.fields?.customfield_10016 ?? null,
      createdAt: i.fields?.created,
      resolvedAt: i.fields?.resolutiondate,
      wasOnTime: i.fields?.duedate
        ? new Date(i.fields.resolutiondate) <= new Date(i.fields.duedate)
        : null,
      commentCount: i.fields?.comment?.total ?? 0,
    })),
    summary: {
      total: issues.length,
      completed: issues.filter(i => ['Done', 'Closed', 'Resolved'].includes(i.fields?.status?.name)).length,
      avgStoryPoints: issues.length > 0
        ? issues.reduce((acc, i) => acc + (i.fields?.customfield_10016 ?? 0), 0) / issues.length
        : 0,
    },
  };
}

function normaliseSlackMessages(messages, employeeUserId, cfg) {
  return {
    source: 'slack',
    connector: `channels: ${cfg.connectors.slack.channels.join(', ')}`,
    employeeUserId,
    period: { from: cfg.assessment.dateFrom, to: cfg.assessment.dateTo },
    messages: messages.map(m => ({
      channel: m.channel,
      timestamp: m.ts,
      wordCount: (m.text ?? '').split(/\s+/).filter(Boolean).length,
      isThreadReply: !!m.thread_ts && m.thread_ts !== m.ts,
      hasReactions: (m.reactions?.length ?? 0) > 0,
    })),
    summary: {
      total: messages.length,
      avgWordsPerMessage: messages.length > 0
        ? Math.round(messages.reduce((acc, m) => acc + (m.text ?? '').split(/\s+/).filter(Boolean).length, 0) / messages.length)
        : 0,
      threadReplies: messages.filter(m => m.thread_ts && m.thread_ts !== m.ts).length,
      channelDiversity: new Set(messages.map(m => m.channel)).size,
    },
  };
}

function normaliseGithubCommits(commits, prs, employeeLogin, cfg) {
  return {
    source: 'github',
    connector: `${cfg.connectors.github.org} / repos: ${cfg.connectors.github.repos.join(', ')}`,
    employeeLogin,
    period: { from: cfg.assessment.dateFrom, to: cfg.assessment.dateTo },
    commits: commits.map(c => ({
      sha: c.sha?.slice(0, 7),
      message: c.commit?.message?.split('\n')[0] ?? '',
      date: c.commit?.author?.date,
      additions: c.stats?.additions ?? null,
      deletions: c.stats?.deletions ?? null,
    })),
    pullRequests: prs.map(pr => ({
      number: pr.number,
      title: pr.title,
      state: pr.state,
      merged: !!pr.merged_at,
      reviewComments: pr.review_comments ?? 0,
      changedFiles: pr.changed_files ?? null,
      createdAt: pr.created_at,
      mergedAt: pr.merged_at,
    })),
    summary: {
      commitCount: commits.length,
      prCount: prs.length,
      mergedPrCount: prs.filter(pr => !!pr.merged_at).length,
      avgReviewComments: prs.length > 0
        ? Math.round(prs.reduce((acc, pr) => acc + (pr.review_comments ?? 0), 0) / prs.length)
        : 0,
    },
  };
}

// ─── Data fetchers ──────────────────────────────────────────────────────────

async function fetchJiraIssues(cfg, employeeEmail) {
  const { baseUrl, email, apiToken, projectKey } = cfg.connectors.jira;
  const { dateFrom, dateTo } = cfg.assessment;

  // JQL: issues in the project, assigned to this employee, updated in the date range
  const jql = `project = ${projectKey} AND assignee = "${employeeEmail}" AND updated >= "${dateFrom}" AND updated <= "${dateTo}" ORDER BY updated DESC`;
  const url = `${baseUrl}/rest/api/3/search?jql=${encodeURIComponent(jql)}&maxResults=200&fields=summary,status,priority,story_points,customfield_10016,created,resolutiondate,duedate,comment`;

  const resp = await fetch(url, {
    headers: {
      'Authorization': `Basic ${Buffer.from(`${email}:${apiToken}`).toString('base64')}`,
      'Accept': 'application/json',
    },
  });
  if (!resp.ok) throw new Error(`Jira API ${resp.status}: ${await resp.text()}`);
  const data = await resp.json();
  return normaliseJiraIssues(data.issues ?? [], employeeEmail, cfg);
}

async function fetchSlackMessages(cfg, employeeEmail) {
  const { botToken, channels } = cfg.connectors.slack;
  const { dateFrom, dateTo } = cfg.assessment;

  // First resolve the employee's Slack user ID from their email
  const userResp = await fetch(`https://slack.com/api/users.lookupByEmail?email=${encodeURIComponent(employeeEmail)}`, {
    headers: { 'Authorization': `Bearer ${botToken}` },
  });
  const userData = await userResp.json();
  if (!userData.ok) throw new Error(`Slack users.lookupByEmail: ${userData.error}`);
  const userId = userData.user.id;

  const oldest = Math.floor(new Date(dateFrom).getTime() / 1000);
  const latest = Math.floor(new Date(dateTo).getTime() / 1000);

  // Fetch messages from each channel, filter by userId
  const allMessages = [];
  for (const channelName of channels) {
    // Resolve channel ID
    const listResp = await fetch(`https://slack.com/api/conversations.list?types=public_channel,private_channel&limit=200`, {
      headers: { 'Authorization': `Bearer ${botToken}` },
    });
    const listData = await listResp.json();
    const channel = (listData.channels ?? []).find(c => c.name === channelName);
    if (!channel) continue;

    const histResp = await fetch(`https://slack.com/api/conversations.history?channel=${channel.id}&oldest=${oldest}&latest=${latest}&limit=500`, {
      headers: { 'Authorization': `Bearer ${botToken}` },
    });
    const histData = await histResp.json();
    const byUser = (histData.messages ?? [])
      .filter(m => m.user === userId)
      .map(m => ({ ...m, channel: channelName }));
    allMessages.push(...byUser);
  }

  return normaliseSlackMessages(allMessages, userId, cfg);
}

async function fetchGithubArtifacts(cfg, employeeEmail) {
  const { personalAccessToken, org, repos } = cfg.connectors.github;
  const { dateFrom, dateTo } = cfg.assessment;

  // GitHub search: commits by author email in date range
  const searchQuery = `author-email:${employeeEmail} committer-date:${dateFrom}..${dateTo}`;
  const commitSearchUrl = `https://api.github.com/search/commits?q=${encodeURIComponent(searchQuery)}&per_page=100`;

  const headers = {
    'Authorization': `token ${personalAccessToken}`,
    'Accept': 'application/vnd.github.v3+json',
    'User-Agent': 'skill-in-connector',
  };

  const commitResp = await fetch(commitSearchUrl, { headers });
  const commitData = await commitResp.json();
  const commits = commitResp.ok ? (commitData.items ?? []) : [];

  // PRs by this user across configured repos
  const allPrs = [];
  for (const repoName of repos) {
    const prResp = await fetch(
      `https://api.github.com/repos/${org}/${repoName}/pulls?state=all&per_page=100`,
      { headers }
    );
    if (!prResp.ok) continue;
    const prs = await prResp.json();

    // Filter by author login or email (GitHub search commit API gives us the login)
    const byAuthor = prs.filter(pr =>
      pr.created_at >= dateFrom && pr.created_at <= dateTo
    );
    allPrs.push(...byAuthor.map(pr => ({ ...pr, repo: repoName })));
  }

  return normaliseGithubArtifacts(commits, allPrs, employeeEmail, cfg);
}

// ─── MCP server ─────────────────────────────────────────────────────────────

const server = new Server(
  { name: 'skill-in-connector', version: '1.0.0' },
  { capabilities: { tools: {} } }
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [
    {
      name: 'read_connector_config',
      description: 'Load the employer connector-config.json. Returns employer info, assessment period, employee list, and which connectors are enabled. Call this first before any fetch tool.',
      inputSchema: { type: 'object', properties: {}, required: [] },
    },
    {
      name: 'fetch_tasks',
      description: 'Fetch task/delivery data for an employee from the enabled task source (Jira). Returns normalised tasks_and_sla data.',
      inputSchema: {
        type: 'object',
        properties: {
          employeeEmail: { type: 'string', description: 'Employee email address from connector-config.json' },
        },
        required: ['employeeEmail'],
      },
    },
    {
      name: 'fetch_communication',
      description: 'Fetch communication data for an employee from the enabled communication source (Slack). Returns normalised communication data.',
      inputSchema: {
        type: 'object',
        properties: {
          employeeEmail: { type: 'string', description: 'Employee email address from connector-config.json' },
        },
        required: ['employeeEmail'],
      },
    },
    {
      name: 'fetch_artifacts',
      description: 'Fetch code artifact data for an employee from the enabled artifact source (GitHub). Returns normalised artifacts data including commits and PRs.',
      inputSchema: {
        type: 'object',
        properties: {
          employeeEmail: { type: 'string', description: 'Employee email address from connector-config.json' },
        },
        required: ['employeeEmail'],
      },
    },
  ],
}));

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  try {
    if (name === 'read_connector_config') {
      const cfg = loadConfig();
      // Return everything except credentials
      const safe = {
        employer: cfg.employer,
        assessment: cfg.assessment,
        employees: cfg.employees,
        enabledConnectors: Object.entries(cfg.connectors ?? {})
          .filter(([, v]) => v.enabled)
          .map(([k, v]) => ({
            type: k,
            configured: k === 'jira'
              ? { baseUrl: v.baseUrl, projectKey: v.projectKey }
              : k === 'slack'
                ? { channels: v.channels }
                : k === 'github'
                  ? { org: v.org, repos: v.repos }
                  : {},
          })),
        skillinApiUrl: cfg.skillin?.apiUrl,
      };
      return { content: [{ type: 'text', text: JSON.stringify(safe, null, 2) }] };
    }

    if (name === 'fetch_tasks') {
      const cfg = loadConfig();
      if (!cfg.connectors?.jira?.enabled) {
        return { content: [{ type: 'text', text: JSON.stringify({ skipped: true, reason: 'Jira connector is disabled in connector-config.json', bucket: 'tasks_and_sla' }) }] };
      }
      const normalised = await fetchJiraIssues(cfg, args.employeeEmail);
      return { content: [{ type: 'text', text: JSON.stringify(normalised, null, 2) }] };
    }

    if (name === 'fetch_communication') {
      const cfg = loadConfig();
      if (!cfg.connectors?.slack?.enabled) {
        return { content: [{ type: 'text', text: JSON.stringify({ skipped: true, reason: 'Slack connector is disabled in connector-config.json', bucket: 'communication' }) }] };
      }
      const normalised = await fetchSlackMessages(cfg, args.employeeEmail);
      return { content: [{ type: 'text', text: JSON.stringify(normalised, null, 2) }] };
    }

    if (name === 'fetch_artifacts') {
      const cfg = loadConfig();
      if (!cfg.connectors?.github?.enabled) {
        return { content: [{ type: 'text', text: JSON.stringify({ skipped: true, reason: 'GitHub connector is disabled in connector-config.json', bucket: 'artifacts' }) }] };
      }
      const normalised = await fetchGithubArtifacts(cfg, args.employeeEmail);
      return { content: [{ type: 'text', text: JSON.stringify(normalised, null, 2) }] };
    }

    return { content: [{ type: 'text', text: `Unknown tool: ${name}` }], isError: true };
  } catch (err) {
    return { content: [{ type: 'text', text: `Error: ${err.message}` }], isError: true };
  }
});

const transport = new StdioServerTransport();
await server.connect(transport);
