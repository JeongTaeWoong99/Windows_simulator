---
date: 2026-10-21
title: 창 여닫기 · 숫자 카운트업 · 버튼 눌림 연출
tags: [client, ui]
---

# 창 여닫기 · 숫자 카운트업 · 버튼 눌림 연출

## 목적 / 배경
- 폴리싱 목업(HTML)에서 사용자가 고른 2·3·4번 — 숫자 카운트업 · 창 열기·닫기 · 버튼 눌림. 일감 없이 바로 진행했다.

## 변경 내용
- `UI/Shared/PanelTransition` — 캔버스(`SetActive`)·상주 팝업(`SetVisible`) 여닫기 연출. 대상에 저절로 붙는다(+`CanvasGroup`).
  - 붙인 곳: `MainCanvasView`·`StateCanvasView`·`InventoryCanvasView`·`HubCanvasView`·`AuctionCanvasView`(판 `body`만 커짐) · `ConfirmPresenter`·`AmountInputPresenter`(새 선택 필드 `panel` — 씬에 MCP로 연결).
- `UI/Shared/NumberRoll` — 숫자 굴림. 상태 패널 골드 · 위젯 누적 골드 · 경매장 머리 줄 골드(튐 없음) · 작업슬롯 배율(쉬는 칸은 `Forget`).
- `UI/Shared/ButtonPressFx` — 씬 `Event System`에 하나(MCP로 추가). 누른 순간 커서 밑에서 클릭을 받을 `Button`을 찾아 줄인다.
- `UIManager` — 열림 판정·토글을 `IsShown`으로. `ShowMainScreen`은 패널을 먼저 고르고 캔버스를 켠다. `CloseAllExceptWidget`은 패널을 갈아 끼우지 않고 `CurrentMainScreen`만 기본으로.

## 주요 결정 / 근거
- **버튼은 하나씩 붙이지 않았다** — 씬·프리팹에 200개가 넘고 목록 줄은 실행 중에 생긴다. 차단막(`Dim Button` 1920×920)은 크기 제한(700×300)으로 뺀다.
- **닫힘을 늦추면 `activeSelf`가 거짓말을 한다** — 닫는 0.1초 동안 켜져 있다. 토글이 그걸 열림으로 읽으면 다시 누른 버튼이 또 닫기가 된다 → `IsShown`.
- **전부 닫기에서 메인 화면을 바로 갈아 끼우지 않는다** — 흐려지는 동안 목록이 비친다. 다음 열림의 `ShowMainScreen`이 캔버스를 켜기 **전에** 갈아 끼운다(켠 뒤면 남은 화면의 `OnEnable`이 한 번 돈다).
- 숫자 처음 값·꺼진 글자는 바로 쓴다 — 접속할 때마다 0 → 보유 골드가 굴러가면 돈이 들어온 것처럼 보인다.
- 숫자 튐 0.12는 골드 앞자리가 왼쪽 동전 아이콘 밑으로 들어가 0.08로 줄였다(캡처로 확인).

## 검증
- 컴파일·콘솔 오류 없음. 플레이 모드(로그인 화면만 끄고)에서 `DOTween.timeScale` 0.01로 늦춰 확인:
  인벤토리 열림(α 0.26 · 0.97) · 확인 팝업 열림 · 골드 0 → 12,480 굴림 · 버튼 눌림 0.94 유지 → 뗌 1.009 튕김 → 1 ·
  닫는 중 다시 열기 · 설정 화면에서 전부 닫기 → 다시 열면 목록 · 경매장 열고 닫기 → 끝 상태 α 1 · 크기 1 · 꺼짐.

## 후속 작업 / 주의사항
- 버튼 눌림은 MCP 입력 주입이 게임 뷰에 들어가지 않아 `Press`/`Release`를 직접 불러 확인했다 — 실제 클릭으로 버튼 찾기(`FindButton`)는 실플레이 확인이 남았다.
- 메인 화면끼리(목록↔선택↔설정) 바꾸는 것엔 연출을 넣지 않았다.
