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

- 엑셀 입력 스크립트는 저장소에 남기지 않았다(선례와 동일). 값은 리터럴이라 문서 5.2 표로 재현 가능하다.

## 업데이트 (2026-09-13) — 서버 구현

데이터·문서는 다른 세션이 `5b8939e`로 커밋했고, 이어서 **경험치 누적·레벨업·저장·푸시**를 서버에 붙였다.
구성은 `tasks/T-003`의 "2026-09-13 구현" 표를 본다. 여기엔 결정과 지뢰만 남긴다.

### 결정
- **`CharacterLevelCatalog`를 따로 뒀다** (`IndustryLevelCatalog`와 같은 모양). `GameTable.CharacterLevelTable`을
  `Character`가 직접 읽게 하면 테스트가 엑셀 값에 묶인다 — 카탈로그를 `User` 생성자로 주입해 테스트가 리터럴 곡선을 넣는다.
- **만렙 = 카탈로그의 최대 TID.** 센티널(RequiredExp 0) 없음. 행이 하나도 없으면 만렙 1 = 아무도 안 자란다(무한 레벨업보다 낫다).
- **`ExpPerJudge` 행이 없으면 0** — 판정 비용(`ResolveJudgeCost`)과 같은 규약. 정산 전체를 죽이지 않는다.
  덕분에 `Levels`가 비어 있는 기존 `UserWorkStationTest`들이 그대로 초록이다(저장·푸시가 안 생긴다).
- **변화가 없으면(만렙·0) 저장도 푸시도 안 한다.** `GrantWorkExp`가 전후를 비교한다.
- 정산 → 배치 변경 순서 덕에 **이전 구간 경험치는 이전 캐릭터에게 간다**(테스트로 잠갔다).

### 지뢰
- **실행 중인 `WSGameServer.exe`가 있으면 `dotnet test`가 DLL 복사에서 죽는다.** 죽이지 않으려면
  `-p:BaseOutputPath=<임시폴더>/`로 출력만 분리하면 된다 — 컴파일·테스트 전부 정상이고 미러(프로토콜)도 돈다.
- 빌드가 `Assets/Plugins/Analyzers/MikaSourceGen.dll`을 다시 써서 `M`으로 뜬다. 소스 변화가 없으면 `git checkout`으로 되돌린다.
- **클라 `PlayerDataModel`은 `CharacterSynced`를 아직 안 받는다.** 서버가 밀어도 화면 레벨은 로그인 스냅샷 값이다 → T-003 클라 항목.
