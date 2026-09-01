# Login 폴더 규칙

> 최종 업데이트: 2026-08-30 (종료 버튼) · 대상: `Assets/Scripts_Client/UI/Login/`

**`!Login Canvas` — 게임에 들어오면 가장 먼저, 그리고 이것만 보이는 화면.**

| 폴더 | 무엇 |
|------|------|
| `LoginCanvasView.cs` | 캔버스 껍데기 — `Show(bool)`만 |
| `LoginPresenter/` | 아이디 입력과 로그인 요청 |

이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>)에 있다.

---

## 지금 있는 규칙

- **Sorting Order는 `100`.** 다른 UI보다 앞에 나와야 해서 `Override Sorting`과
  자기 `GraphicRaycaster`가 필요하다 (→ [`Layout 규칙.md`](<../Layout/Layout 규칙.md>)의
  "Canvas를 다룰 때의 함정"). 값을 띄엄띄엄 주는 이유도 같은 절에 있다.
- **`UIManager`의 인스펙터에서 로그인 캔버스가 맨 위에 온다.** 게임이 거기서 시작하기
  때문이다 — 인스펙터 필드는 화면에 나오는 순서로 둔다.
- `LoginCanvasView`는 `Show(bool)` 하나뿐이다. **여기에 위젯 참조가 하나라도 생기면
  `LoginPresenter`로 옮긴다.**
- **종료 버튼이 여기에도 있다.** 로그인 화면이 떠 있는 동안은 상태 패널의 종료 버튼이
  가려져 손이 닿지 않고, 타이틀바를 끈 보더리스라 창 `X`도 없다. 나가는 길이 ESC 하나뿐이면
  모르는 사용자는 갇힌다. 연결은 `WindowManager.QuitApplication` 단일 경로다
  (→ [`Managers 규칙.md`](<../../Managers/Managers 규칙.md>)).

> 이 문서가 짧은 건 규칙이 적기 때문이다. 로그인 화면에 규칙이 생기면 여기 쌓는다.
