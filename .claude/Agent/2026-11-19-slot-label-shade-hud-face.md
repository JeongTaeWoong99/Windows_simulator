---
date: 2026-11-19
title: 인벤토리 칸 아래 글자 가독성 · 상태 줄 얼굴 그림
tags: [client, ui, art, inventory, hud]
---

# 인벤토리 칸 아래 글자 가독성 · 상태 줄 얼굴 그림

## 목적 / 배경
- 사용자 요청: 프로젝트 전체를 보고 화면·가시성·아트를 알아서 진단하고 손볼 것.
- 실제 플레이 화면을 찍어 진단했다. 눈에 띈 두 곳을 고쳤다.
  - 인벤토리 칸: 실행 중 아이콘이 칸 안쪽 98px까지 차서, 아래 분류·개수 글자(크림색)가 밝은 그림(뿔·늑대·알) 위에서 안 읽혔다.
  - 상태 줄 왼쪽 레벨 원: 가운데가 자리 표시 원반 그대로였다.

## 변경 내용
- `Assets/Scripts_Client/UI/Shared/SlotView.cs`: 선택 참조 `labelShade`와 `RefreshLabelShade`를 추가했다. 보조 문구나 LV 배지가 켜졌을 때만 켠다.
- `Assets/Prefabs/SlotView.prefab`: `Item Image` 바로 뒤에 `Label Shade`를 넣었다(아래 4~40px · 좌우 4 들임). `Sub Text`·`Level Text`의 재질은 `Galmuri EN SDF - Shadow`로 바꿨다.
- `Assets/Resources/Fonts/Galmuri EN SDF - Shadow.mat`: Galmuri EN 재질에 Underlay(진갈 0.95 · 오프셋 0.8 · 확장 1)를 켰다. KR 폴백도 같은 값을 이어받는 것을 실행 중에 확인했다.
- 그림 저장소: `make_slots.py`에 `ui_label_shade`(4×12, 위 투명 → 아래 진갈 0.85)를 추가하고 `make_icons.py`에 `hud_profile`(16px 길드장 얼굴)을 추가했다.
- 씬: `Nick Image` 아래 `Nick Face`(32px, `hud_profile`)를 원반 `Nick Icon (임시)` 바로 뒤에 넣었다.

## 주요 결정 / 근거
- 글자 그림자만으로는 부족했다. Galmuri는 획이 1px이라 Underlay를 최대로 줘도 '3'이 뿔 위에서 여전히 묻혔다. 그래서 띠를 함께 깔았다.
- 띠는 처음에 고른 4단(0→0.8)으로 그렸다. 그랬더니 글자 줄이 옅은 단에 걸렸다. 그래서 아래 2/3를 진하게 바꿨다.
- 얼굴을 `Nick Icon (임시)`에 직접 넣으면 안 된다. 그 원반이 가운데를 덮어 진행도를 고리로 만든다. 투명 귀퉁이가 있는 그림을 넣으면 진행도가 비쳐 파이 차트가 된다(`State 규칙.md` 경고). 실제로 한 번 넣었다가 되돌렸다.
- 로그인 화면은 제목·로고 없이 맨 창이라 비어 보인다. 하지만 게임 이름이 정해지지 않아(`productName`이 프로젝트 이름) 손대지 않았다.

## 후속 작업 / 주의사항
- 이 계정은 캐릭터 41명 중 37명이 창고에 있다. 그래서 캐릭터 탭이 비어 있어, 캐릭터 칸(LV 배지 + 띠)은 화면으로 확인하지 못했다.
- `Assets/Plugins/Analyzers/MikaSourceGen.dll`은 서버를 빌드해 띄우면서 바뀌었다. 커밋하지 않았다.
