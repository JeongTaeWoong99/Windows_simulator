using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 로그인 요청을 보내는 패널. 서버는 'C_LoginRequest'에 Id 하나만 받으므로
// (비밀번호도 계정 DB도 아직 없다) 입력창도 하나뿐이고 검사는 비었는가가 전부다.
// 이 요청 하나면 인벤토리·슬롯·캐릭터·재화가 연달아 따라온다 — '패킷 레퍼런스.md' 참조.
//
// ■ 무응답·실패는 'ServerWaitManager'에 맡긴다
// 서버는 실패해도 응답을 안 보내는 경우가 있다. 같은 Id가 이미 접속 중이면
// (끊겼는데 서버가 아직 모르는 좀비 세션 포함) 응답도 로그도 없이 요청을 버린다
// — 깃허브 이슈 #10, 'UserManager.CreateUser'의 pid 중복 분기.
// 그러면 클라 화면에서는 아무 일도 일어나지 않은 것과 구분되지 않는다.
// 이 "응답이 없다"의 감시·알림은 'ServerWaitManager'가 공통으로 처리한다 — 여기서는
// 요청 직후 대기를 열고('Begin'), 응답 이벤트에서 결과만 보고한다('Succeed'/'Fail').
//
// ⚠️ 화면을 닫는 시점은 "버튼을 누른 때"가 아니라 "성공 응답이 온 때"다.
// 누르자마자 닫으면 위의 무응답 상황에서 아무것도 없는 화면에 갇혀 원인을 알 수 없다.
//
// ■ 두 모드 — Id(에디터) · 스팀(빌드) (T-211 · 이슈 #144 · 2026-11-24 사용자)
//   에디터는 지금처럼 Id 입력 · [로그인] · [종료]. 빌드는 Id 칸을 숨기고 [접속하기] · [종료] 둘만 둔다 —
//   [접속하기]가 스팀 티켓으로 로그인한다. 같은 버튼을 쓰고 글씨 키만 바꾼다(프리팹을 늘리지 않는다).
//   에디터에서 스팀 화면을 보려면 치트 창 도구 줄 [스팀 화면]을 켜고 플레이한다(에디터 설정 'SteamModePrefKey'). 계정은 서버가 'steam:<SteamID>'로 이어 준다.
//
// ■ 처음 뜰 때 검은 판 위로 상자가 서서히 나타난다 (2026-11-24 사용자)
//   상자를 Awake에서 투명하게 두고 모드(Id 칸 숨김)를 먼저 정한 뒤 'FadeInPanel'로 연다 —
//   스팀 모드에서 Id 칸 · [로그인]이 한 프레임 보였다가 바뀌던 깜빡임을 없앤다.
//
// ■ '!Overlay Canvas'의 첫 형제 — 이 오브젝트가 화면 전체를 덮는 어두운 판이고, 상자는 자식 'Panel'이다.
// 오버레이의 다른 팝업과 달리 'CanvasGroup'이 아니라 'SetActive'로 여닫는다('Show') —
// 스스로 이벤트를 받아 뜨는 팝업이 아니라 'UIManager'가 여닫는 화면이라서다('Overlay 규칙.md').
public class LoginPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("아이디 입력창. 엔터로도 로그인되도록 코드가 연결한다")]
    private TMP_InputField idInput = null!;

    [SerializeField, Tooltip("로그인 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다. 스팀 모드에서는 [접속하기]가 된다")]
    private Button loginButton = null!;

    [SerializeField, Tooltip("로그인 버튼 글씨의 번역 칸 — 스팀 모드에서 키를 'UI.Login.Connect'로 바꾼다")]
    private LocalizedText loginButtonLabel = null!;

    // ※ 화면 안의 버튼이 아니라 앱을 끄는 버튼이라 헤더로 가른다('StatePresenter'와 같은 규칙).
    //   로그인 화면에도 종료 출구가 필요하다 — 이 화면이 떠 있는 동안 상태 패널의 종료 버튼은
    //   가려져 손이 닿지 않고, 타이틀바를 끄면 창 'X'도 없다.
    [CenterHeader("시스템")]
    [SerializeField, Tooltip("누르면 앱을 종료한다. OnClick은 코드가 연결한다")]
    private Button quitButton = null!;

    private PlayerDataModel   _data    = null!;
    private NetworkManager    _network = null!;
    private UIManager         _ui      = null!;
    private ServerWaitManager _wait    = null!;

    // 진행 중인 로그인 대기의 손잡이. 응답이 오면 결과를 보고하고 버린다.
    private ServerWaitHandle? _waitHandle;

    // 응답을 기다리는 중인가 — 버튼 잠금·중복 요청 방지에 쓴다.
    private bool _isWaiting;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    private const string ConnectKey = "UI.Login.Connect";

    private const float PanelFadeDelay    = 0.25f; // 초 — 검은 화면을 잠깐 보여 준 뒤
    private const float PanelFadeDuration = 0.4f;  // 초 — 상자가 나타나는 시간

    private CanvasGroup _panelGroup = null!; // 상자('loginButton'의 부모 Panel) — 실행 중에 붙인다(씬을 고치지 않는다)

    // 티켓이 틀렸다('SteamAuthFailed')는 응답에 새 티켓으로 몇 번 더 해 보나 — 만료된 티켓은 새로 받으면 통과한다(#144).
    private const int SteamRetryCount = 1;

    private int _steamRetryLeft;

    // 에디터에서 스팀 화면을 켜는 설정 이름 — 치트 창 [스팀 화면]이 켜고 끈다. 씬에 두지 않아 커밋에 안 섞인다.
    public const string SteamModePrefKey = "DesktopWindowControl.SteamModeInEditor";

    // 스팀 모드인가 — 빌드는 늘, 에디터는 치트 창 [스팀 화면]을 켰을 때만.
#if UNITY_EDITOR
    private static bool IsSteamMode => UnityEditor.EditorPrefs.GetBool(SteamModePrefKey, false);
#else
    private static bool IsSteamMode => true;
#endif

    // 첫 그림 전에 상자를 숨기고 모드를 정한다 (Unity 메시지) — Start보다 먼저 돌아 깜빡임이 없다
    private void Awake()
    {
        var panel = loginButton.transform.parent.gameObject;

        if (!panel.TryGetComponent(out _panelGroup))
        {
            _panelGroup = panel.AddComponent<CanvasGroup>();
        }

        _panelGroup.alpha = 0f;

        if (IsSteamMode)
        {
            idInput.gameObject.SetActive(false); // [접속하기] · [종료] 둘만 — Id 칸은 스팀 계정이 대신한다
        }
    }

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(idInput,     nameof(idInput));
        this.RequireRef(loginButton, nameof(loginButton));
        this.RequireRef(quitButton,  nameof(quitButton));
        this.RequireRef(loginButtonLabel, nameof(loginButtonLabel));

        _data    = Services.Get<PlayerDataModel>();
        _network = NetworkManager.Instance;
        _ui      = Services.Get<UIManager>();
        _wait    = Services.Get<ServerWaitManager>();

        Subscribe();

        quitButton.onClick.AddListener(Services.Get<WindowManager>().QuitApplication); // 종료는 WindowManager가 단일 경로

        if (IsSteamMode)
        {
            loginButtonLabel.SetKey(ConnectKey); // Id 칸은 Awake에서 이미 숨겼다
            loginButton.onClick.AddListener(OnConnectButtonClicked);
        }
        else
        {
            loginButton.onClick.AddListener(OnLoginButtonClicked);

            // 입력창에서 엔터를 쳐도 눌린 것으로 친다. 아이디 한 줄짜리 화면이라 마우스로 옮겨 갈 이유가 없다.
            // 키는 'KeyInputManager'가 모아 관리한다 — 아웃게임 키라 로그인 뒤에는 돌지 않는다.
            Services.Get<KeyInputManager>().BindOutGameSubmit(idInput, OnLoginButtonClicked);

            // 빈 아이디로는 보낼 수 없으므로 버튼을 잠가 둔다. 입력이 생기면 열린다.
            idInput.onValueChanged.AddListener(_ => RefreshButton());
        }

        RefreshButton();

        FadeInPanel();

        _isReady = true;
    }

    // 검은 판을 잠깐 보인 뒤 상자를 서서히 띄운다 (Start · 다시 열 때)
    private void FadeInPanel()
    {
        _panelGroup.DOKill();
        _panelGroup.alpha = 0f;
        _panelGroup.DOFade(1f, PanelFadeDuration).SetDelay(PanelFadeDelay).SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    // 보낼 수 있을 때만 버튼을 연다 (Start · 입력 변경 시 호출)
    // ※ 요청을 보낸 뒤에는 대기가 끝날 때까지 잠가 둔다 — 연타로 중복 요청이 나가면
    //   서버가 같은 Id의 두 번째 접속을 응답 없이 버려(이슈 #10) 스스로 무응답을 만든다.
    private void RefreshButton()
    {
        loginButton.interactable = !_isWaiting && (IsSteamMode || !string.IsNullOrWhiteSpace(idInput.text));
    }

    // 로그인 화면을 열고 닫는다 ('UIManager.ShowLogin'이 부른다)
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (!_isReady)
        {
            return;
        }

        Subscribe();
        RefreshButton(); // 꺼져 있는 동안 감시가 끊겼을 수 있다 — 입력 상태로 다시 맞춘다
        FadeInPanel();   // 로그아웃 등으로 다시 열 때도 같은 모습으로 나타난다
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 로그인 응답 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed         = true;
        _data.LoginCompleted += OnLoginCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed         = false;
        _data.LoginCompleted -= OnLoginCompleted;
    }

    #endregion

    #region 송신

    // 로그인 요청 (loginButton OnClick · 입력창 엔터에 코드로 연결)
    private void OnLoginButtonClicked()
    {
        // 엔터는 버튼 잠금을 지나쳐 들어오므로 여기서 한 번 더 막는다.
        if (!loginButton.interactable)
        {
            return;
        }

        // 앞뒤 공백은 사용자가 의도한 글자가 아니다. 서버에는 다듬은 값을 보낸다.
        string id = idInput.text.Trim();

        if (id.Length == 0)
        {
            ClientLogger.Warn(ClientLogger.UI, "아이디가 비어 있어 로그인 요청을 보내지 않았다.", this);

            return;
        }

        // 서버가 닉네임을 돌려주지 않으므로 보낸 Id를 표시용으로 넘겨 둔다.
        // 수신 전담 매니저는 무엇을 보냈는지 모르기 때문에 보낸 쪽이 알려 줘야 한다.
        _data.SetLoginId(id);

        _network.Send(new C_LoginRequest { Id = id });
        ClientLogger.Info(ClientLogger.Send, $"로그인 요청 — Id={id}");

        // 대기 시작 — 로딩 표시·무응답 감시·알림은 ServerWaitManager가 공통으로 처리한다.
        // 성공·실패·타임아웃 어느 쪽이든 onClosed(OnWaitClosed)로 잠금이 풀린다.
        _isWaiting  = true;
        _waitHandle = _wait.Begin(Lang.Get("UI.Login.Wait"), onClosed: OnWaitClosed);
        RefreshButton(); // 응답을 기다리는 동안 잠근다
    }

    // [접속하기] (스팀 모드 loginButton OnClick) — 새로 누를 때마다 재시도 횟수를 채운다.
    private void OnConnectButtonClicked()
    {
        _steamRetryLeft = SteamRetryCount;
        LoginWithSteam();
    }

    // 스팀 티켓으로 로그인한다 ([접속하기] · 에디터 메뉴 '스팀 로그인 시험').
    public void LoginWithSteam()
    {
        LoginWithSteamAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid LoginWithSteamAsync(CancellationToken token)
    {
        if (_isWaiting)
        {
            return;
        }

        // 티켓을 받는 동안(최대 10초)에도 로딩을 띄운다 — 버튼이 눌린 건지 알 수 있게.
        _isWaiting  = true;
        _waitHandle = _wait.Begin(Lang.Get("UI.Login.Wait"), onClosed: OnWaitClosed);
        RefreshButton();

        var (ticket, error) = await SteamAuth.RequestTicketHexAsync(token);

        if (ticket == null)
        {
            // 스팀이 꺼져 있으면 초기화에서, 그 밖은 티켓 단계에서 실패한다 — 사람이 할 일은 둘 다 "스팀을 켜고 다시"다.
            ClientLogger.Error(ClientLogger.Send, $"스팀 티켓을 받지 못했다 — {error}", this);
            _waitHandle?.Fail(Lang.Get(SteamAuth.IsInitialized ? "UI.Result.SteamAuthFailed" : "UI.Login.SteamNotRunning"));

            return;
        }

        // 표시 이름은 스팀 닉네임 — 서버도 첫 가입 때 같은 이름을 저장한다.
        _data.SetLoginId(SteamAuth.PersonaName);
        _network.Send(new C_LoginRequest { SteamTicket = ticket });
        ClientLogger.Info(ClientLogger.Send, $"스팀 로그인 요청 — 티켓 {ticket.Length / 2}바이트");
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼을 다시 연다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _isWaiting  = false;
        _waitHandle = null;
        RefreshButton();
    }

    #endregion

    #region 응답 처리

    // 응답이 왔으니 대기에 결과를 보고한다. 성공이면 이 화면을 접는다 (PlayerDataModel.LoginCompleted 구독)
    //
    // ※ 닫는 일은 UIManager가 한다 — 여닫는 곳이 흩어지면 화면이 늘어날 때마다 참조가 얽힌다.
    private void OnLoginCompleted(bool success, EResultCode code)
    {
        if (success)
        {
            _waitHandle?.Succeed();
            _ui.ShowLogin(false);

            return;
        }

        // 티켓이 틀렸다 — 만료일 수 있어 새 티켓으로 한 번 더 해 본다(#144 서버 코멘트의 처리 표).
        // ※ 대기를 먼저 닫아야('Succeed' → 'OnWaitClosed') 다음 시도가 '_isWaiting'에 막히지 않는다.
        if (code == EResultCode.SteamAuthFailed && _steamRetryLeft > 0)
        {
            _steamRetryLeft--;
            ClientLogger.Warn(ClientLogger.Send, "스팀 인증 실패 — 새 티켓으로 다시 시도한다.", this);
            _waitHandle?.Succeed();
            LoginWithSteam();

            return;
        }

        // 실패 사유를 사람이 읽을 문구로 옮겨 알림에 띄운다.
        _waitHandle?.Fail(ResultMessages.ToText(code));
    }

    #endregion
}
