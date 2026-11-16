import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import { execFileSync, spawnSync } from 'node:child_process';

const here = path.dirname(fileURLToPath(import.meta.url));
const studyFolder = 'GameDesign/design/research/game-studies';
const headings = ['누구에게 맞는가', '재미가 생기는 과정', '재미를 만드는 요소',
  '불편하거나 재미없는 순간', '직접 볼 관찰 과제', '우리 게임에 던지는 질문', '출처'];

function git(repoRoot, ...args) {
  return execFileSync('git', ['-c', `safe.directory=${repoRoot}`, ...args], {
    cwd: repoRoot, encoding: 'utf8', timeout: 120_000,
    stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true,
  }).trim();
}

export function stampFor(now = new Date()) {
  const parts = Object.fromEntries(new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Asia/Seoul', year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', hourCycle: 'h23',
  }).formatToParts(now).map(p => [p.type, p.value]));
  return { date: `${parts.year}-${parts.month}-${parts.day}`, hour: parts.hour };
}

function publishedIds(repoRoot) {
  const files = git(repoRoot, 'ls-tree', '-r', '--name-only', 'origin/main', '--', studyFolder);
  return new Set(files.split('\n').map(f => f.match(/-study-([a-z0-9-]+)\.md$/)?.[1]).filter(Boolean));
}

function publishedTitles(repoRoot) {
  if (publishedIds(repoRoot).size === 0) return [];
  return git(repoRoot, 'grep', '-h', '^# ', 'origin/main', '--', studyFolder)
    .split('\n').map(line => line.match(/^# (.+?) — /)?.[1]).filter(Boolean);
}

export function validateArticle(article, existingIds = new Set(), existingTitles = []) {
  if (!article || !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(article.game_id ?? ''))
    throw new Error('Invalid game_id');
  if (existingIds.has(article.game_id)) throw new Error(`Duplicate game: ${article.game_id}`);
  if (typeof article.title !== 'string' || !article.title.trim() || /[\r\n]/.test(article.title) || article.title.length > 100)
    throw new Error('Invalid title');
  const normalizeTitle = title => title.normalize('NFKC').toLowerCase().replace(/[^\p{L}\p{N}]/gu, '');
  if ([...existingTitles].some(title => normalizeTitle(title) === normalizeTitle(article.title)))
    throw new Error(`Duplicate game title: ${article.title}`);
  if (typeof article.markdown !== 'string' || article.markdown.length < 3000 || article.markdown.length > 30000 ||
      !article.markdown.startsWith(`# ${article.title} — 재미와 이용자 연구\n`))
    throw new Error('Invalid markdown title or length');
  for (const heading of headings)
    if (!article.markdown.includes(`## ${heading}\n`)) throw new Error(`Missing section: ${heading}`);
  if (!Array.isArray(article.source_urls) || new Set(article.source_urls).size < 3)
    throw new Error('At least three source URLs required');
  const domains = new Set();
  for (const source of article.source_urls) {
    const url = new URL(source);
    if (url.protocol !== 'https:' || !article.markdown.includes(source))
      throw new Error('Invalid or uncited source URL');
    domains.add(url.hostname);
  }
  if (domains.size < 2) throw new Error('At least two source domains required');
  for (const match of article.markdown.matchAll(/\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g)) {
    const target = match[1].split('#')[0];
    if (!target || /^(https?:|mailto:|#)/.test(match[1])) continue;
    try { decodeURIComponent(target); }
    catch { throw new Error('Invalid markdown link encoding'); }
  }
  // 원문 Markdown을 HTML로 실행시키지 않는다. 연구는 텍스트·링크·표만 받는다.
  if (/<\/?[a-z][^>]*>|javascript:/i.test(article.markdown))
    throw new Error('Unsafe markdown content');
  return article;
}

export function buildPrompt({ existingIds, existingTitles = [], stamp }) {
  return `사용자가 한국시간 매일 06·12·18시 게임별 재미 연구와 wsdocs 자동 게시를 허가했다.
지금 회차는 ${stamp.date} ${stamp.hour}시(KST)다. 추가 질문 없이 웹 근거를 조사하고 최종 JSON을 완성하라.
이 작업은 연구 전용이며 파일 변경·git·다른 에이전트 호출은 하지 않는다. 게시·커밋은 별도 검증기가 처리한다.

독자: 방치형·Desktop Companion을 해 본 적 없는 게임 개발자. 게임 하나를 정해 누가 왜 즐기고 어떤 구체적 플레이가 재미를 만드는지 한국어로 가르쳐라.
이미 조사한 게임 ID: ${JSON.stringify(existingIds)}. 동일 게임·다른 표기의 중복은 피한다.
이미 조사한 게임명: ${JSON.stringify(existingTitles)}. 부제나 영문/한글 표기만 바꾸어 다시 쓰지 않는다.
우선 후보: Cookie Clicker, Melvor Idle, Chillquarium, Desktop Mate, Virtual Desktop Pet Simulator, Tiny Pasture.
후보도 이미 조사했거나 공식 자료가 부족하면 다른 실제 방치형·바탕화면 동반 게임을 찾는다. 미출시·동명 작품은 구별한다.
game_id는 공식 영문 이름의 소문자 kebab-case로 고정하고 기존 ID와 별칭 중복도 확인한다.

실시간 웹 조사 필수. 공식 개발자·상점 자료와 실제 플레이어의 긍정/부정 리뷰·커뮤니티 글을 함께 읽는다.
최소 3개 URL, 최소 2개 도메인의 근거를 본문 가까이에 Markdown 링크로 인용하고 출처 절에 조사일·게시일·표본 한계를 적는다.
인구통계(나이·성별·직업)나 이용자 비중을 임의로 만들지 말고 이용 상황과 동기를 설명한다.
공식 기능 사실, 개별 리뷰의 관찰, 작성자 해석/가설을 명시적으로 구분한다. 오래된 불만을 현재 버전 사실로 단정하지 않는다.
직접 플레이·성능 측정을 했다고 주장하지 않는다. 생산성·집중력·심리 효과의 인과를 리뷰만으로 입증하지 않는다.
출처당 직접 인용은 최대 25단어, 가급적 한국어 요약으로 쓴다. 읽지 않은 URL을 근거로 쓰지 않는다.
기능 나열·홍보·우리 게임에 즉시 넣을 제안보다 플레이 장면 → 감정/동기 → 구체적 시스템 → 한계의 연결을 설명한다.

최종 JSON: game_id, title(정확한 게임명), markdown(한국어 3500~9000자 권장), source_urls.
markdown은 "# <title> — 재미와 이용자 연구"로 시작한다.
바로 아래 인용 블록: 장르·기본 상황 / "연계 판정: **학습 자료**" / "조사일: ${stamp.date}".
"## 한눈에"에 "- **무엇:**" 한 줄 요약을 쓴다.
필수 절: ${headings.map(h => '## ' + h).join(' / ')}.
우리 게임은 바탕화면 상주 방치형 Windows Simulator다. 질문 절은 관찰에서 도출한 검증 질문이며 확정 기획이 아니다.
학습 과제는 구매를 전제하지 않고 공식 영상/자료로도 관찰할 수 있게 하고 선택적 직접 플레이는 실제 수행과 구분한다.
마크다운에는 스크립트·iframe·원시 HTML을 넣지 않는다. 자료가 부족하면 성공한 척하지 말고 근거 한계를 설명하되 필수 연구를 채우지 못하면 빈 문서를 내지 말고 실패를 보고한다.`;
}

export function publishArticle({ repoRoot, runDir, article, stamp }) {
  git(repoRoot, 'fetch', 'origin');
  validateArticle(article, publishedIds(repoRoot), publishedTitles(repoRoot));
  fs.mkdirSync(runDir, { recursive: true });
  const checkout = path.resolve(runDir, 'publish');
  const file = `${studyFolder}/${stamp.date}-${stamp.hour}-study-${article.game_id}.md`;
  let added = false;
  try {
    git(repoRoot, 'worktree', 'add', '--detach', checkout, 'origin/main');
    added = true;
    const target = path.join(checkout, file);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, article.markdown.trim() + '\n', 'utf8');
    git(checkout, 'add', '--', file);
    git(checkout, 'commit', '--only', '-m', `docs: ${article.title.slice(0, 32)} 게임 연구 추가`, '--', file);
    for (let attempt = 0; ; attempt++) {
      try {
        git(checkout, 'push', 'origin', 'HEAD:main');
        break;
      } catch (error) {
        if (attempt >= 1) throw error;
        git(checkout, 'fetch', 'origin');
        validateArticle(article, publishedIds(checkout), publishedTitles(checkout));
        // 사용자 main에서는 rebase하지 않는다. 새 문서 하나뿐인 게시 전용 worktree에서만 재시도한다.
        git(checkout, 'rebase', 'origin/main');
      }
    }
    return { commit: git(checkout, 'rev-parse', 'HEAD'), file };
  } finally {
    if (added) {
      try { git(repoRoot, 'worktree', 'remove', checkout); }
      catch { console.error(`게시 worktree 정리 필요: ${checkout}`); }
    }
  }
}

function acquireLock(file) {
  try {
    const fd = fs.openSync(file, 'wx');
    fs.writeFileSync(fd, String(process.pid));
    fs.closeSync(fd);
    return true;
  } catch (error) {
    if (error.code !== 'EEXIST') throw error;
    const pid = Number(fs.readFileSync(file, 'utf8'));
    if (!Number.isInteger(pid) || pid <= 0) throw new Error('Invalid research lock; inspect before removing');
    try { process.kill(pid, 0); return false; }
    catch (probe) { if (probe.code !== 'ESRCH') return false; }
    fs.unlinkSync(file);
    return acquireLock(file);
  }
}

export function runOnce({ repoRoot, codexCli, now = new Date() }) {
  const stateDir = path.join(repoRoot, '.utmp', 'game-research');
  fs.mkdirSync(stateDir, { recursive: true });
  const lock = path.join(stateDir, 'running.lock');
  if (!acquireLock(lock)) return { status: 'skipped', reason: 'already running' };
  const runDir = path.join(stateDir, now.toISOString().replace(/[:.]/g, '-'));
  fs.mkdirSync(runDir);
  const statusFile = path.join(stateDir, 'latest.json');
  const stamp = stampFor(now);
  let fd;
  try {
    if (!fs.existsSync(codexCli)) throw new Error(`Codex CLI not found: ${codexCli}`);
    git(repoRoot, 'fetch', 'origin');
    const existingIds = [...publishedIds(repoRoot)];
    const existingTitles = publishedTitles(repoRoot);
    const prompt = buildPrompt({ existingIds, existingTitles, stamp });
    fs.writeFileSync(path.join(runDir, 'prompt.txt'), prompt, 'utf8');
    const output = path.join(runDir, 'article.json');
    fd = fs.openSync(path.join(runDir, 'codex.log'), 'w');
    const result = spawnSync(process.execPath, [
      codexCli, '--search', '--ask-for-approval', 'never', 'exec',
      '--sandbox', 'read-only', '--ephemeral', '--color', 'never', '--cd', runDir,
      '--output-schema', path.join(here, 'game-research.schema.json'),
      '--output-last-message', output, '-',
    ], { input: prompt, encoding: 'utf8', stdio: ['pipe', fd, fd], timeout: 45 * 60_000, windowsHide: true });
    if (result.error || result.status !== 0) throw result.error ?? new Error(`Codex failed: ${result.status}`);
    const article = validateArticle(JSON.parse(fs.readFileSync(output, 'utf8')), new Set(existingIds), existingTitles);
    const published = publishArticle({ repoRoot, runDir, article, stamp });
    const status = { status: 'published', at: new Date().toISOString(), game: article.title, ...published, runDir };
    fs.writeFileSync(statusFile, JSON.stringify(status, null, 2), 'utf8');
    return status;
  } catch (error) {
    fs.writeFileSync(statusFile, JSON.stringify({ status: 'failed', at: new Date().toISOString(), error: error.message, runDir }, null, 2), 'utf8');
    throw error;
  } finally {
    if (fd !== undefined) fs.closeSync(fd);
    fs.unlinkSync(lock);
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const repoRoot = path.resolve(here, '..', '..');
  const codexCli = path.join(process.env.APPDATA ?? path.join(os.homedir(), 'AppData', 'Roaming'),
    'npm', 'node_modules', '@openai', 'codex', 'bin', 'codex.js');
  if (process.argv.includes('--check')) {
    if (!fs.existsSync(codexCli)) throw new Error(`Codex CLI not found: ${codexCli}`);
    console.log(JSON.stringify({ repoRoot, codexCli, node: process.execPath, timeZone: 'Asia/Seoul', hours: [6, 12, 18] }, null, 2));
  } else {
    try { console.log(JSON.stringify(runOnce({ repoRoot, codexCli }), null, 2)); }
    catch (error) { console.error(error.message); process.exitCode = 1; }
  }
}
