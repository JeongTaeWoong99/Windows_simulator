---
date: 2026-11-11
title: 영약 자동 사용 칸 (이슈 #113 · T-195)
tags: [server, packet, db, consumable]
---

# 영약 자동 사용 칸 (이슈 #113 · T-195)

## 목적 / 배경
- 영약 1개는 20회(Lv1 약 10분)라 효과를 이으려면 자주 게임을 열어야 했다 — P1·P2와 부딪힌다(태웅 제안 #113).
- 진우 결정(2026-11-11): 규칙 그대로 넣는다 · 칸 한도는 `MaxStack` · 꺼낼 때 자리가 없으면 거절 · 처음부터 열림 · T-195(PR #120)에 함께 · 패킷은 서버가 정한다.

## 변경 내용
- 패킷 — `C_SlotAutoUseRequest`(`SlotIndex`·`ItemTID`·`Count`·`Take`) · `S_SlotAutoUseResponse`(`Result`·`SlotIndex`·`ItemTID`·`ItemChangeInfos`) · `WorkStationSlotInfo.AutoUse`(`SlotAutoUseInfo`) · `EResultCode` `NotAutoUsable`(1604)·`AutoUseSlotFull`(1605).
- `WorkStationSlot` — `AutoUse` · `PutAutoUse`·`TryTakeAutoUse`·`RefillAutoUse` · `ConsumeBuffs`가 버프가 다 되면 그 판정 자리에서 칸의 것을 꺼내 잇는다.
- `User.AutoUse.cs` — 넣기(인벤토리→창고 순 차감)·꺼내기(사본으로 자리 먼저 확인)·로그인 적재. 정산은 칸 변화를 모아 `SaveSlotAutoUseRepository`로, 배치는 `RefillAutoUseAndSave`.
- DB `t_user_slot_auto_use`(마이그레이션 `0014-slot-auto-use.sql`) — 칸 · 버프 · 인벤토리를 한 트랜잭션으로. `SaveSlotBuffRepository.WriteAsync`·`SaveItemChangesRepository.WriteAsync`를 같이 쓴다.
- `ItemUseCatalog.IsAutoUsable` — 신속(`SlotWorkSpeed`)·풍요(`SlotYield`)만.
- 테스트 `UserSlotAutoUseTest` 20개. 전체 1204 통과.

## 주요 결정 / 근거
- **칸 키는 아이템 TID** — 버프 키와 같다. 같은 영약끼리만 이어 쓴다. 등급이 늘면 그때 종류별 칸을 다시 본다(#113 알아둘 점).
- **돌고 있는 슬롯이면 바로 채운다**(넣기·배치·정산 끝) — 안 그러면 신속의 영약 첫 판정이 기본 속도로 돌면서 횟수만 깎인다. 쉬는 슬롯은 판정이 없어 꺼내지 않는다(#113 "판정이 없을 때").
- **칸·버프·인벤토리 한 트랜잭션** — 따로 저장하다 하나만 남으면 영약이 사라지거나 두 번 쓰인다.
- 마이그레이션은 서버 기동 대신 `DbMigrator`와 같은 순서(파일 실행 + `user_version` 한 트랜잭션)로 로컬 DB에 적용했다 — 서버를 띄우고 강제 종료하는 명령이 자동 모드에서 막혔다.

## 후속 / 미완
- 클라 칸 화면 — T-194(태웅).
- `MikaDummyClient` 빌드 오류(`S_AccountLevelResponse.TraitPoint`)는 기존 문제 — 이번 작업과 무관.
