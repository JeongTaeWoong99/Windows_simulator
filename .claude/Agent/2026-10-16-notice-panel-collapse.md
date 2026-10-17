---
date: 2026-10-16
title: 경매 등록·수령 뒤 회색 화면 — 알림 창이 좁은 화면에서 접혔다 (T-167)
tags: [client, ui, overlay, auction]
---

# 경매 등록·수령 뒤 회색 화면 — 알림 창이 좁은 화면에서 접혔다 (T-167)

## 목적 / 배경
- 진우 보고(이슈 #76): 로컬에서 경매 판매 등록·대금 수령 뒤 회색 화면에서 넘어가지 않는다. 서버 로그는 정상.
- 할 일·완료 조건 → `tasks/archive/T-167-경매장연결점검.md`

## 주요 결정 / 근거
- **회색 = 알림(`Notice Presenter`) 차단막, 화면 가운데 `|` = 접힌 알림 창.** `Panel`이 stretch 앵커 + `sizeDelta(-1720, -680)`이라
  캔버스 폭이 1720 아래면 폭이 음수가 된다. `Canvas Scaler` 세로 맞춤(match 1) → 캔버스 폭 = 화면 폭 × 1080 / 높이. 진우 1221×842 → 1566.
- 대기(`ServerWaitManager`)는 의심했지만 아니었다 — 5초 타임아웃이 있고, 테스트 계정(`claudetest`)으로 재현해 보니 대기 수 0 · 로딩 꺼짐 · 알림 alpha 1 · 문구 정상.
  1920×1080 에디터에서는 창이 200×400으로 보여 증상이 안 났다.
- 수정은 앵커만 — 가운데 고정 200×400(1920에서 보이던 크기 그대로). 다른 오버레이 창은 이미 가운데 고정이었다. `Overlay 규칙.md`에 금지 규칙으로 남겼다.
- 재현 방법: 플레이 중 `eval_file`로 `LoginPresenter.OnLoginButtonClicked`(입력칸 채움) → 치트 `C_CheatRequest`(골드·자원) → `AuctionCanvasView.OpenRegister` → 폼 입력 → `OnRegisterClicked` → `ConfirmPresenter.OnConfirmClicked`.
  등록 창은 첫 `Open`이 `Start` 전이면 보류만 하므로 두 번 부른다. 게임 뷰 캡처는 오버레이 UI가 안 찍혀 검은 화면이다 — RectTransform 월드 좌표로 확인했다.

## 후속 작업 / 주의사항
- 실측 완료(2026-10-16 사용자) — 작은 창에서 등록·수령 뒤 알림 창이 보이고 닫힌다.
- 서버 관찰: 등록 직후 `내 매물`이 0건 — `S_AuctionMyListingsResponse`가 경매장 등록 전송 완료보다 먼저 나간다(이슈 #76 코멘트).
