---
date: 2026-10-12
title: 낚시 산업 → 유적 발굴 교체 (T-142)
tags: [server, data, design, docs]
---

# 낚시 산업 → 유적 발굴 교체 (T-142)

## 목적 / 배경
- 모션이 대기·공격·달리기뿐이라 낚시(던지기·기다리기·당기기)를 그릴 수 없다 → 유적 발굴로 교체 (#66).
- 사용자 지시: 발굴도 **특별한 것 없이** 다른 4산업과 같게 (#42 공통값 · T-011 같은 루프).

## 변경 내용
- 엑셀 — `Enum`(ItemType·IndustryType 2 = `Excavation`) · `Item`(자원 30종 이름·장소 메모) · `DropFishing.xlsx` → `DropExcavation.xlsx`(시트 `ExcavationBasicTable.Lv1~5`) · `Character`(적성 컬럼 `Excavation`) · `Equip`(13종 이름) · `EquipEnchant` · `Industry`(시트 `.발굴`, 장소 5곳) · `User`(특성 102·202·302) · `Gacha`(메모 속 옛 장비 이름).
- 생성물 재생성 · Unity `.meta`는 옛 `FishingBasicTable` GUID를 새 이름으로 이어 받음.
- 서버 — `PacketEnum` · `DropTableCatalog` · `Character` 적성 · 더미 클라 · 테스트 전부 `Fishing` → `Excavation`.
- `Server/Shared/migrations/0002-excavation-replaces-fishing.sql` — 낚시 자원·장비·특성 삭제, 낚시 슬롯 미지정, Listed 매물 Returned + 에스크로 0, `fishing_bonus` → `excavation_bonus`. 로컬 DB 판 2.
- 클라 — **컴파일이 깨지는 참조만** + `IndustryLabel` 글자. 나머지(프리팹·씬 글자·클라 문서·아트)는 T-148(태웅).
- 기획 문서 · 문서 사이트 라벨 · 스킬 예시 (`gathering/fishing/` → `gathering/excavation/`).

## 주요 결정 / 근거
- enum 2·TID를 **이어 쓴다** — 배열·순서·`ResourceSlotSource` 1:1 가정을 안 깨려고. "enum 뜻 불변"·"TID 재사용 금지"의 예외이고, 조건은 옛 데이터 삭제(0002). 기획코어 4장에 예외로 적었다.
- 저장 데이터는 변환하지 않고 지운다 — 출시 전, 로컬 DB에 자원 4행 · 매물 1건뿐.

## 후속 / 주의
- 경매장 DB(`auction.sqlite3`)에 걸려 있던 낚시 매물은 남는다 — 구매는 메인 정산 0행으로 실패, 만료로 사라짐.
- `BoxSheetTest` 2건 실패는 main에서도 난다(T-112 상자 개편 `a032487` — 상자 16종). 이 작업과 무관.
- Unity 컴파일은 돌려 보지 않았다 → T-148 첫 항목.
