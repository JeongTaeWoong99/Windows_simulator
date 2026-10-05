---
date: 2026-10-06
title: 경매 단가 상한 1조 · 등록 수량 상한 9,999
tags: [server, data, auction]
---

# 경매 단가 상한 1조 · 등록 수량 상한 9,999

## 목적 / 배경
- 이슈 #56(태웅의 "x10 → 절대 상한 10조" 서버 변경 리뷰)에서 나온 수정이다 → `tasks/T-123-경매단가절대상한.md`
- 10조의 근거는 "단가 × 수량 × 수수료가 long을 넘지 않는 선"이었는데, **수량에 상한이 없어 그 근거가 성립하지 않았다.**

## 변경 내용
- `GameDesign/Excel/Constants.xlsx` — `AuctionMaxUnitPrice` 1조 · `AuctionMaxListingCount` 9999 신설
- `Server/WSGameServer/Auction/AuctionRules.cs` · `User/User.Auction.cs` — 등록 수량 상한 검사
- 수치·규칙은 → `GameDesign/design/trade/README.md` 4.3 · `Server/docs/경매장.md` 7장

## 주요 결정 / 근거
- **두 상한은 한 쌍이다.** 1조 × 9,999 × 천분율이 long 안이려면 천분율 ≤ 922. 한쪽만 올리면 다시 넘친다.
- 수수료 식을 `checked`로 감싸거나 나눈 뒤 곱하는 안은 택하지 않았다 — 사용자가 상한 두 개로 묶는 쪽을 골랐다.
  `SaleFee`·`ListingFee`는 여전히 곱한 뒤 나누고 넘치면 조용히 틀린다.
- 수량 초과는 새 결과 코드를 만들지 않고 `AuctionInvalidRequest`("종류·수량·TID가 잘못됐다")로 거절한다 —
  패킷 enum을 늘리면 클라 미러까지 번지고, 정상 클라는 입력 단계에서 자른다.
- 보유량은 `MaxStack`(9,999)을 넘을 수 있다 — `ClampToMaxStack`은 채취 지급에만 걸린다(우편·상자 개봉은 안 자른다).
  그래서 "보유량이 상한"에 기대지 않고 등록에서 직접 자른다.

## 후속 작업 / 주의사항
- 클라 등록 화면이 아직 수량을 보유량까지 받는다 — 10,000개 이상이면 서버가 거절한다(#56으로 넘김).
- 구매 쪽 `User.Market.cs`의 `maxUnitPrice * count`는 `MarketMaxBuyCount`(9,999)가 있으나 단가 쪽 상한 검사는 확인하지 않았다.
