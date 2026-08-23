# Main 폴더 규칙

> 최종 업데이트: 2026-08-23 (`UI 규칙.md`에서 분리) · 대상: `Assets/Scripts_Client/UI/Main/`

**`#Main Canvas` — 한 자리를 여러 화면이 갈아 끼우는 유일한 캔버스.**
`UI/`에서 규칙이 가장 많은 곳이라, 화면을 하나 더 붙이려면 여기를 읽는다.

| 폴더 | 무엇 |
|------|------|
| `MainCanvasView.cs` | 캔버스 껍데기 + **`SetTitle(string)`** (아래 "캔버스 머리의 제목") |
| `WorkStationListPresenter/` | 작업슬롯 목록 (+ 종속 View `WorkStationSlotView`) |
| `WorkStationSelectPresenter/` | 작업슬롯 선택 (+ 종속 View `CharacterStateRowView`) |
| `SettingPresenter/` | 창 설정 |
| `MenuPresenter/` | 하단 메뉴 — **항상 켜져 있다** |

이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>), 레이아웃 함정은
[`Layout 규칙.md`](<../Layout/Layout 규칙.md>)에 있다.

---

## 한 캔버스 안에서 화면을 갈아 끼운다

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
  따로 맞춰야 하고, 하나만 어긋나도 크기가 틀어진다**([`Layout 규칙.md`](<../Layout/Layout 규칙.md>)).

## 전환 층은 하나다 — 화면은 자기를 끄지 않는다

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

## 캔버스 머리의 제목은 `UIManager`가 밀어 넣는다

화면이 바뀌면 `#Main Canvas`의 `Title` 문구도 바뀐다. 문구는 **코드가 아니라
`UI Manager > Main Screens`의 각 줄**에 적혀 있다 — 표시용이라 바뀌어도 로직이 안 바뀌는데
코드에 박으면 문구 하나 고치는 데 컴파일이 필요하고, 화면 목록과 제목 목록이 따로 논다.

**이것이 캔버스 View가 위젯을 쥐는 유일한 예외다.** 어느 화면의 Presenter에 맡겨도
**그 화면이 꺼질 때 함께 죽어서**, 정작 다른 화면으로 넘어간 순간 제목을 못 바꾼다.
그래서 `MainCanvasView`가 `SetTitle(string)`을 갖는다. Model을 구독하지 않고
**위에서 밀어 넣은 값만 그리므로** 역할은 여전히 View다.

## 메인 화면 추가 — 코드는 한 줄이다

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
