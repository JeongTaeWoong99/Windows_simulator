---
date: 2026-11-22
title: 테마 상자 열기 보상 데이터 · 골드 상점 구매 폐기 (T-209)
tags: [data, server, gacha, shop, design]
---

# 테마 상자 열기 보상 데이터 · 골드 상점 구매 폐기 (T-209)

## 목적 / 배경
- 레벨테마 7장(2026-11-22 진우 확정)대로 테마 상자 6종의 열기 풀을 채운다. 캐릭터가 상자에서 나오게 되어 캐릭터 뽑기권 골드 상점은 폐기한다.

## 변경 내용
- `GameDesign/scripts/theme-box-pools.py` — 계층 확률(칸 → 레벨 → 등급 → 하나)을 행마다 정수 가중치로 풀어 `Gacha.xlsx`에 쓴다. `--check`가 DataLog에서 다시 계산해 표와 견준다
- `Gacha.xlsx` — 풀 9 · 12 · 15 · 18 · 21 내용 교체, 새 풀 37(왕실 상자). `Item.xlsx` 100086 `OpenGachaId` 21 → 37, 뽑기권 100071~100074 메모. `Loc_GachaInfo.xlsx` EN "Royal Chest"
- `ShopService.Buy` — 모든 요청을 `InvalidShopRequest`로 거절(데이터는 그대로). `PacketEnum` 주석
- 테스트 — `ThemeBoxPoolSheetTest`(신규) · `ShopBuyTest`(거절 · 보유 뽑기권 열기) · `BoxSheetTest`(캐릭터는 테마 상자 · 뽑기권만) · `ArtifactRewardSheetTest`(테마 상자 1.01% 예외 · 24/25 반씩)
- 문서 — 레벨테마 7장 · 장비레벨 3장 · 아이템 4 · 4.1 · 캐릭터 5.1/5.1.2 · 거래 3.2 · 게임기획코어 · 게임UI · 기획평가 · 일감 T-209/T-184/T-200

## 주요 결정 / 근거
- 서버 추첨(한 풀 = 한 번의 가중치 추첨)은 그대로 두고 가중치를 미리 계산했다. 풀 합 10억(백만분율 × 1000) — int 상한 안
- 정수 분배는 계층마다 최대 나머지 방식 → 칸 · 레벨 · 등급 합이 정확히 맞는다. 행 TID: 자원 001~005 · 소모품 006~010 · 020 · 골드 201~202 · 아티팩트 300+TID · 캐릭터 401~ · 장비 501~ (옛 장비 행 1xx는 재사용하지 않음)
- 비어 있는 (레벨, 등급) 조합은 없어 재분배는 일어나지 않았다(스크립트가 생기면 출력한다)
- 골드는 정확히 ×3^k(옛 데이터의 406 · 2032 같은 반올림 값은 버림)
- 왕실 상자 큐브 6개는 "단계 k에서 k개" 패턴을 이은 것 — 확인 필요로 보고

## 후속 작업 / 주의사항
- 캐릭터 · 장비를 더하면 `theme-box-pools.py`를 다시 돌린다(엑셀을 손으로 고치지 않는다). 대상 엑셀은 닫고 실행
- 뽑기 비용 sink가 빠졌다 — 대체 sink 미정(거래 3.2)
- 클라: 상점 탭(`ShopPresenter`) · `PlayerDataModel.OnShopBought` · `GameDataLoader.GetShopItems` · `UI.Shop.*` 문구 정리 필요(태웅)
