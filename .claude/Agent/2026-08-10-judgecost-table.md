---
date: 2026-08-10
title: 판정 비용을 상수에서 IndustryLevelTable.RequiredScore 조회로 전환
tags: [server, workslot, industry-level, T-017]
---

# 판정 비용 테이블 전환 — JudgeCost 상수 폐기

## 목적 / 배경

- `WorkStationSlot.JudgeCost`가 전역 상수(30초)라 레벨이 올라도 판정 주기가 안 갈렸다
  (T-017 잔여 · 산업레벨.md 2.4). 사용자 지시로 `RequiredScore` 테이블 값으로 전환.
- 같은 날 드롭 롤 레벨 분리([2026-08-10-drop-level-filter.md](2026-08-10-drop-level-filter.md))의 후속.

## 변경 내용

- `Server/WSGameServer/Common/IndustryLevelCatalog.cs` — 신설. `(ItemType, Level)` → `IndustryLevelTableRow`
  인덱스 + `GetJudgeCostUnits` = `RequiredScore × 1000L`
- `WorkStationSlot` — 인스턴스 `JudgeCostUnits`(기본 = 기존 상수) 신설, `EffectiveCycle`·
  `ConsumeJudgeCount`·`TimeUntilNextJudge`·`ToInfo`가 상수 대신 이 값을 쓴다. `Assign`이 레벨과 비용을 함께 받는다
- `User.WorkStation.cs` — `ResolveJudgeCost` 신설(행 없으면 경고 + 30초 폴백), 로그인 적재·배치에 적용.
  `User` 생성자에 `IndustryLevelCatalog?` 선택 주입(드롭 카탈로그와 같은 규약)
- `GameServer.cs` — `IndustryLevelCatalog.Instance.LoadAll()` 추가
- 테스트: `IndustryLevelCatalogTest`(단위 환산·중복·실데이터 25행), `WorkStationSlotTest` 비용 가변 2건,
  `UserWorkStationTest` 배선 1건 — 전체 178건 통과

## 주요 결정 / 근거

- **×1000은 단위 환산이지 값 변경이 아니다.** 엑셀 `RequiredScore`는 초×천분율, 서버 누적은
  밀리초×천분율 — 엑셀에 밀리초를 넣으면 Lv5(24.3억)가 int를 넘겨 깨지므로 환산은 서버 몫(산업레벨.md 2.4).
- **비용을 슬롯 인스턴스 상태로 뒀다** (`CurrentWorkSpeed`와 같은 패턴 — 밖에서 해석해 넣는다).
  `Assign`이 (산업, 레벨, 비용)을 한 번에 바꿔 "레벨만 바뀌고 비용이 남는" 어긋남을 막는다.
- **행 누락은 예외가 아니라 경고 + 30초 폴백** — 드롭 테이블 누락과 같은 규약. 데이터 한 줄에
  로그인이 통째로 죽지 않게 한다. 카탈로그의 `Get` 자체는 fail-fast로 남겼다.
- `JudgeCost` 상수는 **기본값(Lv1)으로 유지** — Lv1 = 30,000×1000 = 상수와 동일해 기존 테스트가 그대로 산다.

## 후속 작업 / 주의사항

- **레벨 선택 수단이 아직 없다** — DB 컬럼(`industry_level`)·패킷·해금 검증 전까지 모든 슬롯은 Lv1.
  Lv1 비용 = 기존 상수라 **운영 동작은 지금과 완전히 동일**하다(회귀 위험 없음).
- 레벨 변경이 생기면 **변경 직전 정산**(`Settle` → `Assign`) 순서를 지켜야 싸게 쌓아 비싸게 쓰는 이월 구멍이 안 열린다.
- 클라 카운트다운은 `WorkStationSlotInfo.JudgeCostUnits`를 이미 쓰므로 패킷 구조 변경 없이 레벨별 주기를 따라온다.
