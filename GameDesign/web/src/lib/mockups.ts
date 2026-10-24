import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { DESIGN_DIR, TASKS_DIR, REPO_ROOT } from './paths';
import { designSlug, taskSlug } from './slugs';
import assets from '../generated/assets.json';

// 목업(.html) 모아 보기 — /mockups (2026-10-25).
// 목업은 sync-assets가 public/assets로 복사해 두지만, 문서 안 링크로만 닿아 "무엇이 있는지"를 볼 곳이 없었다.
// 여기서는 design/ 아래 .html을 전부 모아 제목 · 한 줄 설명 · 고친 날 · 그 목업을 가리키는 문서를 붙인다.

export type MockupRef = { label: string; href: string };

export type Mockup = {
  slug: string;
  /** design/ 기준 상대 경로 — `ui/경매장목업.html` */
  relPath: string;
  /** public/assets의 복사본 주소 */
  src: string;
  title: string;
  /** 목업 머리의 `.lede` 문단 — 없으면 빈 문자열 */
  summary: string;
  /** design/ 바로 아래 폴더 — `ui` · `proposals` … */
  section: string;
  /** 마지막으로 고친 날 YYYY-MM-DD — git이 없으면 파일 시각 */
  updated: string;
  /** 이 목업을 링크하는 기획 문서 · 일감 */
  refs: MockupRef[];
};

// 폴더 이름 → 목록에 보일 묶음 이름. 표에 없으면 폴더 이름 그대로.
const SECTION_LABEL: Record<string, string> = {
  ui: '게임 UI',
  proposals: '지난 제안 (기록)',
};
// 묶음 순서 — 지금 쓰는 것이 위, 기록은 아래.
const SECTION_ORDER = ['ui'];

export function sectionLabel(section: string): string {
  return SECTION_LABEL[section] ?? section;
}

function walk(dir: string, ext: string, base = dir, out: string[] = []): string[] {
  if (!fs.existsSync(dir)) return out;
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, ext, base, out);
    else if (e.name.toLowerCase().endsWith(ext)) out.push(path.relative(base, p).split(path.sep).join('/'));
  }
  return out;
}

function decodeEntities(s: string): string {
  return s
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/&nbsp;/g, ' ')
    .replace(/&amp;/g, '&');
}

function stripTags(s: string): string {
  return decodeEntities(s.replace(/<[^>]+>/g, '')).replace(/\s+/g, ' ').trim();
}

// git으로 마지막 커밋 날을 읽는다. 배포는 이력을 받아 오므로(docs-deploy.yml `fetch-depth: 0`) 그대로 맞는다.
// 커밋 전 파일이거나 git이 없으면 파일 시각으로 대신한다.
function lastUpdated(abs: string): string {
  try {
    const out = execFileSync('git', ['log', '-1', '--format=%cs', '--', abs], {
      cwd: REPO_ROOT,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'ignore'],
    }).trim();
    if (out) return out;
  } catch {
    // git이 없다 — 아래로
  }
  return fs.statSync(abs).mtime.toISOString().slice(0, 10);
}

// 문서 하나가 이 목업 이름을 링크하는가 — 원본은 파일 이름을 그대로 쓰기도, percent-encoding으로 쓰기도 한다.
function mentions(text: string, fileName: string): boolean {
  if (text.includes(fileName)) return true;
  const encoded = encodeURIComponent(fileName).replace(/\(/g, '%28').replace(/\)/g, '%29');
  return text.includes(encoded) || text.includes(encodeURI(fileName));
}

function firstHeading(text: string): string {
  const m = text.match(/^#\s+(.+)$/m);
  return m ? m[1].replace(/[*`]/g, '').trim() : '';
}

type Doc = { label: string; href: string; text: string };

function loadReferenceDocs(): Doc[] {
  const docs: Doc[] = [];
  for (const rel of walk(DESIGN_DIR, '.md')) {
    // 레퍼런스 분석(AI 제안)은 목업을 지나가며 언급할 뿐이다 — 연결 목록이 길어지기만 한다.
    if (rel.startsWith('research/')) continue;
    const text = fs.readFileSync(path.join(DESIGN_DIR, rel), 'utf8');
    docs.push({ label: firstHeading(text) || path.basename(rel, '.md'), href: `/design/${designSlug(rel)}`, text });
  }
  for (const rel of walk(TASKS_DIR, '.md')) {
    const name = path.basename(rel);
    if (!/^T-\d{3}/i.test(name)) continue;
    const text = fs.readFileSync(path.join(TASKS_DIR, rel), 'utf8');
    const id = name.match(/^(T-\d{3})/i)![1];
    const title = text.match(/^제목:\s*(.+)$/m)?.[1]?.trim() ?? '';
    docs.push({ label: `${id} ${title}`.trim(), href: `/tasks/${taskSlug(name)}`, text });
  }
  return docs;
}

let cache: Mockup[] | null = null;

export function loadMockups(): Mockup[] {
  if (cache) return cache;
  const manifest = assets as Record<string, string>;
  const docs = loadReferenceDocs();
  const list: Mockup[] = [];

  for (const rel of walk(DESIGN_DIR, '.html')) {
    const src = manifest[rel];
    if (!src) continue; // sync-assets가 아직 복사하지 않았다 — 다음 빌드에 뜬다
    const abs = path.join(DESIGN_DIR, rel);
    const html = fs.readFileSync(abs, 'utf8');
    const fileName = path.basename(rel);
    const title = stripTags(html.match(/<title[^>]*>([\s\S]*?)<\/title>/i)?.[1] ?? '') || fileName.replace(/\.html$/i, '');
    const lede = html.match(/<p class="lede"[^>]*>([\s\S]*?)<\/p>/i)?.[1] ?? '';

    list.push({
      slug: path.basename(src, '.html'),
      relPath: rel,
      src,
      title,
      summary: stripTags(lede),
      section: rel.includes('/') ? rel.split('/')[0] : '',
      updated: lastUpdated(abs),
      refs: docs.filter((d) => mentions(d.text, fileName)).map(({ label, href }) => ({ label, href })),
    });
  }

  const order = (s: string) => {
    const i = SECTION_ORDER.indexOf(s);
    return i < 0 ? SECTION_ORDER.length : i;
  };
  // 묶음 순서 → 최근에 고친 것이 위
  list.sort((a, b) => order(a.section) - order(b.section) || b.updated.localeCompare(a.updated) || a.title.localeCompare(b.title));
  cache = list;
  return list;
}
