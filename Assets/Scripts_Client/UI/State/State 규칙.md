# State 폴더 규칙

> 최종 업데이트: 2026-09-02 (재화 표시가 아이콘 + 텍스트 짝으로 · 다이아 추가) · 대상: `Assets/Scripts_Client/UI/State/`

**`#State Canvas` — 닉네임 · 재화(골드 · 다이아)를 늘 보여 주고, 메인 화면을 여는 버튼을 쥔다.**

| 폴더 | 무엇 |
|------|------|
| `StateCanvasView.cs` | 캔버스 껍데기 — `Show(bool)`만 |
| `StatePresenter/` | 상태 표시 + `Screen Buttons` 배선 |

이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>)에 있다.

---

## 계층은 2단이다 — `Panel`은 층이 아니다

```
#State Canvas (MAIN VIEW)                       ← 캔버스: 켜고 끈다
└─ State Presenter (↓ SUB VIEW)                 ← 표시·배선은 전부 여기
    ├─ Nick Panel   ─ Nick Image · Nick Text    ← 정렬 상자. 스크립트 없음
    ├─ Gold Panel   ─ Gold Image · Gold Text
    ├─ Dia Panel    ─ Dia Image  · Dia Text
    ├─ Setting Button
    ├─ xxx Button (1..3)
    └─ Exit Button
```

`Xxx Panel`은 **아이콘과 글자를 붙여 두는 정렬 상자**라 MVP 층이 아니다 —
스크립트를 붙이지 않고 레이아웃 그룹만 둔다([`UI 규칙.md`](<../UI 규칙.md>)의
"`Presenter`와 `Panel`을 이름으로 가른다"). Presenter는 여전히 `Xxx Text`를 직접 쥔다.

> ⚠️ **이 캔버스가 규칙을 어기고 있었다** (2026-08-10 교정). `#State Canvas`가 닉네임·골드를
> **캔버스에서 직접** 그렸다. Presenter를 한 겹 넣어 갈랐다.
> **어겼을 때 실제로 생기는 문제 — 캔버스를 끄면 Presenter도 같이 죽는다.**
> 여닫기와 표시가 한 오브젝트에 묶여 "닫혀 있는 동안 갱신"이 불가능해진다.

## 가로 폭은 비율로 나뉜다

`State Presenter`의 `HorizontalLayoutGroup`(spacing 5 · padding 5)이 폭을 정하고,
각 칸의 몫은 `LayoutElement.FlexibleWidth` **비율**이다 — 재화 패널 셋이 같은 몫,
버튼 다섯이 그 1/4씩(현재 `4 : 4 : 4 : 1×5`).

**이 숫자는 인스펙터에서 눈으로 맞추는 값이다.** 양수면 무엇이든 성립해서 틀린 선택지가
없으므로, 여기 적힌 것은 지금의 스냅샷일 뿐이고 **고칠 때 이 문서를 볼 필요는 없다.**
지킬 것은 픽셀을 직접 적지 않는 것뿐이다 — 글자는 전부 Auto Size라 좁아지면 잘리는 대신 작아진다.

> ⚠️ **패널 안쪽 그룹은 `ChildForceExpandWidth`를 끈다.** 켜 두면 아이콘까지 늘어나
> 정사각형이 깨진다. 아이콘은 폭을 스스로 정하고(아래 "정사각형은 숫자가 아니라 파생이다"),
> 남는 폭은 `flexible 1`인 텍스트만 가져간다.

## 아이콘은 색만 있는 자리표시자다

**프로젝트에 아트 에셋이 없다.** 그래서 `Xxx Image`는 유니티 내장 `UISprite`에
**짝이 되는 글자와 같은 색**을 입혀 자리만 잡아 두었다 — `RarityPalette`가 등급을
색으로만 표시하는 것과 같은 방식이다.

**아트가 들어오면 `Sprite`만 갈아 끼우면 된다.** 크기·정렬은 이미 정해져 있다.

### 정사각형은 숫자가 아니라 파생이다

`Xxx Image`에는 `LayoutElement` 대신
**[`SquareLayoutElement`](<../../Common/ugui-layout/ugui-layout 규칙.md>)** 를 붙인다.
"내 폭은 부모 높이만큼"만 주장하므로 **위에서 무엇이 바뀌든 정사각형이 유지된다.**

> ⚠️ **폭에 숫자를 적지 않는다.** 이 자리의 33은 파생값이다 —
> `열 1080 − 가운데 950 = 130` → `위젯 2 : 상태 1`로 나눠 상태 칸 43 → padding 5+5를 빼 33.
> **가운데 높이나 비율을 바꾸면 따라 움직여야 하는데, 베껴 둔 숫자는 안 움직인다**
> (`WidgetPositionLayout`의 "이 값을 상수로 베껴 두지 말 것"과 같은 이유).
> 실측: 가운데를 950 → 900 → 800으로 바꿔도 아이콘이 43→60→93을 따라가며 정사각형을 지켰다.

> ⚠️ **같은 오브젝트에 `LayoutElement`를 함께 두지 않는다.** 둘 다 가로를 주장해
> 어느 쪽이 이기는지가 `layoutPriority`에 좌우된다.

## ⏸ 다이아는 늘 0이다 — 버그가 아니다

`Dia`는 **그릇만 있고 지급·차감 경로가 없다**
([`trade/README.md`](../../../../GameDesign/design/trade/README.md) 1장 — "상점은 골드 상점,
현금 상품을 두지 않는다"). 서버가 잔액을 실어 보내고 클라도 받고 있지만,
**그 값이 움직일 길이 아직 없다.** 결제·상점이 생기면 저절로 움직인다.

## 메인 화면 여는 버튼은 여기서 배선한다

`State Presenter > Screen Buttons`에 (버튼 · `MainScreen` 값) 한 줄을 넣으면 끝이다.
전환 자체는 `UIManager.ShowMainScreen` 한 곳이 한다 —
[`Main 규칙.md`](<../Main/Main 규칙.md>)의 "전환 층은 하나다".

> ⚠️ `screenButtons` 배열 위의 `[CenterHeader]`에는 `[NonReorderable]`을 같이 단다.
> 이 배열은 순서에 의미가 없지만 **헤더는 보여야 한다**
> ([`UI 규칙.md`](<../UI 규칙.md>)의 "공통 작성 규약").
