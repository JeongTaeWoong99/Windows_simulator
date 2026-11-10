---
date: 2026-11-11
title: 아티팩트 지급 치트 GiveArtifact = 19를 추가한다
tags: [server, cheat, artifact, T-198]
---

# 아티팩트 지급 치트 (T-198 · 이슈 #112)

## 목적 / 배경
- 클라(태웅)가 아티팩트 화면 테스트용 치트를 요청했다(#112 · 클라 T-193). 감정이 유일한 경로이고 확률이 약 5%뿐이다.

## 변경 내용
- `Server/MikaProtocol/PacketEnum.cs` — `ECheatCommand.GiveArtifact = 19` (미러 `Assets/Scripts_Server/Protocol` 자동 복사)
- `Server/WSGameServer/User/User.Cheat.cs` — `CheatGiveArtifact`: 범위·TID 검사 → `HasArtifactRoomFor`면 `GainArtifacts`, 아니면 넘침 우편, 우편함도 가득이면 `StorageFull`
- `Server/WSGameServer.Tests/User/UserCheatGiveArtifactTest.cs` — 9건
- `Server/docs/치트.md` 2장 한 줄

## 주요 결정 / 근거
- 개수 상한은 `ArtifactMaxStack`(칸 하나, 지금 1000) — 0 하나 더 친 오타가 받을 수 없는 우편이 되지 않게.
- 칸이 모자라면 나누지 않고 통째로 우편 — 감정의 넘침 처리와 같다.
- 도감 장착은 하지 않는다 — 장착 흐름도 클라가 시험해야 한다.

## 후속
- 클라 치트 창 입력 칸(T-193, 태웅).
