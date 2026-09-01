# Storage 폴더 규칙

> 최종 업데이트: 2026-09-02 (격자 하나 + 탭별 공급자로 전환 · 탭을 채우는 절차 신설) · 대상: `Assets/Scripts_Client/UI/Storage/`

**`#Storage Canvas` — 탭으로 내용을 갈아 끼우는 창고 화면.**

| 폴더 | 무엇 |
|------|------|
| `StorageCanvasView.cs` | 캔버스 껍데기 |
| `StorageTabPresenter/` | 탭 4개를 쥐고 **어느 탭인가**를 정한다 (+ `StorageTab` enum) |
| `StorageGridPresenter/` | **탭이 무엇이든 칸을 그리는 격자 하나** (+ 종속 View `InventorySlotView` · 공급자들) |
| `StorageInformationPresenter/` | 창고 정보 |

이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>), 스크롤·레이아웃 함정은
[`Layout 규칙.md`](<../Layout/Layout 규칙.md>)에 있다.

---

## 탭이 달라도 격자는 하나다

**탭마다 화면을 두지 않는다.** 자원·캐릭터·장비의 **격자·스크롤·레이아웃이 완전히 같기** 때문이다 —
같아야 할 것을 세 벌로 두면 한쪽만 고쳐지고, 칸 프레임 200개도 세 배가 된다.

탭마다 다른 것은 **데이터와 표시 문구뿐**이라 그쪽만 공급자로 갈라 두었다.

```
Grid Presenter  ─ 프레임 200칸 · 칸 풀 · "받은 목록을 앞 칸부터 그린다"
      ↑ ShowTab(tab)
Tab Presenter   ─ 탭 버튼 4개를 쥔다. 전환은 여기 한 곳
      │
      ├ Resource  → ResourceSlotSource  : PlayerDataModel.Inventory   [기본 탭]
      ├ Character → CharacterSlotSource : PlayerDataModel.Characters
      ├ Equipment → 없음 (버튼이 잠긴다)
      └ Trait     → 없음 (버튼이 잠긴다)
```

`StorageSlotSource`는 **"칸 N개, 각 칸에 무엇을 그리는가"만** 답한다.
격자는 그것이 자원인지 캐릭터인지 모른다.

### 세 순서를 나란히 맞춘다

**화면의 탭 순서 · `StorageTab` enum · 인스펙터 `Tabs` 배열이 모두 같다**
— 자원 · 캐릭터 · 장비 · 특성. `TabEntry`가 탭↔버튼을 짝짓기 때문에 **어긋나도 동작은 하지만**,
셋이 나란해야 코드만 읽어도 화면이 그려지고 배선을 눈으로 대조할 수 있다.

> ⚠️ **enum 값을 중간에 끼우거나 재정렬하면 씬 배선을 반드시 함께 고친다.**
> 씬에 int로 저장돼 있어 **코드만 바꾸면 같은 숫자가 다른 탭으로 읽힌다** —
> 컴파일도 경고도 통과하고 배선만 조용히 어긋난다
> (`MainScreen`에서 실제로 겪었다 → [`UIManager.cs`](../../Managers/UIManager.cs) 주석).
> **그냥 늘리기만 할 때는 끝에 붙이면** 씬을 안 건드려도 된다.

### 탭 하나를 채우는 절차

패킷·데이터가 도착해서 빈 탭을 채우게 되면 **이 네 단계가 전부다.**

1. `StorageSlotSource`를 상속한 `XxxSlotSource`를 `StorageGridPresenter/`에 만든다
   — `Fill`(무엇을 그릴지) · `OnSubscribe`/`OnUnsubscribe`(무엇이 바뀌면 다시 그릴지) 셋만 채운다
2. 그 공급자가 **무엇을 구독하는지** 정한다. 표시에 쓰는 값이 둘이면 **둘 다 구독한다** —
   `CharacterSlotSource`가 캐릭터 목록과 슬롯 변경을 함께 듣는 이유다(보조 문구가 배치 상태라서)
3. `StorageGridPresenter.EnsureInitialized`의 공급자 등록에 **한 줄** 더한다
4. **`StorageTabPresenter`는 고치지 않는다** — 공급자가 생기면 그 탭은 저절로 눌린다

> **④가 이 구조의 값이다.** 잠금 여부를 탭 줄에 적어 두지 않고 **격자에 공급자가 있는지**
> (`HasSource`)로 판정하기 때문이다. 두 곳에 적으면 공급자를 붙이고도 버튼이 잠긴 채 남는다.

### 아직 데이터가 없는 탭은 버튼을 잠근다

눌러도 아무 일이 없으면 **고장 난 버튼과 구분되지 않는다.** 잠긴 버튼은 유니티 기본
Disabled 색으로 흐려져 "지금은 없는 것"이 그대로 보인다.

**무엇을 기다리는지는 일감에 적혀 있다 → [`tasks/T-043`](../../../../tasks/T-043-창고장비특성탭.md).**
여기 옮겨 적지 않는다 — 선행은 풀리면 바뀌는 값이라 두 곳에 두면 한쪽만 낡는다.

> ⚠️ `tabs` 배열 위의 `[CenterHeader]`에는 `[NonReorderable]`을 같이 단다 —
> reorderable list 경로에서는 헤더가 통째로 안 보인다([`UI 규칙.md`](<../UI 규칙.md>)의 "공통 작성 규약").

## i번째 항목이 i번째 프레임에 들어간다

예전 `InventoryPresenter`는 `ItemId → 프레임`을 고정해 두고 빈 프레임을 앞에서부터 찾았다.
그러면 **아이템이 처음 들어온 순서로 칸이 영구히 고정돼 정렬을 넣을 자리가 없다.**

순서의 주인을 공급자로 옮겼기 때문에 — **정렬·걸러 내기는 공급자만 고치면 되고 격자는 그대로다**
(산업별 필터·정렬은 [`T-015`](../../../../tasks/T-015-산업아이템클라적용.md)).

- **칸 프레임은 씬에 미리 깔려 있다.** `Content` 아래 `Slot (1..200)`이 프레임이고,
  칸 프리팹은 그 프레임의 **자식**으로 들어간다. Content 직속으로 만들면 레이아웃이 무너진다.
  프레임은 코드가 만들지도 지우지도 않는다.
- **칸은 파괴하지 않고 풀로 되돌린다.** 남는 칸은 `Clear()` 후 꺼 두었다가 다음 탭에서 다시 쓴다 —
  탭을 오갈 때마다 200개를 만들고 부수면 상시 실행 앱에서 GC가 쌓인다.
- **칸이 부족한 탭에서도 프레임은 200개 그대로 둔다.** 켜고 끄면 탭 전환마다 레이아웃 리빌드가
  200번 돈다.

## 반복 칸은 종속 View로 뺀다

`InventorySlotView`가 그 예다. 같은 칸이 N개 복제돼 각자 다른 데이터에 묶이면 위젯을
Presenter가 다 들고 있을 수 없다. **매 프레임 도는 계산은 View가 아니라 Presenter·공급자가
하고, View에는 결과만 넘긴다** — 상시 실행 앱에서 비용이 칸 수만큼 곱해진다.

## ⚠️ `InventorySlotView`는 창고 전용이 아니다

폴더는 `StorageGridPresenter/` 아래지만, **프리팹(`Assets/Prefabs/InventorySlotView.prefab`)은
캔버스를 넘어 재사용된다** — `!System Canvas`의 [`GachaResultPresenter`](<../System/System 규칙.md>)가
가챠 결과 칸으로 같은 프리팹을 찍어 쓴다.

**같아야 할 생김새를 두 벌로 두면 한쪽만 고쳐진다.** 그래서 복제하지 않고 공유한다.

고칠 때 지켜야 할 것 —

- **칸은 완성된 값을 받아 그린다.** 이름도 등급도 칸이 조회하지 않는다 — **출처가 탭마다 다르다.**
  자원은 `ItemTable`, 캐릭터는 `CharacterTable`, 가챠 보상은 패킷(`GachaRewardInfo.Rarity`)이
  실어 온다. 칸이 한쪽을 골라 버리면 다른 쪽이 조용히 무시된다.
- `Bind`는 **`Bind(in StorageSlotData)`** 가 본체이고, `Bind(int itemId, int count, GlobalRarity)`는
  가챠가 쓰는 **얇은 래퍼**다. 표시 항목을 늘릴 때는 `StorageSlotData`에 넣는다.
- `Clear()`는 `Rarity Image` 색을 `RarityPalette.Unknown`으로 되돌린다. **풀에서 재사용되는
  칸이라** 안 되돌리면 이전 등급색이 남는다.
- 프리팹에 참조를 더하면 **양쪽 화면이 다 영향을 받는다.** 지금 잡아 둔 것은
  `Rarity Image`·`Item Image`·`Name Text`·`Sub Text` 넷이다.
- **칸이 화면을 알아보고 분기하지 않는다.** 화면마다 달라야 하는 것은 `SetSubVisible`처럼
  **부르는 쪽이 한 번 정해 주는 스위치**로 뺀다 — 창고는 보조 문구를 켜 두고, 가챠 결과는 끈다
  (거기서는 개수가 칸이 아니라 목록의 길이로 드러난다). `Clear()`는 이 결정을 되돌리지 않는다.

### `Sub Text` — 이름 아래 한 줄은 탭마다 다른 것이 온다

`Count Text`에서 이름이 바뀐 자리다(2026-09-02). **자원은 수량, 캐릭터는 배치 상태**가 들어와
더 이상 수량 칸이 아니다.

**용도를 나열한 이름(`Count + State`)을 쓰지 않는다** — 세 번째 용도가 생기면 또 바뀐다.
무엇이 오든 **"이름 아래 보조 문구 한 줄"이라는 자리**를 가리키는 이름으로 둔다.

> `Item Image`는 참조만 잡혀 있고 **아직 채우지 않는다** — 아이템 아이콘 컬럼이 테이블에 없다.
> 등급도 스프라이트가 없어 색으로만 표시한다(`RarityPalette`의 TODO).
> **캐릭터는 등급 자체가 없다** — `CharacterTable`에 컬럼이 없어 `GlobalRarity.None`(회색)으로 그린다.

## ⚠️ `Start` 순서에 기대지 않는다

`StorageTabPresenter.Start`가 격자에게 `HasSource`를 묻는데, **유니티는 두 `Start`의 순서를
보장하지 않는다.** 격자가 나중에 돌면 공급자가 아직 비어 있어 **모든 탭이 잠긴 채로 굳는다.**

→ `StorageGridPresenter.EnsureInitialized()`가 **양쪽에서 불릴 수 있고 두 번 불려도 한 번만 돈다.**
격자에 무언가를 묻는 public 메서드를 새로 만들면 **그 앞에도 같은 호출을 둔다.**
