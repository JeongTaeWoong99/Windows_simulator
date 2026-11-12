---
date: 2026-11-11
title: 등급 바탕 한 벌 · 칸 여백 · 우편 아이콘 · 그루터기 정령
tags: [client, ui, art]
---

# 등급 바탕 한 벌 · 칸 여백 · 우편 아이콘 · 그루터기 정령

## 목적 / 배경
- 사용자 지적(스크린샷 4장): 우편 대표 아이콘이 글자로 보임 · 인벤토리 칸 이름이 그림과 겹침 · 캐릭터 칸 여백·글자 · 상점 뽑기권 뒤 바탕이 단색판 — "전체적으로 다 별로, 프레임 색 맞춰서".
- 만드라고라가 게임 결과 안 맞는다 — 다른 몬스터 하나.
- 전후 캡처: 바탕화면 `UI 전후 비교/` (비교1~7 · 전_ · 후_ · stump GIF)

## 변경 내용
- `UI/Shared/visual/RaritySkin.cs` 신설 — `Dress`(바탕 그림 + 첫 자식 `Rarity Frame`) · `Set` · `SetFrame`. `SlotView`의 바탕·장식 코드를 옮기고 `ItemIconView` · `WorkStationSlotView` · `CharacterStateRowView` · `EquipPickRowView` · `WidgetMiniSlotView`가 함께 쓴다(단색 칠하기 제거).
- 그림 저장소 `_source/ui/claude-ui/make_slots.py` — `rarity_bg`를 밝은 테두리 + 어두운 속으로(곱하면 등급색 테두리 · 어두운 등급색 속).
- `Assets/Prefabs/SlotView.prefab` 여백 — 이름(위 6~23 · 가운데 · 최대 15) · 아이콘(위 20 · 아래 18 · 좌우 8) · 적성 띠(아래 4~22 · 검은 50% 바탕 · 값 굵게 · 아이콘 30%) · 뱃지·경험치·배치 표시 자리 정리.
- `MailPresenter.BuildIcon` — 첨부가 여럿일 때 `ItemIconContent`를 새로 만들며 그림(`Icon`)을 빠뜨려 글자만 보였다 → 첫 첨부 그림을 넘긴다.
- 그림 저장소 `_source/characters/claude-stump/make_stump.py` 신설(대기 4 · 달리기 6 · 공격 5) · `claude-mandrake/` 삭제.
- 문서: `Shared 규칙.md`(RaritySkin 줄 · SlotView 여백) · `Art 규칙.md`(rarity_bg · claude-stump 줄 · 코드 몬스터는 팩 결) · `FxSprites`·`RarityPalette` 주석 · T-174.

## 주요 결정 / 근거
- 등급은 테두리가 말하고 속은 어둡게 — 밝은 판 × 진한 등급색(초록 #1EFF00 등)은 칸 전체가 형광 단색판이 돼 아이콘·글자를 묻었다. 색의 주인은 그대로 `RarityPalette`.
- 장식을 형제가 아니라 바탕의 **첫 자식**으로 — 루트 Image(줄·위젯)에도 그대로 붙고, 순서상 바탕 위 · 내용 아래다.
- `EfficiencyRowView`는 평소 색 ↔ 등급색을 오가는 얇은 줄(34px)이라 제외.
- 코드 몬스터는 팩(Mushroom·Slime·Golem·Rat) 결을 따른다 — 채도 낮은 흙빛 · 외곽선은 가장 어두운 바탕색 · 붉은 눈. 벌목 희귀 빈 자리(T-174 2순위 트렌트) 후보로 그렸다.

## 검증
- 플레이 + 로그인(계정 1234)으로 우편·자원·캐릭터·장비 탭·상점 캡처 전후 비교 · 콘솔 오류 0.

## 남은 일
- 사용자 실측 · 그루터기 정령을 쓸지와 TID(`Character.xlsx` 행 — 진우).
