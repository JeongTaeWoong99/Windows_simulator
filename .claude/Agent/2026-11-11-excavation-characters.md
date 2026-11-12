---
date: 2026-11-11
title: 발굴 고급·영웅·전설 캐릭터 3종 · 드워프 종족 (#115)
tags: [data, character, gacha, localization]
---

# 발굴 고급·영웅·전설 캐릭터 3종 · 드워프 종족 (#115)

## 목적 / 배경
- 발굴 산업의 고급·영웅·전설 자리에 캐릭터가 없어 그림을 넣을 TID가 필요했다(태웅 요청 #115 · T-174).

## 변경 내용
- `Character.xlsx` — 1032 탐험가 · 1033 드워프 발굴꾼 · 1034 보물 사냥꾼(이슈 제안값 그대로).
- `Enum.xlsx` `CharacterRace` 5 `Dwarf` (뒤에만 덧붙임).
- `Gacha.xlsx` `GachaCharacterTable` — 뽑기권 A~D(32~35) 중 등급이 닿는 풀에 같은 등급 가중치로. TID = 풀 × 1000 + 번호 끝자리(1031 방식).
- `Loc_Character.xlsx` 영어 이름 · `Loc_UI.xlsx` `UI.Character.RaceDwarf`·`RaceDwarfShort`.
- 캐릭터 기획 1장·4.1·4.2 · T-174.

## 주요 결정 / 근거
- 드워프 종족 신설 · 뽑기권 풀 포함은 사용자 결정(2026-11-11).
- 클라 `RaceLabel.cs`는 클라 영역이라 손대지 않았다 — 문구 키만 준비하고 #115로 요청.

## 후속 / 미완
- 클라 `RaceLabel` Dwarf 분기 · 1032~1034 그림(태웅).
- PR #120(T-195)과 `Gacha.xlsx`가 겹친다 — 바이너리라 나중에 머지되는 쪽이 엑셀을 다시 반영하고 파이프라인을 돌린다.
