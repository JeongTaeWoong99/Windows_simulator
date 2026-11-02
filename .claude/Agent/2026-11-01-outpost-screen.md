---
date: 2026-11-01
title: 거점 화면 구현 — 지도 · 건물 창 · 작업장 특성 트리, 인벤토리 특성 탭 제거 (T-182 · #75)
tags: [client, ui, outpost, trait, scene]
---

# 거점 화면 구현

## 목적 / 배경
- 서버의 건물·트리 개편(T-183 · PR #96)에 맞춰 클라 화면을 만든다. 하단 메뉴 [거점] → 3열을 덮는 지도 → 건물 메뉴 → 건물 창(업그레이드 · 특성).

## 변경 내용
- `UI/Outpost/` 신규 — 캔버스 · 머리 줄 · 지도 · 건물 창 · 업그레이드. `UI/Inventory/TraitPresenter` → `UI/Outpost/TraitPresenter`로 옮겨 트리형으로 다시 썼다.
- `TraitRowView`(스크립트 · 프리팹) 삭제. 새 프리팹 `OutpostBuildingView` · `OutpostCostRowView` · `TraitLinkView`.
- `InventoryTab.Trait` 제거 — 탭 줄 · 도구 줄 · 판매 목록 · 창고의 특성 분기 삭제.
- `UIManager.ShowOutpost` — 경매장 · 아티팩트 · 거점은 서로 닫는다.
- `Loc_UI.xlsx` — `UI.Main.Outpost` · `UI.Outpost.*` 30키.
- 씬 — `!Overlay Canvas [2] #Outpost Canvas`, 메뉴 5번 버튼 → 거점, 인벤토리 탭 버튼 4 제거.

## 주요 결정 / 근거
- 사용자 결정 — 코드 접두 `Outpost`, 트리 없는 4채는 업그레이드만 열고 "준비 중" 안내, 닫기는 [X]만, 그림은 임시(T-191).
- `Building Presenter`는 창 머리 줄이고 페이지와 **형제**다(Presenter 중첩 금지) → 창 여닫기는 `window` 필드로 한다.
- 노드는 목업의 원형 + 아이콘 대신 기존 `TraitNodeView` 카드 재사용 — 정식 아이콘이 없고 이름 · 레벨 · 눈금이 이미 된다.
- 격자 숨기기(`SetShown`)는 지금 쓰이지 않지만 남겼다 — 씬의 하위 Canvas까지 걷어 내는 위험에 비해 얻는 게 없고, 공급자 없는 탭이 생기면 다시 필요하다.

## 후속 작업 / 주의사항
- 사용자 실측 완료(T-182 보관). #75로 서버에 `UserTraitLevelTable` · `S_AccountLevelResponse.TraitPoint` 삭제를 넘겼다(T-183 마지막 항목).
- ⚠️ 선은 영역 실제 크기로 재므로 켠 직후 **부모부터** 재배치해야 한다(`Outpost 규칙.md`).
- 그림은 `Windows_simulator_Art` 저장소 소관 — 메인을 받는 사람은 그 저장소도 함께 받아야 참조가 안 끊긴다.
- 게임뷰 캡처(`capture_game_view`)는 이 프로젝트에서 검게 나온다 — 플레이 중 `ScreenCapture.CaptureScreenshot`을 쓴다.
