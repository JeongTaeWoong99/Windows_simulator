---
date: 2026-10-28
title: 아티팩트 칸을 장비 칸과 한 격자로 합치고 번호 단위로 잠근다 (T-188 · 이슈 #87)
tags: [server, artifact, storage, lock, migration]
---

# 아티팩트 장비 칸 합치기 · 잠금

## 목적 / 배경
- 클라는 아티팩트를 장비 탭에 장비와 함께 그리는데, 서버 아티팩트 칸 번호가 장비와 따로라 끌어 옮기기·[정렬]·잠금이 막혀 있었다 (#87).

## 변경 내용
- `Inventory` — `reserved`(같은 격자를 쓰는 남의 칸) 함수를 받는다. 빈 칸·남은 칸 수 계산이 그 칸을 찬 것으로 본다. `Load`가 옮긴 칸의 변경분(옛 칸 비움 + 새 칸)을 돌려준다.
- `User.Artifacts`는 생성자에서 장비 칸(보유 + 지급 대기)을 `reserved`로 받는다. 장비 쪽(`NextFreeEquipPosition` · `EquipSlotsUsed`)도 아티팩트 칸을 센다.
- `User.StorageSlot` — 인벤토리 + (Equip|Artifact) 탭이면 `MoveEquipGrid`/`SortEquipGrid`. 자원 칸 이동 로직은 `MoveStacks`로 뽑아 함께 쓴다.
- 잠금 — `ELockTarget.Artifact` · `t_user_artifact_lock` · `_lockedArtifactTids`. 판매·경매·분해에서 `EntityLocked`를 낸다.
- `0010` 마이그레이션. 워크트리 DB가 판 8이어서 이번에 0009까지 함께 적용해 커밋했다(T-181에 남아 있던 일).

## 주요 결정 / 근거
- 응답 패킷은 새로 만들지 않았다. 장비 자리는 `S_StorageSlotsResponse.Slots`, 아티팩트 자리는 `S_ArtifactInventoryResponse` 스냅샷으로 보낸다(진우 선택). 그래서 `Tab = Artifact`로 이동해도 응답 탭은 `Equip`이다.
- [정렬]은 장비를 앞에, 아티팩트를 뒤에 둔다(진우 선택). 장비와 아티팩트를 섞어 세우지 않는다.
- 적재할 때 겹친 칸을 옮기면 **반드시 저장한다**. 메모리에서만 옮기면 DB의 옛 칸 행이 남고, 다음 저장 때 새 칸 행까지 생겨 아티팩트가 복제된다.
- 창고 장비를 아티팩트가 선 인벤토리 칸에 놓으면 `StorageSlotArtifact`(1107)로 거절한다. 아티팩트는 창고로 갈 수 없어 맞바꿀 수 없기 때문이다.

## 후속 작업 / 주의사항
- 클라 `EquipSlotSource.FillArtifacts`(임시 자리 계산)를 서버 칸 번호로 바꿔야 한다. 이슈 #87로 넘겼다.
- 자원 탭 `Inventory.Load`의 비킴도 저장하지 않는 것은 예전과 같다. 같은 복제 위험이 있지만 손대지 않았다.
- 아티팩트 지급 치트는 없다. `GiveItem`으로 미감정 유물을 받아 감정해 확인한다.
