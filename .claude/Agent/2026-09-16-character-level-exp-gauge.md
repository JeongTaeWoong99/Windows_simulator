---
date: 2026-09-16
title: 창고 캐릭터 칸 — 레벨 표시 · 경험치 세로 게이지 (T-052)
tags: [client, ui, editor]
---

# 창고 캐릭터 칸 — 레벨 표시 · 경험치 세로 게이지 (T-052)

## 목적 / 배경
- 서버가 정산마다 `S_CharacterSyncResponse`로 캐릭터 레벨·경험치를 밀어 주는데(`87d0243`), 클라는 받지도 그리지도 않았다 (이슈 #25).

## 변경 내용
- `Managers/PlayerDataModel.cs` — `CharacterSynced` 구독, 개체 교체 후 `CharactersChanged`. `GetExpProgress(characterId)` 추가. 레벨업 감지 지점에 🎨 TODO + Info 로그
- `Data/GameDataLoader.cs` — `TryGetRequiredExp(level)`. 행 없음 = 만렙이라 경고하지 않는다
- `UI/Storage/StorageGridPresenter/CharacterSlotSource.cs` — 이름 줄을 `LV.N 이름` / `MAX 이름`으로
- `UI/Shared/SlotView.cs` + `Assets/Prefabs/SlotView.prefab` — `Exp Gauge`(Slider, 왼쪽 벽 x=0 · 12×60 · BottomToTop) 추가, `SetExpGauge(float?)`
- `StorageGridPresenter.Redraw` — 캐릭터 탭에서만 게이지 값을 넘긴다
- `Storage 규칙.md` — "레벨 · 경험치 게이지" 절

## 주요 결정 / 근거
- **경험치 진행은 호버(T-050)가 아니라 칸 안 게이지** — 사용자 결정. T-050을 기다리지 않게 됐다
- **레벨은 별도 위젯 없이 이름 줄에** — 사용자가 1텍스트/2텍스트 둘 다 허용. 공급자가 짓는 완성값이라 자원·가챠 칸에 영향이 없는 1텍스트를 택했다
- 게이지 값은 `SlotData`에 넣지 않고 격자가 읽어 넘긴다 — 적성 스트립과 같은 이유(자원·가챠 칸까지 두꺼워진다)
- 레벨업 이벤트는 아직 열지 않았다 — 구독자가 없어서. TODO 자리에 시그니처 안만 적었다

## 후속 작업 / 주의사항
- **플레이 확인 안 했다** — 게이지 색(하늘색)·폭(12)은 임시값. 이름 줄이 `LV.10 긴이름`에서 얼마나 줄어드는지 확인 필요
- 게이지 이미지들은 raycast를 꺼 뒀다 — 켜면 우클릭이 칸에 닿지 않을 수 있다
- 프리팹은 Unity MCP 스크립트로 수정했다(`PrefabUtility.LoadPrefabContents`). 씬은 이 작업으로 바뀌지 않았다

## 업데이트 (2026-09-16) — 레벨을 배지로 · 칸이 프레임을 채움

- **레벨을 이름 줄에서 떼어 아이콘 왼쪽 위 배지(`Level Badge` > `Level Text`)로** — `LV.19 폭스파스크`가 이름 텍스트 자동 크기(최소 1) 때문에 크게 줄었다. 사용자가 C안(모서리 배지) 선택. 문구는 격자 `ReadLevelLabel`이 짓는다(`19`/`MAX`)
- **칸이 프레임을 꽉 채운다** — 빈틈 원인은 5px 여백 규칙이 아니라 프리팹 100×100 고정 vs `FlexibleGridLayoutGroup` 셀 115px. `SnapToFrame`이 앵커를 펴 크기까지 맞춘다
  - 프리팹 루트를 스트레치로 바꾸지 않았다 — 가챠 결과 팝업이 같은 프리팹을 레이아웃에 넣어 쓴다
  - 그래서 `Exp Gauge`를 고정 60 높이에서 위아래 20px 안쪽 스트레치로 바꿨다(칸이 커지면 아이콘과 어긋났을 것)
  - 사용자 요청으로 `UI 규칙.md` 여백 표 아래에 "격자 프레임 안의 칸은 여백 0 — 특수한 경우 괜찮다" 예외를 기록, 상세는 `Storage 규칙.md`
- 🎨 초상화가 들어오면 배지가 모서리를 가린다 — 그때 자리 재검토

## 업데이트 (2026-09-16) — 배지를 게이지 오른쪽 아래로 · `LV.` 복원

- 아이콘 왼쪽 위 모서리 배지는 **초상화 형태(얼굴·전신…)가 제각각이라 숫자만 붕 뜬다**는 사용자 판단으로 게이지 바닥 오른쪽(12, 20 · 36×14)에 붙였다
- 숫자만(`2`)이면 레벨로 안 읽혀 `LV.19` · `LV.MAX`로 되돌렸다. 이름 줄과 분리돼 있어 글자 축소 문제는 재발하지 않는다
