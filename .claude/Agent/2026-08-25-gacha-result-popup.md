---
date: 2026-08-25
title: 가챠 결과 팝업(GachaResultPresenter) 신설 · InventorySlotView 등급 배경 확장
tags: [client, ui, editor]
---

# 가챠 결과 팝업 신설

## 목적 / 배경

`Log/PlayerDataLogger.cs`(임시 발판)를 지우려면 그것이 대신하던 두 화면이 먼저 있어야 한다 —
가챠 결과 표시와 위젯 수확 표시. 이번엔 **앞의 하나**만 만들었다.
그전까지 `GachaPresenter.OnGachaCompleted`는 대기만 닫고 보상 목록을 버렸다.

→ 일감 상태·남은 항목은 `tasks/T-031-클라가챠화면.md`

## 변경 내용

- `UI/System/GachaResultPresenter/GachaResultPresenter.cs` (신규)
- `UI/System/RarityPalette.cs` (신규) — 등급 → 표시색
- `Data/GameDataLoader.cs` — `GetItemRarity` 추가
- `UI/Storage/InventoryPresenter/InventorySlotView.cs` — 참조 2개 추가, `Bind`에 등급 인자
- `UI/Market/GachaPresenter/GachaPresenter.cs` — 주석만(로직 불변)
- 씬 `Assets/Scenes/Original/DesktopWindow_Control.unity` · 프리팹 `Assets/Prefabs/InventorySlotView.prefab`
- 문서 5종 + `tasks/`

## 주요 결정 / 근거

- **왜 `#Market Canvas`가 아니라 `!System Canvas`인가** — 사용자가 요청 직후 거래 열을 닫으면
  마켓 자식으로 둔 팝업은 통째로 사라진다. 차단(`blocksRaycasts`)으로 막을 수 있는 종류가
  아니다(열을 닫는 건 정당한 조작이다). 근거 서술은 `UI/System/System 규칙.md` 마지막 절.
  → **문서에 "Gacha Presenter의 자식으로 붙는다"고 적혀 있던 기존 안을 뒤집은 것이다.**
- **`SetActive`가 아니라 `CanvasGroup`** — 자기 이벤트로만 뜨는 상주 오버레이라,
  자기를 끄면 다시 켤 이벤트를 못 받아 영구 잠김이 된다(`LoadingPresenter`·`NoticePresenter`와 동류).
- **칸은 `InventorySlotView` 프리팹을 공유한다**(복제 안 함) — 같아야 할 생김새를 두 벌 두면
  한쪽만 고쳐진다. 대신 View가 창고 사정을 알면 안 되므로 **등급은 완성된 값을 인자로 받는다**.
- **등급 출처가 둘이다** — 창고는 테이블(`GameDataLoader.GetItemRarity`), 가챠는 패킷
  (`GachaRewardInfo.Rarity`). 그래서 `RarityPalette.Get(EGlobalRarity)` 오버로드 대신
  `ToTableRarity(EGlobalRarity)` 한 줄로 캐스팅을 모았다 — `Bind`가 테이블 enum을 받기 때문에
  오버로드를 뒀다면 죽은 코드가 됐다.
- **`RarityPalette`를 계획의 `Data/`가 아니라 `UI/System/`에 뒀다** — 같은 성격(캔버스를 가로지르는
  표시 변환표)인 `ResultMessages.cs`가 이미 거기 있다.
- 셀은 파괴하지 않고 `List`로 풀링한다. 넘치는 칸은 `Clear()` + 비활성.

## 후속 작업 / 주의사항

- ⚠️ **`Rewards`는 이번에 얻은 델타(연출용)다.** 인벤 수량은 `ItemChangeInfos`(누적 총량)로
  이미 반영돼 있다 — 결과 팝업에서 이 값을 인벤토리에 **더하면 수량이 두 배가 된다.**
  지금 팝업은 표시만 한다. 손댈 때 이 선을 넘지 말 것.
- ⚠️ `InventorySlotView` 프리팹은 이제 **두 화면이 공유한다.** 참조·레이아웃을 고치면
  창고와 가챠가 함께 바뀐다.
- ⚠️ `!System Canvas`의 자식 순서에서 **`Notice Presenter`는 항상 마지막**이어야 한다
  (형제 순서가 곧 앞뒤). 오버레이를 새로 넣으면 그 앞에 끼운다.
- **문서의 Sorting Order가 틀려 있었다** — `100·200·300`이라 적혀 있었으나 실제는
  `!System = 2` · `!Login = 1` · 나머지 `0`. 실제 값으로 정정했다.
- TODO 남김 — 등급 스프라이트(`RarityPalette`), 아이템 아이콘(`itemImage`는 참조만 잡고 비움).
  둘 다 에셋이 없어서다.
- 실행 검증은 서버가 떠 있어야 한다(미실시): 인벤토리 회귀 · 1회/10연차 · 중복 수량 · 풀 재사용 ·
  마켓 열 닫고 뽑기.
