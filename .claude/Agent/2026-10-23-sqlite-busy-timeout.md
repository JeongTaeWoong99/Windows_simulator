---
date: 2026-10-23
title: 게임 DB 연결에 busy_timeout — RETURNING 커밋 잠김으로 세션이 끊기던 문제
tags: [server, db, test]
---

# 게임 DB 연결에 busy_timeout

## 목적 / 배경
- 이슈 #85 · → `tasks/T-180-DB잠김세션끊김.md` (원인 정리는 일감에)

## 변경 내용
- `Server/WSGameServer/DB/DBManager.cs` — `Post`·`RunAsync`가 공통 `OpenAsync()`로 연결을 열고 `PRAGMA busy_timeout = 5000`
- `Server/WSGameServer.Tests/Repository/DBManagerLockTest.cs` — 임시 파일 DB로 잠김 재현 2건

## 주요 결정 / 근거
- **단순 쓰기 경합은 원래도 기다렸다** — `Microsoft.Data.Sqlite`가 step의 BUSY를 `CommandTimeout`(30초)까지 재시도한다. 첫 테스트(쓰기 잠금 중 INSERT)는 수정 전에도 통과했다
- 실패하는 건 `INSERT … RETURNING` + 다른 연결의 읽기(SHARED) 조합 — 커밋이 reset에서 나 드라이버 재시도 밖이다. 그래서 드라이버 옵션(`Default Timeout`)이 아니라 SQLite 쪽 `busy_timeout`이 답이다
- WAL로 바꾸는 안은 기각 — `game.sqlite3`를 git에 커밋하는데 `-wal` 파일에 남은 쓰기가 커밋에서 빠질 수 있다

## 후속 작업 / 주의사항
- 새 리포지토리가 연결을 직접 만들지 말고 `DBManager`를 거쳐야 `busy_timeout`이 걸린다
- `DbMigrator`의 기동 시 연결은 서버가 혼자 쓸 때라 손대지 않았다
