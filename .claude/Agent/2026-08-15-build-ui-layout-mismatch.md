---
date: 2026-08-15
title: 빌드에서 3열 폭이 어긋나는 문제 — 해결 (FlexibleGridLayoutGroup의 min 순환)
tags: [client, ui, window, layout]
---

# 빌드에서 3열 폭이 어긋나던 문제 (A-1)

## 목적 / 배경

- `먼저 할일.MD` A-1. 에디터에선 3열이 정확한데 빌드하면 거래 열이 창 밖으로 밀리고
  `!Horizental Columns`의 Spacing 10이 무너져 보였다.
- 사용자 요구: **모니터 개수·DPI 배율·빌드를 어디로 옮기든 깨지지 않아야 한다.**

## 진단 — 문서에 적혀 있던 원인은 틀렸다

`먼저 할일.MD`는 원인을 "창 크기 자체"로 적어 뒀지만, 두 스크린샷을 PNG 디코딩해
픽셀 단위로 재 보니 **빌드 시점 클라이언트 영역은 정확히 1920×1080이었다.**

- 창고↔작업 열 사이 바탕화면 색이 **정확히 10px**, 가운데 열 폭 **633px**
  → 캔버스 스케일 = 1.0. **캔버스 스케일러·창 크기·DPI는 범인이 아니다.**
- **거래 열 하나만 폭이 ~22px 넓었다.** 그 초과분이 첫 간격과 창고 열 테두리를 덮은 것 —
  "잘림"과 "간격 무너짐"은 같은 하나의 현상이다.
- 세 열의 `LayoutElement`가 씬에서 완전히 동일(min 0 / preferred 0 / flexible 1)하고 부모가
  `childControlWidth` + `childForceExpandWidth`이므로, **정상 동작했다면 폭이 다를 수 없다.**
  → 그 값은 **이전 프레임에 굳은 값**이다.

**에디터에서 한 번도 재현되지 않은 이유** — `LoadSavedPosition()`이 `Application.isPlaying`일 때만
PlayerPrefs를 읽는다. 레지스트리 확인 결과 빌드 저장값은 `Widget.Position = 5`(LowerRight,
거래·창고·작업)인데 씬 저장값은 4(LowerCenter)다. **에디터는 빌드가 쓰는 열 순서를 한 번도
렌더한 적이 없었다.** 스크린샷의 열 순서 차이가 그 증거다.

## 변경 내용

- `DesktopWindow/Win32Native.cs` — `GetClientRect` · `GetDpiForWindow` ·
  `AdjustWindowRectExForDpi`(+`AdjustWindowRectEx` 폴백) · `MonitorFromWindow` ·
  `GetMonitorInfo` · `MONITORINFO` · `MONITOR_DEFAULTTONEAREST` 선언 추가
- `Managers/WindowManager.cs`
  - `ResizeWindow` — 클라이언트 영역 기준으로 전환(`ClientSizeToOuterSize`).
    적용 후 `GetClientRect`로 실측해 어긋나면 그 차이만큼 **한 번** 보정
  - `SetTitleBar` — 스타일 변경 직후 `ResizeWindow` + `ApplyPosition` 재적용
  - `TryGetWorkArea` 신설 — `MonitorFromWindow`+`GetMonitorInfo().rcWork`,
    실패 시에만 `SPI_GETWORKAREA` 폴백. `ApplyPosition`·`GetWorkAreaSize`가 이걸 쓴다
  - `ApplyPosition` — 앵커 좌표를 **외곽 크기** 기준으로 계산
- `UI/Layout/WidgetPositionLayout.cs` — `OnRectTransformDimensionsChange()` 추가,
  `Apply()`를 `ForceRebuildLayoutImmediate`로 전환(+재진입 빗장 `_applying`)
- 문서 — `먼저 할일.MD` A-1 정정·종료, `Managers 규칙.md` 5장 규칙 2개 추가,
  `UI 규칙.md`에 배치 컴포넌트의 리사이즈 대응 명시

## 주요 결정 / 근거

- **`SetWindowPos`는 외곽 크기를 받는다.** 원하는 렌더 해상도를 그대로 넘기면 프레임 두께만큼
  렌더 영역이 줄어 16:9가 깨진다. `SetTitleBar`의 `SWP_NOSIZE`도 같은 문제의 반대 방향 —
  외곽을 유지한 채 프레임만 벗기므로 **클라이언트 영역이 커지는 과도 프레임**이 생긴다.
  캡션 높이를 상수로 박지 않고 `AdjustWindowRectExForDpi`로 OS에 물은 이유가 이것 —
  배율마다 값이 다르다.
- **`GetClientRect` 실측 보정을 굳이 넣은 이유** — DPI 가상화 등에서 프레임 계산이 빗나갈 수 있다.
  한 번만 돌므로 진동하지 않는다. "어디로 옮기든 안 깨진다"는 요구의 마지막 방어선이다.
- **`ForceRebuildLayoutImmediate` 선택** — 예약(`MarkLayoutForRebuild`)은 다음 캔버스 갱신까지
  미뤄지는데, 형제 순서 변경과 화면 크기 변경이 같은 프레임에 겹치면 중첩 Canvas
  (`#Market Canvas`, `overrideSorting`) 서브트리를 건너뛴다. 열 3개짜리 트리라 비용은 무시 가능.
- **`WidgetPositionLayout`이 `WindowManager`를 참조하지 않는다.** `WindowManager`에
  `ScreenSizeChanged` 이벤트를 두는 안도 있었으나, 구독자가 하나뿐이면 죽은 배선이 되고
  Managers → UI 역방향 의존이 생긴다. Unity 콜백으로 자립시켰다.
  (계획에 있던 이벤트 추가는 **의도적으로 넣지 않았다** — 구독자가 없다.)

## 업데이트 (2026-08-15) — ⚠️ 위 진단은 틀렸고, 1차 수정은 실패했다

빌드 후에도 증상이 그대로였고 **열끼리 침범하는 새 증상이 추가**됐다.
빌드 산출물 타임스탬프(스크립트 19:28~19:34 → `Assembly-CSharp.dll` 19:40:53 →
스크린샷 19:45~19:46)로 확인했으니 **구 빌드를 본 게 아니다.**

**"클라이언트가 정확히 1920×1080이었다"는 위 §진단은 오독이다.**
레지스트리 `HKCU\Software\DefaultCompany\DesktopWindow_Control`:

| 키 | 값 |
|---|---|
| `Screenmanager Resolution Window Width` / `Height` | **1902 / 1033** |
| `Window.Scale` | `3` = X2 = **1920×1080을 요청한 상태** |

차이 가로 18(테두리 9×2) · 세로 47(캡션 37 + 아래 10) = **125% 배율 프레임 두께와 일치**.
즉 `SetWindowPos`가 받은 외곽이 그냥 1920×1080이었다 —
`ClientSizeToOuterSize`도 `GetClientRect` 실측 보정도 **결과에 아무 영향을 못 줬다.**

`Match = Height` 캔버스라 `scaleFactor = 1033/1080 = 0.9565` → 캔버스 폭 1988.5 →
균등 열 폭 656.2 캔버스px = **627.6 화면px**. 스크린샷에서 잰 작업 열이 627px이다.
→ **캔버스 스케일러는 정상. 잘못된 건 창 크기다.**

### 회귀: `OnRectTransformDimensionsChange` 안의 즉시 리빌드

```csharp
private void OnRectTransformDimensionsChange() { Apply(); }  // → ForceRebuildLayoutImmediate
```

이 콜백은 UGUI가 **레이아웃 패스를 도는 도중**(`CanvasUpdateRegistry.PerformUpdate` →
HLG `SetLayoutHorizontal` → 자식 크기 변경)에도 날아온다. 그 안에서 같은 서브트리를
즉시 재빌드하면 바깥 패스가 순회 중에 폭이 갈아엎어져 **일부 열만 새 값**으로 남는다.
`_applying` 빗장은 내 `Apply()`의 재귀만 막지 UGUI의 바깥 패스는 못 막는다.
1차 수정 전 `MarkLayoutForRebuild`(예약)에는 없던 사고 — **내가 넣은 회귀다.**

원 로그가 "중첩 Canvas 서브트리를 건너뛴다"를 `ForceRebuild` 전환의 근거로 적었는데,
그건 **검증된 적 없는 추측**이었다.

### 이번에 한 것

- `WidgetPositionLayout` — `ForceRebuildLayoutImmediate` → `MarkLayoutForRebuild` 환원.
  콜백은 `_pendingApply` 플래그만 세우고 **`LateUpdate`에서 실행**한다(레이아웃 패스 밖).
- `WidgetPositionLayout` — **자가 복구 가드**: 리빌드 다음 프레임에 세 열 `rect.width`의
  최대−최소가 1px을 넘으면 경고 + **1회만** 재배치. 폭·순서를 하드코딩하지 않고
  "셋이 서로 같은가"만 보므로 런타임 순서 변경에도 성립한다.
- `WindowManager` · `WidgetPositionLayout` — `#region A-1 진단 (임시)` 계측 로그.
  **원인 확정 후 통째로 지운다.** `Player.log`에 창·레이아웃 로그가 한 줄도 없어
  결함 ①을 가릴 근거가 아예 없었기 때문이다.

계측 결과에 따라 Step 2-A(`Screen.SetResolution` 선행 후 Win32 상태 재적용) 또는
Step 2-B(`ResizeWindow` 실측 보정 3회 루프) 중 하나로 간다.
→ **둘 다 안 갔다.** 계측이 창을 무죄로 처리해 버렸다 (아래 업데이트 참조).

## 업데이트 (2026-08-16) — ✅ 원인 확정. 창도 열도 무죄였다

계측 빌드의 `[A-1]` 로그가 **위 두 진단을 모두 반증**했다.

| 항목 | 값 (전 구간 불변) | 뜻 |
|---|---|---|
| `client` | **1920x1080** | 창 크기 정상 — 2차 진단(1902×1033) 폐기 |
| `canvas` / `scale` | 1920x1080 / **1.0000** | 캔버스 스케일러 정상 |
| 창고·작업·거래 `w` | **633.3 · 633.3 · 633.3** | 열 균등 — 1차 진단("거래 열만 22px 넓다") 폐기 |
| 열 폭 가드 경고 | 한 줄도 없음 | 세션 내내 균등했다 |

**즉 결함 ①·②는 이미 고쳐져 있었다.** 남은 어긋남은 **열보다 한 층 아래**였다.

### 범인 — `FlexibleGridLayoutGroup`의 "내 최소 폭 = 내 현재 폭" 순환

빌드를 또 돌리는 대신 **에디터에서 `LayoutUtility` 값을 서브트리 전체에 대해 덤프**했다
(Unity MCP `Unity_RunCommand`). 다른 노드는 전부 작은 `min`인데 한 갈래만 달랐다.

```
#Market Canvas (MAIN VIEW)     w=633.3  min=633.3  pref=633.3  flex=1.0
  Gacha Presenter (↓ SUB VIEW) w=623.3  min=623.3  pref=623.3  flex=0.0   FlexibleGridLayoutGroup
```

`ResizeCellToFitWidth()`가 `m_CellSize.x`를 **자기 `rect.width`에서 역산**하는데,
`GridLayoutGroup.CalculateLayoutInputHorizontal()`이 그 셀 크기로
`minWidth = padding + (cellSize.x + spacing) × 열수 − spacing`을 발표한다 → **`min == 현재 폭`.**

부모 VLG는 `Clamp(열폭, min, 열폭)`으로 자식 폭을 정하므로
**`flexible > 0`에서 부모보다 넓어질 통로는 `min` 하나뿐**이다.
창이 한 프레임이라도 넓었으면 그 폭이 하한으로 굳어 **다시는 줄지 않는다**.
에디터 Game 뷰는 계속 다시 그려 수렴하고, 빌드는 굳은 값 그대로 간다 —
**"에디터는 멀쩡한데 빌드만 깨진다"의 정확한 기전이다.**

### 이번에 한 것

- `UI/Layout/FlexibleGridLayoutGroup.cs` — `CalculateLayoutInputHorizontal` 재정의.
  `FixedColumnCount` 모드에서 가로 min/pref를 **좌우 패딩만**으로 발표해 순환을 끊는다.
  다른 Constraint 모드는 셀 크기가 배치의 *입력*이라 순환이 없으므로 `base` 값을 그대로 둔다.
- `UI/Layout/WidgetPositionLayout.cs` — 임시 덤프를 **상시 가드 `VerifyNoOverflow`로 승격**.
  `LateUpdate`에서 10프레임마다 세 열 서브트리를 훑어 **자식이 부모보다 넓으면**
  계층 경로·min/pref/flex·폭을 주장하는 컴포넌트까지 남긴다. 같은 내용은 반복하지 않는다.
- `WindowManager` · `WidgetPositionLayout` — `#region A-1 진단 (임시)` **전부 제거**.
- 문서 — `먼저 할일.MD` A-1 교체, `UI 규칙.md` 7-2에 "min을 자기 폭에서 파생시키지 않는다" 절.

### 주요 결정 / 근거

- **`VerifyColumnWidths`로는 이번 사고를 못 잡는다.** 그 가드는 `Apply()`가 예약할 때만 돌아
  초기화·설정 변경 시점밖에 검사하지 않는다. 실제로 스크린샷 시점엔 한 번도 돌지 않았고,
  로그의 `633.3 × 3`은 **깨진 순간의 값이 아니었다.** 그래서 새 가드는 **주기 실행**이다.
- **가드는 고치지 않고 알리기만 한다.** 넘침의 원인은 대개 그 노드가 부모보다 큰 min을
  요구하는 것이라 다시 태워도 같은 값이 나온다. 진단의 입구 역할이 더 값지다.
- **에디터 검증으로 갈음했다.** `min`은 `#if !UNITY_EDITOR` 밖의 순수 UGUI 로직이라
  에디터에서 그대로 재현된다. `① 시작 min=10.0 → ② 강제로 넓힘 min=10.0 → ③ 복귀 min=10.0`
  (수정 전이라면 ②에서 813.3으로 굳었을 값), 세 열은 633.3 유지.
- 다른 `FlexibleGridLayoutGroup` 2개(창고·메인 `Content`)도 `min=606.3 → 10.0`으로 떨어졌으나
  부모가 레이아웃 그룹이 아닌 **Viewport**이고 `ContentSizeFitter(h=Unconstrained)`라 무해하다.

### ✅ 빌드 검증 통과 (2026-08-16) — 이 건은 닫혔다

사용자가 빌드해 확인했고 증상이 사라졌다. **A-1은 `먼저 할일.MD`에서 삭제했다**(A절 번호도 당겨졌다).

정리하며 함께 한 것 — **두 가드를 하나의 주기 검사로 합쳤다.**
`VerifyColumnWidths`는 `Apply()` 다음 프레임에만 돌아 **나중에 나는 사고를 통째로 놓쳤다**
(이번 사고가 정확히 그렇게 새어 나갔다). 이제 둘 다 `LateUpdate`에서 10프레임마다 돈다.
`_verifyFrame` 예약 필드는 지웠다. 두 가드는 **보는 것이 다르므로 둘 다 남긴다** —
열 셋이 서로 달라도 합이 부모에 들어가면 넘침으로 안 잡히고, 넘침은 열이 균등해도 그 아래서 난다.

### 세 번 헛짚은 이유

1·2차는 **화면 전체를 역산해 원인을 추정**하고 고쳤다. 이번에 달랐던 것:
로그가 창·캔버스·열을 **숫자로 무죄 처리**해 범위가 한 노드로 좁혀졌고,
남은 가설(`childControlWidth=0`)조차 **씬 grep으로 반증**한 뒤
**실제 런타임 값을 덤프해** 확정했다. 추정으로 고치지 않았다.

> 폐기된 3차 가설도 기록해 둔다 — `childControlWidth = 0`인 그룹 5개는 전부 Setting Canvas
> 소속이라 Market Column과 무관했다. **같은 계열의 함정이긴 하다**(현재 크기 = 하한).

## 후속 작업 / 주의사항

- ⚠️ **레이아웃 컴포넌트를 새로 만들 때** — "부모에게 무엇을 요구하는가"(`min`/`preferred`)를
  **자기 현재 크기와 무관하게** 정한다. 자기 폭에서 파생시키면 그 폭이 하한으로 굳는다.
  같은 계열로 `Child Control Width = off`도 자식의 현재 `sizeDelta`를 min으로 삼는다.
  → `UI 규칙.md` 7-2 "min을 자기 폭에서 파생시키면 그 폭이 하한으로 굳는다"
- ⚠️ `WindowManager.GetCursorScreenPosition`은 여전히 `GetWindowRect`(외곽)로 클라이언트를
  근사한다. 보더리스에서는 맞지만 **타이틀바를 켜면 캡션 높이만큼 어긋난다** — 이번 범위 밖이라
  건드리지 않았다. 클릭 스루 판정이 이상하면 여기를 본다.
- ⚠️ 에디터와 빌드가 갈리는 근본 구조(`LoadSavedPosition`이 재생 중에만 PlayerPrefs를 읽음)는
  그대로다. 의도된 설계지만, **에디터에서 안 보이는 버그가 또 나올 수 있는 지점**이다.
  빌드 재현이 안 될 때는 씬의 `position` 값을 레지스트리 저장값과 맞춰 놓고 본다.
