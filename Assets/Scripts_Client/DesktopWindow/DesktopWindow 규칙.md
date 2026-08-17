# DesktopWindow 규칙

> 최종 업데이트: 2026-08-17 (창 이동을 캔버스 드래그 + SC_MOVE 위임으로 개정 — §4·§5-7) · 대상: `Assets/Scripts_Client/DesktopWindow/`

**Win32 / DWM 네이티브 API의 P/Invoke 선언만** 두는 곳. 이 게임이 데스크톱 위의 투명 창으로
동작하기 위해 필요한 OS 함수들이다.

---

## 1. P/Invoke 란

C#(관리 코드)에서 운영체제의 C 기반 DLL 함수(비관리 코드)를 **직접 호출**하는 기능이다.

```csharp
[DllImport("user32.dll")]
private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, ...);
```

`[DllImport("...")]`로 **어느 DLL의 어떤 함수인지** 표시하고, C 함수 시그니처를 C#으로 그대로
옮겨 적으면 런타임이 호출을 연결해 준다. (`using System.Runtime.InteropServices`)

### 사용 DLL

| DLL | 담당 |
|-----|------|
| `user32.dll` | 창 스타일·위치·메시지·커서 등 일반 윈도우 관리 |
| `dwmapi.dll` | DWM(Desktop Window Manager, 데스크톱 합성기) — 투명·유리 효과 |
| `kernel32` (`System.Diagnostics.Process`) | 프로세스의 메인 창 핸들 확보 (`Process.MainWindowHandle`) |

---

## 2. 선언과 호출은 갈라져 있다

| | `DesktopWindow/Win32Native` | `Managers/WindowManager` |
|---|---|---|
| 담당 | **선언만** — 함수·상수·구조체 | **판단** — 언제 · 어떤 순서로 부를지 |
| 성격 | OS API를 C#으로 옮겨 적은 것 | 이 게임의 창 정책 |

한쪽은 바뀔 일이 거의 없고(OS API), 다른 쪽은 기획에 따라 자주 바뀐다.
섞어 두면 "이게 OS가 정한 값인가 우리가 정한 값인가"가 흐려진다.

> 창 제어 기능(투명·항상 위·클릭 스루·크기·위치)의 실제 동작은
> [`Managers 규칙.md`](<../Managers/Managers 규칙.md>)를 본다.

---

## 3. 🔴 이 게임은 Built-in 렌더 파이프라인에 묶여 있다

**URP로 바꾸면 투명 배경이 죽는다.** 이건 설정으로 우회할 수 있는 문제가 아니다.

DWM의 투명은 **창 백버퍼(최종 출력)의 알파 채널을 읽어** 판정한다.
`DwmExtendFrameIntoClientArea(MARGINS = -1)`로 프레임을 창 전체로 확장하면
백버퍼에서 알파 0인 픽셀이 그대로 투명해진다. 관건은
**"카메라가 그린 알파 0이 백버퍼까지 살아남는가"** 이고, 여기서 두 파이프라인이 갈린다.

| | Built-in (된다) | URP (안 된다) |
|---|---|---|
| 카메라 출력 경로 | **백버퍼로 직행** | 여러 중간 렌더 타겟 → FinalBlit |
| 백버퍼 알파 | 알파 0이 **그대로 기록됨** | FinalBlit에서 **알파를 1(불투명)로 덮어씀** |
| 결과 | 투명 (바탕화면 비침) | **배경 검정** |

- URP는 "화면은 불투명하다"는 전제로 설계돼 최종 출력에서 알파를 덮어쓴다
- URP 17의 **Alpha Processing도 답이 아니다** — 후처리·RenderTexture 알파만 보존하고
  최종 스왑체인은 보장하지 않는다
- 증상이 헷갈린다: **"UI는 보이는데 배경만 검정"** 이 나온다.
  Canvas Overlay는 알파가 보존되고 **카메라 출력 경로만** 덮어쓰이기 때문이다

> 2026-06-11~12 검증에서 URP를 두 번 시도해 모두 실패하고 Built-in으로 이전했다.
> 과정은 [`2026-06-12 로그`](<../../../.claude/Agent/2026-06-12-desktop-window-urp-to-builtin.md>) 참조.

---

## 4. 기능 ↔ 호출 매핑

기능을 고칠 때 어느 API를 봐야 하는지의 대응표다.

| 기능 | 핵심 호출 | DLL |
|------|-----------|-----|
| 창 핸들 확보 | `Process.MainWindowHandle` (폴백 `GetActiveWindow`) | kernel32 / user32 |
| 타이틀바 On | `GetWindowLong` → `WS_CAPTION\|WS_SYSMENU` → `SetWindowLong` + `SetWindowPos(SWP_FRAMECHANGED)` | user32 |
| 타이틀바 Off (보더리스) | 테두리 비트 제거 + `WS_POPUP` → 같은 방식으로 프레임 갱신 | user32 |
| 투명 배경 | `DwmExtendFrameIntoClientArea(MARGINS = -1)` + 카메라 알파 0 | dwmapi |
| 항상 위 | `SetWindowPos(HWND_TOPMOST, …)` | user32 |
| 클릭 스루 | `SetWindowLong(GWL_EXSTYLE, WS_EX_LAYERED \| WS_EX_TRANSPARENT)` | user32 |
| 창 이동 | 캔버스를 잡아 끌면 `ReleaseCapture` + `SendMessage(WM_SYSCOMMAND, SC_MOVE_HTCAPTION)` 로 OS 이동 루프에 위임 | user32 |
| 위치 / 크기 | `SystemParametersInfo(SPI_GETWORKAREA)` + `SetWindowPos(x, y, w, h)` | user32 |
| 커서 위치 (클릭스루 중 판정) | `GetCursorPos` + `GetWindowRect` | user32 |

> ⚠️ **타이틀바를 켤 때 `WS_THICKFRAME`은 넣지 않는다.** 창 이동은 되게 하되
> 가장자리를 당겨 리사이즈하는 것은 막기 위해서다 — 크기는 프리셋으로만 바꾼다.

---

## 5. ⚠️ 창 제어를 다룰 때의 함정

### 5-1. `#if !UNITY_EDITOR` 가드

실제 Win32 호출은 **모두 빌드(`.exe`)에서만** 실행한다. 에디터에서 메인 윈도우 핸들을 건드리면
**Unity 에디터 창 자체가 영향을 받아** 불안정해지기 때문이다.

> 그래서 **에디터에서는 창 관련 변화가 일어나지 않는다.** 토글을 눌러도 값만 바뀌고 창은 그대로다.
> 설정 화면을 테스트하려면 빌드해야 한다. (`UNITY_EDITOR`는 Unity가 정의하는 플랫폼 심볼이다)

### 5-2. 클릭 스루가 켜지면 Unity 입력이 죽는다

`WS_EX_TRANSPARENT`가 걸리면 **OS가 마우스 메시지를 창에 보내지 않는다.**
`Input.mousePosition`·`EventSystem.IsPointerOverGameObject()`가 동작하지 않아
**클릭 스루가 영영 안 풀린다.**

→ `GetCursorPos`로 전역 커서를 폴링하고, 그 좌표를 `EventSystem.RaycastAll`에 직접 넣어
UI 위인지 판정한다. (`WindowManager.GetCursorScreenPosition` / `IsPointerOverContent`)

### 5-3. `WS_EX_LAYERED`는 단독으로 쓰지 않는다

원래 `SetLayeredWindowAttributes`(균일 알파/색상키) 또는 `UpdateLayeredWindow`(per-pixel)와
**짝으로** 쓰는 스타일이다.

**다만 이 프로젝트는 `SetLayeredWindowAttributes`를 일부러 호출하지 않는다** —
부르면 DWM per-pixel 투명이 균일 알파 모드로 덮여 **창이 검게 변한다.**
(그래서 `Win32Native`엔 선언만 있고 미사용이다. 지우지 말 것)

### 5-4. 보더리스는 스타일을 통째로 덮어쓰지 않는다

`SetWindowLong(GWL_STYLE, WS_POPUP | WS_VISIBLE)`로 전부 교체하면 `WS_CLIPCHILDREN` 같은
필수 비트가 사라져 **창이 깨지거나 안 보인다.**

→ 기존 스타일에서 `WS_CAPTION`·`WS_THICKFRAME`·`WS_MINIMIZEBOX`·`WS_MAXIMIZEBOX`·`WS_SYSMENU`
**만 제거**하고 `WS_POPUP`을 더한 뒤, `SWP_FRAMECHANGED`로 프레임을 갱신한다.

### 5-5. 창 핸들은 스플래시 이후에 잡는다

`Start()`에서 바로 `GetActiveWindow()`를 부르면 **유니티 스플래시 때문에 메인 창이 아직 활성이
아니라** 빈/엉뚱한 핸들을 잡는다. 빌드에서만 재현되고 에디터에선 멀쩡하다.

→ `Process.GetCurrentProcess().MainWindowHandle`이 유효해질 때까지 코루틴으로 대기한 뒤 적용한다.

### 5-6. 좌표계가 뒤집혀 있다

| | 원점 | Y축 |
|---|---|---|
| Win32 데스크톱 좌표 | 좌상단 (0,0) | **아래로** + |
| Unity 화면 좌표 | 좌하단 (0,0) | **위로** + |

둘을 오갈 때 **Y축을 뒤집어야 한다.** (`WindowManager.GetCursorScreenPosition` 참조)

### 5-7. 창 이동은 캔버스 드래그로 하되, 이동 자체는 OS에 위임한다

기본이 보더리스(타이틀바 off)라 OS 타이틀바가 없다. 대신 **메인 뷰 캔버스에 타이틀바 역할**을
주어(`WindowDragArea`) 잡아 끌면 창이 움직인다.

⚠️ **직접 좌표를 옮기지 않는다.** `ReleaseCapture` 후 `WM_SYSCOMMAND(SC_MOVE_HTCAPTION)`을 보내
**OS 이동 루프에 위임**한다 — 그래야 스냅·더블클릭 최대화·모니터 간 이동을 다시 만들지 않는다.
"직접 드래그 구현"의 함정을 피하면서 타이틀바 없이도 이동이 된다.

- 클릭/드래그 구분은 `IBeginDragHandler`(EventSystem 이동 임계값)가 공짜로 해 준다 — 단순 클릭은
  아래 버튼으로 가고, 끌기 시작할 때만 이동이 걸린다.
- 콘텐츠(캔버스 Graphic) 위에서만 발화한다 — 빈 영역은 동적 클릭 스루로 통과하므로 잡히지 않는다.
- ⚠️ OS 모달 이동 루프가 도는 동안 마우스를 놓을 때까지 Unity가 잠깐 멈춘다(정상).

---

## 6. 투명이 동작하기 위한 필수 세팅

하나라도 어긋나면 **검은 화면**이 된다. 프로젝트 설정을 건드릴 때 확인한다.

**Player Settings**

| 항목 | 값 |
|------|-----|
| Fullscreen Mode | **Windowed** |
| 그래픽 API (Windows) | **Direct3D11** (Auto for Windows 해제) |
| **Use Flip Model Swapchain** | **해제** |
| Run In Background | **켜기** — 포커스가 없어도 클릭스루 폴링이 돌아야 한다 |

**카메라** — Clear Flags = `Solid Color`, 배경색 **알파 0**

**UI** — 전체화면 불투명 패널을 두지 않는다 (투명 영역이 남아야 바탕화면이 비친다)

---

## 7. ❓ 미해결 의문

1. **URP에서 정말 방법이 없나** — 커스텀 `ScriptableRenderPass`로 FinalBlit 이후 백버퍼 알파를
   직접 쓰는 길이 이론상 있으나, FinalBlit이 사용자 패스 **이후** 단계라 접근이 막힌다.
   커뮤니티도 확실한 해법을 못 냈고 재검증에서도 실패했다. Unity 정식 지원을 기다리는 편이 현실적이다
2. **클릭 스루 + DWM 투명 공존** — 현재 `SetLayeredWindowAttributes`를 부르지 않아 공존한다.
   다양한 GPU·드라이버에서 추가 검증 여지가 있다
3. **다중 모니터 좌표** — `GetCursorScreenPosition`은 단일 모니터 기준이다.
   **음수 좌표 모니터 환경에서 재검증이 필요하다**

---

## 8. 참고 링크

**Win32 / DWM (Microsoft Learn)**

- [DwmExtendFrameIntoClientArea](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmextendframeintoclientarea) — DWM 프레임을 클라이언트 영역으로 확장(투명)
- [SetWindowLong](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlonga) / [GetWindowLong](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowlonga) — 창 스타일 변경
- [SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos) — 위치/크기/Z순서/프레임 갱신
- [Window Styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-styles) / [Extended Window Styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles) — `WS_*` / `WS_EX_*` 상수
- [SetLayeredWindowAttributes](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setlayeredwindowattributes) — **이 프로젝트는 부르지 않는다** (5-3 참조)
- [GetCursorPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorpos) / [GetWindowRect](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowrect)
- [Process.MainWindowHandle](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.mainwindowhandle)
- [P/Invoke (DllImport)](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke) · [`UNITY_EDITOR` 플랫폼 심볼](https://docs.unity3d.com/Manual/PlatformDependentCompilation.html)
