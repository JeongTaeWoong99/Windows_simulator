# UI 배치 현황

> 최종 업데이트: 2026-08-20 (대기 중 잠금·탈출 경로를 5초 타임아웃 기준으로 정정) · 대상: `Assets/Scenes/Original/`

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

## 1. 오브젝트 트리 (2026-08-10)
위젯은 생략했다. **캔버스 = `(MAIN VIEW)`, 그 자식 = `Xxx Presenter (↓ …)`** 가 예외 없이 지켜진다.
`Panel`이라는 이름은 **Presenter 안쪽에서 서브 뷰를 줄 세우는 상자**에만 남아 있다.

```
Root Canvas
├─ !Login Canvas (MAIN VIEW)                      LoginCanvasView      ← 게임의 시작점
│  └─ Login Presenter (↓ SUB VIEW)                LoginPresenter
├─ !Horizental Columns                            WidgetPositionLayout ← 항상 켜져 있어야 한다
│  ├─ @Storage Column
│  │  └─ #Storage Canvas (MAIN VIEW)              StorageCanvasView
│  │     ├─ Title                                 (정적 요소 — 표기 없음)
│  │     ├─ Tab Presenter (↓ SUB VIEW)            StorageTabPresenter   탭 4개 (자원만 실재)
│  │     ├─ Inventory Presenter (↓ SUB VIEW)      InventoryPresenter
│  │     │  └─ Content > Slot (1..201)            빈 프레임. 그 안에 런타임 생성:
│  │     │     └─ InventorySlotView 프리팹
│  │     └─ Information Presenter (↓ SUB VIEW)    StorageInformationPresenter
│  ├─ @Main Column                                 세 칸 전부 높이 고정 (90+900+90 = 1080)
│  │  ├─ #State Canvas (MAIN VIEW)                StateCanvasView    pref 90 · flexH 0
│  │  │  └─ State Presenter (↓ SUB VIEW)          StatePresenter
│  │  │     └─ 이름 · 골드 · Setting Button · xxx Button (1..4)
│  │  ├─ #Main Canvas (MAIN VIEW)                 MainCanvasView     pref 900 · flexH 0
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
│  │  └─ #Widget Canvas (MAIN VIEW)               WidgetCanvasView   pref 90 · flexH 0 · 상주
│  │     └─ Widget Presenter (↓ SUB VIEW)         WidgetPresenter       열기/닫기 버튼
│  └─ @Market Column
│     └─ #Market Canvas (MAIN VIEW)               MarketCanvasView
│        ├─ Title                                 (정적 요소 — 표기 없음)
│        └─ Gacha Presenter (↓ SUB VIEW)          GachaPresenter
│
└─ !System Canvas (MAIN VIEW)                     SystemCanvasView   Sorting 300 · 상주 오버레이
   ├─ Loading Presenter (↓ SUB VIEW)              LoadingPresenter   CanvasGroup 토글 · 0.15s 지연 표시
   └─ Notice Presenter (↓ SUB VIEW)               NoticePresenter    CanvasGroup 토글 · 닫기=확인/종료
      └─ Panel                                    다이얼로그(문구 · 닫기 버튼)

(캔버스 밖)
Window Manager · UI Manager · Ping Manager · Network Manager · ServerWait Manager
PlayerData (MODEL)                                PlayerDataModel
Player Data Logger                                PlayerDataLogger
```

> ⚠️ `!System Canvas`의 두 SUB VIEW는 **다른 캔버스와 달리 `SetActive`가 아니라 `CanvasGroup`으로**
> 여닫는다(이벤트 구독 유지). 각 SUB VIEW 오브젝트가 스크립트 + `CanvasGroup` + 전체화면 blocker
> `Image`(alpha 0, raycastTarget)를 함께 갖는다 — 근거는 [`UI 규칙.md`](<UI 규칙.md>) §7.

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
([`UI 규칙.md` 3장](<UI 규칙.md>)).

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
> 코드로 잠그지 않는다. 다만 대기가 0.15초를 넘기면 **로딩 오버레이가 화면 전체를 덮으므로**
> 그동안은 뒤로가기도 실제로는 누를 수 없다.
> **응답이 영영 안 와도 갇히지 않는 근거는 5초 타임아웃이다** — `ServerWaitManager`가 대기를
> 스스로 닫고(`onClosed`) 잠금을 풀면서 무응답 알림을 띄운다.

산업 버튼은 두 단계 모두에서 **잠기지 않는다.** 2에서는 캐릭터를 걸러 보는 수단이고,
3에서는 다른 산업으로 갈아 끼우는 수단이라서다. 고른 것은 `interactable`이 아니라
**`colors.normalColor`로만** 표시한다 — `Selectable`이 실행 중 `Image.color`를 덮어쓰기 때문이다.

## 4. 알려진 임시 상태

- **`Character State Row`를 씬에 21줄 깔아 두고 풀처럼 쓴다.** 보유 캐릭터 수만큼만 켜고
  나머지는 끈다. 캐릭터가 21을 넘으면 그때 줄을 프리팹으로 뺀다.
- **산업 버튼 5개는 고를 뿐 캐릭터를 걸러 내지 않는다.** 고른 산업이 배치 요청에 실릴 뿐,
  그 산업을 못 다루는 캐릭터도 목록에 그대로 뜬다.
- **창고 탭 4개 중 자원 하나만 실재한다.** `StorageTabPresenter`가 나머지를 로그로만 알린다.
- **`xxx Button (1)`~`(4)`(상태 패널)는 아직 열 화면이 없다.** `Screen Buttons` 배열에 넣지 않았다.
- **`Title`만 Presenter 없이 캔버스 직속이다.** `#Main Canvas`의 것만 문구가 바뀌고
  (`MainCanvasView.SetTitle`), 창고·거래의 것은 고정이다. 어느 쪽이든 표기는 붙이지 않는다.
- **`Information Presenter`는 고를 수단(칸 클릭)이 아직 없어** 안내 문구만 띄운다.
  `InventorySlotView`에 `Clicked`가 붙으면 이어진다.
- **`WorkStation Select Presenter`는 상태 패널 버튼으로 못 연다.** 슬롯 번호가 있어야 열리는
  화면이라 `Screen Buttons`에 넣을 수 없다 — 목록의 칸 클릭만이 입구다.
- **`WorkStationListPresenter`에 `#region A-2 진단 (임시)`가 남아 있다.** 원인이 확정되면 통째로 지운다.

> 씬의 `m_EditorClassIdentifier`에 옛 클래스 이름이 남아 있어도 **문제 없다.**
> 스크립트 연결은 GUID로 이뤄지고, 그 문자열은 다음 씬 저장 때 Unity가 갱신한다.
