---
date: 2026-09-18
title: 설정에 프레임 제한 · FPS 텍스트 위치 드롭다운
tags: [client, ui]
---

# 설정에 프레임 제한 · FPS 텍스트 위치 드롭다운

## 목적 / 배경
- 상주 앱이라 프레임(CPU·GPU 사용량)을 사용자가 고르게 한다 → `tasks/archive/T-062`
- 사용자 요청으로 범위 추가 — 창 구석 FPS 텍스트와 그 위치(숨김 포함) 선택

## 변경 내용
- `Managers/FrameRateManager.cs` 신설 — 프레임 적용 · FPS 위치 이벤트. 씬 루트 `Frame Rate Manager`
- `UI/System/FpsTextPresenter/FpsTextPresenter.cs` 신설 — `!System Canvas` 첫 자식
- `SettingPresenter` — 드롭다운 2개(`Dropdown Panel`의 `Window Position Dropdown` 복제)
- `WindowSettings` — `Display.FrameRate` · `Display.FpsTextPosition`
- 문서: `Settings 규칙.md` §1 예외·§4 · `Managers 규칙.md` · `System 규칙.md` · `UI 배치 현황.md` · `폴더 구조.md`

## 주요 결정 / 근거
- **에디터도 저장값** — 사용자가 "저장값 우선 · 바꾸면 저장 · 즉시 반영"을 원했다. 프레임은 창 모양과 달리 에디터에서도 실제로 먹어서 창 설정 규칙(에디터=인스펙터)의 예외로 뒀다
- `WindowManager`에 붙이지 않았다 — 이미 1193줄의 창 제어 전담이고, 프레임은 Win32와 무관하다
- FPS 텍스트는 오버레이가 아니라 `CanvasGroup`·차단막 없이 `text.enabled`만 토글 — 자기를 끄면 이벤트를 못 받는다

## 후속 작업 / 주의사항
- ⚠️ FPS 텍스트 `raycastTarget`을 켜면 동적 클릭스루가 그 구석을 콘텐츠로 판정해 클릭이 안 통과한다 (코드가 `Start`에서 끈다)
- 복제한 드롭다운의 영구 `onValueChanged` 호출은 비웠다 — 옛 대상(창 위치)을 부르지 않게
- 남은 것: 에디터 플레이 실측 · 에디터 `모니터 동기화`(Game 뷰 VSync 토글이 덮는지) · 빌드 실측 → `tasks/archive/T-062`

## 업데이트 (2026-09-18)
- 사용자가 에디터·빌드 확인 통과 → T-062 완료 보관 (`70e458d`)

## 업데이트 (2026-09-18) — `FrameRateManager` → `DisplayManager` 개명
- **설정마다 매니저를 만들지 않는다** — 주인은 설정이 무엇을 조작하느냐로 가른다(창 모양 → `WindowManager`, 표시·성능 → `DisplayManager`). 해상도·품질 같은 설정이 생기면 새 매니저가 아니라 여기로 온다
- 이름을 `Display.*` 키 접두사와 맞췄다. `.cs`·`.meta`를 함께 `git mv`해 GUID를 유지 → 씬 참조는 그대로
- 씬의 `m_EditorClassIdentifier`는 유니티가 다시 써 주지 않아 파일을 직접 고쳤다(오브젝트 이름도 `Display Manager`)
- 보류: `WindowSettings`가 `Display.*` 키까지 들고 있어 이름이 좁다 — 설정 축이 하나 더 생길 때 개명·분리한다
- 보류: partial 분할은 하지 않기로 했다 — 역할 이름(`Base`/`Api`)이 아니라 기능 단위 클래스 추출이 먼저
