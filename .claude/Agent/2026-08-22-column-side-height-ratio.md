---
date: 2026-08-22
title: 열의 남는 높이를 위젯 2 : 상태 1로 나눠 스크립트가 못박는다
tags: [client, ui, layout]
---

# 열 여백을 위젯 위치에 따라 비율로 나누기 (A-4)

## 목적 / 배경

- 세 열(`@Storage`·`@Main`·`@Market`)의 사이드 칸이 남는 높이를 **1:1**로 나눠 위·아래가 늘 같았다.
  `#State Canvas`는 이름·골드만 있어 낮아야 하고 위젯은 그보다 커야 한다.
- 위젯을 설정에서 위/아래로 옮겨도 비율이 **위젯 쪽을 따라 뒤집혀야** 세 열의 가운데 캔버스가
  가로로 나란히 맞는다.
- 사용자 결정: 가운데 950 → **900**, 나머지 180을 상태 60 / 위젯 120. 비율은 인스펙터 필드.

## 변경 내용

- `Assets/Scripts_Client/UI/Layout/WidgetPositionLayout.cs` — `ApplySideHeights` 계열 추가.
  `widgetWeight`(2)·`stateWeight`(1) 인스펙터 필드, 열마다 가운데를 뺀 나머지를 비율로 나눠
  `LayoutElement.preferredHeight`에 **숫자로** 써 넣는다.
- 씬 `Assets/Scenes/Original/DesktopWindow_Control.unity` — 가운데 세 캔버스 950 → 900.
  나머지 6칸(스페이서 4 + State + Widget)의 `min 0 / pref 60·120 / flexH 0`은
  `ExecuteAlways`가 써 넣은 결과가 저장된 것이다(사람이 넣은 값 아님).
- 문서: `UI 규칙.md` §7-2 · `UI 배치 현황.md` 트리 · `MainCanvasView.cs` 요약 주석.

## 주요 결정 / 근거

- **`flexibleHeight` 비율은 못 쓴다.** flexible은 "남는 높이를 가져간다"라서
  `UIManager.CloseAllExceptWidget`으로 가운데를 끄면 위젯이 열 전체를 빨아들인다
  (2026-08-10에 두 번 밟은 사고). 그래서 계산해서 pref에 못박고 flexible은 0으로 둔다.
- **새 인스펙터 참조를 만들지 않고 열 참조의 자식(index 0·1·2)에서 꺼낸다.**
  이 파일의 기존 판단(같은 오브젝트를 두 번 배선하지 않는다)과 맞춘다.
  대신 `childCount != 3`이면 경고하고 건너뛴다 — 열 구성이 바뀌면 조용히 틀리는 걸 막는다.
- **가운데 높이는 열마다 따로 읽는다.** 한 열 값을 나머지에 복사하면 인스펙터에 손으로 넣은
  숫자가 소리 없이 사라진다. 대신 세 값이 1px 넘게 벌어지면 경고한다(`AppendCenterMismatch`).

## 후속 작업 / 주의사항

- ⚠️ **`minHeight`를 함께 0으로 눌러야 한다.** UGUI가 쓰는 값은 `max(minHeight, preferredHeight)`라
  `min 65`가 남아 있으면 pref 60을 써도 65로 계산돼 열이 넘친다.
- ⚠️ **가운데 높이를 `LayoutUtility.GetPreferredHeight`로 읽으면 안 된다.** 꺼진 오브젝트를
  건너뛰어 0을 돌려준다 → 가운데가 닫힌 상태에서 위젯이 1080을 80% 가져가 위 사고를 재현한다.
  `GetComponent<LayoutElement>().preferredHeight`로 직렬화 값을 직접 읽는다.
- 사이드 6칸을 인스펙터에서 고쳐도 다음 배치에서 덮어써진다. **사람이 정하는 건 가운데 900 하나다.**
- `ExecuteAlways`의 쓰기는 씬을 dirty로 만들지 않는다 — 값이 바뀌어도 저장하지 않으면 안 남는다.
