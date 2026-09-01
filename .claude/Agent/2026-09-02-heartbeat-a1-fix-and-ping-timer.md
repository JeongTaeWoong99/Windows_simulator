---
date: 2026-09-02
title: 하트비트 — A-1 적용 정정 (클라 타이머분은 롤백, T-041로 분리)
tags: [server, client, infra, design]
---

# 하트비트 — A-1 적용 정정 (클라 타이머분은 롤백)

> **결론부터**: 서버 값(A-1)과 공용 규칙까지 들어갔다.
> **클라 타이머분은 같은 날 롤백해 [T-041](../../tasks/T-041-클라핑타이머.md)로 뗐다** — 아래 업데이트 참조.
> 경위는 [`2026-08-28-ping-disconnect-cause.md`](2026-08-28-ping-disconnect-cause.md) ·
> B-1 서버 몫은 [`2026-08-29-client-session-threadpool.md`](2026-08-29-client-session-threadpool.md) ·
> 일감은 [T-040](../../tasks/T-040-하트비트해결방향.md).

## 목적 / 배경

`add9b9f`("이슈 #19 수정했습니다 A-1, B-1")를 일감에 반영하려고 실제 diff를 대조하다가
**A-1이 엉뚱한 값에 들어간 것**을 발견했다. B의 클라 몫도 아직 없었다. 둘 다 이번에 처리했다.

## 변경 내용

### 서버 — A-1을 제자리에 넣었다

- `Server/WSGameServer/Common/Global.cs`
  — `SessionIdleTimeout` **5초 → 15초**. 불변식(핑 주기 ≤ 판정 ÷ 3)과 T-004 경고를 주석에 추가.
- `Server/WSGameServer/Common/SessionWatchdog.cs`
  — `Interval` **15초 → 5초**, `private` → `internal`(테스트가 읽는다).
  "여기를 늘려 끊김을 줄이려 하지 말라"는 경고 주석 추가.
- `Server/WSGameServer.Tests/Network/SessionIdleSweepTest.cs`
  — `검사_주기는_판정_시간보다_짧다` 신설. **198개 전부 통과.**

### 클라 — 핑 *생성*을 타이머로 옮겼다 → ⛔ **되돌렸다 (아래 업데이트)**

- ~~`Assets/Scripts_Client/Managers/PingManager.cs`~~ **롤백됨. 지금 코드에 없다.**
  — 송신을 `Update()`에서 `System.Threading.Timer`로 이설 · 시간 기준을 `Stopwatch`(단조 시계)로 ·
  긴 프레임 정지 뒤 오탐 알림을 막는 `FrameStallSeconds` 가드 · `OnDisable`/`OnDestroy` 타이머 정리 ·
  타이머 스레드 예외를 메인 스레드에서 대신 찍는 `LogTimerErrorIfAny`.

### 문서 — 불변식을 규칙으로 못 박았다

- `GameDesign/design/workslot/README.md` 3.3 — 부등식 세 개를 표로 + 두 번 어긴 경위.
- `GameDesign/design/게임기획코어.md` 5장 하트비트 행에 한 줄 요약.

## 주요 결정 / 근거

- **`add9b9f`가 바꾼 것은 판정 시간이 아니라 검사 주기였다.** `SessionWatchdog.Interval`은
  *얼마나 자주 들여다보는가*일 뿐이라, 늘려도 **여유는 그대로 0**이고 증상의 빈도만 1/3로 준다.
  게다가 끊김이 최대 15초 늦어져 **좀비 세션 수명이 오히려 늘었다**(이슈 #10의 취지와 충돌).
  같은 파일 주석이 *"`SessionIdleTimeout`보다 충분히 짧게 둔다"* 고 이미 적고 있었다.

- **판정 15초는 새로 정한 값이 아니다.** [작업슬롯 3.3](../../GameDesign/design/workslot/README.md)이
  2026-08-04에 확정한 값이고, 기존 테스트도 15초를 전제로 짜여 있었다.
  **코드가 5초로 내려가 있던 것이 회귀였다** — A-1은 "복구"에 가깝다.

- **핑 주기는 5초를 유지했다.** `45b55c6`에 딸려 있던 2초는 **A-2**이고, A-1을 택한 이상
  판정 15초 ÷ 3 = 5초가 규칙에 맞는 값이다. 트래픽을 2.5배로 늘릴 이유가 없다.

- **`45b55c6`의 틀린 주석은 옮기지 않았다.** 그 커밋은 *"송신 루프는 이미 스레드풀에서 돈다"* 고
  적었지만 **당시엔 사실이 아니었고**, 그래서 1차 수정이 무효였다(경위 로그의 "첫 진단이 틀렸던 지점").
  지금 사실이 된 것은 **B-1 덕분**이라, 둘이 한 쌍이라는 것을 주석에 명시했다.

- **테스트로 지킬 수 있는 것만 테스트로 지켰다.** 서버는 클라 핑 주기를 모르므로
  `검사 주기 < 판정 시간`만 단언하고, `핑 주기 ≤ 판정 ÷ 3`은 **문서와 양쪽 주석**으로 묶었다.

## 후속 작업 / 주의사항

- 🔴 **빌드 실측 전에는 T-040을 닫지 않는다.** 에디터에서는 B가 재현되지 않는다(`#if !UNITY_EDITOR`).
  **2026-08-28에 `dotnet build`만 통과시키고 불완전한 수정을 올린 전례가 있다.**
  확인할 것 둘 — ① 창을 10초 이상 끌어도 안 끊긴다 ② 몇 시간 켜 둬도 무작위 끊김이 없다.
- ⚠️ **[T-004](../../tasks/T-004-전역배수복귀.md)가 배포 전 게이트가 됐다.**
  판정 15초는 채취 기준 주기 30초 기준으로 안전한데, `GatherSpeedMultiplier = 6.0`이면
  실효 주기가 5초라 좀비가 3주기를 부당 적립한다. 개발 중엔 무해하다.
- **값 셋은 함께 본다** — `Global.SessionIdleTimeout` · `SessionWatchdog.Interval` ·
  `PingManager.PingIntervalSeconds`. 한쪽만 고치면 이번 사고가 그대로 재발한다.
- **커밋하지 않았다.** 작업 트리에 서버 상수·테스트·기획 문서가 올라와 있다.


## 업데이트 (2026-09-02) — 클라 변경분 롤백

**`PingManager.cs` 수정을 전부 되돌렸다**(`git checkout --`). 사용자 요청이며,
**서버 값·테스트·기획 문서는 그대로 남는다.**

- 되돌린 것: `Assets/Scripts_Client/Managers/PingManager.cs` **하나뿐**이다.
  같은 시각 작업 트리에 있던 `PlayerDataModel.cs`·`ServerPacketHandler.cs`·`Protocol` 미러는
  **병행 중이던 Currency→Gold 리팩터링분**이라 건드리지 않았다.
- 남긴 것: `Global.cs`(판정 15초) · `SessionWatchdog.cs`(검사 5초, `internal`) ·
  `SessionIdleSweepTest`(불변식 테스트) · `workslot/README.md` 3.3 · `게임기획코어.md`.
- **불변식은 롤백 뒤에도 성립한다** — 원상복구된 `PingIntervalSeconds`가 **5초**라
  `5 ≤ 15 ÷ 3`을 만족한다. 그래서 기획 문서의 규칙 서술을 되돌릴 이유가 없었다.

> ⚠️ **드래그 증상(B)은 다시 재현되는 상태다.** B-1(서버, 큐를 비우는 쪽)만 남아 있고
> **큐에 넣는 쪽이 여전히 `Update()`** 다. 되살릴 조각과 **가져오면 안 되는 것**
> (핑 주기 2초 = A-2 · `45b55c6`의 틀린 주석)은 [T-041](../../tasks/T-041-클라핑타이머.md)과
> **[GitHub 이슈 #21](https://github.com/JeongTaeWoong99/Windows_simulator/issues/21)** 에 정리했다.
