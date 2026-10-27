---
date: 2026-10-27
title: 거점 열 → 길드 열 (Hub → Guild) 개명
tags: [client, ui, data, docs]
---

# 거점 열 → 길드 열

## 목적 / 배경
- T-183이 [거점]을 지도 화면 이름으로 정해 가운데 열과 이름이 겹쳤다 — T-182 0·1번. 사용자가 상회를 거쳐 **길드 / Guild**로 정했다

## 변경 내용
- 폴더 `UI/Hub/` → `UI/Guild/` · `HubCanvasView` → `GuildCanvasView` · `HubTabPresenter` → `GuildTabPresenter` · `HubTab` → `GuildTab` · `Hub 규칙.md` → `Guild 규칙.md` (`git mv`로 `.meta` 함께 — GUID 그대로)
- `UIManager.Hub/ShowHub/ToggleHub` → `Guild/ShowGuild/ToggleGuild` · 필드 `hubCanvas`·`hubButton`·`hubColumn` → `guild…` + `FormerlySerializedAs("hub…")`
- 씬: `@Guild Column` · `#Guild Canvas (MAIN VIEW)` · `Guild Button (2)` · `LocalizedText` 키 2곳
- `Loc_UI.xlsx` — `UI.Main.Hub`(거점/Base) → `UI.Main.Guild`(길드/Guild)
- 클라 규칙 문서 9개 · 기획 README 3곳 · T-182 · T-184 · INDEX(T-182를 ⏸로 — T-183 대기)

## 주요 결정 / 근거
- 옛 이름의 `FormerlySerializedAs("market…")`는 남기고 `hub…`를 더했다 — Test Copy 씬이 옛 이름으로 남아 있을 수 있다
- 문서 상단 `최종 업데이트` 이력 안의 '거점' 표기는 당시 기록이라 고치지 않았다

## 후속 작업 / 주의사항
- ⚠️ 클래스 개명 직후 **열려 있던 씬은 도메인 리로드로 새 필드가 NULL**이 됐다(FormerlySerializedAs가 메모리 속 객체엔 안 먹었다). 씬을 디스크에서 다시 열자 이어졌다 — 저장 전에 다시 여는 순서를 지킨다
- 남은 것: 지도 화면 · 특성 탭을 건물 창으로 → T-183 서버 개편 뒤
