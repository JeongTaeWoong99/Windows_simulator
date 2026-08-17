---
date: 2026-08-17
title: 타이틀바 제거 동반 UI 개편 — 토글 고정·캔버스 드래그·종료 버튼·위치 통합 (A-1 (3))
tags: [client, window, ui, win32, a-1]
---

# 타이틀바 제거 동반 UI 개편 (A-1 (3))

## 목적 / 배경
- 타이틀바 토글을 끄면(보더리스) OS 'X' 버튼과 타이틀바 드래그 이동이 사라진다. 그 공백을 메우는 4건:
  (1) 토글 3개를 UI에서 걷어내고 기본값 고정, (2) 메인 뷰 캔버스 드래그로 창 이동,
  (3) 명시적 종료 버튼, (4) "창 위치"+"위젯 위치" 두 드롭다운을 6칸 1개로 통합(Middle 제거).
- 계획서: `~/.claude/plans/c-users-asus-desktop-md-a-1-sleepy-lantern.md`. 선행 (1) 스냅 수정 로그는
  `2026-08-17-window-snap-atomic-apply.md`.

## 변경 내용
- `Managers/WindowManager.cs`
  - `ScreenAnchor` 9→6값(Upper/Lower × L/C/R). `AnchorPosition` 세로 분기에서 Middle 제거.
  - `LoadSettings`: TitleBar·Transparent·DynamicClickThrough는 저장값 무시하고 `setStart*` 강제.
    `setStartTitleBar` 기본값 `false`. Anchor는 `MigrateAnchor`로 레거시 9값을 6칸으로 접음.
  - 신설 `BeginWindowDrag()`(ReleaseCapture+SC_MOVE_HTCAPTION 위임), `QuitApplication()`(ESC와 공유).
- `UI/Layout/WindowDragArea.cs` **신설** — `IBeginDragHandler` → `BeginWindowDrag`.
- `UI/Main/SettingPresenter/SettingPresenter.cs` — 위치 드롭다운 1개가 창 앵커+위젯 위치 동시 몰이.
  widget 드롭다운 필드·바인딩 제거. 토글 3개 RequireRef/BindToggle은 유지(오브젝트만 비활성).
- `UI/State/StatePresenter/StatePresenter.cs` — `quitButton` 전용 필드 추가(화면전환 배열과 분리).
- `UI/Layout/WidgetPositionLayout.cs` — 기본 `position` LowerCenter→**LowerRight**(창 기본값과 일치).
- 문서 동기화: `DesktopWindow 규칙.md`(§5-7 드래그 금지 개정), `Managers 규칙.md`, `Settings 규칙.md`.

## 주요 결정 / 근거
- **드래그는 `IBeginDragHandler`.** EventSystem drag threshold를 넘겨야 발화 → 클릭/드래그 구분이 공짜라
  별도 타이머·임계값 코드가 없다. SC_MOVE 위임이라 스냅·모니터 간 이동을 재구현하지 않는다.
  → `DesktopWindow 규칙.md` §5-7의 "직접 드래그 바 금지"는 이 방식엔 해당 안 됨(OS 처리)이라 개정.
- **토글 3개는 저장값을 읽지 않는다.** UI로 되돌릴 방법이 없어져, 옛 PlayerPrefs가 보더리스/투명을
  깨지 못하게 `LoadSettings`가 `setStart*`를 항상 대입. 저장은 되지만 로드에서만 무시.
- **⚠️ 부팅 정렬 함정 — `SettingPresenter.Start`는 부팅이 아니라 설정 패널을 처음 열 때 돈다.**
  `UIManager.ResetMainScreen`이 시작 시 Setting 패널을 꺼 두기 때문. 그래서 창·위젯 위치의 부팅 일치는
  `SettingPresenter`가 아니라 **두 공장 기본값을 같은 모서리(LowerRight)로 맞춰** 보장한다. 드롭다운의
  `SetPosition`은 부팅 정렬이 아니라 "설정 여는 순간" 옛 어긋남을 흡수하는 역할.
- **레거시 마이그레이션은 두 키를 그대로 둬도 안전.** 옛 Lower행(6,7,8)→(3,4,5)가 위젯 Lower 인코딩과
  동일해, "짝 맞는" 저장값은 마이그레이션 후에도 일치. 실제 어긋나는 건 옛 Middle 앵커뿐이고 설정 열 때 흡수.
  → 그래서 `WidgetPositionKey`를 `AnchorKey`로 합치는 큰 리팩터를 **하지 않음**(두 키 유지가 더 단순·안전).

## 후속 작업 / 주의사항
- **검증은 빌드 필수** — Win32는 `#if !UNITY_EDITOR`. 캔버스 드래그 이동·종료·보더리스·투명을 exe에서 확인.
- **Unity 수작업(handoff) 남음:**
  1. `#State Canvas`·`#Widget Canvas` 루트에 `WindowDragArea` 추가(GraphicRaycaster·raycast target Graphic 확인).
  2. `State Presenter (↓ SUB VIEW)` 아래 종료 버튼 1개 → `StatePresenter.quitButton` 연결.
  3. 위젯 위치 드롭다운 오브젝트 제거/비활성, 창 위치 드롭다운을 단일 "위치"로 유지.
  4. `WindowManager` 인스펙터 `setStartAnchor`를 `LowerRight`로 재선택(enum 값이 8→5로 이동),
     `WidgetPositionLayout`의 `position`도 씬에서 `LowerRight` 확인, `setStartTitleBar` 체크 해제 확인.
  5. 신규 `WindowDragArea.cs`의 `.meta` 생성 후 원본과 함께 커밋.

## 업데이트 (2026-08-17) — 빌드에서 캔버스 드래그가 안 걸리던 문제
- **증상:** 씬 배선(캔버스에 `WindowDragArea` + GraphicRaycaster + raycast target)까지 맞췄는데도
  빌드에서 창이 안 끌렸다.
- **원인(Unity EventSystem 함정):** `WindowDragArea`가 `IBeginDragHandler`만 구현했다. EventSystem은
  마우스 누를 때 `ExecuteEvents.GetEventHandler<IDragHandler>`로 드래그 대상(`pointerDrag`)을 정하고
  **그 대상에게만** `OnBeginDrag`를 보낸다. `IDragHandler`가 없으면 이 오브젝트는 애초에 드래그
  대상으로 선택되지 않아 `OnBeginDrag`가 영영 호출되지 않는다. 버튼 클릭은 드래그 경로가 아니라
  멀쩡했고, 에디터는 Win32 미동작이라 티가 안 났다.
- **수정:** `WindowDragArea`에 `IDragHandler`를 함께 구현(빈 `OnDrag` — 실제 이동은 OS 모달 루프가 처리).
  → "드래그 이벤트를 받으려면 `IBeginDragHandler`만으로 부족하고 `IDragHandler`가 대상 선택 조건"임을 기억.
