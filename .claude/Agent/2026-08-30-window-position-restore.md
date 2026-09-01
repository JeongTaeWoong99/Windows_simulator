---
date: 2026-08-30
title: 재시작 시 창이 작업표시줄 높이만큼 위로 밀리던 문제
tags: [client, desktop-window, settings]
---

# 재시작 시 창이 작업표시줄 높이만큼 위로 밀리던 문제

## 목적 / 배경

- 작업표시줄에 딱 맞춰 둔 창이 **다시 켜면 살짝 위로** 올라와 있었다. "같은 빌드·같은 해상도인데
  왜?"가 출발점.
- 진단은 **PlayerPrefs 를 레지스트리에서 직접 열어** 확정했다
  (`HKCU:\Software\DefaultCompany\DesktopWindow_Control`). 이 방법을 기억해 둘 것 — 창 관련
  버그는 "무엇이 저장돼 있는가"만 보면 대개 판가름 난다.
  - `Window.Anchor = 3`(LowerLeft) 하나뿐 — **실제 좌표는 저장되지 않고 있었다.**
  - 유니티가 종료 시 남긴 `Window Position Y = 695` + `Resolution Height = 745` → 합이 1440으로
    **모니터 전체 바닥과 정확히 일치** = 사용자가 바닥에 붙여 둔 상태였다는 증거.

## 변경 내용

- `Managers/WindowManager.cs` — `SaveCurrentPosition`·`ClearCustomPosition`·`ResolvePosition` 신설,
  `ApplySizeAndPosition`이 `TryGetMonitorRects`로 `wa`·`full` 둘 다 받도록, `InitializeWindow` 재작성.
- `Settings/WindowSettings.cs` — `PositionXKey`/`PositionYKey` · `HasKey`/`DeleteKey`.
- 문서 — `Managers 규칙.md` 5장 · `Settings 규칙.md` §4.

## 주요 결정 / 근거

- **원인은 "저장을 안 한 것"이 아니라 "두 배치 기준이 다른 것"이었다.** 드래그 클램프는
  `full.bottom`(모니터 전체), 앵커 배치는 `wa.bottom`(작업 영역) → 차이가 정확히 작업표시줄 높이.
  ⚠️ 앞선 작업에서 "두 기준이 다른 건 의도다"라고 문서에 못 박았는데, **그 의도 자체는 맞지만
  드래그로 만든 자리를 앵커로는 표현할 수 없다**는 점이 빠져 있었다. 좌표 저장이 그 구멍을 메운다.
- **복원 좌표는 `full` 기준으로만 클램프한다.** 작업 영역으로 자르면 이 버그로 그대로 되돌아간다.
  🔴 이 기준을 "일관성" 명목으로 `wa`로 바꾸지 말 것.
- **크기·위치 드롭다운을 만지면 좌표를 버린다.** 크기가 달라지면 그 좌표는 맞는 자리가 아니고,
  유지하면 커진 창이 화면 밖으로 밀리는 처리가 또 필요해진다. 사용자와 합의한 동작이다.
- **좌표 유무를 sentinel 이 아니라 `HasKey`로 판단한다** — 0도 음수도(보조 모니터 X는 -2048)
  유효한 좌표라 "없음"을 값으로 표현할 수 없다.
- **부팅은 `SetWindowSizeByIndex`를 쓰지 않는다.** 그건 사용자 조작 경로라 좌표를 버린다.
  `InitializeWindow`가 `ApplySizeAndPosition`을 직접 부른다 — 이 우회는 실수가 아니다.

## 후속 작업 / 주의사항

- 함께 고친 별건: **맞춤 배율을 `Awake`에서 계산하고 있었다.** 그때는 `_hWnd`가 `IntPtr.Zero`라
  `MonitorFromWindow`가 실패하고 주 모니터로 폴백한다 — 보조 모니터에 창이 있으면 부팅할 때만
  크기가 틀어진다. `InitializeWindow`에서 다시 계산하도록 했다.
  ⚠️ **`Awake`에서 Win32 창 관련 조회를 하는 코드를 새로 넣을 때 같은 함정을 확인할 것.**
- 복원 좌표의 클램프 기준은 **"지금 창이 있는 모니터"**다. 부팅 시 유니티가 이전 위치로 창을
  띄우므로 대개 맞지만, 모니터 구성이 크게 바뀌면 엉뚱한 모니터 기준으로 당겨질 수 있다.
  정확히 하려면 `MonitorFromPoint`가 필요한데, 크기·위치를 한 모니터로 원자 적용하는
  `ApplySizeAndPosition`의 구조(A-1)와 충돌해서 하지 않았다.
- 에디터·빌드 두 경로 컴파일 오류 0. **동작 확인은 빌드 후 수동이며 아직 안 했다** —
  창을 끌어 놓고 재시작해 같은 자리인지 볼 것.
