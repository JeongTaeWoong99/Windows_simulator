---
date: 2026-10-28
title: 유물은 1개씩 감정 — 서버 수량 제한 (T-186 · #88)
tags: [server, gacha, relic, design]
---

# 유물은 1개씩 감정

## 목적 / 배경
- #88(태웅 제안)을 진우가 수락 — 유물은 1개씩 감정 · 결과 목록 없음. 낮은 확률의 아티팩트가 50개 목록에 묻히지 않게 한다.

## 변경 내용
- `Server/WSGameServer/Gacha/GachaService.cs` — `RelicAppraiseCount = 1` · `OpenBox`에서 `ItemType.Relic`이면 수량 1이 아니면 `InvalidUseCount`(아무것도 바뀌지 않음). 상자는 그대로 `BoxOpenMax`
- `RelicAppraiseTest` — 새 테스트 1개 · 유물 3개/2개를 감정하던 테스트 3개를 1개 감정으로 (아티팩트 2개 쌓임은 엔트리 Count 2로 유지)
- `유물감정.md` 2장 · 2.2 · 구현 표
- `tasks/T-186` 신설 · `T-151` ⏸ #88 줄을 클라 할 일로

## 주요 결정 / 근거
- 1은 밸런스 수치가 아니라 규칙이라 `Constants.xlsx`에 두지 않고 코드 상수로 뒀다
- 판정 기준은 `ItemType.Relic` — `OpenCostGold > 0`이 아니다(비용과 수량 규칙을 묶지 않는다)

## 후속
- 클라: 수량 팝업 제거 · 결과 목록 생략 (태웅 · #88) — 머지 전까지 클라에서 2개 이상 감정하면 `InvalidUseCount`
- #88의 아티팩트 확률(약 5%)·`GiveArtifact` 치트는 결정하지 않았다
