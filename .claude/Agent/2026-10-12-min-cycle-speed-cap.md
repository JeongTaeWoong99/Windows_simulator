---
date: 2026-10-12
title: 판정 최소 주기 — 서버 속도 상한 (T-102)
tags: [server, test]
---

# 판정 최소 주기 — 서버 속도 상한 (T-102)

## 목적 / 배경
- 클라는 이미 `MinCycleMs`(2100)로 주기를 잘랐지만 서버 판정은 원래 속도로 나가 치트 배속에서 진행 바와 판정이 어긋났다 → tasks/T-102 · 이슈 #63

## 변경 내용
- `WorkStationSlot` — 자르기 전 속도 `_uncappedSpeed` + `SpeedCap`(= `JudgeCostUnits ÷ MinCycleMs` 버림), `CurrentWorkSpeed`는 둘의 최소인 계산 속성
- `ApplyWorkSpeed(int)`는 **확정 속도(잘린 값)** 가 바뀌었을 때만 true
- 테스트 `WorkStation/WorkStationSpeedCapTest` · `UserCheatGatherSpeedTest`에 ×100 → 1분 28판정

## 주요 결정 / 근거
- 잘린 값을 저장하지 않는다 — `Assign`이 판정 비용(레벨)을 바꿀 때 상한이 따라가야 한다. 잘린 값을 들면 Lv1→Lv2로 옮겨도 14285에 묶이고, `ApplyWorkSpeed`가 같은 원래 속도를 받으면 "안 바뀜"으로 끝난다
- 내역(`WorkSpeedBreakdown`)은 자르지 않는다 — 클라가 "상한에 걸렸다"를 그리는 근거
- 상한 버림 때문에 실제 간격은 2.1초가 아니라 2,101ms(Lv1) — 클라도 같은 식이라 맞는다

## 후속 작업 / 주의사항
- 실측(태웅) — 치트 ×2·×10에서 공격이 끊기지 않는지
- `CurrentWorkSpeed`가 내역 합과 같다는 불변식(`UserWorkSpeedBreakdownTest`)은 상한 아래에서만 성립한다
