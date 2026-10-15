---
date: 2026-10-15
title: 창고 화면 · 인벤토리↔창고 끌어 넣기·꺼내기 (T-126)
tags: [client, ui, network]
---

# 창고 화면 · 인벤토리↔창고 끌어 넣기·꺼내기 (T-126)

## 목적 / 배경
- 서버 창고(T-107)가 섰다 → 거점 열 창고 탭의 `(기능 없음)`을 실제 화면으로. 동선은 칸 끌어 놓기(이슈 #58 결정).

## 변경 내용
- `UI/Hub/WarehousePresenter/WarehousePresenter.cs` (새 파일) — 3탭 · [정렬] · 탭은 `InventoryTabPresenter.RequestTab`에 요청하고 `TabChanged`를 따라간다
- `UI/Inventory/InventoryGridPresenter/InventoryGridPresenter.cs` — `container` 필드 · static `Grids` · 공유 대기 `s_slotWaitHandle` · 다른 격자 놓기(`FindDropTarget`·`FirstEmptyFrame`·`TransferTo`·`SendTransfer`) · 창고 격자는 클릭 조작 없음
- 슬롯 공급자 4개 — `(data, container)` 생성자, `Fill`이 `Container`로 거름, 툴팁 조작 줄
- `Managers/PlayerDataModel.cs` — `GetItemCount(itemId)`는 인벤토리만 · 창고 칸 수 · `WarehouseTransferResponded` + Changes 반영
- `ServerPacketHandler.cs` — `Handle_S_WarehouseTransferResponse`
- 창고 것 제외: `AuctionRegisterPresenter` · `InventoryToolPresenter` · `WorkStationSelectPresenter` · `EntityBlockText.InWarehouseText` · `ResultMessages` 거절 문구 3개
- 씬: `Warehouse Page` → `Warehouse Presenter (↓ SUB VIEW)` — 탭 줄·도구 줄·격자를 인벤토리에서 복제
- 문서: `Inventory 규칙.md` "창고 격자" 절 · `Hub 규칙.md` "창고 화면"

## 주요 결정 / 근거
- 격자를 새로 짜지 않고 `InventoryGridPresenter`를 `container = Warehouse`로 재사용 — 칸·스크롤·끌기·정렬이 같다.
- 탭 전환의 주인은 `InventoryTabPresenter` 하나 — 둘이 각자 들면 한쪽만 바뀌는 순간이 생긴다. 특성 탭이면 창고는 보던 탭에 머문다.
- 다른 격자의 빈 곳에 놓으면 첫 빈 칸 — 넣는 게 본뜻이라 칸을 겨누지 않아도 된다.
- 요청 대기는 격자끼리 하나 — 이동이 두 보관함을 함께 바꿔 엇갈린다.

## 후속 작업 / 주의사항
- 에디터 실측(테스트 계정 `t126test` — 로컬 DB에 생김) 정상. 미검증: 캐릭터 탭 이동 · `StorageEntityBusy`.
- 남은 것: 사용자 실측 → T-126 Closed · 이슈 #58 정리.

## 이어서 (같은 날 · 사용자 실측 뒤)
- 창고 정렬을 인벤토리와 같게 — ▼▲ 방향 + 기준 드롭다운(위젯은 인벤토리 도구 줄 복제 · `unity cmd eval_file`로 씬 배선). 코드만 먼저 컴파일되면 `RequireRef`에서 창고 Start가 멈춘다 — 씬 배선까지 한 번에.
- 경매 등록 키 Shift+우클릭 → Ctrl+우클릭 — 2026-10-14 지시(좌·우·Ctrl+좌·Ctrl+우)를 그때 세션이 Shift로 바꿔 해석했다.
- 판매 목록에 담긴 개체를 잠그면 `SellCartModel.OnEntitiesChanged`가 뺀다 · 올릴 수 없는 개체를 Ctrl+우클릭하면 경매장을 열지 않고 사유만.
- 창고 물건 사용은 서버 규칙("꺼내야 씀")이라 이슈 #74 · T-164/T-163으로 넘겼다. T-126·T-107 닫음, #58 닫음.
