import fs from 'node:fs';
import path from 'node:path';
import { DESIGN_DIR } from './paths';
import { renderInline } from './content-loader';

/**
 * 게임기획코어 5장 "확정 / 미확정 현황" 표를 읽는다.
 *
 * ⚠️ 이 표는 **기획이 정해졌는가**이지 **코드가 있는가**가 아니다.
 * 구현 여부는 일감(`tasks/`)이 갖는다 — 상태 페이지에서 둘을 나란히 두되 섞지 않는다.
 */
export type DesignState = 'done' | 'review' | 'open';

export type DesignStatusRow = {
  /** 항목 이름 (렌더된 인라인 HTML) */
  areaHtml: string;
  /** 상태 칸 원문 — `✅ 확정` · `❌ 미작성` 등 */
  stateText: string;
  /** 비고 (렌더된 인라인 HTML) */
  noteHtml: string;
  state: DesignState;
};

export const DESIGN_STATE_LABEL: Record<DesignState, string> = {
  done: '확정',
  review: '검토중',
  open: '미정',
};

export const DESIGN_STATE_STYLE: Record<DesignState, string> = {
  done: 'bg-emerald-100 text-emerald-800',
  review: 'bg-amber-100 text-amber-800',
  open: 'bg-rose-100 text-rose-800',
};

/** 문서가 쓰는 마커가 곧 분류 기준이다. 여기서 임의로 판단하지 않는다. */
function classify(stateText: string): DesignState {
  if (stateText.includes('✅')) return 'done';
  if (stateText.includes('⚠️') || stateText.includes('⏸')) return 'review';
  return 'open';
}

/** `| a | b | c |` 한 줄을 칸으로 가른다. 파이프 이스케이프는 문서에 없어 다루지 않는다. */
function cells(line: string): string[] {
  return line
    .replace(/^\s*\|/, '')
    .replace(/\|\s*$/, '')
    .split('|')
    .map((c) => c.trim());
}

/**
 * 5장 표만 떼어 온다.
 *
 * 표 **뒤에 다른 표를 가진 `###` 소절**이 이어지므로(속도 가변 충돌 절),
 * 다음 헤딩을 만나면 멈춘다 — 안 그러면 남의 표가 섞여 든다.
 */
export function loadDesignStatus(): DesignStatusRow[] {
  const file = path.join(DESIGN_DIR, '게임기획코어.md');
  const text = fs.readFileSync(file, 'utf8');
  const lines = text.split(/\r?\n/);

  const start = lines.findIndex((l) => /^##\s*5\.\s*확정\s*\/\s*미확정 현황/.test(l));
  if (start < 0) return [];

  const rows: DesignStatusRow[] = [];
  const fileDir = path.dirname(file);

  for (let i = start + 1; i < lines.length; i++) {
    const line = lines[i];
    if (/^#{2,3}\s/.test(line)) break; // 다음 절 — 여기서 끊는다
    if (!line.trim().startsWith('|')) continue;

    const c = cells(line);
    if (c.length < 2) continue;
    if (/^-{2,}$/.test(c[0].replace(/\s/g, ''))) continue; // 구분선
    if (c[0] === '영역' && c[1] === '상태') continue; // 헤더

    rows.push({
      areaHtml: renderInline(c[0], fileDir),
      stateText: c[1],
      noteHtml: renderInline(c[2] ?? '', fileDir),
      state: classify(c[1]),
    });
  }

  return rows;
}
