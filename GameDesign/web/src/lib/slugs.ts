import path from 'node:path';

/// <summary>폴더는 이미 영문이라 그대로 쓰고, 한글 파일명만 표에서 영문 슬러그로 바꾼다</summary>
const FILE_SLUGS: Record<string, string> = {
  '게임기획코어': 'core',
  '기획평가': 'review',
  '문서관계도': 'doc-graph',
  '산업레벨': 'industry-level',
  '데스크톱종속형게임-사례조사': 'research-desktop-games',
  '키우기게임-사례조사': 'research-idle-games',
  '2026-07-30-종합제안정리': 'summary',
};

// 표에 없는 한글이 남으면 URL이 percent-encoding으로 깨지므로 눈에 띄게 남긴다.
function slugifyFile(base: string): string {
  if (FILE_SLUGS[base]) return FILE_SLUGS[base];
  const ascii = base
    .toLowerCase()
    .replace(/[^a-z0-9\-]+/g, '-')
    .replace(/^-+|-+$/g, '');
  return ascii || encodeURIComponent(base);
}

/**
 * 기획 문서 파일의 저장소 상대 경로를 라우트 슬러그로 바꾼다.
 * `gathering/fishing/README.md` → `gathering/fishing`
 * `gathering/산업레벨.md`        → `gathering/industry-level`
 */
export function designSlug(relPath: string): string {
  const parts = relPath.split(/[\\/]/);
  const file = parts.pop()!;
  const base = file.replace(/\.md$/i, '');
  if (base.toLowerCase() === 'readme') {
    return parts.join('/');
  }
  return [...parts, slugifyFile(base)].filter(Boolean).join('/');
}

/** 일감 파일명 `T-005-드롭테이블.md` → `t-005` (ID만 쓴다 — 한글 슬러그는 URL에서 깨진다) */
export function taskSlug(fileName: string): string {
  const m = fileName.match(/^(T-\d{3})/i);
  return (m ? m[1] : path.basename(fileName, '.md')).toLowerCase();
}
