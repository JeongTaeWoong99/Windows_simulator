---
date: 2026-08-19
title: WindowManager 시작 설정 권위 소스 = 에디터=인스펙터 / 빌드=저장값 (P1)
tags: [client, window, settings]
---

# WindowManager 시작 설정 동기화 (P1)

## 목적 / 배경
- 사용자 검토: `WindowManager`의 인스펙터 설정(Topmost/Scale/Anchor)이 에디터·플레이·빌드에 걸쳐
  동기화되지 않는다 — "인스펙터를 고쳐도 반영이 안 된다".
- 원인: `WindowManager`엔 `[ExecuteAlways]`·`OnValidate`가 없어 에디터에서 코드가 안 돌고,
  `LoadSettings`가 세 값을 **PlayerPrefs 우선**으로 읽어 한 번 저장되면 인스펙터가 무시됐다.
  위젯 위치가 되던 이유는 `WidgetPositionLayout`이 `[ExecuteAlways]`+`OnValidate`+"PlayerPrefs는 플레이 중에만"
  패턴을 갖췄기 때문.
- 사용자 결정(Option A): **에디터(편집·플레이)=인스펙터가 진실 / 빌드(.exe)=저장값이 진실.**

## 변경 내용
- `Managers/WindowManager.cs`
  - `LoadSettings`: Topmost/Scale/Anchor를 `#if UNITY_EDITOR`(인스펙터) / `#else`(PlayerPrefs)로 분기.
  - `MigrateAnchor`를 `#if !UNITY_EDITOR`로 가둠(저장값 읽는 빌드에서만 필요 — 에디터 미사용 경고 방지).
  - `#if UNITY_EDITOR OnValidate` + `MirrorAnchorToWidgetIfAlive` 추가: 편집 중 `setStartAnchor`를
    씬의 `WidgetPositionLayout`에 거울질(위젯 미리보기 즉시 반영). `delayCall`로 미루고 `SetDirty`.
- `UI/Layout/WidgetPositionLayout.cs`: `LoadSavedPosition`을 `Application.isPlaying` 기준 →
  `#if UNITY_EDITOR return / #else 저장값`으로 정렬(창 앵커와 같은 규칙).
- 문서: `Settings 규칙.md` §1 전면 개정, `Managers 규칙.md`(권위 소스 절 추가·MigrateAnchor 가드),
  `WindowSettings.cs` remark. 날짜 헤더 갱신.
- 씬: 드리프트돼 있던 위젯 position(4=LowerCenter)을 창 앵커(3=LowerLeft)로 동기화하고 저장(부팅 일관성).

## 주요 결정 / 근거
- **`#if UNITY_EDITOR`로 "에디터 전체(편집+플레이)=인스펙터"** — 사용자 의도가 "유니티 안=인스펙터,
  .exe=저장값"이라, `WidgetPositionLayout`의 `Application.isPlaying`(에디터 플레이도 저장값 읽음)과 달리
  에디터 플레이도 인스펙터를 쓰게 했다. 두 컴포넌트를 같은 규칙으로 맞춰 창·위젯이 안 어긋나게 함.
- **미러는 Window→Widget 단방향, `#if UNITY_EDITOR` 격리** — 창 앵커가 런타임 권위(`SettingPresenter`가
  `window.AnchorIndex`로 위젯을 몰이)라 편집 미리보기도 창을 기준으로. 순수 에디터 글루라 Managers→UI
  실행 의존을 만들지 않는다.
- **트레이드오프(수용)**: 에디터 플레이 중 UI 변경은 종료 시 인스펙터로 리셋(공장 기본값 보존). 지속은
  빌드에서 PlayerPrefs로만. 에디터/빌드 PlayerPrefs는 저장 위치가 달라 서로 간섭 안 함.

## 후속 작업 / 주의사항
- 창 자체는 `#if !UNITY_EDITOR`라 **에디터에선 안 움직인다** — 편집 미리보기 대상은 위젯뿐(문서 명시).
- P2(System 캔버스 구조 통일 + 로딩 깜빡임 CanvasGroup·grace), P3(죽은 SaveBool 제거 등)는 계획 파일에
  대기 중. → `~/.claude/plans/c-users-asus-desktop-md-a-2-a-5-dynamic-scone.md` 후속 라운드 절.
- `SetPosition`/`SetAnchorByIndex` 등은 에디터에서도 PlayerPrefs에 쓰지만 에디터 로드가 무시하므로 무해(가드 안 함).
