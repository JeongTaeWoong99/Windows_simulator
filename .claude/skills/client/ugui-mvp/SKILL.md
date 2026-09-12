---
name: ugui-mvp
description: uGUI 화면을 MVP로 짤 때의 역할 분담·이름 규칙·부착 위치·폴더 구조·Presenter 뼈대. 화면이나 위젯을 새로 만들거나, 클래스/오브젝트 이름을 정하거나, 스크립트를 어느 오브젝트에 붙일지 정할 때 적용한다. 이미 있는 화면의 값만 고치는 작업이나 배치 문제(→ ugui-layout)에는 적용하지 않는다.
---

> 최종 업데이트: 2026-09-12 (`UI/Shared/` — 캔버스에 속하지 않는 공용 표현 부품의 자리)

# uGUI MVP 골격

UI Toolkit이 아닌 uGUI 기반 MVP. 형태는 Unity 공식 학습 샘플
`LevelUpYourCode / DesignPatterns / MVP(Legacy)`를 따른다.

**레퍼런스의 `HealthPresenter`는 `[SerializeField] Slider m_HealthSlider`로 위젯을 직접 쥔다.
화면마다 View 클래스를 두지 않는다.** 여기도 같다 — Presenter가 자기 화면의 위젯을 직접 들고 그린다.

> **그럼 슬롯 View 같은 건 왜 있나?** 레퍼런스에는 **반복되는 칸이 없어서** 그 사례가 없었을 뿐이다.
> 같은 것이 N개 복제되고 각자 다른 데이터에 묶이면 위젯을 Presenter가 다 들고 있을 수 없다.
> 그래서 **반복 칸만** View 클래스를 갖는다.

---

## 1. 세 역할 + 조정자

```
┌─ Model ──────────────────────  Managers/
│   XxxModel        상태를 들고 변경 이벤트를 쏜다
└──────────────────────────────────────────────────────────
        ▲ 요청(호출)                     │ 변경 이벤트(구독)
        │                                ▼
┌─ Presenter ──────────────────  UI/<캔버스>/<Presenter>/
│   Model을 구독해 위젯에 그리고, 입력을 받아 Model로 넘긴다.
│   ※ 로직·상태·저장을 갖지 않는다.
└──────────────────────────────────────────────────────────
        │ Bind(값)                       ▲ event
        ▼                                │
┌─ View ───────────────────────
│   캔버스 View  화면 단위로 켜고 끈다 (Show)
│   종속 View    Presenter가 Bind로 값을 밀어넣는 반복 칸
└──────────────────────────────────────────────────────────

┌─ 조정자 (MVP 밖) ────────────
│   UIManager    무엇을 열고 닫을지 결정하는 단일 출입구
└──────────────────────────────
```

**Presenter는 일을 하는 곳이 아니라 넘기는 곳이다.** 이 한 줄이 아래 규칙 전부의 근거다.

```csharp
// 좋다 — 넘기기만 한다. 위젯이 20개가 되어도 20줄이다.
BindToggle(someToggle, model.Flag, model.SetFlag);

// 나쁘다 — 로직이 UI로 새어 들어왔다. 위젯 수 × 로직 줄 수로 폭발한다.
someToggle.onValueChanged.AddListener(on => { /* 조건 분기·저장·부수효과 */ });
```

- **`UIManager`는 Presenter가 아니다.** Model을 구독하지 않고 위젯도 쥐지 않는다 —
  "무엇을 열고 닫나"만 정하는 조정자다.
- **시스템 서비스는 Model을 겸할 수 있다.** 상태를 들고 이벤트를 쏘면 Model이다.

---

## 2. 이름 규칙 — 접미사는 **MVP 역할**을 따른다

> 접미사를 "붙는 오브젝트"(`...CanvasUI` / `...PanelUI`)를 따라 지으면,
> 같은 이름에 껍데기·조정자·Presenter가 뒤섞여 **역할이 안 보인다.**

### 클래스

| 역할 | 접미사 | 예 |
|---|---|---|
| 상태를 들고 이벤트를 쏜다 | `...Model` | `PlayerDataModel` |
| 구독해서 그리고, 입력을 넘긴다 | `...Presenter` | `LoginPresenter` · `ItemGridPresenter` |
| **캔버스 껍데기** — `Show(bool)`만 | `...CanvasView` | `MainCanvasView` |
| **반복되는 한 칸** — Presenter가 `Bind`한다 | `...View` | `ItemSlotView` |
| 배치를 계산하는 컴포넌트 | `...Layout` / `...LayoutGroup` | `FlexibleGridLayoutGroup` |

> **캔버스는 언제나 `...CanvasView`다.** 자기 안의 화면을 갈아 끼우는 캔버스를 `...CanvasPresenter`라
> 부르면 View 캔버스와 Presenter 캔버스가 섞인다. **전환은 캔버스가 아니라 그 안의 Presenter가 한다.**

### 오브젝트 — 접두사로 계층, 이름과 표기로 역할

```
!  최상위 · 다른 축      !Horizontal Columns · !Login Canvas (MAIN VIEW)
@  컬럼                  @Main Column
#  캔버스                #Main Canvas (MAIN VIEW)
(없음)  Presenter·패널·위젯   Menu Presenter (↓ SUB VIEW) · Header Panel · Gold Text
```

| 표기 | 무엇인가 |
|---|---|
| `(MODEL)` | 상태를 들고 이벤트를 쏘는 오브젝트 |
| `(MAIN VIEW)` | **캔버스** — 화면 단위로 켜고 끄는 껍데기 |
| `(↓ SUB VIEW)` | **Presenter** — 아래가 전부 위젯이다 |
| 표기 없음 | 컬럼 · 정렬용 패널 · 위젯 · 정적 요소 |

- **표기는 스크립트가 붙은 오브젝트에만 붙인다.** 위젯 하나하나에는 붙이지 않는다 —
  Presenter 아래는 어차피 전부 그 Presenter가 그리는 것이라 정보를 더하지 않고,
  `Viewport`·`Template`처럼 Unity가 자동 생성하는 부품까지 번지면 **어디가 화면 경계인지 안 보인다.**
- **`↓` 뒤는 "이 Presenter를 펼치면 무엇이 나오는가"다.** 자식이 아예 없으면 그냥 `(PRESENTER)`.

### `Presenter`와 `Panel`을 이름으로 가른다

| 이름 | 무엇인가 | 스크립트 |
|---|---|---|
| `Xxx Presenter (↓ SUB VIEW)` | **화면 하나.** 켜고 끄는 단위이자 배선의 주인 | `XxxPresenter` 하나 |
| `Xxx Panel` | Presenter **안에서** 서브 뷰를 줄 세우는 상자 | **없다.** 레이아웃 그룹만 |

**오브젝트 이름에 캔버스 이름을 되풀이하지 않는다.** 클래스는 어셈블리 전체에서 유일해야 해서
`StorageTabPresenter`처럼 캔버스 이름을 앞에 달지만, 오브젝트는 이미 그 캔버스 안이라
문맥이 붙는다 — `Tab Presenter`면 충분하다.

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

> ⚠️ **어겼을 때 실제로 생기는 문제 — 캔버스를 끄면 Presenter도 같이 죽는다.**
> 여닫기와 표시가 한 오브젝트에 묶여 "닫혀 있는 동안 갱신"이나 "화면만 교체"가 불가능해진다.

### Presenter가 자기 위젯을 쥔다 — 상위가 건너뛰어 잡지 않는다

상위 Presenter가 자식 화면의 버튼을 직접 들면, 버튼이 늘어날수록
**상위가 남의 화면 사정을 알게 된다.** 그 화면의 Presenter가 자기 것을 쥔다.

### 컴포넌트 순서 · 여백은 종류별로 똑같이 맞춘다

같은 종류의 오브젝트가 인스펙터에서 다르게 생겼으면 **다른 것인지 그냥 어긋난 것인지 알 수 없다.**

**순서** — "크기 주장 → 그리기 준비 → 그리는 것 → 입력 → 자식 배치 → 내 코드"

```
RectTransform                          ← Unity가 맨 위에 고정한다. 못 옮긴다
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

**여백** — 계층에 따라 두 값뿐으로 통일한다. 예: 열 사이만 `spacing 10`,
**캔버스 이하 전부 `padding 5 5 5 5` · `spacing 5`**, 위젯 속살(반복 줄·Unity 토글 내부)은 건드리지 않는다.
마지막이 예외인 이유 — 높이 30 남짓인 줄에 위아래 5씩 넣으면 **내용이 눌린다.**
섹션의 여백과 위젯 안쪽 여백은 다른 문제다.

> 에디터를 스크립트로 만질 때: **컴포넌트 순서는 `ComponentUtility.MoveComponentUp`으로만 바꾼다.**
> `m_Component`를 `SerializedObject`로 직접 건드리면 Unity가 거부하고
> (`It is not allowed to modify the m_Component property`),
> `MoveComponentRelativeToComponent`는 대화상자를 띄워 자동화가 끊긴다.

---

## 4. 위젯·화면을 추가할 때 — 판단 흐름

```
버튼(또는 토글/드롭다운)을 하나 더 붙이려 한다
        │
        ├─ 같은 것이 N개 반복되나? (파라미터만 다른가)         → View 컴포넌트 1개 만들고 N번 복제
        ├─ 그 위젯이 자기만의 상태를 구독해 자기 모습을 바꾸나?  → View
        ├─ 캔버스의 자리를 통째로 차지하는 화면인가?            → Presenter 신설 + 전환 담당이 켜고 끈다
        └─ 그냥 기능 위젯 하나 더인가?                          → 그 화면 Presenter의 Start()에 1줄 추가. 끝.
```

**"버튼 하나 = 스크립트 하나"는 하지 않는다.** 로직은 어차피 Model에 있으므로,
버튼마다 클래스를 만들면 `Start()`·`RequireRef`·`Services.Get()` 보일러플레이트만 N배가 되고 얻는 게 없다.

> ⚠️ `button.onClick.AddListener(...)`는 **이미 옵저버 패턴이다.**
> "옵저버로 바꿀까"라는 선택지는 없다. 정할 건 **구독자를 몇 개 둘 것인가**뿐이다.

---

## 5. 폴더 규칙 — `<캔버스>/<Presenter>/`

**캔버스 폴더 아래에 Presenter 이름의 폴더를 둔다.** 그 안에 Presenter 스크립트와
**그 Presenter에 종속된 View**가 함께 산다. 폴더를 열면 "이 화면은 무엇으로 이뤄졌나"가 바로 보인다.

```
UI/
├─ UI 규칙.md
├─ Login/
│   ├─ LoginCanvasView.cs
│   └─ LoginPresenter/
│       └─ LoginPresenter.cs
├─ Storage/
│   ├─ StorageCanvasView.cs
│   ├─ StorageTabPresenter/
│   │   └─ StorageTabPresenter.cs
│   ├─ ItemGridPresenter/
│   │   ├─ ItemGridPresenter.cs
│   │   └─ ItemRowView.cs             ← 종속 View (이 Presenter만 쓴다)
│   └─ ItemDetailPresenter/
│       └─ ItemDetailPresenter.cs
└─ Shared/                            ← 캔버스에 속하지 않는 공용 표현 부품
    ├─ Shared 규칙.md
    └─ ItemSlotView.cs                ← 여러 캔버스가 함께 쓰는 칸
```

- **폴더 이름 = 그 안에 사는 Presenter의 클래스 이름.** 오브젝트 이름이 아니다.
- **깊이는 두 단(`<캔버스>/<Presenter>/`)에서 멈춘다.** Presenter 폴더를 중첩하지 않는다 —
  하이어라키에서도 Presenter 안에 Presenter를 두지 않기 때문이다.
- **캔버스 폴더마다 `<폴더명> 규칙.md`를 둔다.** 그 캔버스에만 걸리는 규칙·함정은 거기 쓴다.
- **빈 폴더는 만들지 않는다.** Presenter 스크립트가 생길 때 그 폴더를 만든다.
- 에디터 전용 스크립트는 반드시 `Editor/` 하위에. 안 그러면 빌드에 포함돼 컴파일이 깨진다.
- Model·조정자는 이 폴더에 두지 않는다 → `Managers/`
- **씬에 붙지 않는 파일에는 비칠 대상이 없다 — 자리를 따로 만든다.**
  폴더가 하이어라키의 거울이면, **여러 캔버스가 함께 쓰는 표현 부품**(반복 칸·색/문구 변환표·
  값 struct)은 어느 캔버스에도 속하지 않는다. 자리를 정해 두지 않으면 *처음 쓴 화면*의 폴더에
  놓이고, 그 순간 **위치가 소유권을 거짓으로 주장한다** — 그 화면을 고치다 다른 화면을 조용히
  바꾸게 되고, 이름에도 그 화면 이름이 붙어 함께 거짓이 된다.
  → **`UI/Shared/`에 두고 `Shared 규칙.md`에 자격과 배제 기준을 못박는다.**

  | 들어온다 | 들어오지 않는다 |
  |---|---|
  | 두 캔버스 이상이 쓰는 반복 칸·완성값 struct | 한 캔버스만 쓰는 종속 View → 그 Presenter 폴더 |
  | 화면과 무관한 표시용 변환표(코드→문구, 등급→색) | 상태를 들고 있는 서비스 → `Managers/` |
  | 이 게임의 개념을 아는 표현 헬퍼 | 게임을 모르는 범용 코드 → `Common/`(툴킷) |

  ⚠️ **"두 곳에서 쓰니까"만으로는 부족하다.** 한 캔버스의 두 Presenter가 함께 쓰는 것은
  아직 소유자가 하나라서 **그 캔버스 폴더**에 둔다. 반대로 **한 캔버스만 쓰게 줄어들면 내린다** —
  공용 폴더는 지금의 사실을 적는 자리다.

### enum은 소유자 파일 안에 둔다

`MainScreen`은 `UIManager.cs`에, `WindowScale`은 `WindowManager.cs`에. **한 줄짜리 파일을 만들지 않는다.**
다른 파일에서 써도 상관없다 — 단일 어셈블리다.

---

## 6. Presenter 뼈대

```csharp
public class XxxPresenter : MonoBehaviour
{
    [CenterHeader("참조")]                                 // ※ < > 를 넣지 않는다 (아래 표)
    [SerializeField, Tooltip("... OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button someButton = null!;

    private XxxModel _data = null!;
    private bool _isSubscribed;
    private bool _isReady;   // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다
    private void Start()
    {
        this.RequireRef(someButton, nameof(someButton));   // 미연결이면 즉시 예외 (fail-fast)

        _data = Services.Get<XxxModel>();                  // ※ 반드시 Start

        Subscribe();
        someButton.onClick.AddListener(OnClicked);
        Refresh();                                          // 이미 데이터가 와 있을 수 있다

        _isReady = true;
    }

    private void OnEnable()  { if (_isReady) { Subscribe(); Refresh(); } }
    private void OnDisable() { Unsubscribe(); }
}
```

| 규칙 | 이유 |
|---|---|
| **`onClick`은 코드로 연결한다.** 인스펙터에서 연결하지 않는다 | 씬 파일에 묻혀 검색이 안 되고, 메서드 이름을 바꾸면 조용히 끊긴다 |
| **`Services.Get<T>()`는 반드시 `Start()`** 에서 | `Awake`·`OnEnable`은 등록 순서가 보장되지 않는다 |
| **필수 참조는 `= null!` + `RequireRef`** | `?`로 두면 미연결이 조용히 무시돼 "왜 안 되지"가 된다. 선택 참조만 `?` |
| **`RequireRef`는 Presenter가 `Start`, 종속 View가 `Awake`** | View는 상위 Presenter의 `Start`가 `Bind`를 부르기 전에 검증이 끝나 있어야 한다 |
| **Unity 객체에 `?.`·`??`를 쓰지 않는다** | Unity의 `==` 오버로드를 건너뛰어 **"가짜 null"**(파괴된 오브젝트)을 통과시킨다. `== null`로 검사한다. `?.`는 순수 C#(이벤트·컬렉션)에만 |
| **`OnEnable`에서 재구독하고 다시 그린다** | 꺼져 있는 동안 도착한 이벤트를 놓쳤다. 재구독만으론 화면이 낡은 채 남는다 |
| **`OnDisable`에서 반드시 구독 해제** | 안 하면 꺼진 UI가 계속 반응한다 |
| **반복 변수를 람다에 그대로 넘기지 않는다** | 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다 |
| **매 프레임 도는 건 한 곳에만** | 슬롯마다 `Update`를 두면 비용이 슬롯 수만큼 곱해진다. 계산은 Presenter가 하고 View엔 결과만 넘긴다 |
| **`[CenterHeader]`에 `< >`를 넣지 않는다** | 드로어가 그릴 때 `$"< {text} >"`로 감싼다. 넣으면 인스펙터에 `< < 참조 > >`가 나온다 |
| **배열 필드 위의 `[CenterHeader]`에는 `[NonReorderable]`을 같이 단다** | 배열은 기본이 reorderable list로 그려지는데, 그 경로가 데코레이터를 건너뛰어 **헤더가 통째로 안 보인다**(에러도 경고도 없다). 덤으로 **드래그로 순서가 뒤바뀌는 사고**도 막는다 |
| **인스펙터 필드는 화면에 나오는 순서로 둔다** | 코드 순서와 사용자가 보는 순서가 어긋나면 읽는 사람이 매번 다시 짜맞춘다 |

### 캔버스 View 쪽 규약

```csharp
public class XxxCanvasView : MonoBehaviour
{
    // 이 캔버스를 열고 닫는다 (UIManager가 호출).
    public void Show(bool on) => gameObject.SetActive(on);
}
```

이게 전부다. **여기에 위젯 참조가 하나라도 생기면 Presenter를 만들어 옮긴다.**
예외는 여러 화면이 함께 쓰는 머리 하나뿐이다(`SetTitle(string)` 꼴).
가르는 기준은 위젯을 쥐느냐가 아니라 **Model을 구독하느냐**다 —
위에서 밀어 넣은 값만 그리면 View, 스스로 이벤트를 받아 갱신하면 Presenter다.

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

    public void Bind(int id, string displayName) { /* 완성된 값을 받아 그린다 */ }
}
```

- **`= null!`만 쓰고 `RequireRef`를 빼면 안 된다** — 미연결이 `Bind` 첫 호출에서
  `NullReferenceException`으로 터지는데, 그때는 **어느 필드인지가 안 찍힌다.**
- **서비스를 조회하지 않는다.** 변환이 필요한 값은 **Presenter가 만들어 넘긴다.**
- **스스로 시간을 세지 않는다.** `Update`·코루틴을 두지 않는다.
