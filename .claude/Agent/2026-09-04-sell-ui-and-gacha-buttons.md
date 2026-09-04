---
date: 2026-09-04
title: 인벤토리 판매 UI + 가챠 4버튼 · 캐릭터 보상 분기
tags: [client, ui, packet]
---

# 인벤토리 판매 UI + 가챠 4버튼 · 캐릭터 보상 분기

## 목적 / 배경

서버가 앞서 나가 클라가 **받아 놓고 안 쓴 패킷**이 쌓여 있었다 —
판매(`C_ItemSellRequest`/`S_ItemSellResponse`)는 화면도 핸들러도 없었고,
`GachaInfoTable`에 풀이 둘인데 `GachaPresenter`는 `gachaId = 1` 고정이라 캐릭터 풀을 뽑을 길이 없었다.

→ 범위·결정은 `tasks/T-032` · `tasks/T-031`, 동선은
[`Storage 규칙.md`](../../Assets/Scripts_Client/UI/Storage/Storage%20규칙.md)에 있다. 여기 다시 쓰지 않는다.

## 주요 결정 / 근거

- **판매 대상을 자원 탭으로 좁혔다.** `C_ItemSellRequest`가 `ItemInfo { ItemId, Count }` —
  **아이템 TID 축**이라 개체 PK(`long CharacterId`)로 식별되는 캐릭터를 담을 수 없다.
  "캐릭터도 담기게 해 두고 판매만 자원으로" 하는 안은 버렸다 — 담기는데 못 파는 UI가 되고,
  서버 패킷이 정해지면 카트 구조를 다시 만들게 된다.
- **카트를 `Managers/SellCartModel`(MonoService)에 뒀다.** 격자(담김 표시)와 정보 칸(목록·합계)이
  같은 것을 봐야 하는데, 둘 중 하나가 들고 있으면 패널 간 직접 참조가 생긴다.
  ⚠️ **수량 팝업은 이 규칙에 걸리지 않는다** — 격자가 띄우고 격자만 쓰는 종속 팝업이라
  인스펙터 직접 참조로 뒀다. 둘을 같은 규칙으로 묶으면 매니저가 UI 흐름까지 알게 된다.
- **확인 모달을 두지 않았다.** 담기 → [판매]가 이미 2단계다. Rare 이상은 목록에서 빼지 않고
  경고만 띄운다 — 빼면 "왜 안 담기지"가 되고, 파는 자유는 남긴다.
- 🔴 **수량 팝업의 자리를 틀렸다 — 하루 뒤(2026-09-05) 옮겼다.** 아래 "정정" 참조.

## 🔴 정정 (2026-09-05) — 팝업의 자리는 "누가 켜 주나"로 정하지 않는다

**판별 축이 둘인데 하나만 물었다.**

| 축 | 무엇을 정하나 | 내가 한 것 |
|---|---|---|
| ① **차단 범위** — 화면 전체를 막아야 하나 | **어느 캔버스에 사는가** | **묻지 않았다** |
| ② **생명주기** — 다시 켜 줄 손이 밖에 있나 | `SetActive` / `CanvasGroup` | 물었고, 답도 맞았다 |

②만 보고 "격자가 켜 주니 `SetActive`" → `#Storage Canvas` 자식으로 뒀다. 그런데 열 캔버스
(`#Widget`·`#Storage`·`#Market`·`#Main`·`#State`)는 **Sorting Order가 전부 0인 형제**라,
열 안에 깐 전체화면 차단막은 **자기 열만 막는다.** 확인을 누르기 전인데 상태바·메인·거래
버튼이 그대로 눌렸다.

⚠️ **증상이 원인을 가린다.** "클릭이 통과한다"를 보면 알파·`raycastTarget`·`blocksRaycasts`를
차례로 의심하게 되는데 전부 정상이었다. 원인은 **캔버스 order**고, 열 안에서는 어떤 값으로도
못 고친다. (게다가 그 차단막 오브젝트에는 `Image`가 아예 없어 **자기 열조차 안 막고 있었다** —
두 결함이 겹쳐 있어서 하나를 고쳐도 증상이 그대로 남았을 자리다.)

→ `!System Canvas`(order 2)로 옮기고 `AmountInputPresenter`로 개명했다(판매 전용이 아니다).
자리를 옮기니 ②의 답도 `CanvasGroup`으로 바뀌고 — 상주 캔버스에서 자기를 끄면 `Start`가
안 돈다 — **차단막 겹이 하나 사라졌다.** 3겹이던 것이 오버레이 셋과 같은 2겹이 된다.

- ⚠️ **`MonoService` 승격을 검토했다가 접었다.** `!System Canvas`의 오버레이는 **밖에서 아무도
  참조하지 않는다** — 전부 매니저 이벤트를 구독해 스스로 뜬다. 이 캔버스의 규약은 "Presenter를
  서비스로 올린다"가 아니라 **"매니저가 쏘고 Presenter가 구독한다"**이고, `Managers/`는
  `MonoService` 상속이 필수인 자리라 UI Presenter를 올리면 규칙이 갈라진다.
  다만 수량 팝업만 **답을 돌려줘야 해서**(`onConfirm`) 구독형이 안 된다 →
  `UIManager.AskAmount`가 중개한다. 캔버스를 넘는 인스펙터 참조를 한 곳에 모으는 것이 목적이다.
- ⚠️ **상주로 옮기면 `OnDisable`이라는 손을 잃는다.** 창고 열이 닫힐 때 팝업을 닫던 경로가
  사라지므로 `UIManager.CloseAllExceptWidget()`이 `Close()`를 부른다. 빠뜨리면 증상이
  "가끔 아무것도 없는 바탕에 팝업만 떠 있다"로만 보인다.

## 지뢰 — 다음 사람이 다시 밟을 것들

- ⚠️ **담아 둔 사이 인벤토리가 줄면 판매가 통째로 막힌다.** 서버 판매는 **전부 되거나 전혀 안 된다** —
  목록 중 한 종류만 모자라도 `NotEnoughItem`으로 전량 거절된다. 방치형이라 담아 둔 동안에도
  채취·판매로 인벤이 계속 바뀌므로 `SellCartModel`이 `InventoryChanged`를 구독해 초과분을 깎는다.
  **이 구독을 떼면 증상이 "가끔 판매가 안 된다"로만 보인다** — 카트를 의심하기 어렵다.
- ⚠️ **`C_ItemSellRequest.Count`는 클라가 값을 넣는 유일한 델타 필드다.** 받는 쪽 `Count`는 전부
  총량이라 같은 감각으로 보유량을 실으면 **전부 팔린다.** `서버 동작 이해.md` 표에 줄을 넣어 뒀다.
- 🔴 **`GachaResultPresenter`가 `reward.ItemId`를 무조건 읽고 있었다.** 캐릭터 보상은 `ItemId = 0`이라
  **빈 칸이 그려지는데, 필드가 추가만 된 형태라 컴파일도 경고도 통과한다.**
  자원 풀만 뽑던 동안엔 드러나지 않던 잠복 결함이고, **캐릭터 버튼을 붙이는 순간 터질 자리였다.**
  → `RewardType`으로 갈라 `ItemId` / `CharacterTid`를 읽는다. 캐릭터 이름은 **종류(TID)** 로 읽는다.
- ⚠️ **`InventorySlotView.Bind(int, int, GlobalRarity)` 래퍼를 걷어냈다.** 아이템 전용이라
  보상이 둘로 갈리면서 성립하지 않게 됐다. `Bind(in StorageSlotData)` 하나만 남는다.
- ⚠️ **`InventorySlotView` 프리팹은 창고와 가챠 결과가 공유한다.** 우클릭·`Sell Mark`를 더했지만
  가챠 결과는 `RightClicked`를 구독하지 않고 `SetSellMark`도 부르지 않아 영향이 없다.
  **여기 참조를 더할 때는 양쪽 화면을 함께 본다.**
- ⚠️ **격자의 카트 구독을 `OnEnable`에만 두면 첫 판을 놓친다.** 탭 줄의 `Start`가 격자보다 먼저 돌아
  `EnsureInitialized`를 부르는 경로가 있어서, 그때는 `OnEnable`이 이미 지나가 있다.
  → `EnsureInitialized` 끝과 `OnEnable` 양쪽에서 부르고 `_isCartSubscribed`로 중복을 막는다.
- ⚠️ **`GachaInfoTable`의 10연차 비용은 단차 × 10이 아니다.** 컬럼이 따로다(`CostSingle`·`CostMulti`).
  곱해서 만들면 기획이 할인율을 넣는 순간 화면과 서버가 다른 값을 말한다.

## 후속 작업 / 주의사항

- ⏳ **에디터 실측이 남았다.** 코드·씬 배선은 다 들어갔다 — 빠진 참조 0건으로 검증했고
  Unity 콘솔에 에러·경고가 없다. 배선 결과는 `tasks/T-032`의 "배선 결과" 절에 있다.
- ⚠️ **`Sell Mark`의 raycast target은 꺼 두었다.** 켜면 마크가 클릭을 먹어 **담긴 칸을 다시 못 뺀다** —
  토글이 한쪽으로만 도는 증상이 되고, 원인을 카트나 격자에서 찾게 된다.
- 🔴 **스크롤을 손으로 만들다 세 가지를 밟았다.** 전부 `Grid Presenter`를 그대로 베꼈으면 안 났다 —
  재발 방지로 [`UI 규칙.md`](../../Assets/Scripts_Client/UI/UI%20규칙.md) 3장에 표로 넣었다.
  [1] `Viewport` 마스크가 `Simple`이라 **목록이 거대한 둥근 사각형으로 잘렸다**(에러도 경고도 없다),
  [2] `ScrollRect`가 `Permanent`라 스크롤바가 뷰포트를 밀지 않아 틈이 남았다,
  [3] 줄 프리팹이 생성된 프레임에만 넘침 경고가 떴다 — `VerifyNoOverflow`는 `LateUpdate`,
  레이아웃 리빌드는 그 프레임 맨 끝이라서다.
  🔴 **여기서 한 번 잘못 고쳤다.** 프리팹 폭을 부모보다 작게(600→100) 저장해 피하려 했는데,
  **자식 TMP(기본 200×50)가 남아 경고가 한 단계 아래로 옮겨 갔을 뿐이었다.**
  크기를 맞추는 방식은 노드 수만큼 손이 가고 프리팹을 고칠 때마다 다시 깨진다 →
  `Refresh` 끝에서 `LayoutRebuilder.ForceRebuildLayoutImmediate(rowParent)`로 바꿨다.
  `InventorySlotView`가 이 경고를 안 내는 건 크기를 맞춰서가 아니라 **`Slot` 프레임이
  `LayoutGroup`이 아니라 앵커 배치**라 즉시 자리가 잡히기 때문이다.
- ⚠️ **`neodgm_pro SDF`는 Static 아틀라스다.** `⚠`를 문구에 넣었더니 `□`로 바뀌고 매 프레임
  경고가 떴다 — **씬 문구는 ASCII 기호만 쓴다.** `font.HasCharacter(c)`로 전수 검사할 수 있다.
- **`StorageInformationPresenter`는 이름과 역할이 어긋나 있다** — 판매 목록을 그리므로
  `SellCartPresenter`가 맞다. 씬 오브젝트 이름·문서가 함께 가는 개명이라 미뤘다.
- **좌클릭 상세 표시는 사라진 상태다.** 판매 목록이 그 자리를 차지했고, 되살릴지는 정하지 않았다.
