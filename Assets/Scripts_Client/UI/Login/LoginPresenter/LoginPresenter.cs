using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그인 요청을 보내는 패널. 서버는 'C_LoginRequest'에 Id 하나만 받으므로
/// (비밀번호도 계정 DB도 아직 없다) 입력창도 하나뿐이고 검사는 비었는가가 전부다.
/// 이 요청 하나면 인벤토리·슬롯·캐릭터·재화가 연달아 따라온다 — '패킷 레퍼런스.md' 참조.
///
/// ■ 무응답·실패는 'ServerWaitManager'에 맡긴다
/// 서버는 실패해도 응답을 안 보내는 경우가 있다. 같은 Id가 이미 접속 중이면
/// (끊겼는데 서버가 아직 모르는 좀비 세션 포함) 응답도 로그도 없이 요청을 버린다
/// — 깃허브 이슈 #10, 'UserManager.CreateUser'의 pid 중복 분기.
/// 그러면 클라 화면에서는 아무 일도 일어나지 않은 것과 구분되지 않는다.
/// 이 "응답이 없다"의 감시·알림은 'ServerWaitManager'가 공통으로 처리한다 — 여기서는
/// 요청 직후 대기를 열고('Begin'), 응답 이벤트에서 결과만 보고한다('Succeed'/'Fail').
///
/// ⚠️ 화면을 닫는 시점은 "버튼을 누른 때"가 아니라 "성공 응답이 온 때"다.
/// 누르자마자 닫으면 위의 무응답 상황에서 아무것도 없는 화면에 갇혀 원인을 알 수 없다.
/// </summary>
public class LoginPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("아이디 입력창. 엔터로도 로그인되도록 코드가 연결한다")]
    private TMP_InputField idInput = null!;

    [SerializeField, Tooltip("로그인 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button loginButton = null!;

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

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(idInput,     nameof(idInput));
        this.RequireRef(loginButton, nameof(loginButton));

        _data    = Services.Get<PlayerDataModel>();
        _network = NetworkManager.Instance;
        _ui      = Services.Get<UIManager>();
        _wait    = Services.Get<ServerWaitManager>();

        Subscribe();

        loginButton.onClick.AddListener(OnLoginButtonClicked);

        // 입력창에서 엔터를 쳐도 눌린 것으로 친다. 아이디 한 줄짜리 화면이라 마우스로 옮겨 갈 이유가 없다.
        idInput.onSubmit.AddListener(_ => OnLoginButtonClicked());

        // 빈 아이디로는 보낼 수 없으므로 버튼을 잠가 둔다. 입력이 생기면 열린다.
        idInput.onValueChanged.AddListener(_ => RefreshButton());
        RefreshButton();

        _isReady = true;
    }

    // 보낼 수 있을 때만 버튼을 연다 (Start · 입력 변경 시 호출)
    // ※ 요청을 보낸 뒤에는 대기가 끝날 때까지 잠가 둔다 — 연타로 중복 요청이 나가면
    //   서버가 같은 Id의 두 번째 접속을 응답 없이 버려(이슈 #10) 스스로 무응답을 만든다.
    private void RefreshButton()
    {
        loginButton.interactable = !_isWaiting && !string.IsNullOrWhiteSpace(idInput.text);
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (!_isReady)
            return;

        Subscribe();
        RefreshButton(); // 꺼져 있는 동안 감시가 끊겼을 수 있다 — 입력 상태로 다시 맞춘다
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
            return;

        _isSubscribed         = true;
        _data.LoginCompleted += OnLoginCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

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
            return;

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
        _waitHandle = _wait.Begin("로그인", onClosed: OnWaitClosed);
        RefreshButton(); // 응답을 기다리는 동안 잠근다
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

        // 실패 사유를 사람이 읽을 문구로 옮겨 알림에 띄운다(콘솔 로그는 PlayerDataLogger가 별도로 남긴다).
        _waitHandle?.Fail(ResultMessages.ToText(code));
    }

    #endregion
}
