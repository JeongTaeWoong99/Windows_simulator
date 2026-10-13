---
date: 2026-10-13
title: 경매·거래소 거래 기록 · 완료 탭 조회 (T-156)
tags: [server, auction, db, protocol, test]
---

# 경매·거래소 거래 기록 · 완료 탭 조회 (T-156)

## 목적 / 배경
- 경매장 목업(T-147) 완료 탭 — 판매 완료 · 구매 완료 · 기간 만료 · 취소 + 새 기록 배지. 거래 결과가 우편으로만 남고 조회 패킷이 없었다

## 변경 내용
- 마이그레이션 `0006-auction-history` — `t_auction_history`(유저별 기록) · `t_auction_history_seen`(마지막으로 본 ID)
- `AuctionDb.SettleAsync`·`SettleMarketAsync`·`ReturnAsync`가 같은 트랜잭션에서 기록을 남긴다 (`AuctionHistoryDb.InsertAsync`)
- 조회 `AuctionHistoryDb.QueryAsync` — 오래된 줄 정리 → 새 기록 수 → 한 쪽(최신순 · 구분 · 기간 · 커서) → MarkSeen
- 패킷 `C_AuctionHistoryRequest`/`S_AuctionHistoryResponse`(79·80) · `EAuctionHistoryType` · `AuctionHistoryInfo` · Unity `AuctionHistoryReceived`

## 주요 결정 / 근거
- `t_auction_trade`로 만들지 않고 따로 쌓는다 — 거래소 구매는 여러 판매자 매물에 걸치고 `buyer_id`는 마지막 구매자뿐이라 구매자 기록을 되살릴 수 없다
- 정산·반환 트랜잭션 안에서 쓴다 — 우편만 오고 기록이 빠지는 일이 없다. 다른 유저(판매자) 행도 구매자 큐에서 INSERT만 하므로 메모리와 겹치지 않는다
- 배지 푸시는 두지 않았다 — 기록이 생기는 곳은 언제나 경매 우편이 함께 온다. 클라가 우편 도착 때 `PageSize 0`으로 다시 묻는다
- 보관 30일 — 목업 기간 드롭다운 최대. 정리는 조회 때 그 유저 것만(배치 작업 없이)
- 빈도 제한은 검색과 같은 통

## 후속 작업 / 주의사항
- 클라 완료 탭 · 배지 (태웅 · #69 재배치와 함께)
- 기록은 이 판부터 쌓인다 — 이전 거래는 없다
