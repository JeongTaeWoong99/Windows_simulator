# UI 규칙

> 최종 업데이트: 2026-08-25 (예시 주석을 `//` 로 교체) · 대상: `Assets/Scripts_Client/UI/`

이 폴더에 스크립트를 새로 만들기 전에 읽는다. **이름을 뭐라고 붙일지 · 어느 오브젝트에 붙일지 ·
어느 폴더에 넣을지**를 여기서 정한다.

이 문서는 `UI/` **전체에 걸리는 규칙**만 담는다. 특정 캔버스의 사정과 함정은
그 폴더의 규칙 문서에 있다.

## 어디를 읽나

| 무엇을 알고 싶나 | 어디 |
|---|---|
| 왜 MVP인가 · 세 역할은 무엇인가 | 이 문서 §0~1 |
| 클래스·오브젝트 **이름**을 뭐라고 붙이나 | 이 문서 §2 |
| 스크립트를 **어느 오브젝트**에 붙이나 · 여백·컴포넌트 순서 | 이 문서 §3 |
| 위젯·화면을 **추가**하려면 | 이 문서 §4 |
| **어느 폴더**에 넣나 | 이 문서 §5 |
| 코드 작성 규약 (Presenter · 캔버스 View · 종속 View) | 이 문서 §6 |
| ⚠️ Canvas · 레이아웃 그룹의 **함정** | [`Layout 규칙.md`](<Layout/Layout 규칙.md>) |
| 로딩·알림 오버레이 · `SetActive` vs `CanvasGroup` | [`System 규칙.md`](<System/System 규칙.md>) |
| 메인 화면을 갈아 끼우는 규칙 · 화면 추가 절차 | [`Main 규칙.md`](<Main/Main 규칙.md>) |
| 창고 탭 · 로그인 · 거래 · 상태 · 위젯 | 각 폴더의 `<폴더명> 규칙.md` |
| 지금 씬에 무엇이 있나 | [`UI 배치 현황.md`](<UI 배치 현황.md>) |

## 0. 이 프로젝트는 MVP(Legacy) 다

UI Toolkit을 쓰지 않는, uGUI 기반 MVP다. 형태는 Unity 공식 학습 샘플
`LevelUpYourCode / DesignPatterns / MVP(Legacy)`를 따른다.

```
[레퍼런스]                              [이 프로젝트]
Scripts/Model/Health.cs                 Managers/PlayerDataModel.cs
Scripts/Presenter/HealthPresenter.cs    UI/<캔버스>/<Presenter>/XxxPresenter.cs
Prefabs/View.prefab (스크립트 없음)      씬의 위젯들 (TMP_Text · Button …)
```

**레퍼런스의 `HealthPresenter`는 `[SerializeField] Slider m_HealthSlider`로 위젯을 직접 쥔다.
화면마다 View 클래스를 두지 않는다.** 우리도 같다 — Presenter가 자기 화면의 위젯을 직접 들고 그린다.

> **그럼 `InventorySlotView` 같은 건 왜 있나?** 레퍼런스에는 **반복되는 칸이 없어서** 그 사례가
> 없었을 뿐이다. 같은 것이 N개 복제되고 각자 다른 데이터에 묶이면 위젯을 Presenter가 다 들고 있을 수
> 없다. 그래서 **반복 칸만** View 클래스를 갖는다(§2).

---

## 1. 세 역할 + 조정자

```
┌─ Model ──────────────────────  Assets/Scripts_Client/Managers/
│   PlayerDataModel     서버가 밀어준 내 계정 상태를 들고 이벤트를 쏜다
│   WindowManager       Win32 창 상태를 들고 실제로 창을 조작한다 (Model 겸 시스템 서비스)
└──────────────────────────────────────────────────────────
        ▲ 요청(호출)                     │ 변경 이벤트(구독)
        │                                ▼
┌─ Presenter ──────────────────  UI/<캔버스>/<Presenter>/
│   Model 을 구독해 위젯에 그리고, 입력을 받아 Model·서버로 넘긴다.
│   ※ 로직·상태·저장을 갖지 않는다.
└──────────────────────────────────────────────────────────
        │ Bind(값)                       ▲ event
        ▼                                │
┌─ View ───────────────────────
│   캔버스 View  화면 단위로 켜고 끈다 (Show)
│   종속 View    Presenter 가 Bind 로 값을 밀어넣는 반복 칸
└──────────────────────────────────────────────────────────

┌─ 조정자 (MVP 밖) ────────────
│   UIManager    무엇을 열고 닫을지 결정하는 단일 출입구
└──────────────────────────────
```

**Presenter는 일을 하는 곳이 아니라 넘기는 곳이다.** 이 한 줄이 아래 규칙 전부의 근거다.

```csharp
// 좋다 — 넘기기만 한다. 위젯이 20개가 되어도 20줄이다.
BindToggle(topmostToggle, window.Topmost, window.SetTopmost);

// 나쁘다 — 로직이 UI로 새어 들어왔다. 위젯 수 × 로직 줄 수로 폭발한다.
topmostToggle.onValueChanged.AddListener(on => {
    var hwnd = Win32Native.GetActiveWindow();
    Win32Native.SetWindowPos(hwnd, on ? -1 : -2, ...);
    PlayerPrefs.SetInt("Topmost", on ? 1 : 0);
});
```

> **Presenter가 두꺼워지면 쪼갤 신호가 아니라, 로직을 Model로 밀어낼 신호다.**

### `UIManager`는 왜 Presenter가 아닌가

화면을 여닫을 뿐 **데이터를 그리지 않는다.** Model을 구독하지도 않는다.
레퍼런스에도 대응물이 없는 이 프로젝트 고유의 층이라 `Manager` 이름을 유지한다.

### `WindowManager`는 왜 `WindowModel`이 아닌가

`SettingPresenter` 입장에서는 Model이 맞다(상태를 들고 변경을 받는다).
하지만 실제로 **Win32 창을 조작하는 부수효과**가 본체라, 그걸 이름에서 지우면 곤란하다.

---

## 2. 이름 규칙 — 접미사는 **MVP 역할**을 따른다

> 2026-08-10 변경. 이전 규칙은 "접미사는 **붙는 오브젝트**를 따른다"(`...CanvasUI` / `...PanelUI`)였다.
> 그러다 보니 같은 `CanvasUI` 이름에 껍데기·조정자·Presenter가 뒤섞여 역할이 안 보였다.

### 클래스

| 역할 | 접미사 | 예 |
|---|---|---|
| 상태를 들고 이벤트를 쏜다 | `...Model` | `PlayerDataModel` |
| 구독해서 그리고, 입력을 넘긴다 | `...Presenter` | `LoginPresenter` · `WorkStationListPresenter` · `StatePresenter` |
| **캔버스 껍데기** — `Show(bool)`만 | `...CanvasView` | `StorageCanvasView` · `MainCanvasView` · `MarketCanvasView` |
| **반복되는 한 칸** — Presenter가 `Bind`한다 | `...View` | `InventorySlotView` · `WorkStationSlotView` · `CharacterStateRowView` |
| 배치를 계산하는 컴포넌트 | `...Layout` / `...LayoutGroup` | `WidgetPositionLayout` · `FlexibleGridLayoutGroup` |

> **캔버스는 언제나 `...CanvasView`다.** 예전엔 자기 안의 화면을 갈아 끼우는 캔버스를
> `...CanvasPresenter`라고 불렀는데, 그러면 한 컬럼 안에 View 캔버스와 Presenter 캔버스가 섞인다.
> **전환은 캔버스가 아니라 그 안의 Presenter가 한다**로 정리했다 — [`Main 규칙.md`](<Main/Main 규칙.md>)의 "한 캔버스 안에서 화면을 갈아 끼운다" 참조.

### 오브젝트 — 접두사로 계층, 이름과 표기로 역할

```
!  최상위 · 다른 축      !Horizental Columns · !Login Canvas (MAIN VIEW)
@  컬럼                  @Storage Column · @Main Column · @Market Column
#  캔버스                #Main Canvas (MAIN VIEW) · #State Canvas (MAIN VIEW)
(없음)  Presenter·패널·위젯   Menu Presenter (↓ SUB VIEW) · Header Panel · Gold Text
```

| 표기 | 무엇인가 |
|---|---|
| `(MODEL)` | 서버 상태를 들고 이벤트를 쏘는 오브젝트 |
| `(MAIN VIEW)` | **캔버스** — 화면 단위로 켜고 끄는 껍데기 |
| `(↓ SUB VIEW)` | **Presenter** — 아래가 전부 위젯이다 |
| 표기 없음 | 컬럼 · 정렬용 패널 · 위젯 · 정적 요소 |

**표기는 스크립트가 붙은 오브젝트에만 붙인다.**
위젯 하나하나에는 붙이지 않는다 — Presenter 아래는 어차피 전부 그 Presenter가 그리는 것이라
표기가 정보를 더하지 않고, `Viewport`·`Template`처럼 Unity가 자동 생성하는 부품까지 번지면
**어디가 화면 경계인지 안 보인다.**

**`↓` 뒤는 "이 Presenter를 펼치면 무엇이 나오는가"다.** 접힌 상태에서도 다음 층이 무엇인지 안다.
자식이 아예 없으면 그냥 `(PRESENTER)`다.

### `Presenter`와 `Panel`을 이름으로 가른다

> 2026-08-10 변경. 이전에는 Presenter가 붙은 오브젝트도 `Xxx Panel`이라 불러서,
> **하이어라키에서 "화면 경계"와 "그 안의 정렬 상자"가 같은 이름을 달고 있었다.**

| 이름 | 무엇인가 | 스크립트 |
|---|---|---|
| `Xxx Presenter (↓ SUB VIEW)` | **화면 하나.** 켜고 끄는 단위이자 배선의 주인 | `XxxPresenter` 하나 |
| `Xxx Panel` | Presenter **안에서** 서브 뷰를 줄 세우는 상자 | **없다.** 레이아웃 그룹만 |

```
WorkStation Select Presenter (↓ SUB VIEW)   ← 화면.  WorkStationSelectPresenter 가 붙는다
├─ Header Panel                             ← 정렬 상자. 스크립트 없음
├─ Industry Panel
├─ Character Assign Scroll View Panel
└─ Character Setting Panel
```

**오브젝트 이름에 캔버스 이름을 되풀이하지 않는다.** 클래스는 어셈블리 전체에서 유일해야 해서
`StorageTabPresenter`·`StorageInformationPresenter`처럼 캔버스 이름을 앞에 달지만, 오브젝트는
이미 그 캔버스 안에 들어 있어 문맥이 붙는다 — `Tab Presenter`·`Information Presenter`면 충분하다.

```
#State Canvas (MAIN VIEW)                       ← 캔버스: 켜고 끈다
└─ State Presenter (↓ SUB VIEW)                 ← 펼치면 위젯뿐
    ├─ Nick Text
    ├─ Gold Text
    └─ Setting Button
        └─ Text (TMP)

#Main Canvas (MAIN VIEW)
├─ Title                                        ← 고정 요소라 표기 없음 (문구만 바뀐다)
├─ WorkStation List Presenter (↓ SUB VIEW)      ┐
├─ WorkStation Select Presenter (↓ SUB VIEW)    │ 셋이 같은 자리를 나눠 쓴다
├─ Setting Presenter (↓ SUB VIEW)               ┘
└─ Menu Presenter (↓ SUB VIEW)                  ← 항상 켜져 있다
```

> **`(PRESENTER ↓ SUB PRESENTER)`는 사라졌다.** Presenter 안에 또 Presenter를 두던 층
> (`WorkStation Panel`)을 없애고 세 화면을 형제로 눕혔기 때문이다 — [`Main 규칙.md`](<Main/Main 규칙.md>)의 "전환 층은 하나다".

---

## 3. 어디에 붙이나

**오브젝트 1개 = 그 종류의 스크립트 1개.** 겹쳐 붙이지 않는다.

| | 규칙 |
|---|---|
| 캔버스 (`...CanvasView`) | `Show(bool)`만 갖는다. `UIManager`가 이걸 부른다 |
| 화면 (`...Presenter`) | 그 화면의 위젯을 `[SerializeField]`로 받아 `Start()`에서 배선한다 |
| 반복 칸 (`...View`) | 프리팹 루트나 반복되는 줄에 붙는다. 자기 데이터만 본다 |
| 정렬용 패널 | **스크립트를 붙이지 않는다.** 레이아웃 그룹만 |
| Layout 컴포넌트 | 배치를 계산하는 오브젝트에. 다른 스크립트와 **같은 오브젝트에 공존해도 된다** |

### 캔버스에는 Presenter를 담는다 — 캔버스가 Presenter가 되지 않는다

**캔버스 오브젝트에 Presenter를 얹지 않는다.** 캔버스가 담는 건 화면이고, 내용은 그 화면이 그린다.

> ⚠️ **이 규칙을 세 곳이 어기고 있었다** (2026-08-10 교정).
> `#State Canvas`가 닉네임·골드를, `#Setting Canvas`가 토글·드롭다운을, `#Widget Canvas`가
> 열기 버튼을 캔버스에서 직접 그렸다. 각각 Presenter를 한 겹 넣어 갈랐다.
> **어겼을 때 실제로 생기는 문제 — 캔버스를 끄면 Presenter도 같이 죽는다.**
> 여닫기와 표시가 한 오브젝트에 묶여 "닫혀 있는 동안 갱신"이나 "화면만 교체"가 불가능해진다.

> 예외는 여러 화면이 함께 쓰는 머리 하나뿐이다 — [`Main 규칙.md`](<Main/Main 규칙.md>)의 "캔버스 머리의 제목은 UIManager가 밀어 넣는다".

### Presenter가 자기 위젯을 쥔다 — 상위가 건너뛰어 잡지 않는다

> ⚠️ 예전엔 `WorkStationPresenter`가 자식인 `Menu Panel`의 창고·거래 버튼을 직접 들고 있었다.
> 버튼이 늘어날수록 **상위 Presenter가 남의 화면 사정을 알게 된다.**
> 지금은 `MenuPresenter`가 그 버튼들을 쥔다.

### 컴포넌트 순서 · 여백은 종류별로 똑같이 맞춘다

같은 종류의 오브젝트가 인스펙터에서 다르게 생겼으면 **다른 것인지 그냥 어긋난 것인지 알 수 없다.**

**컴포넌트 순서** — "크기 주장 → 그리기 준비 → 그리는 것 → 입력 → 자식 배치 → 내 코드"

```
RectTransform                          ← Unity 가 맨 위에 고정한다. 못 옮긴다
LayoutElement / SquareLayoutElement    ← 부모에게 내 크기를 주장
CanvasRenderer
Canvas / CanvasScaler
Image / TextMeshProUGUI
GraphicRaycaster
Mask / RectMask2D · ScrollRect
XxxLayoutGroup                         ← 자식을 배치
ContentSizeFitter
Button / Toggle / Dropdown …
XxxPresenter · XxxView                 ← 내 스크립트는 언제나 맨 아래
```

없는 것은 건너뛴다. 예: `#Main Canvas (MAIN VIEW)`는
`LayoutElement / CanvasRenderer / Canvas / Image / GraphicRaycaster / VerticalLayoutGroup / MainCanvasView`.

**여백** — 계층에 따라 두 값뿐이다.

| 어디 | padding | spacing |
|---|---|---|
| `!Horizental Columns` | 0 | **10** (열 사이) |
| `@Xxx Column` | 0 | 0 |
| **캔버스 이하 전부** | **5 5 5 5** | **5** |
| 위젯 속살 (반복 줄 · Unity 토글 내부) | 건드리지 않는다 | — |

마지막 줄이 예외인 이유 — `Character State Row`는 높이 30 남짓인데 위아래 5씩 넣으면
**내용이 눌린다.** 섹션의 여백과 위젯 안쪽 여백은 다른 문제다.

> Unity 에디터를 스크립트로 만질 때: **컴포넌트 순서는 `ComponentUtility.MoveComponentUp`으로만 바꾼다.**
> `m_Component`를 `SerializedObject`로 직접 건드리면 Unity가 거부하고
> (`It is not allowed to modify the m_Component property`),
> `MoveComponentRelativeToComponent`는 대화상자를 띄워 MCP에서 실행이 끊긴다.

---

## 4. 위젯·화면을 추가할 때 — 판단 흐름

```
버튼(또는 토글/드롭다운)을 하나 더 붙이려 한다
        │
        ├─ 같은 것이 N개 반복되나? (파라미터만 다른가)
        │     예 → View 컴포넌트 1개 만들고 N번 복제한다
        │
        ├─ 그 위젯이 자기만의 상태를 구독해서 자기 모습을 바꾸나?
        │     예 → View
        │
        ├─ #Main Canvas 의 자리를 통째로 차지하는 화면인가? (목록·선택·설정 …)
        │     예 → [`Main 규칙.md`](<Main/Main 규칙.md>)의 "메인 화면 추가"
        │
        ├─ 다른 캔버스 안에서 자리를 나눠 쓰나? (창고 탭 같은)
        │     예 → Presenter 를 새로 만들고, 그 캔버스의 전환 담당 한 곳이 켜고 끈다 (예: `Storage 규칙.md`)
        │
        └─ 그냥 기능 위젯 하나 더인가?
              예 → 그 화면 Presenter 의 Start() 에 1줄 추가. 끝.
```

**"버튼 하나 = 스크립트 하나"는 하지 않는다.** 로직은 어차피 Model에 있으므로,
버튼마다 클래스를 만들면 `Start()`·`RequireRef`·`Services.Get()` 보일러플레이트만 N배가 되고
얻는 게 없다.

> ⚠️ `button.onClick.AddListener(...)`는 **이미 옵저버 패턴이다.**
> "옵저버로 바꿀까"라는 선택지는 없다. 정할 건 **구독자를 몇 개 둘 것인가**뿐이다.

---

## 5. 폴더 규칙 — `<캔버스>/<Presenter>/`

**캔버스 폴더 아래에 Presenter 이름의 폴더를 둔다.** 그 폴더 안에 Presenter 스크립트와
**그 Presenter에 종속된 View**가 함께 산다. 폴더를 열면 "이 화면은 무엇으로 이뤄졌나"가 바로 보인다.

```
UI/
├─ UI 규칙.md                             ← 이 문서 (UI 전체 공통)
├─ UI 배치 현황.md                        ← 지금 씬에 무엇이 놓여 있나
├─ Login/
│   ├─ Login 규칙.md
│   ├─ LoginCanvasView.cs
│   └─ LoginPresenter/
│       └─ LoginPresenter.cs
├─ Storage/
│   ├─ Storage 규칙.md
│   ├─ StorageCanvasView.cs
│   ├─ StorageTabPresenter/
│   │   └─ StorageTabPresenter.cs
│   ├─ InventoryPresenter/
│   │   ├─ InventoryPresenter.cs
│   │   └─ InventorySlotView.cs          ← 종속 View
│   └─ StorageInformationPresenter/
│       └─ StorageInformationPresenter.cs
├─ Main/
│   ├─ Main 규칙.md
│   ├─ MainCanvasView.cs
│   ├─ WorkStationListPresenter/
│   │   ├─ WorkStationListPresenter.cs
│   │   └─ WorkStationSlotView.cs        ← 종속 View
│   ├─ WorkStationSelectPresenter/
│   │   ├─ WorkStationSelectPresenter.cs
│   │   └─ CharacterStateRowView.cs      ← 종속 View
│   ├─ SettingPresenter/
│   │   └─ SettingPresenter.cs
│   └─ MenuPresenter/
│       └─ MenuPresenter.cs
├─ State/
│   ├─ State 규칙.md
│   ├─ StateCanvasView.cs
│   └─ StatePresenter/
│       └─ StatePresenter.cs
├─ Market/
│   ├─ Market 규칙.md
│   ├─ MarketCanvasView.cs
│   └─ GachaPresenter/
│       └─ GachaPresenter.cs
├─ Widget/
│   ├─ Widget 규칙.md
│   ├─ WidgetCanvasView.cs
│   └─ WidgetPresenter/
│       └─ WidgetPresenter.cs
├─ System/                                ← 캔버스 하나에 오버레이 둘
│   ├─ System 규칙.md
│   ├─ SystemCanvasView.cs
│   ├─ ResultMessages.cs
│   ├─ LoadingPresenter/
│   │   └─ LoadingPresenter.cs
│   └─ NoticePresenter/
│       └─ NoticePresenter.cs
└─ Layout/                                ← 예외. 화면이 아니라 배치 계산
    ├─ Layout 규칙.md
    ├─ FlexibleGridLayoutGroup.cs
    ├─ SquareLayoutElement.cs
    ├─ WidgetPositionLayout.cs
    ├─ WindowDragArea.cs
    └─ Editor/
        └─ FlexibleGridLayoutGroupEditor.cs
```

- **폴더 이름 = 그 안에 사는 Presenter의 클래스 이름.** 오브젝트 이름이 아니다
  (오브젝트는 `WorkStation List Presenter (↓ SUB VIEW)`, 폴더는 `WorkStationListPresenter/`).
- **깊이는 두 단(`<캔버스>/<Presenter>/`)에서 멈춘다.** Presenter 폴더를 중첩하지 않는다 —
  하이어라키에서도 Presenter 안에 Presenter를 두지 않기 때문이다([`Main 규칙.md`](<Main/Main 규칙.md>)의 "전환 층은 하나다").
- **캔버스 폴더마다 `<폴더명> 규칙.md`를 둔다.** 그 캔버스에만 걸리는 규칙·함정은 거기 쓰고,
  이 문서에는 `UI/` 전체에 걸리는 것만 남긴다 — 위 "어디를 읽나" 표가 그 지도다.
- **빈 폴더는 만들지 않는다.** Presenter 스크립트가 생길 때 그 폴더를 만든다.
- 에디터 전용 스크립트는 반드시 `Editor/` 하위에 둔다. 안 그러면 빌드에 포함돼 컴파일이 깨진다.
- Model·조정자는 이 폴더에 두지 않는다 → `Assets/Scripts_Client/Managers/`
- **`Layout/`만 예외다.** 화면이 아니라 **배치 계산**이고, 어느 캔버스에도 속하지 않는다.

### enum 은 소유자 파일 안에 둔다

`MainScreen`은 `Managers/UIManager.cs`, `WidgetPosition`은 `UI/Layout/WidgetPositionLayout.cs`,
`ScreenAnchor`·`WindowScale`은 `Managers/WindowManager.cs`에 있다. **한 줄짜리 파일을 만들지 않는다.**
다른 파일에서 써도 상관없다 — 단일 어셈블리다.

---

## 6. 공통 작성 규약

모든 Presenter가 같은 뼈대를 쓴다.

```csharp
public class XxxPresenter : MonoBehaviour
{
    [CenterHeader("참조")]                                 // ※ < > 를 넣지 않는다 (아래 표)
    [SerializeField, Tooltip("... OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button someButton = null!;

    private PlayerDataModel _data = null!;
    private bool _isSubscribed;
    private bool _isReady;   // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다
    private void Start()
    {
        this.RequireRef(someButton, nameof(someButton));   // 미연결이면 즉시 예외 (fail-fast)

        _data = Services.Get<PlayerDataModel>();           // ※ 반드시 Start

        Subscribe();
        someButton.onClick.AddListener(OnClicked);
        Refresh();                                          // 이미 데이터가 와 있을 수 있다

        _isReady = true;
    }

    private void OnEnable()  { if (_isReady) { Subscribe(); Refresh(); } }
    private void OnDisable() { Unsubscribe(); }
}
```

지켜야 하는 것:

| 규칙 | 이유 |
|---|---|
| **`onClick`은 코드로 연결한다.** 인스펙터에서 연결하지 않는다 | 씬 파일에 묻혀 검색이 안 되고, 메서드 이름을 바꾸면 조용히 끊긴다 |
| **`Services.Get<T>()`는 반드시 `Start()`** 에서 | `Awake`·`OnEnable`은 등록 순서가 보장되지 않는다 |
| **필수 참조는 `= null!` + `RequireRef`** | `?`로 두면 미연결이 조용히 무시돼 "왜 안 되지"가 된다. 선택 참조만 `?` |
| **`RequireRef`는 Presenter가 `Start`, 종속 View가 `Awake`** | View는 상위 Presenter의 `Start`가 `Bind`를 부르기 전에 검증이 끝나 있어야 한다. 서비스를 조회하지 않으므로 `Awake`로 충분하다 (아래 "종속 View 쪽 규약") |
| **Unity 객체(`GameObject`·`Component`)에 `?.`·`??`를 쓰지 않는다** | 이 둘은 Unity의 `==` 오버로드를 건너뛰어 **"가짜 null"**(파괴된 오브젝트)을 통과시킨다. `== null`/`!= null`로 검사한다. `?.`는 순수 C#(이벤트·컬렉션)에만 (`MonoBehaviourExtensions` 주석) |
| **`OnEnable`에서 재구독하고 다시 그린다** | 꺼져 있는 동안 도착한 이벤트를 놓쳤다. 재구독만으론 화면이 낡은 채 남는다 |
| **`OnDisable`에서 반드시 구독 해제** | 안 하면 꺼진 UI가 계속 반응한다 |
| **반복 변수를 람다에 그대로 넘기지 않는다** | 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다 |
| **매 프레임 도는 건 한 곳에만** | 슬롯마다 `Update`를 두면 상시 실행 앱에서 비용이 슬롯 수만큼 곱해진다. 계산은 Presenter가 하고 View엔 결과만 넘긴다 |
| **`[CenterHeader]`에 `< >`를 넣지 않는다** | `CenterHeaderDrawer`가 그릴 때 `$"< {text} >"`로 감싼다. `[CenterHeader("< 참조 >")]`로 쓰면 인스펙터에 **`< < 참조 > >`** 가 나온다. 맞는 표기는 `[CenterHeader("참조")]` |
| **배열 필드 위의 `[CenterHeader]`에는 `[NonReorderable]`을 같이 단다** | 배열은 기본이 **reorderable list**로 그려지는데, 그 경로가 데코레이터를 건너뛰어 **헤더가 통째로 안 보인다**(에러도 경고도 없다). `[NonReorderable]`이 그 경로를 끄면 다시 나온다. 덤으로 **드래그로 순서가 뒤바뀌는 사고**도 막는다 — `industryButtons`·`tabButtons`는 순서가 곧 의미다 |
| **인스펙터 필드는 화면에 나오는 순서로 둔다** | `UIManager`가 로그인 캔버스를 맨 위에 두는 이유다 — 게임이 거기서 시작한다. 코드 순서와 사용자가 보는 순서가 어긋나면 읽는 사람이 매번 다시 짜맞춰야 한다 |
| **주석은 한글로** | 프로젝트 공통 |

### 캔버스 View 쪽 규약

```csharp
public class XxxCanvasView : MonoBehaviour
{
    // 이 캔버스를 열고 닫는다 (UIManager가 호출).
    public void Show(bool on) => gameObject.SetActive(on);
}
```

이게 전부다. **여기에 위젯 참조가 하나라도 생기면 Presenter를 만들어 옮긴다.**

**예외는 여러 화면이 함께 쓰는 머리 하나뿐이다** — `MainCanvasView.SetTitle(string)` ([`Main 규칙.md`](<Main/Main 규칙.md>)).
가르는 기준은 위젯을 쥐느냐가 아니라 **Model을 구독하느냐**다. 위에서 밀어 넣은 값만
그리면 View, 스스로 이벤트를 받아 갱신하면 Presenter다.

### 종속 View 쪽 규약

```csharp
public class XxxSlotView : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText = null!;

    public event Action<XxxSlotView>? Clicked;   // 입력은 위로 던지기만

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 Presenter가 Bind를 부르기 전에 이미 검증돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(nameText, nameof(nameText));
    }

    public void Bind(int id, string displayName) { ... }   // 완성된 값을 받아 그린다
}
```

- **필수 참조 검증은 `Start`가 아니라 `Awake`에서 한다.** 상위 Presenter의 `Start`가 `Bind`를
  부르는 순간에는 이미 검증이 끝나 있어야 한다. 서비스를 조회하지 않으므로 `Awake`로 충분하다.
  **`= null!`만 쓰고 `RequireRef`를 빼면 안 된다** — 미연결이 `Bind` 첫 호출에서
  `NullReferenceException`으로 터지는데, 그때는 **어느 필드인지가 안 찍힌다.**
- **서버도 세션도 조회하지 않는다.** 이름처럼 변환이 필요한 값은 **Presenter가 만들어 넘긴다** —
  `CharacterId`는 개체 번호라 테이블에서 이름이 안 나오고 보유 목록을 거쳐야 하는데,
  그 변환은 세션을 아는 쪽의 몫이다.
- **스스로 시간을 세지 않는다.** `Update`·코루틴을 두지 않는다.

