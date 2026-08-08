import path from 'node:path';
import { fileURLToPath } from 'node:url';

// GameDesign/web/src/lib → GameDesign/web → GameDesign → 저장소 루트
// ⚠️ web/이 GameDesign/ 아래라 한 단계가 더 깊다. 여기가 틀리면 문서를 하나도 못 읽는다.
const here = path.dirname(fileURLToPath(import.meta.url));
export const REPO_ROOT = path.resolve(here, '..', '..', '..', '..');

export const TASKS_DIR = path.join(REPO_ROOT, 'tasks');
export const ARCHIVE_DIR = path.join(TASKS_DIR, 'archive');
export const DESIGN_DIR = path.join(REPO_ROOT, 'GameDesign', 'design');

/** 목업·이미지가 복사되어 나가는 자리 (public/ 아래). */
export const ASSET_ROUTE = '/assets';

// 사이트에 없는 저장소 파일(CLAUDE.md·스킬·코드)은 죽은 링크로 두지 않고 GitHub으로 보낸다.
export const GITHUB_BLOB = 'https://github.com/JeongTaeWoong99/Windows_simulator/blob/main';
