---
date: 2026-10-28
title: 골드 직접 캐릭터 뽑기 풀(GachaInfoTable 2) 닫기 — T-184 · #78
tags: [data, gacha, server-test]
---

# 골드 직접 캐릭터 뽑기 풀 닫기

## 목적 / 배경
- #78에서 캐릭터는 상점 뽑기권(A~D)으로만 얻기로 했다. 클라 상점이 붙어(태웅 2026-10-26) 남은 일은 골드 직접 풀 2를 닫는 것뿐이었다.

## 변경 내용
- `GameDesign/Excel/Gacha.xlsx` `GachaInfoTable` 2 — `CostCurrency` Gold → `None`, 비용 0/0, 메모 갱신 → 파이프라인 재생성
- `CharacterTicketSheetTest` — `캐릭터를_주는_풀은_전부_골드로_직접_뽑을_수_없다` 추가 (red 확인 후 데이터로 green)
- `GachaServiceTest`·`StorageLimitTest` — `CharacterPoolId` 2 → 4. 이 테스트들은 카탈로그를 주입하고 비용 메타만 실제 시트에서 읽으므로, 유료 메타가 있는 풀 번호면 된다
- `GameDesign/design/character/README.md` 5.1 구현 메모를 현재 상태로
- `tasks/T-184` 데이터 항목 체크

## 주요 결정 / 근거
- 풀 2의 `GachaCharacterTable` 행(30종)은 지우지 않았다 — TID 재사용 금지 · `None` 풀은 `Draw`가 `InvalidGachaId`로 거절하므로 남겨도 무해하다
- 서버 코드 변경 없음 — `GachaService.Draw`의 `CostCurrency == None` 거절 경로를 그대로 쓴다

## 후속
- T-184 ⏸ #93(뽑기권 단계를 없애고 골드로 A~D 바로 뽑기) 결정이 남아 `Resolve` 유지
- 서버 작업이라 main 머지 대신 PR
