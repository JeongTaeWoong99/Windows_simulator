---
date: 2026-10-16
title: 자원 잠금 — 서버 (T-159 · 이슈 #72)
tags: [server, lock, inventory, auction, migration]
---

# 자원 잠금 — 서버

## 목적 / 배경
- 캐릭터·장비 잠금(T-137)에 이어 자원도 잠근다. 자원은 개체가 없어 종류(TID) 단위.

## 주요 결정 / 근거
- **패킷은 그대로** — `C_EntityLockRequest`/`S_EntityLockResponse`에 `ELockTarget.Resource`(3)만 더했다. `Id` = TID.
- **로그인 전달은 `S_InventoryResponse.LockedItemTids`** — 자원 스냅샷과 같이 오므로 클라가 한 번에 맞춘다. 별도 패킷을 두지 않았다.
- **저장은 `t_user_item_lock (user_id, item_id)`** — 잠긴 종류만 행. 보유 0개가 돼도 지우지 않는다(이슈 요구). 잠그기는 `INSERT OR IGNORE`, 풀기는 `DELETE`. 기존 `EntityLockRepository`에 분기로 넣었다.
- 아이템 테이블에 없는 TID는 `InvalidLockTarget`. 보유 여부는 보지 않는다(0개여도 잠글 수 있다).
- 거절 위치: `ShopService.Sell`(하나라도 잠긴 종류면 전부 거절) · `User.DescribeItem`(경매 자원 등록). 소비(`TryConsumeItems`)는 막지 않는다.
- 아티팩트 경매(`EAuctionKind.Artifact`)는 이번 범위 밖 — 자원이 아니라 별도 보관함이다.

## 겪은 함정
- main 트리의 `game.sqlite3`에 다른 세션의 미커밋 변경이 있어, 워크트리에서 DB를 커밋하면 `--ff-only`가 거절된다 → 워크트리에서는 마이그레이션 파일만 커밋하고, 머지 뒤 main DB가 깨끗해진 것을 보고 main에서 0008을 적용해 DB를 따로 커밋했다.

## 후속 작업 / 주의사항
- 클라(태웅): `Resource` 토글 · 자물쇠 · 일괄 담기 제외 · 로그인 `LockedItemTids` — `Inventory 규칙.md` 304행의 "자원은 잠그지 않는다"도 클라 몫으로 고칠 것.
