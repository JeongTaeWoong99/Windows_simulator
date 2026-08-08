// @ts-check
import { defineConfig } from 'astro/config';
import tailwindcss from '@tailwindcss/vite';

// 정적 빌드만 한다. 배포처(GitHub Pages / Cloudflare Pages)는 아직 정하지 않았으므로
// site·base를 비워 둔다 — 하위 경로에 올릴 때 base를 빼면 CSS·링크가 전부 깨진다.
export default defineConfig({
  output: 'static',
  vite: {
    plugins: [tailwindcss()],
  },
  markdown: {
    shikiConfig: { theme: 'github-light' },
  },
});
