# Auction 폴더 규칙

> 최종 업데이트: 2026-10-14 (거래 열의 탭 → 화면 전체 `#Auction Canvas` · `!Overlay Canvas` 첫째 자식 · 닫기 [X]·Esc — T-147) · 이전 이력은 `Hub 규칙.md`(옛 `Market 규칙.md`)의 git 기록 · 대상: `Assets/Scripts_Client/UI/Auction/`

**`#Auction Canvas` — 경매장. 3열 폭 전부 × 가운데 띠를 쓰는 화면이다(2026-10-14 · [T-147](../../../../tasks/T-147-경매장전체화면목업.md)).**
옛날엔 거래 열(지금 거점 열 — [`Hub 규칙.md`](<../Hub/Hub 규칙.md>))의 탭 하나였다. 탭이 세 겹이라 좁고, 장비 검색 줄이 열 폭을 넘어 두 줄로 쪼개야 했다.

| 폴더 | 무엇 |
|------|------|
| `AuctionCanvasView.cs` | 캔버스 — 여닫기 · **판을 3열 가운데 띠에 맞추기** · 경매 등록 바로가기(`OpenRegister`) · 위에 막는 오버레이가 떠 있나(`IsCovered`) |
| `AuctionHeaderPresenter/` | 머리 줄 — 제목 · [X] · **Esc** 닫기 |
| `AuctionTabPresenter/` | 하위 탭 줄 — `AuctionTab`(구매·등록·내 매물)마다 버튼과 화면을 짝짓는다 |
| `AuctionBuyTabPresenter/` | [구매] 안의 축 줄 — `AuctionBuyKind`(자원·장비·캐릭터)마다 버튼과 화면을 짝짓는다 |
| `MarketItemPresenter/` | 구매 › 자원(거래소) — 자원 목록 · 가격대 · 수량 구매. ※ 이름의 `Market`은 거래소(`MarketItemInfo` 패킷)다 — 옛 거래 열이 아니다 |
| `AuctionSearchPresenter/` | 구매 › 장비 · 캐릭터 — 검색 · [더 보기] · 즉시구매. **같은 클래스를 씬에 둘** 놓고 `searchKind`로 가른다 |
| `AuctionRegisterPresenter/` | 등록 — 인벤토리의 자원·장비·캐릭터를 골라 단가를 정해 올린다 |
| `AuctionMyListingPresenter/` | 내 매물 — 올린 것 보기 · 취소 |
| `AuctionRowView.cs` · `AuctionRowList.cs` · `AuctionText.cs` | 경매장 화면들이 함께 쓰는 목록 한 줄 · 줄 풀 · 문구(매물 줄·판매자·남은 시간·이름→TID). **이 캔버스 안에서만 쓰여** `UI/Shared/`가 아니라 여기 둔다 |
| `AuctionSort.cs` · `AuctionInput.cs` | 목록 가격 정렬(로컬 사본만) · 수량·단가 입력칸 범위 맞추기 — 같은 이유로 여기 둔다 |

이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>)에 있다.

---

## 자리 — `!Overlay Canvas`의 첫째 자식 (2026-10-14)

```
!Overlay Canvas
├─ [0] #Auction Canvas (MAIN VIEW)   Canvas(중첩) · GraphicRaycaster · 차단막 Image(검정 a 0.35) · AuctionCanvasView — 평소 꺼짐
│  └─ Body Panel                     3열 가운데 띠에 맞춘다 · 불투명 검정 판 · VLG 5/5
│     ├─ Background                  ignoreLayout · PanelBg — 다른 창과 같은 바탕색
│     ├─ Header Presenter (↓ SUB VIEW)       AuctionHeaderPresenter · 'AUCTION' · [X]
│     ├─ Auction Tab Presenter (↓ SUB VIEW)  구매 · 등록 · 내 매물
│     └─ Auction Buy Page · Auction Register Presenter · Auction My Listing Presenter
└─ [1] Gacha Result Presenter …      나머지 오버레이는 그대로 — 전부 경매장보다 앞에 그린다
```

판별은 [`Overlay 규칙.md`](<../Overlay/Overlay 규칙.md>)의 두 축 그대로다.

| 축 | 답 | 그래서 |
|---|---|---|
| ① 차단 범위 | **화면 전체**를 막는다(2026-10-14 사용자 결정 — 상태 줄·위젯까지 차단막이 덮는다) | `!Overlay Canvas`에 산다. 열 캔버스는 Sorting Order 0인 형제라 그 안의 차단막은 다른 열에 닿지 않는다 |
| ② 생명주기 | 다시 켜 줄 손(`UIManager.ShowAuction`)이 **밖에 있다** | `CanvasGroup`이 아니라 **`SetActive`** — 로그인과 같은 이유. 안의 Presenter들이 열 때마다 `OnEnable`로 다시 받는 동작을 거래 열 시절 그대로 쓴다 |

- **첫째 자식인 이유** — 오버레이 층은 뒤 형제일수록 앞에 그린다. 경매장이 띄우는 확인·수량 팝업·툴팁·로딩·알림이 전부 경매장 **위**에 떠야 한다.
- **Presenter 여럿을 담는 캔버스라 `(↓ SUB VIEW)`가 아니라 `(MAIN VIEW)`다** — Presenter 안에 Presenter를 두지 않는다. 자기 `Canvas`를 단 것도 같은 이유다: 그래픽이 수백 개라 오버레이 층(그래픽 49개)과 다시 묶기를 나눈다([`Overlay 규칙.md`](<../Overlay/Overlay 규칙.md>) "나누는 조건").
- **판이 불투명한 이유** — 바탕색 `PanelBg`는 반투명(a 0.92)이라 그대로 두면 뒤의 3열 글자가 비친다. 판을 검정으로 칠하고 그 위에 `Background`(PanelBg)를 깐다.

### 가운데 띠에 맞춘다 — `AuctionCanvasView.FitBodyToBand`

판(`Body Panel`)은 앵커를 화면 전체로 늘려 두고, **열 때마다** 위·아래 offset을 3열 가운데 칸 자리로 맞춘다.
위·아래 칸 높이는 위젯 위치(위·아래)에 따라 바뀌어서다 — 값은 `WidgetPositionLayout.TryGetCenterBandInsets`가 거점 열의 위·아래 스페이서에서 읽는다.
⚠️ 씬에 저장된 offset(아래 96 / 위 64)은 위젯이 아래 칸일 때의 값일 뿐이다 — 고쳐도 열 때 덮인다.

### 여는 길 · 닫는 길

| 길 | 무엇이 | 어디 |
|---|---|---|
| 열기 | 하단 메뉴 **[경매장]** | `MenuPresenter.auctionButton` → `UIManager.ShowAuction(true)` — **토글이 아니다**(열리면 메뉴가 가려진다) |
| 열기 | 인벤토리 칸 **Shift+우클릭** | `AuctionCanvasView.OpenRegister` — 경매장 → [등록] → 그 물건을 골라 둔다 |
| 닫기 | 머리 줄 **[X]** · **Esc** | `AuctionHeaderPresenter` → `UIManager.ShowAuction(false)` |
| 닫기 | 전부 접기 | `UIManager.CloseAllExceptWidget`도 닫는다 — 단, 경매장이 열려 있는 동안엔 차단막이 위젯 버튼을 덮는다 |

- ⚠️ **Esc는 경매장 위에 막는 오버레이가 없을 때만 닫는다**(`AuctionCanvasView.IsCovered`) — 확인·수량 팝업이 떠 있을 때 닫으면 팝업만 홀로 남고,
  **로딩 중에 닫으면 안의 Presenter가 구독을 끊어 응답을 놓친다**(거래 열 시절 가챠 사고와 같은 뿌리 — [`Hub 규칙.md`](<../Hub/Hub 규칙.md>)). 클릭은 로딩 차단막이 막지만 키는 못 막는다.
- Esc는 `KeyInputManager.WasClosePressed`로만 읽는다 — 키는 그 매니저 한 곳에서 읽는다.
- 인벤토리는 경매장에 가려진다 — Shift+우클릭은 경매장이 닫힌 상태에서 쓰는 길이다. 목업의 [+ 판매 등록] 창(창 안에 내 인벤토리)이 들어오면 경매장 안에서도 고를 수 있다.

> 🚧 **목업대로의 재배치는 아직이다** — 지금은 거래 열 시절의 하위 탭(구매·등록·내 매물)을 넓은 판에 그대로 옮긴 상태다.
> 목업([`경매장목업.html`](../../../../GameDesign/design/ui/경매장목업.html))의 검색 · 시세 · 판매 · 완료 탭은 [T-147](../../../../tasks/T-147-경매장전체화면목업.md)에서 이어 간다.

---

## 하위 탭 — 구매 · 등록 · 내 매물 (T-096)

**탭은 하는 일로 나눈다(2026-09-30).** 파는 축(자원·장비·캐릭터)으로 나누면 축이 늘 때마다 탭이 는다.
축은 [구매] 안의 축 줄과 [등록]의 종류 드롭다운이 고른다.

```
#Auction Canvas > Body Panel
├─ Auction Tab Presenter (↓ SUB VIEW)        구매 · 등록 · 내 매물
├─ Auction Buy Page
│   ├─ Auction Buy Tab Presenter (↓ SUB VIEW) 자원 · 장비 · 캐릭터
│   ├─ Market Item Presenter                 자원(거래소)
│   ├─ Auction Search Presenter              장비
│   └─ Character Search Presenter            캐릭터 (위의 복제 · searchKind = Character)
├─ Auction Register Presenter
└─ Auction My Listing Presenter
```

화면은 탭 Presenter의 자식이 아니라 **형제**다(Presenter 안에 Presenter를 두지 않는다).

**장비·캐릭터 검색 줄은 두 줄이다** — `Search Panel`(이름 입력 + [검색]) / `Filter Panel`(드롭다운들 + `Spacer` + 정렬).
⚠️ 거래 열 시절 한 줄에 다 넣으면 min 폭 합(685)이 열 폭(603)을 넘어 **바깥 3열 레이아웃까지 밀렸다**(2026-10-03). 판이 1920으로 넓어진 지금은 목업 재배치 때 다시 정한다. 두 줄 모두 `LayoutElement.minWidth = 0` — 칸을 더 넣어도 열 폭을 밀지 않는다. 드롭다운을 늘릴 때는 `Filter Panel`에 넣는다.

| 탭 · 축 | 화면 | 무엇 |
|---|---|---|
| 구매 › 자원 | `MarketItemPresenter` | **거래소** — 자원을 종류별 한 줄로(최저가·판매 중 수량·최근가·전일 평균) → [선택] → 가격대 → "N개, 개당 최대 P" 구매. 다 못 채우면 아무것도 안 산다 |
| 구매 › 장비 | `AuctionSearchPresenter` | 이름·분류·등급·**인챈트 등급**으로 검색. **서버가 단가 싼 순으로 20개씩**(최대 50) 주고, [더 보기]가 받은 것의 마지막 매물(단가·ID)을 커서로 다음 20개를 잇는다 — 더 있는지는 `HasMore`. 버튼에 받은 수를 적는다(전체 수는 서버가 주지 않는다). 매물은 통째로만 산다 |
| 구매 › 캐릭터 | `Character Search Presenter` | 이름·등급으로 검색(분류·인챈트 드롭다운은 꺼 둔다). 줄은 레벨, **적성 5종은 툴팁에**([이슈 #47](https://github.com/JeongTaeWoong99/Windows_simulator/issues/47)) |
| 등록 | `AuctionRegisterPresenter` | 자원·장비·캐릭터. **올릴 수 없는 것도 목록에 흐리게 남기고 툴팁에 사유**(끼고 있는 장비 · 배치 중·장비 낀 캐릭터 · 마지막 캐릭터 — `EntityBlockText`). 인벤토리 칸 **Shift+우클릭**으로 바로 와서 그 물건이 골라진다(`AuctionCanvasView.OpenRegister`) |
| 내 매물 | `AuctionMyListingPresenter` | 열 때마다 새로 받는다. 구매 진행 중(`Reserved`)이면 [취소]를 잠근다. 판매·만료로 바뀌면 탭 버튼에 **배지**(`News Badge`) — 열면 꺼진다 |

### 거래소는 서버 상점이 아니다

**거래소 = 플레이어들이 올린 자원 매물을 종류별로 묶어 보여 주는 창**이다. 서버가 파는 물건·서버가 정하는 시세가 아니다.
자원은 같은 TID면 똑같아 매물을 하나씩 고를 이유가 없으니 "N개를 최대 P에"로 사고, 서버가 싼 매물부터 여러 판매자에 걸쳐 채운다.
장비는 개체마다 인챈트가 달라 매물을 하나씩 고른다 — 그래서 두 축의 화면이 다르다.

### 두 가지 파는 법 — 화면마다 구분해 말한다 (2026-09-30)

| | 즉시 판매 (인벤토리 [즉시 판매]) | 경매 등록 (경매장 [등록]) |
|---|---|---|
| 값 | **즉시 판매가** = `BasePrice` x `SellRatePermille` — 정해져 있다 | 판매자가 정한다 — **즉시 판매가 이상, 1조 이하** · 한 번에 **9,999개까지** |
| 언제 돈이 오나 | 바로 | 팔리면 우편 |

- **상한은 즉시 판매가의 배수가 아니라 절대값이다(2026-10-05 태웅 결정 — `Constants.AuctionMaxUnitPrice` = 1조).**
  x10은 희귀한 물건의 값을 플레이어가 매기지 못하게 막았다.
- **단가 상한(1조)과 수량 상한(`Constants.AuctionMaxListingCount` = 9,999)은 한 쌍이다(2026-10-06 · 이슈 #56 서버 리뷰).**
  처음 잡은 10조는 "단가 x 수량 x 수수료가 `long`을 넘지 않는 선"이었는데 수량에 상한이 없어 성립하지 않았다 —
  보유량은 우편·상자로 `MaxStack`을 넘을 수 있다. 등록 화면 수량 입력은 `GetMaxCount` = min(보유량, 9,999)로 자른다.
  넘기면 서버가 `AuctionInvalidRequest`로 거절한다.
  하한(즉시 판매가)은 그대로다 — 즉시 판매보다 싸게 올리면 되파는 길이 열린다.
- (옛) **가격 밴드는 유지한다(2026-09-30 태웅 결정).** 근거는 기획 [거래](../../../../GameDesign/design/trade/README.md) 3.4 시세 조작 대응 — 서버 `AuctionRules.IsInBand`가 판정한다.
  클라의 식(`AuctionModel.MinUnitPrice`·`MaxUnitPrice`·`InstantSellPrice`)은 **표시용 사본**이다. 서버 식이 바뀌면 함께 고친다.
- 인벤토리 툴팁은 `즉시 판매가` · `경매 등록가` 두 줄을, 판매 목록은 "즉시 판매 N G"를, 등록 화면은 안내문·줄·힌트·확인 창마다 두 값을 나란히 적는다.
- 상한은 `Constants.AuctionMaxUnitPrice`에서 읽는다(`AuctionModel.FormatMaxUnitPrice` — "1조") — 숫자를 코드에 박지 않는다(씬의 안내문 초기 문구는 코드가 덮어쓴다).
- **고른 줄은 버튼 색으로도 말한다** — 등록·거래소의 [선택] 버튼은 **탭과 같은 두 색**이다: 고름 = `ButtonSelected`(주황) "선택됨" · 안 고름 = `Button`(파랑) "선택"
  (`AuctionRowContent.Selectable`·`Selected` → `AuctionRowView.PaintSelection`). 구매·취소 줄은 프리팹 색(`ButtonPrimary`) 그대로.
  ⚠️ 고른 줄만 칠하면 안 보인다 — 프리팹의 `ButtonPrimary`와 `ButtonSelected`가 거의 같은 주황이다.

### 입력칸 · 정렬 · 내 매물

- **넘치게 넣으면 최대값으로 고친다**(`AuctionInput`) — 등록 수량(보유량) · 등록 단가(상한) · 거래소 구매 수량(판매 중 - 내 매물, 1회 상한).
  위로 넘친 값은 입력 중에 바로, 아래로 모자란 값(0·하한 미만)은 입력이 끝날 때 고친다 — 하한 150을 치려고 '1'을 누른 순간 올리면 이어 칠 수 없다.
- **[가격 정렬]은 로컬 사본만 정렬한다**(`AuctionSort`) — **낮은 가격순(기본) ↔ 높은 가격순** 두 가지. 받는 즉시 지금 방향으로 줄 세운다. 남의 매물이라 저장할 것이 없다.
  ("받은 순"은 2026-10-01 뺐다 — 사용자에게 뜻이 없는 기준이었다.)
  ⚠️ 장비 검색은 **받은 페이지 안에서만** 정렬된다 — 서버가 싼 것부터 주므로 높은 가격순은 [더 보기]를 끝까지 받아야 전체 순서가 된다. 거래소 자원 목록·내 매물은 한 번에 전부 온다.
  ⚠️ 모델의 원본 순서를 바꾸지 않는다 — 장비 [더 보기]가 원본의 마지막 매물을 커서로 쓴다. 거래소는 매물 없는 줄이 방향과 상관없이 뒤로 간다.
- **내 매물은 목록에 남기되 `[내 매물]`로 표시하고 [구매]를 잠근다** — 내 물건이 가격대 어디쯤 있는지 보이게. 거래소 목록·가격대에는 "내 매물 N개"를 붙인다.
  매물 정보에 판매자가 없어서 **내 매물 목록**(`AuctionModel.IsMine`)으로 가린다 — 구매 화면을 열 때마다 조용히(대기 없이) 받는다. 빈도 제한 밖이다.
  거래소 구매는 서버가 내 매물을 건너뛰므로 살 수 있는 수량에서도 뺀다.
- **판매자 줄**은 서버가 준 이름이다(내 것은 `나`) — [이슈 #47](https://github.com/JeongTaeWoong99/Windows_simulator/issues/47).
- **정렬 버튼 글자는 "받은 것 중 낮은 가격순"** 이다 — 장비·캐릭터 검색은 받은 페이지 안에서만 정렬되기 때문이다(아래 ⚠️).
- **목록 위에 "n분 전에 받음"** (`Received Text`) — 조회가 빈도 제한 때문에 자동으로 안 돌아서, 본 목록이 언제 것인지 말한다.

### 줄 · 툴팁 · 문구 (2026-10-01)

- **줄 왼쪽에 아이콘 칸**(`ItemIconView` — 등급 바탕 · 이름 첫 글자 · 수량 · 장비는 능력치 칸)이 선다. 🎨 아이콘 그림은 아직 없다.
- **줄은 요약, 툴팁은 전부** — 줄에 마우스를 올리면 등급·종류·기본 능력치·가격·남은 시간·판매자·**가격 기준**(즉시 판매가 · 경매 등록가)·능력치 칸이 뜬다.
  매물 줄 툴팁은 `AuctionText.Build*Tooltip`, 장비 개체 툴팁은 인벤토리와 같은 `EquipLabel.BuildTooltip`이다.
- **글자는 색으로 가른다**(`UIRichText`) — 라벨은 흐린 색(TextSub), 가격은 강조색(Highlight), 모자란 골드·"구매 진행 중"은 Negative, 내 매물은 Highlight.
  색 값을 문구에 박지 않는다 — 팔레트 역할에서 읽는다.
- 거래소 가격대는 `<pos>` 열 맞춤(단가 · 판매 수량 · 내 매물), 등록 힌트는 **단가 범위 / 경매 총액·등록비 / 즉시 판매하면** 세 줄이다.

### 그 밖의 규칙

- **요청은 `AuctionModel`이 보낸다** — 다른 화면과 다르다. 검색 커서·누적 결과가 모델에 있어야 해서다([`Managers 규칙.md`](<../../Managers/Managers 규칙.md>)).
  대기(로딩·실패 알림)는 여전히 각 Presenter가 건다.
- **산 물건·판매 대금·취소 반환은 전부 우편**이다. 거래 응답에는 물건이 실리지 않는다 — 성공 알림이 "우편함에서 받아 주세요"로 끝나는 이유.
- ⚠️ **조회에 빈도 제한이 있다** — 거래소 목록·가격대·장비 검색이 한 통(순간 5회 · 2초마다 1회 회복). 그래서 거래소 목록과
  장비 검색은 **축을 처음 열 때만** 자동으로 받고 이후엔 [검색]으로 받는다. 오갈 때마다 받으면 `AuctionTooManyRequests`가 뜬다.
  내 매물 조회는 제한에 들지 않아 열 때마다 받는다.
- ⚠️ **이름 검색에 맞는 것이 없으면 요청을 보내지 않는다.** 빈 TID 목록은 서버가 "조건 없음"으로 읽어 전체를 돌려준다.
- **능력치 칸 표기는 옛 인챈트 필드로 먼저 붙였다**(2026-10-01) — 매물 줄 `능력치 n/N` · 아이콘의 칸 네모 · 툴팁의 칸 목록. 읽는 곳은 `EquipLabel.ReadStatOptions` 한 곳이다.
  검색 필터는 **인챈트 등급 드롭다운 하나**다(2026-10-03 · [이슈 #46](https://github.com/JeongTaeWoong99/Windows_simulator/issues/46)) — 옵션 다중 선택은 매물이 쌓인 뒤에.
- 처음에는 구매 › 자원 화면만 켜 둔다. 나머지가 켜진 채 저장되면 경매장을 처음 열 때 각자 조회를 보낸다.
- `AuctionTab`·`AuctionBuyKind`는 씬에 int로 저장된다 — 값을 끼우거나 재정렬하면 씬 배선을 함께 고친다.

---
