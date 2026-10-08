import fs from 'node:fs';
import path from 'node:path';
import { DATALOG_DIR } from './paths';

// 데이터 뷰어 — GameDesign/DataLog/*.json(엑셀 → .bytes 덤프)을 테이블 하나 = 페이지 하나로 보여 준다.
// 엑셀을 직접 읽지 않는다: .bytes를 되읽은 값이라 서버·클라가 실제로 받는 데이터와 같다.

export type Cell = string | number | boolean | null | Cell[] | { [k: string]: Cell };
export type Row = Record<string, Cell>;

export interface DataTable {
  name: string; // ItemTable
  slug: string; // itemtable
  columns: string[];
  rows: Row[];
}

/** 이름표 — `<키>TID` 열과 `Name` 열을 함께 가진 테이블이면 다른 테이블의 같은 TID 옆에 이름을 붙인다. */
export type NameLookup = Record<string, Record<string, string>>;

// 가중치 비율을 낼 때 묶는 열 — 앞의 것이 있으면 그것으로 묶는다(가챠 하나 · 레벨 하나가 롤 하나).
export const WEIGHT_GROUP_KEYS = ['GachaId', 'IndustryLevel', 'Grade'];

// 값이 몇 종류 이하면 위에 거르기 상자를 단다. 글자 열(enum 등)과 묶음 숫자 열(`…Level` · `…Id`)만 —
// Weight·가격 같은 수치 열에 달면 상자만 늘고 쓸 일이 없다. 설명·이름처럼 행마다 다른 글은 뺀다.
const FILTER_MAX_DISTINCT = 30;
const NUMERIC_FILTER_COLUMN = /(Level|Id)$/;
const NON_FILTER_COLUMNS = new Set(['Description', 'Tooltip', 'Name', 'Title', 'Body']);

export function loadDataTables(): DataTable[] {
  if (!fs.existsSync(DATALOG_DIR)) {
    return [];
  }
  return fs
    .readdirSync(DATALOG_DIR)
    .filter((f) => f.endsWith('.json'))
    .map((f) => {
      const rows = JSON.parse(fs.readFileSync(path.join(DATALOG_DIR, f), 'utf8')) as Row[];
      const name = path.basename(f, '.json');
      // 열 순서는 첫 행을 따르고, 뒤 행에만 있는 열이 있으면 끝에 붙인다.
      const columns = [...new Set(rows.flatMap((r) => Object.keys(r)))];
      return { name, slug: name.toLowerCase(), columns, rows };
    })
    .sort((a, b) => a.name.localeCompare(b.name));
}

export function buildNameLookup(tables: DataTable[]): NameLookup {
  const lookup: NameLookup = {};
  for (const t of tables) {
    const key = t.columns[0];
    if (!key?.endsWith('TID') || !t.columns.includes('Name')) {
      continue;
    }
    lookup[key] = Object.fromEntries(t.rows.map((r) => [String(r[key]), String(r.Name)]));
  }
  return lookup;
}

/** 열 이름으로 이름표를 찾는다 — `ItemTID` · `ItemTIDs` · `RequiredUnlockTIDs` 모두 걸린다. */
export function lookupFor(column: string, lookup: NameLookup): string | null {
  for (const key of Object.keys(lookup)) {
    if (column.endsWith(key) || column.endsWith(`${key}s`)) {
      return key;
    }
  }
  return null;
}

/** 거르기 상자를 달 열과 그 값 목록. */
export function filterColumns(t: DataTable): Array<{ column: string; values: string[] }> {
  const result: Array<{ column: string; values: string[] }> = [];
  for (const c of t.columns.slice(1)) {
    if (NON_FILTER_COLUMNS.has(c)) {
      continue;
    }
    const numericOk = NUMERIC_FILTER_COLUMN.test(c);
    const values = [
      ...new Set(t.rows.map((r) => r[c]).filter((v) => typeof v === 'string' || (numericOk && typeof v === 'number'))),
    ];
    if (values.length < 2 || values.length > FILTER_MAX_DISTINCT) {
      continue;
    }
    values.sort((a, b) => (typeof a === 'number' && typeof b === 'number' ? a - b : String(a).localeCompare(String(b))));
    result.push({ column: c, values: values.map(String) });
  }
  return result;
}

/** 단위가 이름에 박힌 정수 열은 %를 곁들인다 — 천분율·만분율·백만분율. */
export function percentOf(column: string, value: Cell): string | null {
  if (typeof value !== 'number') {
    return null;
  }
  const scale = column.endsWith('PerMillion') ? 1_000_000 : column.endsWith('Permyriad') ? 10_000 : column.endsWith('Permille') ? 1_000 : 0;
  if (scale === 0) {
    return null;
  }
  return `${trimNumber((value / scale) * 100)}%`;
}

export function trimNumber(n: number): string {
  return Number(n.toFixed(4)).toString();
}

export function cellText(v: Cell): string {
  if (v === null || v === undefined) {
    return '';
  }
  if (Array.isArray(v)) {
    return v.map(cellText).join(', ');
  }
  if (typeof v === 'object') {
    return JSON.stringify(v);
  }
  return String(v);
}
