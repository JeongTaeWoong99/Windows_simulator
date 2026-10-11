---
date: 2026-10-11
title: 게임·경매장 서버 Docker 원격 배포 · appsettings 설정 · 클라 접속 주소 (T-109)
tags: [server, infra, client, docs]
---

# 게임·경매장 서버 Docker 원격 배포

## 목적 / 배경
- 친구들이 이 PC의 서버 하나에 같이 붙어 거래소·경매장을 확인한다 → `tasks/T-109` · 운영법 → `Server/docs/도커.md`

## 변경 내용
- 게임 서버: `Common/GameSettings.cs` + `appsettings.json`(JSON → 환경 변수 → 명령줄) · 포트 30329 · `NetworkManager(port)` · `User.CheatAdminLevel` 설정화 · `Program.WaitForShutdown`
- 경매장: 포트 30330(코드 변경은 기본값뿐 — 원격 값은 compose 환경 변수)
- `DbMigrator.MigrateFile` — 파일이 없으면 새로 만들고 백업은 건너뛴다 · `DBManager.ResolveDbPath` — 절대 경로는 그대로
- Docker: `Server/docker-compose.yml` · Dockerfile 2개 · `Server/.dockerignore`
- Unity: `ServerEndpoint`(빌드 `StreamingAssets/ServerSettings.json` · 에디터 `EditorPrefs`) · `Editor/ServerTargetMenu` · `NetworkManager`가 그것으로 접속

## 주요 결정 / 근거
- **원격 DB는 빈 파일에서 시작한다** — 시드 복사(entrypoint)를 버렸다. 계정은 로그인 때 생기고 스키마는 마이그레이션이 채운다. 개발 DB의 테스트 계정이 원격에 섞이지 않는다.
- 원격 전용 설정 파일(`appsettings.Remote.json`)을 두지 않는다 — 다른 값은 compose `environment` 한 곳에만.
- `WS_AUCTION_*` 환경 변수를 없애고 `Game:Auction` 절로 합쳤다 — 설정 경로가 둘이면 어느 쪽이 이겼는지 로그만으로 모른다.
- 치트 하한은 정적 설정값. 바꾸는 테스트는 `DisableParallelization` 묶음(`CheatLevelCollection`)에 넣었다 — 다른 치트 테스트와 동시에 돌면 서로 깨진다.
- 경매장은 `127.0.0.1:30330`으로만 호스트에 연다(사용자 요청 "로컬에서 접속"). 바깥 공개는 T-110.
- 클라 빌드 주소는 JSON 파일 — 공인 IP가 바뀌어도 다시 빌드하지 않는다.

## 후속 작업 / 주의사항
- ⚠️ Docker를 켜 둔 PC에서 30329·30330 LISTENING 주인은 `com.docker.backend.exe`다. 클라 서버 콘솔 `ServerRunner.KillByPort`를 새 포트로만 바꾸면 Docker를 죽인다 — 이슈로 넘김.
- ⚠️ 개발 게임 서버를 Docker 경매장에 붙이면 원격 게임 서버와 경매 이벤트(정산)를 나눠 받는다 — 경매장은 게임 서버 하나 전제(T-110).
- 서버가 예전엔 stdin EOF에서 바로 꺼졌다(`sleep 7200 |`로 붙들던 이유) — 이제 신호를 기다린다.
- Git Bash에서 `docker compose exec game sqlite3 /data/...`는 경로가 `C:/Program Files/Git/data`로 바뀐다 — `MSYS_NO_PATHCONV=1`.
- Unity `.meta`는 손으로 만들었다(워크트리라 에디터가 못 본다).
- 테스트 출력 폴더의 `appsettings.json`은 경매장 것이 이긴다(두 프로젝트가 같은 이름을 복사) — 게임 설정 테스트는 임시 폴더를 쓴다.
