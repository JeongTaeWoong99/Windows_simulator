# ugui-layout 규칙

> 최종 업데이트: 2026-08-26 (`UI/Layout/` → `Common/ugui-layout/` 이관) · 대상: `Common/ugui-layout/`

**기본 UGUI로는 표현할 수 없는 배치를 채워 주는 컴포넌트.** 게임을 전혀 모르므로
[Arca Unity Toolkit](https://github.com/JeongTaeWoong99/Arca_Unity_Toolkit) 사본인 `Common/` 아래에 있다.
Common 전체에 걸리는 규칙은 상위 폴더의 `Common 규칙.md`에 있다(프로젝트에만 있는 문서다).

| 파일 | 하는 일 |
|------|---------|
| `FlexibleGridLayoutGroup.cs` | 열 개수를 고정한 채, 자기 폭에 맞춰 셀 크기를 역산하는 그리드 |
| `SquareLayoutElement.cs` | 부모 높이를 보고 가로를 주장한다 — "높이만큼 정사각형" |
| `Editor/FlexibleGridLayoutGroupEditor.cs` | 위 그리드의 인스펙터. 기본 에디터를 그대로 두면 추가 필드가 아예 안 나온다 |

---

## ⚠️ 배치 컴포넌트를 새로 만들 때 — `min`을 자기 크기에서 파생시키지 않는다

UGUI가 자식에게 주는 폭은 `Clamp(부모 폭, min, flexible > 0 ? 부모 폭 : preferred)`다.
**`flexible > 0`일 때 부모 폭보다 넓어질 수 있는 통로는 `min` 하나뿐**이라는 뜻이다.
그래서 **자기 현재 폭을 보고 `min`을 계산하는 컴포넌트는 순환에 빠진다.**

```
현재 폭 → min 계산 → min이 곧 하한 → 폭이 그대로 유지 → 다시 min …
```

창이 **한 프레임이라도** 넓었으면 그 폭이 최소 폭으로 남아 **다시는 줄어들지 않는다.**
에디터 Game 뷰는 계속 다시 그려 수렴하므로 티가 안 나고, **빌드는 굳은 값 그대로 간다.**

`FlexibleGridLayoutGroup`이 실제로 이 사고를 냈다(A-1). 그래서 지금은
`CalculateLayoutInputHorizontal`에서 **가로로 패딩만 요구한다** — 주어진 폭에 셀을 맞추는 것이
이 컴포넌트의 존재 이유이므로, 그 이상을 요구할 이유가 없다.

> 배치 컴포넌트를 만들 때는 **"부모에게 무엇을 요구하는가"를 자기 크기와 무관하게** 정한다.

## Canvas·LayoutGroup 함정 전반

이 폴더 밖의 이야기 — `Override Sorting` · `flexible`이 형제 자리를 빨아들이는 문제 ·
`max(min, preferred)` · `Child Force Expand` · 스크롤 뷰의 정석 · 레이아웃 콜백 안에서
즉시 리빌드 금지 등은 **`ugui-layout` 스킬**(저장소 루트의 `.claude/skills/client/ugui-layout/SKILL.md`)에 모여 있다.
배치가 의도대로 안 나오면 거기부터 본다.
