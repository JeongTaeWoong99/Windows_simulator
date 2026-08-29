---
date: 2026-08-30
title: 드래그로 화면 밖에 나간 창을 되올린다 (ClampIntoMonitor)
tags: [client, desktop-window, win32]
---

# 드래그로 화면 밖에 나간 창을 되올린다 (ClampIntoMonitor)

## 목적 / 배경

- 빌드에서 창을 **위로** 끌면 알아서 내려와 잘리지 않지만, **아래로** 끌면 그대로 묻혔다.
- 이 비대칭은 우리 코드가 아니라 **Windows 이동 루프의 기본 동작**이다 — OS는 캡션이 화면
  **위로** 넘어가는 것만 막고, 아래·좌우는 막지 않는다. 그래서 우리 쪽엔 드래그 후 위치를
  검사하는 코드가 **애초에 없었다**(없어도 위쪽만은 되던 이유).
- 사용자 의도는 "창 바닥을 작업표시줄 높이에 딱 맞추기". 튕겨 나오지 않으니 높이를 손으로
  세밀하게 맞춰야 했다.

## 변경 내용

- `Assets/Scripts_Client/Managers/WindowManager.cs`
  - `ClampIntoMonitor()` 신설 — 드래그 종료 시 세로만 모니터 안으로 되돌린다.
  - `BeginWindowDrag()`가 `SendMessage` 직후 이를 호출한다.
  - `TryGetWorkArea`를 `TryGetMonitorRects`(작업 영역 + 전체 동시 반환) + 얇은 래퍼 둘로 분리.
- 문서 — `Managers 규칙.md` 5장 · `DesktopWindow 규칙.md` 5-7에 근거와 한계를 적었다.

## 주요 결정 / 근거

- **클램프 기준을 작업 영역이 아니라 `rcMonitor`(모니터 전체)로 잡았다.** 작업표시줄 **위에
  겹쳐 두는 배치가 이 게임에선 유효한 사용**이기 때문(항상 위라 가려지지 않는다). 작업 영역을
  쓰면 그 배치가 매번 튕겨 나와 못 만든다. 앵커 배치(`AnchorPosition`)는 그대로 **작업 영역**
  기준 — ⚠️ **두 기준이 다른 건 실수가 아니라 의도다.** 통일하려 들지 말 것.
- **보정 시점은 드래그 종료 후.** 실시간 차단은 `WM_MOVING`을 가로채야 하고, 그건 창
  서브클래싱 = "이동은 OS에 위임"(`DesktopWindow 규칙.md` 5-7) 원칙을 깬다. `SendMessage`가
  블로킹이라 반환 시점이 곧 "마우스를 놓은 뒤"여서 훅이 공짜다.
- **가로는 클램프하지 않는다** — 좌우로 걸쳐 두는 것도 의도된 배치라는 사용자 확답.
- `GetWindowRect`(외곽)를 그대로 쓴다 — `SetWindowPos`가 옮기는 대상과 좌표계가 같아
  프레임 두께 보정이 필요 없다. `ClientSizeToOuterSize`를 끌어들이지 말 것.

## 후속 작업 / 주의사항

- ⚠️ **타이틀바를 되살리면(`setStartTitleBar = true`) 보정이 안 걸린다** — OS 타이틀바 드래그는
  `BeginWindowDrag`를 지나지 않는다. 현재 타이틀바는 고정 off라 실사용 경로가 아니다.
- 드래그가 아닌 이동(해상도 변경·작업표시줄 위치 변경)도 대상이 아니다.
- ⚠️ **에디터에서는 검증되지 않는다** — 전부 `#if !UNITY_EDITOR`다. 이번엔 `Assembly-CSharp.csproj`
  사본에서 `UNITY_EDITOR`를 뺀 뒤 `dotnet build`로 컴파일만 확인했다(오류 0). 동작은 빌드 후 수동 확인.
