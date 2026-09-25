---
date: 2026-09-23
title: 장비 인챈트 — 데이터 · DB · 카탈로그 · 패킷 · 서버 로직 · 속도/경험치 반영 · 실서버 확인 · 문서 전파 (T-092)
tags: [server, data, design, docs, test]
---

# 장비 인챈트 (T-092 · Task 1~8)

## 목적 / 배경

- 장비 개체에 추가옵션(인챈트)을 붙인다. 등급(Rare·Epic·Legendary)이 옵션 풀의 질을, 줄 수(2~3)가 개수를 정하고 둘은 독립 축이다.
- 설계 → `docs/superpowers/specs/2026-09-23-equip-enchant-design.md` · 계획 → `docs/superpowers/plans/2026-09-23-equip-enchant.md` · 일감 → `tasks/archive/T-092-인챈트.md`
- 데이터 단계(Task 1)의 상세·함정은 [`2026-09-23-enchant-data-tables.md`](2026-09-23-enchant-data-tables.md)에 따로 있다.

## 변경 내용

- 커밋 `3f4dce1`…`938cf69`(서버) · `04ffa41`(더미 클라) · `f4e5014`(문서) · `0213ce4`(일감). 목록은 `tasks/T-092` "관련 커밋".
- 서버 스위트 457 통과(Task 7 기준). 규칙 서술은 `character/README.md` 1.4가 원본이다.

## 주요 결정 / 근거

- **착용 중인 장비는 인챈트를 거절한다(`EnchantEquipped`).** 이 한 줄 덕에 `TryEnchant`가 정산·`RefreshWorkStationSpeed`를 부르지 않는다 —
  가동 중인 슬롯의 속도·경험치가 인챈트로 바뀔 수 없어 소급 버그가 원천적으로 없다. **착용 중 인챈트를 허용하는 순간 정산 → 변경 순서를 이 경로에도 걸어야 한다.**
- **DB는 `t_user_equip`에 컬럼 4개**(`enchant_grade`·`enchant_1~3`). 별도 테이블로 쪼개지 않았다 — 줄 상한 3이 확정이고 항상 통째로 읽고 쓴다.
  **줄마다 속성이 붙는 순간(줄 고정 등)** 이 `t_user_equip_enchant`로 갈아탈 신호다. 줄 수는 `enchant_3 != 0`으로 유도한다.
- **`Equip.EnchantOptions`는 TID가 아니라 Row를 든다**(스펙 7장의 `IReadOnlyList<int>`를 뒤집음). `Equip`이 카탈로그를 몰라도 효과를 합산한다. 패킷·DB용 TID는 `EnchantOptionTids`.
- **속도 가산의 산업 판단은 `Equip.SpeedAddPermilleFor`가 한다.** 장비 기본값과 인챈트 줄의 대상 산업이 다를 수 있어 `GetEquipSpeedAdd`가 장비 단위 `AppliesTo`로 거르면 틀린다.
- **확률 출처가 동작마다 다르다** — 부여·확장은 아이템 `SuccessPermille`, 큐브는 현재 등급의 `EnchantGradeTable.UpPermille`. 큐브 행의 `SuccessPermille`(1000)은 읽지 않는다.
- 리뷰에서 잡은 것: 알 수 없는 `EnchantAction`은 **아이템 소모 전에** `ItemNotUsable`로 거절(통과시키면 아이템만 사라짐) · Legendary 큐브는 `Success=false`(오른 등급이 없다).
- 치트를 새로 만들지 않았다 — 인챈트 아이템은 평범한 아이템이라 `GiveItem`이 준다.

## 후속 작업 / 주의사항

- **클라 UI 미착수** — T-074(장비 장착 UI)와 함께. 거절 응답은 `BeforeGrade/AfterGrade = 0`·`Options = []`로 온다(실서버 확인). 클라는 `Result != Ok`면 이 필드를 읽지 말 것.
- 옵션 값·`Weight`·아이템 확률(50%·30%)은 **테스트값**. 등급 상승 5%·0.5%만 확정.
- **실서버 확인 지뢰(Task 8)**:
  - Unity 에디터 없이 `Server/MikaDummyClient`를 stdin 파이프로 몰았다(메뉴 12 = `EquipEnchant`). 출력이 **cp949**라 `iconv -f cp949 -t utf-8`로 읽는다.
  - 세션을 끊고 다시 로그인하는 식으로 여러 번 나눠 돌리면 앞 세션 응답에서 ID(장비·캐릭터)를 읽어 다음 입력을 만들 수 있다.
  - `CheatAdminLevel = 0`이라 개발 중엔 `admin_level`을 올릴 필요가 없다(`Server/docs/치트.md`).
  - **`git checkout -- Server/Shared/game.sqlite3`가 "unable to unlink"로 실패할 수 있다** — 사용자의 HeidiSQL이 파일을 열고 있다. 프로세스를 죽이지 말고 `git show HEAD:Server/Shared/game.sqlite3 > Server/Shared/game.sqlite3`로 제자리 덮어쓴다.
- **`check-doc-graph.ps1 -Fix`를 쓰지 말 것.** Windows PowerShell 5.1에서 블록 라벨이 한글 문서명이 아니라 영문 폴더명(`character`·`item` …)으로 바뀌고 무관한 문서 11개까지 다시 쓴다. 블록은 손으로 고친다.
- `check-doc-graph -Changed`의 경고 `[trade] 블록에 남음: trait`는 이 작업 이전부터 있던 것이다(특성 문서가 거래를 링크하지 않는다).

## 업데이트 (2026-09-24) — 최종 리뷰 수정

- `EnchantCatalog.Load` 기동 검증 추가: 알 수 없는 `Action` · `Value ≤ 0` 옵션이면 예외. `HasPool(grade)` 추가
- `User.LoadEquips`: 풀 없는 등급(`Common`·`Mythic` 등)으로 저장된 인챈트는 경고 후 통째로 버린다 — 큐브가 소모 뒤 재롤에서 예외를 내던 구멍
- `User.TryEnchant`: 인챈트 아이템이 아닌 TID → `ItemNotUsable`(302). `EnchantItemNotOwned`(610)는 보유 0 전용
- `S_EquipEnchantResponse` 주석: 거절이면 Result·EquipId만 유효 · Ok일 때 Options는 동작 후 줄
- 규칙: **`EnchantOptionTID`는 삭제·재사용 금지, 퇴역은 `Weight = 0`** — 자리 순서 저장 때문에 가운데 줄 삭제 시 3번째 줄이 영구히 사라진다(데이터-카탈로그 9절 · 스펙 5장)
- 후속: `UserMailTest.접속_중인_유저에게_보내면_그_유저의_우편함으로_도착한다`가 간헐 실패한다(4bd74be에서도 재현 — `UserManager` 싱글턴을 병렬 테스트가 공유하는 듯). 이번 작업과 무관
