import fs from 'node:fs';
import path from 'node:path';
import { PROPOSALS_DIR } from './paths';
import { renderMd } from './content-loader';

export type Proposal = {
  slug: string;
  /** YYYY-MM-DD */
  date: string;
  /** 회차 시각(KST, 0~23) */
  hour: number;
  game: string;
  system: string;
  /** 장르 · 플랫폼 · 출시 · 개발 한 줄 */
  meta: string;
  /** `연계 판정` 또는 `추천안` 값 */
  verdict: string;
  verdictKind: 'apply' | 'adapt' | 'reference' | 'reject' | 'pick';
  /** 구현 코스트 — 문서에 없으면 빈 문자열 */
  cost: string;
  /** 목록에 한 줄로 보일 요약(마크다운 제거) */
  summary: string;
  readMinutes: number;
  bodyHtml: string;
};

// 파일 이름이 곧 회차다 — `2026-10-17-21-genshin-impact-wish-pity.md`
const FILE_RE = /^(\d{4}-\d{2}-\d{2})-(\d{2})-(.+)\.md$/i;

function plain(text: string): string {
  return text
    .replace(/!\[[^\]]*\]\([^)]*\)/g, '')
    .replace(/\[([^\]]+)\]\([^)]*\)/g, '$1')
    .replace(/\*\*|__|`/g, '')
    .trim();
}

// 설계 원칙 번호(게임기획코어 1장) — 번호만 보고는 뜻을 모르니 화면에는 짧은 이름으로 쓴다
const PRINCIPLE_LABEL: Record<string, string> = {
  '1': '주의 안 뺏기',
  '2': '띄워 두면 자람',
  '3': '놓쳐도 손해 없음',
  '4': '재화 순환',
};

function namePrinciples(text: string): string {
  return text
    .replace(/P1\s*~\s*P4/g, '설계 원칙')
    .replace(/^(\s*-\s*)P([1-4])\b/gm, (_, lead, n) => `${lead}**${PRINCIPLE_LABEL[n]}**`)
    .replace(/\bP([1-4])\b/g, (_, n) => PRINCIPLE_LABEL[n]);
}

function verdictKind(verdict: string, isPick: boolean): Proposal['verdictKind'] {
  if (isPick) return 'pick';
  if (/변형/.test(verdict)) return 'adapt';
  if (/참고/.test(verdict)) return 'reference';
  if (/부적합|보류|불가/.test(verdict)) return 'reject';
  return 'apply';
}

/// <summary>문서마다 머리 형식이 조금씩 달라(장르: 접두 유무 · 판정 대신 추천안) 줄 단위로 너그럽게 읽는다</summary>
function parse(fileName: string, raw: string): Proposal | null {
  const m = fileName.match(FILE_RE);
  if (!m) return null;
  const text = namePrinciples(raw);
  const [, date, hour, slug] = m;

  const h1 = text.match(/^#\s+(.+)$/m)?.[1].trim() ?? slug;
  const [game, ...rest] = h1.split(/\s+—\s+/);
  const system = (rest.join(' — ') || h1).replace(/\s*레퍼런스 분석\s*$/, '');

  // h1 바로 아래 인용 블록이 머리 정보다.
  const afterH1 = text.slice(text.indexOf(h1) + h1.length).replace(/^\s*\r?\n/, '');
  const head: string[] = [];
  for (const line of afterH1.split(/\r?\n/)) {
    if (!line.startsWith('>')) break;
    head.push(line.replace(/^>\s?/, '').trim());
  }

  let meta = '';
  let verdict = '';
  let cost = '';
  let isPick = false;
  for (const line of head) {
    if (!line || /작성일|제안 원본/.test(line)) continue;
    const pick = line.match(/추천안\s*:\s*\*\*(.+?)\*\*/);
    const judge = line.match(/판정\s*:\s*\*\*(.+?)\*\*/);
    if (pick || judge) {
      verdict = (pick ?? judge)![1];
      isPick = !!pick;
      cost = line.match(/구현 코스트\s*:\s*([^·]+)/)?.[1].trim() ?? cost;
      continue;
    }
    if (!meta) meta = plain(line.replace(/^장르\s*:\s*/, ''));
  }

  const ours = text.match(/^\s*-\s*\*\*우리에게:\*\*\s*(.+)$/m)?.[1];
  const what = text.match(/^\s*-\s*\*\*무엇:\*\*\s*(.+)$/m)?.[1];
  const summary = plain(ours ?? what ?? '');

  const body = afterH1.split(/\r?\n/).slice(head.length).join('\n');
  return {
    slug,
    date,
    hour: Number(hour),
    game: game.trim(),
    system,
    meta,
    verdict: verdict || '판정 없음',
    verdictKind: verdictKind(verdict, isPick),
    cost,
    summary,
    readMinutes: Math.max(1, Math.round(text.length / 900)),
    bodyHtml: renderMd(body, PROPOSALS_DIR),
  };
}

/** 최신 회차가 위 — 같은 회차 안에서는 파일 이름순 */
export function loadProposals(): Proposal[] {
  if (!fs.existsSync(PROPOSALS_DIR)) return [];
  const out: Proposal[] = [];
  for (const name of fs.readdirSync(PROPOSALS_DIR)) {
    const p = parse(name, fs.readFileSync(path.join(PROPOSALS_DIR, name), 'utf8'));
    if (p) out.push(p);
  }
  return out.sort(
    (a, b) => b.date.localeCompare(a.date) || b.hour - a.hour || a.slug.localeCompare(b.slug),
  );
}

/** 빌드 시각 기준 "N시간 전" — 정적 사이트라 배포할 때마다 다시 센다 */
export function ago(p: Proposal, now = new Date()): string {
  const at = new Date(`${p.date}T${String(p.hour).padStart(2, '0')}:00:00+09:00`);
  const hours = Math.floor((now.getTime() - at.getTime()) / 3_600_000);
  if (hours < 1) return '방금';
  if (hours < 24) return `${hours}시간 전`;
  const days = Math.floor(hours / 24);
  return days < 30 ? `${days}일 전` : p.date;
}
