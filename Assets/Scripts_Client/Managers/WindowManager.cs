using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Utils;

/// <summary>
/// 데스크톱 창 제어 핵심 클래스 (Task Bar Hero 스타일).
/// 타이틀바(+테두리) 표시 · 투명 배경 · 항상 위 · 클릭 스루 · 위치/크기 지정을 담당한다.
///
/// ■ #if !UNITY_EDITOR 가드
///   실제 Win32 호출은 모두 빌드(.exe)에서만 실행한다. 에디터에서 메인 윈도우 핸들을
///   건드리면 Unity 에디터 창 자체가 영향을 받아 불안정해지기 때문이다.
///   (UNITY_EDITOR 는 Unity가 정의하는 플랫폼 심볼 — 에디터에서 컴파일될 때만 참)
///
/// ■ 창 이동 방식
///   커스텀 드래그 바(인포바)를 두지 않는다. 대신 OS 타이틀바를 토글로 켜서
///   그 진짜 타이틀바를 잡고 창을 옮긴다. 타이틀바를 끄면 보더리스(깔끔한 창)가 된다.
///
/// ■ 좌표계 주의
///   Win32 데스크톱 좌표는 좌상단(0,0)·아래로 +Y, Unity 화면 좌표는 좌하단(0,0)·위로 +Y.
///   둘을 오갈 때 Y축을 뒤집어야 한다(GetCursorScreenPosition 참조).
/// </summary>
public class WindowManager : SingletonMonoBehaviour<WindowManager>
{
    [CenterHeader("Window Settings - 시작 시 적용할 창 상태")]
    [SerializeField] private bool setStartTitleBar            = true;  // 시작 시, OS 타이틀바+테두리 표시(켜면 이 바로 창 드래그, 끄면 보더리스)
    [SerializeField] private bool setStartTransparent         = true;  // 시작 시, 투명 배경 상태
    [SerializeField] private bool setStartTopmost             = true;  // 시작 시, 항상 위
    [SerializeField] private bool setStartClickThrough        = true;  // 시작 '초기' 클릭 스루 1회(정적). 동적ON이면 매 프레임 덮여 사실상 무의미
    [SerializeField] private bool setStartDynamicClickThrough = true;  // 동적 클릭 스루: 매 프레임 커서로 자동 On/Off(콘텐츠 위=클릭, 빈 영역=통과)

    [CenterHeader("Window Size/Position - 시작 시 창 크기·위치 (데스크톱 컴패니언용)")]
    [SerializeField] private bool       setStartSize = true;                       // 시작 시 창 크기/위치 지정 여부
    [SerializeField] private Vector2Int startSize    = new Vector2Int(360, 720);   // 시작 창 크기(px). 세로로 긴 작은 창.

    [CenterHeader("Dock Settings - 우하단 도킹")]
    [SerializeField] private bool       setStartDock = false;                      // 시작 시 우하단 도킹 여부
    [SerializeField] private Vector2Int dockSize     = new Vector2Int(600, 200);   // 도킹 시 창 크기(px)

    // ─── 시작 설정 게터 (DemoPanel 토글 초기 상태 동기화용) ───
    public bool StartTitleBar            => setStartTitleBar;
    public bool StartTransparent         => setStartTransparent;
    public bool StartTopmost             => setStartTopmost;
    public bool StartDynamicClickThrough => setStartDynamicClickThrough;
    public bool StartDock                => setStartDock;

    // ─── 내부 상태 ───
    private Camera _raycastCamera;                  // 2D 스프라이트 판정용 카메라(Start에서 Camera.main 자동 확보)
    private IntPtr _hWnd               = IntPtr.Zero; // 제어 대상 창 핸들(HWND). 모든 Win32 호출의 첫 인자.
    private bool   _isClickThrough     = false;      // 현재 클릭 스루 상태(중복 호출 방지용 캐시)
    private bool   _isTopmost          = false;      // 현재 항상 위 상태(MoveWindow/ResizeWindow 시 Z순서 유지에 사용)
    private bool   _initialized        = false;      // 초기화 완료 여부
    private bool   _dynamicClickThrough = false;     // 동적 클릭 스루 런타임 상태(시작 설정 필드와 분리 — 런타임에 토글로 변경)

    // 타이틀바 등 "항상 클릭을 받아야 하는" 상황에서 동적 클릭 스루를 잠시 풀도록 하는 내부 플래그
    private bool _forceInteractive = false;

    // 싱글턴 등록 (Instance 확보) — 창 초기화는 Start에서 진행
    protected override void Awake()
    {
        base.Awake();
    }

    void Start()
    {
        if (_raycastCamera == null)
            _raycastCamera = Camera.main;

        // 창 핸들 확보는 타이밍이 중요하므로 코루틴에서 대기 후 초기화한다.
        StartCoroutine(InitializeWhenReady());
    }

    /// <summary>
    /// Unity 메인 창이 실제로 생성될 때까지 기다린 뒤 창 제어를 적용한다.
    /// - Start() 시점엔 유니티 스플래시/초기화 때문에 GetActiveWindow 가 엉뚱한(또는 빈) 핸들을
    ///   반환할 수 있다. 그래서 Process.MainWindowHandle 이 유효(0이 아님)해질 때까지 폴링한다.
    /// - IEnumerator + yield return null : 코루틴. 매 프레임 한 번씩 끊어가며 대기하는 Unity 패턴.
    /// </summary>
    private System.Collections.IEnumerator InitializeWhenReady()
    {
#if !UNITY_EDITOR
        // 프로세스의 메인 창 핸들이 잡힐 때까지 최대 5초 대기 (못 잡으면 GetActiveWindow 로 폴백)
        float timeout = 5f;
        while (timeout > 0f)
        {
            // 현재 실행 중인 프로세스(이 게임)의 메인 윈도우 핸들. 창 생성 전에는 IntPtr.Zero 일 수 있다.
            _hWnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (_hWnd != IntPtr.Zero)
                break;
            timeout -= Time.unscaledDeltaTime; // timeScale 영향 안 받는 실제 경과 시간
            yield return null;                 // 다음 프레임까지 대기
        }
        if (_hWnd == IntPtr.Zero)
            _hWnd = Win32Native.GetActiveWindow(); // 폴백

        yield return null; // 창을 한 프레임 더 안정화시킨 뒤 효과 적용
#endif
        InitializeWindow();
        yield break;
    }

    /// <summary>확보된 창 핸들에 시작 상태(타이틀바 → 크기/위치 → 투명 → 항상위 → 클릭스루)를 적용한다.</summary>
    private void InitializeWindow()
    {
        _dynamicClickThrough = setStartDynamicClickThrough; // 런타임 상태를 시작 설정으로 초기화
#if !UNITY_EDITOR
        // 타이틀바(+테두리) 표시 여부를 먼저 적용한다(프레임 스타일이 이후 크기 적용에 영향).
        SetTitleBar(setStartTitleBar);

        // 도킹이 켜져 있으면 우하단 도킹, 아니면 시작 크기/위치를 적용.
        if (setStartDock)
            DockToTaskbar();
        else
            ApplyStartSizeAndPosition();

        if (setStartTransparent)
            SetTransparent(true);

        SetTopmost(setStartTopmost);
        SetClickThrough(setStartClickThrough);
#endif
        _initialized = true;
    }

    void Update()
    {
#if !UNITY_EDITOR
        // 안전장치 : 투명/보더리스 상태라 창을 닫기 어려우므로 ESC 로 강제 종료.
        if (Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();
#endif

        // 동적 클릭 스루 : 마우스가 콘텐츠(UI/스프라이트) 위면 클릭을 받고, 빈 영역이면 통과시킨다.
        if (!_dynamicClickThrough || !_initialized)
            return;

        // _forceInteractive(타이틀바 표시 중 등) 또는 콘텐츠 위면 클릭을 받도록 클릭스루를 끈다.
        bool pointerOverContent = _forceInteractive || IsPointerOverContent();
        SetClickThrough(!pointerOverContent);
    }

    // ──────────────────────────────────────────────
    // 1. 타이틀바(+테두리) 표시 / 숨김 (보더리스 토글)
    // ──────────────────────────────────────────────

    /// <summary>
    /// OS 타이틀바+테두리를 켜고 끈다.
    /// - show=true  : 타이틀바(WS_CAPTION)+시스템 메뉴(WS_SYSMENU)만 켠다. 이 타이틀바를 잡고 창을 이동할 수 있다.
    ///                ※ 리사이즈용 WS_THICKFRAME / 최대화 WS_MAXIMIZEBOX 는 넣지 않아 가장자리 드래그로 크기 조절 불가.
    /// - show=false : 타이틀바/테두리 비트를 모두 제거하고 WS_POPUP 으로 만든다(보더리스).
    /// </summary>
    public void SetTitleBar(bool show)
    {
        // 타이틀바가 켜지면 그 바를 잡아야 하므로 동적 클릭 스루를 잠시 풀도록 강제한다.
        _forceInteractive = show;
#if !UNITY_EDITOR
        // 기존 스타일을 읽어와 관련 비트만 켜고 끈다.
        //   ※ 통째로 덮어쓰면 WS_CLIPCHILDREN 등 Unity가 필요로 하는 필수 비트까지 사라져
        //     창이 깨질 수 있으므로 "비트만 조작" 방식을 쓴다.
        uint style = Win32Native.GetWindowLong(_hWnd, Win32Native.GWL_STYLE);

        // 어느 경우든 리사이즈/최대화/최소화 관련 비트는 항상 제거(사용자 리사이즈 차단).
        style &= ~(Win32Native.WS_CAPTION | Win32Native.WS_THICKFRAME
                 | Win32Native.WS_MINIMIZEBOX | Win32Native.WS_MAXIMIZEBOX | Win32Native.WS_SYSMENU
                 | Win32Native.WS_POPUP);

        if (show)
            style |= Win32Native.WS_CAPTION | Win32Native.WS_SYSMENU | Win32Native.WS_VISIBLE; // 타이틀바+시스템 메뉴(이동 O, 리사이즈 X)
        else
            style |= Win32Native.WS_POPUP | Win32Native.WS_VISIBLE;                             // 보더리스

        Win32Native.SetWindowLong(_hWnd, Win32Native.GWL_STYLE, style);

        // ⚠️ SetWindowLong 만으로는 변경이 화면에 반영되지 않는다.
        //    SWP_FRAMECHANGED 로 비클라이언트 영역(프레임)을 강제 재계산해야 실제로 적용된다.
        uint flags = Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE | Win32Native.SWP_NOZORDER
                   | Win32Native.SWP_FRAMECHANGED | Win32Native.SWP_SHOWWINDOW;
        Win32Native.SetWindowPos(_hWnd, IntPtr.Zero, 0, 0, 0, 0, flags);

        // 프레임 재계산으로 DWM 투명 확장이 풀릴 수 있어, 투명 상태면 다시 적용한다.
        if (setStartTransparent)
            SetTransparent(true);
#endif
    }

    // ──────────────────────────────────────────────
    // 2. 투명 배경 (DWM 프레임을 클라이언트 영역 전체로 확장)
    // ──────────────────────────────────────────────
    public void SetTransparent(bool enable)
    {
#if !UNITY_EDITOR
        // 각 변 -1 : DWM 프레임(유리 영역)을 창 전체로 확장한다.
        //   → 카메라가 알파 0으로 클리어한 영역이 그대로 투명해져 바탕화면이 비친다.
        //   끌 때는 0 으로 주어 확장을 해제한다.
        Win32Native.MARGINS margins = new Win32Native.MARGINS
        {
            leftWidth    = enable ? -1 : 0,
            rightWidth   = enable ? -1 : 0,
            topHeight    = enable ? -1 : 0,
            bottomHeight = enable ? -1 : 0
        };
        Win32Native.DwmExtendFrameIntoClientArea(_hWnd, ref margins);
#endif
    }

    // ──────────────────────────────────────────────
    // 3. 항상 위 (Always On Top)
    // ──────────────────────────────────────────────
    public void SetTopmost(bool enable)
    {
        _isTopmost = enable; // MoveWindow/ResizeWindow 가 Z순서를 유지하도록 상태 저장
#if !UNITY_EDITOR
        // hWndInsertAfter 에 HWND_TOPMOST/HWND_NOTOPMOST 를 주어 Z순서만 바꾼다.
        //   SWP_NOMOVE|SWP_NOSIZE 로 위치·크기는 건드리지 않는다.
        IntPtr insertAfter = enable ? Win32Native.HWND_TOPMOST : Win32Native.HWND_NOTOPMOST;
        uint   flags       = Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE | Win32Native.SWP_SHOWWINDOW;
        Win32Native.SetWindowPos(_hWnd, insertAfter, 0, 0, 0, 0, flags);
#endif
    }

    // ──────────────────────────────────────────────
    // 4. 클릭 스루 (빈 영역 클릭을 뒤 창으로 통과)
    // ──────────────────────────────────────────────
    public void SetClickThrough(bool enable)
    {
        if (enable == _isClickThrough)
            return; // 상태가 같으면 불필요한 Win32 호출 생략(Update 에서 매 프레임 불릴 수 있으므로 중요)

        _isClickThrough = enable;
#if !UNITY_EDITOR
        // 확장 스타일을 읽어와 WS_EX_TRANSPARENT 비트를 켜고 끈다.
        uint exStyle = Win32Native.GetWindowLong(_hWnd, Win32Native.GWL_EXSTYLE);

        if (enable)
        {
            // WS_EX_TRANSPARENT : 마우스 입력을 이 창이 받지 않고 뒤 창으로 통과시킨다.
            // WS_EX_LAYERED 도 함께 켠다(전제 플래그).
            // ⚠️ SetLayeredWindowAttributes 는 호출하지 않는다 — 호출하면 DWM per-pixel
            //    투명(DwmExtendFrameIntoClientArea)이 균일 알파 모드로 덮여 창이 검게 변한다.
            exStyle |= Win32Native.WS_EX_LAYERED | Win32Native.WS_EX_TRANSPARENT;
            Win32Native.SetWindowLong(_hWnd, Win32Native.GWL_EXSTYLE, exStyle);
        }
        else
        {
            // 입력 통과 플래그만 제거 → 창이 다시 클릭을 받는다. (WS_EX_LAYERED 는 유지)
            exStyle &= ~Win32Native.WS_EX_TRANSPARENT;
            Win32Native.SetWindowLong(_hWnd, Win32Native.GWL_EXSTYLE, exStyle);
        }
#endif
    }

    // ──────────────────────────────────────────────
    // 5. 창 위치 / 크기 지정 (시작 배치 · 우하단 도킹)
    // ──────────────────────────────────────────────

    /// <summary>창을 데스크톱 좌표 (x, y)로 이동. 크기는 유지(SWP_NOSIZE).</summary>
    public void MoveWindow(int x, int y)
    {
#if !UNITY_EDITOR
        uint flags = Win32Native.SWP_NOSIZE | Win32Native.SWP_NOACTIVATE | Win32Native.SWP_SHOWWINDOW;
        // 현재 항상위 상태를 유지하도록 적절한 Z순서 핸들을 넘긴다.
        IntPtr after = _isTopmost ? Win32Native.HWND_TOPMOST : Win32Native.HWND_NOTOPMOST;
        Win32Native.SetWindowPos(_hWnd, after, x, y, 0, 0, flags);
#endif
    }

    /// <summary>창 크기를 (width, height)로 변경. 위치는 유지(SWP_NOMOVE).</summary>
    public void ResizeWindow(int width, int height)
    {
#if !UNITY_EDITOR
        uint flags = Win32Native.SWP_NOMOVE | Win32Native.SWP_NOACTIVATE | Win32Native.SWP_SHOWWINDOW;
        IntPtr after = _isTopmost ? Win32Native.HWND_TOPMOST : Win32Native.HWND_NOTOPMOST;
        Win32Native.SetWindowPos(_hWnd, after, 0, 0, width, height, flags);
#endif
    }

    /// <summary>시작 설정(startSize)에 따라 창을 우하단 부근의 시작 크기/위치로 배치한다.</summary>
    private void ApplyStartSizeAndPosition()
    {
#if !UNITY_EDITOR
        if (!setStartSize)
            return;

        // 크기를 잡은 뒤 화면 우하단(작업표시줄 위쪽)에 배치. Screen.currentResolution = 주 모니터 해상도.
        ResizeWindow(startSize.x, startSize.y);
        int sw = Screen.currentResolution.width;
        int sh = Screen.currentResolution.height;
        MoveWindow(sw - startSize.x - 40, sh - startSize.y - 60);
#endif
    }

    /// <summary>창을 화면 우하단 모서리(작업표시줄 위)로 도킹한다(dockSize 기준).</summary>
    public void DockToTaskbar()
    {
#if !UNITY_EDITOR
        int x = Screen.currentResolution.width  - dockSize.x;         // 우측 정렬
        int y = Screen.currentResolution.height - dockSize.y - 48;    // 작업표시줄 높이(약 48px)만큼 위
        ResizeWindow(dockSize.x, dockSize.y);
        MoveWindow(x, y);
#endif
    }

    /// <summary>도킹을 켜고 끈다. true=우하단 도킹, false=시작 크기/위치로 복원.(디버그 패널 토글에서 호출)</summary>
    public void SetDock(bool enable)
    {
        if (enable)
            DockToTaskbar();
        else
            ApplyStartSizeAndPosition();
    }

    // ──────────────────────────────────────────────
    // 마우스가 콘텐츠(UI / 2D 스프라이트) 위에 있는지 판정
    //   ★ 클릭 스루 ON 상태에선 OS가 마우스 메시지를 창에 안 보내므로,
    //     Unity의 Input.mousePosition / EventSystem.IsPointerOverGameObject 는 동작하지 않는다.
    //     → Win32 GetCursorPos 로 전역 커서를 직접 폴링해서 판정한다.
    // ──────────────────────────────────────────────

    // RaycastAll 결과 재사용 버퍼(매 프레임 new 방지 → GC 부담 감소)
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();

    private bool IsPointerOverContent()
    {
        Vector2 screenPos = GetCursorScreenPosition();

        // 1) uGUI UI 위에 있는지 : 커서 좌표를 직접 넣어 수동 레이캐스트한다.
        //    (보통은 EventSystem이 자동 처리하지만, 클릭스루 중엔 입력이 안 오므로 수동으로 쏜다)
        if (EventSystem.current != null)
        {
            PointerEventData pointer = new PointerEventData(EventSystem.current) { position = screenPos };
            _raycastResults.Clear();
            EventSystem.current.RaycastAll(pointer, _raycastResults); // 해당 좌표의 모든 UI를 수집
            if (_raycastResults.Count > 0)
                return true; // UI가 하나라도 걸리면 콘텐츠 위
        }

        // 2) 2D 스프라이트(콜라이더) 위에 있는지 : 스크린 좌표 → 월드 좌표 → Physics2D 점 검사.
        if (_raycastCamera != null)
        {
            Vector3   worldPoint = _raycastCamera.ScreenToWorldPoint(screenPos);
            Collider2D hit        = Physics2D.OverlapPoint(worldPoint);
            if (hit != null)
                return true;
        }

        return false; // 아무것도 없으면 빈 영역 → 클릭 통과 대상
    }

    /// <summary>
    /// 전역 커서(데스크톱) 좌표를 Unity 화면 좌표(좌하단 0,0)로 변환해 반환한다.
    /// 클릭 스루 ON 상태에서도 마우스 위치를 얻기 위함.
    /// </summary>
    private Vector2 GetCursorScreenPosition()
    {
#if !UNITY_EDITOR
        // 전역 커서 위치와 창의 화면 사각 영역을 모두 얻을 수 있을 때만 변환한다.
        if (_hWnd != IntPtr.Zero
            && Win32Native.GetCursorPos(out Win32Native.POINT cursor)
            && Win32Native.GetWindowRect(_hWnd, out Win32Native.RECT rect))
        {
            // 데스크톱 좌표 → 창 클라이언트 기준 좌표 (보더리스 popup이라 client ≈ window 로 근사)
            float localX = cursor.x - rect.left;
            float localY = cursor.y - rect.top;
            // Win32는 위가 0, Unity는 아래가 0 → Y축 뒤집기
            return new Vector2(localX, Screen.height - localY);
        }
#endif
        return Input.mousePosition; // 에디터 / 폴백 (레거시 Input — Active Input Handling = Both 필요)
    }

    /// <summary>동적 클릭 스루(마우스 위치 자동 판정)를 켜고 끈다.(디버그 패널 토글에서 호출)</summary>
    public void SetDynamicClickThrough(bool value)
    {
        _dynamicClickThrough = value;
    }

    // 외부(디버그 패널)에서 현재 상태를 읽기 위한 읽기 전용 프로퍼티
    public bool IsClickThrough => _isClickThrough;
    public bool IsTopmost      => _isTopmost;
}
