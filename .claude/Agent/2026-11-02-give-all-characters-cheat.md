---
date: 2026-11-02
title: 전 캐릭터 1장씩 지급 치트 GiveAllCharacters (T-190 · 이슈 #102)
tags: [server, cheat, character, warehouse]
---

# 전 캐릭터 1장씩 지급 치트

## 목적 / 배경
- 클라(태웅)가 캐릭터 그림·모션을 한 번에 보려고 CharacterTable 전 종류를 1장씩 받는 치트를 요청했다(이슈 #102).
- 기존 `GiveCharacter`는 TID 하나 · 인벤토리 칸만 · 모자라면 거절이라 29종을 한 번에 줄 수 없다.

## 변경 내용
- `Server/MikaProtocol/PacketEnum.cs` — `ECheatCommand.GiveAllCharacters = 18` (미러 `Assets/Scripts_Server/Protocol` 자동 동기화)
- `User.Cheat.cs` — `CheatGiveAllCharacters(now)`: 표 순서대로 창고 빈 칸 → 인벤토리 빈 칸 → 나머지는 `StoreOverflowMail` 한 통. 응답 `창고 N · 인벤토리 N · 우편 N`
- `User.Character.cs` — `GrantCharacters(tids, container)` 추가, `GrantGachaCharacters`는 이것의 인벤토리판. `OnGachaCharactersGranted` → `OnCharactersGranted(granted, container)`로 이름·인자 변경. 칸 예약·빈 칸 찾기가 보관함을 받는다
- `User.Storage.cs` — 창고 캐릭터 대기 칸 `_pendingWarehouseCharacterSlots` · `WarehouseCharacterSlotsUsed`
- `User.StorageSlot.cs` — `PendingSlots`가 창고 캐릭터 탭의 대기 칸도 돌려준다(이동·정렬이 대기 칸을 피하게)
- `GrantCharacterRepository.cs` — `Container` 인자(기본 인벤토리), INSERT에 `container` 컬럼
- 테스트: `UserCheatGiveAllCharactersTest`(6) · `WarehouseRepositoryTest`에 창고 INSERT 1건. 전체 1150 통과
- `Server/docs/치트.md` 2장 표에 행 추가

## 주요 결정 / 근거
- **창고로 바로 받는 지급은 이 치트뿐이다.** 게임 지급은 여전히 인벤토리로만 들어온다 — 기존 경로는 기본 인자로 그대로다.
- 우편은 한 통에 다 넣었다. 받을 때 `HasStorageFor`가 인벤토리 칸을 다시 보므로 모자라면 나눠 받는다(이슈 본문이 허용).
- 칸이 없어도 거절하지 않는다 — 넘치는 건 우편으로 간다.
- 지급이 두 번(창고·인벤토리) 나가므로 캐릭터 목록 재전송도 두 번이다. 치트라 묶지 않았다.

## 후속
- 클라: 치트 창 [캐릭터 지급]에 [전 종류 1장씩] 버튼(T-190, 태웅).
- PR 머지 뒤 INDEX(`tasks/README.md`) 갱신.
