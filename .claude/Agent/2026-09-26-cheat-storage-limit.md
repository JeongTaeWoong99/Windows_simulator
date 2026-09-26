---
date: 2026-09-26
title: 치트 지급 3종에 창고 한도 검사 (#40)
tags: [server, test]
---

# 치트 지급 3종에 창고 한도 검사 (#40)

## 목적 / 배경
- `GiveItem`·`GiveCharacter`·`GiveEquip`이 `HasStorageFor`를 거치지 않아 200칸을 넘겨 지급됐다 → 클라 격자에 보이지 않는 개체. 이슈 #40 · T-085 원인 D의 치트 쪽

## 변경 내용
- `Server/WSGameServer/User/User.Cheat.cs` — 세 명령 모두 지급 직전 `HasStorageFor` → 넘치면 `StorageFull`
- `Server/WSGameServer.Tests/User/UserCheatTest.cs` — 거절 3건 + "가득 차도 이미 가진 자원 종류는 쌓인다" 1건
- `Server/docs/치트.md` — 거절 조건 표

## 주요 결정 / 근거
- **한도를 넘기는 우회 치트는 두지 않았다.** 넘긴 개체는 클라에 칸이 없어 보이지 않으므로 검증 용도로도 쓸모가 없다. 필요해지면 별도 명령으로 추가
- 검사는 TID·개수 검증 **뒤**에 둔다 — 잘못된 인자는 `InvalidCheatArgs`가 먼저 나가야 원인이 보인다

## 후속 작업 / 주의사항
- 채취 정산의 한도 처리는 기획 결정(T-085 7장 3안)을 기다린다 — 이번 범위 밖
