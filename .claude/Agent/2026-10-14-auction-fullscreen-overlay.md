---
date: 2026-10-14
title: 경매장을 화면 전체로 — 거래 열 → 거점 열 · #Auction Canvas (T-147 1단계)
tags: [client, ui]
---

# 경매장을 화면 전체로 — 거래 열 → 거점 열 · #Auction Canvas (T-147 1단계)

## 목적 / 배경
- 경매장을 거래 열의 탭이 아니라 3열 폭 전부 × 가운데 띠로 연다 → `tasks/T-147` · 이슈 #69. 목업대로 재배치하기 전 단계로, 지금 있는 하위 탭(구매·등록·내 매물)을 넓은 판에 그대로 옮겼다.
- 사용자 결정 (2026-10-14): 열 이름 **HUB / 거점** · 하단 메뉴 [경매장]으로 열고 **[X] + Esc**로 닫음 · Shift+우클릭은 목업 안대로 · 열 코드까지 전부 개명 · **화면 전체 차단막**(상태 줄·위젯까지 덮음).

## 변경 내용
- `UI/Market` → `UI/Hub`(열) + `UI/Auction`(경매장) — `git mv`로 `.meta` GUID를 그대로 옮겨 씬 참조가 안 끊겼다. 구조·규칙은 `Hub 규칙.md` · `Auction 규칙.md`에.
- 새 파일: `AuctionCanvasView`(여닫기 · 가운데 띠 맞춤 · `OpenRegister` · `IsCovered`) · `AuctionHeaderPresenter`([X]·Esc).
- `WidgetPositionLayout.TryGetCenterBandInsets` 추가 · `KeyInputManager.WasClosePressed`(Esc) 추가.
- 씬: `@Hub Column`·`#Hub Canvas` 개명, 거점 탭에서 경매장 탭 제거, `!Overlay Canvas` 첫째 자식 `#Auction Canvas` 신설, 하단 메뉴 `xxx Button (3)` → [경매장].

## 주요 결정 / 근거
- **오버레이 안이지만 `SetActive`·`(MAIN VIEW)` 캔버스다** — Presenter 7개를 담아 SUB VIEW 하나로 둘 수 없고(Presenter 안 Presenter 금지), 안의 Presenter들이 `OnEnable`에서 다시 받는 동작에 기대고 있어 `CanvasGroup`으로 바꾸면 열 때 갱신이 안 된다. 다시 켜 줄 손(`UIManager`)이 밖에 있으니 로그인과 같은 예외.
- **첫째 자식(맨 뒤)** — 경매장이 띄우는 확인·수량 팝업·툴팁·로딩·알림이 전부 그 위에 떠야 한다.
- **Esc는 `IsCovered`(내 뒤 형제 중 `blocksRaycasts` 켜진 CanvasGroup)가 거짓일 때만** — 로딩 차단막은 클릭만 막고 키는 못 막는다. 요청 중 닫히면 Presenter가 구독을 끊어 응답을 놓친다(거래 열 시절 가챠 사고와 같은 뿌리). `ServerWaitManager`에 IsBusy를 더하는 대신 이 하나로 팝업·로딩·알림을 다 덮었다.
- **띠 위치는 거점 열의 스페이서 높이에서 읽는다** — 작업슬롯 열의 위·아래는 상태·위젯 캔버스라 꺼질 수 있고, 꺼진 `LayoutElement`는 `LayoutUtility`가 0으로 읽는다(같은 파일의 `FixedHeightOf` 주석).
- **판을 불투명 검정 + 자식 `Background`(PanelBg)** — PanelBg는 a 0.92라 뒤의 3열 글자가 비쳤다. `UIThemeColor.alpha`는 0~1이라 1을 넘겨 불투명하게 만들 수 없다.

## 후속 작업 / 주의사항
- 2단계: 목업(검색·시세·판매·완료 · 공용 검색창) 재배치 — T-155·T-156·T-124·T-119 배선을 함께. → `tasks/T-147`
- Esc 키 자체는 eval로 누를 수 없어 실측 안 함 — 사람 손으로 확인.
- `HubTab`에서 Auction(1)을 빼며 Warehouse가 2 → 1로 당겨졌다 — 씬 배선은 다시 넣었다. `Test Copy` 씬(로컬 전용)은 옛 값이라 다시 복사해야 한다.
- `capture_game_view --save_path`는 프로젝트 안 경로만 받고 `Assets/Temp/`에 저장한다 — 찍은 뒤 지울 것.
