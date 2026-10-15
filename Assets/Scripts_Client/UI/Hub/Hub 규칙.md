# Hub 폴더 규칙

> 최종 업데이트: 2026-10-15 (창고 정렬을 인벤토리와 같게 — 방향 ▼▲ · 기준 드롭다운) · 2026-10-15 (창고 화면 — 종류별 3탭 · 인벤토리 격자와 같은 격자 · 끌어 넣기·꺼내기 — T-126) · 2026-10-14 (거래 열 → 거점 열 · 경매장 탭을 `#Auction Canvas`로 분리 — T-147) · 2026-10-10 (뽑기 화면 — 풀마다 카드 · 도움말 아이콘 확률 툴팁 — T-143) · 2026-10-01 (창고 탭의 정의 — 두고두고 보관하는 곳 · T-101) · 2026-09-30 (창고/Storage → 인벤토리/Inventory 개명 · 거래 열 탭 3개 — T-101) · 2026-09-04 (풀 2종 x 단차·10연차 4버튼 · 이름·비용 표시) · 대상: `Assets/Scripts_Client/UI/Hub/`

**`#Hub Canvas` — 거점 열. 뽑기 · 창고 2탭이다.**

> 📌 **2026-10-14 개명 (T-147)** — 이 열은 원래 **거래 열(Market)** 이었다. 경매장이 3열 폭 전부를 쓰는 화면으로 빠져
> [`#Auction Canvas`](<../Auction/Auction 규칙.md>)가 되면서 뽑기·창고만 남아 **거점(Hub)** 으로 바꿨다.
> 코드(`HubCanvasView`·`HubTabPresenter`·`HubTab`)·오브젝트(`@Hub Column`·`#Hub Canvas`)·하단 메뉴 버튼(`거점`)이 모두 이 이름이다.

| 폴더 | 무엇 |
|------|------|
| `HubCanvasView.cs` | 캔버스 껍데기 — `Show(bool)`만 |
| `HubTabPresenter/` | 탭 줄 — `HubTab`(뽑기·창고)마다 버튼과 **화면(page)** 을 짝지어, 지금 탭의 화면만 켠다 |
| `GachaPresenter/` | 뽑기 탭의 화면 — 풀마다 카드(`GachaPoolCardView`) · 카드 문구·확률 계산(`GachaPoolSummary`) |
| `WarehousePresenter/` | 창고 탭의 화면 — 종류별 3탭 · 정렬(방향 ▼▲ · 기준) · 창고 격자. **격자는 인벤토리 격자(`InventoryGridPresenter`)를 `Container = Warehouse`로 둔 것이다** (아래 "창고 화면" 절) |

이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>)에 있다.

---

## 탭마다 화면이 따로다 — 인벤토리 탭과 다르다

인벤토리 열은 격자 하나가 탭 내용을 갈아 끼우지만, 거점 열의 탭은 **내용도 모양도 달라** 화면을 따로 둔다.

| 탭 | 화면 | 지금 |
|---|---|---|
| 뽑기 | `Gacha Presenter (↓ SUB VIEW)` | 동작 |
| 창고 | `Warehouse Presenter (↓ SUB VIEW)` | 동작 — 자원·장비·캐릭터를 두고두고 보관하는 곳(2026-10-01 정의). 서버 [T-107](../../../../tasks/archive/T-107-창고서버.md) · 클라 [T-126](../../../../tasks/archive/T-126-클라창고화면드래그.md) |

- 탭을 새로 붙일 때는 **그 page 안에 Presenter를 넣는다.** `HubTabPresenter`는 안 바뀐다 (창고 탭도 이렇게 붙였다 — 자리 표시 `No Feature Text (TMP)`를 지우고 page에 `WarehousePresenter`를 달았다).
- 아직 기능이 없는 탭도 누를 수 있다 — 화면의 `(기능 없음)`이 고장과 구분해 준다(설정 화면의 표기와 같다).
- ⚠️ page는 꺼졌다 켜진다. page 안의 Presenter는 `OnEnable`/`OnDisable`로 구독을 잇고 끊는다(`GachaPresenter`가 그렇다).
- ⚠️ **여기 '창고'는 인벤토리가 아니다.** 인벤토리(옛 이름 창고/Storage)는 **얻은 것이 바로 들어오는** 왼쪽 열이고, 창고는 **두고두고 쓸 것을 맡겨 두는** 곳이다. 코드에서 헷갈리지 않게 이 탭은 `Warehouse`라 부른다.
- ⚠️ `HubTab`은 씬에 int로 저장된다. 경매장 탭(옛 값 1)을 뺄 때 창고가 2 → 1로 당겨져 씬 배선을 다시 넣었다(2026-10-14). 늘릴 때는 끝에 붙인다.

---

## 창고 화면 — 인벤토리 격자를 하나 더 둔다 (2026-10-15 · T-126)

```
Warehouse Presenter (↓ SUB VIEW)   WarehousePresenter · VLG 0/5 — HubTabPresenter의 창고 page
├─ Tab Row                         자원 · 캐릭터 · 장비 (인벤토리 탭 줄을 본뜸 · 특성 없음) · pref 50
├─ Tool Row                        ▼▲ 방향 · 정렬 기준 · 안내 글 · pref 40
└─ Grid Presenter (↓ SUB VIEW)     InventoryGridPresenter — Container = Warehouse · 프레임 50 · flexH 1
```

**격자를 새로 짜지 않았다.** 칸·스크롤·끌기·정렬·보이는 칸만 그리기가 인벤토리 격자와 완전히 같고, 다른 것은
**어느 보관함을 그리고 어디로 요청하나**뿐이라 `InventoryGridPresenter`에 `container` 필드를 두고 같은 컴포넌트를 하나 더 놓았다.
공급자(`ResourceSlotSource` 등)도 같은 클래스에 보관함만 넘긴다. 상세는 [`Inventory 규칙.md`](<../Inventory/Inventory 규칙.md>)의 "창고 격자" 절.

- **탭은 인벤토리 탭과 늘 같이 바뀐다**(2026-10-06 사용자 결정 → 이슈 #58). 맞닿은 두 격자가 같은 종류여야 끌어 넘길 수 있다.
  전환의 주인은 `InventoryTabPresenter` 하나다 — 창고 탭 버튼은 `RequestTab`으로 요청하고, 바뀌면 `TabChanged`로 따라온다.
  인벤토리가 특성 탭이면 창고는 보던 탭에 머문다.
- **창고 칸에는 클릭 조작이 없다** — 판매·개봉·큐브·응축·잠금은 꺼낸 뒤 인벤토리에서 한다(서버도 인벤토리 것만 받는다).
  그래서 창고 격자의 큐브 창·응축 창 참조는 비워 둔다. 툴팁 조작 줄은 `끌기 — 인벤토리로 꺼내기` 하나다.
- **정렬은 인벤토리 도구 줄과 똑같다** — 방향 버튼(▼▲)과 기준 드롭다운(등급순·이름순·수량순 — 수량순은 자원 탭만). 위젯은 인벤토리 것을 복제했다. 방향·기준은 인벤토리와 따로 든다.
- 칸 수는 `Constants.WarehouseCapacity`(50)다 — `PlayerDataModel.GetStorageCapacity(container, tab)`이 보관함을 보고 답한다.

---

## 풀마다 카드 한 장 — 카드는 인스펙터에 줄로 넣는다 (T-143)

`GachaInfoTable`(`Gacha.xlsx`)의 풀을 **카드 한 장씩** 보인다. `GachaId`가 곧 `GachaInfoTID`다.
버튼 8개를 나열하던 화면을 2026-10-10에 바꿨다 — 어느 버튼이 어느 풀인지 글자를 읽어야 알았다.

```
Gacha Presenter (↓ SUB VIEW)     VLG 5/5
└─ Pool Scroll Panel             ScrollRect · flexH 1 — 창이 작아지면 굴린다
   └─ Viewport (RectMask2D) / Content (VLG 0/5 + ContentSizeFitter)
      └─ Character · Weapon · Accessory · Gem Pool Card   GachaPoolCardView 프리팹 × 4 · pref 146
         ├─ Header Panel   제목 "캐릭터 소환 (일꾼)" · 도움말 아이콘(HelpIcon 프리팹)
         └─ Body Panel     대표 아이콘(ItemIconView) + 종수 · [1회] · [10회](강조색)
```

| 자리 | 출처 |
|---|---|
| 제목 | `GachaInfoTable.Name` + 괄호 — 캐릭터만이면 "일꾼", 장비만이면 "작업속도 증가" |
| 종수 · 대표 아이콘 | 가챠 시트에서 그 풀의 행 수 · 가장 높은 등급의 첫 항목 |
| 도움말 아이콘(?) | **올리면 등급 확률 툴팁** — 기능 설명·확률은 도움말 아이콘에 단다([`Overlay 규칙.md`](<../Overlay/Overlay 규칙.md>) "툴팁 두 종류"). 처음엔 `[P]` 배지였다 |
| 비용 | `CostSingle` · `CostMulti` — 모자라면 경고색 + 잠금 |

**확률은 테이블에서 계산한다** (`GachaPoolSummary`). 서버 `GachaPoolCatalog`처럼 **아이템·캐릭터·장비·골드 네 시트를
같은 GachaId로 섞어** 가중치 합을 분모로 쓴다 — 한 시트만 보면 섞인 풀의 확률이 틀린다. 등급은 각 정의 테이블에서 읽는다.

`GachaPresenter`의 `Pools` 배열에 `{ gachaId, 카드 }`를 **화면과 같은 순서로** 넣는다.
배선이 비면 콘솔로 알리고, 테이블에 없는 풀의 카드는 **끈다** — 켜 두면 버튼이 고장 난 것처럼 보인다.
카드를 늘릴 때는 `Content` 아래에 프리팹을 하나 더 놓고 배열에 한 줄 넣는다. **탭은 풀이 늘면** 그때 나눈다.

### ⚠️ 이름과 비용을 코드에 박지 않는다

`Name`·`CostSingle`·`CostMulti`를 테이블에서 읽어 `Start`에서 채운다.
**10연차가 단차 x 10이라는 보장이 없다** — 컬럼이 따로다. 곱해서 만들면 기획이 할인율을
넣는 순간 화면과 서버가 다른 값을 말한다.

### 골드가 모자란 버튼만 잠근다

`PlayerDataModel.CurrencyChanged`를 구독해 `Gold >= 비용`인 버튼만 연다(`GachaPoolCardView.SetState`).
**두 축을 함께 본다** — 대기 중이면 전부 잠기고, 대기가 풀려도 모자란 버튼은 잠긴 채로 남아야 한다.
한 축만 보면 응답이 온 순간 살 수 없는 버튼까지 함께 열린다.

> **클라 판단은 표시용일 뿐이다.** 실제 거절은 서버가 하고(`NotEnoughCurrency`),
> 그 사유는 `ResultMessages`를 거쳐 알림으로 뜬다.
> 재화 축은 골드뿐이라(다이아 폐지 — #41) `CostCurrency`가 골드가 아니면 경고만 남긴다.

## 지금 있는 규칙

- **가챠가 거점 열에 있는 이유**는 기획이다 — 가챠 티켓이 골드 상점 품목이다
  (`GameDesign/기획/거래/README.md` 3.2). 화면 배치가 아니라 기획이 정한 자리라
  옮길 때는 기획 쪽을 먼저 본다.
- **결과 내용은 이 폴더에서 그리지 않는다.** 뽑힌 보상·인벤토리 반영은 `PlayerDataModel`이
  처리하고, 결과 팝업은 `!Overlay Canvas`의 [`GachaResultPresenter`](<../Overlay/Overlay 규칙.md>)가
  `GachaCompleted`를 **스스로 구독해서** 띄운다. **여기 자식으로 두지 않는다** — 요청 직후 이 열을
  닫으면 결과가 통째로 사라지기 때문이다(아래 사고와 같은 뿌리).
  다만 **성공/실패 도착 여부는 여기서 구독한다** — 요청 중 로딩·버튼 잠금·실패 알림이 필요하기 때문이다.

## ⚠️ 요청 중 열을 닫아 응답을 놓쳤던 사고

가챠 요청을 보낸 직후 하단 [거래](지금 [거점])로 이 열을 닫으면, `OnDisable`이 구독을 끊어 응답을
놓치고 **실제로는 성공한 요청에 5초 뒤 무응답 알림이 떴다.**

대응은 이 폴더가 아니라 오버레이 쪽에 있다 — **대기 차단(`blocksRaycasts`)을 표시보다
먼저, 요청 즉시 건다.** 근거와 두 축(차단/표시)의 구분은
[`Overlay 규칙.md`](<../Overlay/Overlay 규칙.md>)의 "로딩은 두 축을 나눠 쓴다".

**전역 차단이 생겼어도 `GachaPresenter`의 개별 버튼 잠금은 남긴다.** 역할이 다르다 —
차단은 "대기 중 전체"를 막고, 개별 잠금은 "이 버튼은 지금 못 누른다"를 **보이게** 한다.
