---
date: 2026-08-30
title: 창 크기에 '작업표시줄 맞춤' 항목 추가 (FitTaskbar)
tags: [client, ui, desktop-window, settings]
---

# 창 크기에 '작업표시줄 맞춤' 항목 추가 (FitTaskbar)

## 목적 / 배경

- 창을 작업표시줄 위에 얹었을 때 **하단 위젯 바가 작업표시줄과 높이가 안 맞았다.** 1.25배는 살짝
  작고 1.5배는 살짝 컸다. 사용자가 눈으로 맞추려 애쓰던 상황.
- 실측(스크린샷 픽셀 스캔 + DPI-aware Win32): 위젯 바 = 캔버스(1080) 좌표 **85**,
  이 PC 작업표시줄 = **60px**(2560×1440 · DPI 125%) → 필요 배율 **1.41x**.
- 프리셋을 촘촘하게 하는 방식은 기각했다 — 환경별 필요 배율이 **0.94 ~ 1.98**로 흩어져
  (OS·DPI·`TaskbarSi`) 어떤 고정 목록으로도 못 덮는다. → `tasks/` 아님, 이 로그가 유일 기록.

## 변경 내용

- `Managers/WindowManager.cs` — `WindowScale.FitTaskbar` 추가, `ScaleFactorOf`·`RecalculateFitScale`·
  `SetWidgetSlotHeight`·`FitTaskbarLabel` 신설. `BaseSize`가 static → 인스턴스.
- `UI/Layout/WidgetPositionLayout.cs` — `WidgetSlotHeight` 프로퍼티 노출.
- `UI/Main/SettingPresenter/SettingPresenter.cs` — 드롭다운을 채우기 **전에** 위젯 칸 높이 전달.
- `Settings/WindowSettings.cs` — `WidgetSlotHeightKey` · `LoadFloat`/`SaveFloat`.
- 문서 — `Managers 규칙.md` 5장(주 근거) · `Settings 규칙.md` §4 · `Layout 규칙.md`.

## 주요 결정 / 근거

- **별도 토글이 아니라 배율 드롭다운의 5번째 항목으로 뒀다.** 토글이면 "1x + 맞춤"이라는
  성립하지 않는 조합을 표현할 수 있게 되는데, 그 조합은 위젯이 사이드 칸을 거의 다 먹어
  상태 패널을 짜부라뜨린다(1x에서 위젯 120 : 상태 7.5). 맞춤은 크기와 **함께** 고르는 값이
  아니라 크기 선택을 **대체**하는 값이다 → 상호배타 항목이면 잘못된 상태가 표현 불가능해진다.
  ⚠️ 이걸 "창 배율 축 / 위젯 맞춤 축"으로 **분리하자는 설계를 한 번 세웠다가 폐기했다.**
  다시 꺼내지 말 것 — 축을 나누는 순간 위 조합이 되살아난다.
- **위젯 칸 높이(분모)를 코드 상수로 박지 않았다.** 그 값은 씬 레이아웃(3열 구조 ·
  `widgetWeight`/`stateWeight` · 가운데 칸 높이)에서 파생된다. 상수로 베끼면 비율을 조정한
  순간 맞춤이 **조용히** 어긋난다. 그래서 UI가 실측값을 넘겨주는 형태로 만들었다.
- **값을 "받는" 형태인 이유** — `WindowManager`가 직접 읽으려면 UI 계층을 알아야 해서
  Managers → UI 실행 의존이 생긴다. 그 방향은 이 프로젝트에서 만들지 않는다.
- **위젯 칸 높이만 저장하고 작업표시줄 높이는 저장하지 않는다.** 전자는 씬에서 나오는 값이라
  씬을 안 고치면 불변이고, 후자는 모니터·DPI가 바뀌면 달라져야 한다. 이 조합 덕에 부팅 직후
  (설정 패널을 열기 전)에도 맞춤이 성립하면서 환경 변화에도 따라간다.
- **화면 초과를 미리 검사한다.** 안 하면 `ClampToWorkArea`가 조용히 줄여서, 드롭다운은
  "맞춤"이라 말하는데 위젯은 어긋난 상태가 된다. 검사해서 계산 실패로 두고 경고 + 프리셋 폴백.

## 후속 작업 / 주의사항

- ⚠️ **위젯 비중을 줄이면 맞춤이 깨진다.** 필요 창 크기가 위젯 비율에 반비례한다 —
  `1:2`면 2.83x(창 2717×1528)라 화면을 넘어 성립하지 않는다. 비율표는 `Managers 규칙.md` 5장.
  비율을 바꿀 때 이 항목이 살아 있는지 확인할 것.
- ⚠️ `WindowManager.CanvasReferenceHeight`(1080)는 **씬 CanvasScaler의 기준 해상도와 같아야 한다.**
  어긋나면 맞춤 배율이 그 비율만큼 조용히 틀어진다.
- 에디터에서는 `RecalculateFitScale`이 `#if !UNITY_EDITOR`라 늘 폴백(1.25x)이고 라벨에 배율이
  안 붙는다 — 창 제어가 빌드 전용인 기존 규칙과 같다.
- ⚠️ **에디터·빌드 두 경로를 각각 컴파일해야 한다.** 이 기능은 `#if`로 갈린 코드가 많다.
  `Assembly-CSharp.csproj` 사본에서 `UNITY_EDITOR`를 빼고 `dotnet build` — 이번엔 양쪽 오류 0.
  동작 확인(위젯이 실제로 작업표시줄과 같은 높이인지)은 **빌드 후 수동**이며 아직 안 했다.
