---
date: 2026-10-10
title: 인벤토리 격자는 보이는 칸만 그린다 · 넘침 검사 GC 제거 (T-138)
tags: [client, ui, optimization]
---

# 인벤토리 격자는 보이는 칸만 그린다 · 넘침 검사 GC 제거 (T-138)

## 목적 / 배경
- 프로파일에서 인벤토리 열기 197ms 스파이크와 10프레임마다 3MB GC가 보였다 → tasks/T-138

## 변경 내용
- `InventoryGridPresenter` — `Redraw`가 뷰포트에 걸치는 프레임(+위아래 한 줄)만 `DrawCell`, 나머지는 `HideView`. `ScrollRect.onValueChanged` → `OnScrollChanged`가 범위 차이만 그린다
- `WidgetPositionLayout` — `CollectOverflow`는 `TryGetComponent`, `VerifyNoOverflow` 호출은 에디터·개발 빌드에서만

## 주요 결정 / 근거
- **칸을 프레임 사이로 옮기지 않았다** (프레임 200 · 칸은 프레임마다 하나 그대로, 켜고 끄기만) — 칸 번호(서버)·`InventorySlotDrag`의 프레임 번호·놓을 칸 덮개가 전부 프레임 기준이라 손대지 않아도 된다. 칸을 돌려쓰는(부모 이동) 정석 가상 스크롤은 끌기·레이아웃을 같이 고쳐야 해 기각
- 범위는 프레임의 월드 좌표로 잰다 — `FlexibleGridLayoutGroup`이 창 폭에 따라 열 수·칸 크기를 바꿔 줄 번호 계산이 어긋난다
- `ScrollRect`는 뷰포트·콘텐츠 크기가 바뀌어도 `onValueChanged`를 낸다 → 첫 레이아웃·창 크기 변경도 이 구독 하나로 잡힌다(플레이로 확인)
- 열 폭 검사(`VerifyColumnWidths`)는 복구(재배치)를 하므로 릴리즈에도 남겼다

## 후속 작업 / 주의사항
- ⚠️ `HideView`는 이미 꺼진 칸을 건너뛴다 — 칸을 끌 때 반드시 `Clear()`가 함께 불려야 한다(다른 경로로 칸을 끄지 말 것)
- 끌던 칸이 다시 그려져도 흐림을 유지한다(`DrawCell`의 `i == _dragFrom`)
- 처음 스크롤해 내려갈 때는 그 줄의 칸을 그때 만든다(프레임당 한 줄 몫) — 더 줄이려면 미리 만들어 두기
- 에디터 측정: 자원→캐릭터 탭 전환 147ms → 36ms (183명). 남은 36ms는 캔버스 전체 갱신 포함
