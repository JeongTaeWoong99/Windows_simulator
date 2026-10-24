---
date: 2026-10-24
title: 응축 재료의 누적 응축 50% 계승 · 레벨·경험치는 계승 없음
tags: [server, design, data]
---

# 응축 재료의 누적 응축 계승

## 목적 / 배경
- 이슈 #84 · 결정과 이유는 → `tasks/T-179-응축재료계승.md` · 캐릭터 5.5

## 변경 내용
- `Constants.xlsx` — `CondenseInheritPermille` = 500
- `Character.InheritedCondense(count, permille)` — 올림 계산 · `TryCondenseCharacter`가 재료를 지우기 전에 재료마다 `1 + 계승분`을 센다
- `UserCharacterCondenseTest` — 계승 연결 · 레벨 불계승 · 올림 표

## 주요 결정 / 근거
- 처음엔 "★ 단위로 N단계 낮춰 넘긴다" → "레벨·경험치 계승 없음 + 누적 응축 50% 올림"으로 사용자가 정리했다. 단계 낮춤 설정은 만들지 않았다
- 올림은 **재료마다** 한다(합계에 한 번 올림이 아니다) — 재료 하나하나의 누적을 절반으로 줄여 넘긴다는 말 그대로
- 재료는 창고에 있어도 되므로 `_characters`가 아니라 `TryGetCharacter`로 읽는다

## 후속 작업 / 주의사항
- 연결 테스트의 기대값은 `Constants.CondenseInheritPermille`을 읽어 만든다 — 엑셀 값을 바꿔도 테스트가 깨지지 않게. 올림 규칙 자체는 리터럴 표(`계승분은_누적에_비율을_곱해_올림한다`)가 지킨다
- 클라 경고 문구는 아직 "사라집니다" — #84로 태웅에게 넘겼다
