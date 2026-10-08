export const APP_VERSION = '0.5.0';

/** 사이트를 빌드한 시각(KST) — 문서·일감이 바뀔 때마다 다시 빌드되므로 곧 최종 갱신 시각이다. 예: `2026-10-02 14:30` */
export const BUILD_TIME = (() => {
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat('en-CA', {
      timeZone: 'Asia/Seoul',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23',
    })
      .formatToParts(new Date())
      .map((p) => [p.type, p.value]),
  );
  return `${parts.year}-${parts.month}-${parts.day} ${parts.hour}:${parts.minute}`;
})();

/// <summary>영문 폴더명을 사이드바에 보일 한글 라벨로 옮긴다</summary>
export const SECTION_LABELS: Record<string, string> = {
  '': '최상위',
  gathering: '자원채취',
  fishing: '낚시',
  farming: '농사',
  logging: '벌목',
  hunting: '사냥',
  mining: '채굴',
  workslot: '작업슬롯',
  character: '캐릭터',
  quest: '퀘스트',
  trait: '특성',
  item: '아이템',
  trade: '거래',
  progression: '진행 및 성장',
  ui: '게임 UI',
  research: '리서치',
  proposals: '방향제안',
};

// 사이드바에 이 순서로 놓는다. 여기 없는 폴더는 뒤에 알파벳순으로 붙는다.
export const SECTION_ORDER = [
  '',
  'workslot',
  'gathering',
  'character',
  'quest',
  'trait',
  'item',
  'trade',
  'progression',
  'ui',
  'research',
  'proposals',
];

// 일감 상태 — 2주 회차 흐름 (2026-09-28). Resolve는 "작업자는 끝냈고 회의·확인을 기다린다".
export const STATUSES = ['New', 'In Progress', 'Resolve', 'Feedback', 'Closed'] as const;

export const STATUS_STYLE: Record<string, { label: string; cls: string }> = {
  New: { label: 'New', cls: 'bg-slate-100 text-slate-600' },
  'In Progress': { label: 'In Progress', cls: 'bg-emerald-100 text-emerald-800' },
  Resolve: { label: 'Resolve', cls: 'bg-sky-100 text-sky-800' },
  Feedback: { label: 'Feedback', cls: 'bg-amber-100 text-amber-800' },
  Closed: { label: 'Closed', cls: 'bg-slate-100 text-slate-400 line-through' },
};

// 담당은 사람 한 명이다. 역할은 표시용 보조 정보.
export const PEOPLE = [
  { name: '태웅', role: '클라' },
  { name: '규빈', role: '기획' },
  { name: '진우', role: '서버·기획' },
] as const;

export const OWNER_STYLE: Record<string, string> = {
  태웅: 'bg-violet-100 text-violet-800',
  규빈: 'bg-rose-100 text-rose-800',
  진우: 'bg-sky-100 text-sky-800',
};

export const PRIORITY_STYLE: Record<string, string> = {
  높음: 'text-rose-600 font-bold',
  보통: 'text-slate-500',
  낮음: 'text-slate-400',
};

export const PRIORITY_ORDER: Record<string, number> = { 높음: 0, 보통: 1, 낮음: 2 };
// 손이 가야 하는 것부터 — 진행중 → 피드백 → 새 일 → 확인 대기 → 닫힘
export const STATUS_ORDER: Record<string, number> = {
  'In Progress': 0,
  Feedback: 1,
  New: 2,
  Resolve: 3,
  Closed: 4,
};

// 회차 — 2026-10-10(토) 22:00부터 14일 간격. 이름은 YYMMDD.
const CYCLE_ANCHOR = Date.UTC(2026, 9, 10);
const CYCLE_DAYS = 14;

function cycleName(ms: number): string {
  const d = new Date(ms);
  const yy = String(d.getUTCFullYear() % 100).padStart(2, '0');
  const mm = String(d.getUTCMonth() + 1).padStart(2, '0');
  const dd = String(d.getUTCDate()).padStart(2, '0');
  return `${yy}${mm}${dd}`;
}

/// <summary>오늘 기준 다가오는 회차부터 count개 — 빌드 시점에 정해진다</summary>
export function upcomingCycles(count: number, now = Date.now()): string[] {
  const day = 86_400_000;
  const passed = Math.max(0, Math.ceil((now - CYCLE_ANCHOR - day) / (CYCLE_DAYS * day)));
  return Array.from({ length: count }, (_, i) => cycleName(CYCLE_ANCHOR + (passed + i) * CYCLE_DAYS * day));
}
