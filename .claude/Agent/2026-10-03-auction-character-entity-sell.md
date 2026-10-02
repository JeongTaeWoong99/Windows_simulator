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
