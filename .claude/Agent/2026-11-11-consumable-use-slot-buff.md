---
date: 2026-11-11
title: 소모품 사용 효과 · 지도·나침반 3단계 · 슬롯 버프 (T-195)
tags: [server, data, packet, db, consumable]
---

# 소모품 사용 효과 · 지도·나침반 3단계 · 슬롯 버프 (T-195)

## 목적 / 배경
- `C_ItemUseRequest`가 상자만 열어 비약·영약·묘약·지도·나침반 12종이 `ItemNotUsable`로 거절됐다(#107·#110).
- #111에서 지도·나침반 3단계 효과가 확정됐다(태웅). 미커밋 T-192 소모품 파일과 내용이 겹쳐 T-195 하나로 합쳤다.

## 변경 내용
- **데이터 복구** — 11-03에 넣은 지도·나침반 4종(`100034~100037`) 행과 4.4 「사용 규칙」이 11-09 main 트리 머지 실패로 사라져 있었다. dangling WIP 커밋 `786783ec`에서 `Item.xlsx`·`Loc_Item.xlsx`·`item/README.md`를 꺼냈다.
- `Item.xlsx` 새 시트 `ItemUseTable`(`ItemTID`·`UseEffect`·`EffectValue`·`EffectCount`·`SlotCount`) 12행 · `Enum.xlsx` `ItemUseEffect` 7종.
- `Gacha.xlsx` — 상자 전용 몫 210을 같은 단계 지도·나침반(·열쇠)이 나눠 갖는다(나무·은 105씩, 황금 70씩). 감정 풀 25·26도 같은 방식(1050씩).
- 패킷 — `C_ItemUseRequest.TargetCharacterId`·`TargetSlotIndexes` · `S_ItemUseResponse.UsedCount` · `WorkStationSlotInfo.BuffAddPermille`·`Buffs` · `SlotBuffInfo` · `EResultCode` 1600~1603.
- `Server/WSGameServer/User/User.Consumable.cs` — `TryUseConsumable`이 효과 종류로 나눈다. 표에 없으면 false → 핸들러가 `OpenBox`로.
- `WorkStationSlot` — 아이템별 버프(`AddBuff`·`ConsumeBuffs`). `User.SettleWorkStation`이 판정 수만큼 깎고 앞쪽 판정부터 산출·상자 확률 가산, 속도 버프는 `WorkSpeedBreakdown.BuffAddPermille`.
- DB `t_user_slot_buff`(마이그레이션 `0013-slot-buff.sql`) · `SaveSlotBuffRepository` · 로그인 적재(`EnsureUnlockedSlots` 뒤).
- `ItemUseCatalog`(효과 없는 행·슬롯 수 0이면 기동 실패) · `User` 생성자 인자 · `TestUserBuilder.ItemUses`.
- 테스트 `UserConsumableTest` 25개.

## 주요 결정 / 근거
- **효과는 별도 시트** — `ItemTable`에 붙이면 병합 테이블이라 자원 150행·유물 시트까지 기본값 0이 필요했다(사용자 선택).
- **버프 키는 아이템 TID** — 낡은(+50%)·은빛(+100%) 나침반처럼 값이 다른 같은 종류를 합산하면 뜻이 없다. 같은 아이템만 합산, 다른 아이템은 동시에.
- **버프는 정산 구간의 앞쪽 판정부터** 붙는다. 속도 버프가 판정 도중 끝나도 그 구간은 버프 속도로 센다(정산이 짧게 자주 돌아 오차 ≤ 판정 1회).
- **낡은·은빛 나침반은 발굴(유물) 슬롯에 붙지 않는다** — 발굴은 상자 대신 유물이 나온다. 그래도 판정마다 횟수는 준다(규칙 "판정 1회마다 1 차감").
- 비약은 `GrantCharacterExp(accountExp: false)` — 계정은 작업으로만 자란다(4.4).
- 묘약은 치트의 `SetTraitLevels`(전부 기본 레벨로)를 재사용 — 로그 문구만 인자로 받는다.

## 후속 / 미완
- 클라 사용 화면·버프 표시 — T-194(태웅) · #111로 패킷 전달.
- 영약 자동 사용 칸 — #113 판단 대기.
- `MikaDummyClient` 빌드 오류(`S_AccountLevelResponse.TraitPoint`)는 T-183 이후 기존 문제 — 이번 작업과 무관.
