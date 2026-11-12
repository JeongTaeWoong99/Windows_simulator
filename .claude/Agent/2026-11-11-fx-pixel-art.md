---
date: 2026-11-11
title: 연출 이펙트 fx/를 전부 도트로 다시 그린다 (T-178)
tags: [client, art, fx]
---

# 연출 이펙트 fx/를 전부 도트로 다시 그린다 (T-178)

## 목적 / 배경
- UI 틀·타격·동전·칸 등급은 도트인데 연출 이펙트 22장만 부드러운 흰 도형(선형 필터)이라 화풍이 섞였다. 사용자 결정: 전부 도트.
- 일감·완료 조건 → `tasks/art/T-178-이펙트정식그림.md`

## 변경 내용
- `Assets/Art/_source/fx/fx-placeholder/` → `claude-fx/`로 옮기고 `make_fx.py`를 도트 생성기로 다시 씀(그림 저장소 `Assets/Art` 쪽 git)
- `ArtImportPostprocessor.ApplyFx` — 이름별 예외 없이 `fx/` 전부 점 필터
- 문서: `Art 규칙.md` 이펙트 절 · `FxSprites.cs` 주석 · `art-import 규칙.md` · `pixel-fx`·`asset-compression` 스킬 · `CLAUDE.md` 발동 조건 · `tidy_source.py` KEEP_WHOLE

## 주요 결정 / 근거
- 캔버스는 화면 크기 기준으로 작게(별 15 · 번짐 48 · 광선 64 · 모양 24~32). 크게 그리면 도트가 UI(2px)보다 작아져 결이 어긋난다.
- 색은 코드가 곱하므로 흰색 규칙을 "흰색·회색 명암 + 외곽선 OUT_C"로 넓혔다 — 곱하면 그 색의 명암이 된다(`rarity_bg`와 같은 방식).
- `bagfull_cross`는 17px — 자루(32)에 0.55배로 겹쳐 도트 크기가 같아진다.

## 후속 작업 / 주의사항
- 지뢰: `unlock_shackle` 다리 칸(9~11열, 16줄까지)은 `SlotUnlockFx.ShacklePivot`(0.32, 0.475)과 맞춘 것 — 옮기면 고리가 엉뚱한 축으로 돈다.
- `reveal_frame`은 32px·띠 4칸 유지 — Border 5가 `.meta`에 있다.
- 게임 안 확인 전. 화면을 가득 채우는 섬광(`RelicAppraiseFx` FlashSize 900)은 도트가 굵게 보일 수 있다 — 보고 나서 캔버스를 키울지 정한다.
- `outpost/` 임시 그림(평면 벡터 · 선형 필터)은 화풍이 어긋나지만 T-191(거점 개편 T-183 뒤)에서 다룬다.
