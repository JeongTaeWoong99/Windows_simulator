import fs from 'node:fs';
import path from 'node:path';
import MarkdownIt from 'markdown-it';
import { REPO_ROOT, TASKS_DIR, ARCHIVE_DIR, DESIGN_DIR, GITHUB_BLOB } from './paths';
import { designSlug, taskSlug } from './slugs';
import assets from '../generated/assets.json';

const md = new MarkdownIt({ html: true, linkify: false, breaks: false });

// 그래프 검사기와 같은 기준 — 아카이브성 문서는 사이드바 트리에서 접어 둔다.
const ARCHIVE_DIRS = new Set(['research', 'proposals']);

export type DesignDoc = {
  slug: string;
  relPath: string;
  title: string;
  section: string;
  updated: string | null;
  isArchive: boolean;
  bodyHtml: string;
};

export type TaskDoc = {
  taskId: string;
  slug: string;
  title: string;
  owner: string;
  status: string;
  priority: string;
  due: string;
  done: boolean;
  bodyHtml: string;
};

function walk(dir: string, base: string, out: string[] = []): string[] {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, base, out);
    else if (e.name.toLowerCase().endsWith('.md')) {
      out.push(path.relative(base, p).split(path.sep).join('/'));
    }
  }
  return out;
}

/// <summary>`---`로 감싼 프론트매터를 한글 키 그대로 읽는다</summary>
function frontmatter(text: string): { data: Record<string, string>; body: string } {
  const m = text.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n?/);
  if (!m) return { data: {}, body: text };
  const data: Record<string, string> = {};
  for (const line of m[1].split(/\r?\n/)) {
    const kv = line.match(/^([^:]+):\s*(.*)$/);
    if (kv) data[kv[1].trim()] = kv[2].trim().replace(/\s*#.*$/, '');
  }
  return { data, body: text.slice(m[0].length) };
}

/// <summary>표는 페이지가 아니라 표 자신이 가로 스크롤하도록 감싼다</summary>
function renderMd(text: string, fileDir: string): string {
  return md
    .render(rewriteLinks(text, fileDir))
    .replace(/<table>/g, '<div class="table-scroll"><table>')
    .replace(/<\/table>/g, '</table></div>');
}

function firstHeading(body: string): string {
  const m = body.match(/^#\s+(.+)$/m);
  return m ? m[1].trim() : '';
}

function updatedDate(text: string): string | null {
  const m =
    text.match(/(?:^|\n)>.*최종 업데이트\s*:\s*(\d{4}-\d{2}-\d{2})/) ??
    text.match(/(?:^|\n)>\s*작성일\s*:\s*(\d{4}-\d{2}-\d{2})/);
  return m ? m[1] : null;
}

/**
 * 문서 안의 상대 링크를 사이트 라우트로 바꾼다.
 * 원본은 저장소에서 서로를 `.md` 상대경로로 가리키므로, 그대로 두면 웹에서 전부 404다.
 */
function rewriteLinks(body: string, fileDir: string): string {
  return body.replace(/\]\(([^)\s]+)(\s+"[^"]*")?\)/g, (whole, rawTarget: string, title = '') => {
    const [target, hash = ''] = rawTarget.split('#');
    if (!target || /^(https?:|mailto:|#)/.test(rawTarget)) return whole;

    const decoded = decodeURIComponent(target);
    const abs = path.resolve(fileDir, decoded);
    const route = routeFor(abs);
    if (!route) return whole;
    return `](${route}${hash ? '#' + hash : ''}${title})`;
  });
}

/** 저장소 안의 절대 경로 하나를 사이트 라우트로 옮긴다. 대상이 아니면 null. */
function routeFor(abs: string): string | null {
  const rel = (from: string) => path.relative(from, abs).split(path.sep).join('/');

  if (abs.startsWith(DESIGN_DIR)) {
    const r = rel(DESIGN_DIR);
    if (r.toLowerCase().endsWith('.md')) return `/design/${designSlug(r)}`;
    const asset = (assets as Record<string, string>)[r];
    return asset ?? null;
  }
  if (abs.startsWith(ARCHIVE_DIR) || abs.startsWith(TASKS_DIR)) {
    const name = path.basename(abs);
    if (name.toLowerCase() === 'readme.md') return '/tasks';
    if (name.toLowerCase().endsWith('.md')) return `/tasks/${taskSlug(name)}`;
    return null;
  }
  // 사이트에 없는 저장소 파일(CLAUDE.md·스킬·코드)은 GitHub으로 보낸다.
  if (abs.startsWith(REPO_ROOT)) {
    const r = path.relative(REPO_ROOT, abs).split(path.sep).join('/');
    return `${GITHUB_BLOB}/${r.split('/').map(encodeURIComponent).join('/')}`;
  }
  return null;
}

export function loadDesignDocs(): DesignDoc[] {
  const docs: DesignDoc[] = [];
  for (const rel of walk(DESIGN_DIR, DESIGN_DIR)) {
    const full = path.join(DESIGN_DIR, rel);
    const text = fs.readFileSync(full, 'utf8');
    const top = rel.split('/')[0];
    const section = rel.includes('/') ? top : '';
    docs.push({
      slug: designSlug(rel),
      relPath: rel,
      title: firstHeading(text) || path.basename(rel, '.md'),
      section,
      updated: updatedDate(text),
      isArchive: ARCHIVE_DIRS.has(section),
      bodyHtml: renderMd(text, path.dirname(full)),
    });
  }
  return docs.sort((a, b) => a.slug.localeCompare(b.slug));
}

export function loadTasks(): TaskDoc[] {
  const tasks: TaskDoc[] = [];
  const files: Array<{ dir: string; name: string; done: boolean }> = [];

  for (const name of fs.readdirSync(TASKS_DIR)) {
    if (name.toLowerCase().endsWith('.md') && name.toLowerCase() !== 'readme.md') {
      files.push({ dir: TASKS_DIR, name, done: false });
    }
  }
  if (fs.existsSync(ARCHIVE_DIR)) {
    for (const name of fs.readdirSync(ARCHIVE_DIR)) {
      if (name.toLowerCase().endsWith('.md') && name.toLowerCase() !== 'readme.md') {
        files.push({ dir: ARCHIVE_DIR, name, done: true });
      }
    }
  }

  for (const f of files) {
    const full = path.join(f.dir, f.name);
    const text = fs.readFileSync(full, 'utf8');
    const { data, body } = frontmatter(text);
    tasks.push({
      taskId: data['id'] ?? f.name.slice(0, 5),
      slug: taskSlug(f.name),
      title: data['제목'] ?? firstHeading(body),
      owner: data['담당'] ?? '공용',
      status: data['상태'] ?? (f.done ? '완료' : '대기'),
      priority: data['우선순위'] ?? '보통',
      due: data['마감'] ?? '미정',
      done: f.done,
      // 제목은 헤더에서 이미 보여 준다 — 본문 첫 h1을 빼지 않으면 두 번 나온다.
      bodyHtml: renderMd(body.replace(/^#\s+.+\r?\n/m, ''), path.dirname(full)),
    });
  }
  return tasks.sort((a, b) => a.taskId.localeCompare(b.taskId));
}
