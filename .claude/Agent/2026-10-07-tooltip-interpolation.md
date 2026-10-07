---
date: 2026-10-07
title: 플레이어용 설명 Tooltip의 {컬럼} 보간 (T-093)
tags: [server, excelgenerator, data]
---

# 플레이어용 설명 Tooltip의 {컬럼} 보간 (T-093)

## 목적 / 배경
- 설명문에 숫자를 직접 적으면 실제 컬럼과 따로 놀다가 어긋난다 — 숫자는 컬럼 한 곳에만 두고 설명이 따라오게 한다.
- 지금까지 플레이어용 설명 컬럼이 아예 없었다. `Description`은 기획 메모이고, 클라가 일부러 읽지 않는다(`ResourceSlotSource.cs`).

## 주요 결정 / 근거
- **보간은 `Tooltip`이라는 이름의 컬럼에만 건다.** 모든 string 컬럼에 걸면 우편 본문 같은 데 들어간 `{`가 파이프라인을 멈춘다. 이름이 곧 옵트인이라 다른 테이블은 컬럼만 추가하면 된다.
- 컬럼명 `Tooltip`과 범위(`ItemTable`·`EquipTable`)는 사용자가 정했다.
- 지정자는 값 그대로와 `:permille` 두 가지뿐이다. 필요해지면 그때 추가한다.
- 엑셀 수식 · 런타임 보간 · 다른 시트 참조는 일감 문서의 "하지 않는 것" 그대로 따랐다.
- 치환은 `ReferenceValidator` 뒤, 코드 생성 앞에서 `TableData.Rows`의 `string[]`를 제자리에서 고친다 — 그래서 `.bytes`와 `DataLog` 둘 다 치환된 문장을 갖는다.
- 테스트는 `WSGameServer.Tests`에 넣지 않고 `Server/ExcelGenerator.Tests`를 새로 만들었다. Exe인 ExcelGenerator(ClosedXML·Roslyn)를 서버 테스트에 끌어들이지 않으려고.

## 후속 작업 / 주의사항
- `Tooltip` 값은 전부 비어 있다. 문구 채우기는 기획, 클라 툴팁 표시는 클라 몫이다.
- `ExcelGenerator.Tests`는 CI(`server-ci.yml`)에 없다. `AuctionServer.Tests`도 빠져 있어 같은 상태로 뒀다.
- ⚠️ 워크트리에서 `dotnet test`를 돌리면 `Assets/Plugins/Analyzers/MikaSourceGen.dll`이 다시 써진다. `git add -A`로 커밋하면 main의 미커밋 DLL과 겹쳐 `merge --ff-only`가 거절된다. 이번에 그래서 되돌리는 커밋(`a76e04d`)을 하나 더 했다.
- 첫 파이프라인 실행에서 Unity 쪽 `ItemTable.bytes` 사본이 원본과 달랐고, 다시 돌리자 맞았다. 원인은 못 찾았다. 미러가 어긋나 보이면 한 번 더 돌린다.
