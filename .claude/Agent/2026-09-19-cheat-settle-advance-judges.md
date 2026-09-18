---
date: 2026-09-19
title: 정산 치트가 판정을 N회 앞당기게 (T-060)
tags: [server, cheat, workstation]
---

# 정산 치트가 판정을 N회 앞당기게 (T-060)

## 목적 / 배경
- 정산 치트(`ECheatCommand.Settle`)가 매번 0개였다. 쌓인 진행도만 정산했는데, `GatheringScheduler`가 0.1초마다 같은 `SettleWorkStation`을 먼저 불러 남는 게 없었다.

## 변경 내용
- `WorkStationSlot.AdvanceJudges(count)` — 판정 count회분 작업량을 `ProgressUnits`에 얹는다. 비활성 슬롯은 건너뛴다.
- `WorkStationSlot.ConsumeJudgeCount` — 경과 0ms여도 판정을 꺼내게 했다(전에는 조기 return해 앞당긴 작업량이 안 나갔다).
- `User.CheatSettle(Arg1, now)` — Arg1 = 판정 횟수(0 이하 → 1, 상한 `CheatMaxSettleJudges = 100`). 가동 슬롯에 얹은 뒤 `SettleWorkStation`.
- `MikaProtocol/PacketEnum.cs` 주석 · 미러 동기화, `Server/docs/치트.md` 2장, `tasks/T-060` 체크.
- 테스트 `UserCheatTest` — Settle 5건(앞당김 · 경과+앞당김 · 기본 1회 · 빈 슬롯 · 상한).

## 주요 결정 / 근거
- 시계(`LastTickAt`)를 되돌리는 대신 작업량을 얹었다 — 속도·레벨별 판정 비용과 무관하게 정확히 N회가 된다.
- Arg1 0 → 1회: 클라 치트 창 버튼이 인자 없이 보내므로 클라 수정 없이 동작한다.

## 후속
- `권한이_없으면_NoPermission으로_거절하고_아무것도_바꾸지_않는다` 실패 — 사용자가 개발용으로 `CheatAdminLevel = 0`으로 바꾼 결과(미커밋 변경). 처리 방침은 사용자 결정.
- 클라 치트 창에 판정 횟수 입력칸을 붙이면 Arg1로 보내면 된다(클라 담당).

## 업데이트 (2026-09-19)
- 권한 테스트는 `Skip` 처리 — `CheatAdminLevel`을 1로 되돌릴 때 Skip을 뗀다.
