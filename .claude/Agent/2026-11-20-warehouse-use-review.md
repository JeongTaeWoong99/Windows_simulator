---
date: 2026-11-20
title: T-164 창고 물건 사용 푸시 검토 — 분해 재료 증발 수정
tags: [server, warehouse, disassemble, review]
---

# T-164 창고 물건 사용 푸시 검토 (T-165)

## 목적 / 배경
- `383657d`(T-164) 뒤 main 코드 기준으로 창고 물건 사용을 다시 봤다. 항목별 결론 → `tasks/archive/T-165-창고물건사용푸시검토.md`

## 변경 내용
- `User.Disassemble.cs` — 재료 지급을 `Inventory.AddItem` → `Deposit(Warehouse, Inventory, …)`
- 테스트 7개 추가 — 분해 재료 창고 칸(버그 재현) · 큐브 · 응축(캐릭터 싱크 Container) · 경매 등록 3종 · 맡긴 뒤 재로그인(SQLite)
- `Server/docs/인벤토리-창고.md` 4장 — T-164 전 문장 교체

## 주요 결정 / 근거
- **자원 지급은 칸 검사(`ItemsFit`)와 같은 순서여야 한다.** 검사는 `Deposit`을 사본에 돌린다 — 지급이 다른 길(`Inventory.AddItem`)이면 검사 통과분이 버려진다. 새 지급 경로는 `GainItem` 또는 `Deposit`만 쓴다(`Inventory.AddItem` 직접 호출 금지)
- 개체(캐릭터·장비) 칸 배정·칸 수 검사는 인벤토리만 — 창고는 플레이어가 고른 것만 둔다

## 후속 작업 / 주의사항
- 레이드 편성 캐릭터는 `IsCharacterBusy`에 없다(판매·경매 가능). 창고와 무관, 손대지 않음
