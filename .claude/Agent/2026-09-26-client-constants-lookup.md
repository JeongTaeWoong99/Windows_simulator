---
date: 2026-09-26
title: 클라 이중 상수를 Constants 조회로 교체 (T-085 · #34)
tags: [client, data, editor]
---

# 클라 이중 상수를 Constants 조회로 교체

## 목적 / 배경
- 서버가 `Constants.xlsx` → `GameData.Constants`를 만들어(#34, 685c19d) 클라 사본을 걷어 냈다. → tasks/T-085 7장

## 변경 내용
- `Editor/cheat-console/CheatWindow.cs` — 치트 상한 10·100 → `Constants.CheatMax*`
- `UI/Market/GachaPresenter/GachaPresenter.cs` — 뽑기 1·10 → `Constants.GachaDraw*`
- `UI/Storage/StorageGridPresenter/StorageGridPresenter.cs` — `ServerStorageCapacity` 사본 삭제, 프레임 대조는 `Constants.StorageCapacity`로

## 주요 결정 / 근거
- 치트창 상한은 `GameDataLoader.IsLoaded`일 때만 읽고 아니면 1 — 에디터 창은 Play 전에도 그려지고, 그때 `GameTable.ConstantsTable`이 null이라 바로 읽으면 예외가 난다.
- 런타임 쪽(Gacha·Storage)은 가드 없음 — `GameDataLoader`가 `BeforeSceneLoad`에서 적재한다.

## 후속 작업 / 주의사항
- `WorkStationProgress`(스케일 1000)·`DefaultIndustryLevel`은 서버 결정 대기 — #34 코멘트로 추가 시트 후보를 요청했다.
- 창고 프레임은 씬 오브젝트라 `StorageCapacity`를 바꾸면 프레임 수도 손으로 맞춰야 한다(불일치 시 로그).
