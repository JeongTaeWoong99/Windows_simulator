---
date: 2026-10-20
title: 성장 개체 판매 경고 · 응축 재료 경고
tags: [client, ui, sell, condense, localization]
---

# 성장 개체 판매 경고 · 응축 재료 경고 (T-170)

## 목적 / 배경
- ★ 캐릭터·인챈트 장비를 즉시 판매로 실수로 날리기 쉽다(T-170).
- 응축을 쌓은 캐릭터를 응축 재료로 넣으면 쌓은 응축이 그대로 사라진다 — 사용자 요청으로 경고 추가.

## 변경 내용
- `SellCartModel` — `GrownCount` · `IsGrown(CharacterInfo/EquipInfo)`(★>0 · `EnchantGrade`>0). 줄 표시와 경고 개수가 같은 기준을 쓴다.
- `SellCartPresenter` — 줄 이름 뒤 ★(강조색) · 인챈트 등급(등급색), 툴팁에 응축·인챈트 줄. 경고는 `warningText`(TMP) 한 칸에 상위 등급 + 성장 개체 수를 줄로 쌓는다.
- 씬 — `High Rarity Warning`의 `LocalizedText` 제거, `warningText` 참조 연결.
- `CharacterCondensePresenter.BuildCaution` — 누적 응축 ≥1 재료 수를 확인 팝업에 한 줄.
- `Loc_UI.xlsx` — `UI.Sell.GrownWarning` · `UI.Condense.ConfirmCondensed`.

## 주요 결정 / 근거
- 경고 글씨에서 `LocalizedText`를 뗐다 — 코드가 `text`를 쓰는 글씨에 붙이면 언어 변경 때 덮인다(`LocalizedText` 주석). 문구는 `Lang.Changed` 구독으로 다시 그린다.
- 필드 타입을 `GameObject` → `TMP_Text`로 바꿔 `FormerlySerializedAs`가 통하지 않는다 — MCP로 씬 참조를 다시 걸었다.
- 응축 계승(재료 레벨·응축 → 대상)은 패킷 변경 없이 서버 로직 + 비율 데이터로 된다 — 기획·서버에 이슈 #84로 넘겼다.

## 후속 작업 / 주의사항
- #84 결정 뒤 `UI.Condense.ConfirmCondensed` 문구를 "사라집니다" → "N% 계승"으로.
- T-170 남은 항목(자동 응축·판매 설정)은 T-169 결정 대기.
- 테스트 중 끊김은 서버 `database is locked`(이슈 #85) — 클라 변경과 무관.
