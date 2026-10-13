---
date: 2026-10-13
title: 미감정 유물 · 감정 · 아티팩트 기획과 첫 데이터 (T-150)
tags: [design, data, mockup]
---

# 미감정 유물 · 감정 · 아티팩트 기획과 첫 데이터 (T-150)

## 목적 / 배경
- 유적 발굴(T-142)의 고유 콘텐츠 — 상자 자리에 미감정 유물이 나오고, 골드로 감정하면 상자 같은 보상 + 낮은 확률로 아티팩트(보유만으로 계정 영구 효과).

## 변경 내용
- 기획: `GameDesign/design/gathering/excavation/유물감정.md` (구상) · 목업 `GameDesign/design/ui/유물감정목업.html` (아이콘 7종 · Special 칸 애니메이션 · 감정 연출 · 보관함=도감).
- enum: `GlobalRarity.Special`(7) · `ItemType.Box`(8)·`Consumable`(9)로 상자·소모품 재분류(`Special`(7)은 비움) · `UserTraitEffect.AppraiseCostDiscount`(4)·`RelicChanceAdd`(5).
- 새 엑셀 `Artifact.xlsx` / `ArtifactTable` — 일반 12 · 고급 10 = 22행 (번호 001~022). Unity `.meta` 2개는 손으로 만들었다(새 GUID).

## 주요 결정 / 근거
- 유물은 희귀도당 1종(레벨 없음) — 레벨은 확률만 가른다. 상자를 **대신**하고 합계 확률은 상자의 절반쯤.
- Special 유물은 판정에서 안 나오고 **다른 유물 감정에서만** — 판정 주기 차이로 Lv1이 81배 유리해지던 문제가 사라진다. 확률은 감정 비용 비례(추천, 미확정).
- 아티팩트 번호 = **도감에 실린 순서**. 등급 대역(5자리 → 1만 칸 → 100번대)을 거쳐 폐기 — 등급을 고치면 DB 번호를 옮겨야 하고, 등급은 색으로 이미 보인다.
- 중복 보유 가능 · 효과는 1개분 · 인벤토리 아닌 전용 보관함(= 도감 한 화면) · 효과 합계 표시 없음 · 세트 효과 없음 · 상한 없음(감정 비용 할인만 50%).

## 후속 / 주의
- 서버 미구현: 감정(골드 차감) · `t_user_artifact` · 효과 합산 · 가챠 보상 종류 `Artifact` · 유물 아이템 7종 · `CommonRewardOverrideTable` 발굴 행. `ArtifactTable`은 아직 아무도 읽지 않는다.
- 남은 결정: 레벨별 확률 값 · Special 확률 · 희귀 이상 아티팩트.
- `BoxSheetTest` 2건 실패는 T-149(기존).
