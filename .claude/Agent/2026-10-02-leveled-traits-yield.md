---
date: 2026-10-02
title: 특성을 레벨형으로 바꾸고 산출량 특성을 더한다 (T-108)
tags: [design, data, server, trait, yield, simulation]
---

# 레벨형 특성 · 산출량

## 목적 / 배경
- 마일스톤 역산 시뮬레이션(세션 scratchpad `sim.py`)에서 특성 트리가 끝까지 닿지 않음을 확인 — 5레벨마다 1점이면 Lv100까지 20점, 트리 45점.
- 사용자 결정: 포인트는 계정 레벨마다 1점 · 특성은 레벨형 · 산출량(공통 + 산업) 신설 · 개척 특성 기본 Lv1 최대 Lv5(특성 레벨 = 산업 레벨) · 산업 Lv1은 처음부터 열림.

## 변경 내용
- 엑셀: `User.xlsx` `UserTraitTable`(16행, `BaseLevel`·`MaxLevel` 추가) + `UserTraitLevelTable`(130행) · `Enum.xlsx` `UserTraitEffect`에 `IndustryUnlock`·`YieldAdd` · `Unlock.xlsx` 특성 노드 45행 삭제 · `Industry.xlsx` `UnlockTID` 0
- 서버: `UserTraitCatalog` 재작성 · `User.Trait` 재작성(`_traitLevels`, `TryLearnTrait` = 1레벨 올리기, `GetTraitYieldAdd`) · `User.ApplyYield`(정산에서 상자 얹기 전 자원에만) · `IsIndustryLevelUnlocked` = 개척 레벨 · `t_user_trait` + `SaveUserTraitRepository` · 로그인 `S_UserTraitListResponse` · `TraitMaxLevel` 803
- DB 마이그레이션 `Server/Shared/migrations/2026-10-02-user-trait.sql` — game.sqlite3에 적용함
- 문서 11개 전파(포크 에이전트) · 클라 이슈 #50

## 주요 결정 / 근거
- 비용·효과를 레벨 표가 아니라 특성 표에 둔 이유: 클라가 `UserTraitTableRow.TraitPoint`·`EffectValue`를 읽어 옮기면 Unity 컴파일이 깨진다. 레벨마다 값이 같아 손해도 없다.
- `IndustryLevelTable.UnlockTID` 컬럼은 클라(`GameDataLoader`)가 읽어 남겼다(0).
- 특성 레벨을 주는 치트가 없다 — 옛 해금 치트로는 특성이 안 열린다(T-108 후속).

## 후속 / 주의
- 시뮬레이션: 계정 곡선이 초반 너무 빠르고(Lv30 1.5일) 후반 너무 느리다(Lv50 35일). 가챠 비용 고정이라 중반 폭주. 수치 역산은 다음 단계.
