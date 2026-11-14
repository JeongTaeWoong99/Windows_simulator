---
date: 2026-11-14
title: 작업슬롯 2.5줄 · 칸 표시 모서리로 · 카드 문구 정리 · 빈 칸 점선 제거 · sprite-gen 후처리 이식
tags: [client, ui, art, pipeline]
---

# 작업슬롯 2.5줄 · UI 정리 · sprite-gen 후처리

## 목적 / 배경
- 사용자 지시 묶음: 작업슬롯을 2.5줄로(아래가 더 있다는 표시) · 빈 칸 점선 네모 제거 · 인벤토리 칸 표시 자리 정리 · 뽑기·상점 카드의 종수/보유 문구 제거 · sprite-gen 후처리 이식 후 커밋.

## 한 일
- `FlexibleGridLayoutGroup._fitRowsInParent`를 float로 — 사이 간격은 `Ceil(rows)-1`. 씬 값 2.5.
- `SlotView` 프리팹: '배'·'판' (-5,-5) 오른쪽 위 모서리 · 자물쇠 (-27,-7) · LV 배지 (5,5) 왼쪽 아래 · 경험치 게이지 왼쪽 벽 6~10, 아래 22 ~ 위 8.
- `GachaPoolCardView`: `SetKind` 삭제, Kind Text 끔, 아이콘 72px 가운데. `ShopPresenter`의 보유 문구 호출 삭제 — `UI.Shop.Owned` 키는 지금 안 쓴다.
- 아트: `make_slots.py slot_empty()` 점선 제거 · `sprite_post.py`(key·unfake·slice·breathe) + `sprite-gen~/` 원본 모듈.

## 결정 근거
- sprite-gen `key`는 회색 배경에서 나무꾼 얼굴(피부)까지 지웠다 → 무채색 배경은 거절하고 `cut_ai_background.py`로 보낸다. 초록 배경에선 깨끗했다.
- `unfake`는 ×7.3 가짜 픽셀을 정확히 되돌렸지만, 실제 AI 원본은 격자가 흐려 못 찾았다 → 못 찾으면 줄이지 않고 `ai_to_pixel.py --oe`로 안내.

## 사고
- 사용자가 플레이 중일 때 `.cs`를 고친 채 Refresh와 프리팹 수정을 돌렸다(메모리 규칙 위반). 플레이 종료 후 다시 Refresh — 컴파일 오류 없음.

## 남은 것
- 숲·어두운 숲 배경 위쪽 이어 그리기(사용자 a안 결정) — 맨 윗줄 늘림이 줄무늬로 보인다.
- Taskbar Heroes 보스 느낌 캐릭터 3종(대기·달리기·공격, 공격은 이펙트 + 넓은 범위) — 미리보기 단계.
