---
date: 2026-09-26
title: 공용 상수 시트 Constants.xlsx — 행마다 속성 하나 (T-085)
tags: [server, data, docs]
---

# 공용 상수 시트 Constants.xlsx (T-085)

## 목적 / 배경
- 클라·서버가 같은 숫자를 각자 코드에 들고 있던 문제(T-085 5-1·5-2)의 서버 몫. 값의 원본을 엑셀 한 곳으로 모았다.

## 주요 결정 / 근거 (사용자 확정 2026-09-26)
- **기존 시트처럼 세로형**(한 줄 = 값 하나)이되, 생성기가 **행마다 `public static long Xxx` 속성**을 가진 `GameData.Constants`를 만든다 — `Constants.StorageCapacity`. 문자열 키 조회는 오타가 런타임까지 숨는다.
- **이름이 곧 키**(TID 없음) — TID와 이름이 둘 다 있으면 항상 1:1인 컬럼이 둘이 된다.
- **값은 전부 `long`**(소수는 천분율 정수 + 이름 끝 `Permille`). 타입 컬럼을 두는 안은 사용자가 기각했다.
- 서버 기존 이름(`User.StorageCapacity`, `AuctionRules.*` 등)은 남기고 **값만 `Constants`에서 읽게** 했다 — 사용처 수정을 최소화.

## 후속 작업 / 주의사항
- ⚠️ `const` → 정적 속성으로 바뀌었다. **`GameTable.LoadAll` 전에 읽으면 터진다.** 테스트는 `TestAssemblySetup`(ModuleInitializer)이 한 번 적재한다 — 이게 없으면 병렬 순서에 따라 붙었다 떨어진다(실제로 `AuctionRulesTest`가 그랬다).
- 서버 기동 때 `ConstantsCheck.EnsureAll()`이 모든 상수를 읽어 본다 — 코드와 `.bytes` 판 불일치 차단.
- Unity 새 파일 3개(`Constants.cs`·`ConstantsTable.cs`·`ConstantsTable.bytes`)의 `.meta`는 에디터가 꺼져 있어 손으로 만들었다(GUID 새로 발급).
- 남은 것: 기준 주기 30초·속도 스케일 1000(단위 약속 → 코드 공용 상수 후보), 전역 배수 6.0(패킷 후보, T-055) — T-085 7장. 클라는 `CheatWindow`·`GachaPresenter` 등을 `Constants`로 바꾸면 된다.
