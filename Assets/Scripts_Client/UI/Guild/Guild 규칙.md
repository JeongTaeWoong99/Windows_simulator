# Guild 폴더 규칙

> 최종 업데이트: 2026-11-22 (상점 · 뽑기 탭을 뺐다 — 창고 1탭 + 빈 자리 버튼 둘 · `GachaPresenter`·`ShopPresenter`·카드 프리팹 삭제 · #117) · 2026-11-14 (카드에서 종수·보유 문구를 뺐다 — 대표 아이콘 72px을 가운데에 · 보유 수는 인벤토리가 말한다 · 번역 키 `UI.Shop.Owned`는 지금 안 쓴다) · 2026-11-01 (인벤토리 특성 탭 제거 반영 · T-182) · 2026-10-27 (탭 순서 상점 · 뽑기 · 창고 — `GuildTab` 값도 이 순서 · T-182) · 2026-10-27 (거점 열 → 길드 열 · `Hub` → `Guild` 개명 — 거점은 지도 화면 이름으로 간다 · T-182) · 2026-10-27 (상점 카드 [열기]를 뺐다 — 뽑기권은 인벤토리에서 연다 · 결과 제목 '<이름> 개봉' — T-184) · 2026-10-26 (상점 탭 — 캐릭터 뽑기권 A~D 구매 · 뽑기 탭에서 캐릭터 카드를 뺐다 — T-184 · 이슈 #78) · 2026-10-25 ([창고에 쌓기] — 창고에 이미 있는 자원만 인벤토리에서 채운다 — T-175 · 이슈 #82) · 2026-10-16 (창고 칸 클릭 조작이 인벤토리와 같다 — T-163) · 2026-10-15 (창고 정렬을 인벤토리와 같게 — 방향 ▼▲ · 기준 드롭다운) · 2026-10-15 (창고 화면 — 종류별 3탭 · 인벤토리 격자와 같은 격자 · 끌어 넣기·꺼내기 — T-126) · 2026-10-14 (거래 열 → 거점 열 · 경매장 탭을 `#Auction Canvas`로 분리 — T-147) · 2026-10-10 (뽑기 화면 — 풀마다 카드 · 도움말 아이콘 확률 툴팁 — T-143) · 2026-10-01 (창고 탭의 정의 — 두고두고 보관하는 곳 · T-101) · 2026-09-30 (창고/Storage → 인벤토리/Inventory 개명 · 거래 열 탭 3개 — T-101) · 2026-09-04 (풀 2종 x 단차·10연차 4버튼 · 이름·비용 표시) · 대상: `Assets/Scripts_Client/UI/Guild/`

**`#Guild Canvas` — 길드 열. 지금은 창고 1탭이다.** 탭 줄의 창고 오른쪽 빈 버튼 둘은 뒤에 들어올 탭 자리다(비활성).

> 📌 **2026-10-14 개명 (T-147)** — 이 열은 원래 **거래 열(Market)** 이었다. 경매장이 3열 폭 전부를 쓰는 화면으로 빠져
> [`#Auction Canvas`](<../Auction/Auction 규칙.md>)가 되면서 뽑기·창고만 남아 **길드(Guild)** 으로 바꿨다.
> 코드(`GuildCanvasView`·`GuildTabPresenter`·`GuildTab`)·오브젝트(`@Guild Column`·`#Guild Canvas`)·하단 메뉴 버튼(`길드`)이 모두 이 이름이다.

| 폴더 | 무엇 |
|------|------|
| `GuildCanvasView.cs` | 캔버스 껍데기 — `Show(bool)`만 |
| `GuildTabPresenter/` | 탭 줄 — `GuildTab`(지금 창고 하나)마다 버튼과 **화면(page)** 을 짝지어, 지금 탭의 화면만 켠다 |
| `WarehousePresenter/` | 창고 탭의 화면 — 종류별 3탭 · 정렬(방향 ▼▲ · 기준) · 창고 격자. **격자는 인벤토리 격자(`InventoryGridPresenter`)를 `Container = Warehouse`로 둔 것이다** (아래 "창고 화면" 절) |

이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>)에 있다.

---

## 탭마다 화면이 따로다 — 인벤토리 탭과 다르다

인벤토리 열은 격자 하나가 탭 내용을 갈아 끼우지만, 길드 열의 탭은 **내용도 모양도 달라** 화면을 따로 둔다.

| 탭 | 화면 | 지금 |
|---|---|---|
| 창고 | `Warehouse Presenter (↓ SUB VIEW)` | 동작 — 자원·장비·캐릭터를 두고두고 보관하는 곳(2026-10-01 정의). 서버 [T-107](../../../../tasks/archive/T-107-창고서버.md) · 클라 [T-126](../../../../tasks/archive/T-126-클라창고화면드래그.md) |

- 탭을 새로 붙일 때는 **그 page 안에 Presenter를 넣는다.** `GuildTabPresenter`는 안 바뀐다 (창고 탭도 이렇게 붙였다 — 자리 표시 `No Feature Text (TMP)`를 지우고 page에 `WarehousePresenter`를 달았다).
- 아직 기능이 없는 탭도 누를 수 있다 — 화면의 `(기능 없음)`이 고장과 구분해 준다(설정 화면의 표기와 같다).
- ⚠️ page는 꺼졌다 켜진다. page 안의 Presenter는 `OnEnable`/`OnDisable`로 구독을 잇고 끊는다(`WarehousePresenter`가 그렇다).
- ⚠️ **여기 '창고'는 인벤토리가 아니다.** 인벤토리(옛 이름 창고/Storage)는 **얻은 것이 바로 들어오는** 왼쪽 열이고, 창고는 **두고두고 쓸 것을 맡겨 두는** 곳이다. 코드에서 헷갈리지 않게 이 탭은 `Warehouse`라 부른다.
- ⚠️ `GuildTab`은 씬에 int로 저장된다. 탭을 뺄 때마다 창고 값이 당겨져 씬 배선을 다시 넣었다(경매장 2026-10-14 · 상점·뽑기 2026-11-22 → 창고 = 0). 늘릴 때는 끝에 붙인다.
- **빈 자리 버튼**(`Empty Tab Button 1·2`) — 버튼만 있고 `GuildTab`·page가 없다(`interactable` 끔 · 글씨 없음). 새 탭은 이 버튼에 글씨를 넣고 `Tabs`에 한 줄을 더해 쓴다.

---

## 창고 화면 — 인벤토리 격자를 하나 더 둔다 (2026-10-15 · T-126)

```
Warehouse Presenter (↓ SUB VIEW)   WarehousePresenter · VLG 0/5 — GuildTabPresenter의 창고 page
├─ Tab Row                         자원 · 캐릭터 · 장비 (인벤토리 탭 줄을 본뜸) · pref 50
├─ Tool Row                        ▼▲ 방향 · 정렬 기준 · 안내 글 · [창고에 쌓기](자원 탭만) · pref 40
└─ Grid Presenter (↓ SUB VIEW)     InventoryGridPresenter — Container = Warehouse · 프레임 50 · flexH 1
```

**격자를 새로 짜지 않았다.** 칸·스크롤·끌기·정렬·보이는 칸만 그리기가 인벤토리 격자와 완전히 같고, 다른 것은
**어느 보관함을 그리고 어디로 요청하나**뿐이라 `InventoryGridPresenter`에 `container` 필드를 두고 같은 컴포넌트를 하나 더 놓았다.
공급자(`ResourceSlotSource` 등)도 같은 클래스에 보관함만 넘긴다. 상세는 [`Inventory 규칙.md`](<../Inventory/Inventory 규칙.md>)의 "창고 격자" 절.

- **탭은 인벤토리 탭과 늘 같이 바뀐다**(2026-10-06 사용자 결정 → 이슈 #58). 맞닿은 두 격자가 같은 종류여야 끌어 넘길 수 있다.
  전환의 주인은 `InventoryTabPresenter` 하나다 — 창고 탭 버튼은 `RequestTab`으로 요청하고, 바뀌면 `TabChanged`로 따라온다.
- **창고 칸의 클릭 조작은 인벤토리 칸과 같다**(2026-10-16 · T-163) — 판매 담기·개봉·큐브·응축·잠금·경매를 꺼내지 않고 한다.
  창고 격자의 큐브 창·응축 창 참조도 인벤토리 열의 같은 창을 넣는다. 툴팁 끌기 줄만 `칸 이동 · 인벤토리로 꺼내기`로 다르다.
- **정렬은 인벤토리 도구 줄과 똑같다** — 방향 버튼(▼▲)과 기준 드롭다운(등급순·이름순·수량순 — 수량순은 자원 탭만). 위젯은 인벤토리 것을 복제했다. 방향·기준은 인벤토리와 따로 든다.
- **[창고에 쌓기]** (2026-10-25 · T-175 · 이슈 #82) — 인벤토리 자원을 창고에 **이미 있는** 같은 자원 칸으로 MaxStack까지 채운다(`C_WarehouseStackRequest` → `S_WarehouseTransferResponse`).
  창고 빈 칸에 새 묶음은 만들지 않고, 창고 칸이 차서 남는 몫은 인벤토리에 남는다. 자원 탭에서만 보인다.
  옮길 것이 없으면(`PlayerDataModel.CanStackIntoWarehouse`) 보내지 않고 토스트로 말한다 — 서버는 빈 목록에도 Ok라 눌러도 아무 일이 없어 보인다.
  요청은 창고 격자가 보낸다(`InventoryGridPresenter.StackIntoWarehouse`) — 칸 요청 대기가 격자끼리 하나다.
- **새 자원은 창고의 같은 자원 칸부터 찬다**(서버 T-175) — 지급 응답의 `ItemChangeInfo.Container = Warehouse`로 창고 칸이 갱신되어 오고, 창고 격자가 바로 다시 그린다(패킷 변경 없음).
- 칸 수는 `Constants.WarehouseCapacity`(50)다 — `PlayerDataModel.GetStorageCapacity(container, tab)`이 보관함을 보고 답한다.

---

## 빠진 탭 — 상점 · 뽑기 (2026-11-22 · #117)

골드 뽑기를 없애고 캐릭터 · 장비는 상자에서만 나온다(#117 · T-200 · T-209). 서버는 장비 뽑기를 먼저 닫았고(`InvalidGachaId`), 클라는 두 탭을 통째로 뺐다.
`GachaPresenter` · `GachaPoolCardView` · `GachaPoolSummary` · `ShopPresenter` · `Prefabs/GachaPoolCardView.prefab`을 지웠다 — 옛 모양은 git 이력에 있다.
결과 팝업([`GachaResultPresenter`](<../Overlay/Overlay 규칙.md>))의 [n회 더 뽑기]는 늘 숨고, 그 버튼 줄은 감정 무대의 [1개 더 감정] 견본으로만 쓴다.
테스트용 캐릭터 · 장비는 치트로 받는다.

---

## ⚠️ 요청 중 열을 닫아 응답을 놓쳤던 사고

가챠 요청을 보낸 직후 하단 [거래](지금 [길드])로 이 열을 닫으면, `OnDisable`이 구독을 끊어 응답을
놓치고 **실제로는 성공한 요청에 5초 뒤 무응답 알림이 떴다.**

대응은 이 폴더가 아니라 오버레이 쪽에 있다 — **대기 차단(`blocksRaycasts`)을 표시보다
먼저, 요청 즉시 건다.** 근거와 두 축(차단/표시)의 구분은
[`Overlay 규칙.md`](<../Overlay/Overlay 규칙.md>)의 "로딩은 두 축을 나눠 쓴다".

**전역 차단이 생겼어도 개별 버튼 잠금은 남긴다**(옛 `GachaPresenter`에서 정한 규칙 · 창고 요청도 같다). 역할이 다르다 —
차단은 "대기 중 전체"를 막고, 개별 잠금은 "이 버튼은 지금 못 누른다"를 **보이게** 한다.
