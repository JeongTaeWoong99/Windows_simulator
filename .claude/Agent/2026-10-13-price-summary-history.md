---
date: 2026-10-13
title: 장비·캐릭터 시세 · 일별 시세 그래프 (T-118)
tags: [server, auction, protocol, test]
---

# 장비·캐릭터 시세 · 일별 시세 그래프 (T-118)

## 목적 / 배경
- 이슈 #52(장비·캐릭터 TID별 시세 요약) + 경매장 목업 시세 탭(14일 그래프 · 단가별 수량, T-147)

## 변경 내용
- 메인 `User.Market` — 목록·가격대·일별 시세는 `TryQuoteKind`(자원·아티팩트·장비·캐릭터), 수량 구매는 `TryMarketKind`(자원·아티팩트) 그대로
- 경매장 `GetPriceHistory` rpc · `AuctionStore.DailyPrices` · `AuctionEngine.PriceHistoryAsync`(1~60일, 쓰기 채널에서 읽음)
- 메인 패킷 `C_MarketPriceHistoryRequest`/`S_MarketPriceHistoryResponse`(77·78) · `MarketDailyPriceInfo` · Unity `MarketPriceHistoryReceived`

## 주요 결정 / 근거
- 장비·캐릭터 요약에 새 패킷을 만들지 않았다 — 경매장 인덱스·`t_deal`이 이미 종류 무관이라 `Kind`만 열면 된다
- 인챈트 등급별로 쪼개지 않는다 — 참고값. 등급별 비교는 검색 필터로
- 일별 시세는 빈 날도 0으로 채운다 — 클라 그래프가 칸 수를 믿을 수 있게. 하루 경계는 전일 평균과 같은 `MarketDayOffset`
- 14일은 코드 상수(`User.PriceHistoryDays`) — 목업 값. 바뀌면 엑셀 상수로

## 후속 작업 / 주의사항
- 클라 T-119(시세 요약 표시) · 목업 시세 탭 그래프 (태웅)
