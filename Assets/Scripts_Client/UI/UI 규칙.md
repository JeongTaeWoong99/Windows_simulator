# UI 규칙

> 최종 업데이트: 2026-08-16 (`min`을 자기 폭에서 파생시키지 않는다 — 함정 추가) · 대상: `Assets/Scripts_Client/UI/`

이 폴더에 스크립트를 새로 만들기 전에 읽는다. **이름을 뭐라고 붙일지 · 어느 오브젝트에 붙일지 ·
어느 폴더에 넣을지**를 여기서 정한다.

**지금 씬에 무엇이 놓여 있는지**는 여기가 아니라 [`UI 배치 현황.md`](<UI 배치 현황.md>)에 있다.
이 문서는 **변하지 않는 규칙**만 담는다.

## 필요한 절만 읽는다

| 무엇을 알고 싶나 | 절 |
|---|---|
| 왜 MVP인가 · 세 역할은 무엇인가 | 0~1 |
| 클래스·오브젝트 **이름**을 뭐라고 붙이나 | 2 |
| 스크립트를 **어느 오브젝트**에 붙이나 · 여백·컴포넌트 순서 | 3 |
| 위젯·화면을 **추가**하려면 | 4 |
| **어느 폴더**에 넣나 | 5 |
| 코드 작성 규약 (캔버스 View · 종속 View) | 6 |
| ⚠️ Canvas · 레이아웃 그룹의 **함정** | 7 · 7-2 |
| 지금 씬에 무엇이 있나 | → [`UI 배치 현황.md`](<UI 배치 현황.md>) |

---

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
> **전환은 캔버스가 아니라 그 안의 Presenter가 한다**로 정리했다 — §3의 `#Main Canvas` 참조.

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
> (`WorkStation Panel`)을 없애고 세 화면을 형제로 눕혔기 때문이다 — §3 "전환 층은 하나다".

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

**유일한 예외는 여러 화면이 함께 쓰는 머리다** — `#Main Canvas`의 `Title`.
어느 화면의 Presenter에 맡겨도 **그 화면이 꺼질 때 함께 죽어서**, 정작 다른 화면으로 넘어간
순간 제목을 못 바꾼다. 그래서 `MainCanvasView`가 `SetTitle(string)`을 갖는다.
Model을 구독하지 않고 **위에서 밀어 넣은 값만 그리므로** 역할은 여전히 View다.

### Presenter가 자기 위젯을 쥔다 — 상위가 건너뛰어 잡지 않는다

> ⚠️ 예전엔 `WorkStationPresenter`가 자식인 `Menu Panel`의 창고·거래 버튼을 직접 들고 있었다.
> 버튼이 늘어날수록 **상위 Presenter가 남의 화면 사정을 알게 된다.**
> 지금은 `MenuPresenter`가 그 버튼들을 쥔다.

### 한 캔버스 안에서 화면을 갈아 끼운다

같은 자리를 여러 화면이 나눠 쓰면 **캔버스를 여러 개 두지 않고 Presenter를 여러 개 둔다.**

```
#Main Canvas (MAIN VIEW)   LayoutElement(pref 900 · flexH 0) · Canvas · GraphicRaycaster · VerticalLayoutGroup
├─ Title                                        pref 50  · flexH 0   ← 항상
├─ WorkStation List Presenter   (↓ SUB VIEW)    pref  0  · flexH 1   ┐
├─ WorkStation Select Presenter (↓ SUB VIEW)    pref  0  · flexH 1   │ 하나만 켜진다
├─ Setting Presenter            (↓ SUB VIEW)    pref  0  · flexH 1   ┘
└─ Menu Presenter               (↓ SUB VIEW)    pref 100 · flexH 0   ← 항상
```

- **갈아 끼워지는 화면은 전부 같은 레이아웃 값(`preferredHeight 0` · `flexibleHeight 1`)을 준다.**
  그래야 어느 것이 켜지든 같은 자리에 같은 크기로 들어간다.
  겹쳐 놓을 필요가 없다 — **꺼진 오브젝트는 레이아웃 계산에서 아예 빠진다.**
- **배경 Image는 "전부 꺼질 수 있는가"로 정한다.** 여기처럼 `Title`·`Menu`가 늘 켜져 있으면
  캔버스에 배경을 둬도 된다(빈 판이 보일 일이 없다). 자식이 전부 꺼질 수 있는 캔버스라면
  **배경을 자식으로 내린다** — 안 그러면 다 껐는데 빈 판만 남는다.
- 화면마다 캔버스를 두면 **화면을 하나 붙일 때마다 `Canvas`·`GraphicRaycaster`·`LayoutElement` 높이를
  따로 맞춰야 하고, 하나만 어긋나도 크기가 틀어진다**(§7-2).

### 항상 켜져 있어야 하는 컴포넌트는 여닫히는 캔버스에 두지 않는다

`OnEnable`에서 일하는 컴포넌트(`WidgetPositionLayout`)를 토글 대상에 붙이면,
**그게 꺼져 있는 동안 아무 일도 하지 않는다.** 기본 상태로 꺼져 있으면 한 번도 안 돈다.
`!Horizental Columns`처럼 상주하는 오브젝트에 둔다.

### 배치 컴포넌트는 화면 크기 변화에 스스로 반응한다

`WidgetPositionLayout`은 `OnRectTransformDimensionsChange()`로 **캔버스(=창의 렌더 영역) 크기가
바뀔 때마다 배치를 다시 태운다.** 창 크기 프리셋 변경 · 타이틀바 토글 · 배율이 다른 모니터로
드래그가 전부 이 콜백으로 모인다.

- **`WindowManager`를 참조하지 않는다.** Unity 콜백만으로 자립하므로 Managers → UI 역방향
  의존이 생기지 않는다.
- **콜백은 플래그만 세우고, 실제 배치는 `LateUpdate`에서 한다.** 이유는 바로 아래 함정 참고.

### ⚠️ 레이아웃 콜백 안에서 즉시 리빌드하지 않는다

`OnRectTransformDimensionsChange`는 UGUI가 **레이아웃 패스를 도는 도중에도** 날아온다
(`CanvasUpdateRegistry.PerformUpdate` → `HorizontalLayoutGroup.SetLayoutHorizontal` → 자식 크기 변경).
그 안에서 `LayoutRebuilder.ForceRebuildLayoutImmediate`를 부르면 **같은 서브트리를 재진입 재빌드**하게
되고, 바깥 패스가 자식을 순회하던 중에 폭이 갈아엎어져 **일부만 새 값, 일부는 옛 값**으로 남는다.

```csharp
// ✗ 열끼리 침범한다 — 패스 도중 재진입
private void OnRectTransformDimensionsChange() => Apply();   // Apply 안에서 ForceRebuild...

// ✓ 패스 밖으로 미룬다
private void OnRectTransformDimensionsChange() => _pendingApply = true;
private void LateUpdate() { if (_pendingApply) { _pendingApply = false; Apply(); } }
// Apply 안에서는 LayoutRebuilder.MarkLayoutForRebuild
```

- 자기 클래스에 재진입 빗장(`_applying`)을 둬도 **소용없다.** 막아야 할 바깥 패스가 UGUI 것이다.
- `LateUpdate`는 `Canvas.willRenderCanvases`보다 먼저 돌아, 예약해도 **같은 프레임에** 반영된다.
- 형제 순서 변경(`SetSiblingIndex`)은 UGUI가 알아서 dirty 처리하므로 예약으로 충분하다.

> 2026-08-15. 실제로 `ForceRebuild`로 바꿨다가 열 침범 회귀를 만들었다
> (`.claude/Agent/2026-08-15-build-ui-layout-mismatch.md`).

### 전환 층은 하나다 — 화면은 자기를 끄지 않는다

> 2026-08-10 변경. 예전엔 **같은 일을 두 층이 따로 했다.**
> ```
> UIManager                  작업슬롯 ↔ 설정        ← 바깥에서
>   └ WorkStationPresenter   목록 ↔ 선택            ← 안에서
> ```
> 폴더도 하이어라키도 이 중첩을 그대로 베껴 `WorkStation Panel` 한 겹이
> **아무 위젯도 안 가진 채** 끼어 있었다. 세 화면을 형제로 눕히고 `WorkStationPresenter`를 지웠다.

**`MainScreen` 하나로 셋을 다룬다. 켜고 끄는 곳은 `UIManager.ShowMainScreen` 한 곳뿐이다.**

```csharp
// 목록 — 자기를 끄지 않는다. 번호를 넣고 자리를 넘길 뿐이다.
selectPresenter.Open(slotIndex);
ui.ShowMainScreen(MainScreen.WorkStationSelect);   // 이 호출이 목록을 끈다

// 선택 — 뒤로가기도 마찬가지다. 스스로 끄면 목록이 켜지기 전 빈 칸이 남는다.
ui.ShowMainScreen(MainScreen.WorkStationList);
```

**참조 방향은 하나뿐이고, 방향에 이유가 있다.** 목록이 선택 화면을 참조한다 —
반대로 선택 화면이 목록의 이벤트를 구독하면 **평소 꺼져 있어서 신호를 못 받는다.**
`OnDisable`에서 구독을 끊는 규약과 정면으로 부딪힌다.

> **살아 있는 쪽이 넘긴다.** 이게 "이벤트를 쏘고 위층이 받는다"를 대신하는 규칙이다.
> 위층이 없어졌으므로 받을 사람도 없다.

> 꺼져 있는 화면을 열 때는 **인자를 먼저 넣고 켠다**(`Open(slotIndex)` 안에서 `SetActive(true)`).
> 꺼진 오브젝트는 `Start()`가 아직 안 돌았을 수 있어, 켠 직후 값을 넣으면 초기화가 덮어쓴다.

### 캔버스 머리의 제목은 `UIManager`가 밀어 넣는다

화면이 바뀌면 `#Main Canvas`의 `Title` 문구도 바뀐다. 문구는 **코드가 아니라
`UI Manager > Main Screens`의 각 줄**에 적혀 있다 — 표시용이라 바뀌어도 로직이 안 바뀌는데
코드에 박으면 문구 하나 고치는 데 컴파일이 필요하고, 화면 목록과 제목 목록이 따로 논다.

---

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
        │     예 → 아래 "메인 화면 추가"
        │
        ├─ 다른 캔버스 안에서 자리를 나눠 쓰나? (창고 탭 같은)
        │     예 → Presenter 를 새로 만들고, 그 캔버스의 전환 담당 한 곳이 켜고 끈다 (§3)
        │
        └─ 그냥 기능 위젯 하나 더인가?
              예 → 그 화면 Presenter 의 Start() 에 1줄 추가. 끝.
```

**"버튼 하나 = 스크립트 하나"는 하지 않는다.** 로직은 어차피 Model에 있으므로,
버튼마다 클래스를 만들면 `Start()`·`RequireRef`·`Services.Get()` 보일러플레이트만 N배가 되고
얻는 게 없다.

> ⚠️ `button.onClick.AddListener(...)`는 **이미 옵저버 패턴이다.**
> "옵저버로 바꿀까"라는 선택지는 없다. 정할 건 **구독자를 몇 개 둘 것인가**뿐이다.

### 메인 화면 추가 — 코드는 한 줄이다

`#Main Canvas`의 같은 자리를 나눠 쓰는 화면들은 `MainScreen` enum이 **seam**이다.
여는 쪽(`StatePresenter`의 버튼 · `WorkStationListPresenter`의 칸 클릭)과 패널을 쥔 `UIManager`가
서로의 오브젝트를 모른다.

```
[1] MainScreen 에 값 추가                                       ← 코드는 여기 한 줄뿐
[2] #Main Canvas 아래에 XX Presenter (↓ SUB VIEW) 를 만든다
       LayoutElement: preferredHeight 0 · flexibleHeight 1     ← 다른 화면들과 같은 값
[3] UI Manager > Main Screens 에 (값 · 오브젝트 · 제목) 한 줄
[4] 상태 패널 버튼으로 열 화면이면 State Presenter > Screen Buttons 에 (버튼 · 값) 한 줄
```

동작은 **버튼을 누르면 그 화면만 켜지고, 같은 버튼을 다시 누르면 기본(작업슬롯 목록)으로** 돌아온다.
여는 일만 하면 이미 열려 있을 때 눌러도 변화가 없어 버튼이 고장 난 것처럼 보이기 때문이다.

> ⚠️ **`MainScreen`에 값을 중간에 끼워 넣으면 씬의 배선이 조용히 어긋난다.**
> enum은 씬에 **int로** 저장돼서, 값 순서가 밀리면 `Screen Buttons`·`Main Screens`가
> 엉뚱한 화면을 가리킨다. 컴파일도 통과하고 경고도 없다 — **끝에 추가하거나,
> 순서를 바꿨으면 두 배열을 전수 확인한다.**

### 아직 화면이 없는 버튼도 Presenter가 잡아 둔다

`StorageTabPresenter`가 그 예다. 탭 4개 중 자원 하나만 실재하지만, 버튼을 잡아 두고
"아직 없다"를 로그로 남긴다. **안 잡아 두면 눌러도 아무 일이 없어 버튼이 고장 난 것과 구분되지 않는다.**

---

## 5. 폴더 규칙 — `<캔버스>/<Presenter>/`

**캔버스 폴더 아래에 Presenter 이름의 폴더를 둔다.** 그 폴더 안에 Presenter 스크립트와
**그 Presenter에 종속된 View**가 함께 산다. 폴더를 열면 "이 화면은 무엇으로 이뤄졌나"가 바로 보인다.

```
UI/
├─ UI 규칙.md          ← 이 문서
├─ Login/
│   ├─ LoginCanvasView.cs
│   └─ LoginPresenter/
│       └─ LoginPresenter.cs
├─ Storage/
│   ├─ StorageCanvasView.cs
│   ├─ StorageTabPresenter/
│   │   └─ StorageTabPresenter.cs
│   ├─ InventoryPresenter/
│   │   ├─ InventoryPresenter.cs
│   │   └─ InventorySlotView.cs          ← 종속 View
│   └─ StorageInformationPresenter/
│       └─ StorageInformationPresenter.cs
├─ Main/
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
│   ├─ StateCanvasView.cs
│   └─ StatePresenter/
│       └─ StatePresenter.cs
├─ Market/
│   ├─ MarketCanvasView.cs
│   └─ GachaPresenter/
│       └─ GachaPresenter.cs
├─ Widget/
│   ├─ WidgetCanvasView.cs
│   └─ WidgetPresenter/
│       └─ WidgetPresenter.cs
└─ Layout/                      ← 예외. 화면이 아니라 배치 계산
    ├─ FlexibleGridLayoutGroup.cs
    ├─ SquareLayoutElement.cs
    ├─ WidgetPositionLayout.cs
    └─ Editor/
        └─ FlexibleGridLayoutGroupEditor.cs
```

- **폴더 이름 = 그 안에 사는 Presenter의 클래스 이름.** 오브젝트 이름이 아니다
  (오브젝트는 `WorkStation List Presenter (↓ SUB VIEW)`, 폴더는 `WorkStationListPresenter/`).
- **깊이는 두 단(`<캔버스>/<Presenter>/`)에서 멈춘다.** Presenter 폴더를 중첩하지 않는다 —
  하이어라키에서도 Presenter 안에 Presenter를 두지 않기 때문이다(§3 "전환 층은 하나다").
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
| **`RequireRef`는 Presenter가 `Start`, 종속 View가 `Awake`** | View는 상위 Presenter의 `Start`가 `Bind`를 부르기 전에 검증이 끝나 있어야 한다. 서비스를 조회하지 않으므로 `Awake`로 충분하다 (§6 종속 View 규약) |
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
    /// <summary>이 캔버스를 열고 닫는다 (UIManager가 호출).</summary>
    public void Show(bool on) => gameObject.SetActive(on);
}
```

이게 전부다. **여기에 위젯 참조가 하나라도 생기면 Presenter를 만들어 옮긴다.**

**예외는 여러 화면이 함께 쓰는 머리 하나뿐이다** — `MainCanvasView.SetTitle(string)` (§3).
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

---

## 7. Canvas를 다룰 때의 함정

| 증상 | 원인 | 해결 |
|---|---|---|
| **Sorting Order를 올려도 계속 뒤에 그려진다** | 중첩 Canvas는 `Override Sorting`을 켜야 `Sorting Order`가 먹는다. 끄면 숫자가 **통째로 무시**되고 계층 순서로만 그려진다 | `Override Sorting` 체크 |
| **앞에는 나오는데 버튼이 안 눌린다** | `Override Sorting`을 켠 Canvas는 **자기 `GraphicRaycaster`** 가 필요하다 | `GraphicRaycaster` 추가 |
| **자식으로 옮겼더니 화면에서 사라졌다** | 루트 Canvas일 땐 Unity가 `localScale`을 관리해 줬다. 자식이 되면 저장된 값이 그대로 적용된다 | `localScale`을 `1,1,1`로 |
| **창 배율을 바꾸면 그 Canvas만 안 따라간다** | `CanvasScaler`가 `Constant Pixel Size`(기본값) | `Scale With Screen Size` / `1920×1080` / `Match = 1(Height)` — **Root Canvas와 동일하게** |
| 열을 껐더니 다른 열들이 가운데로 몰린다 | Column을 껐다 | Column이 아니라 **그 안의 Canvas만** 끈다 (`UIManager` 주석) |
| **화면을 다 껐는데 빈 판이 남는다** | 배경 `Image`가 캔버스에 있는데 자식이 전부 꺼질 수 있다 | 배경을 자식으로 내린다. **늘 켜진 자식이 있는 캔버스면 그냥 둬도 된다** (§3) |

**Sorting Order는 띄엄띄엄 준다** — `Login = 100`, `Log = 200`. 사이에 끼워 넣을 일이 반드시 생긴다.

---

## 7-2. 레이아웃 그룹의 함정

| 증상 | 원인 | 해결 |
|---|---|---|
| **형제를 껐더니 남은 칸이 화면 전체로 늘어난다** | 그 칸의 `LayoutElement.flexibleHeight = 1`. flexible은 **"남는 높이를 가져간다"** 라서, 형제가 꺼져 자리가 통째로 비면 **혼자 다 빨아들인다** | 크기가 고정이어야 하는 칸은 **`preferredHeight = 실제 높이` · `flexibleHeight = 0`**. `@Main Column`은 90 + 900 + 90 = 1080 = 컬럼 높이라 **남는 높이 자체가 없다** — 나눌 것이 없으면 사고도 없다 |
| **전부 닫았는데 위젯이 화면 가장자리에서 밀린다** | 내용이 꺼진 캔버스의 `LayoutElement`가 컬럼 안에서 높이를 계속 차지한다 | 화면만 끄지 말고 **캔버스까지 끈다** (`UIManager.CloseAllExceptWidget`) |
| **`LayoutElement`를 고쳐도 높이가 안 변한다** | 부모가 **`Child Control Height = off`**. 그러면 `LayoutElement.preferredHeight`는 **부모가 자기 총높이를 셀 때만** 읽히고, 실제 높이는 `RectTransform`의 `Height`가 그대로 쓰인다 | 부모의 `Child Control Height`를 **켠다**(지금 컬럼들은 켜져 있다). 끈 채로 두려면 `RectTransform.Height`와 `preferredHeight`를 **둘 다** 맞춰야 한다 — 두 곳을 손으로 동기화하는 셈이라 반드시 어긋난다 |
| **화면을 갈아 끼웠더니 크기·위치가 달라진다** | 같은 자리를 나눠 쓰는 화면들의 `LayoutElement` 값이 서로 다르다 | 셋 다 `preferredHeight 0` · `flexibleHeight 1`로 **똑같이** 준다. 겹쳐 놓을 필요는 없다 — 꺼진 오브젝트는 레이아웃에서 빠진다 |
| **자식들이 폭을 똑같이 나눠 갖는다** | `Child Force Expand Width`가 켜져 있다. 이건 "남는 폭을 **모두에게 균등 분배**"라서, 한 자식만 늘리고 싶을 때는 정반대로 동작한다 | **끄고**, 늘릴 자식에만 `LayoutElement.flexibleWidth = 1` |
| **`preferredHeight 50`을 줬는데 머리 칸이 220으로 부푼다** | 부모의 **`Child Force Expand Height`가 켜져 있다.** 남는 높이를 `flexibleHeight`와 무관하게 **모든 자식에게 균등 분배**해서, 고정하려던 칸까지 함께 부푼다 | **끈다.** 높이를 나누는 건 `flexibleHeight`지 `expand`가 아니다 (아래 "세로 3단 배치의 정석") |
| **둘이 "나머지를 반씩"인데 크기가 다르다** | `preferredHeight = -1`은 "**내 내용물 높이를 먼저 챙기고** 남은 것만 flexible로 나눈다"는 뜻이다. 내용물이 다르면(토글 4개 vs 드롭다운 3개) 결과가 달라진다 | 둘 다 **`preferredHeight = 0` · `flexibleHeight = 1`**. 그래야 전체를 1:1로만 나눈다 |
| **`Preferred Height`를 줬는데 안 먹는다** | 같은 오브젝트의 `ScrollRect` 등이 자식 RectTransform을 따로 건드린다 | 안 쓰는 `ScrollRect`를 뗀다 |
| 높이 합이 부모를 넘친다 | 레이아웃에 빠진 자식이 있다 (`LayoutElement` 없이 큰 preferred를 가진 것) | 모든 자식에 높이 정책을 준다 — 고정은 `preferredHeight` + `flexibleHeight = 0`, 나머지를 채울 하나만 `flexibleHeight = 1` |
| **"높이만큼 정사각형"이 안 된다** | UGUI는 **가로를 먼저 다 정하고 세로를 정한다.** 가로를 정할 때 자기 높이가 아직 없다 | `SquareLayoutElement` (`UI/Layout/`) — 부모 높이를 보고 가로를 주장한다 |
| **내용이 늘어도 스크롤이 안 늘어난다** | `Viewport`에 직접 자식을 넣었다. Viewport는 **크기가 고정**이라 내용이 늘어도 커지지 않는다. `ScrollRect.content`도 비어 있으면 스크롤은 아예 동작하지 않는다 | 아래 "스크롤 뷰의 정석" |
| **내용이 스크롤바 밑으로 깔린다** | 자식이 자기 폭(패널 전체폭)을 주장한다. Viewport는 스크롤바만큼 좁다 | `Content`의 레이아웃 그룹에서 `Child Control Width`를 켜 폭을 넘겨받게 한다 |
| **한 번 넓어진 패널이 다시 안 줄어든다 (에디터는 멀쩡, 빌드만 깨진다)** | 그 노드의 **`min`이 자기 `rect.width`에서 파생**된다 → 아래 함정 참고 | 그 컴포넌트가 부모에게 폭을 **요구하지 않게** 한다 |

### ⚠️ `min`을 자기 폭에서 파생시키면 그 폭이 하한으로 굳는다

UGUI가 자식에게 주는 폭은 `Clamp(부모 폭, min, flexible > 0 ? 부모 폭 : preferred)`다.
**`flexible > 0`일 때 부모 폭보다 넓어질 수 있는 통로는 `min` 하나뿐**이라는 뜻이다.

그래서 **자기 현재 폭을 보고 `min`을 계산하는 컴포넌트는 순환에 빠진다.**

```
현재 폭 → min 계산 → min이 곧 하한 → 폭이 그대로 유지 → 다시 min …
```

창이 **한 프레임이라도** 넓었으면 그 폭이 최소 폭으로 남아 **다시는 줄어들지 않는다**.
에디터 Game 뷰는 계속 다시 그려 수렴하므로 티가 안 나고, **빌드는 굳은 값 그대로 간다.**

실제 사고(A-1) — `FlexibleGridLayoutGroup`이 `cellSize.x`를 자기 폭에서 역산하는데,
`GridLayoutGroup`이 그 셀 크기로 `min = padding + (cellSize.x + spacing) × 열수 − spacing`을
발표해 **`min == 현재 폭`**이 됐다. 거래 열 패널이 열보다 넓어져 옆 열을 침범했다.

**같은 계열의 함정** — `Child Control Width`를 끄면 UGUI가 그 자식의 min·preferred를
**자식의 현재 `sizeDelta`**로 잡는다. 역시 "현재 크기 = 하한"이다.

> 배치 컴포넌트를 새로 만들 때는 **"부모에게 무엇을 요구하는가"를 자기 크기와 무관하게** 정한다.
> 주어진 폭에 맞추는 것이 목적인 컴포넌트라면 가로로 요구할 것은 **패딩뿐**이다.

`WidgetPositionLayout.VerifyNoOverflow`가 이 부류를 상시 감시한다 —
자식이 부모보다 넓으면 어느 노드가 무슨 `min`을 요구했는지까지 경고로 남긴다.
단 **부모에 레이아웃 그룹이 있는 곳만** 본다. 앵커·`sizeDelta`로 직접 배치한 부모는
자식이 자기보다 넓은 게 정상일 수 있다 — 유니티 기본 `Scrollbar`의 `Sliding Area`(폭 0)가 그 예다.

### 스크롤 뷰의 정석

```
Scroll View Panel   ScrollRect   content = Content · viewport = Viewport   ← 둘 다 반드시 채운다
├─ Viewport         Image + Mask   sizeDelta (-17, 0)      ← 스크롤바 폭만큼 좁다. 레이아웃 그룹을 두지 않는다
│   └─ Content      LayoutGroup + ContentSizeFitter(v = PreferredSize)
│                   anchor (0,1)~(1,1)  pivot (0,1)        ← 위에 붙어서 아래로 자란다
│                   ChildControlWidth = on                 ← 줄 폭을 여기서 정해 스크롤바 침범을 막는다
│       └─ 줄 / 칸  LayoutElement preferredHeight 고정
└─ Scrollbar Vertical
```

**`Viewport`는 창이고 `Content`가 두루마리다.** 창에 직접 붙이면 두루마리가 없어 감을 수 없다.

### 세로 3단 배치의 정석

```
부모      VerticalLayoutGroup   ctrl(W,H)=on  expand(W)=on  expand(H)=off
├─ 머리   LayoutElement  preferredHeight 50   flexibleHeight 0    ← 고정
├─ 탭     LayoutElement  preferredHeight 50   flexibleHeight 0    ← 고정
└─ 본문   LayoutElement  preferredHeight -1   flexibleHeight 1    ← 나머지를 전부
```

`expand(H)`를 켜면 고정하려던 칸까지 늘어난다. **높이를 나누는 건 `flexibleHeight`지 `expand`가 아니다.**

**나머지를 채울 칸이 둘 이상이고 "똑같이" 나눠야 하면 `preferredHeight`를 `-1`이 아니라 `0`으로 준다.**
`-1`은 "내 내용물 높이를 먼저 챙긴다"라서, 내용이 다르면 결과가 갈린다
(`Setting Presenter`의 토글 4개 · 드롭다운 3개가 실제로 355/305로 갈렸다).

**칸 전체가 고정이어야 하는 열이면 `flexibleHeight`를 전부 0으로 둔다.**
`@Main Column`이 그렇다 — 90 + 900 + 90 = 1080 = 컬럼 높이라 남는 높이 자체가 없다.
하나라도 `flexibleHeight = 1`이면 **형제가 꺼질 때 그 자리를 혼자 빨아들인다.**

---
