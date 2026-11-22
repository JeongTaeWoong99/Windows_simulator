---
date: 2026-11-22
title: 채취 6단계(Lv1 · 20 · 40 · 60 · 80 · 100) · 테마 상자 6종 — 데이터 · 서버 (T-208)
tags: [data, server, db, migration, docs]
---

# 채취 6단계 · 테마 상자 — 데이터 · 서버

## 목적 / 배경
- T-208 — 상자 15종(재질 3 × Lv1~5)을 채취 단계마다 테마 상자 1종으로 줄이고, 채취 레벨을 20 단위 6단계로 바꾼다. 이름은 전부 `GameDesign/design/gathering/레벨테마.md` 그대로.
- 워크트리 `worktree-T-208-level-step-rework` · PR #151. 클라(`Scripts_Client`)는 건드리지 않았다 → #150.

## 변경 내용
- **엑셀** — `Industry.xlsx` `RequiredTraitLevel`(1~6) 추가 · TID x01~x06 · Lv100 행. `User.xlsx` 개척 101~105 MaxLevel 6. `Item.xlsx` 자원 이름·가격(단계 ×3) · Lv100 30종(x0051~x0056) · 옛 상자 100019~100033 삭제 · 테마 상자 100081~100086. `Drop*.xlsx` 시트 `.Lv1/.Lv20/…/.Lv100` · DropTID = 산업×100000 + 레벨×100 + 순번. `DropCommon.xlsx` 공통 6행 · 발굴 덮어쓰기 레벨 재배치 + Lv100(Lv80 복사). `Gacha.xlsx` 은·황금 풀 10건 삭제 · 9/12/15/18/21 = 테마 상자. `Mail.xlsx` 템플릿 1 = 100081. `Equip.xlsx` Lv10~100 이름 660행. `Loc_*` EN 채움.
- **서버** — `User.IsIndustryLevelUnlocked`: 개척 특성 Lv ≥ 행의 `RequiredTraitLevel`(표에 없는 레벨은 닫힘). 주석(`IndustryLevelCatalog` · `UserTraitCatalog`) · 더미 클라 프롬프트 · `docs/데이터-카탈로그.md`.
- **마이그레이션 `0017-level-step-reset.sql`** — 슬롯 `industry_level` = 1 · 개척 특성 행 삭제 · 옛 상자를 인벤토리 · 잠금 · 우편 첨부(json_each) · 경매 Listed(반환 · 에스크로 0, 0002 방식)에서 뺀다. 로컬 `game.sqlite3` user_version 17.
- **테스트** — `LevelStepResetMigrationTest`(5) 신설 · 단계/테마 상자 기준으로 UserWorkStation · UserTrait · BoxSheet · DropSheet · IndustryLevelCatalog · RelicSheet 등 수정. 아티팩트 25 InlineData 삭제.
- **기획 문서** — 산업레벨(1~6장) · 장비레벨(1.1 이름 · 3장) · 아이템 4.1 · 레벨테마 상태 · 특성(개척 1→6 · 합계 135점) · 건물 · 진행 · 유물감정 · 캐릭터 · 게임기획코어.

## 주요 결정 / 근거
- `IndustryLevelTID`는 산업×100 + **단계 순번** — 레벨 100을 넣으면 다음 산업 대역과 겹친다. `DropTID`는 ×100000이라 실제 레벨을 넣어도 안 겹친다.
- 개척 특성 포인트는 저장하지 않으므로 행 삭제 = 기본 Lv1 + 포인트 자동 반환.

## 검증
- WSGameServer.Tests 1338 · AuctionServer.Tests 140 · ExcelGenerator.Tests 24 통과 · 솔루션 빌드 오류 0 · `check-doc-graph -Changed` 통과.

## 후속 / 판단 필요
- 은빛 수확패(아티팩트 25) 획득처 없음 · 테마 상자 열기 보상 · 왕실 상자 풀 → T-209.
- 발굴 Lv100 덮어쓰기는 Lv80 복사 · 속도 특성 201~205가 개척 Lv6을 요구하게 됨.
- 클라: `WorkStationSelectPresenter.cs:745 · 812`(`i + 1`) · `PlayerDataModel.cs:610`(특성 Lv ≥ 레벨 비교) · `GameDataLoader.cs:615`(GetMaxIndustryLevel → 100이라 버튼 100개 가정) → #150.
- 원격 DB 적용 전 경매장 쪽 옛 상자 매물 확인 · 치트로 새 단계 실서버 확인은 안 했다.
