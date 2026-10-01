---
date: 2026-10-01
title: 인벤토리 칸 위치 저장 · 정렬 · 자리 이동 (T-058)
tags: [server, data, test]
---

# 인벤토리 칸 위치 저장 · 정렬 · 자리 이동 (T-058)

## 목적 / 배경
- 클라 [정렬]·빈 칸 유지가 세션 한정이라 재접속하면 자리가 사라졌다 → 서버가 칸을 들게 했다.
- 같은 날 "창고 = 인벤토리 확장 보관소"가 정해져(→ `Server/docs/인벤토리-창고.md`), 칸 모델에 `container`를 미리 넣었다. 창고 본체는 → `tasks/T-107`.

## 변경 내용
- 설계·계획 → `Server/docs/인벤토리-창고.md` · `docs/superpowers/plans/2026-10-01-T-058-칸위치.md`
- 커밋 목록 → `tasks/T-058` 관련 커밋

## 주요 결정 / 근거
- **칸은 개체가 든다** (`Item.Slot`·`Character.Slot`·`Equip.SlotPosition`). 처음엔 상태 있는 `SlotGrid`(키→칸 사본)를 설계했다가 버렸다 — 개체 필드와 사본 두 곳이 어긋난다. 계산만 순수 함수 `StorageSlots`·`StorageSort`.
- **정렬 규칙은 클라 코드에서 옮겼다**(`InventorySlotSource.CompareByRule`·탭별 `CompareForSort`). 설계 초안의 "방향은 주 기준만 뒤집는다"는 틀렸다 — 클라는 규칙 **전체**를 뒤집고, 나가 있는 것(배치·장착)만 방향과 무관하게 맨 뒤. 장비 동점은 종류→산업도 본다.
- **칸 중복을 DB 제약으로 막지 않는다** — 교환은 두 행을 함께 고치는데 유니크 제약은 문장마다 걸려 중간 상태에서 터진다. 메모리가 원천.
- 로그인 때 자원 칸이 겹치면 **겹치지 않은 칸을 먼저 전부 잡고** 겹친 것만 빈 칸으로 옮긴다 — 한 번에 돌면 겹친 것이 뒤 항목의 제 칸을 빼앗는다(계획 코드가 이 버그를 갖고 있었다).
- 캐릭터도 장비처럼 **지급 전에 칸을 예약**(`_pendingCharacterSlots`) — 응답 전 연속 뽑기가 같은 칸을 잡는다. #47이 같은 날 만든 **우편 캐릭터 수령**에도 같은 예약을 넣었다(`UnlockCharacterAsync`에 slot 인자).
- 패킷 ID는 65~67 — 동시 세션(#47)이 63·64를 먼저 썼다.

## 후속 작업 / 주의사항
- `t_user_inventory` 저장 SQL은 **4곳**이다(InventoryRepository 2 · ShopRepository · AuctionDb.WriteInventoryAsync). 새 경로를 만들면 `container`·`slot`을 함께 써야 한다 — 빠뜨리면 메모리는 맞는데 재로그인에서 칸이 0으로 돌아간다.
- 새 캐릭터·장비 획득 경로를 만들면 반드시 `ReserveCharacterSlots`/`NextFreeEquipPosition`으로 칸을 예약한다.
- 로그인·정렬·이동은 `container = 0`만 본다. 창고(T-107)가 `container = 1`을 붙일 때 `SaveStorageSlotsRepository.SqlFor`도 container를 받게 고친다.
- 운영 DB 마이그레이션은 적용·커밋됐다(`Server/Shared/migrations/2026-10-01-storage-slot.sql`). 다른 사람 로컬 DB는 같은 스크립트를 한 번 돌려야 한다.
- `UserMailTest.접속_중인_유저에게…`는 기존 불안정 테스트(→ `tasks/T-089`) — 이번 작업과 무관하게 가끔 빨개진다.
