---
date: 2026-08-10
title: UI 레이어를 MVP(Legacy) 구조로 재편 + 메인 화면 토글
tags: [client, ui, refactor, mvp, scene]
---

# UI 레이어를 MVP(Legacy) 구조로 재편 + 메인 화면 토글

## 목적 / 배경

구조는 이미 MVP였는데 **이름과 계층이 역할을 말하지 않았다.** 접미사가 "붙는 오브젝트"를 따르는
규칙이라 같은 `CanvasUI` 이름에 껍데기·조정자·Presenter가 뒤섞여 있었다.

목표는 Unity 공식 샘플 `LevelUpYourCode / DesignPatterns / MVP(Legacy)` 형태에 맞추는 것.
작업 중 사용자 요청으로 **`#Main Canvas` 통합**, **메인 화면 토글**, **역할 표기**,
**껍데기 Presenter 신설**, 그리고 마지막에 **전환 층 평탄화 + `Presenter`/`Panel` 이름 분리**가 붙었다.

> 표기 방식은 **세 번 바뀌었다** — `(VIEW)`/`(PRESENTER)` → `(SUB VIEW)`까지 전면 부착 →
> 최종 `(MAIN VIEW)`/`(↓ SUB VIEW)`. 아래 "표기를 세 번 갈아엎었다" 참조.

결과 구조와 규칙은 전부
[`UI 스크립트 규칙.md`](../../Assets/Scripts_Client/UI/UI%20스크립트%20규칙.md)에 있다. 여기엔 결정과 지뢰만 남긴다.

## 주요 결정 / 근거

### 레퍼런스에는 화면별 View 클래스가 없다 — 그래서 만들지 않았다

"Presenter와 View를 더 나눌까"가 두 번 쟁점이 됐는데, **레퍼런스를 직접 열어 보니
`HealthPresenter`가 `[SerializeField] Slider m_HealthSlider`로 위젯을 직접 쥐고 있었다.**
`Scripts/` 아래는 `Model/`·`Presenter/`·`User/` 셋뿐이고 `View/` 폴더가 없다.
`View.prefab`은 스크립트가 안 붙은 UI 오브젝트다.

→ **Presenter가 위젯을 직접 보유하는 것이 이 패턴의 정상 형태다.** 화면별 `XxxView` 신설은
레퍼런스보다 엄격한 것이라 하지 않았다. 반복 칸 3종만 View인 이유는
**레퍼런스에 반복 요소가 없어서 그 사례가 없었을 뿐**이다.

### 화면마다 캔버스를 두지 않는다 — `#Main Canvas` 하나에 화면 여럿

예전엔 `#WorkStation Canvas`·`#Setting Canvas`가 `@Main Column`의 형제였다. 그러면
**화면을 하나 붙일 때마다 캔버스가 늘고, 각각 `Canvas`·`GraphicRaycaster`·`LayoutElement` 높이를
따로 맞춰야 한다.** 하나만 어긋나면 크기가 틀어진다 — 실제로 `#Setting Canvas`가 그랬다.

→ `#Main Canvas (MAIN VIEW)` 하나에 화면들을 담는다.
덤으로 컬럼의 자식이 **전부 캔버스**가 되어 계층이 균일해졌다.

### 전환 층을 하나로 눕혔다 (마지막 라운드)

통합 직후의 모양은 이랬다:

```
#Main Canvas
└─ WorkStation Panel (PRESENTER ↓ SUB PRESENTER)   ← WorkStationPresenter
   ├─ WorkStation List Panel (PRESENTER ↓ SUB VIEW)
   └─ WorkStation Select Panel (PRESENTER ↓ SUB VIEW)
└─ Setting Panel (PRESENTER ↓ SUB VIEW)
```

**같은 일(같은 자리를 나눠 쓰는 화면 중 하나만 켠다)을 두 층이 따로 하고 있었다** —
바깥은 `UIManager`, 안은 `WorkStationPresenter`. 폴더와 표기도 그 중첩을 그대로 베꼈고,
`WorkStation Panel`은 **자기 위젯을 하나도 안 가진 채** 껍데기로만 존재했다.

→ 세 화면을 `#Main Canvas`의 형제로 눕히고 `WorkStationPresenter`를 지웠다.
`MainScreen`이 `WorkStationList`/`WorkStationSelect`/`Setting` 3값이 되고, 켜고 끄는 곳은
`UIManager.ShowMainScreen` **한 곳뿐**이다. 폴더도 두 단(`Main/XxxPresenter/`)으로 평탄해졌다.

> **`MainCanvasView`에 합병하지 않았다.** 캔버스에 Presenter 로직을 얹는 건 이번 작업에서
> `#State`·`#Setting`·`#Widget` 세 곳을 고친 바로 그 위반이고, 무엇보다
> **`#Main Canvas`는 `CloseAllExceptWidget`에서 꺼진다** — 꺼진 오브젝트에 전환을 맡길 수 없다.

### 참조 방향은 "살아 있는 쪽이 넘긴다"

전환 층이 없어지자 슬롯 번호를 누가 옮기느냐가 남았다. 답은 **목록 → 선택 한 방향.**
반대로 선택 화면이 목록의 이벤트를 구독하면 **평소 꺼져 있어서 신호를 못 받는다**
(`OnDisable`에서 구독을 끊는 규약과 정면으로 부딪힌다).

```csharp
selectPresenter.Open(slotIndex);                  // 번호를 먼저 (Start 전일 수 있다)
ui.ShowMainScreen(MainScreen.WorkStationSelect);  // 이 호출이 목록을 끈다
```

화면이 **스스로를 끄지 않는 것**도 규칙으로 삼았다. 스스로 끄면 다음 화면이 켜지기 전 빈 칸이 남는다.

### `MainScreen` enum을 seam으로 뒀다

여는 쪽(`StatePresenter`의 버튼 · `WorkStationListPresenter`의 칸 클릭)과 패널을 쥔 `UIManager`가
서로의 오브젝트를 모르게 하려고 enum을 사이에 뒀다.
화면을 하나 더 붙일 때 **코드는 enum 한 줄, 나머지는 인스펙터 한두 행**이다.
enum을 별도 파일로 빼지 않고 `Managers/UIManager.cs` 안에 둔 것은 프로젝트 관례다
(`WidgetPosition`은 `WidgetPositionLayout.cs`, `ScreenAnchor`는 `WindowManager.cs`).

### 제목은 `MainCanvasView`가 쥔다 — 캔버스 View의 유일한 위젯

화면이 바뀌면 `Title` 문구도 바뀐다. `Title`은 **세 화면이 함께 쓰는 머리**라 어느 화면의
Presenter에 맡겨도 **그 화면이 꺼질 때 함께 죽는다** — 정작 다른 화면으로 넘어간 순간 제목을 못 바꾼다.

→ `MainCanvasView.SetTitle(string)`. "캔버스 View엔 위젯 참조를 두지 않는다"의 예외를 하나 뚫되,
**가르는 기준을 "위젯을 쥐느냐"가 아니라 "Model을 구독하느냐"로** 다시 적었다.
문구 자체는 코드가 아니라 `UI Manager > Main Screens`의 각 줄에 있다 — 표시용 문자열이라
바뀌어도 로직이 안 바뀌는데 코드에 박으면 문구 하나 고치는 데 컴파일이 필요하다.

### 표기를 세 번 갈아엎었다

| 라운드 | 표기 | 왜 버렸나 |
|---|---|---|
| 1 | `(VIEW)` / `(PRESENTER)` | 캔버스 껍데기와 Presenter 아래 위젯이 같은 `(VIEW)`라 **화면 경계가 안 보였다** |
| 2 | 위젯까지 전부 `(SUB VIEW)` (370개) | 하이어라키가 표기로 뒤덮여 **읽을 수 없었다** |
| 3 | `(MAIN VIEW)` / `(↓ SUB VIEW)` | 스크립트가 붙은 것에만. `↓` 뒤로 **접힌 채로도 다음 층을 안다** |

교훈: **표기는 정보를 더하는 곳에만 붙인다.** Presenter 아래는 어차피 전부 그 Presenter가
그리는 것이라 표기가 아무것도 말해 주지 않는다.

### `Presenter`와 `Panel`을 이름으로 갈랐다

`Xxx Panel (PRESENTER ↓ SUB VIEW)`라는 이름은 **화면 경계와 그 안의 정렬 상자가 같은 단어**를
쓰게 만들었다. `WorkStation Select Panel` 안에 `Header Panel`·`Industry Panel`이 있는 식이다.

| 이름 | 무엇 | 스크립트 |
|---|---|---|
| `Xxx Presenter (↓ SUB VIEW)` | 화면 — 켜고 끄는 단위 | `XxxPresenter` |
| `Xxx Panel` | 그 안에서 줄 세우는 상자 | 없다 |

**오브젝트 이름엔 캔버스 이름을 되풀이하지 않는다.** 클래스는 어셈블리 전체에서 유일해야 해
`StorageTabPresenter`지만, 오브젝트는 이미 `#Storage Canvas` 안이라 `Tab Presenter`면 충분하다.

### 폴더 = Presenter 클래스 이름, 깊이는 두 단에서 멈춘다

`UI/<캔버스>/<Presenter>/` 안에 Presenter와 **그에 종속된 View**를 함께 둔다.
폴더를 열면 "이 화면은 무엇으로 이뤄졌나"가 바로 보인다.
**Presenter 폴더를 중첩하지 않는다** — 하이어라키에서도 Presenter 안에 Presenter를 두지 않기 때문이다.

### 껍데기 Presenter를 미리 만들었다

`WidgetPresenter`·`MenuPresenter`·`StorageTabPresenter` 셋은 **이번에 새로 만든 것**이다.
전에는 그 위젯들이 캔버스에 직접 붙어 있거나(위젯 버튼) 상위 Presenter가 건너뛰어 잡고 있었다
(메뉴 버튼) 아예 아무도 안 잡고 있었다(창고 탭 4개).

`StorageTabPresenter`는 탭 화면이 아직 없지만 버튼을 잡아 두고 "아직 없다"를 로그로 남긴다 —
**안 잡아 두면 눌러도 아무 일이 없어 버튼이 고장 난 것과 구분되지 않는다.**

### `UIManager`·`WindowManager`는 개명하지 않았다

`UIManager`는 화면을 여닫을 뿐 데이터를 그리지 않아 MVP 3역할 밖이다.
`WindowManager`는 `SettingPresenter` 입장에선 Model이 맞지만 **Win32 창을 조작하는 부수효과가 본체**라,
그걸 이름에서 지우면 곤란하다.

## 지뢰 — 다시 밟기 쉬운 것

### 1. 컬럼은 자식 높이를 정해 주지 않는다

작업 초반 `@Main Column`의 `VerticalLayoutGroup`은 **`Child Control Height = off`**였다. 그래서
`LayoutElement.preferredHeight`는 컬럼이 자기 총높이를 셀 때만 읽히고, 실제 높이는
`RectTransform`의 `Height`가 그대로 쓰였다 — `LayoutElement`만 보고 "900 맞네" 하면 못 찾는다.
(`#Setting Canvas`가 옛 높이 415를 들고 온 사고가 이것이었다.)

**작업 막바지에 여백을 정리하면서 `Child Control Height`가 켜졌고, 그 순간 다른 사고가 터졌다.**
`#State Canvas`·`#Widget Canvas`가 `flexibleHeight = 1`이었던 것 —
꺼져 있던 `LayoutElement`가 진짜로 높이를 몰기 시작하자 **형제가 꺼질 때 위젯이 화면 전체로 늘어났다.**

| | 증상 | 원인 | 처방 |
|---|---|---|---|
| ctrlH **off** | `LayoutElement`를 고쳐도 높이가 안 변한다 | `sizeDelta.y`가 실제 높이 | **켠다** |
| ctrlH **on** | 형제를 껐더니 남은 칸이 화면을 다 채운다 | `flexibleHeight = 1` = "남는 높이를 가져간다" | 고정 칸은 **`flexH = 0`** |

지금은 **ctrlH = on + 세 칸 모두 고정**이다 — 90 + 900 + 90 = 1080 = 컬럼 높이.
**남는 높이 자체가 없으니 나눌 것도 없다.**

> 교훈: `Child Control Height`를 켜고 끄는 건 **한 오브젝트의 설정이 아니라 그 컬럼 전체의 높이 규약**이다.
> 토글하면 자식들의 `LayoutElement`가 전부 의미가 바뀐다 — 켤 때 자식 값을 같이 훑어야 한다.

### 2. 전부 닫을 때 캔버스까지 꺼야 한다

패널만 끄고 `#Main Canvas`를 켜 두면 `LayoutElement`가 컬럼 안에서 900px를 계속 차지해
**위젯이 창 가장자리에서 밀린다.** `CloseAllExceptWidget`이 캔버스까지 끈다.

### 3. `OnEnable`에서 일하는 컴포넌트를 여닫히는 캔버스에 두면 안 된다

`WidgetPositionLayout`이 `#Setting Canvas`에 붙어 있었다. 그 캔버스가 토글 대상이 되는 순간
**꺼져 있는 동안 3열 순서와 위젯 배치가 죽고, 기본 상태(꺼짐)에서는 한 번도 안 돈다.**
`!Horizental Columns`로 옮겼다.

### 4. 캔버스에 Presenter를 얹으면 여닫기와 표시가 한 몸이 된다

`#State`·`#Setting`·`#Widget` 셋이 이걸 어기고 있었다. 캔버스를 끄면 Presenter도 같이 죽어서
"닫혀 있는 동안 갱신"이나 "패널만 교체"가 불가능해진다.

### 5. 한글이 붙은 식별자는 `sed \b`로 안 잡힌다

UTF-8 로케일에서 한글은 **단어 문자**라 `PlayerDataManager보다`·`InventoryPanelUI가` 같은
주석에서 `\b`가 경계로 인식되지 않는다. 개명 후 경계 없는 `grep`으로 전수 확인해야 한다.

### 6. enum 값을 중간에 끼워 넣으면 씬 배선이 조용히 어긋난다

`MainScreen`이 2값(`WorkStation`·`Setting`)에서 3값(`WorkStationList`·`WorkStationSelect`·`Setting`)이
되면서 **`Setting`이 1에서 2로 밀렸다.** enum은 씬에 **int로** 저장되므로
`StatePresenter.screenButtons[0].screen`이 그대로 1(= 새 `WorkStationSelect`)을 가리키게 된다.
**컴파일도 통과하고 경고도 없다.** `SerializedProperty.enumValueIndex`로 직접 고쳤다.

### 7. 시작 화면은 씬 저장 상태를 믿으면 안 된다

처음엔 `EnforceSingleMainScreen`(켜진 것 중 첫 번째만 남긴다)이었다. 그러면 **작업하다 설정을
켜 둔 채 저장한 씬이 설정 화면으로 게임을 시작한다.** `ResetMainScreen`(언제나 기본값 하나만 켠다)으로 바꿨다.

> 단, **캔버스는 켜지 않는다.** 여기서 `mainCanvas.Show(true)`를 하면 로그인 전에 게임 화면이 비친다.
> 안쪽만 정리해 두면 나중에 캔버스가 켜지는 순간 이미 올바른 화면이 떠 있다.

### 8. 컴포넌트 순서는 `MoveComponentUp`으로만 바꿀 수 있다

- `SerializedObject`로 `m_Component`를 옮기면 Unity가 거부한다 —
  `It is not allowed to modify the m_Component property`
- `ComponentUtility.MoveComponentRelativeToComponent`는 **대화상자를 띄워 MCP 실행이 통째로 끊긴다**
  (`AssetDatabase.DeleteAsset`과 같은 부류. 로그도 안 남아 원인 파악이 어렵다)
- **`ComponentUtility.MoveComponentUp`만 조용히 동작한다.** 목표 순서를 정렬로 구한 뒤
  각 컴포넌트를 제자리까지 위로 끌어올리는 식으로 돌렸다.

### 9. `[CenterHeader]` 문구에 `< >`를 넣으면 두 겹이 된다

`CenterHeaderDrawer`가 그릴 때 `$"< {text} >"`로 감싼다. `[CenterHeader("< 참조 >")]`로 쓰면
인스펙터에 **`< < 참조 > >`** 가 나온다. 30곳이 이 상태였다 — 어트리뷰트 파일에 경고 주석을 박아 뒀다.

## 작업 방식 — 다음에도 쓸 것

- **Unity가 켜져 있으면 파일 이동은 `AssetDatabase.MoveAsset`으로.** meta를 Unity가 알아서
  따라가게 해서 GUID 경합을 피한다. Unity를 닫을 수 있으면 `.cs`+`.cs.meta`를 짝지어 `git mv` 해도 된다.
  ⚠️ **`AssetDatabase.DeleteAsset`은 MCP에서 실패한다**(대화상자). 삭제는 파일 시스템으로 하고 새로고침한다.
- **컴파일 검증은 Unity 없이도 된다.** `Assembly-CSharp.csproj`의 `HintPath`·`DefineConstants`를
  추려 응답 파일을 만들고 dotnet SDK의 Roslyn `csc.dll`로 돌린다. 분석기
  (`Assets/Plugins/Analyzers/MikaSourceGen.dll`)를 `/analyzer:`로 붙여야 `MikaGenerated`가 해결된다.
- **씬 수술은 Unity MCP(`Unity_RunCommand`)로.** 컴포넌트는
  `ComponentUtility.CopyComponent` → `PasteComponentAsNew`로 옮기면 **직렬화 참조가 보존된다**
  (`SettingPresenter`의 위젯 8개, `WidgetPositionLayout`의 열 6개를 손으로 다시 안 걸었다).
  ⚠️ MCP 스크립트에서 `Image`는 네임스페이스와 충돌하니 `using UIImage = UnityEngine.UI.Image;`,
  `HashSet<>`은 참조 어셈블리가 없으니 배열 + LINQ `Contains`를 쓴다.
- 역할 표기는 이름이 아니라 **붙어 있는 컴포넌트 타입**으로 판정해 일괄 처리했다.
- 씬 작업 전에 **현재 씬 상태를 커밋해 복구 지점**을 만들어 두면 마음이 편하다.

## 후속 작업 / 주의사항

- **재생 검증이 아직 안 끝났다.** 배선 누락 0·컴파일 에러 0까지 확인했고, 런타임 시나리오는 대기.
- **커밋하지 않았다** (사용자 요청). 작업 트리에 그대로 있다.
- **MVVM 전환 검토 문서는 저장소에 없다** — 사용자가 바탕화면으로 옮겼다.
  실질 관문은 `WindowManager.IsPointerOverContent()`가 `EventSystem.RaycastAll`에 묶여 있는 것이다
  (UI Toolkit은 `EventSystem`을 안 써서 클릭스루 판정을 다시 짜야 한다).
- `xxx Button (1)`~`(4)`(상태 패널)는 열 화면이 없어 `Screen Buttons` 배열에 넣지 않았다.
- **`WorkStation Select`는 `Screen Buttons`에 넣을 수 없다** — 슬롯 번호가 있어야 열리는 화면이라
  목록의 칸 클릭만이 입구다. `MainScreen` 값 중 유일하게 버튼으로 못 여는 것.
- `Title`은 Presenter 없이 캔버스 직속이다. `#Main Canvas`의 것만 문구가 바뀐다.
- `Information Presenter`는 껍데기다 — `InventorySlotView`에 `Clicked`가 없어 고를 수단이 아직 없다.
- **T-015**(인벤토리 156종)가 `InventoryPresenter`·`InventorySlotView`를 건드린다. 이 재편이 먼저다.
- `WorkStationListPresenter`의 `#region A-2 진단 (임시)`는 이번에 건드리지 않았다.
- 오타 `Ttitle Text`·`!Horizental Columns`와 `@Market Column(마켓 기능들이…` 이름 속 한글 메모는
  MVP와 무관해 그대로 뒀다.
