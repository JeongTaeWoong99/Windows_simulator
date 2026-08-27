---
date: 2026-08-28
title: "이유 없이 연결이 끊긴다" 원인 규명 — 핑 여유 0 · 창 드래그 정지
tags: [client, network, docs]
---

# "이유 없이 연결이 끊긴다" 원인 규명 — 핑 여유 0 · 창 드래그 정지

## 목적 / 배경

- 사용자 증상: **아무것도 안 했는데 가끔 "서버와의 연결이 끊어졌습니다" 알림이 뜬다.**
- 사용자 추측은 "핑이 중간에 비활성화되나 / UI를 닫으면 꺼지나"였다. **둘 다 아니었다.**

## 원인 (둘)

### 1. 판정과 주기가 같아 여유가 0이었다 (주원인)

서버 `Global.SessionIdleTimeout = 5초` == 클라 `PingIntervalSeconds = 5f`.
서버는 `MikaServer.DisconnectIdle`에서 `now - LastReceivedAt >= 판정`이면 **즉시** 끊는다.

지뢰는 여기다 — `_nextPingTime = now + 5f`가 **프레임 오버슈트를 매번 누적**해
실제 송신 간격이 5.008초쯤 된다. 그래서 매 핑 직전 **수 ms 동안 판정선을 넘긴 상태**가 되고,
5초 주기 스윕이 그 창을 밟을 확률이 스윕당 0.2% 남짓 → **수십 분에 한 번 무작위 끊김.**
"가끔씩·이유 없이"의 통계적 정체가 이것이다.

> ⚠️ 이 문제는 **`.claude/Agent/2026-08-14-editor-server-console.md`에 이미
> "남아 있는 문제"로 적혀 있었다.** 그때는 로그인 전 무핑만 고치고 여유 0은 그대로 뒀다.
> **기록해 둔 미해결 항목이 2주 뒤 증상으로 돌아온 사례다.**

### 2. 창을 끄는 동안 메인 루프가 통째로 멈춘다 (부원인)

`WindowManager.BeginWindowDrag`는 `WM_SYSCOMMAND(SC_MOVE_HTCAPTION)`으로 **OS 모달 이동 루프**에
위임한다 — 마우스를 놓을 때까지 `Update`가 한 번도 안 돈다. 핑이 `Update`에 있으면
**드래그 시간만큼 0개**가 나간다. 판정 5초에서는 5초만 끌어도 확정 끊김.
절전·긴 GC 히치도 같은 경로다. (데스크톱 위젯 앱이라 드래그가 일상 동작이다)

### 사실이 아니었던 것 — 다시 파지 않도록

- `Ping Manager`·`Network Manager`는 **씬 루트의 항상 활성 오브젝트**다
  (`Assets/Scenes/Original/DesktopWindow_Control.unity`, `m_Father: {fileID: 0}` · `m_IsActive: 1`).
  **어떤 UI 계층에도 속하지 않아 화면을 닫아도 꺼지지 않는다.**
- 구독·재기동 로직에 결함 없음 (`_isSubscribed` 가드 · `OnEnable`의 기준 재설정 정상).
- 포커스 상실도 아니다 — `ProjectSettings.asset` `runInBackground: 1`.

## 변경 내용

- `Assets/Scripts_Client/Managers/PingManager.cs` — 송신을 `System.Threading.Timer`로 이설 ·
  주기 5초 → 2초 · 시간 기준을 `Stopwatch`로 · 프레임 정지 가드 추가 · 타이머 Dispose 경로.
- 문서 3건 — `Managers 규칙.md`(근거 두 절 신설) · `서버 동작 이해.md` · `Log 규칙.md`.
- 일감 — `tasks/T-037`(서버 판정 복구) 신규, `tasks/README.md` INDEX·순서 메모.

## 주요 결정 / 근거

- **서버 `Global.cs`를 직접 고치지 않았다.** 서버 담당 폴더라 합의 대상이고,
  이 값은 [T-004](../../tasks/T-004-전역배수복귀.md)(채취 배수 6.0)와 **같은 커밋 `d2c52c6`의 짝**이라
  원복을 함께 판단해야 한다 → [T-037](../../tasks/T-037-세션판정시간복구.md).
  대신 **서버가 5초인 채로도 증상이 안 나도록** 클라에서 여유를 만들었다.
- **주기를 2초로 정한 근거는 "판정의 절반 이하"** 다. 한 번 놓쳐도 살아남는 여유가 그것뿐이다.
  서버가 15초로 복구돼도 2초는 그대로 유효하므로 되돌릴 필요가 없다.
- **송신만 스레드로 옮기고 판정·알림은 `Update`에 남겼다.** 송신 경로는 이미 메인 스레드와
  무관하지만(`MikaSendQueue`가 `ConcurrentQueue`, `SendLoop`가 스레드풀), 판정은 `ServerWaitManager`·
  화면을 만진다. 축을 나누지 않으면 알림이 스레드에서 뜬다.
- **소켓 `Disconnected` 이벤트를 구독하는 방안은 택하지 않았다.** 서버 폴더의 이벤트이고,
  즉시 감지가 되면 오히려 "끊기자마자 앱 종료"가 되어 일시적 흔들림에 더 민감해진다.
  재접속이 생기기 전까지는 15초 유예가 있는 편이 낫다.

## 후속 작업 / 주의사항

- ⚠️ **타이머 콜백에서 Unity API·`ClientLogger`를 부르면 안 된다.** 예외는 삼켜 두었다가
  메인 스레드가 대신 찍는다 — 타이머 콜백의 미처리 예외는 프로세스를 내린다.
  지금은 `C_PingRequest`가 `QuietPacketIds`에 있어 송신 훅(`ClientLogger.OnPacketSent`)이
  `Debug.Log` 전에 조기 반환한다. **핑을 조용한 패킷 목록에서 빼면 이 전제가 깨진다.**
- ⚠️ **`OnDisable`·`OnDestroy`의 `Dispose`를 지우지 말 것.** 에디터에서 플레이를 멈춰도
  타이머 스레드가 도메인 리로드 너머까지 살아남는다.
- ⚠️ **프레임 정지 가드(1초)는 진짜 끊김도 한 프레임 미룬다.** 의도된 트레이드오프다 —
  없으면 드래그 직후 첫 프레임에 **멀쩡한 연결에 종료 알림**이 뜬다
  (`NetworkManager.Update`의 `Flush`와 `PingManager.Update`의 실행 순서가 보장되지 않는다).
- **미검증** — Unity 에디터·빌드에서 실제 구동을 아직 못 돌렸다. `dotnet build Assembly-CSharp.csproj`
  오류 0개까지만 확인했다. 드래그 재현은 `BeginWindowDrag`가 `#if !UNITY_EDITOR`라 **빌드에서만** 된다.
- 슬롯 해금 일감(T-012 갱신 · T-038 · T-039)은 이 작업과 별건으로 같은 세션에 등록했다 →
  `tasks/README.md` 순서 메모의 🔓 항목.
