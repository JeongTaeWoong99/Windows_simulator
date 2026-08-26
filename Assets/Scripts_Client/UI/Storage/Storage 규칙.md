# Storage 폴더 규칙

> 최종 업데이트: 2026-08-27 (빈 탭 셋이 무엇을 기다리는지 명시) · 대상: `Assets/Scripts_Client/UI/Storage/`

**`#Storage Canvas` — 탭으로 내용을 갈아 끼우는 창고 화면.**

| 폴더 | 무엇 |
|------|------|
| `StorageCanvasView.cs` | 캔버스 껍데기 |
| `StorageTabPresenter/` | 탭 4개를 쥐고 무엇을 켤지 정한다 |
| `InventoryPresenter/` | 자원 목록 (+ 종속 View `InventorySlotView`) |
| `StorageInformationPresenter/` | 창고 정보 |

이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>), 스크롤·레이아웃 함정은
[`Layout 규칙.md`](<../Layout/Layout 규칙.md>)에 있다.

---

## 탭 전환도 층은 하나다

`#Main Canvas`와 같은 원리다 — 자리를 나눠 쓰는 화면이 여럿이면 **캔버스를 여러 개 두지 않고
Presenter를 여러 개 두고, 켜고 끄는 곳을 한 군데로 모은다.** 여기서는 `StorageTabPresenter`가
그 한 군데다. 탭 enum 하나를 seam으로 두고, 각 Presenter는 자기를 끄지 않는다.

## 아직 화면이 없는 버튼도 Presenter가 잡아 둔다

탭 4개 중 자원 하나만 실재하지만, `StorageTabPresenter`가 **네 버튼을 다 잡아 두고**
"아직 없다"를 로그로 남긴다. **안 잡아 두면 눌러도 아무 일이 없어 버튼이 고장 난 것과
구분되지 않는다.**

### 나머지 셋이 비어 있는 이유는 클라가 아니다

**셋 다 화면을 만들 수 있는 상태가 아니다.** 착수하기 전에 여기를 본다 —
"UI만 만들면 된다"고 시작하면 데이터가 없다는 걸 중간에 발견한다.

| 탭 | 무엇이 없나 |
| --- | --- |
| **캐릭터** | 보유 캐릭터가 **시작 지급 1종뿐**이라 목록·필터를 실검증할 수 없다. 획득 경로(가챠)가 `tasks/T-026` |
| **장비** | **장비 시스템 자체가 없다.** `tasks/T-002`는 장착·해제만 다루고 **획득 경로는 기획에도 없다** → `tasks/README.md` 순서 메모의 "일감조차 없는 공백" |
| **특성** | 기획은 있으나(`GameDesign/design/trait/`) 서버 구현·패킷이 없다 |

> ⚠️ `tabButtons` 배열 위의 `[CenterHeader]`에는 `[NonReorderable]`을 같이 단다 —
> **순서가 곧 탭의 의미**라 드래그로 뒤바뀌면 안 되고, reorderable list 경로에서는
> 헤더가 통째로 안 보인다([`UI 규칙.md`](<../UI 규칙.md>)의 "공통 작성 규약").

## 반복 칸은 종속 View로 뺀다

`InventorySlotView`가 그 예다. 같은 칸이 N개 복제돼 각자 다른 데이터에 묶이면 위젯을
Presenter가 다 들고 있을 수 없다. **매 프레임 도는 계산은 View가 아니라 `InventoryPresenter`가
하고, View에는 결과만 넘긴다** — 상시 실행 앱에서 비용이 칸 수만큼 곱해진다.

## ⚠️ `InventorySlotView`는 창고 전용이 아니다

폴더는 `InventoryPresenter/` 아래지만, **프리팹(`Assets/Prefabs/InventorySlotView.prefab`)은
캔버스를 넘어 재사용된다** — `!System Canvas`의 [`GachaResultPresenter`](<../System/System 규칙.md>)가
가챠 결과 칸으로 같은 프리팹을 찍어 쓴다.

**같아야 할 생김새를 두 벌로 두면 한쪽만 고쳐진다.** 그래서 복제하지 않고 공유한다.

고칠 때 지켜야 할 것 —

- **창고 사정에 맞춘 로직을 View에 넣지 않는다.** 이름은 출처가 `ItemTable` 하나뿐이라 View가
  직접 조회하지만, **등급은 출처가 둘**이다 — 창고는 테이블(`GameDataLoader.GetItemRarity`),
  가챠는 패킷(`GachaRewardInfo.Rarity`). 그래서 등급은 **완성된 값을 받아 그린다**.
- `Bind`는 **`Bind(int itemId, int count, GlobalRarity rarity)`** 다. 인자를 늘릴 때는
  두 호출부(`InventoryPresenter`·`GachaResultPresenter`)를 함께 고친다.
- `Clear()`는 `Rarity Image` 색을 `RarityPalette.Unknown`으로 되돌린다. **풀에서 재사용되는
  칸이라** 안 되돌리면 이전 등급색이 남는다(창고는 안 쓰지만 가챠가 쓴다).
- 프리팹에 참조를 더하면 **양쪽 화면이 다 영향을 받는다.** 지금 잡아 둔 것은
  `Rarity Image`·`Item Image`·`Name Text`·`Count Text` 넷이다.
- **칸이 화면을 알아보고 분기하지 않는다.** 화면마다 달라야 하는 것은 `SetCountVisible`처럼
  **부르는 쪽이 한 번 정해 주는 스위치**로 뺀다 — 창고는 수량을 켜 두고, 가챠 결과는 끈다
  (거기서는 개수가 칸이 아니라 목록의 길이로 드러난다). `Clear()`는 이 결정을 되돌리지 않는다.

> `Item Image`는 참조만 잡혀 있고 **아직 채우지 않는다** — 아이템 아이콘 컬럼이 테이블에 없다.
> 등급도 스프라이트가 없어 색으로만 표시한다(`RarityPalette`의 TODO).
