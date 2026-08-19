using MikaNetwork;
using MikaProtocol;
using UnityEngine;

/// <summary>
/// 서버와의 연결이 살아 있는지를 주기적으로 확인한다 (Ping/Pong).
/// 5초마다 보내고 15초 넘게 무응답이면 로그로 알린다 — 소켓을 끊지는 않는다.
/// </summary>
/// <remarks>
/// 하트비트가 필요한 이유(TCP는 끊김을 즉시 알려주지 않는다)와
/// ⚠️ Ping만 매니저가 송신하는 예외인 근거는 'Managers 규칙.md' 2장 참조.
///
/// ★ 핑은 <b>로그인 전, 연결 시점부터</b> 보낸다. 서버는 유휴 세션(마지막 수신 후 일정 시간
///   아무 바이트도 못 받은 세션)을 로그인 여부와 무관하게 끊는데, 로그인 전에 아무것도 안 보내면
///   그 사이 연결만 되어 있어도 유휴로 판정돼 끊긴다. 서버의 Ping 핸들러는 로그인 없이도 Pong을
///   돌려주므로(<c>Handle_C_PingRequest</c>), 연결 즉시 핑을 시작해 소켓을 살려 둔다.
/// </remarks>
public class PingManager : MonoService<PingManager>
{
    // Ping 주기. 짧을수록 끊김을 빨리 알지만 그만큼 패킷이 늘어난다.
    private const float PingIntervalSeconds = 5f;

    // 이 시간을 넘겨 Pong이 없으면 끊긴 것으로 본다.
    // ★ 서버가 같은 판정을 넣으면 이 값이 곧 "부당하게 적립되는 최대 채취 시간"이 된다.
    //   채취 1주기(30초)보다 짧게 잡아 손실 구간이 생기지 않게 했다.
    private const float ResponseTimeoutSeconds = 15f;

    // ─── 참조 캐시 ───
    private NetworkManager    _network = null!; // 없으면 게임이 성립하지 않는다
    private ServerWaitManager _wait    = null!; // 치명 오류 알림 창구(연결 끊김 → 종료)

    // ─── 내부 상태 ───
    private float _nextPingTime;
    private float _lastPongTime;
    private bool  _isRunning;
    private bool  _isTimedOut;   // 무응답 로그를 1회만 남기기 위한 상태 (매 프레임 도배 방지)
    private bool  _everConnected; // Pong을 한 번이라도 받았는가 — 최초 접속 실패와 도중 끊김을 가른다
    private bool  _isSubscribed;
    private bool  _isReady;      // Start 완료 여부 — OnEnable 재구독 가드

    /// <summary>
    /// 서버가 하트비트에 응답하고 있는가.
    /// ⏸ 아직 읽는 곳이 없다 — 연결 상태를 표시하는 위젯이 붙을 자리다.
    /// </summary>
    public bool IsServerResponding => !_isTimedOut;

    // ─── Unity 메시지 ───

    // 참조 확보 → 구독 → 하트비트 시작 순서로 진행한다 (매니저 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        CacheReferences();
        Subscribe();
        BeginHeartbeat();
        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 + 재기동 (Unity 메시지)
    private void OnEnable()
    {
        if (!_isReady)
            return;

        Subscribe();
        BeginHeartbeat(); // 꺼져 있던 동안 무응답으로 오판하지 않도록 기준 시각을 다시 지금으로 잡는다
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
        _isRunning = false;
    }

    // 하트비트 주기 검사 (Unity 메시지)
    private void Update()
    {
        if (!_isRunning)
            return;

        // 타임스케일을 0으로 만들어도(일시정지 연출 등) 연결은 계속 살아 있어야 한다.
        float now = Time.unscaledTime;

        if (now >= _nextPingTime)
        {
            // 연결 전(Session이 아직 null)에는 안전하게 무시되고, 연결되면 그때부터 실제로 나간다.
            _network.Send(new C_PingRequest());
            _nextPingTime = now + PingIntervalSeconds;
        }

        WarnIfSilentTooLong(now);
    }

    #region 초기화

    // 다른 서비스를 확보해 캐시한다 (Start에서 호출)
    private void CacheReferences()
    {
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();
    }

    // Pong 수신 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed = true;

        ServerPacketHandler.PongReceived += OnPongReceived;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed = false;

        ServerPacketHandler.PongReceived -= OnPongReceived;
    }

    #endregion

    #region 하트비트 진행

    // 하트비트를 시작(또는 재기동)한다 (Start · OnEnable에서 호출)
    // 로그인 전이라도 연결만 되면 핑을 보내야 서버의 유휴 세션 정리에 끊기지 않는다.
    private void BeginHeartbeat()
    {
        float now = Time.unscaledTime;

        _lastPongTime = now; // 시작하자마자 무응답으로 판정되지 않도록 기준을 지금으로 잡는다
        _nextPingTime = now;
        _isTimedOut   = false;
        _isRunning    = true;

        ClientLogger.Info(ClientLogger.Network,
            $"하트비트 시작 — {PingIntervalSeconds:F0}초마다 확인, {ResponseTimeoutSeconds:F0}초 무응답이면 경고");
    }

    // Pong 도착 — 마지막 응답 시각을 갱신한다 (ServerPacketHandler.PongReceived 구독)
    // 평시엔 로그를 남기지 않는다. 5초마다 찍으면 정작 봐야 할 로그가 밀려난다.
    private void OnPongReceived()
    {
        float now = Time.unscaledTime;

        // 끊겼다고 알렸던 연결이 돌아왔을 때만 말한다 — 상태가 바뀐 순간이 유일하게 알릴 가치가 있다.
        if (_isTimedOut)
        {
            ClientLogger.Info(ClientLogger.Network, $"서버 응답 복구 — {now - _lastPongTime:F0}초 만에 돌아왔다");
            _isTimedOut = false;
        }

        _everConnected = true; // 한 번이라도 응답을 받았다 — 이후 무응답은 "도중 끊김"이다
        _lastPongTime  = now;
    }

    // 무응답이 판정 시간을 넘겼는지 검사한다 (Update에서 호출)
    private void WarnIfSilentTooLong(float now)
    {
        if (_isTimedOut)
            return;

        float silentSeconds = now - _lastPongTime;
        if (silentSeconds < ResponseTimeoutSeconds)
            return;

        _isTimedOut = true;

        ClientLogger.Error(ClientLogger.Network,
            $"서버 무응답 {silentSeconds:F0}초 — 연결이 끊긴 것으로 본다. " +
            $"지금 화면의 인벤토리·슬롯은 서버 상태와 다를 수 있다(재접속 필요).");

        // 치명 알림을 띄운다 — 사용자가 확인하면 앱이 종료된다(에디터에서는 플레이가 멈춘다).
        // 한 번이라도 연결됐었는지로 "도중 끊김"과 "최초 접속 실패"를 갈라 문구를 고른다.
        _wait.RaiseFatal(_everConnected
            ? "서버와의 연결이 끊어졌습니다. 앱을 종료합니다."
            : "서버에 접속하지 못했습니다. 서버 상태를 확인한 뒤 다시 실행해 주세요.");
    }

    #endregion
}
