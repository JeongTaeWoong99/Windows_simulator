# UI 배치 현황

> 최종 업데이트: 2026-09-21 (계정 경험치가 가로 띠 → 닉 아이콘 원형 진행도 — T-068) · 대상: `Assets/Scenes/Original/`

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
│  │  │  ├─ Trait Presenter (↓ SUB VIEW)          TraitPresenter        특성 탭에서만 켜진다 (격자를 쓰지 않는 유일한 탭)
│  │  │  │  ├─ Trait Tab Panel                    [산업 속도] [산업 레벨]        pref 34
│  │  │  │  ├─ Point Text (TMP)                   "특성 포인트 n"                pref 22
│  │  │  │  └─ Scroll View Panel                  스크롤바 Permanent · flexH 1
│  │  │  │     └─ Content                         GridLayoutGroup 5열 = 산업. cell 106x58 · spacing 8x20
│  │  │  │        └─ TraitNodeView 프리팹 (노드 수만큼 런타임 생성 · 풀)
│  │  │  │           └─ Link Image                위 노드와 잇는 세로 선. 칸 위 20px(격자 간격)으로 뻗는다
│  │  │  ├─ Tool Presenter (↓ SUB VIEW)           StorageToolPresenter  pref 40 — 정렬 화살표 · -(Layout) · 등급 범위 드롭다운 · [판매]
│  │  │  │                                        특성 탭에서는 자식이 전부 꺼진다
│  │  │  ├─ Grid Presenter (↓ SUB VIEW)           StorageGridPresenter  자원·캐릭터·장비를 이 격자 하나가 그린다
│  │  │  │                                        특성 탭에서는 자기 오브젝트를 끈다
│  │  │  │  └─ Content > Slot (1..200)            빈 프레임. 그 안에 런타임 생성:
│  │  │  │     └─ SlotView 프리팹        Sell Mark(자원 탭) · Assign Mark(캐릭터 탭)
│  │  │  │        └─ Aptitude Strip               캐릭터 탭 전용 5칸. 보조 문구와 같은 밴드를 나눠 쓴다
│  │  │  ├─ Sell Cart Presenter (↓ SUB VIEW)      SellCartPresenter            판매 목록 · 합계 · [판매]
│  │  │  │                                        특성 탭에서는 자기 오브젝트를 끈다
│  │  │  │  ├─ Sell Scroll View Panel             Content 에 SellCartRowView 프리팹이 쌓인다
│  │  │  │  │  └─ Empty Text (TMP)                목록 위에 겹쳐 둔다 (담긴 게 없을 때만)
│  │  │  │  ├─ Summary Panel                      High Rarity Warning(평소 꺼짐) · Total Text
│  │  │  │  └─ Sell Button                        [판매]
│  │  └─ -(Layout)                                 아래 스페이서          pref 87/43 ← 계산됨
│  ├─ @Main Column                                 세 칸 전부 높이 고정 (43+950+87 = 1080)
│  │  ├─ #State Canvas (MAIN VIEW)                StateCanvasView    pref 43 · flexH 0 ← 계산됨
│  │  │  └─ State Presenter (↓ SUB VIEW)          StatePresenter     가로 한 줄
│  │  │     ├─ Nick / Gold / Dia Panel               정렬 상자 (아이콘 + 텍스트)  flex 4씩
│  │  │     │  └─ Nick Image                      검은 원반(Knob). 계정 레벨이 여기 겹친다
│  │  │     │     ├─ Exp Ring Fill                Filled · Radial360 — 계정 경험치
│  │  │     │     ├─ Nick Icon (임시)             가운데를 덮어 원반을 고리로 만든다 (T-054 대기)
│  │  │     │     ├─ Percent Text                 ⏸ 꺼 둔 상태 — 배선만 살아 있다
│  │  │     │     └─ Lv Text                      "Lv.n" (가장 위)
│  │  │     └─ Setting Button · xxx Button (1..3 ⏸) · Exit Button      flex 1씩
│  │  ├─ #Main Canvas (MAIN VIEW)                 MainCanvasView     pref 950 · flexH 0 ← 사람이 정함
│  │  │  ├─ Title                                 문구만 바뀐다 (SetTitle)      pref  50
│  │  │  ├─ WorkStation List Presenter (↓ SUB VIEW)    WorkStationListPresenter   [기본]
│  │  │  │  └─ Content > Work Slot (0..7)         WorkSlotFrame 프리팹 [Button]
│  │  │  │     └─ WorkStationSlotView 프리팹 (배치된 칸에만 런타임 생성)
│  │  │  ├─ WorkStation Select Presenter (↓ SUB VIEW)  WorkStationSelectPresenter (평소 꺼짐)
│  │  │  │  ├─ Header Panel                       (정렬용 — 스크립트 없음)       pref 50
│  │  │  │  ├─ Industry Panel                     산업 5개                       pref 90
│  │  │  │  │  └─ Farming … Hunting Button        VLG → Icon (임시) 흰 네모 · Text (TMP)
│  │  │  │  ├─ Industry Level Panel               레벨 5개                       pref 32
│  │  │  │  │  └─ Level 1..5 Button               라벨은 코드가 채운다 ("Lv2 밭"). 안 연 레벨은 회색·비활성
│  │  │  │  ├─ Character Assign Scroll View Panel
│  │  │  │  │  ├─ Content > CharacterStateRowView 프리팹 (보일 수만큼 런타임 생성)
│  │  │  │  │  │    Portrait (임시) · 이름 / 종족 (임시) / 적성 5칸 · [배치]      pref 90
│  │  │  │  │  └─ Empty Text (TMP)                목록 위에 겹쳐 둔다 (고를 것이 없을 때만)
│  │  │  │  └─ Character Setting Panel            VLG ctrl on · exp off → 자식 모두 flexW 1
│  │  │  │     ├─ Character Label                 "캐릭터"                         pref 30
│  │  │  │     ├─ Assigned Character Card         CharacterStateRowView 프리팹 · [해제]  pref 90
│  │  │  │     ├─ Equipment Label                 "장비"                           pref 30
│  │  │  │     ├─ Equipment Panel                 무기 · 장신구 1·2 · 보석 (임시 — T-002)  pref 110
│  │  │  │     ├─ Efficiency Label                "효율 계산"                      pref 30
│  │  │  │     └─ Efficiency Scroll View Panel    위 스크롤 뷰와 같은 설정 · 스크롤바 Permanent  flexH 1
│  │  │  │        └─ Content > EfficiencyRowView 프리팹 (줄 수만큼 런타임 생성)
│  │  │  ├─ Setting Presenter (↓ SUB VIEW)        SettingPresenter            (평소 꺼짐)
│  │  │  │  ├─ Header Panel                       뒤로가기 (Select 와 같은 규격)  pref 50
│  │  │  │  ├─ Toggle Panel                       토글 4              pref 0 · flexH 1
│  │  │  │  └─ Dropdown Panel                     드롭다운 5 (크기·위치·프레임·FPS 위치 + 미사용 1)          pref 0 · flexH 1
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
│     │  └─ Gacha Presenter (↓ SUB VIEW)          GachaPresenter   Draws 8줄 (풀 4종 x 1회·10회)
│     │     └─ Gacha Send Button (0..7)           캐릭터 · 무기 · 장신구 · 보석 순, 각 1·10회.
│     │                                           이름·비용 문구는 Start 가 테이블에서 채운다
│     │                                           ※ 구슬(아이템) 풀은 2026-09-20 화면에서 뺐다 → 이슈 #30
│     └─ -(Layout)                                 아래 스페이서          pref 87/43 ← 계산됨
│
└─ !System Canvas (MAIN VIEW)                     SystemCanvasView   Sorting 2 · 상주 오버레이
   ├─ Fps Text Presenter (↓ SUB VIEW)             FpsTextPresenter   구석 FPS 표시 · 차단막·CanvasGroup 없음
   │  └─ Text (TMP)                               회색 22 · raycastTarget 끔(클릭스루)
   ├─ Loading Presenter (↓ SUB VIEW)              LoadingPresenter   차단 즉시 · 표시만 0.15s 뒤
   ├─ Gacha Result Presenter (↓ SUB VIEW)          GachaResultPresenter  CanvasGroup 토글 · 5열 x n
   │                                               가챠·상자 개봉 공용 (T-033)
   │  └─ Panel                                    제목 · Content(5열 그리드) · 닫기 버튼
   ├─ Amount Input Presenter (↓ SUB VIEW)          AmountInputPresenter  UIManager.AskAmount 가 연다
   │  └─ Panel                                    440x240 (제목 · 수량 입력 · 확인/취소)
   ├─ Confirm Presenter (↓ SUB VIEW)               ConfirmPresenter   UIManager.AskConfirm 이 연다
   │  └─ Panel                                    440x240 (문구 · 확인/취소)
   └─ Notice Presenter (↓ SUB VIEW)               NoticePresenter    CanvasGroup 토글 · 닫기=확인/종료
      └─ Panel                                    다이얼로그(문구 · 닫기 버튼)

(캔버스 밖)
Window Manager · Display Manager · UI Manager · Ping Manager · Network Manager · ServerWait Manager
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

   ┌─ WorkStation Select Presenter ─ Header(뒤로가기) · Industry(산업 5개) · Industry Level(1~5) 은 2·3 모두 ─┐
   │                                                                                             │
2  │  Character Assign Scroll View Panel    캐릭터 줄 목록                                       │
   │      │ 줄의 [배치] → 배치 요청 → 응답 성공 ──→ 3                                            │
   │      │                                                                                      │
3  │  Character Setting Panel               배치된 캐릭터 카드 · 장비 · 효율 계산               │
   │      │ 카드의 [해제] → 해제 요청 → 응답 성공 ──────→ 2                                             │
   └──────┴─ [뒤로가기] 또는 응답 실패 ───────────→ 1 ───────────────────────────────────────────┘
```

2·3은 **한 Presenter 안의 단계**다(정렬용 패널을 켜고 끈다). 1↔2·3 은 **화면 전환**이라
`UIManager`를 거친다 — **경계가 어디인지가 이름에 드러난다.**

**요청이 성공한 뒤에 슬롯 목록으로 튕기지 않는다.** 배치했으면 이어서 세팅할 것이고, 해제했으면
이어서 다른 캐릭터를 고를 것이기 때문이다.

**단계는 응답을 보고 정한다.** 누르자마자 넘어가면 서버가 거절해도 넘어간다 — 아직 열리지 않은
슬롯에 배치를 걸면 실제로는 아무 일도 없는데 세팅 화면이 뜬다. 그래서 `WorkStationAssignCompleted`를
기다렸다가 성공이면 다음 단계로, **실패면 슬롯 목록으로 물러난다.**

> ⚠️ **산업 교체(3에서 산업 버튼)만 실패해도 물러나지 않는다.** 배치 실패는 대개 *열리지 않은
> 슬롯*이라 그 칸에서 배치도 해제도 할 수 없지만, 교체 실패는 **이미 열린 칸**에서 난다
> (적성 0 산업을 눌러 본 경우 등). 산업을 눌러 봤다는 이유로 화면이 튕기면 조작이 어렵다 —
> 알림만 띄우고 3에 남는다. 3의 성공·실패 모두 **제자리**다.

> 기다리는 동안 `ApplyWaitingLock`이 잠그는 것은 **카드의 해제 버튼과 캐릭터 줄**뿐이다 — 뒤로가기는
> 코드로 잠그지 않는다. 다만 **요청을 보낸 순간부터** 로딩 오버레이가 클릭을 막으므로
> (보이기 전에도 막는다 — [`System 규칙.md`](<System/System 규칙.md>)의 "로딩은 두 축을 나눠 쓴다") 그동안은 뒤로가기도 실제로는 누를 수 없다.
> **응답이 영영 안 와도 갇히지 않는 근거는 5초 타임아웃이다** — `ServerWaitManager`가 대기를
> 스스로 닫고(`onClosed`) 잠금을 풀면서 무응답 알림을 띄운다.

**2의 산업 버튼은 잠기지 않는다** — 캐릭터를 걸러 보는 수단이라, 못 하는 산업도 눌러 봐야
"이 산업을 다루는 캐릭터가 없다"를 볼 수 있다. **3은 배치된 캐릭터의 적성으로 잠근다**(2026-09-11).

**색이 셋이고 상태도 셋이다** (2026-09-11).

| 상태 | 색 | 어디서 칠하나 |
|------|-----|--------------|
| 눌림(고른 산업) | 노랑 `(0.839, 0.682, 0.067)` — 화면 테두리에 쓰는 금색 | `normal`·`highlighted`·`pressed`·`selected` **넷 다** |
| 안 눌림 + 고를 수 있음 | 하양 | `normal`·`highlighted`·`pressed`·`selected` **넷 다** |
| 잠김(적성 0 · 3단계만) | 회색 | **`colors.disabledColor`** |

⚠️ **네 상태를 같은 색으로 덮는다.** 기본값(`highlighted`·`selected` = 0.961 흰색)을 두면
**마우스를 올렸다는 이유로, 마지막에 눌렀다는 이유로** 색이 바뀐다 — 특히 `selected`는
EventSystem이 클릭한 버튼을 계속 잡고 있어 **고른 표시가 엉뚱한 버튼에 남는다.**
이 버튼의 색은 "고른 산업인가" 하나만 말해야 한다.

⚠️ **잠긴 버튼만은 `disabledColor`로 칠해진다** — 회색을 `normalColor`에 넣으면
잠근 순간 그 값이 무시되고 직전 색이 남는다.
`Image.color`가 아니라 `colors`를 바꾸는 이유는 `Selectable`이 실행 중 `Image.color`를 덮어쓰기 때문이다.

**같은 버튼인데 누른 결과가 단계마다 다르다.** 2에서는 화면이 값만 바꿔 캐릭터 줄을 다시 그리고,
3에서는 **배치 요청을 한 번 보내 산업을 갈아 끼운다**(`C_WorkStationAssignRequest` — 캐릭터는 배치된
그대로, 산업만 바꿔 보낸다). 해제 → 배치 2연발이 아니다: 서버가 정산을 두 번 돌리고 그 사이
빈 슬롯이 한 번 내려와 **칸이 깜빡인다.** 지금 배치된 것과 같은 산업을 다시 누르면 보내지 않는다.

> **3에서 켜진 불빛은 화면이 기억한 값이 아니라 슬롯에서 파생된다**
> (`SyncSelectedIndustryToSlot`). 눌러도 값을 미리 바꾸지 않으므로 **성공하면 슬롯 갱신이 새 산업을
> 켜고, 실패하면 원래 산업이 그대로 남는다** — 되돌리는 코드가 없다.
> 3의 산업 버튼은 **배치된 캐릭터가 적성 0인 산업을 잠근다**(`CanSelectIndustry` · 2026-09-11).
> 2가 적성 0인 캐릭터를 아예 걸러 내므로, 3만 열어 두면 같은 화면이 두 말을 하게 된다.

**산업 아래에 레벨 축이 하나 더 있다** (2026-09-20 · T-068). 같은 산업이라도 레벨이 오르면
판정 1회의 시간·경험치가 3배씩 늘고 **나오는 것이 통째로 바뀐다**(밀·감자 → 보리·고구마 → …).

- **레벨은 슬롯마다 따로다.** 전역이 아니라 `t_user_workstation_slot.industry_level`에 저장된다 —
  특성으로 Lv3을 열어도 **이미 배치된 칸은 그대로다.** 올리려면 그 칸에서 다시 고른다.
- 잠금은 `IsUnlocked(GetIndustryLevelUnlockTid(산업, 레벨))`에서 파생된다.
  **Lv1은 `UnlockTID = 0`이라 늘 열려 있다.** 해금은 창고 **특성 탭 → 산업 레벨**에서 한다.
- **산업이 바뀌면 레벨 줄을 다시 그리고, 잠긴 레벨이 골라져 있으면 Lv1로 되돌린다.**
  산업마다 열어 둔 레벨이 다르기 때문이다.
- 3에서 레벨 버튼을 누르면 **산업 교체와 똑같이 재배치 요청 1회**다(같은 레벨이면 보내지 않는다).
  실패해도 제자리고, 켜진 불빛은 슬롯의 `IndustryLevel`에서 파생된다 — 산업 버튼과 같은 축이다.
- ⚠️ **해제 요청에는 레벨을 `1`로 실어 보낸다.** 해제는 산업이 `None`으로 가는데
  `None`에는 Lv2 이상이 없어, 고른 레벨을 그대로 보내면 서버가 `IndustryLevelLocked`로 거절한다.

## 4. 알려진 임시 상태

- **작업슬롯 선택 화면의 `(임시)` 자리는 데이터·리소스를 기다린다 (2026-09-14 · T-053).**
  산업 탭 `Icon (임시)`·줄의 `Portrait (임시)`는 흰 네모이고 종족은 "종족 추가 예정"이다
  → [`T-054`](../../../tasks/T-054-종족과초상화.md). 장비 4칸은 "추가 예정" 문구만 있다 → [`T-002`](../../../tasks/archive/T-002-장비슬롯.md).
  목업의 **색은 따르지 않았다** — 지금 게임 팔레트다.
- 🔴 **효율 계산의 "개발용 전역 배수" 안내는 역산이라 이제 실제로 틀릴 수 있다** (`현재 작업속도 ÷ 적성 기본값`).
  2026-09-20부터 **특성 속도 가산을 찍을 수 있게 되어**, 찍은 몫까지 전역 배수로 보인다.
  클라에서 역산을 고칠 방법이 없다 — 서버가 배수를 명시해 주는
  [`T-055`](../../../tasks/T-055-속도보정내역전달.md)가 먼저다.
- **상태 패널의 `Nick Icon (임시)`도 회색 원이다** — 작업슬롯 줄과 같은 리소스를 기다린다
  → [`T-054`](../../../tasks/T-054-종족과초상화.md). ⚠️ 다만 이건 **자리표시자이면서 동시에 고리를
  만드는 부품**이라 비워 둘 수 없다 (위 항목).
- **세팅 카드는 목록 줄과 같은 `CharacterStateRowView` 프리팹이다.** 줄 모양을 고치면 카드도 바뀐다 —
  의도한 것이다. 버튼 라벨("배치"/"해제")만 코드가 넣는다.
- **선택 화면의 두 스크롤(캐릭터 목록·효율)은 스크롤바를 늘 보인다**(`Permanent` — 판매 목록과 같다).
  `AutoHideAndExpandViewport`로 두면 줄이 적을 때 스크롤바가 사라져 **빠진 것처럼 보였다.**
- **`Character State Row`의 여백은 `5 5 5 5`다.** 예전에 "여기만 `1`이다"라고 적혀 있었는데
  **씬은 이미 5로 돌아와 있다** — 낡은 메모라 지웠다(2026-09-04 실측).
  줄이 눌리면 여백이 아니라 **줄 높이**를 본다.
- ✅ **줄은 씬에 깔지 않는다 — 코드가 프리팹을 찍어 풀로 쓴다**(2026-09-11 · T-046).
  예전에는 "21줄 깔아 뒀다"고 적혀 있었는데 **씬에는 1줄뿐이라 16마리가 안 보였다.**
  프레임을 깔지 않는 이유는 **보유 수가 곧 줄 수**라 프레임 개수가 그대로 상한이 되기 때문이다
  (창고 격자와 다른 점 — 거기는 칸 200개가 고정이다).
- ✅ **산업 버튼을 누르면 목록이 걸러진다**(2026-09-11 · T-046). 그 산업의 적성이 0이거나
  **다른 슬롯에서 일하는 중**인 캐릭터는 빠진다. 고를 것이 하나도 없으면 `Empty Text (TMP)`가
  *"해당 적성을 가진 캐릭터가 없습니다."* 로 뜬다 — **판매 목록의 빈 안내와 같은 자리·같은 방식**이다.
  숨긴 캐릭터의 적성은 **창고 캐릭터 탭**이 보여 준다 — 칸 아래 **적성 스트립 5칸**이고
  **위치가 곧 산업**(농사·낚시·채굴·벌목·사냥)이며 적성 0은 `X`다(2026-09-12 · T-048).
- **창고 탭은 화면 왼쪽부터 `자원 · 캐릭터 · 장비 · 특성`이고,
  `StorageTab` enum과 인스펙터 `Tabs` 배열도 같은 순서다.** 셋을 나란히 맞춰 둔다 —
  ⚠️ **enum을 재정렬할 때는 씬 배선을 함께 고친다**(씬에 int로 저장된다).
- ✅ **네 탭이 모두 실재한다** (기본 탭은 자원 · 장비는 2026-09-19 · 특성은 2026-09-20 · T-043).
  잠금 판정은 `StorageTabPresenter.HasScreen` **한 곳**에서 갈린다 —
  셋은 격자가 공급자를 갖고 있는지로, **특성 하나는 전용 화면(`TraitPresenter`)이 배선돼 있는지로** 본다.
  - 장비 칸은 **이름 + 효과 한 줄**(`낚시 +30%`)이고, 캐릭터가 끼고 있으면 **'배' 마크**가 켜진다
    (캐릭터 탭의 배치 마크와 같은 표시다). 지금 장비를 넣는 경로는 **치트 창의 `장비 지급`뿐**이다 —
    뽑기는 서버·엑셀 선행(`tasks/archive/T-067-장비뽑기.md`).
- **탭이 달라도 격자는 하나다 — 단 특성 탭은 예외다.** `Grid Presenter`가 칸 200개를 쥐고 공급자만
  갈아 끼우지만, 특성은 칸 목록이 아니라 선으로 이어진 트리라 `Trait Presenter`가 따로 그린다.
  구조와 "탭 하나를 채우는 절차"는 [`Storage 규칙.md`](<Storage/Storage 규칙.md>).
- **특성 탭에서 꺼지는 것은 셋이다** — 도구 줄 · 격자 · 판매 목록. **각자 스스로 끈다**
  (탭 줄이 목록을 들고 있지 않다). 셋 다 `TabChanged` 구독을 **`Start`/`OnDestroy`** 에 건다 —
  `OnDisable`에서 풀면 자기를 끈 순간 **다시 켤 신호를 받을 길이 사라진다.**
- **상태 패널의 계정 경험치는 `Nick Image`를 두르는 원형 진행도다**(2026-09-21 · `Image.fillAmount` · Slider가 아니다).
  처음엔 줄 아래 가로 띠였는데 43px을 위아래로 나누니 재화 줄이 눌려서, **줄을 건드리지 않고 아이콘 안으로** 옮겼다.
  🔴 **고리 스프라이트가 없어 원반 두 장 + 가운데를 덮는 `Nick Icon (임시)`으로 만든다** —
  그 칸을 지우면 진행도가 고리가 아니라 파이 차트가 된다(근거는 [`State 규칙.md`](<State/State 규칙.md>)).
  **남은 특성 포인트는 상태 패널이 아니라 특성 화면 머리**(`Point Text`)에 있다 — 43px에 더 넣으면 배치가 깨진다.
- **특성 노드 45개를 씬에 깔지 않는다** — `TraitNodeView` 프리팹 하나를 찍어 풀로 쓴다(캐릭터 줄과 같은 판단).
  **선은 노드가 들고 있다**(`Link Image`) — 각 열 첫 줄에서만 꺼진다. 선을 그리는 코드는 없다.
- **`xxx Button (1)`~`(3)`(상태 패널)는 아직 열 화면이 없다.** `Screen Buttons` 배열에 넣지 않았고
  **씬에서도 꺼 두었다**(2026-09-21) — 빈 버튼이 줄의 가로 몫을 먹고 있었다.
- **`Title`만 Presenter 없이 캔버스 직속이다.** `#Main Canvas`의 것만 문구가 바뀌고
  (`MainCanvasView.SetTitle`), 창고·거래의 것은 고정이다. 어느 쪽이든 표기는 붙이지 않는다.
- ✅ **`Sell Cart Presenter`로 개명했다 (2026-09-12 · T-049).** 예전 `Information Presenter`는
  아이템 상세를 띄우는 자리였는데 2026-09-04에 판매 목록이 들어와 **이름이 거짓이 된 상태**였다.
  **좌클릭 상세 표시는 이 패널로 돌아오지 않는다** — 칸 정보는 커서 옆 호버 UI로 간다
  ([`T-050`](../../../tasks/T-050-칸정보호버.md) · 패널 자리를 다투지 않는 쪽을 골랐다).
- **판매는 자원 탭에서만 된다.** 서버 판매 패킷이 아이템 TID 축이라 캐릭터를 담을 수 없어서,
  격자가 다른 탭의 우클릭을 무시한다 — 근거는 [`Storage 규칙.md`](<Storage/Storage 규칙.md>).
- 🔴 **수량 팝업은 `#Storage Canvas`에 있다가 `!System Canvas`로 옮겼다 (2026-09-05).**
  열 캔버스는 넷 다 Sorting Order가 **0인 형제**라, 창고 안에 깐 차단막이 다른 열에 닿지 않아
  **확인을 누르기 전인데 상태바·메인·거래 버튼이 눌렸다.** 옮기면서 이름도
  `Amount Input Presenter`로 바꾸고(판매 전용이 아니다) 차단막 겹 하나를 걷어냈다.
  창고는 `UIManager.AskAmount(...)` 한 줄로 부르므로 팝업 참조를 들지 않는다.
- ✅ **Presenter 오브젝트 이름의 낱말을 띄우는 것으로 통일했다 (2026-09-12 · T-049).**
  `AmountInput Presenter`·`GachaResult Presenter` 둘만 붙여 써서 나머지 13개와 어긋나 있었다
  → `Amount Input Presenter`·`Gacha Result Presenter`. **`WorkStation`은 한 낱말인 도메인 용어라**
  `WorkStation List Presenter`처럼 그대로 둔다. 컴포넌트·배선은 건드리지 않았다(이름만 바뀐다).
- **`Sell Mark`·`Assign Mark`는 임시 표시다** (배지 + "판"/"배"). 아이콘 리소스가 생기면 갈아 끼운다.
  자리는 같지만 **자원 탭과 캐릭터 탭에서 따로 켜져** 겹치지 않는다.
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
