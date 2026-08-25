# Widget 폴더 규칙

> 최종 업데이트: 2026-08-26 (상단 줄·수확 스트립 추가) · 대상: `Assets/Scripts_Client/UI/Widget/`

**`#Widget Canvas` — 다른 화면을 전부 닫아도 남는, 데스크톱 위젯 본체.**

| 폴더 | 무엇 |
|------|------|
| `WidgetCanvasView.cs` | 캔버스 껍데기 |
| `WidgetPresenter/` | 위젯 내용(상단 줄·수확 스트립)과 열기 버튼. 스트립 한 칸은 `WidgetMiniSlotView` |

이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>)에 있다.

---

## 위젯에 무엇이 들어 있나

높이가 **87px뿐**이다(열 높이 1080에서 가운데 950을 뺀 나머지를 위젯 2 : 상태 1로 나눈 값).
그 안에 두 줄이 들어간다.

```
Widget Presenter        VerticalLayoutGroup
├─ Top Panel            골드 · 가동 N/M · 시간당 산출 · 누적 수확      pref 26
├─ Strip Panel          배치된 칸이 왼쪽부터. 빈 칸 없음              flexH 1
│  └─ WidgetMiniSlotView 프리팹 (런타임 생성)                        49×49
└─ Open/Close Button    ignoreLayout — 레이아웃 밖에서 우하단 고정
```

**글자를 스트립에 넣지 않는다.** 49px 칸에 슬롯 번호·산업·남은 초를 적으면 읽히지 않는다.
그 정보는 큰 창의 `WorkStationSlotView`가 맡는다 — 위젯은 **"몇 칸이 돌고 있고 얼마나 찼는가"**만 본다.

### 빈 칸을 두지 않는다

큰 창의 슬롯 목록은 빈 칸도 프레임으로 남긴다 — **눌러서 배치해야 하기 때문**이다.
위젯 스트립은 누를 것이 없으므로 배치된 칸만 만들고, 해제되면 뷰를 파괴해 레이아웃이 뒤를 당긴다.

### 카운트다운 계산식은 여기 없다

`WorkStationProgress`(`UI/System/`)가 갖고 있고, 큰 창의 목록과 **같은 것을 쓴다.**
복사하면 서버 판정식이 두 벌이 되어 한쪽만 고쳐진다.

> ⏸ **아직 자리만 잡아 둔 것** — 미니 슬롯의 `Character Image`는 회색 네모이고
> `Harvest Text (TMP)`는 빈 문자열이다. 상단의 `시간당 산출`·`누적 수확`도 씬의 더미 문구다
> (산출 정의가 기획 미정이라 코드가 손대지 않는다). 지금 실제로 도는 것은 **게이지 하나**다.

---

## ⚠️ 항상 켜져 있어야 하는 컴포넌트를 여기 두지 않는다

`OnEnable`에서 일하는 컴포넌트(`WidgetPositionLayout`)를 **토글 대상 캔버스에 붙이면
그게 꺼져 있는 동안 아무 일도 하지 않는다.** 기본 상태로 꺼져 있으면 한 번도 안 돈다.
그래서 `WidgetPositionLayout`은 이 캔버스가 아니라 **상주하는 `!Horizental Columns`**에 있다
(→ [`Layout 규칙.md`](<../Layout/Layout 규칙.md>)).

## 위젯 자리는 다른 캔버스가 닫혀야 제대로 나온다

`UIManager.CloseAllExceptWidget`이 **화면만 끄지 않고 캔버스까지 끄는** 이유가 여기다.
내용이 꺼진 캔버스라도 `LayoutElement`가 컬럼 안에서 높이를 계속 차지해서, 캔버스를
남겨 두면 그 950px가 안 사라져 **위젯이 창 가장자리에서 밀린다.**

> ⚠️ 이것이 오버레이를 뺀 나머지를 `CanvasGroup`이 아니라 `SetActive`로 여닫는 근거 중
> 하나다 — [`System 규칙.md`](<../System/System 규칙.md>)의 "무엇으로 여닫는가".
