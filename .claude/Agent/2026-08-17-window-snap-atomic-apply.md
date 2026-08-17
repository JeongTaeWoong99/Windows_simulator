---
date: 2026-08-17
title: 창 스냅 어긋남 수정 — 크기·위치 원자 적용 (A-1)
tags: [client, window, win32, a-1]
---

# 창 스냅 어긋남 수정 — 크기·위치 원자 적용 (A-1)

## 목적 / 배경
- `먼저 할일.MD` A-1 (1): DPI 배율(125/150%)·다중 모니터·"크기와 위치를 연속으로 바꾸는" 상황에서
  창이 지정 9분할 앵커에 정확히 스냅되지 않던 문제. 범위는 (1) 스냅 수정만 — Fallback(2)·UI 정리(3)는 제외.

## 변경 내용
- `Assets/Scripts_Client/Managers/WindowManager.cs`
  - `ApplySizeAndPosition(scale, anchor)` 신설 — 크기·위치를 한 기준 모니터로 원자 적용.
  - `AnchorPosition`(정적)·`BaseSize`(원시 프리셋 px)·`ClampToWorkArea(size, wa)` 분리.
  - `SetWindowSizeByIndex`·`SetAnchorByIndex`·`SetTitleBar` → `ApplySizeAndPosition` 호출로 교체.
  - 제거: `ResizeWindow`·`MoveWindow`·`ApplyPosition`·`GetSize`·`GetWorkAreaSize`.
- `Assets/Scripts_Client/Managers/Managers 규칙.md` §5 — 원자 적용 원칙 서술 추가.

## 주요 결정 / 근거
- **버그의 뿌리는 크기·위치를 따로 적용한 것**이었다. 두 지뢰가 겹쳤다:
  1. 리사이즈(SWP_NOMOVE)로 좌상단 고정한 채 창을 키우면, 그 커진 사각형으로 `MonitorFromWindow`가
     **다른 모니터**를 잡아 클램프와 앵커가 서로 다른 작업 영역 기준으로 계산됨(멀티모니터 오스냅).
  2. 옛 `ApplyPosition`이 외곽 크기를 **다시 추정**해, `ResizeWindow`의 실측 보정(dx/dy)과 어긋나
     오른쪽·아래 앵커가 프레임 두께만큼 넘침(DPI 배율 오차).
- 그래서 ① 작업 영역을 **한 번만** 고정(`TryGetWorkArea` 1회) → ② 단일 `SetWindowPos`로 이동+크기 동시 →
  ③ 실측 보정 시 **앵커 좌표까지 보정된 외곽으로 재계산**. 크기·위치가 항상 같은 외곽을 공유하게 만든 게 핵심.
- 대안 B(ApplyPosition만 GetWindowRect로 실측)는 원인 1은 잡아도 2(모니터 오판정)·비원자성이 남아 기각.

## 후속 작업 / 주의사항
- **검증은 빌드 필수** — Win32는 `#if !UNITY_EDITOR`라 에디터에선 값만 바뀐다. DPI 125/150%,
  듀얼 모니터, 크기↔위치 연속 변경, 타이틀바 토글 후 16:9 유지를 실제 exe에서 확인해야 한다.
- `BaseSize`·`ClampToWorkArea`는 `#if` 밖에 있어 에디터에선 미참조(옛 `GetSize`와 동일 패턴 — 컴파일 OK).
- A-1 (2) Fallback·(3) UI 정리(토글 3개 제거·캔버스 드래그 이동)는 미착수. → `먼저 할일.MD` A-1.
