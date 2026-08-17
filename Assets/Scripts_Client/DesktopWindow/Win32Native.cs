using System;
using System.Runtime.InteropServices;

/// <summary>
/// Win32 / DWM 네이티브 API의 P/Invoke 선언 모음.
/// 실제 호출 순서·조건은 'WindowManager'가 가진다.
/// </summary>
/// <remarks>
/// P/Invoke 개념 · 사용 DLL · 기능↔호출 매핑 · 함정은 'DesktopWindow 규칙.md' 참조.
/// </remarks>
public static class Win32Native
{
    // ──────────────────────────────────────────────────────────────
    // 상수 : 창 스타일 인덱스 (SetWindowLong / GetWindowLong 의 nIndex 인자)
    //   어떤 스타일 묶음을 읽고 쓸지 가리키는 "주소" 역할. 음수 매직 넘버는 Win32 규약.
    // ──────────────────────────────────────────────────────────────
    public const int GWL_STYLE   = -16; // 기본 윈도우 스타일(GWL_STYLE) 묶음
    public const int GWL_EXSTYLE = -20; // 확장 윈도우 스타일(GWL_EXSTYLE) 묶음

    // ──────────────────────────────────────────────────────────────
    // 상수 : 기본 스타일 플래그 (GWL_STYLE 에 OR/AND 로 켜고 끈다)
    //   값은 비트 플래그(2의 거듭제곱)라 | 로 합치고 & ~ 로 제거한다.
    // ──────────────────────────────────────────────────────────────
    public const uint WS_POPUP       = 0x80000000; // 팝업 창 : 타이틀바·테두리 없는 형태
    public const uint WS_VISIBLE     = 0x10000000; // 창을 화면에 표시
    public const uint WS_CAPTION     = 0x00C00000; // 타이틀바(캡션) — 보더리스 시 제거 대상
    public const uint WS_THICKFRAME  = 0x00040000; // 크기 조절용 두꺼운 테두리 — 제거 대상
    public const uint WS_MINIMIZEBOX = 0x00020000; // 최소화 버튼 — 제거 대상
    public const uint WS_MAXIMIZEBOX = 0x00010000; // 최대화 버튼 — 제거 대상
    public const uint WS_SYSMENU     = 0x00080000; // 좌상단 시스템 메뉴 — 제거 대상

    // ──────────────────────────────────────────────────────────────
    // 상수 : 확장 스타일 플래그 (GWL_EXSTYLE)
    // ──────────────────────────────────────────────────────────────
    public const uint WS_EX_LAYERED     = 0x00080000; // 레이어드 창(합성 대상) — 클릭스루의 전제 플래그
    public const uint WS_EX_TRANSPARENT = 0x00000020; // 입력 통과 : 마우스 메시지를 이 창이 받지 않고
                                                       //   뒤(아래)에 있는 창으로 흘려보냄(클릭 스루의 핵심)

    // ──────────────────────────────────────────────────────────────
    // 상수 : SetWindowPos 의 hWndInsertAfter 인자 (Z순서에서 어디에 삽입할지)
    //   특수 핸들값(-1, -2)을 IntPtr 로 감싼 것.
    // ──────────────────────────────────────────────────────────────
    public static readonly IntPtr HWND_TOPMOST   = new IntPtr(-1); // 항상 다른 창들 위(최상위)
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2); // 최상위 해제(일반 Z순서로 복귀)

    // ──────────────────────────────────────────────────────────────
    // 상수 : SetWindowPos 의 uFlags (어떤 변경을 적용/생략할지)
    // ──────────────────────────────────────────────────────────────
    public const uint SWP_NOSIZE       = 0x0001; // cx,cy(크기) 인자 무시 → 현재 크기 유지
    public const uint SWP_NOMOVE       = 0x0002; // X,Y(위치) 인자 무시 → 현재 위치 유지
    public const uint SWP_NOZORDER     = 0x0004; // hWndInsertAfter 무시 → 현재 Z순서 유지
    public const uint SWP_NOACTIVATE   = 0x0010; // 창을 활성화(포커스)하지 않음
    public const uint SWP_FRAMECHANGED = 0x0020; // 프레임(비클라이언트 영역)을 다시 계산 →
                                                 //   SetWindowLong 으로 바꾼 스타일을 화면에 실제 반영시킴
    public const uint SWP_SHOWWINDOW   = 0x0040; // 창을 보이게 함

    // ──────────────────────────────────────────────────────────────
    // 상수 : 창 드래그용 시스템 메시지
    // ──────────────────────────────────────────────────────────────
    public const int WM_SYSCOMMAND     = 0x0112; // 시스템 명령 메시지(이동/크기/최소화 등)
    public const int SC_MOVE_HTCAPTION = 0xF012; // SC_MOVE(0xF010) | HTCAPTION(0x0002).
                                                 //   "타이틀바를 잡고 창을 옮긴다"는 명령을 OS에 위임하는 트릭.
                                                 //   보더리스라 타이틀바가 없어도 이 메시지로 창을 끌 수 있다.

    // ──────────────────────────────────────────────────────────────
    // 상수 : SetLayeredWindowAttributes 의 dwFlags
    //   ⚠️ 이 함수는 호출하지 않는다 — 부르면 DWM per-pixel 투명이 덮여 창이 검게 변한다.
    //      선언만 보존한다. 지우지 말 것 (DesktopWindow 규칙.md 5-3)
    // ──────────────────────────────────────────────────────────────
    public const uint LWA_ALPHA    = 0x00000002; // 창 전체에 균일 알파값 적용
    public const uint LWA_COLORKEY = 0x00000001; // 특정 색을 투명색(색상키)으로 처리

    /// <summary>
    /// DWM 프레임 여백 구조체 (DwmExtendFrameIntoClientArea 인자).
    /// 각 변의 값을 -1 로 주면 "프레임(유리 영역)을 창 전체로 확장"하라는 특수 지시가 되어,
    /// 클라이언트 영역에서 알파가 0인 픽셀이 그대로 투명해진다.
    /// [StructLayout(Sequential)] : 필드를 선언 순서대로 메모리에 배치 → C의 MARGINS 구조체와 1:1 호환.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int leftWidth;    // 왼쪽 여백
        public int rightWidth;   // 오른쪽 여백
        public int topHeight;    // 위쪽 여백
        public int bottomHeight; // 아래쪽 여백
    }

    /// <summary>
    /// 창의 화면상 사각 영역 구조체 (GetWindowRect 의 out 인자).
    /// 데스크톱(스크린) 좌표계 기준이며 left/top 이 창의 좌상단.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left;   // 좌측 X
        public int top;    // 상단 Y
        public int right;  // 우측 X
        public int bottom; // 하단 Y
    }

    /// <summary>
    /// 화면 좌표 점 구조체 (GetCursorPos 의 out 인자).
    /// Win32 데스크톱 좌표는 좌상단이 (0,0)이고 아래로 갈수록 Y가 커진다(Unity와 Y축 반대).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x; // 데스크톱 X (좌상단 0, 오른쪽 +)
        public int y; // 데스크톱 Y (좌상단 0, 아래쪽 +)
    }

    // ──────────────────────────────────────────────────────────────
    // user32.dll — 일반 윈도우 관리 함수
    // ──────────────────────────────────────────────────────────────

    /// <summary>현재 스레드의 활성 창 핸들을 반환. (스플래시 타이밍엔 부정확할 수 있어 폴백용으로만 사용)</summary>
    [DllImport("user32.dll")]
    public static extern IntPtr GetActiveWindow();

    /// <summary>
    /// 창의 스타일 값을 설정(쓰기). nIndex 로 어떤 묶음(GWL_STYLE/GWL_EXSTYLE)인지 지정.
    /// 반환값은 변경 전 값.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    /// <summary>창의 현재 스타일 값을 읽기. (비트 제거/추가를 위해 먼저 현재 값을 읽을 때 사용)</summary>
    [DllImport("user32.dll")]
    public static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    /// <summary>
    /// 레이어드 창의 균일 알파/색상키 설정. ⚠️ 호출하지 않는다 — DWM 투명과 충돌한다.
    /// crKey: 색상키, bAlpha: 0~255 알파, dwFlags: LWA_ALPHA / LWA_COLORKEY.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    /// <summary>창의 화면상 사각 영역(RECT)을 가져온다. 커서가 창 안 어디인지 계산할 때 사용.</summary>
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>
    /// 창의 위치·크기·Z순서를 한 번에 설정. uFlags(SWP_*)로 무엇을 적용/생략할지 제어.
    /// hWndInsertAfter 에 HWND_TOPMOST 등을 주면 Z순서(항상 위)까지 조정.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    /// <summary>창에 윈도우 메시지를 보낸다. 창 드래그(WM_SYSCOMMAND) 트리거에 사용.</summary>
    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    /// <summary>
    /// 현재 마우스 캡처를 해제. 창 드래그 직전에 호출 →
    /// 이어지는 WM_SYSCOMMAND(SC_MOVE) 가 "타이틀바를 잡은 것"처럼 동작하게 만든다.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    /// <summary>
    /// 데스크톱 전역 커서 위치(POINT)를 가져온다.
    /// 클릭스루(WS_EX_TRANSPARENT) 상태에선 Unity가 마우스 메시지를 못 받으므로,
    /// 이 전역 폴링으로 커서 위치를 직접 얻어 UI 위인지 판정한다.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    public const uint SPI_GETWORKAREA = 0x0030; // SystemParametersInfo 액션: 작업 영역(작업표시줄 제외) 조회

    /// <summary>
    /// 시스템 파라미터를 조회/설정한다. 여기선 SPI_GETWORKAREA 로 주 모니터의
    /// "작업 영역"(작업표시줄을 제외한 사용 가능 화면 사각형)을 RECT 로 받는다.
    /// → 창을 9분할 위치로 배치할 때 작업표시줄에 가려지지 않게 계산하는 데 사용.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

    public const int SM_CXSCREEN = 0; // GetSystemMetrics: 주 모니터 전체 가로 픽셀(작업표시줄 포함)
    public const int SM_CYSCREEN = 1; // GetSystemMetrics: 주 모니터 전체 세로 픽셀(작업표시줄 포함)

    /// <summary>
    /// 시스템 지표를 조회한다. SM_CXSCREEN/SM_CYSCREEN 으로 주 모니터의 "전체" 해상도를 얻는다.
    /// → 프리셋 크기가 모니터를 넘는지 판정하는 데 쓴다
    ///   (창 크기 자체는 16:9 절대 픽셀이다 — 'Managers 규칙.md' 5장).
    /// (SPI_GETWORKAREA 와 같은 좌표계 — DPI 가상화 환경에서도 서로 일관된다.)
    /// </summary>
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    /// <summary>
    /// 창의 "클라이언트 영역"(실제 렌더 영역) 크기를 가져온다. left/top 은 항상 0이고
    /// right/bottom 이 곧 가로/세로다 — GetWindowRect(외곽)와 다르다는 점이 핵심.
    /// → 창 크기를 적용한 뒤 렌더 영역이 정말 의도한 값인지 검증하는 데 쓴다.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>
    /// 창이 올라가 있는 모니터의 DPI를 반환한다(96 = 100%, 120 = 125%, 144 = 150%).
    /// AdjustWindowRectExForDpi 에 넘길 값이다.
    /// ⚠️ Windows 10 1607 미만에는 없다 — 호출부에서 EntryPointNotFoundException 을 잡아 96으로 폴백한다.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hWnd);

    /// <summary>
    /// 원하는 "클라이언트 영역" 사각형을, 그 영역을 얻기 위해 필요한 "외곽" 사각형으로 부풀린다.
    /// 타이틀바·테두리 두께를 OS가 현재 스타일과 dpi 기준으로 직접 계산해 주므로,
    /// 캡션 높이를 코드에 상수로 박을 필요가 없다.
    ///
    /// ⚠️ SetWindowPos 는 "외곽" 크기를 받는다. 원하는 렌더 해상도를 그대로 넘기면
    ///   프레임 두께만큼 렌더 영역이 줄어 화면비가 깨진다 — 이 함수는 그걸 막으려고 있다.
    /// ⚠️ Windows 10 1607 미만에는 없다 → AdjustWindowRectEx 폴백.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool AdjustWindowRectExForDpi(ref RECT lpRect, uint dwStyle, bool bMenu, uint dwExStyle, uint dpi);

    /// <summary>AdjustWindowRectExForDpi 의 dpi 없는 구형 버전. 폴백 전용.</summary>
    [DllImport("user32.dll")]
    public static extern bool AdjustWindowRectEx(ref RECT lpRect, uint dwStyle, bool bMenu, uint dwExStyle);

    public const uint MONITOR_DEFAULTTONEAREST = 0x00000002; // MonitorFromWindow: 겹치는 모니터가 없으면 가장 가까운 것

    /// <summary>
    /// 모니터 정보 구조체 (GetMonitorInfo 의 out 인자).
    /// cbSize 를 sizeof(MONITORINFO) 로 채워 넣어야 호출이 성공한다 — Win32 규약.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int  cbSize;    // 구조체 크기(바이트). 호출 전에 반드시 채운다
        public RECT rcMonitor; // 모니터 전체 사각형
        public RECT rcWork;    // 작업 영역(작업표시줄 제외) 사각형
        public uint dwFlags;   // MONITORINFOF_PRIMARY 등
    }

    /// <summary>
    /// 창이 실제로 올라가 있는 모니터의 핸들을 반환한다.
    /// ⚠️ SPI_GETWORKAREA 는 "주 모니터"만 알려 준다 — 듀얼 모니터에서 창이 보조 모니터에 있으면
    ///   그 값으로 계산한 위치·크기가 전부 어긋난다. 그래서 이 쪽을 쓴다.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    /// <summary>모니터 핸들로 그 모니터의 전체/작업 영역 사각형을 가져온다.</summary>
    [DllImport("user32.dll")]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    // ──────────────────────────────────────────────────────────────
    // dwmapi.dll — 데스크톱 합성기(투명/유리 효과)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// DWM 프레임(유리 영역)을 클라이언트 영역으로 확장한다.
    /// MARGINS 의 각 변을 -1 로 주면 창 전체로 확장 → 카메라가 알파 0으로 그린 영역이
    /// 그대로 투명해져 바탕화면이 비친다. (Built-in 투명의 핵심 호출)
    /// 반환값은 HRESULT(0이면 성공).
    /// </summary>
    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);
}
