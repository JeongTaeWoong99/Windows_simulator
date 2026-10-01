---
date: 2026-10-01
title: 경매 판매자 닉네임 · 캐릭터·장비 즉시 판매 · 캐릭터 경매 (#47)
tags: [server, auction, shop, character, packet, db]
---

# 경매 판매자 닉네임 · 개체 즉시 판매 · 캐릭터 경매 (#47)

## 목적 / 배경
- 태웅이 경매장 클라 화면(T-096)을 붙이며 요청한 서버 몫 세 가지. 순서는 요청대로 ① 판매자 이름 → ③ 즉시 판매(T-075) → ② 캐릭터 경매.

## 변경 내용
- ① `AuctionItemSnapshot.SellerName`(등록 순간 `NickName`) → proto `seller_name` → 경매장 `t_listing.seller_name` → `AuctionListingInfo.SellerName`.
- ③ `C_EntitySellRequest`/`S_EntitySellResponse`(패킷 63·64) · `User.TrySellEntities` · `SellEntitiesRepository`. 결과 코드 304~306.
- ② `EAuctionKind.Character = 3`. 장비 경매와 같은 길: 등록 때 메모리에서 빼고 `t_character.auction_trade_id`로 잠금 → 정산 때 `user_id`만 구매자로 → 우편 `t_user_mail.character_ids`(`MailCharacter` JSON) → 수령 때 잠금 해제 + 스냅샷으로 `Character` 복원. 로그인은 `auction_trade_id = 0`만 읽는다. 결과 코드 1014·1015.
- 경매장 DB 판 2 → 3 (`seller_name`·`detail`). 판 2 파일은 ALTER로 이어 쓴다.
- `game.sqlite3` — `t_character.auction_trade_id` · `t_auction_trade.character_id` · `t_user_mail.character_ids` ADD COLUMN.

## 주요 결정 / 근거
- **캐릭터 상세는 경매장에 `detail`(불투명 JSON)로 맡긴다.** 경매장은 GameData를 모르는 설계라 레벨·적성 컬럼을 늘리지 않았다. 메인이 검색 결과를 받을 때 `MailCharacter`로 읽어 `CharacterInfo`를 만든다(깨진 JSON은 그 매물의 캐릭터만 null).
- 판매자 이름은 **등록 순간 스냅샷**이다 — 닉네임이 바뀌어도 매물은 옛 이름. 검색마다 메인 DB를 조회하는 쪽은 버렸다.
- 판매·경매 공통 거절: 배치 중 · 장비를 낀 캐릭터(`IsCharacterBusy`) · 마지막 남은 캐릭터. 장비를 낀 캐릭터를 팔면 착용 매핑이 허공을 가리킨다.
- 캐릭터 매물의 분류(`Category`)는 0 — 산업 하나에 묶이지 않는다.
- 개체 판매 응답에 지운 ID 목록을 싣지 않았다 — Ok면 요청한 개체가 전부 사라진 것이라 클라가 요청으로 지운다(태웅 제안 모양 그대로).
- `AuctionItemSnapshot.ToAttachment`는 캐릭터 매물에 개체가 없으면 **예외** — 아래로 흘리면 캐릭터 TID가 자원으로 지급된다.

## 후속 작업 / 주의사항
- 클라(태웅): 판매자 텍스트 · 캐릭터 축 화면 · 창고 캐릭터·장비 판매 배선(T-075 클라 몫) · `MailInfo.Characters` 표시.
- `t_auction_trade.character_id`는 ADD COLUMN이라 운영 DDL 주석은 `/* */`에 있다.
- 같은 트리의 T-058 세션과 패킷 번호(65~67)·결과 코드(1100~)를 나눠 썼다. 내 커밋 뒤 그쪽이 `ShopRepository`·`LoginRepository`·`SqliteFixture`를 고친다.
