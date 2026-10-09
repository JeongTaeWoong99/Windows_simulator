---
date: 2026-10-10
title: 프레임 여유 구조 정리 — 레이아웃 경계 · 격자 Canvas 숨김 · 레이캐스트 · 주기 수신 로그 (T-139)
tags: [client, ui, optimization]
---

# 프레임 여유 구조 정리 (T-139)

## 목적 / 배경
- 144fps(6.94ms)를 넘는 튀는 프레임 — 수확 8~9ms · 탭 전환 20~40ms → tasks/T-139
- 사용자 지시: 새 구조를 들이지 말고 **기존 규칙 안에서** 다듬는다 · 로그는 토글(b) · 진행 바는 설정 FPS를 따른다(주기 낮추기 안 함)

## 변경 내용
- 씬: `#Main`·`#Inventory`·`#Market` Canvas의 VLG → 자식 `Body Panel` · `Tool Presenter`의 VLG → 자식 `Tool Panel` · 격자에 `Canvas`·`GraphicRaycaster`
- `InventoryGridPresenter.SetShown` — 숨길 때 Canvas·레이캐스터 끄기 + `ignoreLayout`
- Raycast Target 244개 끔 — 프리팹 안의 것(WorkSlotFrame·CharacterStateRowView·Gacha Send Button)은 원본에서 (씬 인스턴스 덮어쓰기 0)
- `ClientLogger.ShowPeriodic` + `PeriodicLogSettings`(EditorPrefs) + 치트 창 도구 줄 토글 · `ServerPacketHandler` 수신 4종을 가드
- `ItemGainEffectView.Start`에서 `Prewarm(maxIcons)` · `WorkStationSlotView.SetRemainText`(0.1초 단위가 바뀔 때만) · `WidgetPositionLayout` 넘침 검사 1초 간격
- 문서: UI · Layout · Inventory · Log · cheat-console · Shared 규칙 · `ugui-layout` 스킬 §7 · CLAUDE.md 스킬 표

## 주요 결정 / 근거
- **레이아웃 경계 = 부모에 LayoutGroup이 없는 상자.** 더티는 부모에 그룹이 있는 동안 올라가고 중첩 Canvas를 보지 않는다. 기존 `Xxx Panel`(정렬 상자) 이름 규칙에 맞췄다
- 격자 Canvas는 `#` 접두를 붙이지 않는다 — 화면 단위 캔버스가 아니라 그리기 분리 부품
- 기각: RectMask2D(CPU +1.2ms) · 진행 바마다 Canvas(악화) · 아틀라스(이득 작음) · 재구성 합치기(0.3ms 이하)
- 도구 줄 경계는 측정 중 발견해 추가 — 장비 탭 전환 캔버스 갱신 8.3 → 1.3ms

## 후속 작업 / 주의사항
- 남은 탭 전환 15~20ms는 격자 칸 내용 다시 그리기 — 줄이려면 여러 프레임에 나눠 그리는 별도 일감
- ⚠️ 라이브 프로파일러(`ProfilerDriver.enabled`) 녹화가 `profiling::Dispatcher::AcquireFreeBuffer`에서 에디터를 죽였다 — `ProfilerRecorder`만 쓰는 쪽이 안전
- Test Copy 씬은 갱신하지 않았다 — 씬 복사 흐름으로 맞춘다
- `ugui-layout` 스킬 사본을 고쳤다 — 마스터 반영은 사용자 확인 후 `/unity-skill-sync`
