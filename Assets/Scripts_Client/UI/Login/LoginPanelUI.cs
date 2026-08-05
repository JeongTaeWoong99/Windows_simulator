using System.Collections;
using MikaNetwork;
using MikaProtocol;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그인 요청을 보내는 패널.
///
/// <para>
/// ■ 이 한 번이 전부다<br/>
/// 로그인 요청 하나면 서버가 인벤토리·작업슬롯·캐릭터·재화를 연달아 밀어준다.
/// 그것들을 따로 요청할 패킷은 서버에 없다 — 수신 세트는 <see cref="PlayerDataManager"/> 주석 참조.
/// </para>
///
/// <para>
/// ■ 무응답 감시가 여기 있는 이유<br/>
/// <b>서버는 실패해도 응답을 안 보내는 경우가 있다.</b> 같은 Id가 이미 접속 중이면
/// (끊겼는데 서버가 아직 모르는 좀비 세션 포함) 응답도 로그도 없이 요청을 버린다
/// — 깃허브 이슈 #10, <c>UserManager.CreateUser</c>의 pid 중복 분기.
/// 그러면 클라 화면에서는 <b>아무 일도 일어나지 않은 것</b>과 구분되지 않는다.
/// "응답이 없다"를 사용자에게 알릴 주체가 로그인 화면이므로 감시도 여기서 돈다.
/// </para>
/// </summary>
public class LoginPanelUI : MonoBehaviour
{
    // 로그인 응답을 이만큼 기다려 본다. 넘기면 경고를 남긴다.
    private const float ResponseTimeoutSeconds = 5f;

    [CenterHeader("< 참조 >")]
    [SerializeField, Tooltip("로그인 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button loginButton = null!;

    [CenterHeader("< 설정 >")]
    [SerializeField, Tooltip("로그인에 사용할 계정 Id. 입력창이 생기기 전까지 쓰는 고정값")]
    private string loginId = "test";

    private PlayerDataManager _data    = null!;
    private NetworkManager    _network = null!;

    // 진행 중인 무응답 감시. 응답이 오거나 다시 로그인하면 취소한다.
    private Coroutine? _timeoutWatch;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(loginButton, nameof(loginButton));

        _data    = Services.Get<PlayerDataManager>();
        _network = NetworkManager.Instance;

        Subscribe();

        loginButton.onClick.AddListener(OnLoginButtonClicked);

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
            Subscribe();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
        StopTimeoutWatch(); // 꺼진 오브젝트의 코루틴은 Unity가 멈추므로 핸들만 버린다
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

    // 로그인 요청 (loginButton OnClick에 코드로 연결)
    private void OnLoginButtonClicked()
    {
        // 서버가 닉네임을 돌려주지 않으므로 보낸 Id를 표시용으로 넘겨 둔다.
        // 수신 전담 매니저는 무엇을 보냈는지 모르기 때문에 보낸 쪽이 알려 줘야 한다.
        _data.SetLoginId(loginId);

        _network.Send(new C_LoginRequest { Id = loginId });
        ClientLogger.Info(ClientLogger.Send, $"로그인 요청 — Id={loginId}");

        StartTimeoutWatch();
    }

    #endregion

    #region 무응답 감시

    // 감시 시작 — 이전 감시가 있으면 갈아탄다 (로그인 요청 시 호출)
    private void StartTimeoutWatch()
    {
        StopTimeoutWatch();
        _timeoutWatch = StartCoroutine(WatchResponse());
    }

    // 감시 중단 (응답 도착·재요청·비활성화 시 호출)
    private void StopTimeoutWatch()
    {
        if (_timeoutWatch == null)
            return;

        StopCoroutine(_timeoutWatch);
        _timeoutWatch = null;
    }

    // 제한 시간까지 응답이 없으면 경고를 남긴다 (StartTimeoutWatch가 시작)
    private IEnumerator WatchResponse()
    {
        yield return new WaitForSecondsRealtime(ResponseTimeoutSeconds);

        _timeoutWatch = null;

        ClientLogger.Warn(ClientLogger.Network,
            $"로그인 응답이 {ResponseTimeoutSeconds:F0}초 동안 없다 (Id={loginId}). " +
            $"서버가 안 떠 있거나, 같은 Id가 이미 접속 중일 수 있다(서버 pid 중복 — 이슈 #10).");
    }

    // 응답이 왔으니 감시를 끝낸다 (PlayerDataManager.LoginCompleted 구독)
    private void OnLoginCompleted(bool success)
    {
        StopTimeoutWatch();
    }

    #endregion
}
