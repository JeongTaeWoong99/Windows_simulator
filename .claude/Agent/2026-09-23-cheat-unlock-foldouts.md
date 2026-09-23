---
date: 2026-09-23
title: 치트창 해금 칸을 세 묶음으로 접는다
tags: [client, editor]
---

# 치트창 해금 칸을 세 묶음으로 접는다

## 목적 / 배경

치트창 「해금」이 `UnlockTable` 51줄을 한 줄기로 늘어놓아 원하는 줄을 찾기 어려웠다(2026-09-23 요청).
작업슬롯 6 · 산업 속도 25 · 산업 레벨 20으로 갈라 각각 접게 하고, 묶음 안은 산업 이름 줄로 한 번 더 갈랐다.

## 변경 내용

- `Assets/Scripts_Client/Editor/cheat-console/CheatWindow.cs` — `DrawUnlockRows`를 묶음 셋으로 나누고
  `DrawUnlockGroup` · `DrawUnlockRow` · `GroupOf` · `IndustryOf` 추가. 펼침 상태 `[SerializeField]` 3개.
- `Assets/Scripts_Client/Editor/cheat-console/cheat-console 규칙.md` — "해금 칸은 세 묶음으로 접는다" 절.

## 주요 결정 / 근거

- **묶음을 TID 대역(1xxx·2xxx·3xxx)으로 가르지 않았다.** `GameDataLoader.UserTraits` 주석이
  "TID 규칙을 클라에 베끼지 않는다"를 명시하고 있어 같은 축을 썼다 —
  `UserTraitTable`에 TID가 없으면 특성 노드가 아니다(→ 작업슬롯), 있으면 `EffectType == SpeedAdd`로
  속도/산업 레벨이 갈린다. **특성 트리 화면(`TraitPresenter.CollectColumn`)과 같은 판정**이라,
  시트에 단이 늘어도 두 화면이 함께 따라온다.
- **머리에 `열림/전체`를 적는다.** 접은 묶음의 진행 상황이 안 보이면 결국 다시 펴게 되고
  접는 의미가 사라진다. 로그인 전에는 열림 수를 모르므로 `?/20`.
- **기본 펼침은 작업슬롯(6줄)만.** 전부 펼치면 고치기 전과 같고, 전부 접으면 창이 빈 것처럼 보인다.
- **산업 이름 줄은 접지 않는다**(사용자 명시). 5산업이 한 덩어리로 붙어 보이는 것만 끊으면 되고,
  접는 층을 하나 더 만들면 "접었다 펴는 게 불편하다"(2026-09-17)는 원래 요청으로 되돌아간다.
- **묶음 순서는 작업슬롯 → 산업 속도 → 산업 레벨**이다(사용자가 직접 바꿨다 — 자주 만지는 속도를 위로).
- **산업 이름은 띠로 그린다**(`DrawIndustryHead` — 옅은 주황 바탕 + 아래 실선). 처음엔 `miniBoldLabel` 한 줄만
  띄웠는데 "구분은 되는데 안 예쁘다"는 지적을 받았다 — 이름이 허공에 떠서 어느 쪽에 붙는 줄인지 안 보였다.
  칸 머리(`BeginSection`)가 이미 쓰는 언어를 얇게 재사용해 창 안에서 따로 놀지 않게 했다.

## 후속 작업 / 주의사항

- ⚠️ **`EditorGUI.IndentLevelScope`는 이 목록에서 쓸 수 없다.** `indentLevel`은 `EditorGUILayout` 컨트롤에만
  먹고 `GUILayout.Label`·`HorizontalScope` 줄에는 안 먹는다 — 처음에 이걸로 들여썼더니 **산업 띠만 밀리고
  해금 줄은 제자리에 남아** 두 층이 어긋났다. 지금은 `UnlockIndent`(px)로 둘 다 직접 민다.

- ⚠️ **`cheat-console 규칙.md`의 "칸은 접지 않는다"(2026-09-17 요청)와 부딪히지 않게 범위를 좁혔다.**
  접는 것은 **해금 칸 *안*의 세 묶음뿐**이고 칸 자체는 여전히 접지 않는다. 접는 층을 하나 더 만들면
  그 요청으로 되돌아간다 — 규칙 문서에도 ⚠️로 적어 뒀다.
- **특성 노드가 아닌 해금이 새로 생기면 조용히 '작업슬롯' 묶음에 섞인다.** 분류가 테이블에서
  파생되는 대가다. 그때 묶음을 하나 더 판다.
