---
date: 2026-11-20
title: 게임 DB를 WAL로 열고 로그 수준을 설정으로 뺀다 (T-192)
tags: [server, db, infra]
---

# 게임 DB를 WAL로 열고 로그 수준을 설정으로 뺀다

## 목적 / 배경
- 261107 측정에서 기본(rollback) 모드는 동접 500명에 `database is locked`로 세션이 끊겼고 WAL은 깨끗했다 → `tasks/T-192`, `Server/docs/서버-비용.md` 4장
- 패킷마다 Debug 로그(5분 70MB) — 운영에서 낮출 수 있게 설정만 연다. 기본값 결정은 사람 몫

## 주요 결정 / 근거
- `journal_mode=WAL`은 파일 속성이라 `DBManager.Initialize(string)`에서 한 번만, `synchronous=NORMAL`은 연결 속성이라 `OpenAsync`에서 `busy_timeout`(T-178, main에 이미 있음)과 같이 건다. `:memory:` 팩토리 오버로드엔 WAL을 걸지 않는다
- `DbMigrator`는 WAL을 몰라도 된다 — 풀링 끈 연결이라 닫을 때 WAL을 합친다
- **git 본체 문제**: 풀링 연결이 열려 있는 동안 쓰기는 `-wal`에만 있다. `GameServer.Dispose`에서 `Checkpoint()`(TRUNCATE + `ClearPool`) → Program이 `using`으로 Dispose. 엔터·Ctrl+C는 확실히 타고, SIGTERM(`docker stop`)·클라 서버 콘솔의 프로세스 강제 종료는 못 탈 수 있다 — 데이터는 `-wal`에 남아 다음 기동이 합치므로 손실은 아니고, 커밋 전 서버를 끄라고 `Server/CLAUDE.md`에 적었다
- `LogLevel`은 `GameSettings.LogLevel`(enum 이름 문자열 바인딩) → `Program`에서 `ServerLog.MinLevel`에 넣는다. GameServer 생성 전에 넣어야 기동 로그부터 적용된다

## 후속 작업 / 주의사항
- 다음에 개발 서버를 한 번 띄우면 `Shared/game.sqlite3` 헤더가 WAL로 바뀌어 바이너리 diff가 생긴다 — 정상, 커밋한다
- 운영(Docker) 로그 기본을 Info로 둘지는 미결 — compose에 주석으로만 자리를 둠
- 1000명 잠금 오류 11건(쓰기 묶기)은 남은 과제
- 부하 봇은 프로세스 이름으로 첫 프로세스를 잰다 — main 트리 개발 서버가 떠 있으면 그걸 잰다(CPU 0.3%로 나옴). exe 복사본 `WSLoadServer.exe` + `--process WSLoadServer`로 피했다 → `서버-비용.md` 5장
- 실측 확인: 엔터 종료 시 `-wal`·`-shm`이 사라지고 본체에 합쳐짐 · 강제 종료(taskkill)면 `-wal`이 남음
