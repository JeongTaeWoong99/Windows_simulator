---
date: 2026-09-06
title: 창고 캐릭터 칸에 등급 색 표시 (T-045)
tags: [client, ui, data]
---

# 창고 캐릭터 칸에 등급 색 표시

## 목적 / 배경

창고 캐릭터 탭의 칸이 전부 회색이라 등급이 목록에서 드러나지 않았다.
데이터가 없어서가 아니라 `CharacterSlotSource.Fill`이 `GlobalRarity.None`을 못박아
넘기고 있었기 때문이다 → `tasks/archive/T-045-캐릭터등급표시.md`

## 변경 내용

- `Assets/Scripts_Client/Data/GameDataLoader.cs` — `GetCharacterRarity(int characterTid)` 추가
- `Assets/Scripts_Client/UI/Storage/StorageGridPresenter/CharacterSlotSource.cs` — 그 값을 칸에 넘긴다
- 낡아진 🔴 문구 정리 — 위 파일 주석 · `StorageSlotData.cs` · `UI/Storage/Storage 규칙.md`

## 주요 결정 / 근거

- **칸(`InventorySlotView`)·`RarityPalette`는 건드리지 않았다.** "칸은 조회하지 않고 받아 그린다"가
  창고의 규약이라, 출처가 탭마다 다른 것은 공급자가 흡수한다 → `Storage 규칙.md`
- **창고는 테이블에서 읽고, 가챠 결과창은 패킷(`GachaRewardInfo.Rarity`)에서 읽는다 — 일부러 다르다.**
  가챠가 테이블을 다시 뒤지면 두 값이 어긋났을 때 조용히 패킷 쪽을 무시하게 된다
  (`GetItemRarity`의 주석과 같은 이유). 대신 **두 경로가 같은 색을 내는지는 눈으로만 확인된다** —
  실측에서 창고 색과 결과창 색을 반드시 비교한다.

## 후속 작업 / 주의사항

- 🔴 **`CharacterId`(DB 개체 PK)와 `CharacterTid`(테이블 종류)를 섞으면 조용히 회색이 된다.**
  조회는 전부 TID, 칸의 `Key`만 개체 번호다. 컴파일도 경고도 통과하는 종류의 실수다.
- 실측 완료 (2026-09-06) — 창고 색과 가챠 결과창 색이 같다. 커밋 `2828fe6`.
- 등급 **스프라이트**는 여전히 없다(색만) — `RarityPalette`의 TODO이자 T-031의 🎨 항목이다.
