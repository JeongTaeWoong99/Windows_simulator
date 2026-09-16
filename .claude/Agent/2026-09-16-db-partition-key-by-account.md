---
date: 2026-09-16
title: DB 작업 파티션 키를 SessionId에서 계정(Pid 해시)으로
tags: [server, db, test]
---

# DB 작업 파티션 키를 SessionId에서 계정(Pid 해시)으로

## 목적 / 배경
- 서버 코드리뷰에서 발견. `DBExecutor`는 `Key % 채널수`로 잡을 배정해 같은 Key만 순서를 지키는데,
  모든 Repository의 `Key`가 `User.SessionId`였다.
- 중복 로그인으로 밀려난 세션의 종료 정산 쓰기(`AddItemRepository`)와 새 세션의 `LoginRepository`
  읽기가 SessionId가 달라 다른 채널에서 병렬로 돌았다. 새 세션이 낡은 값을 읽고 "확정값 저장"으로
  덮어쓰면 마지막 수확·재화·경험치가 사라진다.

## 변경 내용
- `Server/WSGameServer/User/User.cs` — `DbKey` 추가(Pid의 FNV-1a 64비트 해시). 생성자에서 계산.
- `Server/WSGameServer/Repository/*.cs` — 9개 Repository의 `Key`를 전부 `User.DbKey`로.
- `Server/MikaNetwork.Lib/MikaNetwork.Server/MikaExecutor.cs` — "sessionId로 파티션" 주석을 Key 일반형으로.
- `Server/WSGameServer.Tests/Repository/RepositoryKeyTest.cs` — 같은 pid·다른 sid는 키가 같고, 다른 pid는 다르다.

## 주요 결정 / 근거
- **Uid가 아니라 Pid 해시**: `AccountRepository`가 돌기 전에는 Uid를 모른다. 로그인 첫 잡부터 같은 채널이어야 한다.
- **`string.GetHashCode()` 대신 FNV-1a**: 프로세스마다 값이 달라져 로그로 채널을 추적할 수 없다.
- 테스트 이음새는 `IRepository.Key`다. DBExecutor의 채널 배정을 직접 검증하지 않는다 — 그건 Lib 내부 구현이다.

## 후속 작업 / 주의사항
- 새 Repository를 만들 때 `Key => User.DbKey`로 둔다. `SessionId`를 쓰면 이 버그가 되살아난다.
- 리뷰에서 함께 나온 미해결 항목: 종료 시 큐 드레인 없음, 가챠·해금 비원자 쓰기, 스키마 `.sql` 부재,
  `C_AddItemRequest` 권한 검사 없음, 슬롯이 미보유 캐릭터를 참조해도 채취가 도는 문제(DB에 실제 오염 행 있음).
  DB 실패 무응답은 같은 날 처리했다 → `2026-09-16-db-failure-path.md`.
