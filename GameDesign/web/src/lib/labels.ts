export const APP_VERSION = '0.1.0';

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

export const STATUS_STYLE: Record<string, { label: string; cls: string }> = {
  진행중: { label: '진행중', cls: 'bg-emerald-100 text-emerald-800' },
  대기: { label: '대기', cls: 'bg-slate-100 text-slate-600' },
  보류: { label: '보류', cls: 'bg-amber-100 text-amber-800' },
  완료: { label: '완료', cls: 'bg-slate-100 text-slate-400 line-through' },
};

export const OWNER_STYLE: Record<string, string> = {
  서버: 'bg-sky-100 text-sky-800',
  클라: 'bg-violet-100 text-violet-800',
  공용: 'bg-slate-100 text-slate-700',
};

export const PRIORITY_STYLE: Record<string, string> = {
  높음: 'text-rose-600 font-bold',
  보통: 'text-slate-500',
  낮음: 'text-slate-400',
};

export const PRIORITY_ORDER: Record<string, number> = { 높음: 0, 보통: 1, 낮음: 2 };
export const STATUS_ORDER: Record<string, number> = { 진행중: 0, 보류: 1, 대기: 2, 완료: 3 };
