---
date: 2026-10-15
title: 인벤토리 칸 프레임을 칸 수만큼 열기 · 칸별 묶음 서버 요청
tags: [client, ui]
---

# 인벤토리 칸 프레임을 칸 수만큼 열기 · 칸별 묶음 서버 요청

## 목적 / 배경
- 시트 `StorageCapacity` 50인데 씬 프레임 200개가 다 열려 있었다. 유저별 칸 확장(특성 등) 여지도 남긴다 → tasks/T-162
- 자원이 `MaxStack`에 닿으면 칸이 남아도 버려진다 → 칸별 묶음은 서버 구조(종류 = 칸 하나 · DB PK에 item_id)라 이슈 #73 · tasks/T-160 · 클라 후속 T-161

## 변경 내용
- `Managers/PlayerDataModel.cs` — `GetStorageCapacity(tab)` 칸 수의 단일 출처
- `UI/Inventory/InventoryGridPresenter/InventoryGridPresenter.cs` — `ApplyCapacity`(Redraw 첫머리) · `_openFrames` · `_frameTemplate` · 프레임 루프가 `_openFrames`까지만

## 주요 결정 / 근거
- 엑셀은 그대로 — 칸 상한은 이미 `Item.MaxStack`(아이템별)에 있다. 뜻만 "총량 → 칸 하나"로 바뀐다(사용자 확인).
- 원본 프레임은 `CacheFrames`에서 칸이 들어가기 전에 뜬다 — 나중에 첫 프레임을 복제하면 그 안의 SlotView까지 따라온다.
- 원본은 격자 루트 아래에 꺼 둔다 — Content 밖이라 레이아웃·`slotParent` 열거에 끼지 않는다.
- 칸 수가 바뀔 때만 SetActive — Redraw마다 켜고 끄면 레이아웃 리빌드가 매번 돈다.

## 후속 작업 / 주의사항
- 씬 프레임 200 → 50 삭제는 사용자 허락 뒤 에디터 eval로 했다(`Undo.DestroyObjectImmediate` + `SaveScene`). 에디터 모드에서는 `Constants`가 안 올라와 NRE — 50을 직접 넣었다.
- 씬 diff의 '+' 블록 대부분은 줄 맞춤 착시다(같은 fileID가 HEAD에도 있다). 진짜 추가는 레이아웃 값 재직렬화뿐.
- `Test Copy` 씬도 같은 격자를 쓴다 — 빌드 씬은 `Original`뿐.
