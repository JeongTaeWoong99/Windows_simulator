---
date: 2026-09-14
title: 작업슬롯 세팅 패널에 배치된 캐릭터 이름·적성 표시
tags: [client, ui, editor]
---

# 작업슬롯 세팅 패널에 배치된 캐릭터 이름·적성 표시

## 목적 / 배경
- 3단계 `2 Character Setting Panel`에 해제 버튼만 있어 누가 일하는지 화면에 없었다 → `tasks/archive/T-035`

## 변경 내용
- `WorkStationSelectPresenter.cs` — `assignedInfoText` + `RefreshAssignedInfo()` (`Refresh` 세팅 분기)
- 씬 — 패널 VLG를 ctrl(W,H) on · expand off로, `Assigned Info Text (TMP)` · `Button Row > Unassign Button`
- `UI 배치 현황.md` 트리 갱신

## 주요 결정 / 근거
- 새 구독을 두지 않았다 — 산업 교체는 `WorkStationSlotsChanged`, 캐릭터 값(T-052 푸시 포함)은 `CharactersChanged`가 이미 `Refresh`를 부른다
- 글자색은 진회색(0.196) — 패널 바탕이 밝은 회색(0.8)이라 같은 화면 `Empty Text`의 흰색을 따르면 안 보인다

## 후속 작업 / 주의사항
- ⚠️ 원래 패널 VLG가 ctrl off · expand on이라 **형제를 하나 더하면 높이가 반씩 갈려 가운데로 뜬다.** 그래서 정석 배치로 바꿨다
- ⚠️ expand를 끄면 자식이 폭을 안 채운다 — 채울 자식에 `flexibleWidth 1`을 줘야 한다(처음에 빠뜨려 156px로 몰렸다)
- ⚠️ MCP로 **꺼진 Presenter를 잠깐 켜서 측정·저장하면** 에디터 상태(목록·선택 화면 동시 활성)로 계산된 `sizeDelta`·앵커가 기존 오브젝트에 저장된다. 원래 값으로 되돌린 뒤 커밋했다 — 다음에 측정할 때는 저장 전에 diff를 본다
- 목업 수준 개편(산업 아이콘·캐릭터 카드·장비·효율 계산)은 별도 일감으로 이어진다
