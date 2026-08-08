// 기획 문서 옆에 있는 목업(.html)·이미지를 public/assets로 복사하고,
// "원본 상대경로 → 라우트" 대응표를 src/generated/assets.json에 남긴다.
// 로더는 이 표를 보고 문서 안의 링크를 바꾼다 — 슬러그 규칙이 두 곳에 흩어지지 않게 한다.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const WEB_ROOT = path.resolve(here, '..');
const DESIGN_DIR = path.resolve(WEB_ROOT, '..', 'design');
const OUT_DIR = path.join(WEB_ROOT, 'public', 'assets');
const MANIFEST = path.join(WEB_ROOT, 'src', 'generated', 'assets.json');

const COPY_EXT = new Set(['.html', '.png', '.jpg', '.jpeg', '.gif', '.svg', '.webp']);

function walk(dir, base = dir, out = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, base, out);
    else if (COPY_EXT.has(path.extname(e.name).toLowerCase())) {
      out.push(path.relative(base, p).split(path.sep).join('/'));
    }
  }
  return out;
}

function routeName(rel) {
  const ext = path.extname(rel);
  const base = path.basename(rel, ext);
  const ascii = base
    .toLowerCase()
    .replace(/[^a-z0-9\-]+/g, '-')
    .replace(/^-+|-+$/g, '');
  // 한글만으로 된 이름은 ascii가 비므로, 상위 폴더와 길이를 붙여 충돌을 막는다.
  const dir = path.dirname(rel).split('/').filter((s) => s !== '.').join('-');
  return [dir, ascii || 'asset', String(base.length)].filter(Boolean).join('-') + ext;
}

fs.rmSync(OUT_DIR, { recursive: true, force: true });
fs.mkdirSync(OUT_DIR, { recursive: true });
fs.mkdirSync(path.dirname(MANIFEST), { recursive: true });

const manifest = {};
for (const rel of walk(DESIGN_DIR)) {
  const name = routeName(rel);
  fs.copyFileSync(path.join(DESIGN_DIR, rel), path.join(OUT_DIR, name));
  manifest[rel] = `/assets/${name}`;
}

fs.writeFileSync(MANIFEST, JSON.stringify(manifest, null, 2) + '\n', 'utf8');
console.log(`목업·이미지 ${Object.keys(manifest).length}개를 public/assets로 옮겼습니다.`);
