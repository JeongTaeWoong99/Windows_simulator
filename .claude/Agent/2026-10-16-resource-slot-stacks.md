---
date: 2026-10-16
title: 자원 칸별 묶음 — MaxStack은 칸 하나의 상한 (T-160)
tags: [server, db, protocol]
---

# 자원 칸별 묶음 (T-160 · 이슈 #73)

## 목적 / 배경
- 자원이 종류 하나 = 칸 하나라 MaxStack(9,999 · 상자 1,000)에 닿으면 칸이 남아도 버렸다. 칸이 다 찼을 때만 버리게 한다.
- 범위·결정 → `tasks/T-160-자원칸별묶음서버.md` · 규칙 → `Server/docs/인벤토리-창고.md` 1·3·5·6장

## 주요 결정 / 근거
- **`Inventory`는 칸 → 묶음 사전이다.** `Item.Slot`은 사전 키라 읽기 전용으로 바꿨다 — 밖에서 칸을 고치면 사전이 어긋난다. 칸을 옮길 때는 `SetSlot`·`Rewrite`로 새로 만든다.
- **칸 수는 `Func<int>`로 늦게 읽는다.** `User`가 GameTable(Constants)보다 먼저 만들어지는 테스트가 많다 — 생성자에서 읽으면 예외가 난다.
- **칸 수 검사(`HasStorageFor`)는 인벤토리 사본(`Clone`)에 실제로 넣어 본다.** "새 종류 수"를 세던 옛 방식은 덜 찬 칸 채우기·여러 칸 넘침을 셀 수 없다. 상자 개봉은 `consumed`로 인벤토리 몫을 먼저 빼고 본다(인벤토리부터 빠지는 `TryRemoveAcross`와 같은 순서).
- **DB는 칸째 쓴다** — 모든 자원 저장이 `SaveItemChangesRepository.WriteAsync` 하나를 지난다(UPSERT on `(user_id, container, slot)`, 0이면 DELETE). 판매·경매에 같은 SQL이 세 벌 있던 것을 합쳤다. 자원 쪽 `SaveStorageSlotsRepository`(item_id로 slot UPDATE)는 지웠다 — 칸이 PK가 되면 교환 중에 키가 겹친다.
- **자리 이동·정렬은 "칸 전체를 다음 상태로 바꾸고 달라진 칸만" 내보낸다(`Inventory.Rewrite`).** 합치기 때문에 수량이 바뀌어 `(Key, Slot)`만으로는 표현이 안 된다 → `S_StorageSlotsResponse.Items` 추가.
- **창고 이동은 놓은 칸만 본다.** 전에는 "받는 쪽 어디든 같은 자원이 있으면 그 칸에 합침"이었다 — 같은 자원이 여러 칸이 되면 어느 칸인지 모호하다. 같은 자원 칸에 놓으면 넘치는 몫은 원래 칸에 남기고, 하나도 안 들어가면 `StorageOverMaxStack`.
- 차감은 덜 찬 칸부터(이슈 제안 그대로) — 같은 수량이면 뒤 칸부터라 앞 칸이 자리를 지킨다.
- 마이그레이션 `0007`은 겹친 칸을 `-item_id` 칸으로 비켜 둔다 → `Inventory.Load`가 격자 밖 칸을 첫 빈 칸으로 옮긴다. 로컬 DB는 겹침 0건이었지만 원격 DB를 위해 넣었다.

## 후속 작업 / 주의사항
- **클라 T-161(태웅)** 전까지 클라는 `ItemId` 하나 = 칸 하나로 캐시한다. 자원 `Slots`(Key = ItemId)를 옛 형식으로 함께 싣지만, 같은 자원이 둘째 칸에 생기면 클라 표시가 어긋난다. T-161이 끝나면 자원 `Slots`를 빼고 `Items`만 남긴다.
- 정렬 동점 규칙에 "묶음 순번"을 더했다(`ResourceTieBreak` 4번째 값) — 클라 `CompareByRule`도 T-161에서 같은 칸 순서를 맞춰야 한다.
- 치트 `GiveItem`은 칸이 모자라면 전부 거절한다(`StorageFull`) — 전에는 MaxStack까지만 주고 조용히 잘랐다.
- 경매 테스트 3개 실패는 이번 작업과 무관한 기존 실패(이슈 #71 빈도 제한 하드코딩).
