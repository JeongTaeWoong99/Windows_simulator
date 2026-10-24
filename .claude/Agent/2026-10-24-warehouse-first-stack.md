---
date: 2026-10-24
title: 자원 지급을 창고의 같은 자원 칸부터 · 합치기 · 창고에 쌓기 (T-175 · #82)
tags: [server, inventory, warehouse, packet]
---

# 자원 지급을 창고의 같은 자원 칸부터 채운다

## 목적 / 배경
- 이슈 #82(태웅 제안 + 보강 코멘트)를 진우가 "그대로" 받기로 함
- 창고에 맡긴 자원이 들어올 때마다 인벤토리에 새 칸이 생겨 손으로 옮겨야 했다

## 변경 내용
- `Inventory.TopUp` — 같은 자원의 덜 찬 칸만 채움(새 칸 안 만듦). `AddItem`은 TopUp + 빈 칸으로 재구성
- `Inventory.Merge` — 같은 자원을 칸 순서 그대로 앞 칸부터 MaxStack까지 다시 채움(`Rewrite` 재사용)
- `User.Deposit` — 창고 TopUp → 인벤토리 AddItem. `GainItem`과 `ItemsFit`(HasStorageFor)이 같은 함수를 지남
- `ItemsFit` — 창고 사본도 만들고 consumed도 인벤토리 → 창고 순으로 뺀 뒤 판정
- `User.MergeStorage`(StorageSlot) · `User.StackToWarehouse`(Warehouse)
- 패킷 `C_StorageMergeRequest = 85 { Container }` · `C_WarehouseStackRequest = 86 { }` — 응답은 기존 `S_StorageSlotsResponse`·`S_WarehouseTransferResponse` 재사용
- 테스트 `UserWarehouseFirstGainTest`(7) · `UserStorageMergeTest`(8), 전체 1065 통과
- `Server/docs/인벤토리-창고.md` 결정 표·칸 배정·패킷 표 갱신

## 주요 결정 / 근거
- 창고 빈 칸에 새 묶음 금지 — 창고는 플레이어가 고른 것만 두는 곳
- [창고에 쌓기]에서 창고가 차 남는 몫은 인벤토리에 그대로(#82 열린 질문 — 제안 그대로 수용)
- 쌓기 때 인벤토리 차감은 `TryRemoveItems`(적은 칸부터) — 조각을 줄이는 기존 차감 규칙과 같게
- 지급 순서를 `Deposit` 하나로 묶은 이유: 지급과 칸 수 검사가 갈라지면 `StorageFull` 오판이 다시 난다

## 후속 작업 / 주의사항
- 클라(태웅): 창고 칸 갱신 화면 반영 확인 · [합치기]·[창고에 쌓기] 버튼 — #82 클라로 넘김
- 더미 클라이언트 메뉴에는 새 요청을 넣지 않았다
- `인벤토리-창고.md` 4장 끝 "한도 검사는 인벤토리 쪽만 본다" 문단은 T-164 때부터 낡은 문장 — 손대지 않음
