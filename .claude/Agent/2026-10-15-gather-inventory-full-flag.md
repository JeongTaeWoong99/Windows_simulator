---
date: 2026-10-15
title: 수확 "가방 가득" 연출을 서버 InventoryFull로 바꾼다
tags: [client, ui]
---

# 수확 "가방 가득" 연출을 서버 InventoryFull로 바꾼다

## 목적 / 배경
- 이슈 #67 — 서버가 수확 응답에 `S_GatherResultResponse.InventoryFull`을 싣게 됐다(→ tasks/T-157).
  클라가 2026-10-11부터 쓰던 추정(받은 게 없음 · 보유량이 MaxStack에 닿음)을 걷는다.

## 변경 내용
- `UI/Shared/ItemGainEffectView.cs` `ReadGains` — 추정 두 조건을 지우고 `res.InventoryFull`일 때만 `Gain.BagFull`
- `Managers/AuctionModel.cs` — "보유량이 우편·상자로 MaxStack을 넘을 수 있다" 주석을 고침(이슈의 `Market 규칙.md` 91줄은 이 주석이었다 — 문서는 없다)
- `tasks/T-145` 참고 줄 — 판정 출처를 `InventoryFull`로

## 주요 결정 / 근거
- 등록 수량 상한(`MaxListingCount`)은 그대로 둔다 — 단가 상한과 짝(수수료 long 넘침 방지)이라 보유량 상한과 별개다.
- `GameDataLoader.GetItemMaxStack`은 이제 호출처가 없지만 남겼다(데이터 접근자).

## 후속 작업 / 주의사항
- 실측(가방 가득·최대 수량 수확) 뒤 #67을 닫는다. 위젯 위 표시는 T-145.
