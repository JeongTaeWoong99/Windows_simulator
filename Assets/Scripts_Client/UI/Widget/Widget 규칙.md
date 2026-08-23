# Widget 폴더 규칙

> 최종 업데이트: 2026-08-23 (`UI 규칙.md`에서 분리) · 대상: `Assets/Scripts_Client/UI/Widget/`

**`#Widget Canvas` — 다른 화면을 전부 닫아도 남는, 데스크톱 위젯 본체.**

| 폴더 | 무엇 |
|------|------|
| `WidgetCanvasView.cs` | 캔버스 껍데기 |
| `WidgetPresenter/` | 위젯 내용과 열기 버튼 |

이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>)에 있다.

---

## ⚠️ 항상 켜져 있어야 하는 컴포넌트를 여기 두지 않는다

`OnEnable`에서 일하는 컴포넌트(`WidgetPositionLayout`)를 **토글 대상 캔버스에 붙이면
그게 꺼져 있는 동안 아무 일도 하지 않는다.** 기본 상태로 꺼져 있으면 한 번도 안 돈다.
그래서 `WidgetPositionLayout`은 이 캔버스가 아니라 **상주하는 `!Horizental Columns`**에 있다
(→ [`Layout 규칙.md`](<../Layout/Layout 규칙.md>)).

## 위젯 자리는 다른 캔버스가 닫혀야 제대로 나온다

`UIManager.CloseAllExceptWidget`이 **화면만 끄지 않고 캔버스까지 끄는** 이유가 여기다.
내용이 꺼진 캔버스라도 `LayoutElement`가 컬럼 안에서 높이를 계속 차지해서, 캔버스를
남겨 두면 그 900px가 안 사라져 **위젯이 창 가장자리에서 밀린다.**

> ⚠️ 이것이 오버레이를 뺀 나머지를 `CanvasGroup`이 아니라 `SetActive`로 여닫는 근거 중
> 하나다 — [`System 규칙.md`](<../System/System 규칙.md>)의 "무엇으로 여닫는가".
