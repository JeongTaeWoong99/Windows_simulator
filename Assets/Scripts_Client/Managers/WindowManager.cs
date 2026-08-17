using System;
using System.Collections.Generic;
using System.Runtime.InteropServices; // MONITORINFO.cbSize 를 채우는 Marshal.SizeOf
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 데스크톱 창 제어 핵심 클래스. 타이틀바(+테두리) 표시 · 투명 배경 · 항상 위 · 클릭 스루 ·
/// 위치/크기 지정을 담당한다 — Win32 선언은 'DesktopWindow/Win32Native'에 있다.
/// </summary>
/// <remarks>
/// ⚠️ 실제 Win32 호출은 '#if !UNITY_EDITOR'로 빌드에서만 돈다 — 에디터에서는 값만 바뀌고
/// 창은 그대로다.
/// 창 정책(크기 프리셋·캔버스 기준 해상도)은 'Managers 규칙.md' 5장,
/// Win32 함정(좌표계 뒤집힘·스타일 교체·클릭스루)은 'DesktopWindow 규칙.md' 5장 참조.
/// </remarks>

/// <summary>
/// 창을 배치할 6칸 앵커(위치). 'WidgetPosition'과 같은 나열 순서(가로 3 × 세로 2)를 써서
/// index%3 = 가로(0=좌,1=중,2=우), index/3 = 세로(0=위,1=아래)로 계산할 수 있다.
/// 위젯 위치와 인덱스가 1:1이라 설정에서 드롭다운 하나가 창·위젯 위치를 함께 정한다.
/// </summary>
public enum ScreenAnchor
{
    UpperLeft, UpperCenter, UpperRight,
    LowerLeft, LowerCenter, LowerRight,
}

/// <summary>
/// 창 크기 배율 프리셋. 기준 960x540(16:9)에 배율을 곱한 절대 픽셀이다.
///   X1 = 960x540, X1_25 = 1200x675, X1_5 = 1440x810, X2 = 1920x1080.
/// </summary>
/// <remarks>
/// 모니터 비례가 아닌 이유와 16:9 유지 규칙은 'Managers 규칙.md' 5장 참조.
/// </remarks>
public enum WindowScale
{
    X1,    // 960x540
    X1_25, // 1200x675
    X1_5,  // 1440x810
    X2     // 1920x1080
}

public class WindowManager : MonoService<WindowManager>
{
    // ─── 창 크기 기준값 (16:9) ───
    private const int BaseWidth  = 960; // 배율 1x 일 때의 가로
    private const int BaseHeight = 540; // 배율 1x 일 때의 세로

    // ─── static readonly 표 (WindowScale enum 순서와 1:1) ───
    private static readonly string[] SizeLabels   = { "1x", "1.25x", "1.5x", "2x" }; // 드롭다운 표시 라벨
    private static readonly float[]  ScaleFactors = { 1f  , 1.25f  , 1.5f  , 2f   }; // 기준 960x540에 곱할 배율

    // ─── 공장 초기값 (인스펙터) ───
    // ⚠️ 여기 적은 값은 "저장된 설정이 없을 때"만 쓰인다. 사용자가 한 번이라도 토글·드롭다운을
    //   만지면 그 값이 PlayerPrefs에 저장되고, 다음 실행부터는 저장값이 이긴다(WindowSettings 참조).
    [CenterHeader("Window Settings - 저장값이 없을 때 쓸 초기 상태")]
    [SerializeField] private bool         setStartTitleBar            = false;                   // OS 타이틀바+테두리 표시(끄면 보더리스 — 창 이동은 캔버스 드래그로). 토글 제거로 이제 이 값이 고정값이다
    [SerializeField] private bool         setStartTransparent         = true;                    // 투명 배경 상태
    [SerializeField] private bool         setStartTopmost             = true;                    // 항상 위
    [SerializeField] private bool         setStartDynamicClickThrough = true;                    // 동적 클릭 스루: 매 프레임 커서로 자동 On/Off(콘텐츠 위=클릭, 빈 영역=통과)
    [SerializeField] private WindowScale  setStartScale               = WindowScale.X1;          // 창 크기 배율(프리셋 1개 선택)
    [SerializeField] private ScreenAnchor setStartAnchor              = ScreenAnchor.LowerRight; // 창 위치(9분할 앵커 1개 선택)

    // ─── 런타임 상태 ───
    // 6개 설정 전부가 여기 짝을 갖는다. 인스펙터 필드를 직접 읽으면 사용자가 바꾼 값이 무시된다.
    private bool         _isTitleBar;                              // 현재 타이틀바 표시 상태
    private bool         _isTransparent;                           // 현재 투명 배경 상태
    private bool         _isTopmost;                               // 현재 항상 위 상태(창 이동·리사이즈 시 Z순서 유지에 사용)
    private bool         _dynamicClickThrough;                     // 현재 동적 클릭 스루 상태
    private WindowScale  _currentScale  = WindowScale.X1;          // 현재 적용된 크기 배율 프리셋
    private ScreenAnchor _currentAnchor = ScreenAnchor.LowerRight; // 현재 적용된 위치 앵커

    // ─── 내부 상태 ───
    private Camera? _raycastCamera;                 // 2D 스프라이트 판정용 카메라(Start에서 Camera.main 자동 확보 — 없을 수 있어 nullable)
    private IntPtr  _hWnd           = IntPtr.Zero;  // 제어 대상 창 핸들(HWND). 모든 Win32 호출의 첫 인자.
    private bool    _isClickThrough = false;        // 현재 클릭 스루 상태(중복 호출 방지용 캐시). 매 프레임 바뀌므로 저장하지 않는다.
    private bool    _initialized    = false;        // 초기화 완료 여부

    // 타이틀바 등 "항상 클릭을 받아야 하는" 상황에서 동적 클릭 스루를 잠시 풀도록 하는 내부 플래그
    private bool _forceInteractive = false;

    // 항상 위 재확정 : 작업표시줄(그 자체가 topmost)이 앞으로 오면 우리 창이 topmost 밴드 안에서
    //   뒤로 밀린다. 켜져 있는 동안 주기적으로 맨 앞으로 되돌리기 위한 누적 시간.
    private       float _topmostReassertTimer;
    private const float TopmostReassertInterval = 0.5f; // 초. 짧을수록 즉각적이지만 SetWindowPos 호출이 잦아진다

    // RaycastAll 결과 재사용 버퍼(매 프레임 new 방지 → GC 부담 감소)
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();

    // ─── 프로퍼티 ───
    // 현재 설정 게터 — 창 제어 패널이 토글/드롭다운 초기 상태를 맞추는 데 쓴다.
    // 저장에서 복원된 값이므로 "시작값"이 아니라 "지금 값"이다.
    public bool TitleBar            => _isTitleBar;
    public bool Transparent         => _isTransparent;
    public bool Topmost             => _isTopmost;
    public bool DynamicClickThrough => _dynamicClickThrough;
    public int  SizeIndex           => (int)_currentScale;
    public int  AnchorIndex         => (int)_currentAnchor;

    // ──────────────────────────────────────────────
    // Unity 생명주기
    // ──────────────────────────────────────────────

    /// <summary>
    /// 서비스 등록('base.Awake') 후 저장된 설정을 런타임 상태로 읽어 온다.
    /// ⚠️ 매니저 중 유일하게 'Awake'에서 초기화한다 — 'SettingPresenter.Start()'가
    /// 이 값을 읽기 때문이다. 다른 서비스를 건드리지 않는 순수 값 로드라 안전하다
    /// ('Managers 규칙.md' 3장).
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        LoadSettings();
    }

    // 창 제어 초기화 (Unity 메시지)
    private void Start()
    {
        if (_raycastCamera == null)
            _raycastCamera = Camera.main;

        // 창 핸들 확보는 타이밍이 중요하므로 코루틴에서 대기 후 초기화한다.
        StartCoroutine(InitializeWhenReady());
    }

    private void Update()
    {
        // 안전장치 : 투명/보더리스 상태라 창을 닫기 어려우므로 ESC 로 강제 종료(종료 버튼과 같은 경로).
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            QuitApplication();

        // 항상 위 유지 : 작업표시줄 등 다른 topmost 창이 앞으로 오면 밴드 안에서 밀리므로 주기적으로 재확정.
        //   (동적 클릭 스루와 무관하게 돌아야 하니 아래 early-return 앞에 둔다)
        if (_isTopmost && _initialized)
        {
            _topmostReassertTimer += Time.unscaledDeltaTime;
            if (_topmostReassertTimer >= TopmostReassertInterval)
            {
                _topmostReassertTimer = 0f;
                ReassertTopmost();
            }
        }

        // 동적 클릭 스루 : 마우스가 콘텐츠(UI/스프라이트) 위면 클릭을 받고, 빈 영역이면 통과시킨다.
        if (!_dynamicClickThrough || !_initialized)
            return;

        // _forceInteractive(타이틀바 표시 중 등) 또는 콘텐츠 위면 클릭을 받도록 클릭스루를 끈다.
        bool pointerOverContent = _forceInteractive || IsPointerOverContent();
        SetClickThrough(!pointerOverContent);
    }

    #region 초기화

    /// <summary>
    /// 저장된 설정을 런타임 상태로 읽어 온다. 없으면 인스펙터의 공장 초기값을 쓴다 (Awake에서 호출).
    /// 값만 채우고 Win32는 건드리지 않는다 — 실제 적용은 'InitializeWindow'다.
    /// </summary>
    private void LoadSettings()
    {
        // ⚠️ 타이틀바·투명·동적 클릭스루는 설정 UI에서 토글을 걷어냈다 — 이제 저장값을 읽지 않고
        //   인스펙터 공장값을 그대로 고정한다. 저장값을 읽으면 옛 실행에서 남은 상태가 되살아나
        //   UI로는 되돌릴 방법이 없어진다(토글이 없으므로).
        _isTitleBar          = setStartTitleBar;
        _isTransparent       = setStartTransparent;
        _dynamicClickThrough = setStartDynamicClickThrough;

        // Topmost·크기·위치는 토글/드롭다운이 남아 있어 저장값을 계속 쓴다.
        _isTopmost = WindowSettings.LoadBool(WindowSettings.TopmostKey, setStartTopmost);

        // 저장값이 열거형 범위를 벗어나면(버전이 바뀌어 항목이 줄었다면) 안쪽으로 당긴다.
        int scale  = WindowSettings.LoadInt(WindowSettings.ScaleKey,  (int)setStartScale);
        int anchor = WindowSettings.LoadInt(WindowSettings.AnchorKey, (int)setStartAnchor);

        _currentScale  = (WindowScale)Mathf.Clamp(scale, 0, (int)WindowScale.X2);
        _currentAnchor = (ScreenAnchor)MigrateAnchor(anchor);
    }

    /// <summary>
    /// 레거시 9분할 앵커 저장값(0~8)을 현재 6칸(0~5)으로 옮긴다. Upper 행(0~2)은 그대로,
    /// 옛 Middle(3~5)·Lower(6~8) 행은 모두 새 Lower 행(3~5)으로 접는다.
    /// 새 6칸 값에 대해서는 자기 자신을 돌려주므로(멱등) 여러 번 실행해도 안전하다.
    /// </summary>
    private static int MigrateAnchor(int saved)
    {
        int mapped = saved <= 2 ? saved : saved >= 6 ? saved - 3 : saved;
        return Mathf.Clamp(mapped, 0, (int)ScreenAnchor.LowerRight);
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

    /// <summary>
    /// 확보된 창 핸들에 현재 상태(타이틀바 → 크기/위치 → 투명 → 항상위 → 클릭스루)를 적용한다.
    /// 값은 'LoadSettings'가 이미 채워 뒀다 — 여기선 Win32에 반영만 한다.
    /// </summary>
    private void InitializeWindow()
    {
#if !UNITY_EDITOR
        // 타이틀바(+테두리) 표시 여부를 먼저 적용한다(프레임 스타일이 이후 크기 적용에 영향).
        SetTitleBar(_isTitleBar);
#endif
        // 크기 적용(에디터에서도 실행되지만 창 리사이즈/이동은 빌드에서만 일어난다).
        SetWindowSizeByIndex((int)_currentScale);
#if !UNITY_EDITOR
        SetTransparent(_isTransparent);
        SetTopmost(_isTopmost);
        SetClickThrough(false); // 정적 클릭스루 시작값은 동적 클릭스루가 있으면 무의미 → 클릭 받는 상태로 시작(동적ON이면 Update가 관리)
#endif
        _initialized = true;
    }

    #endregion

    #region 창 상태 제어 (타이틀바 · 투명 · 항상위 · 클릭스루)

    /// <summary>
    /// OS 타이틀바+테두리를 켜고 끈다.
    /// - show=true  : 타이틀바(WS_CAPTION)+시스템 메뉴(WS_SYSMENU)만 켠다. 이 타이틀바를 잡고 창을 이동할 수 있다.
    ///                ※ 리사이즈용 WS_THICKFRAME / 최대화 WS_MAXIMIZEBOX 는 넣지 않아 가장자리 드래그로 크기 조절 불가.
    /// - show=false : 타이틀바/테두리 비트를 모두 제거하고 WS_POPUP 으로 만든다(보더리스).
    /// </summary>
    public void SetTitleBar(bool show)
    {
        _isTitleBar = show;
        WindowSettings.SaveBool(WindowSettings.TitleBarKey, show);

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

        // ⚠️ 위 호출은 'SWP_NOSIZE'라 외곽 크기를 그대로 둔다 — 프레임 두께만 바뀌므로
        //   그만큼 클라이언트(렌더) 영역이 늘거나 줄어 16:9가 깨진다. 캔버스가 그 폭으로
        //   레이아웃을 한 번 계산하면 3열 폭이 어긋난 채 굳는다 (A-1 버그).
        //   → 스타일이 바뀐 직후 크기·위치를 다시 적용해 렌더 영역과 앵커를 되돌린다.
        ApplySizeAndPosition(_currentScale, _currentAnchor);

        // 프레임 재계산으로 DWM 투명 확장이 풀릴 수 있어, 투명 상태면 다시 적용한다.
        // ⚠️ 인스펙터의 공장 초기값이 아니라 현재 상태를 봐야 한다 — 사용자가 투명을 끈 뒤
        //   타이틀바를 토글하면 껐던 투명이 되살아난다.
        if (_isTransparent)
            SetTransparent(true);
#endif
    }

    /// <summary>투명 배경 On/Off — DWM 프레임(유리)을 클라이언트 영역 전체로 확장/해제한다.</summary>
    public void SetTransparent(bool enable)
    {
        _isTransparent = enable;
        WindowSettings.SaveBool(WindowSettings.TransparentKey, enable);
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

    /// <summary>항상 위 On/Off — Z순서를 HWND_TOPMOST/HWND_NOTOPMOST 로 바꾼다(위치·크기는 유지).</summary>
    public void SetTopmost(bool enable)
    {
        _isTopmost            = enable; // 창 이동·리사이즈가 Z순서를 유지하도록 상태 저장
        _topmostReassertTimer = 0f;     // 방금 확정했으니 재확정 주기를 처음부터
        WindowSettings.SaveBool(WindowSettings.TopmostKey, enable);
#if !UNITY_EDITOR
        // hWndInsertAfter 에 HWND_TOPMOST/HWND_NOTOPMOST 를 주어 Z순서만 바꾼다.
        //   SWP_NOMOVE|SWP_NOSIZE 로 위치·크기는 건드리지 않는다.
        IntPtr insertAfter = enable ? Win32Native.HWND_TOPMOST : Win32Native.HWND_NOTOPMOST;
        uint   flags       = Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE | Win32Native.SWP_SHOWWINDOW;
        Win32Native.SetWindowPos(_hWnd, insertAfter, 0, 0, 0, 0, flags);
#endif
    }

    /// <summary>
    /// 항상 위를 다시 확정한다 — Z순서만 topmost 밴드 맨 앞으로 되돌린다(작업표시줄에 가려지지 않게).
    /// 위치·크기·포커스는 건드리지 않는다('SWP_NOACTIVATE' 로 활성 창을 뺏지 않음).
    /// Update 의 주기 재확정과 포커스 상실 시 호출된다.
    /// </summary>
    private void ReassertTopmost()
    {
#if !UNITY_EDITOR
        if (_hWnd == IntPtr.Zero)
            return;

        uint flags = Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE | Win32Native.SWP_NOACTIVATE;
        Win32Native.SetWindowPos(_hWnd, Win32Native.HWND_TOPMOST, 0, 0, 0, 0, flags);
#endif
    }

    /// <summary>
    /// 포커스를 잃는 순간(다른 창이 앞으로 옴) 항상 위면 즉시 재확정한다 — 주기(0.5초)를 기다리지 않고
    /// 바로 작업표시줄·다른 창 위로 되돌린다. 'NOACTIVATE' 라 포커스를 도로 뺏지는 않는다. (Unity 메시지)
    /// </summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && _isTopmost && _initialized)
            ReassertTopmost();
    }

    /// <summary>
    /// 클릭 스루 On/Off — WS_EX_TRANSPARENT 로 빈 영역 입력을 뒤 창으로 통과시킨다.
    /// ⚠️ 저장하지 않는다. 동적 클릭 스루가 켜져 있으면 Update가 커서 위치에 따라
    /// 매 프레임 이 값을 뒤집는다 — 저장할 "사용자 설정"이 아니라 순간 상태다.
    /// </summary>
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

    /// <summary>동적 클릭 스루(마우스 위치 자동 판정)를 켜고 끈다.(디버그 패널 토글에서 호출)</summary>
    public void SetDynamicClickThrough(bool value)
    {
        _dynamicClickThrough = value;
        WindowSettings.SaveBool(WindowSettings.DynamicClickThroughKey, value);
    }

    #endregion

    #region 위치 · 크기

    /// <summary>크기 드롭다운 옵션 라벨("1x"/"1.25x"/"1.5x"/"2x")을 WindowScale enum 순서대로 만든다.</summary>
    public List<string> GetSizeLabels() => new List<string>(SizeLabels);

    /// <summary>위치 드롭다운 옵션 라벨(6칸)을 ScreenAnchor enum 순서대로 만든다.</summary>
    public List<string> GetAnchorLabels() => new List<string>
    {
        "Upper Left", "Upper Center", "Upper Right",
        "Lower Left", "Lower Center", "Lower Right",
    };

    /// <summary>
    /// 크기 프리셋을 인덱스로 적용한다(드롭다운 onValueChanged / 시작 초기화에서 호출).
    /// 크기·위치를 하나의 기준 모니터로 원자 적용한다('ApplySizeAndPosition').
    /// ⚠️ 'Screen.SetResolution'과 캔버스 기준 해상도 변경을 쓰지 않는다 —
    /// 둘 다 창 제어를 망가뜨린다 ('Managers 규칙.md' 5장).
    /// </summary>
    public void SetWindowSizeByIndex(int index)
    {
        _currentScale = (WindowScale)Mathf.Clamp(index, 0, ScaleFactors.Length - 1);
        WindowSettings.SaveInt(WindowSettings.ScaleKey, (int)_currentScale);
#if !UNITY_EDITOR
        ApplySizeAndPosition(_currentScale, _currentAnchor);
#endif
    }

    /// <summary>
    /// 위치 앵커를 인덱스로 적용한다(드롭다운 onValueChanged에서 호출).
    /// 크기·위치를 함께 재적용한다 — 위치만 따로 옮기면 이전 크기 적용이 남긴 외곽 오차가
    /// 그대로 위치에 실려 어긋날 수 있어, 매번 같은 기준으로 원자 적용한다.
    /// </summary>
    public void SetAnchorByIndex(int index)
    {
        // 마지막 앵커(LowerRight)를 상한으로 클램프 — 매직 넘버(8) 대신 enum 값 사용
        _currentAnchor = (ScreenAnchor)Mathf.Clamp(index, 0, (int)ScreenAnchor.LowerRight);
        WindowSettings.SaveInt(WindowSettings.AnchorKey, (int)_currentAnchor);
#if !UNITY_EDITOR
        ApplySizeAndPosition(_currentScale, _currentAnchor);
#endif
    }

#if !UNITY_EDITOR
    /// <summary>
    /// 크기와 위치를 <b>하나의 기준 모니터·단일 SetWindowPos</b>로 원자 적용한다(A-1 근본 수정).
    ///
    /// 왜 통합했나 — 예전엔 리사이즈(SWP_NOMOVE)와 이동(SWP_NOSIZE)을 따로 불렀는데,
    ///   ① 리사이즈가 좌상단을 고정한 채 창을 키우면 그 과도 상태에서 'MonitorFromWindow' 판정이
    ///      다른 모니터로 넘어가 클램프·앵커가 서로 다른 작업 영역을 기준으로 계산됐고,
    ///   ② 위치 계산이 외곽 크기를 <b>다시 추정</b>해, 리사이즈의 실측 보정(dx/dy)과 어긋나
    ///      오른쪽·아래 앵커가 프레임 두께만큼 넘쳤다.
    /// → 작업 영역을 <b>한 번만</b> 고정하고, 실측 보정 시 <b>위치까지 같은 외곽으로 재계산</b>한다.
    ///
    /// ⚠️ 'SetWindowPos'는 클라이언트가 아니라 외곽 크기를 받는다 — 그대로 넘기면 렌더 영역이
    /// 프레임 두께만큼 줄어 16:9가 깨진다('Managers 규칙.md' 5장). 'ClientSizeToOuterSize'로 부풀린다.
    /// </summary>
    private void ApplySizeAndPosition(WindowScale scale, ScreenAnchor anchor)
    {
        // ① 기준 모니터를 한 번만 고정 — 크기 클램프와 앵커 계산이 같은 작업 영역(wa)을 쓰게 해
        //    리사이즈 도중 모니터 판정이 바뀌는 것을 막는다. 실패하면 조정을 건너뛴다.
        if (!TryGetWorkArea(out Win32Native.RECT wa))
            return;

        IntPtr after = _isTopmost ? Win32Native.HWND_TOPMOST : Win32Native.HWND_NOTOPMOST;
        uint   flags = Win32Native.SWP_NOACTIVATE | Win32Native.SWP_SHOWWINDOW; // 이동+크기 동시(NOMOVE/NOSIZE 없음)

        // ② 렌더 크기를 기준 작업 영역에 맞춰 16:9 유지 클램프 → ③ 외곽 크기·앵커 좌표 계산 → 원자 적용.
        Vector2Int client = ClampToWorkArea(BaseSize(scale), wa);
        Vector2Int outer  = ClientSizeToOuterSize(client.x, client.y);
        Vector2Int pos    = AnchorPosition(anchor, outer, wa);
        Win32Native.SetWindowPos(_hWnd, after, pos.x, pos.y, outer.x, outer.y, flags);

        // ④ 실측 보정 — 프레임 두께 추정이 빗나가는 환경(DPI 가상화 등)에서도 렌더 영역이 정확히
        //    client가 되도록 외곽 크기를 실측 차이만큼 보정하고, 앵커 좌표도 보정된 외곽으로 재계산한다.
        //    크기와 위치가 항상 같은 외곽 값을 공유하므로 오른쪽·아래 앵커가 어긋나지 않는다.
        //    한 번만 돌므로 반복 보정으로 진동할 일은 없다.
        if (Win32Native.GetClientRect(_hWnd, out Win32Native.RECT actual))
        {
            int dx = client.x - (actual.right  - actual.left);
            int dy = client.y - (actual.bottom - actual.top);

            if (dx != 0 || dy != 0)
            {
                outer = new Vector2Int(outer.x + dx, outer.y + dy);
                pos   = AnchorPosition(anchor, outer, wa);
                Win32Native.SetWindowPos(_hWnd, after, pos.x, pos.y, outer.x, outer.y, flags);
            }
        }
    }

    /// <summary>
    /// 9분할 앵커의 <b>외곽</b> 좌상단 좌표를 작업 영역(wa)과 외곽 크기로 계산한다.
    /// ⚠️ 클라이언트가 아니라 외곽 크기로 계산한다 — 'SetWindowPos'가 옮기는 게 외곽 사각형이라,
    /// 클라이언트 크기로 계산하면 타이틀바가 켜졌을 때 오른쪽·아래 앵커가 프레임 두께만큼 넘친다.
    /// 창이 작업 영역보다 커도 좌상단(타이틀바)은 보이도록 안쪽으로 클램프한다.
    /// </summary>
    private static Vector2Int AnchorPosition(ScreenAnchor anchor, Vector2Int outer, Win32Native.RECT wa)
    {
        int waW = wa.right - wa.left;

        int hi = (int)anchor % 3; // 0=Left 1=Center 2=Right
        int vi = (int)anchor / 3; // 0=Upper 1=Lower

        int x = hi == 0 ? wa.left : hi == 1 ? wa.left + (waW - outer.x) / 2 : wa.right - outer.x;
        int y = vi == 0 ? wa.top  : wa.bottom - outer.y;

        return new Vector2Int(Mathf.Max(wa.left, x), Mathf.Max(wa.top, y));
    }
#endif

#if !UNITY_EDITOR
    /// <summary>
    /// 원하는 클라이언트 크기를, 그 크기를 얻는 데 필요한 외곽 크기로 바꾼다 ('ApplySizeAndPosition'에서 호출).
    /// 현재 스타일과 창이 놓인 모니터의 DPI를 OS에 그대로 물어보므로,
    /// 타이틀바 On/Off·배율 100/125/150% 어디서든 렌더 영역이 정확히 요청한 값이 된다.
    /// 보더리스(WS_POPUP)면 프레임이 없어 결과가 입력과 같다.
    /// </summary>
    private Vector2Int ClientSizeToOuterSize(int width, int height)
    {
        var rect = new Win32Native.RECT { left = 0, top = 0, right = width, bottom = height };

        uint style   = Win32Native.GetWindowLong(_hWnd, Win32Native.GWL_STYLE);
        uint exStyle = Win32Native.GetWindowLong(_hWnd, Win32Native.GWL_EXSTYLE);

        try
        {
            // Windows 10 1607+ : 모니터별 DPI를 반영해 프레임 두께를 계산한다.
            uint dpi = Win32Native.GetDpiForWindow(_hWnd);
            if (dpi == 0)
                dpi = 96; // 조회 실패 — 100% 로 간주

            if (!Win32Native.AdjustWindowRectExForDpi(ref rect, style, false, exStyle, dpi))
                return new Vector2Int(width, height);
        }
        catch (EntryPointNotFoundException)
        {
            // 구형 Windows 폴백 — 주 모니터 DPI 기준이라 배율이 섞인 환경에선 약간 어긋날 수 있다.
            if (!Win32Native.AdjustWindowRectEx(ref rect, style, false, exStyle))
                return new Vector2Int(width, height);
        }

        return new Vector2Int(rect.right - rect.left, rect.bottom - rect.top);
    }
#endif

    /// <summary>
    /// 배율 프리셋의 실제 렌더(클라이언트) 픽셀 크기를 구한다. 기준 960x540(16:9)에 배율을 곱한 절대
    /// 픽셀이며, 모니터 해상도에 비례시키지 않는다(WindowScale 주석 참조). 작업 영역 클램프는 'ClampToWorkArea'가 따로 한다.
    /// </summary>
    private static Vector2Int BaseSize(WindowScale scale)
    {
        float factor = ScaleFactors[(int)scale];
        return new Vector2Int(Mathf.RoundToInt(BaseWidth * factor), Mathf.RoundToInt(BaseHeight * factor));
    }

    /// <summary>
    /// 창이 작업 영역(작업표시줄 제외)을 넘으면 16:9를 유지한 채 안으로 줄인다. 이미 들어가면 그대로 돌려준다.
    /// 가로·세로 중 더 많이 넘치는 쪽 비율 하나로 양쪽을 함께 줄여야 비율이 보존된다
    /// — 축마다 따로 상한을 걸면 UI가 찌그러진다. 기준 작업 영역(wa)은 호출부가 한 번 고정해 넘긴다.
    /// </summary>
    private static Vector2Int ClampToWorkArea(Vector2Int size, Win32Native.RECT wa)
    {
        int waW = wa.right  - wa.left;
        int waH = wa.bottom - wa.top;

        if (waW <= 0 || waH <= 0)
            return size; // 작업 영역이 비정상 — 줄이지 않는다

        float fitRatio = Mathf.Min(1f, (float)waW / size.x, (float)waH / size.y);

        return new Vector2Int(Mathf.RoundToInt(size.x * fitRatio), Mathf.RoundToInt(size.y * fitRatio));
    }

#if !UNITY_EDITOR
    /// <summary>
    /// 창이 <b>실제로 올라가 있는 모니터</b>의 작업 영역 사각형을 얻는다.
    ///
    /// ⚠️ 'SPI_GETWORKAREA'는 <b>주 모니터</b>의 값만 돌려준다 — 듀얼 모니터에서 창을 보조
    /// 모니터로 옮기면 위치·클램프 계산이 전부 어긋난다. 그래서 'MonitorFromWindow'로
    /// 현재 모니터를 먼저 찾고, 그게 실패할 때만 'SPI_GETWORKAREA'로 폴백한다.
    /// </summary>
    private bool TryGetWorkArea(out Win32Native.RECT workArea)
    {
        IntPtr monitor = Win32Native.MonitorFromWindow(_hWnd, Win32Native.MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            // cbSize 를 구조체 크기로 채워야 호출이 성공한다 — Win32 규약.
            var info = new Win32Native.MONITORINFO { cbSize = Marshal.SizeOf(typeof(Win32Native.MONITORINFO)) };
            if (Win32Native.GetMonitorInfo(monitor, ref info))
            {
                workArea = info.rcWork;
                return true;
            }
        }

        workArea = new Win32Native.RECT();
        return Win32Native.SystemParametersInfo(Win32Native.SPI_GETWORKAREA, 0, ref workArea, 0);
    }
#endif

    #endregion

    #region 창 이동 · 종료

    /// <summary>
    /// 캔버스(타이틀바 역할)를 잡아 끌 때 창 이동을 시작한다('WindowDragArea'의 드래그 시작에서 호출).
    /// 직접 좌표를 옮기지 않고 OS 이동 루프에 위임한다 — 'ReleaseCapture'로 캡처를 푼 뒤
    /// 'WM_SYSCOMMAND(SC_MOVE_HTCAPTION)'을 보내면 "타이틀바를 잡은 것"처럼 동작해
    /// 스냅·모니터 간 이동을 OS가 그대로 처리한다(보더리스라도 됨).
    /// ⚠️ 이 호출 동안 OS 모달 이동 루프가 돌아 마우스를 놓을 때까지 Unity가 잠깐 멈춘다(정상).
    /// </summary>
    public void BeginWindowDrag()
    {
#if !UNITY_EDITOR
        if (_hWnd == IntPtr.Zero)
            return;

        Win32Native.ReleaseCapture();
        Win32Native.SendMessage(_hWnd, Win32Native.WM_SYSCOMMAND, Win32Native.SC_MOVE_HTCAPTION, 0);
#endif
    }

    /// <summary>
    /// 앱을 종료한다(종료 버튼·ESC에서 호출). 보더리스라 창 'X'가 없어 명시적 출구가 필요하다.
    /// 에디터에서는 Application.Quit이 동작하지 않으므로 플레이 모드를 멈춘다.
    /// </summary>
    public void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region 콘텐츠 위 판정 (클릭 스루용)

    // ★ 클릭 스루 ON 상태에선 OS가 마우스 메시지를 창에 안 보내므로,
    //   Unity의 Mouse.current / EventSystem.IsPointerOverGameObject 는 동작하지 않는다.
    //   → Win32 GetCursorPos 로 전역 커서를 직접 폴링해서 판정한다.

    /// <summary>마우스가 콘텐츠(uGUI UI / 2D 스프라이트) 위에 있는지 판정한다. (Update 의 동적 클릭스루에서 호출)</summary>
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
        // 에디터 / 폴백. 마우스가 없는 환경(원격·터치 전용)이면 Mouse.current 가 null 이라 0을 돌려준다.
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
    }

    #endregion
}
