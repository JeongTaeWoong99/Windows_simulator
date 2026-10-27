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

## 업데이트 (2026-10-27) — 탭 순서 · 편집 화면 글씨 · 감정 건너뛰기 오류

- 탭 순서 **상점 · 뽑기 · 창고** — `GuildTab` 값도 이 순서(Shop 0 · Gacha 1 · Warehouse 2)로 바꾸고 씬의 탭 줄 값·버튼·화면 형제 순서를 맞췄다. 기본 탭은 그대로 뽑기
- 편집 화면(실행 전) 글씨 — 열 제목 '거점' → '길드' · 하단 메뉴 '[거점]' → '[길드]' · 상점 탭 버튼 '창고' → '상점'(키 `UI.Shop.Tab`이라 실행하면 맞았다) · `Shop Presenter` 안 `Gacha Panel` → `Shop Panel`
- **오류** — 유물 감정 무대를 클릭으로 건너뛰면 DOTween `IndexOutOfRangeException`(RemoveActiveTween → 이후 Reorganize·`WidgetHeadMotion.KillAll`까지 줄줄이). 토큰 취소 → UniTask가 트윈 Kill → **그 Kill 안에서** `AppraiseThenShowAsync`의 await 뒤가 동기로 깨어나 `CancelAppraise` → `DOTween.Kill(this)`가 죽는 중인 트윈을 또 Kill했다. 건너뛰기로 깨어났으면 `UniTask.Yield` 한 번 쉬고 정리 · 쉬는 사이 새 감정이 시작됐는지 토큰으로 가른다. dotween 스킬 함정 표에 한 줄 더함(툴킷 사본 — 마스터 반영은 확인 후)
- ⚠️ 건너뛰기 수정은 플레이(로그인 + 유물 감정)로 아직 재현 확인 전
