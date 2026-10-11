---
date: 2026-10-11
title: 서버 기동 시 DB 마이그레이션 자동 적용 (T-146)
tags: [server, db, test, infra]
---

# 서버 기동 시 DB 마이그레이션 자동 적용

## 목적 / 배경
- 10-07 "마이그레이션 없음"을 뒤집었다 — 원격 Docker DB(T-109)에 친구 데이터가 쌓이면 덮어쓸 수 없다. 10-07의 원인(사람이 SQL을 안 돌림)은 자동 적용으로 없앤다 → `tasks/T-146`

## 변경 내용
- `WSGameServer/DB/DbMigrator.cs` — `LoadFrom`(파일명·번호 검사) · `Apply`(판보다 큰 번호, 파일당 트랜잭션) · `MigrateFile`(백업 + 적용 + 로그)
- `GameServer.Run` — `DBManager.Initialize` 직전에 `MigrateFile` · `DBManager.ResolveDbPath`를 public으로
- `Shared/migrations/0001-baseline.sql` — 지금 DB의 `sqlite_master` 덤프(테이블 15 · 인덱스 6)를 `IF NOT EXISTS`로 · csproj가 `Migrations/`로 복사
- `SqliteFixture` — 손으로 베낀 DDL 5묶음을 지우고 `CreateSchema()`(마이그레이션 적용) 하나로 · 호출처 12파일 교체
- `game.sqlite3` — 서버를 띄워 판 0 → 1 (스키마·행 38개 그대로 확인)

## 주요 결정 / 근거
- `game.sqlite3`는 git에 남긴다 — 새로 받은 사람이 바로 띄운다. DB 커밋을 빠뜨려도 받은 쪽 서버가 스스로 올린다.
- `user_version`을 **마이그레이션과 같은 트랜잭션에서** 올린다 — 따로 올리면 실패한 파일이 "적용됨"으로 남는다.
- 번호 겹침·빈 번호·DB 판이 더 높음 → 기동 중단. 두 사람이 따로 번호를 따면 어느 쪽이 먼저인지 모른다.
- 백업은 `File.Copy`가 아니라 SQLite 백업 API — 저널이 WAL로 바뀌어도 안전하다. 마이그레이터 커넥션은 `Pooling=false`(기동 뒤 파일을 붙잡지 않게).
- 경매장 DB(`AuctionStore`)는 자체 판·단계 변환을 그대로 둔다.

## 후속 작업 / 주의사항
- 실제 DB의 `t_character`에 10-06 `drop-aptitude-bonus`가 **적용되지 않은 채** 적성 보너스 5컬럼이 남아 있다(0001에 그대로 들어갔다). 지우려면 `0002`로.
- `BoxSheetTest` 2개가 main에서 실패한다 — `a032487`(T-112 상자 보상 개편) 데이터와 테스트 기대값(상자 15종·상자 풀에 상자 없음)이 어긋난다. 이번 작업과 무관.
- 마이그레이션 파일에 `BEGIN`/`COMMIT`·`VACUUM`·`PRAGMA journal_mode`를 쓰면 트랜잭션 안이라 실패한다.
