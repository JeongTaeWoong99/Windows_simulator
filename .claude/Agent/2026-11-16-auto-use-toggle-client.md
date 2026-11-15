---
date: 2026-11-16
title: 영약 자동 사용 ON/OFF — 클라 연동
tags: [client, ui, data]
---

# 영약 자동 사용 ON/OFF — 클라 연동 (T-194 · 이슈 #123 · PR #126)

## 목적 / 배경
- 서버 PR #126이 `C_SlotAutoUseRequest`를 `Count/Take` → `On`으로, `SlotAutoUseInfo.Count` → `On`으로 바꿨다.
  클라가 옛 계약을 써서 PR의 Unity 빌드가 깨져 있었다 — 클라 수정을 같은 브랜치에 얹어 함께 머지되게 한다.

## 변경 내용
- `SlotAutoUsePresenter` · `AutoUseCellView` — 넣기(수량 팝업)·꺼내기(우클릭) → 클릭 한 번으로 켜고 끄기. 칸 글씨 `ON ×보유` / `OFF`, 꺼지면 아이콘 흐리게
- `SlotBuffChipsView` · `SlotBuffChipView` — 켜진 영약은 남은 횟수 뒤에 작게 `+보유`. 켜졌는데 버프가 없으면(재고 소진) 흐린 칩 `0+0`으로 남긴다
- `Loc_UI.xlsx` — `AutoUseInSlot`·`AutoUseHint`·`WaitAutoUse` 문구 변경, `AutoUseOff` 추가(옛 `AskAutoPut` 줄 재사용), `AskAutoTake`·`AutoUseNoItem`·`UI.Result.AutoUseSlotFull`(서버 결번 1605) 삭제

## 주요 결정 / 근거
- 보유 0이어도 켤 수 있게 했다 — 서버가 ON을 유지하고 재획득하면 쓴다. 막으면 "미리 켜 두기"가 안 된다
- 프리팹은 건드리지 않았다 — 켜짐 표시는 기존 `Count Text`·아이콘 알파로만. 토글 그림이 필요하면 그때 프리팹에 더한다
- 컴파일 확인은 main 트리의 `Assembly-CSharp.csproj`를 워크트리 루트에 복사해 `dotnet build`로 했다(오류 0) — 워크트리엔 `Library`가 없어 Unity로 못 연다

## 후속 작업 / 주의사항
- 버프 칩의 `+보유`는 슬롯 동기화 때만 다시 그린다 — 인벤토리 변화(구매 등)만으로는 다음 동기화까지 옛 숫자. 툴팁은 띄울 때 읽어 항상 맞다
- `Count Text` 폭 40에 `ON ×12`가 들어가는지 실측 필요(자동 축소 여부)
- 실측: 토글 · 재고 소진/재획득 · 창고 소비 · 재접속 (이슈 #123 체크리스트)
