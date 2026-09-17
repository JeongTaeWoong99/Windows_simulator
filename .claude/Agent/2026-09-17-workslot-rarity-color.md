---
date: 2026-09-17
title: 작업슬롯 목록·위젯 미니 칸 바탕을 캐릭터 등급 색으로
tags: [client, ui]
---

# 작업슬롯 목록·위젯 미니 칸 바탕을 캐릭터 등급 색으로

## 목적 / 배경
- 작업슬롯 칸에서 일하는 캐릭터의 등급이 글자로만 보였다 → `tasks/archive/T-061`
- 같은 세션에서 T-039를 🎨 표로 옮겼다(사용자가 해금까지 플레이 확인)

## 변경 내용
- `WorkStationSlotView` · `WidgetMiniSlotView` — `backgroundImage` + `SetRarity(GlobalRarity)`
- `WorkStationListPresenter.Rebuild` · `WidgetPresenter.Rebuild` — `Bind` 뒤 `SetRarity` 호출
- `WorkStationSlotView.prefab` · `WidgetMiniSlotView.prefab` — 루트 Image를 `backgroundImage`에 연결

## 주요 결정 / 근거
- `Bind` 인자를 늘리지 않고 `SetRarity`를 따로 뒀다 — `CharacterStateRowView.SetRarity` 선례와 같은 모양
- 위젯은 `characterImage`가 아니라 루트 Image를 칠한다 — 캐릭터 그림 자리에 색을 입히면 나중 스프라이트가 물든다
- 연결은 씬이 아니라 프리팹에 했다 — 두 뷰 모두 런타임 `Instantiate`라 씬에 인스턴스가 없다
- 빈 칸 처리(`Unknown`)는 따로 두지 않았다 — 두 패널 모두 배치된 칸에만 뷰를 만들고 해제되면 파괴한다

## 후속 작업 / 주의사항
- 플레이 눈 확인 통과 (2026-09-17) — 흰 글자도 등급색 위에서 읽힌다
- `WorkStationSlotView` 루트 Image는 기존에 흰색이었다 — 이제 항상 등급 색으로 덮인다
