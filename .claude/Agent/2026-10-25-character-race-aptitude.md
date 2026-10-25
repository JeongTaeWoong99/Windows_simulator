---
date: 2026-10-25
title: 캐릭터 종족(Race) 추가 · 적성을 그림 기준으로 재배치
tags: [data, excel, character, excelgenerator]
---

# 캐릭터 종족(Race) 추가 · 적성을 그림 기준으로 재배치

## 목적 / 배경
- 이슈 #79 · T-054 — 태웅 님 배치안(30종)을 그대로 반영. 진우 결정: 종족은 표시용, 종족별 효과 없음, 적성은 사람이 캐릭터마다 조절

## 변경 내용
- `Enum.xlsx` — `CharacterRace` 시트(Human·Monster·Undead·Golem)
- `Character.xlsx` — `Race` 컬럼(E열, `eCharacterRace`) · 적성 9줄 · 이름 3줄(1008·1010·1014) · 메모
- `Localization/Loc_Character.xlsx` — 바뀐 3개 이름 EN 채우고 `🔄 EN` 상태 지움
- `Server/ExcelGenerator/ExcelGenerator.cs`·`Localization.cs` — 엑셀 파일 열거를 이름순 정렬(OrdinalIgnoreCase)
- 기획 `character/README.md` 4.2 종족 목록 · T-054 갱신

## 주요 결정 / 근거
- 파일 열거 정렬: macOS(APFS)는 `EnumerateFiles` 순서가 Windows(NTFS, 이름순)와 달라 `GameTable.cs`·`TableRegistry.cs`·`LocalizationTable` 순서가 통째로 바뀌었다 → 정렬로 고정

## 후속 / 주의
- macOS에는 pwsh가 없어 `generate-tables.ps1`의 미러 단계(.cs → `Assets/Scripts_Server/GameData`, .bytes → `StreamingAssets/Data`)를 손으로 돌렸다. dotnet은 `~/.dotnet/dotnet`
- macOS에서 생성하면 `Loc_UI`의 `\r\n` 문구가 `\n`으로 나온다(xlsx 원본은 `\r\n`) — Windows에서 다시 생성하면 되돌아갈 수 있다
- 남은 일: 클라 선택 화면 `Race Text` 연결(enum → 한글 표시명)
