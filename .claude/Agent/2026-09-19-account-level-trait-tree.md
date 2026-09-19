---
date: 2026-09-19
title: 계정 레벨 · 특성 트리 · 산업 레벨 해금 이관 (T-021)
tags: [server, gamedesign, excel, protocol, db, trait, unlock]
---

# 계정 레벨 · 특성 트리 · 산업 레벨 해금 이관 (T-021)

## 목적 / 배경
- 산업 레벨 Lv2~5가 열 방법이 없어 전부 Lv1에 묶여 있었다(T-021 ⏸ "계정 레벨 뒤").
- 사용자 결정: 캐릭터 경험치 → 계정 경험치 전량 · 계정 레벨 곡선 = 캐릭터 × 8 · 5레벨마다 특성 포인트 1 ·
  **산업 레벨은 특성 트리 노드로 연다** · 산업별 속도 특성 10→50% · 영속은 `t_user_unlock` · 시트명 `UserTraitTable`.
- 여러 조건(계정 레벨 + 선행 + 골드)은 `UnlockTable` 컬럼 AND로 표현 — 특성 표에는 비용·효과만.

## 변경 내용
- 엑셀: `User.xlsx`(신규 — `AccountLevelTable` 100행 · `UserTraitTable` 45행) · `Unlock.xlsx` 45행 추가 · `Industry.xlsx` `RequiredAptitude`/`RequiredAccountLevel` → `UnlockTID` · `Enum.xlsx` `UserTraitEffect`
- 프로토콜: `C_UserTraitLearnRequest`(34) · `S_UserTraitLearnResponse`(35) · `S_AccountLevelResponse`(36) · 결과 코드 800~ · 치트 `GiveAccountExp = 9`
- **중복 번호 수정**: `PacketId` 27·28(적성 포인트 ↔ 장비) → 32·33, `EResultCode` 600(캐릭터 ↔ 장비) → 700. `PacketEnumTest`에 중복 방지 테스트 2건
- 서버: `AccountLevelCatalog` · `UserTraitCatalog` · `User.Account.cs` · `User.Trait.cs` · `CheckUnlockConditions` 추출 · `TryUnlock` 특성 노드 거절 · `IsIndustryLevelUnlocked` · 슬롯 속도에 `GetTraitSpeedAdd` · `SaveAccountRepository` · 로그인 조회/스냅샷
- DB: `t_user_account` 신설 · `t_user_industry_level` DROP(0행) — 실DB 적용, 원본 백업은 세션 scratchpad
- 테스트: `UserAccountTest` 7 · `UserTraitTest` 11 · `AccountLevelRepositoryTest` 1 · 치트 1 · 중복 2 · 기존 산업 레벨 테스트를 해금 기반으로 교체. 전체 377 통과 · 1 Skip
- 문서: 특성(재작성 — 1·2·3장) · 산업레벨 3·6장 · 해금 1·4·6·8장 · 캐릭터 3.1 · 게임기획코어 5장 · 진행·작업슬롯 · 데이터-카탈로그 6·7장 · 치트.md
- 일감: T-021 진행중(실측만) · T-068 신설(클라) · T-016 · T-012 · T-009 · T-043(▶로 이동) · 공백 표

## 주요 결정 / 근거
- 특성 전용 DB 테이블을 두지 않았다 — 노드 = 해금이라 찍은 기록은 `t_user_unlock`, 남은 포인트만 `t_user_account`.
- 포인트 차감은 조건을 전부 통과한 뒤(해금 2.1과 같은 순서).
- 캐릭터가 만렙이어도 계정 경험치는 쌓인다("캐릭터가 얻은 경험치" = 지급량).
- **2026-08-01 "특성은 전역만 · 산업별 특성 폐지"를 뒤집었다** — 사용자 결정. 특성 1.1에 이유를 적었다.

## 후속
- 클라 T-068 · 실측(T-021 마지막 항목) · `M`·곡선·포인트 수치 확정
- 특성 리셋 · 액티브 미착수
