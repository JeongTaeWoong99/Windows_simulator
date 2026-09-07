---
date: 2026-08-29
title: 클라 소켓 루프를 스레드풀로 이설 (이슈 #19 B-1 · 서버 몫)
tags: [server, network, test]
---

# 클라 소켓 루프를 스레드풀로 이설 (B-1 서버 몫)

> 경위·후보 비교는 [`2026-08-28-ping-disconnect-cause.md`](2026-08-28-ping-disconnect-cause.md)와
> [이슈 #19](https://github.com/JeongTaeWoong99/Windows_simulator/issues/19)에 있다. 여기는 **결정 이후 실제로 넣은 것**만 적는다.
> 일감: [T-040](../../tasks/archive/T-040-하트비트해결방향.md)

## 목적 / 배경

**B를 B-1로 정했다** — 소켓 루프를 Unity 메인 스레드에서 떼어낸다.
A(판정 5초 ↔ 핑 주기 5초, 여유 0)는 **여전히 미정**이라 손대지 않았다.

## 주요 결정 / 근거

### `ConfigureAwait(false)`를 라이브러리 전역 기본으로 뒀다

`Task.Run` 안에서는 `SynchronizationContext.Current`가 `null`이라 **사실상 중복**이다.
그래도 붙인 이유는 이 계층이 호스트를 모르는 라이브러리이기 때문이다 — 캡처하지 않는 것이
기본값이어야 다음에 어디서 불려도 같은 함정을 다시 밟지 않는다.

### 컨텍스트 이탈만으로는 부족했다 — 종료 경로를 함께 고쳤다

스레드를 가르면 종료 경로가 **동시에 세 곳**에서 들어온다(수신 루프 종료·송신 실패·`StartAsync`의 `finally`).
`MikaServerSession`이 이미 답을 갖고 있어 그 수준으로 맞췄다.

- `Disconnect()` — `Interlocked.Exchange` 멱등 가드. 검사-후-대입은 둘이 함께 통과한다
- `ReceiveLoop` — `try/catch/finally`. **없으면 RST에 세션이 매달린다**(아래 함정)
- `IsConnected` — `volatile` 뒷필드. 루프가 다른 스레드의 쓰기를 못 볼 수 있다
- `Dispose()` — `_cts.Cancel()`을 소켓 파기보다 **먼저**. 순서가 반대면 송신 루프가 예외로 깨진다

> ⚠️ **의미가 하나 바뀌었다** — 예전에는 `StartAsync` 전에 `Disconnect()`를 부르면 조용히 반환하고
> `Disconnected`도 안 떴다. 이제는 그 경우에도 한 번 발화한다. `MikaServerSession`과 같은 동작이고,
> 구독자는 `MikaClient`의 `Debug.Log` 하나뿐이라 영향이 없다.

## 겪은 함정

**`ReceiveLoop`에 `catch`가 없으면 세션이 끝나지 못하고 매달린다.**
원격이 RST로 끊으면 `ReceiveAsync`가 던지고 → `ReceiveLoop`의 `Disconnect()`에 도달하지 못하고 →
`Task.WhenAll`은 **아직 대기 중인 `SendLoop`을 기다리는데** 그 `SendLoop`을 깨울
`_cts.Cancel()`은 `Disconnect()` 안에 있다. 서로를 기다리며 영원히 멈춘다.
회귀 테스트가 5초 타임아웃으로 실패하는 것으로 이게 드러났다(예외가 아니라 **행**이었다).

## 후속 작업 / 주의사항

- **이것만으로는 드래그 증상이 사라지지 않는다.** B-1은 *큐를 비우는 쪽*만 푼다 —
  `PingManager`가 핑을 `Update()`에서 만드는 한 드래그 중에는 **큐에 들어갈 핑 자체가 없다.**
  클라 몫(핑 생성을 백그라운드 타이머로)은 T-040에 남겼다.
- **검증은 빌드에서 한다.** 에디터에서는 창 제어 코드가 `#if !UNITY_EDITOR`라 재현되지 않는다.
  지난번에 `dotnet build`만 통과시키고 올렸다가 불완전한 수정이 나갔다.
- **`MikaNetwork.Client`는 자동 미러가 아니다.** `Assets/Scripts_Server/Network/MikaNetwork.Client/`는
  손으로 맞추는 사본이라 한쪽만 고치면 조용히 어긋난다. 이번에 중괄호 스타일까지 원본에 맞춰
  **`MikaClientSession.cs`·`MikaConnector.cs`는 바이트 단위로 같아졌다** — 이제 `diff`가 곧 어긋남이다.
  `MikaClient.cs`만 로그 줄이 다르다(`Console.WriteLine` ↔ `Debug.Log`).
- `MikaServerSession`도 `IsConnected` 가시성·`Dispose` 순서가 같은 상태다. 이미 스레드풀에서 돌아
  이번 증상과 무관해 건드리지 않았다 — T-040의 후속 항목.
- 테스트가 `WSGameServer.Tests`에서 `MikaNetwork.Client`를 직접 참조한다(WSGameServer는 참조하지 않는다).
  커버리지 측정 대상은 `WSGameServer`뿐이라 수치에는 영향이 없다.
