---
date: 2026-09-16
title: DB 작업 실패 경로 — OnFailed로 되돌리고 세션을 끊는다 · 밴/삭제 응답
tags: [server, db, protocol, test]
---

# DB 작업 실패 경로 — OnFailed로 되돌리고 세션을 끊는다 · 밴/삭제 응답

## 목적 / 배경
- 서버 코드리뷰 2번 항목. `DBManager.Post`의 잡이 `ExecuteAsync`에서 예외로 끝나면 `Apply`가 안 불렸고,
  예외는 Lib의 `Console.WriteLine`으로만 남았다. 로그인 중이면 클라가 무한 대기, 로그인 뒤면 메모리·DB가 갈라진 채 진행.
- 밴·삭제 계정도 `AccountRepository.Apply`가 `return`만 해서 같은 증상이었다.

## 변경 내용
- `Server/WSGameServer/Repository/IRepository.cs` — `User` 노출, `OnFailed(Exception)` 추가(기본 구현 = `User.OnDbFailed`).
- `Server/WSGameServer/DB/DBManager.cs` — try/catch로 실패를 `_logicExecutor.Post(() => repository.OnFailed(e))`로 되돌린다.
- `Server/WSGameServer/User/User.cs` — `OnDbFailed`: 로그인 전이면 `S_LoginResponse(DbError)` 전송 → `Destroy` + `CloseChannel`. `IsLoggedIn` 플래그 추가.
- `Server/WSGameServer/Repository/AccountRepository.cs` — 밴/삭제/행 없음 전부 응답 후 세션 종료.
- `Server/MikaProtocol/PacketEnum.cs` — `EResultCode.Banned(3)·Deleted(4)·DbError(5)`. 빌드 시 Unity로 미러링됨.
- `Server/MikaNetwork.Lib/.../MikaExecutor.cs` — `DBExecutor.JobFailed`·`LogicExecutor.JobFailed` 훅. `Console.WriteLine` 제거. `GameServer.Run`에서 `ServerLog.Error`로 연결.
- 테스트: `User/UserDbFailureTest.cs`(FakeDBQueue.FailWith), `Repository/AccountRepositoryTest.cs`·`DBManagerTest.cs`(첫 `:memory:` SQLite 테스트), `Repository/SqliteFixture.cs`.

## 주요 결정 / 근거
- **실패 정책은 "세션 종료" 하나다.** 재시도·부분 복구를 두지 않았다 — 메모리를 버리고 재접속 때 DB를 다시 읽는 것이
  갈라진 상태를 이어 가는 것보다 항상 안전하다. 재시도가 필요해지면 `OnFailed`를 Repository별로 override한다.
- `OnFailed`를 인터페이스 기본 구현으로 둔 이유: 9개 Repository가 전부 같은 정책이고, 다르게 할 곳이 생기면 그때 override한다.
- 로그인 뒤 실패에는 `S_LoginResponse`를 다시 보내지 않는다 — 클라가 로그인 화면으로 되돌아간다. `IsLoggedIn`이 그 구분이다.
- `SqliteFixture`의 DDL은 운영 DB와 손으로 맞춘 복사본이다. 스키마 `.sql`이 생기면 그 파일을 읽도록 바꾼다.

## 후속 작업 / 주의사항
- 세션 종료가 곧 종료 정산(`SettleWorkStation`)을 부르고 그것이 또 DB 쓰기를 낸다. 연쇄 실패 시 `Destroy`·`CloseChannel`이 멱등이라 무한 루프는 없다.
- 가챠·해금처럼 쓰기가 여러 잡으로 갈라진 곳은 한 잡만 실패하면 여전히 재화·아이템이 어긋난다 — 리뷰 4번(원자성)이 남아 있다.
- `DBExecutor`는 싱글턴이라 `DBManagerTest`가 프로세스에서 한 번만 `Start(1)`한다. 다른 테스트가 `Start`를 부르면 채널이 갈아끼워진다.
