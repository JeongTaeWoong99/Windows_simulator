---
date: 2026-11-21
title: 장비 아이템 레벨 데이터·서버 구현 · 장비 골드 뽑기 닫기
tags: [server, data, design, equip, gacha]
---

# 장비 아이템 레벨 데이터·서버 구현 · 장비 골드 뽑기 닫기 (T-200)

## 목적 / 배경
- T-199에서 확정한 [장비레벨](../../GameDesign/design/character/장비레벨.md)을 데이터·서버에 넣는다
- 이슈 #117(골드 뽑기 폐지) — 장비는 상자에서만

## 변경 내용
- `Equip.xlsx` — `ItemLevel` 컬럼(int · 1~100 · 기본값 없음) · Lv1 69종 레벨 1 · 새 가산 · Lv10~100 660행
- `Loc_Equip.xlsx` — 새 이름 EN 660개 · `Loc_UI.xlsx` — `UI.Result.EquipLevelTooLow`
- `Gacha.xlsx` — `GachaInfoTable` 3·4·5 `CostCurrency = None` · 비용 0
- `PacketEnum.cs` — `EquipLevelTooLow = 604`
- `User.Equip.cs` `TryEquip` — 종류 검사 다음에 `character.Level < equip.ItemLevel`이면 거절 · `Equip.ItemLevel`
- 테스트 — `UserEquipTest` 레벨 거절·경계 2개 · `UserCheatTest` 레벨 장비 지급 · `GachaEquipSheetTest`를 "장비 풀은 골드로 못 뽑는다"로 · `GachaServiceTest`·`StorageLimitTest`는 구슬 상자(1) 메타를 빌린다
- 기획 문서 — 캐릭터 1.3·5.1.1 · 아이템 4.1·뽑기 · 거래 3.2 · 게임UI · 게임기획코어 · 장비레벨 상태·TID

## 주요 결정 / 근거 (사용자 확인 2026-11-21)
- TID = 레벨 × 10000 + 종류·산업·등급 — 앞자리가 곧 레벨 (`101101` · `1003506`)
- 목검·원석(전 산업 입문)은 산업 장비의 절반 · 탐험가의 목걸이는 희귀 장신구 ×2 — 지금 비율 유지
- Lv10~100 판매가 = Lv1 같은 종류·등급 값 (레벨 반영 미정)
- **상자 확률표(장비레벨 3장)는 뺐다** — T-206(상자 15종 → 재질 5종)이 상자 구조를 바꿀 수 있어서. 지금 상자는 Lv1 장비만 준다
- 구슬 상자(풀 1)는 열어 둔다 — 뽑기 경로 테스트가 비용 메타를 빌리는 유일한 골드 풀

## 후속
- T-200 남은 것: 상자 풀(레벨 범위 · 상자 Lv별 등급 확률표) — T-206 방향 확정 뒤
- 클라(태웅): 뽑기 탭에서 장비 카드 빼기 · 새 장비 아이콘 · `EquipLevelTooLow` 표시 · 아이템 레벨 표시
- ⚠️ 이 맥에서는 `pwsh`가 세션 격리로 막혀 `generate-tables.ps1` 대신 `dotnet run --project Server/ExcelGenerator -- <루트>` + rsync 미러로 돌렸다. `check-doc-graph.ps1`은 못 돌렸다
- ⚠️ macOS 빌드는 `Assets/Plugins/Analyzers/MikaSourceGen.dll`을 바꾼다 — 소스 변경이 없으면 되돌린다
