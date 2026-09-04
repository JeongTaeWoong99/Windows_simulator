using System;
using UnityEngine;

// '#Main Canvas' 안에서 같은 자리를 나눠 쓰는 화면들.
// 'Title'과 'Menu Presenter' 사이의 한 칸을 이 중 하나가 차지한다.
//
// ⚠️ 값을 중간에 끼우지 않는다 — 씬에 int로 저장돼 있어 순서가 밀리면
// 'Main Screens'·'Screen Buttons' 배선이 조용히 어긋난다(컴파일도 경고도 통과한다).
// 끝에 추가하거나, 순서를 바꿨으면 두 배열을 전수 확인한다.
// 이 enum이 seam인 이유와 화면 추가 절차는 'Main 규칙.md'의 "메인 화면 추가" 절 참조.
public enum MainScreen
{
    // 작업슬롯 목록 — 기본 화면. 다른 화면을 닫으면 언제나 여기로 돌아온다.
    WorkStationList,

    // 작업슬롯 한 칸의 배치·해제. 목록에서 칸을 눌러야 들어온다(버튼으로 직접 열지 않는다).
    WorkStationSelect,

    // 설정 — 창 제어와 위젯 위치.
    Setting,
}

// 화면 골격의 단일 출입구 — 3열 + 위젯을 참조로 들고, 무엇을 열고 닫을지 결정한다.
// 두 축을 다룬다: 캔버스 여닫기(#Main·#State·#Storage·#Market·!Login)와
// 메인 화면 전환(#Main Canvas 안의 목록↔선택↔설정, 하나만 남긴다).
//
// ⚠️ 캔버스를 켜지 않는다 — 켜면 로그인 전에 게임 화면이 비친다.
// 안쪽 메인 화면만 기본값으로 맞춰 두면 나중에 캔버스가 켜지는 순간 이미 올바른 화면이 떠 있다.
// 이 클래스가 Presenter가 아닌 이유는 'UI 규칙.md'의 "세 역할 + 조정자",
// 전환 흐름 · 진입 순서는 'Main 규칙.md'의 "전환 층은 하나다",
// 화면 동선 기획은 'GameDesign/design/ui/README.md' 2.0 참조.
public class UIManager : MonoService<UIManager>
{
    // 아무 조작도 없을 때의 메인 화면. 시작할 때 · 전부 접었다 열 때 · 같은 버튼을 다시 누를 때
    // 모두 여기로 돌아온다. 세 곳이 같은 값을 봐야 해서 상수로 둔다.
    private const MainScreen DefaultMainScreen = MainScreen.WorkStationList;

    // '#Main Canvas'의 한 자리를 차지하는 화면 하나. 인스펙터에서 짝지어 넣는다.
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

    // ※ '!System Canvas' 자체는 여기 들지 않는다 — 상주라 여닫을 일이 없다('SystemCanvasView').
    //   여기 드는 것은 캔버스가 아니라 **답을 돌려줘야 해서 중개가 필요한 팝업 하나**다.
    //   로딩·알림·가챠 결과는 매니저 이벤트를 스스로 구독해 뜨므로 참조가 필요 없다.
    [CenterHeader("시스템 오버레이 — 중개가 필요한 것만")]
    [SerializeField, Tooltip("수량 입력 팝업. !System Canvas 아래에 있다 — 화면 전체를 막아야 해서다")]
    private AmountInputPresenter amountInput = null!;

    // ─── 참조 ───
    public StorageCanvasView Storage => storageCanvas;
    public MarketCanvasView  Market  => marketCanvas;
    public WidgetCanvasView  Widget  => widgetCanvas;

    // 지금 '#Main Canvas'에 떠 있는 화면. 전부 닫힌 상태에서도 "다음에 열면 이것"을 뜻한다
    // — 그래서 'CloseAllExceptWidget'이 이 값을 기본으로 되돌린다.
    public MainScreen CurrentMainScreen { get; private set; } = DefaultMainScreen;

    // 위젯 말고 하나라도 열려 있는가. 'ToggleAll'의 방향을 정한다.
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
        this.RequireRef(amountInput,   nameof(amountInput));

        ValidateMainScreens();
        ResetMainScreen();
    }

    #region 로그인 — 게임의 시작점

    // 로그인 열을 열고 닫는다 ('LoginPresenter'가 로그인 성공 응답을 받고 부른다).
    //
    // ⚠️ 버튼을 누른 시점이 아니라 성공 응답이 온 시점에 닫는다. 서버는 같은 Id가 이미
    // 접속 중이면 응답도 로그도 없이 요청을 버린다(이슈 #10). 누르자마자 닫으면 그때
    // 아무것도 없는 화면에 갇혀 원인을 알 수 없다.
    public void ShowLogin(bool on) => loginCanvas.Show(on);

    #endregion

    #region 메인 화면

    // 인스펙터 배선이 'MainScreen'과 맞는지 본다 (Start에서 한 번).
    //
    // 빠진 화면은 조용히 안 열린다 — 버튼을 눌러도 아무 일이 없어서 버튼이 고장 난 것처럼 보인다.
    // 원인이 인스펙터라는 걸 드러내려고 여기서 먼저 알린다.
    private void ValidateMainScreens()
    {
        foreach (MainScreen screen in Enum.GetValues(typeof(MainScreen)))
        {
            int count = 0;

            foreach (var entry in mainScreens)
            {
                if (entry.screen == screen && entry.panel != null)
                {
                    count++;
                }
            }

            if (count != 1)
            {
                ClientLogger.Error(ClientLogger.UI,
                    $"메인 화면 '{screen}'의 패널이 {count}개다 (정확히 1개여야 한다). " +
                    $"UI Manager의 Main Screens를 확인할 것.", this);
            }
        }
    }

    // 메인 화면을 기본값 하나만 켜진 상태로 되돌린다 (Start · 'CloseAllExceptWidget').
    // ★ 캔버스는 건드리지 않는다 — 여기서 켜면 로그인 전에 게임 화면이 비친다.
    // 씬에 무엇이 켜진 채 저장됐든 무시하고 언제나 'DefaultMainScreen'으로 간다
    // (근거는 'Main 규칙.md'의 "전환 층은 하나다").
    private void ResetMainScreen()
    {
        CurrentMainScreen = DefaultMainScreen;

        foreach (var entry in mainScreens)
        {
            if (entry.panel == null)
            {
                continue;
            }

            bool on = entry.screen == DefaultMainScreen;
            entry.panel.SetActive(on);

            // 제목도 함께 맞춘다 — 씬에 저장된 문구가 실제로 켜진 화면과 다를 수 있다
            if (on)
            {
                mainCanvas.SetTitle(entry.title);
            }
        }
    }

    // 그 화면만 켜고 나머지 메인 화면은 끈다. 메인 캔버스가 꺼져 있었으면 함께 켜고,
    // 캔버스 머리의 제목도 그 화면 것으로 바꾼다.
    //
    // ※ 꺼져 있던 패널에 넘길 값이 있으면 이걸 부르기 전에 넣는다
    // ('WorkStationListPresenter'가 'Open(slotIndex)'를 먼저 부르는 이유).
    // 꺼진 오브젝트는 'Start()'가 아직 안 돌았을 수 있어, 켠 뒤에 넣으면 초기화가 덮어쓴다.
    public void ShowMainScreen(MainScreen screen)
    {
        CurrentMainScreen = screen;
        mainCanvas.Show(true);

        foreach (var entry in mainScreens)
        {
            if (entry.panel == null)
            {
                continue;
            }

            bool on = entry.screen == screen;
            entry.panel.SetActive(on);

            if (on)
            {
                mainCanvas.SetTitle(entry.title);
            }
        }
    }

    // 상태 패널의 화면 버튼이 부른다 — 이미 그 화면이면 기본(작업슬롯 목록)으로 되돌린다.
    //
    // 여는 일만 하면 이미 열려 있을 때 눌러도 변화가 없어 버튼이 고장 난 것처럼 보인다.
    // 닫는 버튼을 따로 두지 않아도 되는 것은 'ToggleStorage'와 같은 이유다.
    public void ToggleMainScreen(MainScreen screen)
        => ShowMainScreen(CurrentMainScreen == screen ? DefaultMainScreen : screen);

    #endregion

    #region 전체 여닫기

    // 위젯의 열기/닫기 버튼이 부른다 — 열려 있으면 전부 접고, 닫혀 있으면 작업슬롯을 연다.
    // 진입 순서(위젯 → 작업슬롯 → 창고·거래)의 되돌아오는 길이라, 어느 단계에서 눌러도 한 번에 접힌다.
    public void ToggleAll()
    {
        if (IsOpen)
        {
            CloseAllExceptWidget();
        }
        else
        {
            OpenWorkStation();
        }
    }

    // 작업슬롯 목록과 상태 캔버스를 연다. 창고·거래는 작업슬롯의 하단 버튼으로 연다.
    public void OpenWorkStation()
    {
        ShowMainScreen(DefaultMainScreen);
        stateCanvas.Show(true);
    }

    // 위젯을 뺀 전부를 닫는다. 위젯은 상주가 존재 이유라 건드리지 않는다.
    //
    // ★ 패널만 끄는 게 아니라 '#Main Canvas'까지 끈다. 캔버스를 켜 둔 채 두면
    // 'LayoutElement'가 열 안에서 900px를 계속 차지해 위젯이 창 가장자리에서 밀린다.
    //
    // ★ 메인 화면도 기본으로 되돌린다 — 이유는 'Main 규칙.md'의 "전환 층은 하나다".
    public void CloseAllExceptWidget()
    {
        storageCanvas.Show(false);
        marketCanvas.Show(false);
        stateCanvas.Show(false);
        mainCanvas.Show(false);

        // ★ 팝업은 상주 캔버스에 살아 'OnDisable'이 오지 않는다 — 여기서 안 닫으면
        //   아무 화면도 없는 바탕에 홀로 떠 있게 되고, 그때 들고 있던 콜백은 이미 의미가 없다.
        amountInput.Close();

        ResetMainScreen(); // 캔버스를 끈 뒤라 안쪽을 정리해도 화면에는 아무 변화가 없다
    }

    #endregion

    #region 좌우 열

    // 창고 열을 열고 닫는다.
    public void ShowStorage(bool on) => storageCanvas.Show(on);

    // 거래 열을 열고 닫는다.
    public void ShowMarket(bool on) => marketCanvas.Show(on);

    // 창고 열을 뒤집는다 (작업슬롯 하단 버튼).
    // 토글이어야 하는 이유 — 여는 일만 하면 이미 열려 있을 때 눌러도 아무 변화가 없어
    // 버튼이 고장 난 것처럼 보인다. 닫는 버튼을 따로 두지 않아도 된다.
    public void ToggleStorage() => ShowStorage(!storageCanvas.gameObject.activeSelf);

    // 거래 열을 뒤집는다 (작업슬롯 하단 버튼).
    public void ToggleMarket() => ShowMarket(!marketCanvas.gameObject.activeSelf);

    #endregion

    #region 시스템 오버레이 중개

    // 수량을 묻고, 확인을 누르면 그 수를 'onConfirm'으로 돌려준다 (취소면 부르지 않는다).
    //
    // ★ 왜 부르는 쪽이 팝업을 직접 알지 않는가
    //   팝업은 '!System Canvas'에 산다 — 화면 전체를 막아야 해서다(열 캔버스는 Sorting Order가
    //   전부 0인 형제라 열 안의 차단막이 다른 열에 닿지 않는다). 그런데 그렇게 두면 창고 격자가
    //   **캔버스를 넘어 남의 패널을 인스펙터로 붙드는** 모양이 된다. 그 참조를 여기로 모은다.
    //
    // ※ 로딩·알림·가챠 결과에는 이런 중개가 없다 — 매니저 이벤트를 스스로 구독해 뜨는 단방향이라서다.
    //   이 팝업만 **답을 돌려주는 왕복**이라 구독형으로 만들 수 없다('System 규칙.md').
    public void AskAmount(int itemId, int maxCount, Action<int> onConfirm)
    {
        amountInput.Open(itemId, maxCount, onConfirm);
    }

    #endregion
}
