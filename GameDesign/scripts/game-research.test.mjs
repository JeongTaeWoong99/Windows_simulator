import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { execFileSync } from 'node:child_process';
import { stampFor, validateArticle, publishArticle, buildPrompt } from './run-game-research.mjs';

const headings = ['누구에게 맞는가', '재미가 생기는 과정', '재미를 만드는 요소',
  '불편하거나 재미없는 순간', '직접 볼 관찰 과제', '우리 게임에 던지는 질문', '출처'];
const sources = ['https://store.steampowered.com/app/123/test/', 'https://example.org/developer', 'https://steamcommunity.com/app/123/reviews/'];
const fixture = () => ({
  game_id: 'test-game', title: 'Test Game', source_urls: sources,
  markdown: '# Test Game — 재미와 이용자 연구\n\n> 테스트 장르\n> 연계 판정: **학습 자료**\n\n## 한눈에\n- **무엇:** 게임별 이용 동기 연구\n\n' +
    headings.map(h => `## ${h}\n${'기능 사실과 이용자 관찰을 구분한다. '.repeat(30)}\n`).join('\n') +
    sources.map((url, i) => `[근거 ${i}](${url})`).join('\n'),
});

test('파일 날짜는 호스트 시간대와 관계없이 한국시간이다', () => {
  assert.deepEqual(stampFor(new Date('2026-11-17T21:00:00Z')), { date: '2026-11-18', hour: '06' });
});

test('출처와 필수 절을 갖춘 새 게임만 받는다', () => {
  assert.equal(validateArticle(fixture(), new Set()).game_id, 'test-game');
  assert.throws(() => validateArticle(fixture(), new Set(['test-game'])), /duplicate/i);
  assert.throws(() => validateArticle({ ...fixture(), game_id: 'another-id' }, new Set(), ['Test: Game']), /duplicate/i);
});

test('경로 탈출·출처 부족·단일 도메인·빈 연구·누락 절을 거절한다', () => {
  assert.throws(() => validateArticle({ ...fixture(), game_id: '../outside' }, new Set()), /game_id/);
  assert.throws(() => validateArticle({ ...fixture(), source_urls: sources.slice(0, 1) }, new Set()), /source/i);
  const singleDomain = ['https://a.test/1', 'https://a.test/2', 'https://a.test/3'];
  assert.throws(() => validateArticle({ ...fixture(), source_urls: singleDomain, markdown: fixture().markdown + singleDomain.join('\n') }, new Set()), /domain/i);
  assert.throws(() => validateArticle({ ...fixture(), markdown: '# empty' }, new Set()), /markdown/i);
  assert.throws(() => validateArticle({ ...fixture(), markdown: fixture().markdown.replace('## 누구에게 맞는가', '## 없음') }, new Set()), /누구에게/);
  assert.throws(() => validateArticle({ ...fixture(), markdown: fixture().markdown + '<img src=x onerror=alert(1)>' }, new Set()), /Unsafe/);
});

test('프롬프트에 기존 게임·출처 한계·사용자 허가와 현재 회차가 들어간다', () => {
  const prompt = buildPrompt({ existingIds: ['bongo-cat'], stamp: { date: '2026-11-17', hour: '12' } });
  assert.match(prompt, /bongo-cat/);
  assert.match(prompt, /2026-11-17/);
  assert.match(prompt, /인구통계/);
  assert.match(prompt, /허가/);
});

test('문서 빌드를 깨뜨리는 잘못된 퍼센트 링크를 게시 전에 거절한다', () => {
  assert.throws(() => validateArticle({ ...fixture(), markdown: fixture().markdown + '\n[할인 참고](50%)' }, new Set()), /markdown link/i);
});

test('게시기는 원래 브랜치·staged 변경을 보존하고 연구 한 편만 원격에 보낸다', () => {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'ws-research-test-'));
  const repo = path.join(tmp, 'repo');
  const origin = path.join(tmp, 'origin.git');
  const git = (cwd, ...args) => execFileSync('git', ['-c', 'core.autocrlf=false', ...args], { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  try {
    fs.mkdirSync(repo);
    git(repo, 'init', '--bare', origin);
    git(repo, 'init', '-b', 'main');
    git(repo, 'config', 'user.name', 'Research Test');
    git(repo, 'config', 'user.email', 'research-test@example.invalid');
    fs.writeFileSync(path.join(repo, 'README.md'), 'baseline');
    git(repo, 'add', 'README.md');
    git(repo, 'commit', '-m', 'baseline');
    git(repo, 'remote', 'add', 'origin', origin);
    git(repo, 'push', '-u', 'origin', 'main');
    fs.writeFileSync(path.join(repo, 'user.txt'), 'user work');
    git(repo, 'add', 'user.txt');
    const head = git(repo, 'rev-parse', 'HEAD');
    const result = publishArticle({ repoRoot: repo, runDir: path.join(tmp, 'run'), article: fixture(), stamp: { date: '2026-11-17', hour: '12' } });
    assert.equal(git(repo, 'rev-parse', 'HEAD'), head);
    assert.equal(git(repo, 'branch', '--show-current'), 'main');
    assert.equal(git(repo, 'diff', '--cached', '--name-only'), 'user.txt');
    const file = 'GameDesign/design/research/game-studies/2026-11-17-12-study-test-game.md';
    assert.match(git(repo, 'show', `origin/main:${file}`), /Test Game/);
    assert.equal(git(repo, 'diff-tree', '--no-commit-id', '--name-only', '-r', result.commit), file);
    assert.throws(() => publishArticle({ repoRoot: repo, runDir: path.join(tmp, 'again'), article: fixture(), stamp: { date: '2026-11-17', hour: '18' } }), /duplicate/i);
  } finally {
    if (path.resolve(tmp).startsWith(path.resolve(os.tmpdir()) + path.sep))
      fs.rmSync(tmp, { recursive: true, force: true });
  }
});
