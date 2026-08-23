# Layout 폴더 규칙

> 최종 업데이트: 2026-08-23 (`UI 규칙.md`에서 분리) · 대상: `Assets/Scripts_Client/UI/Layout/`

**화면이 아니라 배치를 계산하는 컴포넌트를 두는 곳.** 어느 캔버스에도 속하지 않아
`UI/` 아래에서 유일하게 캔버스 폴더가 아니다.

| 파일 | 하는 일 |
|------|---------|
| `WidgetPositionLayout.cs` | 세 열의 위·아래 칸 높이를 계산해 `preferredHeight`에 써 넣는다 + 위젯 자리 배치 · 넘침 감시 |
| `FlexibleGridLayoutGroup.cs` | 폭에 맞춰 셀 크기를 역산하는 그리드 |
| `SquareLayoutElement.cs` | 부모 높이를 보고 가로를 주장한다("높이만큼 정사각형") |
| `Editor/FlexibleGridLayoutGroupEditor.cs` | 위 그리드의 인스펙터 |

**이 문서는 캔버스·레이아웃 함정도 함께 담는다** — `UI/` 어느 폴더에서 일하든 배치가
어긋나면 여기를 본다. 이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>)에 있다.

---

## Canvas를 다룰 때의 함정

| 증상 | 원인 | 해결 |
|---|---|---|
| **Sorting Order를 올려도 계속 뒤에 그려진다** | 중첩 Canvas는 `Override Sorting`을 켜야 `Sorting Order`가 먹는다. 끄면 숫자가 **통째로 무시**되고 계층 순서로만 그려진다 | `Override Sorting` 체크 |
| **앞에는 나오는데 버튼이 안 눌린다** | `Override Sorting`을 켠 Canvas는 **자기 `GraphicRaycaster`** 가 필요하다 | `GraphicRaycaster` 추가 |
| **자식으로 옮겼더니 화면에서 사라졌다** | 루트 Canvas일 땐 Unity가 `localScale`을 관리해 줬다. 자식이 되면 저장된 값이 그대로 적용된다 | `localScale`을 `1,1,1`로 |
| **창 배율을 바꾸면 그 Canvas만 안 따라간다** | `CanvasScaler`가 `Constant Pixel Size`(기본값) | `Scale With Screen Size` / `1920×1080` / `Match = 1(Height)` — **Root Canvas와 동일하게** |
| 열을 껐더니 다른 열들이 가운데로 몰린다 | Column을 껐다 | Column이 아니라 **그 안의 Canvas만** 끈다 (`UIManager` 주석) |
| **화면을 다 껐는데 빈 판이 남는다** | 배경 `Image`가 캔버스에 있는데 자식이 전부 꺼질 수 있다 | 배경을 자식으로 내린다. **늘 켜진 자식이 있는 캔버스면 그냥 둬도 된다** ([`Main 규칙.md`](<../Main/Main 규칙.md>)의 "한 캔버스 안에서 화면을 갈아 끼운다") |

**Sorting Order는 띄엄띄엄 준다** — `Login = 100`, `Log = 200`, `!System = 300`. 사이에 끼워 넣을 일이 반드시 생긴다.

---

## 레이아웃 그룹의 함정

| 증상 | 원인 | 해결 |
|---|---|---|
| **형제를 껐더니 남은 칸이 화면 전체로 늘어난다** | 그 칸의 `LayoutElement.flexibleHeight = 1`. flexible은 **"남는 높이를 가져간다"** 라서, 형제가 꺼져 자리가 통째로 비면 **혼자 다 빨아들인다** | 크기가 고정이어야 하는 칸은 **`preferredHeight = 실제 높이` · `flexibleHeight = 0`**. `@Main Column`은 60 + 900 + 120 = 1080 = 컬럼 높이라 **남는 높이 자체가 없다** — 나눌 것이 없으면 사고도 없다. 비율이 필요하면 flexible이 아니라 **숫자를 계산해 써 넣는다**(아래 "비율은 flexible이 아니라 숫자로") |
| **`preferredHeight`를 줬는데 더 큰 값으로 계산된다** | UGUI가 쓰는 값은 preferred가 아니라 **`max(minHeight, preferredHeight)`** 다. `minHeight`가 남아 있으면 그게 이긴다 | 높이를 못박을 때는 **`minHeight`도 함께 0으로** 내린다. `WidgetPositionLayout.SetFixedHeight`가 그렇게 한다 |
| **전부 닫았는데 위젯이 화면 가장자리에서 밀린다** | 내용이 꺼진 캔버스의 `LayoutElement`가 컬럼 안에서 높이를 계속 차지한다 | 화면만 끄지 말고 **캔버스까지 끈다** (`UIManager.CloseAllExceptWidget`) |
| **`LayoutElement`를 고쳐도 높이가 안 변한다** | 부모가 **`Child Control Height = off`**. 그러면 `LayoutElement.preferredHeight`는 **부모가 자기 총높이를 셀 때만** 읽히고, 실제 높이는 `RectTransform`의 `Height`가 그대로 쓰인다 | 부모의 `Child Control Height`를 **켠다**(지금 컬럼들은 켜져 있다). 끈 채로 두려면 `RectTransform.Height`와 `preferredHeight`를 **둘 다** 맞춰야 한다 — 두 곳을 손으로 동기화하는 셈이라 반드시 어긋난다 |
| **화면을 갈아 끼웠더니 크기·위치가 달라진다** | 같은 자리를 나눠 쓰는 화면들의 `LayoutElement` 값이 서로 다르다 | 셋 다 `preferredHeight 0` · `flexibleHeight 1`로 **똑같이** 준다. 겹쳐 놓을 필요는 없다 — 꺼진 오브젝트는 레이아웃에서 빠진다 |
| **자식들이 폭을 똑같이 나눠 갖는다** | `Child Force Expand Width`가 켜져 있다. 이건 "남는 폭을 **모두에게 균등 분배**"라서, 한 자식만 늘리고 싶을 때는 정반대로 동작한다 | **끄고**, 늘릴 자식에만 `LayoutElement.flexibleWidth = 1` |
| **`preferredHeight 50`을 줬는데 머리 칸이 220으로 부푼다** | 부모의 **`Child Force Expand Height`가 켜져 있다.** 남는 높이를 `flexibleHeight`와 무관하게 **모든 자식에게 균등 분배**해서, 고정하려던 칸까지 함께 부푼다 | **끈다.** 높이를 나누는 건 `flexibleHeight`지 `expand`가 아니다 (아래 "세로 3단 배치의 정석") |
| **둘이 "나머지를 반씩"인데 크기가 다르다** | `preferredHeight = -1`은 "**내 내용물 높이를 먼저 챙기고** 남은 것만 flexible로 나눈다"는 뜻이다. 내용물이 다르면(토글 4개 vs 드롭다운 3개) 결과가 달라진다 | 둘 다 **`preferredHeight = 0` · `flexibleHeight = 1`**. 그래야 전체를 1:1로만 나눈다 |
| **`Preferred Height`를 줬는데 안 먹는다** | 같은 오브젝트의 `ScrollRect` 등이 자식 RectTransform을 따로 건드린다 | 안 쓰는 `ScrollRect`를 뗀다 |
| 높이 합이 부모를 넘친다 | 레이아웃에 빠진 자식이 있다 (`LayoutElement` 없이 큰 preferred를 가진 것) | 모든 자식에 높이 정책을 준다 — 고정은 `preferredHeight` + `flexibleHeight = 0`, 나머지를 채울 하나만 `flexibleHeight = 1` |
| **"높이만큼 정사각형"이 안 된다** | UGUI는 **가로를 먼저 다 정하고 세로를 정한다.** 가로를 정할 때 자기 높이가 아직 없다 | `SquareLayoutElement` (`UI/Layout/`) — 부모 높이를 보고 가로를 주장한다 |
| **내용이 늘어도 스크롤이 안 늘어난다** | `Viewport`에 직접 자식을 넣었다. Viewport는 **크기가 고정**이라 내용이 늘어도 커지지 않는다. `ScrollRect.content`도 비어 있으면 스크롤은 아예 동작하지 않는다 | 아래 "스크롤 뷰의 정석" |
| **내용이 스크롤바 밑으로 깔린다** | 자식이 자기 폭(패널 전체폭)을 주장한다. Viewport는 스크롤바만큼 좁다 | `Content`의 레이아웃 그룹에서 `Child Control Width`를 켜 폭을 넘겨받게 한다 |
| **한 번 넓어진 패널이 다시 안 줄어든다 (에디터는 멀쩡, 빌드만 깨진다)** | 그 노드의 **`min`이 자기 `rect.width`에서 파생**된다 → 아래 함정 참고 | 그 컴포넌트가 부모에게 폭을 **요구하지 않게** 한다 |

### ⚠️ `min`을 자기 폭에서 파생시키면 그 폭이 하한으로 굳는다

UGUI가 자식에게 주는 폭은 `Clamp(부모 폭, min, flexible > 0 ? 부모 폭 : preferred)`다.
**`flexible > 0`일 때 부모 폭보다 넓어질 수 있는 통로는 `min` 하나뿐**이라는 뜻이다.

그래서 **자기 현재 폭을 보고 `min`을 계산하는 컴포넌트는 순환에 빠진다.**

```
현재 폭 → min 계산 → min이 곧 하한 → 폭이 그대로 유지 → 다시 min …
```

창이 **한 프레임이라도** 넓었으면 그 폭이 최소 폭으로 남아 **다시는 줄어들지 않는다**.
에디터 Game 뷰는 계속 다시 그려 수렴하므로 티가 안 나고, **빌드는 굳은 값 그대로 간다.**

실제 사고(A-1) — `FlexibleGridLayoutGroup`이 `cellSize.x`를 자기 폭에서 역산하는데,
`GridLayoutGroup`이 그 셀 크기로 `min = padding + (cellSize.x + spacing) × 열수 − spacing`을
발표해 **`min == 현재 폭`**이 됐다. 거래 열 패널이 열보다 넓어져 옆 열을 침범했다.

**같은 계열의 함정** — `Child Control Width`를 끄면 UGUI가 그 자식의 min·preferred를
**자식의 현재 `sizeDelta`**로 잡는다. 역시 "현재 크기 = 하한"이다.

> 배치 컴포넌트를 새로 만들 때는 **"부모에게 무엇을 요구하는가"를 자기 크기와 무관하게** 정한다.
> 주어진 폭에 맞추는 것이 목적인 컴포넌트라면 가로로 요구할 것은 **패딩뿐**이다.

`WidgetPositionLayout.VerifyNoOverflow`가 이 부류를 상시 감시한다 —
자식이 부모보다 넓으면 어느 노드가 무슨 `min`을 요구했는지까지 경고로 남긴다.
단 **부모에 레이아웃 그룹이 있는 곳만** 본다. 앵커·`sizeDelta`로 직접 배치한 부모는
자식이 자기보다 넓은 게 정상일 수 있다 — 유니티 기본 `Scrollbar`의 `Sliding Area`(폭 0)가 그 예다.

### 스크롤 뷰의 정석

```
Scroll View Panel   ScrollRect   content = Content · viewport = Viewport   ← 둘 다 반드시 채운다
├─ Viewport         Image + Mask   sizeDelta (-17, 0)      ← 스크롤바 폭만큼 좁다. 레이아웃 그룹을 두지 않는다
│   └─ Content      LayoutGroup + ContentSizeFitter(v = PreferredSize)
│                   anchor (0,1)~(1,1)  pivot (0,1)        ← 위에 붙어서 아래로 자란다
│                   ChildControlWidth = on                 ← 줄 폭을 여기서 정해 스크롤바 침범을 막는다
│       └─ 줄 / 칸  LayoutElement preferredHeight 고정
└─ Scrollbar Vertical
```

**`Viewport`는 창이고 `Content`가 두루마리다.** 창에 직접 붙이면 두루마리가 없어 감을 수 없다.

### 세로 3단 배치의 정석

```
부모      VerticalLayoutGroup   ctrl(W,H)=on  expand(W)=on  expand(H)=off
├─ 머리   LayoutElement  preferredHeight 50   flexibleHeight 0    ← 고정
├─ 탭     LayoutElement  preferredHeight 50   flexibleHeight 0    ← 고정
└─ 본문   LayoutElement  preferredHeight -1   flexibleHeight 1    ← 나머지를 전부
```

`expand(H)`를 켜면 고정하려던 칸까지 늘어난다. **높이를 나누는 건 `flexibleHeight`지 `expand`가 아니다.**

**나머지를 채울 칸이 둘 이상이고 "똑같이" 나눠야 하면 `preferredHeight`를 `-1`이 아니라 `0`으로 준다.**
`-1`은 "내 내용물 높이를 먼저 챙긴다"라서, 내용이 다르면 결과가 갈린다
(`Setting Presenter`의 토글 4개 · 드롭다운 3개가 실제로 355/305로 갈렸다).

**칸 전체가 고정이어야 하는 열이면 `flexibleHeight`를 전부 0으로 둔다.**
`@Main Column`이 그렇다 — 60 + 900 + 120 = 1080 = 컬럼 높이라 남는 높이 자체가 없다.
하나라도 `flexibleHeight = 1`이면 **형제가 꺼질 때 그 자리를 혼자 빨아들인다.**

### 비율은 flexible이 아니라 숫자로

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

---

## 배치 컴포넌트를 새로 만들 때

### 화면 크기 변화에 스스로 반응한다

`WidgetPositionLayout`은 `OnRectTransformDimensionsChange()`로 **캔버스(=창의 렌더 영역) 크기가
바뀔 때마다 배치를 다시 태운다.** 창 크기 프리셋 변경 · 타이틀바 토글 · 배율이 다른 모니터로
드래그가 전부 이 콜백으로 모인다.

- **`WindowManager`를 참조하지 않는다.** Unity 콜백만으로 자립하므로 Managers → UI 역방향
  의존이 생기지 않는다.
- **콜백은 플래그만 세우고, 실제 배치는 `LateUpdate`에서 한다.** 이유는 바로 아래 함정 참고.

### ⚠️ 레이아웃 콜백 안에서 즉시 리빌드하지 않는다

`OnRectTransformDimensionsChange`는 UGUI가 **레이아웃 패스를 도는 도중에도** 날아온다
(`CanvasUpdateRegistry.PerformUpdate` → `HorizontalLayoutGroup.SetLayoutHorizontal` → 자식 크기 변경).
그 안에서 `LayoutRebuilder.ForceRebuildLayoutImmediate`를 부르면 **같은 서브트리를 재진입 재빌드**하게
되고, 바깥 패스가 자식을 순회하던 중에 폭이 갈아엎어져 **일부만 새 값, 일부는 옛 값**으로 남는다.

```csharp
// ✗ 열끼리 침범한다 — 패스 도중 재진입
private void OnRectTransformDimensionsChange() => Apply();   // Apply 안에서 ForceRebuild...

// ✓ 패스 밖으로 미룬다
private void OnRectTransformDimensionsChange() => _pendingApply = true;
private void LateUpdate() { if (_pendingApply) { _pendingApply = false; Apply(); } }
// Apply 안에서는 LayoutRebuilder.MarkLayoutForRebuild
```

- 자기 클래스에 재진입 빗장(`_applying`)을 둬도 **소용없다.** 막아야 할 바깥 패스가 UGUI 것이다.
- `LateUpdate`는 `Canvas.willRenderCanvases`보다 먼저 돌아, 예약해도 **같은 프레임에** 반영된다.
- 형제 순서 변경(`SetSiblingIndex`)은 UGUI가 알아서 dirty 처리하므로 예약으로 충분하다.

> 2026-08-15. 실제로 `ForceRebuild`로 바꿨다가 열 침범 회귀를 만들었다
> (`.claude/Agent/2026-08-15-build-ui-layout-mismatch.md`).

