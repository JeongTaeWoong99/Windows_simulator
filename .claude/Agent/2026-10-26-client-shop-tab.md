---
date: 2026-10-26
title: 클라 상점 탭 — 캐릭터 뽑기권 A~D 구매 · 뽑기 탭 캐릭터 카드 삭제
tags: [client, ui, data]
---

# 클라 상점 탭 (T-184 · #78)

## 목적 / 배경
- 서버·데이터가 끝난 캐릭터 뽑기권 상점(→ `tasks/T-184`)의 클라 화면. 진우가 골드 직접 캐릭터 풀(`GachaInfoTable` 2)을 닫으려면 이 화면이 먼저 있어야 했다.

## 변경 내용
- 거점 열에 **상점 탭**(`HubTab.Shop` — enum 끝에 붙임) · `UI/Hub/ShopPresenter/ShopPresenter.cs`
- 뽑기 탭의 캐릭터 카드(풀 2)를 씬과 `pools` 배열에서 지웠다
- `GachaPoolCardView.Bind`는 버튼 머리줄을 **글자로** 받는다(뽑기 `n회` · 상점 `n장`) + `SetKind`
- `S_ShopBuyResponse` 수신 → `PlayerDataModel.ShopBuyCompleted/Failed`
- `Loc_UI.xlsx` — `UI.Shop.*` 5줄 · `UI.Result.InvalidShopRequest`
- 구조·규칙 → `UI/Hub/Hub 규칙.md` "상점 화면" 절

## 주요 결정 / 근거
- **카드는 뽑기 카드 프리팹을 그대로 쓴다.** 뽑기권은 여는 풀이 있어서 제목 괄호·확률 툴팁이 `GachaPoolSummary`로 그대로 나온다. 새 프리팹을 만들 이유가 없었다.
- **카드는 `ShopTable` 행마다 코드로 만든다**(뽑기 화면은 인스펙터 줄). 품목이 엑셀에 늘면 화면도 저절로 는다.
- **개수 선택은 [1장]·[10장] 두 버튼으로 했다**(서버는 1~50 허용). 수량 팝업을 붙이지 않은 건 뽑기 화면과 조작을 같게 하려고. 필요하면 `UIManager.AskAmount`로 쉽게 바꿀 수 있다.
- **상점에서 열지 않는다.** 뽑기권은 `OpenGachaId`가 있어서 인벤토리의 상자 개봉 흐름으로 이미 열린다(유물 감정·우편 넘침까지 처리돼 있음). 열기 버튼을 두 군데 두면 대기·연타 방지가 두 벌이 된다.

## 실측 (2026-10-26 · 로컬 서버 · 계정 `shoptest01`)
- 골드 0 → [1장] → `NotEnoughCurrency` 알림 ✅
- 골드 100,000 → A [10장] → 골드 90,000 · 보유 10장 ✅ · D [10장](200,000)은 잠김 ✅
- `C_ItemUseRequest 100071 ×1` → 캐릭터 1마리 지급 ✅

## 후속 작업 / 주의사항
- 진우: 골드 직접 캐릭터 풀(`GachaInfoTable` 2) 닫기 — 클라에서는 이미 카드가 빠졌다
- 파이프라인을 돌리면 `Assets/Plugins/Analyzers/MikaSourceGen.dll`이 다시 빌드돼 바이너리 diff가 생긴다 — 코드 변경이 없으면 되돌리고 커밋한다
- `capture_game_view --save_path`는 프로젝트 안 경로만 받고 결과를 `Assets/` 아래에 쓴다 — 찍은 뒤 옮기고 `.meta`까지 지운다
