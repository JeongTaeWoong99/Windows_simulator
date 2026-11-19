---
date: 2026-11-18
title: 경매 더미 매물 치트 — 장비·캐릭터·아티팩트 종류 고르기 (T-204 · 이슈 #124)
tags: [server, auction, cheat]
---

# 경매 더미 매물 치트 — 장비·캐릭터·아티팩트 종류 고르기

## 목적 / 배경
- 이슈 #124 — `SeedAuctionListings`가 자원만 올려 장비·캐릭터·아티팩트 검색·구매 화면을 시험할 수 없었다.

## 변경 내용
- `Auction/AuctionDummy.cs` — `Build(DummyPool, count, now, Random?)`. 종류는 자원 → 장비 → 캐릭터 → 아티팩트 순으로 돌아가고, 빈 종류는 건너뛴다
- `Auction/AuctionAttrs.cs`(신규) — 검색 속성 계산을 `User.Auction`에서 옮겼다. 실제 등록과 더미가 같은 함수를 쓴다
- `Auction/AuctionDb.cs` — `SeedDummyAsync`가 장비·캐릭터의 개체 행을 더미 소유로 INSERT → 거래 → `auction_trade_id` 잠금. `PurgeDummyAsync`가 더미 소유의 장비·캐릭터 행도 지운다
- `User/User.Cheat.cs` — `Arg2`: 0 = 전체 · 1~4 = `EAuctionKind` · 그 밖 = ItemTID
- `docs/치트.md`, `PacketEnum.cs` 주석(Unity 미러 동기화 포함)

## 주요 결정 / 근거
- **장비·캐릭터는 개체 행까지 만든다.** 정산(`SettleAsync`)이 `t_user_equip`·`t_character`의 `auction_trade_id`로 소유를 넘기므로, 스냅샷만 있으면 구매 시 "장비 이전 실패" 예외가 난다
- **Arg2에 ItemTID를 남겼다.** 클라 치트 창(`AuctionDummyCheatSection`)이 지금 ItemTID를 보낸다 — ItemTID는 1만대부터라 1~4와 겹치지 않는다. 패킷에 Arg3을 늘리는 안은 클라·미러까지 번져서 택하지 않았다
- 0의 뜻이 "자원을 돌아가며"에서 "네 종류 전부"로 바뀌었다(이슈 요청). 클라 창의 `_isMixed`(Arg2 = 0)는 이제 전 종류를 섞는다

## 후속 작업 / 주의사항
- 클라 치트 창의 종류 칸은 태웅 몫(이슈 #124에 인자 안내)
- 팔린 더미 개체는 구매자 소유라 삭제해도 남는다(의도)
