/**
 * Skill-in Platform Connector MCP Server
 *
 * Reads the 4 normalized data buckets directly from the Skill-in backend.
 * This is the internal connector used when Skill-in itself acts as the employer —
 * no external Jira/Slack/GitHub credentials needed.
 *
 * Data sources → normalized buckets:
 *   tasks_and_sla    ← Trello sprint cards + checklists (trello-dashboard-stats)
 *   communication    ← MentorChat + CustomerChat + GroupChat + PrivateChats + BoardMeetings attendance
 *   artifacts        ← BoardState GitHub data (validate-backend / validate-frontend)
 *   design_content   ← ProjectModules ModuleType=2 (per-sprint feature spec written by PM role:
 *                       user stories, acceptance criteria, sprint brief. Sequence = sprintNumber - 1)
 */

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { CallToolRequestSchema, ListToolsRequestSchema } from '@modelcontextprotocol/sdk/types.js';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname   = path.dirname(fileURLToPath(import.meta.url));
const CONFIG_PATH = path.resolve(__dirname, '..', 'connector-config.json');

function loadConfig() {
  if (!fs.existsSync(CONFIG_PATH))
    throw new Error('connector-config.json not found. Edit the file in the plugin root with your student IDs.');
  return JSON.parse(fs.readFileSync(CONFIG_PATH, 'utf8'));
}

function apiUrl(cfg) {
  return (cfg.platform?.apiUrl ?? 'https://skill-in-backend-dvdmgbe7fmhmg4hp.eastus2-01.azurewebsites.net').replace(/\/$/, '');
}

async function apiFetch(url) {
  const resp = await fetch(url);
  const text = await resp.text();
  if (!resp.ok) throw new Error(`API ${resp.status}: ${text.slice(0, 200)}`);
  return JSON.parse(text);
}

// ─── Student details cache (per run) ────────────────────────────────────────

const studentCache = {};

async function getStudentDetails(studentId, base) {
  if (studentCache[studentId]) return studentCache[studentId];
  const data = await apiFetch(`${base}/api/Boards/use/student/${studentId}`);
  const me = (data.teamMembers ?? []).find(m => m.id === studentId);
  const details = {
    boardId:     data.boardId,
    projectId:   data.projectId ?? data.project?.id,
    studentName: me ? `${me.firstName ?? ''} ${me.lastName ?? ''}`.trim() : `Student #${studentId}`,
    roleName:    me?.roles?.[0]?.roleName ?? 'Unknown',
    email:       me?.email ?? null,
    // All team members (for private chat pair lookups)
    teamMembers: (data.teamMembers ?? []).map(m => ({
      id:    m.id,
      email: m.email ?? null,
      name:  `${m.firstName ?? ''} ${m.lastName ?? ''}`.trim(),
      role:  m.roles?.[0]?.roleName ?? null,
    })),
    // Meeting attendance already computed by the student endpoint
    meetingStats: data.meetingStatistics ?? data.MeetingStatistics ?? null,
  };
  studentCache[studentId] = details;
  return details;
}

// ─── Normalisers ─────────────────────────────────────────────────────────────

function normaliseTasksData(trelloStats, studentDetails, sprintNumber) {
  // trello-dashboard-stats returns lists[] with cards[] with checkItems[]
  const lists     = trelloStats.lists ?? trelloStats.Lists ?? [];
  const allCards  = lists.flatMap(l => (l.cards ?? l.Cards ?? []).map(c => ({ ...c, listName: l.name ?? l.Name })));

  // Filter to cards matching this student's role (assignedToEmail or role-based)
  const roleName  = studentDetails.roleName?.toLowerCase() ?? '';
  const email     = studentDetails.email?.toLowerCase() ?? '';
  const relevant  = allCards.filter(c => {
    const assignee = (c.assignedToEmail ?? c.AssignedToEmail ?? '').toLowerCase();
    const cardRole = (c.roleName ?? c.RoleName ?? '').toLowerCase();
    return assignee === email || cardRole.includes(roleName) || (email === '' && cardRole !== '');
  });

  const tasks = relevant.map(c => {
    const items     = c.checklistItems ?? c.ChecklistItems ?? [];
    const complete  = items.filter(i => (i.state ?? i.State) === 'complete').length;
    return {
      id:            c.id ?? c.Id,
      title:         c.name ?? c.Name ?? '',
      list:          c.listName,
      dueDate:       c.dueDate ?? c.DueDate ?? null,
      estimatedHours: c.estimatedHours ?? c.EstimatedHours ?? null,
      checklistTotal:    items.length,
      checklistComplete: complete,
      completionRate:    items.length > 0 ? Math.round((complete / items.length) * 100) : null,
    };
  });

  const withChecklists = tasks.filter(t => t.checklistTotal > 0);
  const avgCompletion  = withChecklists.length > 0
    ? Math.round(withChecklists.reduce((s, t) => s + t.completionRate, 0) / withChecklists.length)
    : null;

  return {
    bucket:      'tasks_and_sla',
    source:      'trello',
    studentId:   studentDetails.boardId,
    role:        studentDetails.roleName,
    sprintNumber,
    tasks,
    summary: {
      totalCards:      tasks.length,
      cardsWithChecks: withChecklists.length,
      avgChecklistCompletion: avgCompletion,
      totalChecklistItems:    tasks.reduce((s, t) => s + t.checklistTotal, 0),
      completedItems:         tasks.reduce((s, t) => s + t.checklistComplete, 0),
    },
  };
}

function normaliseCommunicationData({
  mentorChat, customerChat, groupChatRaw, privateChats, meetingStats, studentId, sprintNumber,
}) {
  const mentorMsgs   = (mentorChat?.Messages   ?? []).filter(m => m.Role === 'user');
  const customerMsgs = (customerChat?.messages ?? customerChat?.Messages ?? []).filter(m => (m.Role ?? m.role) === 'user');

  // Group chat is a raw text blob — count non-empty lines as proxy for messages
  const groupText  = groupChatRaw?.chatHistory ?? groupChatRaw?.ChatHistory ?? '';
  const groupLines = groupText.split('\n').filter(l => l.trim().length > 0).length;

  function msgStats(msgs) {
    if (msgs.length === 0) return { count: 0, avgWords: 0 };
    const words = msgs.map(m => (m.Message ?? m.message ?? '').split(/\s+/).filter(Boolean).length);
    return { count: msgs.length, avgWords: Math.round(words.reduce((s, w) => s + w, 0) / words.length) };
  }

  // Private chats: one entry per team member pair
  const privateSummary = privateChats.map(p => ({
    with:      p.partnerEmail,
    withName:  p.partnerName,
    hasChat:   p.chatHistory.trim().length > 0,
    lineCount: p.chatHistory.split('\n').filter(l => l.trim().length > 0).length,
  }));
  const totalPrivateLines = privateSummary.reduce((s, p) => s + p.lineCount, 0);

  // Meeting attendance — already computed by the student endpoint
  const meetings = meetingStats
    ? {
        attended:        meetingStats.AttendedCalls    ?? meetingStats.attendedCalls    ?? 0,
        missed:          meetingStats.NotAttendingCalls ?? meetingStats.notAttendingCalls ?? 0,
        participationRate: meetingStats.ParticipationRate ?? meetingStats.participationRate ?? null,
      }
    : null;

  const totalInteractions =
    mentorMsgs.length + customerMsgs.length + groupLines + totalPrivateLines + (meetings?.attended ?? 0);

  return {
    bucket:      'communication',
    source:      'skill-in-platform-chat',
    studentId,
    sprintNumber,
    channels: {
      mentorChat:   msgStats(mentorMsgs),
      customerChat: msgStats(customerMsgs),
      groupChat:    { messageLineCount: groupLines },
      privateChats: privateSummary,
      meetings,
    },
    summary: {
      totalInteractions,
      mentorEngagement:    mentorMsgs.length,
      customerEngagement:  customerMsgs.length,
      groupParticipation:  groupLines,
      privateChatActivity: totalPrivateLines,
      meetingAttendance:   meetings?.participationRate ?? null,
    },
  };
}

function normaliseArtifactsData(backendVal, frontendVal, boardId, sprintNumber) {
  function extractGitHub(val) {
    if (!val) return null;
    return {
      branch:         val.branchName   ?? val.BranchName   ?? null,
      branchStatus:   val.branchStatus ?? val.BranchStatus ?? null,
      prStatus:       val.prStatus     ?? val.PRStatus     ?? val.PrStatus     ?? null,
      lastCommitDate: val.latestCommitDate ?? val.LatestCommitDate ?? null,
      lastCommitMsg:  val.latestCommitDescription ?? val.LatestCommitDescription ?? null,
      merged:         val.lastMergeDate != null,
      buildStatus:    val.lastBuildStatus ?? val.LastBuildStatus ?? null,
    };
  }

  const be = extractGitHub(backendVal?.boardState ?? backendVal);
  const fe = extractGitHub(frontendVal?.boardState ?? frontendVal);

  return {
    bucket:      'artifacts',
    source:      'github-via-skill-in',
    boardId,
    sprintNumber,
    backend:  be,
    frontend: fe,
    summary: {
      backendBranchActive:  be?.branchStatus === 'Active',
      backendPrOpen:        be?.prStatus === 'Requested',
      backendMerged:        be?.merged ?? false,
      frontendBranchActive: fe?.branchStatus === 'Active',
      frontendPrOpen:       fe?.prStatus === 'Requested',
      frontendMerged:       fe?.merged ?? false,
      lastCommitDate:       be?.lastCommitDate ?? fe?.lastCommitDate ?? null,
    },
  };
}

function normaliseDesignContent(modules, projectId) {
  const items = (Array.isArray(modules) ? modules : modules?.modules ?? modules?.Modules ?? []);
  return {
    bucket:    'design_content',
    source:    'project-modules',
    projectId,
    modules:   items.map(m => ({
      id:          m.id   ?? m.Id,
      title:       m.title ?? m.Title ?? '',
      type:        m.moduleType ?? m.ModuleType ?? null,
      sprint:      m.sequence   ?? m.Sequence   ?? null,
      hasContent:  !!(m.description ?? m.Description),
      wordCount:   ((m.description ?? m.Description ?? '')).split(/\s+/).filter(Boolean).length,
    })),
    summary: {
      totalModules:      items.length,
      modulesWithContent: items.filter(m => !!(m.description ?? m.Description)).length,
      sprints:           [...new Set(items.map(m => m.sequence ?? m.Sequence).filter(Boolean))].sort(),
    },
  };
}

// ─── MCP Server ──────────────────────────────────────────────────────────────

const server = new Server(
  { name: 'skill-in-platform', version: '1.0.0' },
  { capabilities: { tools: {} } }
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [
    {
      name: 'read_platform_config',
      description: 'Load the connector-config.json. Returns company info, platform API URL, student IDs, and sprint numbers to assess.',
      inputSchema: { type: 'object', properties: {}, required: [] },
    },
    {
      name: 'fetch_student_info',
      description: 'Fetch board ID, project ID, role, and team members for a student. Call this before any fetch_* tool if you need boardId or projectId.',
      inputSchema: {
        type: 'object',
        properties: {
          studentId: { type: 'number', description: 'Numeric student ID' },
        },
        required: ['studentId'],
      },
    },
    {
      name: 'fetch_tasks',
      description: 'Fetch sprint task data for a student from Trello (tasks_and_sla bucket). Returns cards, checklists, completion rates.',
      inputSchema: {
        type: 'object',
        properties: {
          studentId:   { type: 'number' },
          sprintNumber: { type: 'number' },
        },
        required: ['studentId', 'sprintNumber'],
      },
    },
    {
      name: 'fetch_communication',
      description: 'Fetch communication data for a student (communication bucket). Returns mentor chat, customer chat, group chat, private 1:1 chats with each team member, and meeting attendance statistics.',
      inputSchema: {
        type: 'object',
        properties: {
          studentId:   { type: 'number' },
          sprintNumber: { type: 'number' },
        },
        required: ['studentId', 'sprintNumber'],
      },
    },
    {
      name: 'fetch_artifacts',
      description: 'Fetch GitHub artifact data for a student (artifacts bucket). Returns backend and frontend branch/PR/commit status from BoardState.',
      inputSchema: {
        type: 'object',
        properties: {
          studentId:   { type: 'number' },
          sprintNumber: { type: 'number' },
        },
        required: ['studentId', 'sprintNumber'],
      },
    },
    {
      name: 'fetch_design_content',
      description: 'Fetch design spec data for a student (design_content bucket). Returns ProjectModules with ModuleType=2 — per-sprint feature specs written by the PM role student, containing user stories and acceptance criteria. Sequence = sprintNumber - 1. Most relevant for PM role assessment.',
      inputSchema: {
        type: 'object',
        properties: {
          studentId: { type: 'number' },
        },
        required: ['studentId'],
      },
    },
  ],
}));

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  try {
    const cfg  = loadConfig();
    const base = apiUrl(cfg);

    // ── read_platform_config ─────────────────────────────────────────────────
    if (name === 'read_platform_config') {
      return {
        content: [{
          type: 'text',
          text: JSON.stringify({
            company:     cfg.company,
            platformUrl: base,
            students:    cfg.students,
            assessment:  cfg.assessment,
          }, null, 2),
        }],
      };
    }

    // ── fetch_student_info ───────────────────────────────────────────────────
    if (name === 'fetch_student_info') {
      const details = await getStudentDetails(Number(args.studentId), base);
      return { content: [{ type: 'text', text: JSON.stringify(details, null, 2) }] };
    }

    // ── fetch_tasks ──────────────────────────────────────────────────────────
    if (name === 'fetch_tasks') {
      const sid     = Number(args.studentId);
      const sprint  = Number(args.sprintNumber);
      const details = await getStudentDetails(sid, base);

      const trelloStats = await apiFetch(`${base}/api/Boards/use/trello-dashboard-stats?boardId=${encodeURIComponent(details.boardId)}`);
      const normalised  = normaliseTasksData(trelloStats, details, sprint);
      return { content: [{ type: 'text', text: JSON.stringify(normalised, null, 2) }] };
    }

    // ── fetch_communication ──────────────────────────────────────────────────
    if (name === 'fetch_communication') {
      const sid    = Number(args.studentId);
      const sprint = Number(args.sprintNumber);
      const details = await getStudentDetails(sid, base);
      const bid    = encodeURIComponent(details.boardId);

      // Fetch AI chats + group chat in parallel
      const [mentorChat, customerChat, groupChat] = await Promise.allSettled([
        apiFetch(`${base}/api/Mentor/use/chat-history?studentId=${sid}&sprintNumber=${sprint}`),
        apiFetch(`${base}/api/Customer/use/chat-history?studentId=${sid}&sprintNumber=${sprint}`),
        apiFetch(`${base}/api/Boards/use/chat?boardId=${bid}`),
      ]);

      // Fetch private chats with every other team member in parallel
      const studentEmail  = details.email ?? '';
      const otherMembers  = details.teamMembers.filter(m => m.email && m.email !== studentEmail);
      const privateFetches = await Promise.allSettled(
        otherMembers.map(m =>
          apiFetch(
            `${base}/api/Boards/use/chat?boardId=${bid}` +
            `&email1=${encodeURIComponent(studentEmail)}&email2=${encodeURIComponent(m.email)}`
          ).then(data => ({ partnerEmail: m.email, partnerName: m.name, chatHistory: data?.chatHistory ?? data?.ChatHistory ?? '' }))
        )
      );
      const privateChats = privateFetches
        .filter(r => r.status === 'fulfilled')
        .map(r => r.value);

      const normalised = normaliseCommunicationData({
        mentorChat:   mentorChat.status   === 'fulfilled' ? mentorChat.value   : null,
        customerChat: customerChat.status === 'fulfilled' ? customerChat.value : null,
        groupChatRaw: groupChat.status    === 'fulfilled' ? groupChat.value    : null,
        privateChats,
        meetingStats: details.meetingStats,
        studentId:    sid,
        sprintNumber: sprint,
      });

      return { content: [{ type: 'text', text: JSON.stringify(normalised, null, 2) }] };
    }

    // ── fetch_artifacts ──────────────────────────────────────────────────────
    if (name === 'fetch_artifacts') {
      const sid     = Number(args.studentId);
      const sprint  = Number(args.sprintNumber);
      const details = await getStudentDetails(sid, base);
      const bid     = encodeURIComponent(details.boardId);

      const [backendVal, frontendVal] = await Promise.allSettled([
        apiFetch(`${base}/api/Mentor/use/validate-backend?boardId=${bid}`),
        apiFetch(`${base}/api/Mentor/use/validate-frontend?boardId=${bid}`),
      ]);

      const normalised = normaliseArtifactsData(
        backendVal.status  === 'fulfilled' ? backendVal.value  : null,
        frontendVal.status === 'fulfilled' ? frontendVal.value : null,
        details.boardId,
        sprint,
      );

      return { content: [{ type: 'text', text: JSON.stringify(normalised, null, 2) }] };
    }

    // ── fetch_design_content ─────────────────────────────────────────────────
    if (name === 'fetch_design_content') {
      const sid     = Number(args.studentId);
      const details = await getStudentDetails(sid, base);

      if (!details.projectId) {
        return { content: [{ type: 'text', text: JSON.stringify({ skipped: true, reason: 'projectId not found for student', bucket: 'design_content' }) }] };
      }

      const modules    = await apiFetch(`${base}/api/ProjectModules/by-project/${details.projectId}`);
      const normalised = normaliseDesignContent(modules, details.projectId);
      return { content: [{ type: 'text', text: JSON.stringify(normalised, null, 2) }] };
    }

    return { content: [{ type: 'text', text: `Unknown tool: ${name}` }], isError: true };

  } catch (err) {
    return { content: [{ type: 'text', text: `Error: ${err.message}` }], isError: true };
  }
});

const transport = new StdioServerTransport();
await server.connect(transport);
