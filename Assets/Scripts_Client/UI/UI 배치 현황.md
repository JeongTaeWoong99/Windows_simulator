# UI 배치 현황

> 최종 업데이트: 2026-09-05 (수량 팝업이 !System Canvas로 이사 · 차단막 색 통일) · 대상: `Assets/Scenes/Original/`

**지금 씬에 무엇이 어떻게 놓여 있는가**의 스냅샷이다.
규칙이 아니라 **현황**이라, 씬을 고치면 여기도 함께 갱신한다.

> ⚠️ **씬을 고치면 이 문서를 갱신한다.** 안 하면 다음 사람이 없는 오브젝트를 찾는다.

**규칙(이름·배치·폴더·함정)은 [`UI 규칙.md`](<UI 규칙.md>)에 있다** — 이 문서에는 두지 않는다.

| 무엇을 알고 싶나 | 절 |
|---|---|
| 씬에 어떤 캔버스·Presenter가 있나 | 1. 오브젝트 트리 |
| 메인 화면 셋이 어떻게 갈리나 | 2. 메인 화면 전환 흐름 |
| 작업슬롯 화면은 어떻게 이어지나 | 3. 작업슬롯 화면 흐름 |
| 아직 안 끝난 것 · 임시로 둔 것 | 4. 알려진 임시 상태 |

---

## 1. 오브젝트 트리 (2026-08-26)
**캔버스 = `(MAIN VIEW)`, 그 자식 = `Xxx Presenter (↓ …)`** 가 예외 없이 지켜진다.
`Panel`이라는 이름은 **Presenter 안쪽에서 서브 뷰를 줄 세우는 상자**에만 남아 있다.

> Root Canvas 바로 아래의 **형제 순서는 씬에서 `!System` → `!Login` → `!Horizental Columns`** 다.
> 아래 트리는 읽기 좋게 위에서부터 늘어놓았을 뿐이다 — 이 셋은 각자 `Override Sorting`을 켜고
> Sorting Order로 앞뒤가 정해지므로 **형제 순서가 그림 순서를 바꾸지 않는다**.
> 형제 순서가 곧 앞뒤인 것은 `!System Canvas` **안쪽의 오버레이 셋**이다(아래 주석).

```
Root Canvas
├─ !Login Canvas (MAIN VIEW)                      LoginCanvasView      ← 게임의 시작점
│  └─ Login Presenter (↓ SUB VIEW)                LoginPresenter
├─ !Horizental Columns                            WidgetPositionLayout ← 항상 켜져 있어야 한다
│  ├─ @Storage Column                              -(Layout) · 캔버스 · -(Layout) 세 칸
│  │  ├─ -(Layout)                                 위 스페이서            pref 43/87 ← 계산됨
│  │  ├─ #Storage Canvas (MAIN VIEW)              StorageCanvasView   pref 950 · flexH 0
│  │  │  ├─ Title                                 (정적 요소 — 표기 없음)
│  │  │  ├─ Tab Presenter (↓ SUB VIEW)            StorageTabPresenter   자원·캐릭터·장비·특성 순
│  │  │  ├─ Grid Presenter (↓ SUB VIEW)           StorageGridPresenter  탭이 무엇이든 이 격자가 그린다
│  │  │  │  └─ Content > Slot (1..200)            빈 프레임. 그 안에 런타임 생성:
│  │  │  │     └─ InventorySlotView 프리팹        Sell Mark 는 담겼을 때만 켜진다
│  │  │  ├─ Information Presenter (↓ SUB VIEW)    StorageInformationPresenter  ← 지금은 판매 목록
│  │  │  │  ├─ Sell Scroll View Panel             Content 에 SellCartRowView 프리팹이 쌓인다
│  │  │  │  │  └─ Empty Text (TMP)                목록 위에 겹쳐 둔다 (담긴 게 없을 때만)
│  │  │  │  ├─ Summary Panel                      High Rarity Warning(평소 꺼짐) · Total Text
│  │  │  │  └─ Sell Button                        [판매]
│  │  └─ -(Layout)                                 아래 스페이서          pref 87/43 ← 계산됨
│  ├─ @Main Column                                 세 칸 전부 높이 고정 (43+950+87 = 1080)
│  │  ├─ #State Canvas (MAIN VIEW)                StateCanvasView    pref 43 · flexH 0 ← 계산됨
│  │  │  └─ State Presenter (↓ SUB VIEW)          StatePresenter
│  │  │     ├─ Nick / Gold / Dia Panel               정렬 상자 (아이콘 + 텍스트)  flex 4씩
│  │  │     └─ Setting Button · xxx Button (1..3) · Exit Button      flex 1씩
│  │  ├─ #Main Canvas (MAIN VIEW)                 MainCanvasView     pref 950 · flexH 0 ← 사람이 정함
│  │  │  ├─ Title                                 문구만 바뀐다 (SetTitle)      pref  50
│  │  │  ├─ WorkStation List Presenter (↓ SUB VIEW)    WorkStationListPresenter   [기본]
│  │  │  │  └─ Content > Work Slot (0..7)         WorkSlotFrame 프리팹 [Button]
│  │  │  │     └─ WorkStationSlotView 프리팹 (배치된 칸에만 런타임 생성)
│  │  │  ├─ WorkStation Select Presenter (↓ SUB VIEW)  WorkStationSelectPresenter (평소 꺼짐)
│  │  │  │  ├─ Header Panel · Industry Panel      (정렬용 — 스크립트 없음)
│  │  │  │  ├─ Character Assign Scroll View Panel
│  │  │  │  │  └─ Content > Character State Row ×21
│  │  │  │  └─ Character Setting Panel
│  │  │  ├─ Setting Presenter (↓ SUB VIEW)        SettingPresenter            (평소 꺼짐)
│  │  │  │  ├─ Header Panel                       뒤로가기 (Select 와 같은 규격)  pref 50
│  │  │  │  ├─ Toggle Panel                       토글 4              pref 0 · flexH 1
│  │  │  │  └─ Dropdown Panel                     드롭다운 3          pref 0 · flexH 1
│  │  │  └─ Menu Presenter (↓ SUB VIEW)           MenuPresenter    창고·거래 버튼  pref 100
│  │  └─ #Widget Canvas (MAIN VIEW)               WidgetCanvasView   pref  87 · flexH 0 · 상주
│  │     └─ Widget Presenter (↓ SUB VIEW)         WidgetPresenter    세로 2줄 + 버튼
│  │        ├─ Top Panel                          (정렬용 — 스크립트 없음)       pref  26
│  │        │  └─ Gold · Active Slot · Per Hour · Total  뒤의 둘은 더미 문구(미연결)
│  │        ├─ Strip Panel                        (정렬용) 왼쪽 정렬 · flexH 1
│  │        │  └─ WidgetMiniSlotView 프리팹 (배치된 칸만 런타임 생성)
│  │        └─ Open/Close Button                  ignoreLayout · 우하단 40×40
│  └─ @Market Column                               창고 열과 같은 세 칸 구성
│     ├─ -(Layout)                                 위 스페이서            pref 43/87 ← 계산됨
│     ├─ #Market Canvas (MAIN VIEW)               MarketCanvasView   pref 950 · flexH 0
│     │  ├─ Title                                 (정적 요소 — 표기 없음)
│     │  └─ Gacha Presenter (↓ SUB VIEW)          GachaPresenter   Draws 4줄 (풀 2종 x 1회·10회)
│     │     └─ Gacha Send Button (0..3)           자원 1·10회 · 캐릭터 1·10회 순.
│     │                                           이름·비용 문구는 Start 가 테이블에서 채운다
│     └─ -(Layout)                                 아래 스페이서          pref 87/43 ← 계산됨
│
└─ !System Canvas (MAIN VIEW)                     SystemCanvasView   Sorting 2 · 상주 오버레이
   ├─ Loading Presenter (↓ SUB VIEW)              LoadingPresenter   차단 즉시 · 표시만 0.15s 뒤
   ├─ GachaResult Presenter (↓ SUB VIEW)          GachaResultPresenter  CanvasGroup 토글 · 5열 x n
   │  └─ Panel                                    제목 · Content(5열 그리드) · 닫기 버튼
   ├─ AmountInput Presenter (↓ SUB VIEW)          AmountInputPresenter  UIManager.AskAmount 가 연다
   │  └─ Panel                                    440x240 (제목 · 수량 입력 · 확인/취소)
   └─ Notice Presenter (↓ SUB VIEW)               NoticePresenter    CanvasGroup 토글 · 닫기=확인/종료
      └─ Panel                                    다이얼로그(문구 · 닫기 버튼)

(캔버스 밖)
Window Manager · UI Manager · Ping Manager · Network Manager · ServerWait Manager
PlayerData (MODEL)                                PlayerDataModel
SellCart (MODEL)                                  SellCartModel   판매 목록. 서버 상태가 아니다
```

> **"← 계산됨" 칸은 인스펙터에서 고쳐도 소용없다.** `WidgetPositionLayout`이 열 높이(1080)에서
> 가운데 950을 뺀 나머지를 **위젯 쪽 2 : 상태 쪽 1**로 나눠 매번 덮어쓴다. 위젯이 위 칸이면
> 위쪽이 87, 아래 칸이면 아래쪽이 87이다. **사람이 정하는 건 가운데 950 하나뿐이다**
> — 근거는 [`Layout 규칙.md`](<Layout/Layout 규칙.md>)의 "비율은 flexible이 아니라 숫자로".

> ⚠️ `!System Canvas`의 SUB VIEW 넷은 **다른 캔버스와 달리 `SetActive`가 아니라 `CanvasGroup`으로**
> 여닫는다 — 상주 캔버스라 자기를 끄면 `Start`가 돌지 않거나 다시 켤 이벤트를 못 받는다.
> 각 SUB VIEW 오브젝트가 스크립트 + `CanvasGroup` + 전체화면 blocker `Image`(raycastTarget 켬)를
> 함께 갖는다. 차단막 색은 셋이 **검정 a 0.35**, `Loading`만 **흰색 a 0.851**(축이 다르다).
> **여기만 다른 게 아니라 기준이 있다** — 판별 축이 둘(차단 범위 / 생명주기)이라는 것과
> 전부 통일하면 안 되는 이유는 [`System 규칙.md`](<System/System 규칙.md>).
>
> **`Notice Presenter`는 형제 순서에서 항상 마지막이다** — 나중에 올수록 위에 그려지고,
> 알림은 무엇에도 가려지면 안 된다. 오버레이를 새로 넣을 때는 그 앞에 끼운다.

## 2. 메인 화면 전환 흐름 — 셋이 한 자리를 나눈다

```
                    ┌──────────────── #Main Canvas ────────────────┐
                    │  Title                       (문구가 바뀐다) │
                    │  ┌────────────────────────────────────────┐  │
   State Presenter  │  │  WorkStation List Presenter   [기본]   │  │
   [Setting] ──────►│  │  WorkStation Select Presenter          │  │  ← 셋 중 하나
   List 의 칸 ─────►│  │  Setting Presenter                     │  │
                    │  └────────────────────────────────────────┘  │
                    │  Menu Presenter              (항상 켜져 있다)│
                    └──────────────────────────────────────────────┘

[Setting] 을 누른다        ToggleMainScreen(Setting)      → Setting 켜짐 · 제목 "Setting"
같은 버튼을 다시 누른다     이미 Setting 이므로 기본으로   → List 켜짐 · 제목 "WorkStation List"
칸을 누른다                 Open(i) → ShowMainScreen(Select) → Select 켜짐 · 제목 "WorkStation Select"
[뒤로] · 요청 실패          ShowMainScreen(List)           → List 켜짐
```

**나가는 길은 `Header Panel`의 뒤로가기로 통일했다.** `WorkStation Select`와 `Setting`이 같은 규격의
헤더(높이 50 · 버튼 오른쪽)를 쓴다. `Setting`엔 제목을 두지 않는다 — 캔버스 `Title`이 이미 "Setting"이다.

```

위젯 [열기/닫기] 로 전부 접었다 다시 열면
       │ CloseAllExceptWidget 이 #Main Canvas 까지 끄고 ResetMainScreen 을 돌려 놨다
       ▼
  언제나 WorkStation List

게임을 처음 시작하면
       │ UIManager.Start > ResetMainScreen  — 씬에 무엇이 켜진 채 저장됐든 무시한다
       ▼
  언제나 WorkStation List
```

마지막 두 줄이 중요하다 — 안 되돌리면 위젯 버튼이 "게임을 연다"가 아니라
**"마지막에 보던 걸 연다"**가 되고, 작업하다 설정을 켜 둔 채 저장한 씬은
**설정 화면으로 게임이 시작한다.**

> ⚠️ `ResetMainScreen`은 **캔버스를 켜지 않는다.** 여기서 `mainCanvas.Show(true)`를 하면
> 로그인 전에 게임 화면이 비친다. 안쪽만 정리해 두면 나중에 캔버스가 켜지는 순간
> 이미 올바른 화면이 떠 있다.

**제목은 `UI Manager > Main Screens`의 각 줄에 적혀 있다** — 코드에 없다
([`Main 규칙.md`](<Main/Main 규칙.md>)의 "캔버스 머리의 제목은 UIManager가 밀어 넣는다").

## 3. 작업슬롯 화면 흐름

```
1  WorkStation List Presenter        칸 8개
       │ 칸을 누른다 → Open(slotIndex) → ShowMainScreen(WorkStationSelect)
       │
       ├── 빈 칸 ──────────────→ 2
       └── 이미 배치된 칸 ──────→ 3

   ┌─ WorkStation Select Presenter ─ Header(뒤로가기) · Industry(산업 5개) 는 2·3 모두에서 보인다 ─┐
   │                                                                                             │
2  │  Character Assign Scroll View Panel    캐릭터 줄 목록                                       │
   │      │ 줄의 [배치] → 배치 요청 → 응답 성공 ──→ 3                                            │
   │      │                                                                                      │
3  │  Character Setting Panel               배치된 캐릭터 세팅                                   │
   │      │ [해제] → 해제 요청 → 응답 성공 ──────→ 2                                             │
   └──────┴─ [뒤로가기] 또는 응답 실패 ───────────→ 1 ───────────────────────────────────────────┘
```

2·3은 **한 Presenter 안의 단계**다(정렬용 패널을 켜고 끈다). 1↔2·3 은 **화면 전환**이라
`UIManager`를 거친다 — **경계가 어디인지가 이름에 드러난다.**

**요청이 성공한 뒤에 슬롯 목록으로 튕기지 않는다.** 배치했으면 이어서 세팅할 것이고, 해제했으면
이어서 다른 캐릭터를 고를 것이기 때문이다.

**단계는 응답을 보고 정한다.** 누르자마자 넘어가면 서버가 거절해도 넘어간다 — 아직 열리지 않은
슬롯에 배치를 걸면 실제로는 아무 일도 없는데 세팅 화면이 뜬다. 그래서 `WorkStationAssignCompleted`를
기다렸다가 성공이면 다음 단계로, **실패면 슬롯 목록으로 물러난다.**

> 기다리는 동안 `ApplyWaitingLock`이 잠그는 것은 **해제 버튼과 캐릭터 줄**뿐이다 — 뒤로가기는
> 코드로 잠그지 않는다. 다만 **요청을 보낸 순간부터** 로딩 오버레이가 클릭을 막으므로
> (보이기 전에도 막는다 — [`System 규칙.md`](<System/System 규칙.md>)의 "로딩은 두 축을 나눠 쓴다") 그동안은 뒤로가기도 실제로는 누를 수 없다.
> **응답이 영영 안 와도 갇히지 않는 근거는 5초 타임아웃이다** — `ServerWaitManager`가 대기를
> 스스로 닫고(`onClosed`) 잠금을 풀면서 무응답 알림을 띄운다.

산업 버튼은 두 단계 모두에서 **잠기지 않는다.** 2에서는 캐릭터를 걸러 보는 수단이고,
3에서는 다른 산업으로 갈아 끼우는 수단이라서다. 고른 것은 `interactable`이 아니라
**`colors.normalColor`로만** 표시한다 — `Selectable`이 실행 중 `Image.color`를 덮어쓰기 때문이다.

## 4. 알려진 임시 상태

- **`Character State Row`의 여백은 `5 5 5 5`다.** 예전에 "여기만 `1`이다"라고 적혀 있었는데
  **씬은 이미 5로 돌아와 있다** — 낡은 메모라 지웠다(2026-09-04 실측).
  줄이 눌리면 여백이 아니라 **줄 높이**를 본다.
- **`Character State Row`를 씬에 21줄 깔아 두고 풀처럼 쓴다.** 보유 캐릭터 수만큼만 켜고
  나머지는 끈다. 캐릭터가 21을 넘으면 그때 줄을 프리팹으로 뺀다.
- **산업 버튼 5개는 고를 뿐 캐릭터를 걸러 내지 않는다.** 고른 산업이 배치 요청에 실릴 뿐,
  그 산업을 못 다루는 캐릭터도 목록에 그대로 뜬다.
- **창고 탭은 화면 왼쪽부터 `자원 · 캐릭터 · 장비 · 특성`이고,
  `StorageTab` enum과 인스펙터 `Tabs` 배열도 같은 순서다.** 셋을 나란히 맞춰 둔다 —
  ⚠️ **enum을 재정렬할 때는 씬 배선을 함께 고친다**(씬에 int로 저장된다).
- **자원·캐릭터 둘이 실재한다** (기본 탭은 자원). 장비·특성은 **버튼이 잠겨 있다** —
  데이터가 없어서고, 잠금은 `StorageGridPresenter.HasSource`에서 파생된다(탭 줄이 따로 적지 않는다).
  무엇을 기다리는지는 `tasks/T-043-창고장비특성탭.md`.
- **탭이 달라도 격자는 하나다.** `Grid Presenter`가 칸 200개를 쥐고 공급자만 갈아 끼운다 —
  구조와 "탭 하나를 채우는 절차"는 [`Storage 규칙.md`](<Storage/Storage 규칙.md>).
- **`xxx Button (1)`~`(4)`(상태 패널)는 아직 열 화면이 없다.** `Screen Buttons` 배열에 넣지 않았다.
- **`Title`만 Presenter 없이 캔버스 직속이다.** `#Main Canvas`의 것만 문구가 바뀌고
  (`MainCanvasView.SetTitle`), 창고·거래의 것은 고정이다. 어느 쪽이든 표기는 붙이지 않는다.
- ⚠️ **`Information Presenter`는 이름과 역할이 어긋나 있다.** 지금 그리는 것은 아이템 상세가
  아니라 **판매 목록**이라 `SellCartPresenter`가 맞다 — 씬 오브젝트 이름·문서가 함께 가는
  개명이라 미뤘다. **좌클릭 상세 표시는 그 자리를 내주고 사라진 상태**이고, 되살릴지는 정하지 않았다.
- **판매는 자원 탭에서만 된다.** 서버 판매 패킷이 아이템 TID 축이라 캐릭터를 담을 수 없어서,
  격자가 다른 탭의 우클릭을 무시한다 — 근거는 [`Storage 규칙.md`](<Storage/Storage 규칙.md>).
- 🔴 **수량 팝업은 `#Storage Canvas`에 있다가 `!System Canvas`로 옮겼다 (2026-09-05).**
  열 캔버스는 넷 다 Sorting Order가 **0인 형제**라, 창고 안에 깐 차단막이 다른 열에 닿지 않아
  **확인을 누르기 전인데 상태바·메인·거래 버튼이 눌렸다.** 옮기면서 이름도
  `AmountInput Presenter`로 바꾸고(판매 전용이 아니다) 차단막 겹 하나를 걷어냈다.
  창고는 `UIManager.AskAmount(...)` 한 줄로 부르므로 팝업 참조를 들지 않는다.
- **`Sell Mark`는 임시 표시다** (주황 배지 + "판"). 아이콘 리소스가 생기면 갈아 끼운다.
- **스프라이트·여백을 씬 전체에서 한 번 맞췄다 (2026-09-04).** `Tab`·`Menu`·`Setting`·`Gacha`
  Presenter의 배경이 `null` → `Background`로, `Setting Presenter`의 여백이 `5` → `0`(패널을
  쌓기만 하는 자리)으로 바뀌었다. 기준은 [`UI 규칙.md`](<UI 규칙.md>) 3장의 여백·스프라이트 표다.
- **`WorkStation Select Presenter`는 상태 패널 버튼으로 못 연다.** 슬롯 번호가 있어야 열리는
  화면이라 `Screen Buttons`에 넣을 수 없다 — 목록의 칸 클릭만이 입구다.
- **`WorkStationListPresenter`에 `#region A-2 진단 (임시)`가 남아 있다.** 원인이 확정되면 통째로 지운다.
- **위젯 상단의 `Per Hour Text`·`Total Text`는 씬의 더미 문구가 그대로 보인다.** 산출 정의가
  기획에서 안 정해져 코드가 손대지 않는다 — 참조만 잡혀 있다. 정해지면 `WidgetPresenter`에 연결한다.
- **`WidgetMiniSlotView`의 `Character Image`는 회색 네모다.** 캐릭터 스프라이트가 없어 자리만
  잡아 뒀고, `Harvest Text (TMP)`도 빈 문자열이다. 지금 실제로 도는 것은 게이지 하나다.

> 씬의 `m_EditorClassIdentifier`에 옛 클래스 이름이 남아 있어도 **문제 없다.**
> 스크립트 연결은 GUID로 이뤄지고, 그 문자열은 다음 씬 저장 때 Unity가 갱신한다.
