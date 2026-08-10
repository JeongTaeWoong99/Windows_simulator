using System;
using UnityEngine;

/// <summary>
/// <c>#Main Canvas</c> 안에서 <b>같은 자리를 나눠 쓰는 화면들.</b>
/// <c>Title</c>과 <c>Menu Presenter</c> 사이의 한 칸을 이 중 하나가 차지한다.
///
/// <para>
/// ■ 이 enum이 seam이다<br/>
/// 화면을 여는 쪽(<c>StatePresenter</c>의 버튼 · <c>WorkStationListPresenter</c>의 칸 클릭)과
/// 패널을 쥔 쪽(<see cref="UIManager"/>)이 서로의 오브젝트를 모른다. 양쪽 다 이 값만 안다 —
/// 그래서 화면이 늘어도 참조가 얽히지 않는다.
/// </para>
///
/// <para>
/// ■ 전환 층을 하나로 눕혔다 (2026-08-10)<br/>
/// 예전엔 <c>WorkStationPresenter</c>가 자기 안에서 목록↔선택을 갈아 끼우고,
/// <see cref="UIManager"/>가 그 바깥에서 작업슬롯↔설정을 갈아 끼웠다. <b>같은 일을 두 층이 따로 했다.</b>
/// 지금은 세 화면이 형제로 나란히 있고 전환하는 곳도 여기 하나다.
/// </para>
///
/// <para>
/// ■ 화면을 하나 더 붙이려면<br/>
/// 여기에 값을 하나 추가하고, <c>UI Manager</c>의 <c>Main Screens</c>에 (값 · 패널 · 제목) 한 줄을 넣는다.
/// 상태 패널의 버튼으로 열 화면이면 <c>Screen Buttons</c>에도 한 줄. <b>코드는 이 한 줄뿐이다.</b>
/// 새 패널은 <c>#Main Canvas</c>의 자식으로 두고 <c>LayoutElement</c>를
/// <c>preferredHeight 0 · flexibleHeight 1</c>로 준다 — <b>캔버스를 새로 만들지 않는다.</b>
/// </para>
/// </summary>
public enum MainScreen
{
    /// <summary>작업슬롯 목록 — <b>기본 화면</b>. 다른 화면을 닫으면 언제나 여기로 돌아온다.</summary>
    WorkStationList,

    /// <summary>작업슬롯 한 칸의 배치·해제. 목록에서 칸을 눌러야 들어온다(버튼으로 직접 열지 않는다).</summary>
    WorkStationSelect,

    /// <summary>설정 — 창 제어와 위젯 위치.</summary>
    Setting,
}

/// <summary>
/// 화면 골격의 단일 출입구 — 3열 + 위젯을 참조로 들고, 무엇을 열고 닫을지 결정한다.
///
/// <para>
/// ■ 왜 필요한가<br/>
/// 패널을 여는 쪽(위젯의 열기 버튼, 작업슬롯 하단의 창고·거래 버튼, 상태 패널의 화면 버튼)과
/// 열리는 쪽이 서로를 직접 알면 화면이 늘어날 때마다 참조가 그물처럼 얽힌다.
/// <b>여는 결정은 여기 하나로 모은다.</b>
/// </para>
///
/// <para>
/// ■ 두 개의 축을 다룬다<br/>
/// <code>
/// 캔버스 여닫기   #Main / #State / #Storage / #Market / !Login          켜고 끈다
/// 메인 화면 전환  #Main Canvas 안의 패널들 (목록 ↔ 선택 ↔ 설정)         하나만 남기고 끈다
/// </code>
/// 좌우 열(창고·거래)은 <b>자리를 안 뺏으므로</b> 메인 화면 전환에 끼지 않는다 — 각자 토글이다.
/// </para>
///
/// <para>
/// ■ 캔버스는 씬 그대로, 메인 화면은 기본값으로<br/>
/// <b>캔버스를 켜지 않는다</b> — 여기서 켜면 씬을 열어 본 모습과 재생했을 때가 달라지고,
/// 무엇보다 로그인 전에 게임 화면이 비친다.
/// <b>반면 <c>#Main Canvas</c> 안의 메인 화면은 시작할 때 언제나 기본값으로 맞춘다</b>
/// (<see cref="ResetMainScreen"/>) — 셋 중 하나만 켜져 있어야 한다는 불변식이 있고,
/// 씬에 무엇이 켜진 채 저장됐든 게임을 시작하면 작업슬롯 목록부터여야 하기 때문이다.
/// 캔버스가 꺼져 있으면 안쪽을 정리해도 화면에는 아무 변화가 없다.
/// </para>
///
/// <para>
/// ■ 진입 순서 (기획 2.0)<br/>
/// <code>
/// 위젯 [열기/닫기] ──→ #Main Canvas(작업슬롯) + 상태 캔버스
///                        ├─ 상태 패널 버튼 ──→ 설정 (작업슬롯과 자리를 바꾼다)
///                        └─ 하단 버튼 ──→ 창고 / 거래 (좌우 열이라 자리를 안 뺏는다)
/// 위젯 [열기/닫기] 다시 ──→ 위젯만 남기고 전부 닫힘
/// </code>
/// 창고·거래는 <b>위젯에서 직접 열지 않는다.</b> 작업슬롯을 거쳐야 재료→작업→시장 동선이 유지된다.
/// → GameDesign/design/ui/README.md 2.0
/// </para>
/// </summary>
public class UIManager : MonoService<UIManager>
{
    /// <summary>
    /// 아무 조작도 없을 때의 메인 화면. <b>시작할 때 · 전부 접었다 열 때 · 같은 버튼을 다시 누를 때</b>
    /// 모두 여기로 돌아온다. 세 곳이 같은 값을 봐야 해서 상수로 둔다.
    /// </summary>
    private const MainScreen DefaultMainScreen = MainScreen.WorkStationList;

    /// <summary><c>#Main Canvas</c>의 한 자리를 차지하는 화면 하나. 인스펙터에서 짝지어 넣는다.</summary>
    [Serializable]
    private struct MainScreenEntry
    {
        [Tooltip("이 줄이 어느 화면인가 — 상태 패널의 버튼이 같은 값을 들고 있다")]
        public MainScreen screen;

        [Tooltip("그 화면의 Presenter 오브젝트 (WorkStation List Presenter · Setting Presenter …)")]
        public GameObject panel;

        // ※ 제목을 enum이 아니라 여기에 두는 이유 — 문구는 표시용이라 바뀌어도 로직이 안 바뀐다.
        //   코드에 박으면 문구 하나 고치는 데 컴파일이 필요하고, 화면 목록과 제목 목록이 따로 놀 수 있다.
        [Tooltip("이 화면일 때 #Main Canvas 머리(Title)에 뜨는 문구")]
        public string title;
    }

    // ※ 로그인은 3열·위젯과 다른 축이다 — 게임에 들어오기 전까지 이것만 보이고, 성공하면 다시 안 나온다.
    //   그래서 ToggleAll·CloseAllExceptWidget의 대상에 넣지 않는다.
    //   ★ 게임이 여기서 시작하므로 맨 위에 둔다.
    [CenterHeader("로그인 캔버스 — 가장 먼저 보이는 화면")]
    [SerializeField, Tooltip("로그인 열. 다른 UI보다 앞에 오도록 Override Sorting을 켜고 Sorting Order를 크게 준다")]
    private LoginCanvasView loginCanvas = null!;

    // ※ 메인 화면만 GameObject 배열로 받는다 — 여기 들어오는 패널은 여닫히기만 하면 되고,
    //   공통 타입을 강요하면 화면을 하나 붙일 때마다 상속·인터페이스를 먼저 손봐야 한다.
    [CenterHeader("메인 화면 — #Main Canvas 의 같은 자리")]
    [SerializeField, Tooltip("#Main Canvas 의 본체. 전부 닫을 때 이것까지 끈다 — 켜 두면 빈 칸이 열 높이를 먹는다")]
    private MainCanvasView mainCanvas = null!;

    [SerializeField, Tooltip("서로 자리를 바꾸는 패널들. MainScreen 값마다 정확히 한 줄씩 넣는다")]
    private MainScreenEntry[] mainScreens = new MainScreenEntry[0];

    // ※ 여닫는 대상은 전부 Canvas다 — Column이 아니다.
    //   Column을 끄면 남은 열들이 Horizental Columns 안에서 가운데로 다시 몰려 위젯 가로 칸이 어긋난다.
    //   Canvas만 끄면 Column 3개와 (Layout) 스페이서가 남아 폭이 그대로라 위젯이 제자리에 있는다.
    [CenterHeader("좌우 열 캔버스")]
    [SerializeField, Tooltip("창고 열의 본체 — @Storage Column 안의 #Storage Canvas")]
    private StorageCanvasView storageCanvas = null!;

    [SerializeField, Tooltip("거래 열의 본체 — @Market Column 안의 #Market Canvas")]
    private MarketCanvasView marketCanvas = null!;

    [CenterHeader("사이드 2개 캔버스")]
    [SerializeField, Tooltip("상태 캔버스 — 위젯의 반대편. 메인 캔버스와 함께 여닫힌다")]
    private StateCanvasView stateCanvas = null!;

    [SerializeField, Tooltip("바탕화면에 항상 떠 있는 위젯. 여닫지 않고 참조만 들고 있는다")]
    private WidgetCanvasView widgetCanvas = null!;

    // ─── 참조 ───
    public StorageCanvasView Storage => storageCanvas;
    public MarketCanvasView  Market  => marketCanvas;
    public WidgetCanvasView  Widget  => widgetCanvas;

    /// <summary>
    /// 지금 <c>#Main Canvas</c>에 떠 있는 화면. 전부 닫힌 상태에서도 <b>"다음에 열면 이것"</b>을 뜻한다
    /// — 그래서 <see cref="CloseAllExceptWidget"/>이 이 값을 기본으로 되돌린다.
    /// </summary>
    public MainScreen CurrentMainScreen { get; private set; } = DefaultMainScreen;

    /// <summary>위젯 말고 하나라도 열려 있는가. <see cref="ToggleAll"/>의 방향을 정한다.</summary>
    public bool IsOpen =>
        mainCanvas.gameObject.activeSelf     ||
        stateCanvas.gameObject.activeSelf    ||
        storageCanvas.gameObject.activeSelf  ||
        marketCanvas.gameObject.activeSelf;

    // 필수 참조 검증 → 메인 화면 불변식 정리 (Unity 메시지)
    // ※ 이 매니저는 다른 서비스를 조회하지 않는다 — 인스펙터 참조만 쓰므로 확보 단계가 없다.
    private void Start()
    {
        this.RequireRef(loginCanvas,   nameof(loginCanvas));
        this.RequireRef(mainCanvas,    nameof(mainCanvas));
        this.RequireRef(storageCanvas, nameof(storageCanvas));
        this.RequireRef(stateCanvas,   nameof(stateCanvas));
        this.RequireRef(marketCanvas,  nameof(marketCanvas));
        this.RequireRef(widgetCanvas,  nameof(widgetCanvas));

        ValidateMainScreens();
        ResetMainScreen();
    }

    #region 로그인 — 게임의 시작점

    /// <summary>
    /// 로그인 열을 열고 닫는다 (<c>LoginPresenter</c>가 로그인 성공 응답을 받고 부른다).
    ///
    /// <para>
    /// ⚠️ <b>버튼을 누른 시점이 아니라 성공 응답이 온 시점에 닫는다.</b> 서버는 같은 Id가 이미
    /// 접속 중이면 응답도 로그도 없이 요청을 버린다(이슈 #10). 누르자마자 닫으면 그때
    /// 아무것도 없는 화면에 갇혀 원인을 알 수 없다.
    /// </para>
    /// </summary>
    public void ShowLogin(bool on) => loginCanvas.Show(on);

    #endregion

    #region 메인 화면

    /// <summary>
    /// 인스펙터 배선이 <see cref="MainScreen"/>과 맞는지 본다 (Start에서 한 번).
    ///
    /// <para>
    /// <b>빠진 화면은 조용히 안 열린다</b> — 버튼을 눌러도 아무 일이 없어서 버튼이 고장 난 것처럼 보인다.
    /// 원인이 인스펙터라는 걸 드러내려고 여기서 먼저 알린다.
    /// </para>
    /// </summary>
    private void ValidateMainScreens()
    {
        foreach (MainScreen screen in Enum.GetValues(typeof(MainScreen)))
        {
            int count = 0;
            foreach (var entry in mainScreens)
            {
                if (entry.screen == screen && entry.panel != null)
                    count++;
            }

            if (count != 1)
            {
                ClientLogger.Error(ClientLogger.UI,
                    $"메인 화면 '{screen}'의 패널이 {count}개다 (정확히 1개여야 한다). " +
                    $"UI Manager의 Main Screens를 확인할 것.", this);
            }
        }
    }

    /// <summary>
    /// 메인 화면을 기본값 하나만 켜진 상태로 되돌린다 (Start · <see cref="CloseAllExceptWidget"/>).
    ///
    /// <para>
    /// ★ <b>캔버스는 건드리지 않는다.</b> 여기서 <c>mainCanvas.Show(true)</c>를 하면
    /// 로그인 전에 게임 화면이 비친다. 안쪽만 정리해 두면 나중에 캔버스가 켜지는 순간
    /// 이미 올바른 화면이 떠 있다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>씬에 무엇이 켜진 채 저장됐든 무시한다.</b> 예전엔 "켜진 것 중 첫 번째만 남긴다"였는데,
    /// 그러면 작업하다 설정을 켜 둔 채 저장한 씬이 <b>설정 화면으로 시작한다.</b>
    /// 게임의 시작은 언제나 <see cref="DefaultMainScreen"/>이다.
    /// </para>
    /// </summary>
    private void ResetMainScreen()
    {
        CurrentMainScreen = DefaultMainScreen;

        foreach (var entry in mainScreens)
        {
            if (entry.panel == null)
                continue;

            bool on = entry.screen == DefaultMainScreen;
            entry.panel.SetActive(on);

            // 제목도 함께 맞춘다 — 씬에 저장된 문구가 실제로 켜진 화면과 다를 수 있다
            if (on)
                mainCanvas.SetTitle(entry.title);
        }
    }

    /// <summary>
    /// 그 화면만 켜고 나머지 메인 화면은 끈다. <b>메인 캔버스가 꺼져 있었으면 함께 켜고,
    /// 캔버스 머리의 제목도 그 화면 것으로 바꾼다.</b>
    ///
    /// <para>
    /// ※ 꺼져 있던 패널에 넘길 값이 있으면 <b>이걸 부르기 전에 넣는다</b>
    /// (<c>WorkStationListPresenter</c>가 <c>Open(slotIndex)</c>를 먼저 부르는 이유).
    /// 꺼진 오브젝트는 <c>Start()</c>가 아직 안 돌았을 수 있어, 켠 뒤에 넣으면 초기화가 덮어쓴다.
    /// </para>
    /// </summary>
    public void ShowMainScreen(MainScreen screen)
    {
        CurrentMainScreen = screen;
        mainCanvas.Show(true);

        foreach (var entry in mainScreens)
        {
            if (entry.panel == null)
                continue;

            bool on = entry.screen == screen;
            entry.panel.SetActive(on);

            if (on)
                mainCanvas.SetTitle(entry.title);
        }
    }

    /// <summary>
    /// 상태 패널의 화면 버튼이 부른다 — <b>이미 그 화면이면 기본(작업슬롯 목록)으로 되돌린다.</b>
    ///
    /// <para>
    /// 여는 일만 하면 이미 열려 있을 때 눌러도 변화가 없어 버튼이 고장 난 것처럼 보인다.
    /// 닫는 버튼을 따로 두지 않아도 되는 것은 <see cref="ToggleStorage"/>와 같은 이유다.
    /// </para>
    /// </summary>
    public void ToggleMainScreen(MainScreen screen)
        => ShowMainScreen(CurrentMainScreen == screen ? DefaultMainScreen : screen);

    #endregion

    #region 전체 여닫기

    /// <summary>
    /// 위젯의 열기/닫기 버튼이 부른다 — <b>열려 있으면 전부 접고, 닫혀 있으면 작업슬롯을 연다.</b>
    /// 진입 순서(위젯 → 작업슬롯 → 창고·거래)의 되돌아오는 길이라, 어느 단계에서 눌러도 한 번에 접힌다.
    /// </summary>
    public void ToggleAll()
    {
        if (IsOpen)
            CloseAllExceptWidget();
        else
            OpenWorkStation();
    }

    /// <summary>작업슬롯 목록과 상태 캔버스를 연다. 창고·거래는 작업슬롯의 하단 버튼으로 연다.</summary>
    public void OpenWorkStation()
    {
        ShowMainScreen(DefaultMainScreen);
        stateCanvas.Show(true);
    }

    /// <summary>
    /// 위젯을 뺀 전부를 닫는다. <b>위젯은 상주가 존재 이유라 건드리지 않는다.</b>
    ///
    /// <para>
    /// ★ <b>패널만 끄는 게 아니라 <c>#Main Canvas</c>까지 끈다.</b> 캔버스를 켜 둔 채 두면
    /// <c>LayoutElement</c>가 열 안에서 900px를 계속 차지해 <b>위젯이 창 가장자리에서 밀린다.</b>
    /// </para>
    ///
    /// <para>
    /// ★ 메인 화면을 <b>기본으로 되돌린다.</b> 설정이나 슬롯 선택 화면을 켜 둔 채 접었다가
    /// 다시 열었을 때 그게 튀어나오면, 위젯 버튼이 "게임을 연다"가 아니라
    /// "마지막에 보던 걸 연다"가 되어 버린다.
    /// </para>
    /// </summary>
    public void CloseAllExceptWidget()
    {
        storageCanvas.Show(false);
        marketCanvas.Show(false);
        stateCanvas.Show(false);
        mainCanvas.Show(false);

        ResetMainScreen(); // 캔버스를 끈 뒤라 안쪽을 정리해도 화면에는 아무 변화가 없다
    }

    #endregion

    #region 좌우 열

    /// <summary>창고 열을 열고 닫는다.</summary>
    public void ShowStorage(bool on) => storageCanvas.Show(on);

    /// <summary>거래 열을 열고 닫는다.</summary>
    public void ShowMarket(bool on) => marketCanvas.Show(on);

    /// <summary>
    /// 창고 열을 뒤집는다 (작업슬롯 하단 버튼).
    /// <b>토글이어야 하는 이유</b> — 여는 일만 하면 이미 열려 있을 때 눌러도 아무 변화가 없어
    /// 버튼이 고장 난 것처럼 보인다. 닫는 버튼을 따로 두지 않아도 된다.
    /// </summary>
    public void ToggleStorage() => ShowStorage(!storageCanvas.gameObject.activeSelf);

    /// <summary>거래 열을 뒤집는다 (작업슬롯 하단 버튼).</summary>
    public void ToggleMarket() => ShowMarket(!marketCanvas.gameObject.activeSelf);

    #endregion
}
