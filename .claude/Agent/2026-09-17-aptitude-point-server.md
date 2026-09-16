---
date: 2026-09-17
title: 적성 포인트 — 엑셀 상한·DB·패킷·서버 구현 (T-003)
tags: [server, data, protocol, db, test]
---

# 적성 포인트 — 엑셀 상한·DB·패킷·서버 구현 (T-003)

## 목적 / 배경
- 캐릭터 레벨 효과가 적성 포인트로 확정됐다(캐릭터 기획 5.4, 커밋 `75796c9`). 그 구현.
- 규칙·수치는 기획서가 원본이다 → `GameDesign/design/character/README.md` 5.4. 여기엔 구현 결정만 남긴다.

## 변경 내용
- `GameDesign/Excel/Character.xlsx` — `CharacterTable`에 `FarmingCap`…`HuntingCap`(Description 앞, `Ref WorkSpeedTable.WorkSpeedTID`), `CharacterLevelTable`에 `AptitudePoint`. openpyxl로 편집했다(스타일은 왼쪽 열 복사).
- `Server/Shared/game.sqlite3` — `t_character`에 `*_bonus` 5열 `ALTER TABLE ADD COLUMN`. 주석은 `/* */`로 넣었다 — `--`는 ALTER 문의 닫는 괄호를 삼킨다.
- 프로토콜 — `AptitudeInfo.Cap` · `CharacterInfo.AptitudePoints` · `C_AptitudeUpRequest`(27) · `S_AptitudeUpResponse`(28) · `EResultCode` 600~.
- 서버 — `AptitudeBonus`(record struct) · `Character` 실효 적성/상한/찍기 · `CharacterLevelCatalog.PointsEarnedBy` · `User.RaiseAptitude` · `SaveCharacterAptitudeRepository` · `CharacterTableValidator`.
- 테스트 — `CharacterAptitudeTest`(순수) · `UserAptitudeTest`(조립) · `CharacterAptitudeRepositoryTest`(:memory: 왕복) · `CharacterTableValidatorTest`. `SqliteFixture.CreatePlayerTables`가 운영 DDL 사본을 갖는다.

## 주요 결정 / 근거
- **남은 포인트를 저장하지 않는다.** `curve.PointsEarnedBy(level) − bonus.Total`로 늘 계산한다. 따로 저장하면 어긋났을 때 진실을 못 정한다. 기존 Lv10 캐릭터가 자동으로 포인트를 갖는 것도 이 덕이다.
- **기본 ≤ 상한 검사는 파이프라인이 아니라 서버 기동 시.** `ExcelGenerator`는 컬럼 하나의 `Min/Max/Ref`만 보고, 두 컬럼 관계를 넣으려면 새 마커가 필요하다. 기동 검사 + 실제 엑셀 데이터 테스트로 같은 효과를 냈다.
- 찍은 뒤 속도 반영은 기존 `RefreshWorkStationSpeed(now)`를 그대로 부른다 — 정산 → `ApplyWorkSpeed` 순서 불변식이 거기 있어서 소급이 없다. 바뀐 슬롯만 싱크가 나간다.
- 상한 초안은 등급표 윗값을 전 산업에 같은 값으로 넣었다(기본값이 그보다 크면 기본값). "주력 외 산업을 낮추는" 조정은 기획자 몫으로 5.3 미정 #4에 남겼다.

## 후속 작업 / 주의사항
- **클라 찍기 화면은 안 만들었다**(상대 담당). `ServerPacketHandler.AptitudeUpResponded` 이벤트와 `CharacterInfo.AptitudePoints`·`AptitudeInfo.Cap`까지 준비돼 있다. 화면 위치는 게임UI 6장 #14가 미정.
- `SqliteFixture`의 DDL은 손으로 맞춘 사본이다. 스키마 `.sql`이 생기면 그 파일을 읽게 바꾼다.
- `game.sqlite3`는 다른 세션의 런타임 변경(유저 행)과 함께 커밋됐다 — 바이너리라 갈라 담을 수 없었다.
