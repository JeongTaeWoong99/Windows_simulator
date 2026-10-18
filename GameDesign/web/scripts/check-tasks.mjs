// 일감 정합성 검사 — 손으로 맞추는 tasks/ 목록들이 서로 어긋났는지 찾는다 (T-129).
//
//   node GameDesign/web/scripts/check-tasks.mjs              전수 검사 — 어긋남이 있으면 exit 1
//   node GameDesign/web/scripts/check-tasks.mjs --no-issues  닫힌 이슈 검사를 건너뛴다 (gh 없이·오프라인)
//
// 검사 항목:
//   1. 목록 → 파일   INDEX·art·later·archive 목록의 일감 링크가 그 폴더의 파일을 가리키는가 (보관된 일감이 INDEX에 남았나)
//   2. 파일 → 목록   폴더의 일감 파일이 목록에 다 있는가 · 프론트매터 `상태`와 목록의 `상태` 칸이 같은가
//   3. 보관 건수     INDEX의 `끝난 일감 N건`이 archive/ 파일 수와 같은가
//   4. 끝난 선행     ⏸ 항목 · `막고 있는 것` · ⏸ 표가 보관된 일감이나 닫힌 이슈를 기다리는가 (gh로 이슈 상태 조회)
//   5. 옛 경로 링크  저장소 문서의 `tasks/…T-0XX-….md` 링크가 옮겨 간 파일을 가리키는가
//
// CI(tasks-check.yml)가 .md push · 이슈 닫힘마다 돌린다. 공돌리기(github-issue-writer 3장) 끝에도 로컬에서 돌린다.
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const TASKS_DIR = path.join(REPO_ROOT, 'tasks');

// 목록 파일 → 그 목록이 담는 일감 폴더. 루트 INDEX는 루트 파일만 담는다.
const LISTS = [
  { dir: '', label: 'INDEX' },
  { dir: 'art', label: 'art/' },
  { dir: 'later', label: 'later/' },
  { dir: 'archive', label: 'archive/' },
];

// 이 상태면 끝난 일감이다 — 보관 전이라도 기다릴 대상이 아니다.
const FINISHED = new Set(['Closed', 'Done']);

// 옛 경로 링크 검사에서 빼는 곳 — 그 시점의 기록이라 링크를 고치지 않는다.
const HISTORY_PREFIXES = ['tasks/archive/', '.claude/Agent/'];

// 이미 커밋·로그가 그 번호로 부르고 있어 번호를 바꾸지 못한 중복 ID — 새로 늘리지 않는다.
//   T-132 — 2026-10-08 두 세션이 같은 번호로 동시에 등록했다 (데이터 뷰어 · archive/풀링과 UniTask 전환)
const KNOWN_DUPLICATES = new Set(['T-132']);

const args = new Set(process.argv.slice(2));
const findings = [];

function report(check, file, line, message) {
  findings.push({ check, where: `${path.relative(REPO_ROOT, file).replaceAll('\\', '/')}:${line}`, message });
}

const read = (file) => fs.readFileSync(file, 'utf8');
const taskFilesIn = (dir) => fs.readdirSync(path.join(TASKS_DIR, dir)).filter((f) => /^T-\d+.*\.md$/.test(f));

function frontMatter(text) {
  const head = text.match(/^---\r?\n([\s\S]*?)\r?\n---/);
  const field = (key) => head?.[1].match(new RegExp(`^${key}:\\s*(.*?)\\s*$`, 'm'))?.[1] ?? '';
  return { id: field('id'), status: field('상태') };
}

// ── 일감 위치표 — 파일 경로 → { id, dir, file, status } · ID → [일감] ─────────────

const tasks = new Map();
const byId = new Map();
for (const { dir } of LISTS) {
  for (const name of taskFilesIn(dir)) {
    const file = path.join(TASKS_DIR, dir, name);
    const task = { id: name.match(/^(T-\d+)/)[1], dir, file, status: frontMatter(read(file)).status };
    tasks.set(file, task);
    byId.set(task.id, [...(byId.get(task.id) ?? []), task]);
  }
}

const isFinished = (task) => task.dir === 'archive' || FINISHED.has(task.status);
const folderOf = (task) => `tasks/${task.dir ? task.dir + '/' : ''}`;

// ── 0. 일감 ID 중복 ──────────────────────────────────────────────────────────

for (const [id, list] of byId) {
  if (list.length > 1 && !KNOWN_DUPLICATES.has(id)) {
    report('0 ID 중복', list[1].file, 1, `${id}가 ${list.length}개다 — ${list.map((t) => folderOf(t) + path.basename(t.file)).join(' · ')}`);
  }
}

// ── 1·2 앞. 끝났는데 보관되지 않은 일감 ─────────────────────────────────────────

for (const task of tasks.values()) {
  if (task.dir !== 'archive' && FINISHED.has(task.status)) {
    report('2 파일 → 목록', task.file, 1, `${task.id}가 \`${task.status}\`인데 ${folderOf(task)}에 있다 — archive/로 내린다`);
  }
}

// ── 표 파싱 ──────────────────────────────────────────────────────────────────

/// 마크다운 표의 일감 행을 { id, target, cells(헤더 → 값), line }으로 꺼낸다
function parseRows(text) {
  const lines = text.split(/\r?\n/);
  const rows = [];
  let header = null;
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (!line.startsWith('|')) {
      header = null;
      continue;
    }
    const cells = line.trim().replace(/^\||\|$/g, '').split(/(?<!\\)\|/).map((c) => c.trim());
    if (/^\|[\s:|-]+\|$/.test(lines[i + 1]?.trim() ?? '')) {
      header = cells.map((c) => c.replace(/\*/g, '').trim());
      continue;
    }
    const link = cells[0]?.match(/^\[(T-\d+)\]\(([^)]+)\)/);
    if (!header || !link) {
      continue;
    }
    rows.push({
      id: link[1],
      target: decodeURIComponent(link[2].split('#')[0]),
      cells: Object.fromEntries(header.map((h, k) => [h, cells[k] ?? ''])),
      line: i + 1,
    });
  }
  return rows;
}

const clean = (cell) => cell.replace(/\*/g, '').trim();

// ── 1·2. 목록 ↔ 파일 ─────────────────────────────────────────────────────────

const indexFile = path.join(TASKS_DIR, 'README.md');

for (const { dir, label } of LISTS) {
  const listFile = path.join(TASKS_DIR, dir, 'README.md');
  const rows = parseRows(read(listFile));
  const listed = new Set();

  for (const row of rows) {
    const resolved = path.resolve(path.dirname(listFile), row.target);
    const task = tasks.get(resolved);
    if (path.dirname(resolved) !== path.join(TASKS_DIR, dir) || !task) {
      const others = (byId.get(row.id) ?? []).map(folderOf);
      const where = others.length ? `실제로는 ${others.join(' · ')}에 있다` : '파일이 없다';
      report('1 목록 → 파일', listFile, row.line, `${row.id}가 ${label} 목록에 있는데 ${where}`);
      continue;
    }
    listed.add(task.file);
    // 보관 목록에는 상태 칸이 없다 — 보관됐으면 Closed다
    const cell = row.cells['상태'];
    if (cell !== undefined && clean(cell) !== task.status) {
      report('2 파일 → 목록', listFile, row.line, `${row.id} 상태가 다르다 — 목록 \`${clean(cell)}\` · 파일 \`${task.status}\``);
    }
  }

  for (const name of taskFilesIn(dir)) {
    const file = path.join(TASKS_DIR, dir, name);
    if (!listed.has(file)) {
      report('2 파일 → 목록', file, 1, `${tasks.get(file).id}가 ${label} 목록에 없다`);
    }
  }
}

// ── 3. 보관 건수 ─────────────────────────────────────────────────────────────

{
  const text = read(indexFile);
  const m = text.match(/끝난 일감 (\d+)건/);
  const actual = taskFilesIn('archive').length;
  if (!m) {
    report('3 보관 건수', indexFile, 1, '`끝난 일감 N건` 줄이 없다');
  } else if (Number(m[1]) !== actual) {
    const line = text.slice(0, m.index).split('\n').length;
    report('3 보관 건수', indexFile, line, `\`끝난 일감 ${m[1]}건\`인데 archive/에는 ${actual}건이다`);
  }
}

// ── 4. 끝난 선행을 기다리는 ⏸ ────────────────────────────────────────────────

/// 이슈 상태 — 닫힌 번호 집합. gh가 없거나 실패하면 null(이슈 검사를 건너뛴다)
function closedIssues() {
  if (args.has('--no-issues')) {
    return null;
  }
  try {
    const out = execFileSync('gh', ['issue', 'list', '--state', 'closed', '--limit', '1000', '--json', 'number'], {
      cwd: REPO_ROOT,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    return new Set(JSON.parse(out).map((i) => i.number));
  } catch (e) {
    console.warn(`⚠️ 이슈 상태를 못 읽어 닫힌 이슈 검사를 건너뛴다 — ${e.message.split('\n')[0]}`);
    return null;
  }
}

const closed = closedIssues();

/// 한 줄에서 기다리는 대상을 찾아 끝난 것을 보고한다
function checkWaits(file, line, text, selfId, what) {
  for (const [, id] of text.matchAll(/\b(T-\d{3})\b/g)) {
    // 중복 ID는 전부 끝났을 때만 끝난 것으로 본다
    const list = byId.get(id) ?? [];
    if (id !== selfId && list.length && list.every(isFinished)) {
      report('4 끝난 선행', file, line, `${what}이 끝난 ${id}를 기다린다`);
    }
  }
  if (!closed) {
    return;
  }
  for (const [, num] of text.matchAll(/(?:issues\/|#)(\d+)\b/g)) {
    if (closed.has(Number(num))) {
      report('4 끝난 선행', file, line, `${what}이 닫힌 이슈 #${num}를 기다린다`);
    }
  }
}

for (const task of tasks.values()) {
  if (task.dir === 'archive') {
    continue;
  }
  const id = task.id;
  const lines = read(task.file).split(/\r?\n/);
  let inBlockers = false;
  lines.forEach((line, k) => {
    if (/^#{1,6} /.test(line)) {
      inBlockers = /^## 막고 있는 것/.test(line);
      return;
    }
    // 아직 안 끝난 ⏸ 항목 — 줄 안의 일감·이슈를 전부 기다린다
    if (/^\s*- \[ \].*⏸/.test(line)) {
      checkWaits(task.file, k + 1, line, id, '⏸ 항목');
      return;
    }
    // `막고 있는 것`의 항목 — 일감·이슈로 **시작하는** 줄만 선행 선언으로 본다 (없음·✅·설명 줄은 넘긴다)
    const lead = inBlockers && line.match(/^- (?:\*\*)?\[?((?:T-\d{3}|이슈 #\d+|#\d+))/);
    if (lead) {
      checkWaits(task.file, k + 1, lead[1], id, '`막고 있는 것`');
    }
  });
}

for (const row of parseRows(read(indexFile))) {
  const wait = Object.entries(row.cells).find(([h]) => h.includes('기다리나'));
  if (wait) {
    checkWaits(indexFile, row.line, wait[1], row.id, `⏸ 표의 ${row.id}`);
  }
}

// ── 5. 옛 경로 링크 ──────────────────────────────────────────────────────────

const docs = execFileSync('git', ['ls-files', '-z', '--', '*.md'], { cwd: REPO_ROOT, encoding: 'utf8' })
  .split('\0')
  .filter((f) => f && !HISTORY_PREFIXES.some((p) => f.startsWith(p)));

for (const rel of docs) {
  const file = path.join(REPO_ROOT, rel);
  if (!fs.existsSync(file)) {
    continue;
  }
  read(file).split(/\r?\n/).forEach((line, k) => {
    for (const [, target] of line.matchAll(/\]\(([^)\s]*T-\d{3}[^)\s]*\.md)(?:#[^)]*)?\)/g)) {
      if (/^[a-z]+:/i.test(target)) {
        continue;
      }
      let decoded;
      try {
        decoded = decodeURIComponent(target);
      } catch {
        decoded = target;
      }
      const resolved = path.resolve(path.dirname(file), decoded);
      if (!resolved.startsWith(TASKS_DIR + path.sep) || fs.existsSync(resolved)) {
        continue;
      }
      // 파일 이름이 같은 일감을 찾는다 — 중복 ID가 있어도 슬러그까지 같으면 하나다
      const moved = [...tasks.values()].find((t) => path.basename(t.file) === path.basename(resolved));
      const hint = moved ? ` → 지금은 ${path.relative(path.dirname(file), moved.file).replaceAll('\\', '/')}` : '';
      report('5 옛 경로 링크', file, k + 1, `${target}가 없다${hint}`);
    }
  });
}

// ── 결과 ─────────────────────────────────────────────────────────────────────

if (findings.length === 0) {
  console.log(`일감 정합성 — 어긋남 0건 (일감 ${tasks.size}개 · 문서 ${docs.length}개${closed ? '' : ' · 이슈 검사 건너뜀'})`);
  process.exit(0);
}

console.log(`일감 정합성 — 어긋남 ${findings.length}건`);
let last = '';
for (const f of findings.sort((a, b) => a.check.localeCompare(b.check))) {
  if (f.check !== last) {
    console.log(`\n[${f.check}]`);
    last = f.check;
  }
  console.log(`  ${f.where}  ${f.message}`);
}
process.exit(1);
