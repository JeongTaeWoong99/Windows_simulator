---
date: 2026-10-13
title: 경매 검색 확장 — 정렬 · 건수 · 단가 하한 · 수치 속성 필터 (T-155)
tags: [server, auction, protocol, db, test]
---

# 경매 검색 확장 — 정렬 · 건수 · 단가 하한 · 수치 속성 필터 (T-155)

## 목적 / 배경
- 경매장 전체 화면 목업(T-147) 검색 탭 — 정렬 드롭다운·열 머리 · "N건" · 왼쪽 상세 옵션. 서버는 단가 낮은순 하나 · `HasMore`뿐이었다

## 변경 내용
- 경매장 `ListingIndex.Search` — 조건에 맞는 매물을 전부 골라 정렬 값으로 줄 세운 뒤 커서 다음부터 한 쪽. 응답에 건수 · 다음 커서
- `Listing.Attrs`(키 → 값) · `t_listing.attrs` JSON · 경매장 DB 판 3 → 4 (ALTER로 이어 쓰기)
- proto `SearchRequest` sort · sort_attr · min_unit_price · attrs(AttrRange) / `SearchReply` total_count · next_cursor_* / `ListingSnapshot.attrs`
- 메인 `EAuctionSort` · `EAuctionAttr` · `AuctionAttrRangeInfo` · `C_AuctionSearchRequest`(MinUnitPrice · Sort · SortAttr · Attrs · CursorSortValue) · `S_AuctionSearchResponse`(TotalCount · NextCursor*)
- 메인 `DescribeEquip`/`DescribeCharacter`가 속성 계산 — 장비 산업 · 작업속도(기본 + 인챈트 속도 합) · 인챈트 속도·경험치 합 / 캐릭터 레벨 · ★ · 산업별 적성

## 주요 결정 / 근거
- 경매장은 GameData를 모른다(경매장.md 2장 7번) → 메인이 숫자 속성을 만들어 싣는다. 키는 `EAuctionAttr`로 뒤에만 더한다(경매장 DB에 남는다)
- 정렬 값 하나 + 매물 ID 커서로 통일 — 내림차순은 부호를 뒤집는다. 단가 낮은순이면 정렬 값 = 단가라 옛 `CursorUnitPrice`가 그대로 통한다(proto 필드 이름도 그대로)
- 검색은 정렬 인덱스를 버리고 전체를 훑는다 — 건수와 아무 정렬의 전체 순서가 필요하다. 매물은 유저당 20건이라 작다. 커지면 분류·정렬별 인덱스를 다시 본다
- 작업속도 속성은 산업 한정 인챈트도 더한다(근삿값) — 정확히는 산업별 키가 필요하지만 목업 필터가 "작업속도 ≥" 하나라 단순하게

## 후속 작업 / 주의사항
- 클라: 목업 왼쪽 패널·정렬·건수 배선 (#69 재배치와 함께, 태웅)
- 옛 매물은 속성이 없어 0 — 속성 필터에 안 걸리고 속성 정렬에서 맨 뒤 (48시간이면 사라진다)
