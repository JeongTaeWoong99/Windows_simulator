---
date: 2026-10-09
title: 효율 계산을 서버가 준 속도 내역으로 그리고 치트 창에 전역 배수 칸 추가 (T-055 · 이슈 #60)
tags: [client, workstation, cheat, t-055]
---

# 효율 계산을 서버 내역으로 · 전역 배수 치트 칸

## 목적 / 배경
- 서버가 `WorkStationSlotInfo`에 속도 내역(기본값·레벨·특성·장비·★·전역 배수)을 싣기 시작했다(이슈 #60).
- 클라는 서버 식을 베껴 가산을 되짚고 전역 배수를 역산해 왔다 — 서버가 항목을 늘리면 조용히 틀린다.

## 변경 내용
- `WorkStationSelectPresenter.RefreshEfficiency` — 받은 필드를 그대로 그린다. 역산·허용 오차 상수·`GetEquipSpeedAdd`·`AppliesTo` 삭제.
- `GameDataLoader.GetLevelSpeedAdd` 삭제 — 쓰는 곳이 효율 계산뿐이었다.
- `CheatWindow` — 정산 칸 아래에 전역 배수(천분율, 100~100,000) 입력·[적용]·[×1]. 지금 값은 첫 슬롯의 `GatherSpeedPermille`.

## 주요 결정 / 근거
- 전역 배수 안내는 `GatherSpeedPermille`이 0(필드 없는 옛 서버)이거나 1000이면 띄우지 않는다.
- 산출량 줄은 서버가 값을 보내지 않아 클라 사본(`GetTraitEffectSum(YieldAdd)`)을 그대로 둔다.
- `GetStarSpeedAdd`·`GetBaseWorkSpeed`는 응축 화면·인벤·경매 툴팁이 계속 써서 남겼다.

## 후속 작업 / 주의사항
- 실측 남음 — 치트로 ×2 → 슬롯 속도·주기가 바로 바뀌는지. 확인되면 이슈 #60 닫고 T-055 완료 처리.
