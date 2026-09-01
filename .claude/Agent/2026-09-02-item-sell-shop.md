---
date: 2026-09-02
title: 즉시 판매(T-028) 서버 구현 · BasePrice 156종 임시값
tags: [server, economy, packet, excel, transaction]
---

# 즉시 판매 — 아이템을 팔아 골드를 얻는다

## 목적 / 배경

- **골드를 버는 길이 하나도 없어 지갑이 항상 0이었다.** `CurrencyWallet`·`S_CurrencyResponse`는
  이미 있었지만 잔액을 늘리는 경로가 없었다. 그래서 가챠 비용(T-027)도 슬롯 확장(T-038)도
  실제로 검증할 수 없는 상태였다.
- 클라(T-032)는 **판매 패킷이 없어 한 줄도 쓰지 못하고** 있었다 — 클라가 서버에 기다리던 것 중 가장 급한 항목.
- 사용자가 worktree(`worktree-T-028-baseprice-shop`)에서 진행할 것을 지정했다.

## 변경 내용

**엑셀 / 데이터**
- `GameDesign/Excel/Item.xlsx` — `BasePrice` 컬럼(`int`, `Min=0`, 기본값 없음) 추가 + 156종 값.
  `레벨배율(1·3·9·27·81) × 등급배율(10·25·60·150·400·1000)`으로 일괄 산출. 파이프라인 실행.

**프로토콜**
- `Server/MikaProtocol/MikaPacket.cs` — `C_ItemSellRequest`(19) · `S_ItemSellResponse`(20).
- `Server/MikaProtocol/PacketEnum.cs` — `EResultCode` 300번대(상점): `InvalidSellRequest`·`NotEnoughItem`.

**서버**
- `User/Inventory/Inventory.cs` — `TryRemoveItems` 추가(빼는 메서드가 아예 없었다).
- `Shop/ShopService.cs` (신규) — 검증·가격 계산·응답. `SellRatePermille = 1000`.
- `User/User.Shop.cs` (신규) — `TrySellItems`. 차감 → 지갑 → DB 태스크 1개 → 잔액 통지.
- `Repository/ShopRepository.cs` (신규) — `SellItemsRepository`. 인벤·재화를 **한 트랜잭션**으로.
- `DB/DbConnection.cs` — `InTransactionAsync` + 모든 메서드에 `IDbTransaction` 전달.
- `Network/ClientPacketHandler.cs` — `Handle_C_ItemSellRequest`.
- `MikaDummyClient` — `Handle_S_ItemSellResponse` · `PacketMenu`의 `ItemSell` 액션.

**테스트** — `Inventory/InventoryRemoveTest.cs`(5) · `Shop/ShopServiceTest.cs`(8). 전체 210개 통과.

## 주요 결정 / 근거

- **`BasePrice`는 `Item.xlsx` 컬럼으로 넣었다.** 별도 `Market.xlsx`를 만들자는 안이 있었으나,
  아이템 1종 = 값 1개인 고유 속성이라 조인 테이블을 하나 더 만들 이득이 없다.
  단조 증가 검증도 `GlobalRarity` 옆에서 봐야 눈으로 대조된다.
- **판매율은 엑셀이 아니라 코드 상수 1곳에 뒀다.** T-028 문서는 "엑셀에 둔다"였고 `Economy.xlsx`
  신설을 제안했으나, **사용자가 값 하나 때문에 새 엑셀 파일을 만들지 않기로 했다.**
  거래소가 붙어 수수료·가격 밴드가 함께 생길 때 그때 옮긴다.
- **DB 작업을 하나로 묶었다.** 기존 패턴(`AddItemRepository` + `SaveCurrencyRepository`를 각각 Post)을
  그대로 따르면 **DB 작업이 둘로 갈라져** 차감만 커밋되고 지급이 실패하는 창이 열린다.
  그래서 `GainCurrency`를 쓰지 않고 `Wallet.Gain`만 부른 뒤 통합 Repository에 저장을 맡겼다.
  `ShopServiceTest.판매는_DB작업을_하나만_예약한다`가 이 결정을 지킨다.
- **전부 되거나 전혀 안 된다.** 목록 중 한 종류라도 모자라면 아무것도 팔지 않는다.
  부분 성공을 허용하면 클라가 "무엇이 팔렸는지"를 다시 물어야 한다.
- **판매 불가 아이템은 지금 만들지 않았다.** 156종 전부 채취물이라 전부 팔린다.
  `ShopService.IsSellable`이 판정 지점만 잡고 있고, 실제로 갈리는 것은 상자가 생기는 T-029다.
  ⚠️ `BasePrice = 0`을 "판매 불가"로 쓰지 않는다 — 이 값은 거래소 하한·자산 환산에도 쓰여
  "가치 없음"과 "팔 수 없음"이 겹친다.
- **상점의 구매 쪽은 만들지 않았다.** 지금 골드로 살 수 있는 물건이 하나도 없다 —
  슬롯 확장권은 T-038, 가챠 티켓은 T-027에서 골드 직접 결제로 대체됐다.

## 후속 작업 / 주의사항

- ⚠️ **Unity 클라에 `Handle_S_ItemSellResponse`가 없다.** 분석기가 MIKA001 경고를 낸다 —
  사용자 지시로 클라는 손대지 않았고, T-032와 GitHub 이슈에 인계했다.
- ⚠️ **가챠 비용(T-027)을 정할 때 `Special` 6종(`100001~`)의 판매가와 대조해야 한다.**
  비용이 판매가 기댓값보다 낮으면 **뽑아서 되파는 무한 골드 루프**가 생긴다. 지금은 10~1000이다.
- `BasePrice` 값 확정은 T-018 — 지금 5산업이 같은 배율이라 시간당 수익이 갈릴 수 있다(T-008·T-009와 한 세트).
- 데이터 파이프라인을 돌리면 `Server/GameData`·`ExcelGenerator/Output` 아래 파일 30여 개가
  **개행(LF→CRLF)만 바뀐 채로** 수정됨 표시가 뜬다. 내용 변화가 없어 `git add`하면 자동으로 빠진다.
