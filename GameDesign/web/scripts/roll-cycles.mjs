// 회차 자동 이월 — 지난 회차를 목표로 둔 미완료 일감을 다음 회차로 민다.
//
//   node scripts/roll-cycles.mjs            일감 파일·INDEX만 고친다
//   node scripts/roll-cycles.mjs --issues   + GitHub 이슈 마일스톤도 옮긴다 (GH_TOKEN · GITHUB_REPOSITORY 필요)
//   node scripts/roll-cycles.mjs --dry-run  고칠 것만 출력한다 (--now=YYYY-MM-DD로 날짜를 바꿔 볼 수 있다)
//
// 배포 워크플로(docs-deploy.yml)가 빌드 전에 돌리고, 바뀐 파일을 커밋한다. 여러 번 돌려도 결과가 같다.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const TASKS_DIR = path.join(REPO_ROOT, 'tasks');
const INDEX_FILE = path.join(TASKS_DIR, 'README.md');

// ⚠️ src/lib/labels.ts의 회차 계산과 같아야 한다 — 바꾸면 둘 다 고친다.
const CYCLE_ANCHOR = Date.UTC(2026, 9, 10);
const CYCLE_DAYS = 14;
const DAY = 86_400_000;

// 끝난 일감 — 이 상태면 회차가 지나도 그대로 둔다.
const FINISHED = new Set(['Closed', 'Done']);

const args = new Set(process.argv.slice(2));
const dryRun = args.has('--dry-run');

function cycleName(ms) {
  const d = new Date(ms);
  const yy = String(d.getUTCFullYear() % 100).padStart(2, '0');
  const mm = String(d.getUTCMonth() + 1).padStart(2, '0');
  const dd = String(d.getUTCDate()).padStart(2, '0');
  return `${yy}${mm}${dd}`;
}

/// 지금 굴러가는(다가오는) 회차 — 회의 다음 날 00:00(UTC)부터 다음 회차로 넘어간다
function currentCycle(now = Date.now()) {
  const passed = Math.max(0, Math.ceil((now - CYCLE_ANCHOR - DAY) / (CYCLE_DAYS * DAY)));
  return cycleName(CYCLE_ANCHOR + passed * CYCLE_DAYS * DAY);
}

const isCycle = (v) => /^\d{6}$/.test(v);

function todayKst() {
  return new Date(Date.now() + 9 * 3_600_000).toISOString().slice(0, 10);
}

/// 루트 tasks/의 일감 중 지난 회차에 남은 것을 다음 회차로 옮긴다. 옮긴 [{ id, from }]을 돌려준다.
function rollTaskFiles(current) {
  const rolled = [];
  const files = fs.readdirSync(TASKS_DIR).filter((f) => /^T-\d+.*\.md$/.test(f));

  for (const name of files) {
    const file = path.join(TASKS_DIR, name);
    const text = fs.readFileSync(file, 'utf8');
    const head = text.match(/^---\r?\n([\s\S]*?)\r?\n---/);
    if (!head) {
      continue;
    }

    const field = (key) => head[1].match(new RegExp(`^${key}:\\s*(.*?)\\s*$`, 'm'))?.[1] ?? '';
    const id = field('id');
    const target = field('목표');
    if (!isCycle(target) || target >= current || FINISHED.has(field('상태'))) {
      continue;
    }

    rolled.push({ id, from: target });
    if (dryRun) {
      continue;
    }

    const eol = text.includes('\r\n') ? '\r\n' : '\n';
    const note = `> ⏩ ${target} 회차에 못 끝내 ${current}로 자동 이월 (${todayKst()})`;
    const front = head[0].replace(/^목표:.*$/m, `목표: ${current}`);
    let body = text.slice(head[0].length);
    // 이월 사유는 제목 바로 아래 한 줄 — 제목이 없으면 본문 맨 앞
    const title = body.match(/^# .*$/m);
    body = title
      ? body.replace(title[0], `${title[0]}${eol}${eol}${note}`)
      : `${eol}${eol}${note}${body}`;
    fs.writeFileSync(file, front + body);
  }
  return rolled;
}

/// INDEX 표에서 옮긴 일감 줄의 마지막 칸(목표)을 바꾼다
function rollIndex(rolled, current) {
  if (rolled.length === 0 || dryRun) {
    return;
  }
  const ids = new Set(rolled.map((r) => r.id));
  const text = fs.readFileSync(INDEX_FILE, 'utf8');
  const next = text.replace(/^\| \[(T-\d+)\]\(.*\| *(\d{6}) *\|[ \t]*$/gm, (line, id, target) => {
    if (!ids.has(id) || target >= current) {
      return line;
    }
    return line.replace(/\| *\d{6} *\|[ \t]*$/, `| ${current} |`);
  });
  fs.writeFileSync(INDEX_FILE, next);
}

async function gh(method, url, body) {
  const res = await fetch(`https://api.github.com/repos/${process.env.GITHUB_REPOSITORY}${url}`, {
    method,
    headers: {
      Authorization: `Bearer ${process.env.GH_TOKEN}`,
      Accept: 'application/vnd.github+json',
      'X-GitHub-Api-Version': '2022-11-28',
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) {
    throw new Error(`${method} ${url} → ${res.status} ${await res.text()}`);
  }
  return res.json();
}

/// 지난 회차 마일스톤의 열린 이슈를 이번 회차로 옮기고, 지난 마일스톤은 닫는다
async function rollIssues(current) {
  if (!process.env.GH_TOKEN || !process.env.GITHUB_REPOSITORY) {
    throw new Error('--issues에는 GH_TOKEN · GITHUB_REPOSITORY가 필요하다');
  }

  const open = await gh('GET', '/milestones?state=open&per_page=100');
  const past = open.filter((m) => isCycle(m.title) && m.title < current);
  if (past.length === 0) {
    return [];
  }

  let target = open.find((m) => m.title === current);
  if (!target && !dryRun) {
    const due = `20${current.slice(0, 2)}-${current.slice(2, 4)}-${current.slice(4)}T13:00:00Z`;
    target = await gh('POST', '/milestones', { title: current, due_on: due });
  }

  const moved = [];
  for (const m of past) {
    const issues = await gh('GET', `/issues?milestone=${m.number}&state=open&per_page=100`);
    for (const issue of issues) {
      moved.push({ number: issue.number, from: m.title });
      if (!dryRun) {
        await gh('PATCH', `/issues/${issue.number}`, { milestone: target.number });
      }
    }
    // 열린 이슈를 다 옮겼으니 지난 회차는 닫는다 — 이슈 화면의 마일스톤 목록에서 빠진다
    if (!dryRun) {
      await gh('PATCH', `/milestones/${m.number}`, { state: 'closed' });
    }
  }
  return moved;
}

// --now=2026-10-25 — 그날인 것처럼 돌린다 (확인용)
const nowArg = process.argv.find((a) => a.startsWith('--now='));
const current = currentCycle(nowArg ? Date.parse(nowArg.slice(6)) : Date.now());
const rolled = rollTaskFiles(current);
rollIndex(rolled, current);
console.log(`이번 회차 ${current} — 일감 ${rolled.length}건 이월${dryRun ? ' (dry-run)' : ''}`);
for (const r of rolled) {
  console.log(`  ${r.id}  ${r.from} → ${current}`);
}

if (args.has('--issues')) {
  const moved = await rollIssues(current);
  console.log(`이슈 ${moved.length}건 마일스톤 이월`);
  for (const m of moved) {
    console.log(`  #${m.number}  ${m.from} → ${current}`);
  }
}
