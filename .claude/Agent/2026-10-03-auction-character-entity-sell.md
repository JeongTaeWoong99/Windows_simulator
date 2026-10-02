---
date: 2026-10-03
title: 캐릭터 경매 · 캐릭터·장비 즉시 판매 · 경매장 개선(P4·P5·P6) · Shift+우클릭 등록
tags: [client, ui, auction, inventory]
---

# 캐릭터 경매 · 캐릭터·장비 즉시 판매 · 경매장 개선

## 목적 / 배경
- 서버 #46(능력치 칸 · 인챈트 등급) · #47(캐릭터 경매 · 판매자 이름 · 개체 판매) 처리가 도착해 클라를 붙였다 (T-096 · T-075 · T-095).
- 사용자 결정(2026-10-03): 적성 5종은 툴팁에 · 인챈트 검색은 **등급 드롭다운 하나** · 등록 불가 개체는 **사유를 보인다** · 인벤토리에서 바로 경매 등록 · 개선안 P4(내 매물 자동 갱신·배지)·P5("n분 전에 받음")·P6(정렬 "받은 것 중") 채택 · P1(장비·캐릭터 TID 시세 요약)은 서버 이슈 #52 + T-118/T-119.

## 주요 결정 / 근거
- **캐릭터 검색 화면은 `AuctionSearchPresenter`를 씬에 하나 더 복제**(`searchKind = Character`)했다. 장비와 흐름(검색·커서·즉시구매)이 같아 클래스를 나누지 않는다. 복제본은 분류·인챈트 드롭다운을 끄고 참조를 비웠다(`IsEquip`일 때만 RequireRef).
- 옛 `Character Auction Page`(`(준비 중)`)는 **꺼 둔 채 남겼다** — 삭제는 확인받을 작업이라 실측 뒤 지운다(T-096 체크박스).
- **불가 사유 문구는 `EntityBlockText` 한 곳** — 즉시 판매 담기(알림)와 경매 등록(흐린 줄 툴팁)이 같은 말을 한다. 서버 거절 규칙의 표시용 사본이다.
- **마지막 캐릭터 판정은 이미 카트에 담은 캐릭터를 빼고 센다** — 서버는 "판 뒤 0명"을 거절한다.
- **인벤토리 → 경매 등록은 Shift+우클릭.** 우클릭(즉시 판매 담기)과 같은 손가락 — 둘 다 "판다". 올릴 수 없는 개체여도 등록 화면을 연다(사유를 한 곳에서만 말하게).
- `MarketTabPresenter`·`AuctionTabPresenter`·`AuctionRegisterPresenter`는 Start 전에 밖에서 불릴 수 있어 **요청을 기억했다가 Start가 기본값으로 덮지 않게** 했다(`_hasRequestedTab` · `_pendingKind/_pendingKey`).
- 즉시 판매 요청은 축마다 따로(`C_ItemSellRequest` · `C_EntitySellRequest`) 보내고 **응답이 다 와야** 성공·실패를 말한다. 일괄 담기는 자원만(`ClearItems`) — 개체를 등급으로 쓸어 담으면 아끼던 것이 섞인다.

## 함정
- `UnityEngine`을 쓰는 파일에서 `CharacterInfo`가 모호해진다(CS0104) → `using CharacterInfo = MikaProtocol.CharacterInfo;`.
- RunCommand 스크립트에서 `Image`가 네임스페이스로 잡힌다 → `UnityEngine.UI.Image`로 쓴다.
- 씬 diff가 크다(약 1만 줄) — 대부분 캐릭터 검색 화면 복제본이다.

## 후속 작업 / 주의사항
- **실측(계정 둘)** — 캐릭터 경매 등록·구매·우편 · 판매자 이름 · 인챈트 검색 · 즉시 판매(장비·캐릭터) · Shift+우클릭 · 내 매물 배지. T-096 · T-075가 Resolve로 이것만 남았다.
- 실측 뒤 옛 `Character Auction Page` 삭제.
- P1 시세 요약은 서버 [T-118](../../tasks/T-118-경매시세요약서버.md) → 클라 [T-119](../../tasks/T-119-클라경매시세요약.md).

## 업데이트 (2026-10-03) — 실측 폴리싱

- **장비 검색 줄이 Market 열 폭을 밀었다** — 한 줄에 입력칸(min 120)+드롭다운 3개(min 110)+검색(80)+정렬(130)+간격 = min 685 > 603.
  VLG가 자식에게 min 폭을 그대로 주고 그 min이 바깥 열 레이아웃까지 올라가 열 고정 폭이 깨졌다.
  → 장비·캐릭터 검색 모두 **1줄 = 이름 입력 + 검색 / 2줄 `Filter Panel` = 드롭다운들 + Spacer(flex) + 정렬**로 나눴다.
  두 줄의 `LayoutElement.minWidth = 0` — 다시 넘쳐도 열 폭을 밀지 않는다.
- **등록 화면 3줄 텍스트가 칸을 넘쳤다** — `Sell Guide Text` 44→60, `Hint Text` 24→66 (둘 다 min·pref).
- **내 매물·검색이 비는 것은 클라 버그가 아니다** — 메인 DB를 비워(8a479cd) `trade_id`가 1부터 다시 났는데
  경매장 `auction.sqlite3`(gitignore)에는 9/30 테스트 매물 1~21이 남아 있었다. 경매장 등록은
  `ON CONFLICT (listing_id) DO NOTHING`이라 새 등록이 조용히 무시되고, 옛 매물(Sold·Expired)만 남았다.
  복구: 두 서버 정지 → `auction.sqlite3*` 삭제 → 메인 `t_auction_outbox.sent_at = NULL` → 재시작(Relay가 다시 보낸다).
  **메인 DB만 비우면 같은 일이 또 난다** — DB를 비울 때 경매장 DB도 같이 지운다.
- **복구 실행 결과** — 서버를 내리기 전에 메인의 5분 대사가 먼저 돌아 거래 1~6은 이미 종결(state 3)됐고
  물건·등록비가 반환 우편 6통으로 와 있었다. 그래서 outbox는 **7번(그 뒤 등록)만** `sent_at = NULL`로 되돌렸다 —
  1~6까지 되돌리면 이미 돌려준 물건이 다시 매물로 올라가 복제된다. 재시작 후 경매장에 매물 7이 Listed로 들어간 것을 확인.
- 경매장 서버는 전역 .NET에 ASP.NET 10이 없어 `~/.dotnet/dotnet.exe AuctionServer.dll`(작업 폴더 `Server/AuctionServer`)로 띄웠다.
