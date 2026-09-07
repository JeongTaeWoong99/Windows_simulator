---
date: 2026-08-28
title: 하트비트 끊김 — 원인 규명, 시험 적용, 그리고 철회 (이슈 #19)
tags: [client, server, infra, docs]
---

# 하트비트 끊김 — 원인 규명, 시험 적용, 그리고 철회

> **결론부터**: 원인 두 겹을 모두 찾아 고쳤고 빌드 실측으로 증상이 사라지는 것까지 확인했지만,
> **효과를 낸 수정이 서버 담당 폴더라 전부 되돌렸다.** 지금 `main`은 수정 전 상태다.
> 방향 결정은 [이슈 #19](https://github.com/JeongTaeWoong99/Windows_simulator/issues/19) ·
> 일감은 [T-040](../../tasks/archive/T-040-하트비트해결방향.md)(보류).

## 목적 / 배경

"아무것도 안 했는데 서버와의 연결이 끊어졌다는 알림이 뜬다"는 제보에서 출발했다.
사용자 추측은 "핑이 중간에 비활성화되나? UI를 닫으면 꺼지나?"였는데 **둘 다 아니었다.**

## 확인한 사실 (원인은 두 겹이었다)

### A — 여유가 0이다 (수십 분에 한 번, 무작위)

- `Server/WSGameServer/Common/Global.cs`의 `SessionIdleTimeout`이 **5초**,
  `PingManager.PingIntervalSeconds`도 **5초**. 여유가 없다.
- 서버 `MikaServer.DisconnectIdle`은 `now - LastReceivedAt >= 판정`이면 **즉시** 끊는다(`>` 아님).
- 클라 실제 간격은 `_nextPingTime = now + 5f`라 **프레임 오버슈트가 누적**돼 5.008초쯤 된다.
  → 매 핑 직전 수 ms가 판정선 밖이고, 5초 주기 스윕이 그 창을 밟으면 끊긴다(확률 ~0.2%).
- `Global.cs`의 주석은 아직 **"세 번 연속 놓쳐야 끊기는 값"** 이다 — 15초였던 시절의 잔재.
- 이 값은 `d2c52c6`에서 `GatherSpeedMultiplier = 6.0`과 **함께 내려온 확인용 값의 짝**이다
  (= [T-004](../../tasks/T-004-전역배수복귀.md)).
- 전조는 이미 [`2026-08-14-editor-server-console.md`](2026-08-14-editor-server-console.md) 94행에
  "남아 있는 문제"로 적혀 있었다.

### B — 세션 루프가 Unity 메인 스레드에 묶여 있다 (드래그 5초 = 확정)

> ⚠️ **먼저 알아야 할 전제 — 창 드래그는 없앨 수 없다.**
> 이 게임은 **데스크톱 상주 방치형**이라 창이 배경에 얹혀 보여야 해서 **OS 타이틀바를 지웠다**
> (보더리스 + 투명). 타이틀바를 지우면 **창을 옮길 손잡이도 사라지므로**
> **UI 패널(`WindowDragArea`)이 타이틀바 역할**을 한다 — 창 이동은 빼도 되는 기능이 아니라
> **타이틀바를 포기한 대가로 반드시 있어야 하는 기능**이다.
> 이동은 직접 좌표 계산 대신 OS에 위임하는데, 그래야 **스냅 · 더블클릭 최대화 · 모니터 간 이동 ·
> DPI 전환**이 공짜로 따라온다(`DesktopWindow 규칙.md` §5-7).
> **이 위임은 데스크톱 상주형의 특수 사정이다** — 전체화면 게임은 타이틀바가 있어 이 코드도 이 문제도 없다.
> 그래서 해결책은 "드래그를 없앤다"가 될 수 없고, **소켓을 메인 루프에서 떼거나 위임을 직접 구현으로
> 바꾸거나** 둘 중 하나다.

- `NetworkManager.Start()`가 `async void`라 **메인 스레드**에서 돌고,
  거기서 부른 `Session.StartAsync()`의 `ReceiveLoop`/`SendLoop`이 첫 `await`에서
  **`UnitySynchronizationContext`를 캡처**한다.
- 그 뒤 `_socket.SendAsync`/`ReceiveAsync`의 continuation까지 전부 메인 스레드 큐로 가고,
  **그 큐는 플레이어 루프가 돌 때만 비워진다.**
- `WindowManager.BeginWindowDrag`(`WindowManager.cs:623`)는 `ReleaseCapture` +
  `WM_SYSCOMMAND(SC_MOVE_HTCAPTION)`으로 창 이동을 **OS 모달 루프에 위임**한다.
  이 동안 Unity 메인 루프가 통째로 멈추므로 **소켓도 함께 멈춘다.**
- **에디터에서는 재현되지 않는다** — `BeginWindowDrag`가 `#if !UNITY_EDITOR`다.

### 아니었던 것 (다시 파지 말 것)

- ❌ UI를 닫아서 꺼지는 것 — `PingManager`·`NetworkManager` 모두 **씬 루트 오브젝트**
  (`m_Father: {fileID: 0}`, `m_IsActive: 1`). UI 개폐와 무관.
- ❌ 구독이 풀리는 것 — `_isSubscribed` 가드·`OnEnable` 재기준화 정상.
- ❌ 포커스 상실 — `runInBackground: 1`.

## ⚠️ 첫 진단이 틀렸던 지점 (같은 함정을 반복하지 말 것)

1차 수정에서 **"송신 경로는 이미 메인 스레드와 무관하다 — `MikaSendQueue`가 `ConcurrentQueue` 기반이고
송신 루프도 스레드풀에서 돈다"** 고 적었다. **틀린 말이었다.**

> **"자료구조가 스레드 안전하다"와 "그 코드가 다른 스레드에서 돈다"는 완전히 다른 말이다.**
> `MikaSendQueue`가 스레드 안전한 것은 **아무 스레드에서나 넣을 수 있다**는 뜻일 뿐,
> **꺼내서 보내는 `SendLoop` 자체는 캡처된 `UnitySynchronizationContext`에 묶여 있었다.**

그래서 1차 수정(핑 송신만 백그라운드 타이머로 이설)은 **큐에 쌓이기만 하고 나가지 않았다.**
사용자가 빌드 테스트로 "드래그를 꾹 누르니 5초 후 그대로 끊긴다"고 잡아냈다.

**놓쳤던 결정적 단서**: `ConfigureAwait`·`Task.Run`이 **코드베이스 전체에 0건**이었다.
async 코드가 있는데 이 둘이 하나도 없으면 **전부 캡처된 컨텍스트로 돌아온다**는 뜻이다.

> 📌 **절차 반성** — `dotnet build`만 통과시키고 커밋했다가 불완전한 수정을 올렸다.
> **런타임 증상은 런타임으로 검증한다.** 빌드 성공은 검증이 아니다.

## 실제로 효과가 있었던 수정 (지금은 되돌아가 있다)

```csharp
// Assets/Scripts_Server/Network/MikaNetwork.Client/MikaClient.cs
_ = Task.Run(() => Session.StartAsync());   // 원래: _ = Session.StartAsync();
```
\+ `MikaClientSession`의 `await` 6곳에 `.ConfigureAwait(false)`.

이걸 넣자 **드래그를 오래 끌어도 핑이 정상적으로 오갔고 끊기지 않았다** (사용자 빌드 실측).

## 왜 되돌렸는가

1. **효과를 낸 것이 서버 담당 폴더**(`Assets/Scripts_Server/Network/`)였다.
   CLAUDE.md 협업 규칙상 상대 폴더 변경은 합의 후다.
2. **유일한 답이 아니다.** 후보가 여럿이고 각각 **경제 밸런스**(좀비 부당 적립 구간) ·
   **창 제어 설계**(OS 위임을 버릴 것인가) · **패킷 추가**와 얽힌다.
   클라가 단독으로 고를 문제가 아니다.

되돌린 커밋 `45b55c6` → 되돌림 커밋 `2fb9dcb`. **revert의 revert로 그대로 되살릴 수 있다.**
`T-037`(판정 15초 복구)도 함께 철회했다 — **답 하나를 이미 정해 둔 일감**이라 판단을 받는다는
취지와 어긋났다.

## B-1(스레드풀 이설)을 택할 경우 미리 확인해 둔 것

"네트워크 스레드로 옮기면 Unity API를 건드리지 않나"를 먼저 훑어 뒀다. **안전하다.**

- `MikaPacketManager.Register`는 **역직렬화만** 네트워크 스레드에서 하고 핸들러는 job으로 넘긴다.
  `_handlers`는 생성자 이후 읽기 전용 → `ServerPacketHandler`는 **그대로 메인 스레드 실행**.
- `Dispatching`·`UnknownReceived` 훅은 **Unity 측 구독자가 0명**이다.
- `NetworkMessageQueue.Instance`는 `Lazy<T>`(기본 `ExecutionAndPublication`) → 스레드 안전.
- 송신 훅 `ClientLogger.OnPacketSent`는 `SendPacket`을 **부른 스레드**에서 돈다.
  Presenter 송신은 메인 스레드 그대로, 핑은 `QuietPacketIds`라 `Debug.Log` 전에 조기 반환.
- ⚠️ **`MikaClientSession.Disconnect()`에 멱등 가드가 없다.** `MikaServerSession`은 같은 문제를
  `Interlocked.Exchange`로 막아 뒀는데 **Unity 사본만 안 돼 있다.** 스레드가 갈리면 드러난다.

## 다음 사람에게

- **먼저 [이슈 #19](https://github.com/JeongTaeWoong99/Windows_simulator/issues/19)를 읽는다.**
  **A(여유 0)와 B(메인 스레드 종속)는 독립된 문제라 각각 하나씩, 총 두 개를 고른다.**
  후보 A-1~A-3 / B-1~B-4와 트레이드오프가 전부 거기 있다. 여기 로그는 **경위**만 담는다.
- **재현은 반드시 빌드에서.** 에디터에서는 B가 안 나온다.
- 값을 고칠 때 **한쪽만 바꾸지 않는다** — 판정과 주기의 여유가 다시 0이 된다.
