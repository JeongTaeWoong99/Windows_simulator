# State 폴더 규칙

> 최종 업데이트: 2026-08-23 (`UI 규칙.md`에서 분리) · 대상: `Assets/Scripts_Client/UI/State/`

**`#State Canvas` — 닉네임 · 골드 등 내 상태를 늘 보여 주고, 메인 화면을 여는 버튼을 쥔다.**

| 폴더 | 무엇 |
|------|------|
| `StateCanvasView.cs` | 캔버스 껍데기 — `Show(bool)`만 |
| `StatePresenter/` | 상태 표시 + `Screen Buttons` 배선 |

이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>)에 있다.

---

## 계층은 2단이다

```
#State Canvas (MAIN VIEW)                       ← 캔버스: 켜고 끈다
└─ State Presenter (↓ SUB VIEW)                 ← 펼치면 위젯뿐
    ├─ Nick Text
    ├─ Gold Text
    └─ Setting Button
        └─ Text (TMP)
```

> ⚠️ **이 캔버스가 규칙을 어기고 있었다** (2026-08-10 교정). `#State Canvas`가 닉네임·골드를
> **캔버스에서 직접** 그렸다. Presenter를 한 겹 넣어 갈랐다.
> **어겼을 때 실제로 생기는 문제 — 캔버스를 끄면 Presenter도 같이 죽는다.**
> 여닫기와 표시가 한 오브젝트에 묶여 "닫혀 있는 동안 갱신"이 불가능해진다.

## 메인 화면 여는 버튼은 여기서 배선한다

`State Presenter > Screen Buttons`에 (버튼 · `MainScreen` 값) 한 줄을 넣으면 끝이다.
전환 자체는 `UIManager.ShowMainScreen` 한 곳이 한다 —
[`Main 규칙.md`](<../Main/Main 규칙.md>)의 "전환 층은 하나다".

> ⚠️ `screenButtons` 배열 위의 `[CenterHeader]`에는 `[NonReorderable]`을 같이 단다.
> 이 배열은 순서에 의미가 없지만 **헤더는 보여야 한다**
> ([`UI 규칙.md`](<../UI 규칙.md>)의 "공통 작성 규약").
