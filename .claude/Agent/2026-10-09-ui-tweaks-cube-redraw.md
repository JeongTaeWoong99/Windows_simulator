---
date: 2026-10-09
title: 획득 아이콘 1.5배 · 캐릭터 자리 비율화 · 큐브 제외 토글 · 가챠 다시 뽑기
tags: [client, ui]
---

# 획득 아이콘 1.5배 · 캐릭터 자리 비율화 · 큐브 제외 토글 · 가챠 다시 뽑기

## 목적 / 배경
사용자 요청 네 가지를 한 번에 처리했다.
1. 획득 연출 아이콘 1.5배 — `ItemGainEffectView`의 `iconSize`·`iconSpacing` (SO가 아니라 **프리팹 컴포넌트 값**)
   - `WorkStationSlotView.prefab` 24→36 / 4→6 · `WidgetMiniSlotView.prefab` 12→18 / 2→3
2. 슬롯 무대 캐릭터가 오른쪽 끝에 쏠림 → 열 칸 중 7~8번째 자리로
3. 일괄 담기에 [큐브 제외] 토글 (기본 켬) — [상자 제외]와 같은 방식
4. 가챠 결과 팝업 닫기 왼쪽에 [n회 더 뽑기]

## 주요 결정 / 근거
- **`SlotStageSettings.rightPad`(아트 px) → `characterAnchor`(패널 폭 비율, 0.7)** — 칸 폭이 창 크기에 따라 바뀌므로(지금 셀 약 296px)
  픽셀 여백이면 넓을수록 오른쪽으로 쏠린다. 발 X = `Round(width × anchor)`. `stopGap`은 그대로 발 기준 아트 px.
- 큐브 판정은 `GameDataLoader.IsCube` = `EnchantItemTable`에 행이 있는가 (큐브 창 버튼 목록과 같은 출처).
- 알림 문구는 실제로 뺀 쪽 토글만 적는다 — "(상자 2종 · 큐브 1종 제외 — [상자 제외]·[큐브 제외]를 끄면 함께 담깁니다)".
- 툴 줄 폭이 모자라 `Bulk Rarity Dropdown` 폭 170→150.
- **다시 뽑기 요청은 `GachaPresenter.DrawAgain`에 맡긴다** — 대기·연타 방지·실패 알림을 한 벌로. 마지막에 누른 줄(`_lastIndex`)을 기억.
  팝업은 `DrawStateChanged`를 듣고 잠금(`CanDrawAgain` = 패널 켜짐 · 대기 아님 · 골드 충분)을 맞춘다. 상자·우편 결과에서는 버튼을 숨긴다.
- 씬: `Gacha Result Presenter/Panel/Button Row`(HorizontalLayout) 아래 `Draw Again Button` · `Close Button`.

## 후속 작업 / 주의사항
- 검증: 컴파일 성공 · 플레이 모드 진입 에러 0(RequireRef 통과). **실제 가챠 다시 뽑기·큐브 제외 담기는 로그인 후 실측 필요.**
- 아이콘 크기·캐릭터 자리는 사용자 조정값 — 이후 덮지 않는다.
