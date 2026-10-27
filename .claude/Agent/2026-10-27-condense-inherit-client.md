---
date: 2026-10-27
title: 응축 창이 재료의 누적 응축 계승분까지 센다 · 확인 문구 "N%만 넘어갑니다"
tags: [client, data]
---

# 응축 창 — 계승분 반영 (T-179 클라)

## 목적 / 배경
- 서버는 2026-10-24부터 재료 누적의 50%(올림)를 대상에 넘긴다(`3183da4e`). 클라는 문구가 "사라집니다"였고 미리보기도 재료 마리 수만 셌다 — 이슈 #84

## 변경 내용
- `CharacterCondensePresenter` — `AddCountOf`(서버 `Character.InheritedCondense` 사본) · `MaterialAddCount` · `CountOverflowMaterials` · `InheritPercentText`
  - 미리보기 ★ · 진행 바 · '누적 n + m' · 다음 단계까지 남은 수 · 넘침(알림 줄·확인 창) · [다음 단계까지 채우기]가 계승분까지 센다
- `Loc_UI.xlsx` — `UI.Condense.ConfirmCondensed`({1} = 비율 %) · `UI.Condense.HelpLost`({0} = 비율 %) 문구
- `Inventory 규칙.md` 응축 절 · `T-179` 클라 항목 체크

## 주요 결정 / 근거
- 비율은 문구에 숫자로 박지 않고 `Constants.CondenseInheritPermille`에서 읽는다 — 진우가 #84에 "더 생각하기"를 남겨 값이 바뀔 수 있다
- 넘침은 "마리" 문구를 그대로 쓰므로 고른 순서대로 더해 최고 누적에 이미 닿은 뒤의 재료만 센다. 일부만 넘치는 재료는 세지 않는다

## 후속 작업 / 주의사항
- 플레이 확인 전 — 누적을 쌓은 재료(★1 이상 캐릭터)를 재료로 골라 노랑 몫·확인 문구를 본다
- 비율이 0이 되면 문구가 "0%(올림)만 넘어갑니다"가 된다 — 그때는 문구를 다시 본다
