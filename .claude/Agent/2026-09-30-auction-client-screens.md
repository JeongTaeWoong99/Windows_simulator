---
date: 2026-09-30
title: 경매장 탭 클라 화면 — 거래소·장비·등록·내 매물 (T-096)
tags: [client, ui, network]
---

# 경매장 탭 클라 화면 — 거래소·장비·등록·내 매물 (T-096)

## 목적 / 배경
- 서버(T-091·T-094)는 섰는데 클라 구독자가 0건이었다. 거래 열 `Auction Page` 자리(T-101)를 채운다. → `tasks/T-096`
- 사용자 결정(2026-09-30): 거래소는 경매장 탭 안 하위 탭 · 등록은 화면의 [등록] · 인챈트는 뺀다(#46).

## 변경 내용
- 수신: `ServerPacketHandler` 경매·거래소 region · `ResultMessages` 1000~1013 · `PlayerDataModel.OnAuctionRegisterResponded`
- 모델: `Managers/AuctionModel.cs` (씬 루트 `Auction (MODEL)`)
- 화면: `UI/Market/` — `AuctionTabPresenter` + 화면 4개 + 공용 `AuctionRowView`·`AuctionRowList`·`AuctionText` · `Assets/Prefabs/AuctionRowView.prefab`(MailRowView 복제)
- 씬: `Auction Page`에 VLG + 하위 탭 줄 + 화면 4개. 위젯은 우편함 `Body Scroll Panel`·인벤토리 도구 줄의 입력칸/드롭다운/버튼을 복제해 테마를 물려받았다. `No Feature Text` 삭제.
- 문서: `Market 규칙.md`(경매장 탭 절) · `Managers 규칙.md`(8개) · `UI 배치 현황.md`

## 주요 결정 / 근거
- **`AuctionModel`은 송신한다** — 다른 모델과 다르다. 장비 검색 [더 보기]는 마지막 매물 (단가, ID)가 커서라 조건·커서·누적이 한 곳에 있어야 한다. Presenter가 보내면 "이어 붙일지 갈아 끼울지"를 모델에 따로 알려야 해서 기각. 대기(`ServerWaitManager`)는 Presenter가 그대로 건다.
- **등록 응답의 인벤토리 반영은 `PlayerDataModel`** — 내 계정 상태의 주인이 한 곳이어야 한다. `AuctionModel`은 결과만 알린다.
- **하위 탭 화면은 탭 Presenter의 형제** — Presenter 안에 Presenter를 두지 않는다. 거래 열 탭 줄을 복제해 스크립트만 바꿨다.
- 목록 줄 하나(`AuctionRowView`)를 네 화면이 같이 쓴다 — 모양이 같고 캔버스 하나 안이라 `UI/Shared/`가 아니라 `UI/Market/`.

## 후속 작업 / 주의사항
- ⚠️ **장비 등록 성공 시 서버는 EquipSync를 보내지 않는다** — `EquipId`로 클라가 지운다. 거절 응답에도 `EquipId`가 실려 오니 결과부터 본다.
- ⚠️ **빈 TID 목록 = 조건 없음**(서버). 이름 검색이 0건이면 요청을 보내지 않는다 — 보내면 전체가 나온다.
- ⚠️ 목록·가격대·검색이 **빈도 제한 한 통**(5회·2초 회복). 자동 조회는 탭을 처음 열 때만. 내 매물은 제한 밖이라 열 때마다.
- ⚠️ 하위 화면은 거래소만 켠 채 저장했다. 나머지를 켠 채 저장하면 첫 활성화에 각자 조회를 보낸다.
- 실측 안 함(플레이 모드·경매장 서버 필요). 인챈트 필터·표기는 #46 뒤 — `AuctionText.ToRow`와 `SearchEquips` 두 곳.

## 업데이트 (2026-09-30) — 실측 피드백
- **"점검 중" 원인**: 경매장 서버(10060)가 안 떠 있었다. 등록은 메인 DB outbox에 쌓여 성공처럼 보이고, 검색·내 매물·거래소는 `AuctionUnavailable`. → `ServerRunner`가 경매장을 먼저 빌드 후 `start /b`로 함께 띄운다(두 서버가 `AuctionProtocol` obj를 공유해 빌드를 나눴다). `auction.sqlite3*`는 `.gitignore`.
- **가격 밴드 폐지 결정(태웅)**: 단가 1 G~무제한. 즉시 판매가(`BasePrice` x `SellRatePermille`)는 인벤토리 즉시 판매 값일 뿐 경매 바닥이 아니다. 클라 검사는 풀었고(`AuctionModel.MinUnitPrice = 1`), 서버 `AuctionRules.IsInBand`·기획 D안 변경은 이슈로 요청.
- 캐릭터·장비 즉시 판매는 기존 T-075가 맡는다(새 일감 없음).

## 업데이트 2 (2026-09-30) — 가격 밴드 유지 · 탭 3개 · 내 매물·정렬
- **가격 밴드 폐지 요청은 철회**(태웅 — 시세 조작 대응이 이유라 유지). 기획 문서·이슈는 원래 건드리지 않았고, 클라 완화만 되돌렸다(`AuctionModel.MinUnitPrice/MaxUnitPrice(basePrice)` = 서버 `AuctionRules` 사본).
- 대신 **즉시 판매 vs 경매 등록**을 화면마다 구분해 적는다 — 인벤토리 툴팁 두 줄 · [즉시 판매] 버튼 · 등록 안내문·힌트·확인 창.
- 경매장 탭 **구매(자원·장비·캐릭터 축) · 등록 · 내 매물**. 거래소 = 플레이어 자원 매물의 종류별 묶음 창(서버 상점 아님) — 구매 › 자원 축으로 들어갔다. `AuctionTab` int가 바뀌어 씬을 다시 배선했다.
- 내 매물 판별: 매물에 판매자가 없어 **내 매물 목록 ID**로 가린다(구매 화면 열 때 조용히 조회 · 빈도 제한 밖). 거래소 구매는 서버가 내 매물을 건너뛰므로 살 수 있는 수량에서도 뺀다.
- 정렬은 로컬 사본만(`AuctionSort` — 안정 정렬). ⚠️ 원본 순서를 바꾸면 장비 [더 보기] 커서가 깨진다.
- 입력칸: 위로 넘침은 입력 중, 아래로 모자람은 입력 끝에 고친다(`AuctionInput`).
- `Character.xlsx` `CharacterTable.BasePrice` 추가(등급별 50~4000).
- 서버 요청 [이슈 #47](https://github.com/JeongTaeWoong99/Windows_simulator/issues/47): 판매자 이름 · 캐릭터 경매 · 개체 즉시 판매(T-075).

## 업데이트 3 (2026-10-01) — 가독성 · 우편 탭 · 정렬 2단 (T-096 · T-103)
- **공용 아이콘 칸 `ItemIconView`**(`UI/Shared` + `Assets/Prefabs/ItemIconView.prefab`) — 등급 바탕 · 이름 첫 글자(아이콘 에셋 전 임시) · 모서리 수량 · 능력치 칸. 경매장·우편·판매 목록·장비 고르기 줄 왼쪽에 `SquareLayoutElement`로 붙였다(줄 HLG가 높이를 강제 확장해 LayoutElement로는 정사각형이 안 된다).
- **줄 툴팁** — 경매 매물·거래소·등록 후보·우편(첨부 전부)·판매 목록·장비 고르기. 장비 개체 툴팁은 `EquipLabel.BuildTooltip` 하나로 합쳐 인벤토리와 같게.
- **능력치 칸 읽기를 `EquipLabel.ReadStatOptions`로 옮겼다**(`EquipSlotSource`에서) — 경매장 매물이 같은 칸을 그린다. 새 구조를 `AuctionListingInfo`·검색 필터·우편 `Equips`에도 실어 달라고 [이슈 #46](https://github.com/JeongTaeWoong99/Windows_simulator/issues/46)에 코멘트.
- **문구** — `UIRichText`(라벨 TextSub · 가격 Highlight · 부족 Negative). 등록 안내문·힌트를 경매/즉시 판매 줄로 나눴고, 거래소 가격대는 `<pos>` 열 맞춤.
- **정렬**: "받은 순" 제거 → 낮은 가격순(기본) ↔ 높은 가격순. ⚠️ 장비 검색은 서버가 싼 순 20개씩(커서·`HasMore`) 주므로 높은 가격순은 받은 페이지 안에서만 맞다. [더 보기]에 받은 수.
- **우편함 탭** 안 받은/받은 — 모두 받기는 안 받은 탭만, 받은 탭은 "n일 뒤 삭제"(받은 뒤 7일 · 기획 우편 1장 9번).
- 드롭다운 라벨 오른쪽 28px 비움(화살표와 겹침). 정렬 버튼 폭 130.
- **판매 합계 식을 서버와 맞췄다** — `AuctionModel.InstantSellTotal` = BasePrice x 개수 x 비율 / 1000(서버 `ShopService` 순서). 이전엔 BasePrice x 개수라 비율이 1000이 아니면 어긋났다(지금 값은 1000이라 드러나지 않았다).
- ⚠️ 폰트(neodgm_pro SDF)가 **Static**이다 — 새 문구의 글자 241자를 검사해 없는 `→`만 바꿨다. 새 문구에 기호를 넣으면 같은 검사를 한다.
- 실측 안 함(플레이 모드 필요).
