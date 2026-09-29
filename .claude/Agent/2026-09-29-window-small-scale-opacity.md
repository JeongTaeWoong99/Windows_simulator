---
date: 2026-09-29
title: 창 작은 배율(0.5x·0.75x) · UI 투명도 설정
tags: [client, ui, window, settings]
---

# 창 작은 배율(0.5x·0.75x) · UI 투명도 설정

## 목적 / 배경
- 트레이 상주 대신 "작게·흐리게 비켜 두기" → tasks/T-070

## 주요 결정 / 근거
- **`X0_75`·`X0_5`는 `FitTaskbar` 뒤에 붙였다.** 배율이 PlayerPrefs·씬에 int로 저장돼 있어 중간 삽입 시 저장값이 다른 배율을 가리킨다.
  드롭다운 순서는 `WindowManager.SizeOrder`가 따로 정하고, `SizeIndex`/`SetWindowSizeByIndex`는 **드롭다운 인덱스**를 주고받는다(enum 값 아님).
- 저장값 검사를 범위 클램프 → `Enum.IsDefined`로 바꿨다. 클램프는 마지막 enum 값을 상한으로 가정해 뒤에 붙인 항목을 자른다.
- 투명도는 Win32 알파가 아니라 `Root Canvas`의 `CanvasGroup.alpha`. `SetLayeredWindowAttributes`는 DWM per-pixel 투명을 덮어 창이 검게 된다.
- 투명도 주인은 `WindowManager`가 아니라 `DisplayManager`, 키는 `Display.Opacity`(에디터도 저장값). 창 모양이 아니라 UI 알파라 에디터에서도 먹는다.
- 투명도는 인덱스가 아니라 **% 값**을 저장 — 단계를 바꿔도 저장값이 어긋나지 않는다. 모르는 값이면 불투명으로(흐린 쪽 오류는 UI를 잃게 만든다). 하한 40%.
- 슬라이더 대신 단계 드롭다운 — 설정 화면의 다른 항목과 같은 모양이고 기존 드롭다운 복제로 끝난다.

## 후속 작업 / 주의사항
- **빌드 실측 남음** — 0.5x에서 글자 가독성(0.92x에서도 작다는 QA가 있었다), 투명도 단계, 흐려도 클릭·클릭스루 정상인지.
- ⚠️ DWM은 premultiplied alpha로 합성한다. UI 기본 블렌드로 알파 0 배경 위에 그리면 결과 알파가 a²이 되어, 단계가 표기(%)보다 더 비쳐 보일 수 있다 — 실측에서 이상하면 여기부터 본다.
- 하위 캔버스의 `CanvasGroup`(오버레이들)은 `ignoreParentGroups`가 꺼져 있어 루트 alpha가 곱해진다. 켜는 캔버스가 생기면 그 캔버스만 투명도를 무시한다.

## 업데이트 (2026-09-29) — 투명도 슬라이더 · 설정 탭 4개 (T-098)
- 투명도를 단계 드롭다운 → 슬라이더(10~100% — 처음 30에서 사용자 요청으로 10까지 넓혔다)로 바꿨다(사용자 요청). 드래그 중엔 적용만, `PointerUp`(`EventTrigger`)에서 한 번 저장 — Slider엔 "손 뗌" 이벤트가 없다.
- 설정 화면을 탭 4개 + 줄 목록으로 재구성. 구조·규칙은 `UI/Main/Main 규칙.md` "설정 화면" 절.
- `(기능 없음)` 줄은 **씬에만** 있고 코드는 모른다 — 기능을 붙일 때 필드로 받아 배선하고 표시 글씨를 지운다.
- 씬은 `eval_file` 스크립트로 만들었다. 에디터 Game 뷰 캡처는 Overlay UI가 안 찍혀(플레이 전용) **Root Canvas를 잠깐 ScreenSpaceCamera로 돌려 임시 카메라로 렌더**해 확인했다.
- 복원 버튼은 공장값(`setStartScale`·`setStartAnchor`)으로 되돌린다 — 위젯 위치는 부른 쪽(`SettingPresenter`)이 함께 맞춘다(Managers → UI 의존 금지).
