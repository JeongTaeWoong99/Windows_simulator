using MikaNetwork;
using MikaProtocol;
using UnityEngine;

/// <summary>
/// 서버와의 연결이 <b>살아 있는지</b>를 주기적으로 확인한다 (하트비트).
///
/// <para>
/// ■ 왜 필요한가<br/>
/// TCP는 끊김을 즉시 알려주지 않는다. 소켓이 FIN/RST 없이 사라지는 종료
/// (유니티 플레이 중지·PC 절전·랜선 분리)에서는 <b>양쪽 다 상대가 죽은 걸 모른다.</b>
/// 서버는 그동안 접속 중으로 보고 채취를 계속 굴리고(깃허브 이슈 #10 — 좀비 유저),
/// 클라는 화면이 멀쩡해 보이는데 아무것도 반영되지 않는다.
/// </para>
///
/// <para>
/// ■ 이 클래스가 하는 것 / 하지 않는 것<br/>
/// 5초마다 Ping을 보내고 Pong이 돌아오는지 본다. 15초 넘게 무응답이면 <b>로그로 알린다.</b><br/>
/// 소켓을 끊지는 않는다 — 세션은 서버 담당 코드(<c>MikaClient</c>)의 것이라 여기서 손대지 않는다.
/// <b>좀비 세션을 실제로 정리하는 것은 서버 몫</b>이고(이슈 #10의 서버 파트), 이건 그 절반이다.
/// </para>
/// </summary>
public class HeartbeatManager : MonoService<HeartbeatManager>
{
    // Ping 주기. 짧을수록 끊김을 빨리 알지만 그만큼 패킷이 늘어난다.
    private const float PingIntervalSeconds = 5f;

    // 이 시간을 넘겨 Pong이 없으면 끊긴 것으로 본다.
    // ★ 서버가 같은 판정을 넣으면 이 값이 곧 "부당하게 적립되는 최대 채취 시간"이 된다.
    //   채취 1주기(30초)보다 짧게 잡아 손실 구간이 생기지 않게 했다.
    private const float ResponseTimeoutSeconds = 15f;

    private SessionManager _sessionManager = null!; // Start에서 주입 — 없으면 게임이 성립하지 않는다

    private float _nextPingTime;
    private float _lastPongTime;
    private bool  _isRunning;
    private bool  _isTimedOut; // 무응답 로그를 1회만 남기기 위한 상태 (매 프레임 도배 방지)

    /// <summary>서버가 하트비트에 응답하고 있는가. 연결 상태를 표시하는 UI가 볼 수 있다.</summary>
    public bool IsServerResponding => !_isTimedOut;

    // 수신 이벤트 구독 (Unity 메시지)
    private void OnEnable()
    {
        ServerPacketHandler.PongReceived += OnPongReceived;
    }

    // 서비스 조회 + 로그인 완료 구독 (Unity 메시지)
    // ※ 조회는 Start에서 한다 — MonoService 주석의 초기화 순서 규칙 참조.
    private void Start()
    {
        _sessionManager = Services.Get<SessionManager>();
        _sessionManager.LoginCompleted += OnLoginCompleted;
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
            NetworkManager.Instance.Send(new C_PingRequest());
            _nextPingTime = now + PingIntervalSeconds;
        }

        WarnIfSilentTooLong(now);
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        ServerPacketHandler.PongReceived -= OnPongReceived;

        if (_sessionManager != null)
            _sessionManager.LoginCompleted -= OnLoginCompleted;

        _isRunning = false;
    }

    #region 하트비트 진행

    // 로그인 결과 도착 — 성공했을 때만 하트비트를 시작한다 (SessionManager.LoginCompleted 구독)
    // 로그인 전에는 서버에 User가 없어 감시할 대상 자체가 없다.
    private void OnLoginCompleted(bool isSuccess)
    {
        if (!isSuccess)
            return;

        float now = Time.unscaledTime;

        _lastPongTime = now; // 시작하자마자 무응답으로 판정되지 않도록 기준을 지금으로 잡는다
        _nextPingTime = now;
        _isTimedOut   = false;
        _isRunning    = true;

        ClientLog.Info(ClientLog.Network,
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
            ClientLog.Info(ClientLog.Network, $"서버 응답 복구 — {now - _lastPongTime:F0}초 만에 돌아왔다");
            _isTimedOut = false;
        }

        _lastPongTime = now;
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

        ClientLog.Error(ClientLog.Network,
            $"서버 무응답 {silentSeconds:F0}초 — 연결이 끊긴 것으로 본다. " +
            $"지금 화면의 인벤토리·슬롯은 서버 상태와 다를 수 있다(재접속 필요).");
    }

    #endregion
}
