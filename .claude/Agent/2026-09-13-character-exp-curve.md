---
date: 2026-09-13
title: 캐릭터 경험치 곡선 — 판정 1회당 ExpPerJudge · CharacterLevelTable 만렙 100
tags: [data, design, character, excel]
---

# 캐릭터 경험치 곡선 — 판정 1회당 `ExpPerJudge` · `CharacterLevelTable` 만렙 100

## 목적 / 배경

- T-003(캐릭터 성장)이 "획득 경로·곡선·레벨 효과" 셋이 비어 보류였다.
  사용자가 이 중 둘을 결정했다 — **획득은 판정 1회마다, 일정 횟수를 채우면 레벨업.**
  레벨이 무엇을 올리는지는 여전히 미정 → `tasks/T-003`.
- 결정을 엑셀 두 곳에 넣고 기획 문서에 전파했다. **서버는 손대지 않았다.**

## 변경 내용

- `GameDesign/Excel/Industry.xlsx` — `IndustryLevelTable.*` 5시트에 `ExpPerJudge` 열 삽입(`RequiredScore` 바로 뒤). Lv1~5 = 1·3·9·27·81.
- `GameDesign/Excel/Character.xlsx` — `CharacterLevelTable` 시트 신설. `CharacterLevelTID`(=레벨) · `RequiredExp` · `Description`, 100행.
- 생성물(`generate-tables.ps1`) — `CharacterLevelTable.cs/.bytes/.json` 신규, `IndustryLevelTable` 갱신, Unity 미러 포함.
- 기획 문서 — 캐릭터 5.2 신설(5.2 미정 → 5.3) · 게임기획코어 5장 · 산업레벨 2.4/6.1 · 작업슬롯 5장 · 기획평가 R20 · 문서관계도 2·4장 · progression 헤더 블록.

## 주요 결정 / 근거

- **`ExpPerJudge`를 `RequiredScore`와 같은 ×3 등비로 뒀다.** 점수 30,000당 경험치 1이 되어
  시간당 경험치가 산업 레벨 선택과 무관해진다 — "경험치 때문에 Lv1만 돌린다"는 경로를 막는다.
  경험치 1 = Lv1 판정 1회라 곡선 값이 "Lv1 환산 판정 횟수"로 읽힌다.
- **`RequiredExp`는 "직전 레벨에서 이 레벨까지"다(Lv1 = 0).** 마지막 행이 곧 만렙이라
  0 같은 센티널 값이 필요 없다. `t_character.exp`는 현재 레벨에서 쌓은 양(누적 아님)으로 정했다.
- **만렙은 처음 30으로 잡았다가 사용자 지시로 100, 증가율은 ×1.2로 확정.** 100레벨에 ×1.2면 Lv100이 5.75억이라
  ×1.05를 제안했으나 사용자가 ×1.2를 택했다. 누적(34.5억)은 int를 넘지만 저장은 레벨별이라 문제없다. 전부 테스트값이다.
- **먹이기는 넣지 않았다.** 사용자가 "우선 1가지만"이라 했으므로 캐릭터 5.3 #2에 미정으로 남겼다.

## 함정

- **`check-doc-graph.ps1 -Fix`를 쓰지 말 것.** 17개 문서의 `바뀌면 갱신` 블록을 **영문 폴더명 라벨**
  (`[gathering]`·`[item]`)로 전부 다시 쓴다. 기존 블록은 한글 라벨이라 diff가 폭발한다.
  이번엔 `git checkout -- GameDesign/design`으로 되돌리고 progression 블록 한 줄만 손으로 고쳤다.
  검사기(`-Changed`)는 링크 대상만 비교하므로 한글 라벨로 두어도 "그래프 정합성 OK"가 난다.
- `Industry.xlsx`가 Excel에 열려 있으면 openpyxl 저장이 `PermissionError`로 죽는다. `~$Industry.xlsx`가 있으면 잠긴 것이다.
- openpyxl `insert_cols`는 셀만 밀고 **열 너비는 안 민다** — 스크립트에서 `column_dimensions`를 따로 옮겼다.
- `generate-tables.ps1` 후 손 안 댄 `.cs`들이 `M`으로 뜨는 것은 LF/CRLF 정규화뿐이다(`git diff` 내용 없음).

## 후속 작업 / 주의사항

- **Unity `.meta` 없음** — `Assets/Scripts_Server/GameData/Tables/CharacterLevelTable.cs`,
  `Assets/StreamingAssets/Data/CharacterLevelTable.bytes`. 에디터를 한 번 열어 생성한 뒤 함께 커밋한다.
- 서버 구현은 `tasks/T-003` 할 일에 적었다 — `SettleWorkStation`에서 `JudgeCount × ExpPerJudge` 가산,
  `CharacterLevelTable`로 레벨업, `t_character` 갱신. `Character.Level/Exp`가 `private set`이라 변경 메서드가 필요하다.
- 엑셀 입력 스크립트는 저장소에 남기지 않았다(선례와 동일). 값은 리터럴이라 문서 5.2 표로 재현 가능하다.
