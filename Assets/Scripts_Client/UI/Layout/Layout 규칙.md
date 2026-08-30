# Layout 폴더 규칙

> 최종 업데이트: 2026-08-30 (위젯 칸 높이가 창 배율 역산의 근거가 됐다) · 대상: `Assets/Scripts_Client/UI/Layout/`

**화면이 아니라 배치를 계산하는 컴포넌트를 두는 곳.** 어느 캔버스에도 속하지 않아
`UI/` 아래에서 유일하게 캔버스 폴더가 아니다.

| 파일 | 하는 일 |
|------|---------|
| `WidgetPositionLayout.cs` | 세 열의 위·아래 칸 높이를 계산해 `preferredHeight`에 써 넣는다 + 위젯 자리 배치 · 넘침 감시 |
| `WindowDragArea.cs` | 이 오브젝트를 타이틀바처럼 잡아 끌면 `WindowManager`에게 창 이동을 맡긴다 |

## ⚠️ 먼저 — Canvas·LayoutGroup 함정은 여기 없다

**게임을 모르는 uGUI 지식은 전부 [`ugui-layout` 스킬](<../../../../.claude/skills/client/ugui-layout/SKILL.md>)로 옮겼다**(2026-08-26).
배치가 의도대로 안 나오면 거기부터 본다 — `Override Sorting` · `flexible`이 형제 자리를
빨아들이는 문제 · `max(min, preferred)` · `Child Force Expand` · 스크롤 뷰의 정석 ·
`min`을 자기 폭에서 파생시키면 굳는 문제 · 레이아웃 콜백 안에서 즉시 리빌드 금지가 거기 있다.

범용 컴포넌트 `FlexibleGridLayoutGroup`·`SquareLayoutElement`도 함께
[`Common/ugui-layout/`](<../../Common/ugui-layout/ugui-layout 규칙.md>)으로 내려갔다.

**이 문서에는 이 프로젝트에만 해당하는 것만 남는다.**

---

## 세 열의 높이는 flexible이 아니라 숫자로 나눈다

세 열의 위·아래 칸은 **가운데를 뺀 나머지를 위젯 쪽 2 : 상태 쪽 1**로 나눈다.
비율의 자연스러운 도구는 `flexibleHeight`지만 **여기서는 쓸 수 없다** — flexible은
"남는 높이를 가져간다"라서 `CloseAllExceptWidget`으로 가운데를 끄면 위젯이 열 전체를 빨아들인다.

→ **`WidgetPositionLayout`이 `column.rect.height - 가운데 preferredHeight`를 비율로 나눠
`preferredHeight`에 숫자로 써 넣는다.** `flexibleHeight`는 계속 0이다.

| 지키는 것 | 왜 |
|---|---|
| 사이드 칸의 `minHeight`도 0으로 내린다 | UGUI가 쓰는 값은 `max(min, preferred)`다 |
| 한쪽만 반올림하고 나머지는 빼서 채운다 | 둘 다 반올림하면 합이 1px 어긋나 가운데가 밀린다 |
| 가운데 높이는 `LayoutUtility`가 아니라 `GetComponent<LayoutElement>()`로 읽는다 | `LayoutUtility`는 **꺼진 오브젝트를 건너뛰어 0**을 돌려준다. 닫힌 상태에서 읽으면 위 사고를 flexible 없이 재현한다 |
| **사람이 정하는 건 가운데 900 하나뿐** | 사이드를 인스펙터에서 고쳐도 다음 배치에서 덮어써진다 |

**`@Main Column`은 `flexibleHeight`를 전부 0으로 둔다** — 60 + 900 + 120 = 1080 = 컬럼 높이라
**남는 높이 자체가 없다.** 나눌 것이 없으면 사고도 없다.

## 이 프로젝트의 Canvas 값

- **`Sorting Order`** — `Login = 100`, `Log = 200`, `!System = 300`. 띄엄띄엄 준다.
- **`CanvasScaler`** — `Scale With Screen Size` / `1920×1080` / `Match = 1(Height)`.
  **Root Canvas와 동일하게** 맞춘다.
- **열을 껐더니 다른 열들이 가운데로 몰린다** → Column을 껐다. Column이 아니라
  **그 안의 Canvas만** 끈다 (`UIManager` 주석).

## `WidgetPositionLayout`의 넘침 감시

`VerifyNoOverflow`가 "자식이 부모보다 넓은" 부류를 상시 감시한다 —
어느 노드가 무슨 `min`을 요구했는지까지 경고로 남긴다.
단 **부모에 레이아웃 그룹이 있는 곳만** 본다. 앵커·`sizeDelta`로 직접 배치한 부모는
자식이 자기보다 넓은 게 정상일 수 있다 — 유니티 기본 `Scrollbar`의 `Sliding Area`(폭 0)가 그 예다.

## 화면 크기 변화에 스스로 반응한다

`WidgetPositionLayout`은 `OnRectTransformDimensionsChange()`로 **캔버스(=창의 렌더 영역) 크기가
바뀔 때마다 배치를 다시 태운다.** 창 크기 프리셋 변경 · 타이틀바 토글 · 배율이 다른 모니터로
드래그가 전부 이 콜백으로 모인다.

- **`WindowManager`를 참조하지 않는다.** Unity 콜백만으로 자립하므로 Managers → UI 역방향
  의존이 생기지 않는다.
- **콜백은 플래그만 세우고, 실제 배치는 `LateUpdate`에서 한다.**
  이유(패스 도중 재진입)는 [`ugui-layout` 스킬](<../../../../.claude/skills/client/ugui-layout/SKILL.md>) §6.

> 2026-08-15. 실제로 `ForceRebuild`로 바꿨다가 열 침범 회귀를 만들었다
> (`.claude/Agent/2026-08-15-build-ui-layout-mismatch.md`).

## 위젯 칸 높이는 창 배율을 역산하는 근거다

`WidgetPositionLayout.WidgetSlotHeight`(= 위젯 칸의 `LayoutElement.preferredHeight`)를
`SettingPresenter`가 읽어 `WindowManager.SetWidgetSlotHeight`로 넘긴다. 창 크기 드롭다운의
**'작업표시줄 맞춤'** 항목이 이 값으로 배율을 역산한다 — 캔버스가 기준 해상도(1080) 좌표라
이 숫자는 창 배율과 무관하게 일정하고, 화면 픽셀 높이만 캔버스 스케일만큼 커지기 때문이다.

⚠️ **이 값을 상수로 베껴 두지 않는다.** `widgetWeight`/`stateWeight`를 조정하거나 가운데 칸
높이를 바꾸면 여기가 따라 움직이는데, 베껴 둔 쪽은 안 움직여 **맞춤이 조용히 어긋난다.**

⚠️ **위젯 비중을 줄이면 맞춤이 성립하지 않을 수 있다.** 위젯이 창에서 차지하는 비율이 작아질수록
같은 작업표시줄 높이를 얻는 데 필요한 창이 커진다(비율이 절반이면 창이 두 배). 화면을 넘으면
`WindowManager.RecalculateFitScale`이 계산 실패로 두고 경고를 남긴 뒤 프리셋으로 떨어뜨린다.
자세한 근거는 [`Managers 규칙.md`](<../../Managers/Managers 규칙.md>) 5장.
