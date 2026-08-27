using MikaNetwork;
using MikaProtocol;
using UnityEngine;

// 서버와의 연결이 살아 있는지를 주기적으로 확인한다 (Ping/Pong).
// 2초마다 보내고 15초 넘게 무응답이면 로그를 남긴 뒤 'ServerWaitManager.RaiseFatal'로
// 치명 알림을 띄운다 — 사용자가 확인하면 앱이 종료된다. 소켓 자체를 끊지는 않는다.
//
// 하트비트가 필요한 이유(TCP는 끊김을 즉시 알려주지 않는다)와
// ⚠️ Ping만 매니저가 송신하는 예외인 근거는 'Managers 규칙.md' 2장 참조.
//
// ★ 핑은 로그인 전, 연결되는 순간부터 보낸다. 서버는 유휴 세션(마지막 수신 후 일정 시간
//   아무 바이트도 못 받은 세션)을 로그인 여부와 무관하게 끊는데, 로그인 전에 아무것도 안 보내면
//   그 사이 연결만 되어 있어도 유휴로 판정돼 끊긴다. 서버의 Ping 핸들러는 로그인 없이도 Pong을
//   돌려주므로('Handle_C_PingRequest'), 연결 즉시 핑을 시작해 소켓을 살려 둔다.
//
// ⚠️ 송신은 'Update'가 아니라 백그라운드 타이머가 한다 — 아래 '송신 타이머' 구역 참조.
public class PingManager : MonoService<PingManager>
{
    // Ping 주기. 짧을수록 끊김을 빨리 알지만 그만큼 패킷이 늘어난다.
    //
    // ★ 이 값은 취향이 아니라 제약이다 — 서버 무응답 판정('Global.SessionIdleTimeout')의
    //   절반 이하로 둔다. 한 번 놓쳐도 살아남는 여유가 그것뿐이다.
    //   판정과 주기를 같은 값(둘 다 5초)으로 두었더니 여유가 0이 되어, 프레임 오버슈트 몇 ms
    //   때문에 멀쩡한 세션이 수십 분에 한 번씩 무작위로 끊겼다 (2026-08-28).
    private const float PingIntervalSeconds = 2f;

    // 이 시간을 넘겨 Pong이 없으면 끊긴 것으로 본다.
    // ★ 서버가 같은 판정을 넣으면 이 값이 곧 "부당하게 적립되는 최대 채취 시간"이 된다.
    //   채취 1주기(30초)보다 짧게 잡아 손실 구간이 생기지 않게 했다.
    private const float ResponseTimeoutSeconds = 15f;

    // 프레임이 이만큼 넘게 끊겼으면 "메인 루프가 멈춰 있었다"로 보고 판정을 건너뛴다.
    // 창 드래그(OS 모달 이동 루프)·절전·긴 GC 뒤의 첫 프레임에서 오탐 알림을 막는 값이다.
    private const float FrameStallSeconds = 1f;

    // ─── 참조 캐시 ───
    private NetworkManager    _network = null!; // 없으면 게임이 성립하지 않는다
    private ServerWaitManager _wait    = null!; // 치명 오류 알림 창구(연결 끊김 → 종료)

    // ─── 내부 상태 ───
    private System.Threading.Timer? _pingTimer;  // 송신 전용. 메인 루프가 멈춰도 계속 돈다
    private volatile string?        _timerError; // 타이머 스레드의 예외 — 메인 스레드가 대신 로그한다

    private double _lastPongTime;
    private double _lastUpdateTime; // 직전 Update 시각 — 메인 루프 정지 구간을 알아내는 데 쓴다
    private bool   _isRunning;
    private bool   _isTimedOut;    // 무응답 로그를 1회만 남기기 위한 상태 (매 프레임 도배 방지)
    private bool   _everConnected; // Pong을 한 번이라도 받았는가 — 최초 접속 실패와 도중 끊김을 가른다
    private bool   _isSubscribed;
    private bool   _isReady;       // Start 완료 여부 — OnEnable 재구독 가드

    // 서버가 하트비트에 응답하고 있는가.
    // ⏸ 아직 읽는 곳이 없다 — 연결 상태를 표시하는 위젯이 붙을 자리다.
    public bool IsServerResponding => !_isTimedOut;

    // 단조 시계(초). 타이머 스레드와 메인 스레드가 같은 시각을 읽어야 해서 'Time.unscaledTime'을
    // 쓸 수 없다 — Unity 시간 API는 메인 스레드 전용이다. 'DateTime.UtcNow'도 아니다.
    // 시스템 시각이 보정되면 판정이 함께 흔들린다.
    private static double NowSeconds =>
        (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;

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
        {
            return;
        }

        Subscribe();
        BeginHeartbeat(); // 꺼져 있던 동안 무응답으로 오판하지 않도록 기준 시각을 다시 지금으로 잡는다
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
        StopPingTimer();
        _isRunning = false;
    }

    // 타이머 정리 (Unity 메시지 — 이어서 MonoService가 서비스 등록을 해제한다)
    // ★ 이게 없으면 에디터에서 플레이를 멈춰도 타이머 스레드가 살아남아 도메인 리로드 너머까지 간다.
    protected override void OnDestroy()
    {
        StopPingTimer();

        base.OnDestroy();
    }

    // 무응답 검사 (Unity 메시지). 송신은 여기서 하지 않는다 — 타이머의 몫이다.
    private void Update()
    {
        if (!_isRunning)
        {
            return;
        }

        LogTimerErrorIfAny();

        double now      = NowSeconds;
        double frameGap = now - _lastUpdateTime;

        _lastUpdateTime = now;

        // 메인 루프가 멈춰 있었다면 그 구간은 "서버가 응답하지 않은 시간"이 아니다.
        // 판정을 건너뛰고 기준을 지금으로 다시 잡는다 — 진짜 끊김이면 다음 판정 시간 안에 다시 잡힌다.
        if (frameGap >= FrameStallSeconds)
        {
            _lastPongTime = now;

            return;
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
        {
            return;
        }

        _isSubscribed = true;

        ServerPacketHandler.PongReceived += OnPongReceived;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        ServerPacketHandler.PongReceived -= OnPongReceived;
    }

    #endregion

    #region 하트비트 진행

    // 하트비트를 시작(또는 재기동)한다 (Start · OnEnable에서 호출)
    // 로그인 전이라도 연결만 되면 핑을 보내야 서버의 유휴 세션 정리에 끊기지 않는다.
    private void BeginHeartbeat()
    {
        double now = NowSeconds;

        _lastPongTime   = now; // 시작하자마자 무응답으로 판정되지 않도록 기준을 지금으로 잡는다
        _lastUpdateTime = now;
        _isTimedOut     = false;
        _isRunning      = true;

        StartPingTimer();

        ClientLogger.Info(ClientLogger.Network,
            $"하트비트 시작 — {PingIntervalSeconds:F0}초마다 확인, {ResponseTimeoutSeconds:F0}초 무응답이면 경고");
    }

    // Pong 도착 — 마지막 응답 시각을 갱신한다 (ServerPacketHandler.PongReceived 구독)
    // ※ 이 콜백은 메인 스레드에서 온다 — 수신 패킷은 'NetworkMessageQueue'를 거친다.
    // 평시엔 로그를 남기지 않는다. 2초마다 찍으면 정작 봐야 할 로그가 밀려난다.
    private void OnPongReceived()
    {
        double now = NowSeconds;

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
    private void WarnIfSilentTooLong(double now)
    {
        if (_isTimedOut)
        {
            return;
        }

        double silentSeconds = now - _lastPongTime;

        if (silentSeconds < ResponseTimeoutSeconds)
        {
            return;
        }

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

    #region 송신 타이머

    // ■ 왜 'Update'가 아니라 타이머인가
    //
    // 창을 잡아 끄는 동안 'WindowManager.BeginWindowDrag'가 창 이동을 OS 모달 루프에 위임한다.
    // 마우스를 놓을 때까지 Unity 메인 루프가 통째로 멈추므로, 'Update'에서 보내면 그동안
    // 핑이 한 개도 나가지 않는다 — 서버는 그것을 유휴 세션으로 보고 끊는다.
    // 절전·긴 GC 히치도 같은 경로다.
    //
    // 소켓 송신은 이미 메인 스레드와 무관하다 — 'MikaSendQueue'가 ConcurrentQueue 기반이고
    // 실제 송신 루프('MikaClientSession.SendLoop')도 스레드풀에서 돈다. 그래서 큐에 넣는 일만
    // 타이머 스레드로 옮기면 되고, 판정·알림은 그대로 메인 스레드(Update)에 남는다.

    // 송신 타이머를 켠다(재기동 포함) (BeginHeartbeat에서 호출)
    private void StartPingTimer()
    {
        StopPingTimer();

        int periodMs = (int)(PingIntervalSeconds * 1000f);

        _pingTimer = new System.Threading.Timer(SendPing, null, 0, periodMs);
    }

    // 송신 타이머를 끈다 (OnDisable · OnDestroy · 재기동에서 호출)
    private void StopPingTimer()
    {
        _pingTimer?.Dispose();
        _pingTimer = null;
    }

    // Ping 한 발 (타이머 스레드에서 호출)
    //
    // ⚠️ 여기는 메인 스레드가 아니다. Unity API·ClientLogger를 부르지 않는다 —
    //    로그는 UnityEngine.Object 컨텍스트를 만지므로 다른 스레드에서 부르면 터진다.
    //    타이머 콜백의 미처리 예외는 프로세스를 내리므로 반드시 삼키고, 문구만 남겨
    //    'LogTimerErrorIfAny'가 메인 스레드에서 대신 찍게 한다.
    private void SendPing(object? state)
    {
        try
        {
            // 연결 전(Session이 아직 null)에는 안전하게 무시되고, 연결되면 그때부터 실제로 나간다.
            _network.Send(new C_PingRequest());
        }
        catch (System.Exception e)
        {
            _timerError = e.Message;
        }
    }

    // 타이머 스레드에서 난 예외를 메인 스레드에서 찍는다 (Update에서 호출)
    private void LogTimerErrorIfAny()
    {
        string? error = _timerError;

        if (error == null)
        {
            return;
        }

        _timerError = null; // 같은 오류로 콘솔을 덮지 않는다 — 다음 예외가 오면 다시 찍힌다

        ClientLogger.Error(ClientLogger.Network, $"Ping 송신 실패 — {error}", this);
    }

    #endregion
}
