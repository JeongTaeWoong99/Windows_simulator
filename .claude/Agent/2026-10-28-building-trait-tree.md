---
date: 2026-10-28
title: 거점 개편 — 건물 5채 레벨 · 작업장 특성 트리 (서버 · 데이터)
tags: [server, data, design, docs]
---

# 거점 개편 — 건물 5채 레벨 · 작업장 특성 트리 (T-183 · #75)

## 목적 / 배경
- 계정 특성 포인트를 없애고, 건물 레벨(골드 + 자원 · 계정 레벨 상한)이 그 건물 트리의 포인트가 되도록 바꿨다.
- T-166의 "산업당 건물 · 자원만 · 해금 4xxx 행" 구조를 갈아엎었다.
- 1차 범위는 작업장 트리뿐이다(옛 계정 특성 16개). 나머지 4채는 레벨만 오르고 노드가 없다.

## 변경 내용
- 데이터: `BuildingTable`(MaxLevel 150) · `BuildingLevelTable`(TID = 건물×1000 + 레벨, 750행) 추가 · `BuildingNodeTable` · `BuildingEffectType` · `Unlock` 4xxx 삭제 · `UserTraitTable`에 `BuildingTID`/`Column`/`Row`/`RequiredTraitTIDs` · `AccountLevelTable`에서 `TraitPoint` 삭제, 150레벨.
- 서버: `UserTraitCatalog`이 건물·레벨 비용까지 들고 기동 시 검증(레벨 행 빠짐 · 선행 순환 · 다른 건물 선행). `User.Building`(업그레이드 · 목록 · 치트). `User.Trait`는 계정 레벨 조건 대신 선행 노드 + 건물 포인트.
- DB: 마이그레이션 `0010` — `t_user_building` 생성, `t_user_trait`·해금 4xxx 초기화, `trait_point` 컬럼 삭제.
- 패킷: `S_BuildingListResponse`(96) · `C/S_BuildingUpgrade`(97·98) · 결과 코드 804 · 1500~1502 · 치트 17.

## 주요 결정 / 근거
- **포인트는 저장하지 않고 파생한다** — 건물 레벨 − Σ(노드 레벨 − BaseLevel) × TraitPoint. 저장하면 레벨·노드와 어긋날 수 있다.
- **업그레이드 판정은 차감이 맨 뒤** — 무효 → 최대 → 계정 상한 → 골드 → 자원 → 차감. 자원은 인벤토리 + 창고 합산(`TryConsumeItems`).
- 노드 찍기는 새 패킷을 만들지 않고 기존 `C/S_UserTraitLearn`을 쓴다(클라 요청 5 — 옛 패킷 유지).
- 옛 특성 기록은 옮기지 않고 초기화했다(사용자 결정 — 테스트 단계).
- 클라 컴파일을 깨지 않으려고 `UserTraitLevelTable`(서버 미사용) · `S_AccountLevelResponse.TraitPoint`(항상 0)를 남겼다.

## 후속 작업 / 주의사항
- 클라 전환(#75) 뒤 `UserTraitLevelTable` · `S_AccountLevelResponse.TraitPoint`를 지운다.
- 커밋된 `game.sqlite3`가 v8이었다 — 이번에 0009까지 함께 적용돼 v10이 됐다.
- `TestUserBuilder`는 노드 0개 **이고** 건물 0채일 때만 실데이터를 읽는다. 건물만 넣은 테스트가 실데이터로 덮이던 함정.
- 더미 클라이언트에 새 패킷 핸들러를 넣지 않았다(MIKA001 경고는 원래 있던 빈칸).
- 비용 수치는 목업식 테스트값(골드 300 × L² + 200 · 자원 = 기본량 × L).
