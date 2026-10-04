// Parse BACKLOG.md and create GitHub issues. Usage: node create_issues.js [--apply]
const fs = require('fs');
const os = require('os');
const path = require('path');
const { execFileSync } = require('child_process');

const REPO = 'leo-brgn/SousTension';
const APPLY = process.argv.includes('--apply');
const md = fs.readFileSync(path.join(__dirname, '..', 'BACKLOG.md'), 'utf8').split(/\r?\n/);

const MILESTONES = {
  PROTO: 'PROTO (S+8)', VS: 'VS - Vertical slice (M+5)', ALPHA: 'ALPHA (M+9)',
  BETA: 'BETA (M+12)', GOLD: 'GOLD (M+14)', POST: 'POST-lancement',
};
const TYPES = { '🧪': 'type:spike', '⚙️': 'type:code', '🎨': 'type:art', '🔊': 'type:audio', '✍️': 'type:design', '🧰': 'type:tooling' };
// Jalon par défaut quand le backlog n'en précise pas pour l'item
const EPIC_DEFAULT_MS = { 0: 'PROTO', 1: 'PROTO', 2: 'PROTO', 3: 'VS', 4: 'PROTO', 5: 'VS', 6: 'VS', 7: 'ALPHA', 8: 'ALPHA', 9: 'ALPHA', 10: 'ALPHA', 11: 'VS', 12: 'VS', 13: 'BETA', 14: 'ALPHA', 15: 'BETA', 16: 'VS', 17: 'BETA', 18: 'POST' };
const PROG ='role:game-programmer', ART = 'role:3d-designer';

// --- parse ---
const items = [];
let epicNum = null, epicName = null, section0 = false;
for (const line of md) {
  let m = line.match(/^# EPIC (\d+) — (.+)$/);
  if (m) { epicNum = +m[1]; epicName = m[2].trim(); section0 = false; continue; }
  if (/^# 0\./.test(line)) { section0 = true; epicNum = 0; epicName = 'Décisions et questions ouvertes'; continue; }
  m = line.match(/^- \[[ x~]\] \*\*((?:D-\d+)|(?:E\d+-\d+[a-c]?))(.*?)\*\*(.*)$/);
  if (!m) continue;
  const id = m[1];
  let titlePart = m[2].trim();
  let rest = m[3].trim();
  let icon = null;
  for (const ic of Object.keys(TYPES)) {
    if (titlePart.startsWith(ic)) { icon = ic; titlePart = titlePart.slice(ic.length).trim(); break; }
  }
  // Title may be empty (e.g. "**E18-01** 4ᵉ zone") -> use first words of rest
  let title = titlePart;
  if (!title) { title = rest.replace(/^[—\-:\s]+/, '').split(/[.(—]/)[0].trim(); }
  title = title.replace(/\s*[—:]\s*$/, '');
  items.push({ id, epicNum, epicName, icon, title, rest, raw: line });
}

// --- number mapping (issues created in order => #1..#N) ---
const num = {};
items.forEach((it, i) => { num[it.id] = i + 1; });
const link = (text) => text.replace(/\b(E\d+-\d+)([a-c]?)\b|\b(D-\d+)\b/g, (s, a, b, d) => {
  const k = a || d; return num[k] ? `${s} (#${num[k]})` : s;
});

// --- labels/milestone ---
function build(it) {
  const labels = new Set();
  const all = it.raw;
  // milestone: first tag found in text after the title
  let ms = null, best = Infinity;
  for (const k of Object.keys(MILESTONES)) {
    const re = new RegExp('\\b' + k + '\\b');
    const mm = re.exec(it.rest);
    if (mm && mm.index < best) { best = mm.index; ms = k; }
  }
  let defaulted = false;
  if (!ms) { ms = EPIC_DEFAULT_MS[it.epicNum]; defaulted = true; }
  const sz = it.rest.match(/`(S|M|L|XL)`/);
  const isDecision = it.id.startsWith('D-');
  if (isDecision) labels.add('decision');
  if (it.icon) labels.add(TYPES[it.icon]);
  if (sz) labels.add('size:' + sz[1]);
  labels.add(it.epicNum === 0 ? 'epic:00-decisions' : `epic:${String(it.epicNum).padStart(2, '0')}`);
  // role
  if (isDecision) {
    labels.add(PROG);
    if (['D-01', 'D-03', 'D-05', 'D-12'].includes(it.id)) labels.add(ART);
  } else if (it.icon === '🎨') labels.add(ART);
  else if (it.epicNum === 18 && ['E18-01', 'E18-02'].includes(it.id)) { labels.add(PROG); labels.add(ART); }
  else labels.add(PROG);
  if (/📦|🔧/.test(all) && it.icon === '🎨') labels.add('asset');
  return { labels: [...labels], ms, defaulted };
}

const BODY_TAIL = '\n\n---\n_Source : `BACKLOG.md` / `GDD_Sous_Pression.md` (v0.3). Les références `Exx-yy (#n)` pointent vers les issues liées._';

const plan = items.map((it) => {
  const { labels, ms, defaulted } = build(it);
  const title = `[${it.id}] ${it.title}`.slice(0, 250);
  const body = [
    `**Epic :** ${it.epicNum === 0 ? '0 — ' : it.epicNum + ' — '}${it.epicName}`,
    `**Jalon :** ${MILESTONES[ms]}${defaulted ? ' _(jalon par défaut de l\'epic, à affiner)_' : ''}`,
    '',
    link(it.rest.replace(/^[—\-:\s]+/, '')) || '_Voir BACKLOG.md._',
    BODY_TAIL,
  ].join('\n');
  return { id: it.id, title, body, labels, ms };
});

if (!APPLY) {
  const byLabel = {};
  plan.forEach((p) => p.labels.forEach((l) => (byLabel[l] = (byLabel[l] || 0) + 1)));
  console.log('Issues:', plan.length);
  console.log(byLabel);
  console.log('No milestone:', plan.filter((p) => !p.ms).map((p) => p.id).join(' '));
  console.log('No size:', plan.filter((p) => !p.labels.some((l) => l.startsWith('size:'))).map((p) => p.id).join(' '));
  plan.slice(0, 3).concat(plan.slice(-2)).forEach((p) => console.log('\n###', p.title, p.labels.join(','), p.ms, '\n' + p.body));
  process.exit(0);
}

// --- apply ---
const gh = (args, opts = {}) => execFileSync('gh', args, { encoding: 'utf8', ...opts });
const COLORS = { 'role:': '1d76db', 'type:': 'fbca04', 'size:': 'c2e0c6', 'epic:': '5319e7', decision: 'd93f0b', asset: 'bfd4f2' };
const colorOf = (l) => { for (const k of Object.keys(COLORS)) if (l.startsWith(k)) return COLORS[k]; return 'ededed'; };

const allLabels = new Set(plan.flatMap((p) => p.labels));
for (const l of allLabels) {
  try { gh(['label', 'create', l, '-R', REPO, '--color', colorOf(l), '--force']); } catch (e) { console.error('label fail', l, e.message); }
}
const existing = JSON.parse(gh(['api', `repos/${REPO}/milestones?state=all`]));
for (const t of Object.values(MILESTONES)) {
  if (!existing.some((m) => m.title === t)) gh(['api', `repos/${REPO}/milestones`, '-f', `title=${t}`]);
}

const sleep = (ms) => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
const logFile = path.join(__dirname, 'issues_created.log');
const done = fs.existsSync(logFile) ? fs.readFileSync(logFile, 'utf8').split('\n').filter(Boolean) : [];
plan.forEach((p, i) => {
  if (done.some((d) => d.startsWith(p.id + ' '))) return;
  const tmp = path.join(os.tmpdir(), `issue_${p.id}.md`);
  fs.writeFileSync(tmp, p.body);
  const args = ['issue', 'create', '-R', REPO, '--title', p.title, '--body-file', tmp];
  p.labels.forEach((l) => args.push('--label', l));
  if (p.ms) args.push('--milestone', MILESTONES[p.ms]);
  const url = gh(args).trim();
  const n = +url.split('/').pop();
  fs.appendFileSync(logFile, `${p.id} ${url}\n`);
  if (n !== i + 1) { console.error(`NUMBER MISMATCH for ${p.id}: expected #${i + 1}, got #${n}`); process.exit(1); }
  console.log(p.id, url);
  sleep(1500);
});
console.log('DONE');
