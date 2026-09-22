---
date: 2026-09-22
title: 창고 칸 한도 — 뽑기·상자 개봉을 StorageFull로 거절 (T-063)
tags: [server, gacha, storage, T-063]
---

# 창고 칸 한도 (T-063)

## 목적 / 배경
- 서버에 창고 한도가 없어서 200칸을 넘겨도 뽑기가 통과했다. 우편(T-081)의 원자적 수령에도 같은 칸 계산이 필요하다.

## 변경 내용
- `User.Storage.cs`(새 파일) — `StorageCapacity = 200`, 칸 수 3종, `HasStorageFor(itemTids, characters, equips, freedItemTid)`
- `GachaService` — 뽑기: 추첨 → 칸 검사 → 차감 → 지급. 상자: 수량 확인 → 추첨 → 칸 검사 → 차감 → 지급
- `User.Character` — `_pendingCharacterCount`(DB가 PK를 발급하기 전의 캐릭터 수)
- `Inventory.KindCount` · `EResultCode.StorageFull = 103`(미러 반영됨)
- `StorageLimitTest` 8건

## 주요 결정 / 근거
- **캐릭터 대기분을 센다.** 뽑은 캐릭터는 DB가 PK를 준 뒤에야 `_characters`에 들어간다. 대기분을 안 세면 응답 전 연속 뽑기가 한도를 넘긴다. 장비는 원래 `_pendingEquipPositions`로 세고 있었다. DB가 실패하면 세션이 끊기므로 카운터가 새지 않는다.
- **상자를 전부 열면 상자 칸이 빈다**는 것까지 계산한다. 이게 없으면 200칸이 찬 상태에서 상자를 열어 정리할 방법이 없다.
- 캐릭터·장비 수는 `Math.Max(Count, MaxCount)`로 계산했다. 지급 때 굴리는 구간 값이 검사한 값보다 커질 수 없게 하려는 것이다.
- 채취·치트 방침은 이번 범위에서 정하지 않고 T-084로 넘겼다.

## 후속 작업 / 주의사항
- 클라 `ResultMessages`에 `StorageFull` 문구가 아직 없다 → T-064
- 한도를 시트로 옮기는 일 → T-077
- 넘침 보관(상자가 창고에 안 들어가면 우편으로) → T-082. `OpenBox`의 `StorageFull` 분기가 그 자리다
- 커밋하면 T-063을 완료로 보관한다
