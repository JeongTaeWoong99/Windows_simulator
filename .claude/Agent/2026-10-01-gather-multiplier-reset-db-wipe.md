---
date: 2026-10-01
title: 채취 전역 배수 1.0 복귀 · DB 데이터 비움 (T-004)
tags: [server, config, db]
---

# 채취 전역 배수 1.0 복귀 · DB 데이터 비움

## 목적 / 배경
- 사용자 지시 — 확인용 6.0배를 걷고(T-004 배포 전 게이트) DB를 새로 시작한다.

## 변경 내용
- `Global.GatherSpeedMultiplier` 6.0 → 1.0. 6.0 전제였던 `UserAptitudeTest` 기대값(7500 → 1250), workslot 3장·`채취-정산.md`·`세션-감시.md` 경고 문구 정리.
- `game.sqlite3` 전 테이블 `DELETE` + `sqlite_sequence` 초기화 + `VACUUM`. 스키마는 그대로.
- 로컬 `AuctionServer/auction.sqlite3`(git 밖)는 확인해 보니 이미 비어 있었다.
- T-004 Closed → `tasks/archive/`.

## 주요 결정 / 근거
- `WorkSpeedTest`의 `.Multiply(6.0)`은 승산 계산 검증용 리터럴이라 그대로 두고 주석만 "전역 배수"에서 "승산"으로 바꿨다.

## 후속 작업 / 주의사항
- 시작 로그(`INF 채취 전역 배수 1.0배`)는 코드 경로로만 확인했다 — 서버를 띄워 보지 않았다.
- 클라의 배수 안내 문구(T-055)는 서버 값이 1.0이면 저절로 사라진다.
