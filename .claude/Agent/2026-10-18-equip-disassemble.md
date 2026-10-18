---
date: 2026-10-18
title: 장비 분해 서버 — 장비 → 등급별 마력의 파편 (T-131)
tags: [server, equip, packet]
---

# 장비 분해 서버 (T-131)

## 목적 / 배경
- 중복 장비의 출구(T-076). 재료 `100050` 마력의 파편 · 등급별 수량은 `Equip.xlsx` `DisassembleTable`(등급이 키).

## 결정
- 패킷 `C_EquipDisassembleRequest { EquipIds }` → `S_EquipDisassembleResponse { Result, ItemChangeInfos }` (83·84). 개체 판매(`TrySellEntities`)와 같은 모양 — 여러 개를 한 번에, 전부 성공하거나 전부 실패
- 결과 코드 1300~ 절: `InvalidDisassembleRequest`(빈·중복) · `DisassembleEquipWorn`. 나머지는 기존 코드 재사용(EquipNotOwned · EntityLocked · EnchantPending · StorageFull)
- 칸 검사는 `HasStorageFor`(사본에 넣어 보기) — 이미 가진 파편 칸에 쌓이면 칸이 꽉 차도 통과한다. 모자라면 장비를 지우지 않는다(우편 넘침 안 씀 — 2026-10-12 결정)
- 재료 지급은 `GainItem`이 아니라 `Inventory.AddItem` 직접 — 장비 삭제와 한 트랜잭션(`DisassembleEquipsRepository`)으로 쓰려고. 인챈트는 장비 행 안이라 함께 지워진다
- 창고 장비도 분해한다(`TryGetEquip`이 둘 다 본다) — 재료는 인벤토리로
- 분해 표에 없는 등급은 `InvalidDisassembleRequest`로 거절(데이터 누락 방어)

## 검증
- 서버 테스트 1048개 통과 — `UserEquipDisassembleTest` 10 · `UserPremiumCubeTest` +1(보류 중 분해 거절) · `DisassembleEquipsRepositoryTest` 2(실제 SQLite)

## 남은 것
- 클라 분해 버튼·결과 표시 — 태웅(이슈)
- 치트(`GiveEquip`)로 받아 화면에서 확인 — 클라 버튼 뒤
