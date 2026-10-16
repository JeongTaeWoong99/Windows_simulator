---
date: 2026-10-16
title: 창고 물건도 인벤토리와 똑같이 쓴다 — 서버 (T-164 · 이슈 #74)
tags: [server, warehouse, inventory, auction]
---

# 창고 물건도 인벤토리와 똑같이 쓴다 — 서버

## 목적 / 배경
- 창고가 "꺼내야 쓰는 보관소"라 쓰는 것은 맡길 수 없고 맡긴 것은 쓸 수 없었다. 2026-10-15 결정으로 창고는 놓인 자리일 뿐, 막는 것은 잠금뿐.

## 주요 결정 / 근거
- **보관함별 사전(`_characters`/`_warehouseCharacters` 등)은 그대로 둔다.** 칸·정렬·이동 코드가 보관함별 묶음을 전제한다. 대신 조회(`TryGetCharacter`·`TryGetEquip`)와 삭제(`RemoveCharacter`·`RemoveEquip`)가 양쪽을 본다 — 배치·장착·큐브·응축·잠금·판매·경매·로그인 복원·정산이 전부 이걸로 풀렸다.
- `ToCharacterInfo`·`ToEquipInfo`는 컨테이너를 인자로 받지 않고 **개체가 있는 사전에서 읽는다.** 인자 기본값(Inventory)으로 두면 창고 개체의 싱크(장착·응축·경험치)가 클라에 "인벤토리로 옮겨졌다"로 간다.
- 자원 소모는 `Inventory.TryRemoveAcross(first, second)` 하나 — 인벤토리 먼저, 모자란 만큼 창고. 판매·경매·상자·큐브가 `User.TryRemoveItems`를 지난다. 지급(`GainItem`·`ClampToMaxStack`)은 인벤토리만 본다(새 것은 언제나 인벤토리로).
- 상자 개봉의 "칸이 빈다" 판정은 `FreesInventorySlot` — 합계가 아니라 **인벤토리 몫이 count 이하**일 때 빈다.
- 마지막 캐릭터 판정은 `OwnedCharacterCount`(창고 포함).
- `StorageEntityBusy`(1104)는 패킷 호환 때문에 enum에 남기고 서버는 더 쓰지 않는다.

## 겪은 함정
- **경매 반환·구매 우편 수령 SQL이 `container`를 안 건드렸다.** 지금까지는 창고 개체를 올릴 수 없어 문제가 없었지만, 이제 창고에서 올린 개체가 돌아오면 인벤토리 칸 번호를 받은 채 창고에 남아 겹친다 → `UnlockEquipAsync`·`UnlockCharacterAsync`에 `container = 0`.

## 후속 작업 / 주의사항
- 클라 T-163(태웅)이 창고 제외 처리를 걷어내야 화면에서 쓸 수 있다.
- 기존 실패 3건(`검색은_순간_5회를_넘으면_거절한다`·`너무_잦으면_조회하지_않는다`·`목록_가격대_검색은_빈도_제한을_함께_쓴다`)은 이번 작업과 무관 — 7c31669에서 `AuctionSearchBurst`를 두 배로 늘리고 테스트를 안 고쳤다.
