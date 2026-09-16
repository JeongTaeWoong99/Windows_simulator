---
date: 2026-09-16
title: 해금 시스템 서버 구현 — UnlockCatalog · t_user_unlock · 해금 패킷, 첫 적용은 작업슬롯 (T-038)
tags: [server, protocol, data, test]
---

# 해금 시스템 서버 구현 — 첫 적용은 작업슬롯 (T-038)

## 목적 / 배경

- 기획([`unlock/README.md`](../../GameDesign/design/unlock/README.md))·데이터(`Unlock.xlsx`)는 09-14·09-16에 끝났고
  서버가 없었다. 슬롯은 `DefaultSlotCount = 1`로 열리고 잠긴 칸을 표현할 길이 없었다.
- 무엇을 했는지는 커밋 `39ca310`과 보관된 [`T-038`](../../tasks/archive/T-038-슬롯해금서버.md)이 말한다. 여기는 결정·지뢰만.

## 주요 결정 / 근거

- **빈 열린 칸은 DB에 저장하지 않는다.** 예전 `EnsureDefaultSlots`는 기본 칸을 만들며 `t_user_workstation_slot`에 행을 썼다.
  이제 "열렸다"의 원본이 `t_user_unlock`이라 슬롯 행은 **배치가 생길 때만** 쓴다. 로그인은 `WorkSlotTable`을 돌며
  `IsUnlocked`인 칸을 메모리에서 만들 뿐이다. 잠긴 칸의 배치 행은 경고 후 무시(기획 #18).
- **`AccountLevel`은 `User.AccountLevel => 0` 자리만.** 검사 코드는 들어가 있고 값만 T-012 뒤에 바뀐다.
  프로퍼티 하나로 두어 "검사가 빠진 것"과 "값이 0인 것"을 코드에서 구분할 수 있게 했다.
- **지불 컬럼 검사는 `row.Gold > 0`일 때만.** `Gold = 0`이면 요청의 `Currency`를 무시하고 무료로 연다(기획 6장 —
  "지불 컬럼이 없는 해금은 Currency를 무시한다"). `Dia` 컬럼이 생기면 이 분기에 OR을 더한다 — `User.Unlock.cs` 주석 자리.
- **`ECurrencyType`을 프로토콜에 새로 만들었다.** `GameData.CurrencyType`을 클라에 직접 노출하지 않는 기존 관례(`EIndustryType`)를 따랐고
  `PacketEnumTest`가 1:1을 잠근다.
- **치트 `Unlock = 7`은 이미 열린 것에 `AlreadyUnlocked`를 돌려준다.** `GrantUnlock` 자체는 조용히 false지만,
  운영자가 "왜 아무 일도 안 나지"를 겪지 않게 치트 응답에서는 구분한다.
- **`UnlockCatalog` 검증에 "없는 선행 TID"·"없는 UnlockTID를 참조하는 칸"도 넣었다.** 파이프라인 `Ref?`가 이미 잡지만
  카탈로그가 자기 표를 믿지 않는 편이 싸다 — 테스트에서 가짜 행을 넣을 때도 같은 규칙이 적용된다.
- **테스트 빌더는 `Unlocks`가 비어 있으면 실데이터를 적재한다.** 빈 카탈로그면 `WorkSlots`가 0개라 로그인 경로의 슬롯이
  전부 사라져 기존 테스트(`저장된_슬롯_레벨과_해금_레벨은_로그인_때_되살아난다`)가 깨진다. 기대값을 고정할 테스트는 먼저 `Load`한다.

## 검증 — Unity 에디터를 unity CLI로 몰아서 실서버 왕복

- `unity command eval_file --file <cs>`로 Play 모드 클라의 `NetworkManager.Instance.Send(...)`를 직접 불렀다.
  로그인 → 해금 10건(선행·골드·재화·중복·없는 TID·치트·잠긴 칸 배치) → Play 정지·재시작 → 재로그인.
  결과는 `unity command console`(ClientLogger 수신 로그)과 서버 로그·`sqlite3`로 대조했다. 전부 기대대로.
- 테스트 계정(`unlock-test`, user_id 7)은 검증 뒤 DB에서 지웠다 — 커밋된 `game.sqlite3`의 diff는 DDL뿐이다.

## 함정

- **서버 콘솔 프로그램을 백그라운드로 띄우면 `Console.ReadLine()`이 즉시 EOF를 받아 종료된다.** `sleep 7200 | WSGameServer.exe`로 stdin을 붙들었다.
- `unity command eval`의 인자는 위치 인자가 아니라 **`--code`/`--file`** 이다. 위치로 넘기면 `--timeout` 파싱 오류가 난다.
  따옴표·한글이 섞이면 `eval_file`이 안전하다.
- `unity` CLI는 PATH에 없다 — `%LOCALAPPDATA%\Unity\bin\unity.exe`. 에디터 연결은 `com.unity.pipeline`(manifest에 이미 있음).
- `check-doc-graph.ps1`의 갱신일 역전 경고 5건(unlock 09-16 > gathering·trait·character 등)은 09-16 재검토 때부터 있던 것이다. 이번 건은 아니다.

## 후속 작업 / 주의사항

- 클라 잠긴 칸 UI → [T-039](../../tasks/T-039-클라슬롯해금UI.md) (⏸ → ▶ 올렸다).
- 계정 레벨이 생기면 `User.AccountLevel`만 바꾸면 검사가 산다 (T-012). 산업 레벨 이관은 T-021.
- `Packages/manifest.json`·`packages-lock.json`의 `com.unity.pipeline` 추가는 사용자 작업 트리에 있던 것이라 커밋하지 않았다.
