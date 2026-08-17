---
date: 2026-08-17
title: 항상 위(Topmost)가 작업표시줄에 가려지던 문제 — 주기·포커스 재확정
tags: [client, window, win32]
---

# 항상 위가 작업표시줄에 가려지던 문제

## 목적 / 배경
- topmost 토글 ON이어도 창을 Windows 작업표시줄 위 영역으로 끌면 작업표시줄에 가려졌다.
- 기대: topmost ON이면 작업표시줄까지 포함해 모든 것 위에 계속 보여야 한다.

## 변경 내용
- `Managers/WindowManager.cs`
  - `ReassertTopmost()` 신설 — `SetWindowPos(HWND_TOPMOST, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE)`로
    Z순서만 topmost 밴드 맨 앞으로. 위치·크기·포커스는 안 건드림.
  - `Update()` — `_isTopmost`인 동안 `TopmostReassertInterval`(0.5초)마다 `ReassertTopmost()`.
    동적 클릭스루와 무관해야 하므로 클릭스루 early-return 앞에 배치.
  - `OnApplicationFocus(false)` — 포커스 잃는 즉시 재확정(주기 대기 없이).
  - `SetTopmost()` — 재확정 타이머 리셋만 추가(기존 HWND_TOPMOST/NOTOPMOST 로직은 그대로).

## 주요 결정 / 근거
- **원인은 "한 번만 확정"이었다.** 작업표시줄(`Shell_TrayWnd`)도 topmost 창이라, topmost끼리는 같은
  밴드 안에서 Z순서로 경쟁한다. 다른 topmost가 활성화되거나 작업표시줄이 앞으로 오면 밴드 안에서
  뒤로 밀리는데, 최초 `SetWindowPos` 뒤 재확정이 없어 가려졌다. → **주기 + 포커스 재확정**으로 해결.
- `WS_EX_TOPMOST` 확장 스타일을 따로 세팅할 필요 없음 — `SetWindowPos(HWND_TOPMOST)`가 자동 적용한다.
  즉 스타일 문제가 아니라 "재확정 부재"가 원인이었다.
- `SWP_NOACTIVATE` 필수 — 재확정할 때마다 포커스를 뺏으면 다른 앱 작업을 방해한다. 오버레이는
  "앞에 보이되 포커스는 안 가져간다"가 맞다.
- **플랫폼 가드는 파일 관례를 따라 `#if !UNITY_EDITOR` 유지**(제안된 `UNITY_STANDALONE_WIN && !UNITY_EDITOR`
  대신). 이 파일의 모든 Win32 호출이 `!UNITY_EDITOR`로 통일돼 있어 혼용이 더 나쁘다. 빌드 타깃도 Windows
  단독이라 실질 차이 없음. 에디터에선 `ReassertTopmost` 본문이 비어(no-op) 안전.
- 재확정 주기 0.5초 — 즉각성과 `SetWindowPos` 호출 빈도의 절충. 포커스 상실 시엔 즉시라 체감 지연 없음.

## 후속 작업 / 주의사항
- **검증은 빌드 필수**(Win32는 에디터 미동작). topmost ON으로 창을 작업표시줄 위로 끌어 계속 보이는지,
  다른 앱을 Alt+Tab으로 띄웠을 때 오버레이가 위에 남되 포커스는 그 앱에 있는지 확인.
- topmost OFF → `HWND_NOTOPMOST`로 정상 복원(재확정은 `_isTopmost` 가드라 OFF면 안 돈다).
- 창 드래그 중(SC_MOVE 모달 루프)에는 Update가 멈춰 재확정이 잠깐 안 돌지만, 놓는 즉시 다음
  Update에서 재확정된다.
