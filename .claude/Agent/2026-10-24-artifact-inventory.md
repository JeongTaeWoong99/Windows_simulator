---
date: 2026-10-24
title: 아티팩트를 인벤토리로 받아 도감에 장착 · 켜고 끄기 · 중복 즉시 판매
tags: [server, design, data]
---

# 아티팩트 인벤토리 · 도감 장착

## 목적 / 배경
- 이슈 #81 · 결정과 이유는 → `tasks/T-181-아티팩트인벤토리도감장착.md` · `유물감정.md` 3장

## 변경 내용
- `User.Artifact.cs` 전면 교체 — `_codex`(번호 → 켜짐) + `Artifacts`(인벤토리 아티팩트 탭 · `Inventory` 클래스 재사용). 효과는 `_codex`의 켜진 번호만
- 패킷 — `S_ArtifactInventoryResponse`(탭 스냅샷) · `S_ArtifactListResponse`가 `ArtifactCodexInfo`로 · 장착 · 켜고 끄기 · 즉시 판매 · `EStorageTab.Artifact` · `C_StorageMergeRequest.Tab`
- DB `0009-artifact-inventory.sql` — `t_user_artifact_stack` · `t_user_artifact`에 `is_active` · `count` 삭제
- 감정(`GachaService.OpenBox`)은 아티팩트 칸까지 보고 안 들어가면 결과 전체를 우편(`ToAttachment`에 `ArtifactItems`) · 우편 수령도 칸 검사
- 경매 등록 — `AuctionDb.RegisterAsync`가 아티팩트 칸 변경분을 같은 트랜잭션에 쓴다(변경이 비면 던져서 되돌린다)

## 주요 결정 / 근거
- **아티팩트 탭을 장비 탭과 나눴다** — 제안은 "장비 탭의 칸"이었지만 장비 칸은 개체 PK 키라 번호별 스택을 넣으면 이동·정렬·창고·판매·경매가 전부 두 종류를 갈라야 한다. 자원 탭의 `Inventory`가 (번호, 수량, 칸)에 그대로 맞는다. 클라는 장비 탭 안에 그려도 되지만 칸 번호는 따로다
- 창고·잠금은 이번에 넣지 않았다 — 창고는 탭이 없다고 거절, 잠금 표는 ItemTID 공간이라 섞인다
- 즉시 판매가는 `SellPrice(BasePrice)` — 장비·캐릭터와 같은 비율(지금 1000‰)이라 결정(감정 비용 2배 = BasePrice)과 같다

## 후속 작업 / 주의사항
- `game.sqlite3`에 0009를 적용해 커밋하지 않았다 — main 트리 DB가 다른 세션 변경으로 미커밋이라 ff 머지가 막힌다. 서버 기동 시 자동 적용되니 그때 커밋한다
- 클라는 T-151 (태웅) — #81로 넘겼다
