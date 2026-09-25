# UI 규칙

> 최종 업데이트: 2026-09-25 (부가 정보는 툴팁으로 — T-088) · 2026-09-16 (여백 예외 — 격자 프레임 안의 칸은 꽉 채운다) · 2026-09-14 (폴더 트리에 `EfficiencyRowView`·`AptitudeLabel` 추가 — T-053) · 대상: `Assets/Scripts_Client/UI/`

이 폴더에 스크립트를 새로 만들기 전에 읽는다. **이름을 뭐라고 붙일지 · 어느 오브젝트에 붙일지 ·
어느 폴더에 넣을지**를 여기서 정한다.

이 문서는 `UI/` **전체에 걸리는 규칙**만 담는다. 특정 캔버스의 사정과 함정은
그 폴더의 규칙 문서에 있다.

> **§0~§6의 범용 골격은 [`ugui-mvp` 스킬](<../../../.claude/skills/client/ugui-mvp/SKILL.md>)로 승격했다**(2026-08-26).
> MVP 세 역할 · 이름 규칙 · 부착 위치 · 폴더 규칙 · Presenter 뼈대는 거기가 원본이고,
> 여기는 **이 프로젝트의 실제 캔버스·오브젝트로 그 규칙을 구체화한 것**이다. 둘이 어긋나면 스킬이 기준이다.


## 어디를 읽나

| 무엇을 알고 싶나 | 어디 |
|---|---|
| 왜 MVP인가 · 세 역할은 무엇인가 | 이 문서 §0~1 |
| 클래스·오브젝트 **이름**을 뭐라고 붙이나 | 이 문서 §2 |
| 스크립트를 **어느 오브젝트**에 붙이나 · 여백·컴포넌트 순서 | 이 문서 §3 |
| 위젯·화면을 **추가**하려면 | 이 문서 §4 |
| **어느 폴더**에 넣나 | 이 문서 §5 |
| 코드 작성 규약 (Presenter · 캔버스 View · 종속 View) | 이 문서 §6 |
| ⚠️ Canvas · 레이아웃 그룹의 **함정** | [`ugui-layout` 스킬](<../../../.claude/skills/client/ugui-layout/SKILL.md>) — 이 프로젝트 고유 배치는 [`Layout 규칙.md`](<Layout/Layout 규칙.md>) |
| **여러 캔버스가 함께 쓰는 칸·변환표**를 어디 두나 | [`Shared 규칙.md`](<Shared/Shared 규칙.md>) |
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

> **그럼 `SlotView` 같은 건 왜 있나?** 레퍼런스에는 **반복되는 칸이 없어서** 그 사례가
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
| **반복되는 한 칸** — Presenter가 `Bind`한다 | `...View` | `SlotView` · `WorkStationSlotView` · `CharacterStateRowView` |
| 배치를 계산하는 컴포넌트 | `...Layout` / `...LayoutGroup` | `WidgetPositionLayout` · `FlexibleGridLayoutGroup` |

> ⚠️ **반복 칸 중에도 "주인이 하나가 아닌 것"이 있다.** `WorkStationSlotView`·`CharacterStateRowView`는
> 한 Presenter만 쓰므로 그 폴더에 살지만, `SlotView`는 **창고 격자와 가챠 결과 팝업이 함께**
> 쓴다 — 그래서 자리가 [`Shared/`](<Shared/Shared 규칙.md>)다. **이름 규칙은 같고, 자리만 다르다.**

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
| `(MODEL)` | **상태를 들고 이벤트를 쏘는** 오브젝트. 서버 상태(`PlayerData`)뿐 아니라 여러 화면이 함께 보는 선택 상태(`SellCart`)도 여기다 |
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

**클래스 이름은 어셈블리 전체에서 유일해야 한다.** 그래서 `StorageTabPresenter`·`StorageGridPresenter`처럼
캔버스 이름을 앞에 다는 것이 많다 — 하지만 **접두는 규칙이 아니라 수단이다.**
바른 이름이 이미 유일하면 붙이지 않는다: `MenuPresenter`·`GachaPresenter`·`WorkStationListPresenter`가 그렇고,
`SellCartPresenter`는 `SellCartModel`과 짝이 맞는 고유한 이름이라 `Storage` 접두를 달지 않는다(2026-09-12 · T-049).
⚠️ **접두를 붙였다가 역할이 바뀌면 이름이 거짓이 된다** — `StorageInformationPresenter`가 그래서 개명됐다.

**오브젝트 이름에는 캔버스 이름을 되풀이하지 않는다.** 오브젝트는
이미 그 캔버스 안에 들어 있어 문맥이 붙는다 — `Tab Presenter`·`Grid Presenter`면 충분하다.
**낱말은 띄운다** — 클래스 `AmountInputPresenter`는 오브젝트 `Amount Input Presenter`다
(`WorkStation`처럼 한 낱말인 도메인 용어는 붙여 둔다).

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

**여백** — **"안에 무엇이 들어가는가"로 가른다.** 깊이로 가르지 않는다.

| 어디 | padding | spacing |
|---|---|---|
| `!Horizental Columns` | 0 | **10** (열 사이) |
| `@Xxx Column` | 0 | 0 |
| 캔버스 | **5 5 5 5** | 5 |
| **위젯이 직접 들어가는 상자** — 버튼 줄 · 스크롤 `Content` · 반복 줄 · 팝업 창 | **5 5 5 5** | 5 |
| **상자를 쌓기만 하는 자리** — Presenter가 하위 패널을 세로로 세우는 곳 | **0** | 5 |
| **스크롤 패널** | LayoutGroup을 두지 않는다 (아래) | — |
| 이미 여백을 준 상자 **안**의 정렬 전용 상자 | 0 | 5 |

**스크롤 패널에 여백을 주지 않는 이유** — 뷰포트에 여백이 붙으면 스크롤바가 안쪽으로
밀려 들어와 목록과 어긋난다. **여백 대신 뷰포트 `sizeDelta`로 바 자리를 비운다**
(`Grid Presenter`·`Sell Scroll View Panel` 둘 다 `(-17, 0)`). 여백이 필요하면
바깥이 아니라 **안쪽 `Content`에** 준다 — 그래야 바가 제자리에 남는다.

마지막 줄의 예 — `Window Panel`이 이미 5를 줬으므로 그 안의 `Button Panel`은 0이다.
둘 다 주면 10이 된다.

**예외 — 격자 프레임 안의 칸은 여백 0으로 꽉 채운다** (2026-09-16). 창고 `Slot (N)` 프레임에 들어가는
`SlotView`가 그렇다 — 프레임이 곧 칸의 테두리이고 칸 사이 간격은 바깥 `Content`의 격자가 이미 준다.
이런 **특수한 경우는 괜찮다.** 대신 예외를 둔 자리의 폴더 규칙에 이유를 적는다
→ [`Storage 규칙.md`](<Storage/Storage 규칙.md>) "칸은 프레임을 꽉 채운다".

**스프라이트** — 역할이 곧 스프라이트다.

| 역할 | 스프라이트 | 왜 |
|---|---|---|
| **맨 뒤 베이스** — 캔버스 본체 · 전체화면 차단막 | **`null`** (각진 네모) | 화면을 꽉 채우는 것에 둥근 모서리를 주면 구석이 비어 보인다 |
| **격자에 빈틈없이 붙는 반복 칸** — `Slot` · `Work Slot` · `Character State Row` · 판매 줄 | **`null`** | 둥글면 칸 사이에 틈이 생긴 것처럼 보인다 |
| **그 위에 얹히는 영역 상자** — `Title` · Presenter 배경 · `Xxx Panel` · 스크롤 패널 · 스크롤바 트랙 · 팝업 창 | **`Background`** (Sliced) | 얹힌 것은 경계가 보여야 한다 |
| **누르는 것** — Button · Toggle · Dropdown · Scrollbar `Handle` | `UISprite` (Sliced) | 누를 수 있음이 생김새로 드러난다 |
| 뷰포트 | `UIMask` (Sliced) | ⚠️ `Simple`이면 마스크가 통째로 둥글어진다 (아래) |
| 입력칸 | `InputFieldBackground` (Sliced) | — |

⚠️ **`Background`·`UISprite`·`UIMask`는 반드시 `Sliced`다.** `Simple`이면 9-slice가 풀려
모서리가 크기에 비례해 늘어난다.

> Unity 에디터를 스크립트로 만질 때: **컴포넌트 순서는 `ComponentUtility.MoveComponentUp`으로만 바꾼다.**
> `m_Component`를 `SerializedObject`로 직접 건드리면 Unity가 거부하고
> (`It is not allowed to modify the m_Component property`),
> `MoveComponentRelativeToComponent`는 대화상자를 띄워 MCP에서 실행이 끊긴다.

### 스크롤은 이미 서 있는 것을 그대로 베낀다

`Grid Presenter`가 기준이다. 손으로 만들면 **눈에 안 띄는 두 값이 빠진다.**

| 어디 | 값 | 빠뜨리면 |
|---|---|---|
| `Viewport > Image` | 스프라이트 `UIMask` + **`type = Sliced`** | Simple이면 32×32 스프라이트의 둥근 모서리가 뷰포트 크기로 늘어나, **마스크가 목록을 거대한 둥근 사각형으로 잘라낸다** |
| `Viewport > RectTransform` | 앵커 전체 늘림 · pivot `(0, 1)` · **sizeDelta `(-17, 0)`** | 바(20)에서 겹침(3)을 뺀 값이다. 안 맞추면 목록과 스크롤바 사이에 틈이 남거나 바 밑으로 들어간다 |

**둘 다 조용히 틀린다** — 에러도 경고도 없고 화면만 이상하다.

**스크롤바 표시는 목록 길이가 변하는지로 가른다.**

| | `ScrollRect` | 왜 |
|---|---|---|
| 창고 격자 | `AutoHideAndExpandViewport` · spacing **-3** | 칸 200개라 **늘 넘친다.** 자동이든 상시든 결과가 같다 |
| 판매 목록 | **`Permanent`** | 담고 빼기로 길이가 계속 변한다. 자동이면 바가 나타났다 사라질 때마다 **뷰포트 폭이 바뀌어 줄이 다시 흐른다** |

> ⚠️ **`Permanent`는 뷰포트 폭을 `ScrollRect`가 더 이상 건드리지 않는다는 뜻이다.**
> `AutoHide`에서 바꿔 오면 그때 써 둔 값이 그대로 굳으므로 **`sizeDelta`를 직접 못박는다.**

**휠 감도(`Scroll Sensitivity`)는 목록 스크롤 전부 `20`이다.** 유니티 기본값 `1`은 한 번 굴려
1픽셀이라 목록이 안 움직이는 것처럼 보인다. 화면마다 다르면 같은 손짓에 다른 거리가 나가
"이 화면만 뻑뻑하다"가 된다 — **새 스크롤을 만들면 여기도 맞춘다.**
※ 드롭다운 안의 `Template`은 예외다 — Unity 기본 위젯의 속살이라 건드리지 않는다(여백과 같은 이유).

### `LayoutGroup`이 배치하는 프리팹은 만든 자리에서 바로 태운다

```csharp
Instantiate(rowPrefab, rowParent);   // ← 아직 프리팹에 저장된 크기 그대로다
...
LayoutRebuilder.ForceRebuildLayoutImmediate(rowParent);   // 다 만든 뒤 한 번
```

uGUI의 레이아웃 계산은 **그 프레임 맨 끝**(`Canvas.willRenderCanvases`)에 돈다.
그 사이 `WidgetPositionLayout.VerifyNoOverflow`가 `LateUpdate`에서 훑고 지나가
**"자식이 부모보다 넓다" → 다음 검사에서 "넘침이 해소됐다"** 가 왕복으로 찍힌다.
고칠 것이 없는 경고라 진짜 넘침을 덮는다.

> ⚠️ **프리팹 크기를 부모보다 작게 저장해 피하려 들지 말 것.** 실제로 그렇게 해 봤는데
> **자식 하나(TMP 기본 200×50)가 남아 경고가 한 단계 아래로 옮겨 갔을 뿐이다.**
> 크기를 맞추는 방식은 노드 수만큼 손이 가고, 프리팹을 고칠 때마다 다시 깨진다.
>
> `SlotView`가 이 경고를 안 내는 것은 크기를 맞춰서가 아니라
> **`Slot` 프레임이 `LayoutGroup`이 아니라 앵커로 배치**해서 즉시 자리가 잡히기 때문이다.

### 폰트에 없는 글자를 씬에 적지 않는다

`neodgm_pro SDF`는 **Static 아틀라스**라 실행 중에 글리프가 추가되지 않는다.
`⚠` 같은 기호는 `□`로 바뀌고 매 프레임 경고가 뜬다 — **기호 대신 `!`·`[!]`처럼 ASCII를 쓴다.**
확인은 코드에서 `font.HasCharacter(c)`로 한다.

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

### 부가·세부 정보는 레이아웃에 펼치지 않고 툴팁으로 (2026-09-25 · T-088)

"고르는 데 꼭 필요하지는 않지만 궁금하면 보는 것"(레벨 스펙·드롭 확률)과 **이미지로 바뀔 버튼의 이름**은
대상에 `TooltipTrigger`를 붙여 **올리면 뜨게** 한다. 버튼 아래로 패널을 펼쳤다 접으면 아래 내용이
밀리고, 누르는 동작(고르기)과 보는 동작이 한 버튼에 묶인다 — 산업 레벨 정보가 그래서 옮겨졌다.
고정 문구는 인스펙터 `text`에, 동적 내용은 Presenter가 `SetProvider`로 넘긴다
(→ [`System 규칙.md`](<System/System 규칙.md>)의 "툴팁").

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
│   ├─ StorageGridPresenter/
│   │   ├─ StorageGridPresenter.cs
│   │   └─ XxxSlotSource.cs              ← 탭별 공급자 (화면이 아니라 데이터)
│   │                                      ※ 칸은 캔버스를 넘어 공유돼 Shared/ 에 있다
│   └─ SellCartPresenter/
│       ├─ SellCartPresenter.cs
│       └─ SellCartRowView.cs            ← 종속 View
├─ Main/
│   ├─ Main 규칙.md
│   ├─ MainCanvasView.cs
│   ├─ WorkStationListPresenter/
│   │   ├─ WorkStationListPresenter.cs
│   │   └─ WorkStationSlotView.cs        ← 종속 View
│   ├─ WorkStationSelectPresenter/
│   │   ├─ WorkStationSelectPresenter.cs
│   │   ├─ CharacterStateRowView.cs      ← 종속 View (목록 줄 · 세팅 카드)
│   │   └─ EfficiencyRowView.cs          ← 종속 View (효율 계산 한 줄)
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
├─ System/                                ← 캔버스 하나에 오버레이 넷
│   ├─ System 규칙.md
│   ├─ SystemCanvasView.cs
│   ├─ LoadingPresenter/
│   │   └─ LoadingPresenter.cs
│   ├─ GachaResultPresenter/
│   │   └─ GachaResultPresenter.cs
│   ├─ AmountInputPresenter/             ← 화면 전체를 막아야 해서 여기 산다
│   │   └─ AmountInputPresenter.cs
│   ├─ TooltipPresenter/                 ← 어느 캔버스 위에든 떠야 해서 여기 산다 (막지 않는다)
│   │   ├─ TooltipPresenter.cs
│   │   └─ TooltipRowView.cs
│   └─ NoticePresenter/
│       └─ NoticePresenter.cs
├─ Shared/                                ← 예외. 캔버스에 속하지 않는 공용 표현 부품
│   ├─ Shared 규칙.md
│   ├─ SlotView.cs              ← 창고 격자와 가챠 결과가 함께 쓰는 칸
│   ├─ SlotData.cs                ← 그 칸에 넘기는 완성값
│   ├─ ResultMessages.cs · RarityPalette.cs
│   ├─ WorkStationProgress.cs · IndustryLabel.cs · AptitudeLabel.cs
│   └─ TooltipTrigger.cs · TooltipContent.cs  ← 툴팁을 달 대상 · 그 내용
└─ Layout/                                ← 예외. 화면이 아니라 배치 계산
    ├─ Layout 규칙.md
    ├─ WidgetPositionLayout.cs
    └─ WindowDragArea.cs

       ※ 범용 배치 컴포넌트(FlexibleGridLayoutGroup · SquareLayoutElement)는
         2026-08-26에 Common/ugui-layout/ 으로 내려갔다
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
- **예외는 둘이다.** `Layout/`은 화면이 아니라 **배치 계산**이고,
  `Shared/`는 **캔버스를 넘어 공유되는 표현 부품**이다 — 둘 다 어느 캔버스에도 속하지 않는다.
  ⚠️ **폴더가 하이어라키의 거울이면 씬에 붙지 않는 파일은 비칠 대상이 없다.** 자리를 정해 두지
  않으면 *처음 쓴 화면*의 폴더에 놓이고, 그 순간 **위치가 소유권을 거짓으로 주장한다**
  (→ [`Shared 규칙.md`](<Shared/Shared 규칙.md>)의 들어올 자격·배제 기준).

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

