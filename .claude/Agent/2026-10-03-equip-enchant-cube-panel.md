---
date: 2026-10-03
title: 큐브 창 · 효율 계산 능력치 칸 반영 (T-095)
tags: [client, ui, editor]
---

# 큐브 창 · 효율 계산 능력치 칸 반영 (T-095)

## 목적 / 배경
- #46 서버 처리(`ac9a8e3`)로 규칙이 확정돼 클라 남은 몫(받기 · 큐브 화면 · 거절 문구 · 효율 계산)을 붙였다.
- 사용자 결정 D1~D6 · "판매 목록 자리에 연다" · "규칙 설명을 화면·툴팁에" → `tasks/T-095` 요약에 적었다.

## 변경 내용
- 수신: `ServerPacketHandler.Handle_S_EquipEnchantResponse` → `PlayerDataModel.EquipEnchantCompleted` · `FindEquip` 추가
- 화면: `UI/Inventory/EquipEnchantPresenter/` 신설(`EquipEnchantPresenter` · `EnchantCubeView`), 씬 `#Inventory Canvas`에 `Equip Enchant Presenter` (특성 정보 영역·특성 칸 프리팹을 복제해 만듦 · 꺼진 채 저장)
- 배선: 격자 장비 칸 좌클릭 → `Open` · 우클릭 → `Close` / 판매 목록이 `OpenChanged`로 비우고 물러남
- 설명: `EquipLabel.BuildCubeRuleTooltip`(규칙 + 등급별 확률 표) · 장비 툴팁에 `인챈트 등급` 줄 · `GameDataLoader.GetEnchantUpPermyriad`(서버 식 사본)
- 효율 계산: `WorkStationSelectPresenter.GetEquipSpeedAdd`가 장비 기본값·칸의 산업을 따로 본다
- 문서: `Inventory 규칙.md`("큐브 창" 절) · `UI 배치 현황.md` · `Main 규칙.md`

## 주요 결정 / 근거
- **자리 전환을 각자 판단** — 판매 목록이 큐브 창을 듣고 스스로 켜고 끈다. 캔버스 View 경유(경매 등록 바로가기 방식)는 같은 캔버스 안이라 과했다.
- **이전 칸은 보낼 때 복사** — `S_EquipEnchantResponse`에 이전 옵션이 없다(BeforeGrade만 있다).
- **해제하고 사용**은 해제 응답을 받은 뒤 큐브를 보낸다(동시에 보내지 않는다). `EquipCompleted`는 작업슬롯 화면도 들으므로 `_unequipThenEnchantId`가 있을 때만 반응.
- 이름 텍스트에서 `UIThemeColor`를 뺐다 — 등급색을 코드가 칠하는데 테마가 덮을 수 있다.

## 후속 작업 / 주의사항
- D6 엑셀 — 처음엔 자동 승인 분류기에 막혔고, 사용자가 settings.local.json에 openpyxl·format-excel·generate-tables 허용 규칙을 넣은 뒤 반영했다. 생성 `.cs`는 줄바꿈만 바뀌어 되돌렸다.
- 실측 안 함(서버 미기동) — 첫 사용 · 상승 · 해제하고 사용 · 큐브 0개 · 효율 계산 일치.
- `OnWaitClosed`는 해제 손잡이의 닫힘이 큐브 손잡이를 지우지 않게 `IsClosed`를 본다 — 대기 둘이 이어지는 유일한 곳이다.
- 효율 계산 사본은 T-055가 오면 지운다.
