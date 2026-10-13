---
date: 2026-10-13
title: 캐릭터·장비 잠금 (T-137)
tags: [server, protocol, db, test]
---

# 캐릭터·장비 잠금 (T-137)

## 목적 / 배경
- 응축·고등급 개체를 판매·경매·응축 재료로 실수로 날리지 않게 (이슈 #65 · 태웅 요청)
- 대상은 캐릭터·장비만 — 자원은 TID 하나 = 칸 하나라 개체 단위 잠금이 맞지 않는다 (2026-10-13 진우 결정)

## 변경 내용
- 패킷 `C_EntityLockRequest { Target, Id, Locked }` / `S_EntityLockResponse` (75·76) · `ELockTarget` · 결과 코드 `EntityLocked` 1200 · `InvalidLockTarget` 1201
- `CharacterInfo`·`EquipInfo.IsLocked` · 도메인 `Character`·`Equip.IsLocked` · 로그인 적재
- 마이그레이션 `0005-entity-lock` — `t_character`·`t_user_equip`에 `is_locked`
- 거절: `TrySellEntities` · `DescribeEquip`/`DescribeCharacter`(경매 등록) · `TryCondenseCharacter` 재료
- Unity `ServerPacketHandler.EntityLockResponded` 이벤트

## 주요 결정 / 근거
- 같은 상태로 다시 요청해도 Ok — 클라가 두 번 눌러도 어긋나지 않게
- 응축 **대상**·장착·배치는 막지 않는다 — 잠금은 없애는 동작만 막는다
- `EntityLockRepository`는 `auction_trade_id = 0`만 바꾼다 — 경매로 넘어간 행을 건드리지 않게
- 마이그레이션 번호는 0005 — T-142(0002)·T-150(0003)·T-152(0004) 뒤에 main으로 들어갔다

## 후속 작업 / 주의사항
- 장비 분해(T-131) 구현 때 잠긴 장비 거절을 넣는다
- 창고(container 1) 개체는 아직 메모리에 없다(T-107) — 잠금은 DB에 남으니 창고가 열리면 그대로 읽으면 된다
- 클라: 자물쇠 표시 · 토글 · 잠긴 칸 판매 카트·재료 선택 막기 (태웅)
