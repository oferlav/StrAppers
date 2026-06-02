/**
 * Skill-in Workflow Server
 * Plain Node.js — no npm install needed. Requires Node 18+.
 *
 * Serves the workflow UI and handles connector-config.json read/write.
 *
 *   node serve.js          → http://localhost:3456
 *   PORT=8080 node serve.js
 */

const http = require('http');
const fs   = require('fs');
const path = require('path');

const PORT        = parseInt(process.env.PORT || '3456');
const WORKFLOW    = path.join(__dirname, 'workflow');
const CONFIG_PATH = path.join(__dirname, 'connector-config.json');
const TEMPLATE    = path.join(__dirname, 'connector-config.template.json');

const MIME = {
  '.html': 'text/html',
  '.js':   'application/javascript',
  '.css':  'text/css',
  '.json': 'application/json',
  '.png':  'image/png',
  '.svg':  'image/svg+xml',
};

function json(res, status, obj) {
  res.writeHead(status, { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' });
  res.end(JSON.stringify(obj, null, 2));
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    req.on('data', c => chunks.push(c));
    req.on('end',  () => resolve(Buffer.concat(chunks).toString('utf8')));
    req.on('error', reject);
  });
}

const server = http.createServer(async (req, res) => {
  res.setHeader('Access-Control-Allow-Origin',  '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  if (req.method === 'OPTIONS') { res.writeHead(204); res.end(); return; }

  const url = new URL(req.url, `http://localhost:${PORT}`);

  // ── Config API ───────────────────────────────────────────────────────────

  if (url.pathname === '/api/config') {
    if (req.method === 'GET') {
      const src = fs.existsSync(CONFIG_PATH) ? CONFIG_PATH : TEMPLATE;
      try {
        const raw  = fs.readFileSync(src, 'utf8');
        const data = JSON.parse(raw);
        // Strip template-only fields before sending to the UI
        delete data._readme;
        Object.values(data.connectors ?? {}).forEach(c => delete c._notes);
        json(res, 200, data);
      } catch (e) {
        json(res, 500, { error: e.message });
      }
      return;
    }

    if (req.method === 'POST') {
      try {
        const body = await readBody(req);
        const data = JSON.parse(body);             // validate JSON
        fs.writeFileSync(CONFIG_PATH, JSON.stringify(data, null, 2), 'utf8');
        json(res, 200, { success: true, savedTo: CONFIG_PATH });
      } catch (e) {
        json(res, 400, { error: e.message });
      }
      return;
    }
  }

  // ── Static files from workflow/ ──────────────────────────────────────────

  let filePath = path.join(WORKFLOW, url.pathname === '/' ? 'index.html' : url.pathname);
  // Safety: prevent path traversal
  if (!filePath.startsWith(WORKFLOW)) { res.writeHead(403); res.end(); return; }

  if (!fs.existsSync(filePath)) { res.writeHead(404); res.end('Not found'); return; }

  const ext  = path.extname(filePath);
  const mime = MIME[ext] ?? 'application/octet-stream';
  res.writeHead(200, { 'Content-Type': mime });
  fs.createReadStream(filePath).pipe(res);
});

server.listen(PORT, () => {
  console.log(`\n  Skill-in Workflow  →  http://localhost:${PORT}`);
  console.log(`  Config file        →  ${CONFIG_PATH}\n`);
});
