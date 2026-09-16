using MikaNetwork;
using MikaNetwork.Server;
using MikaUtils;

namespace WSGameServer;

// 오래 아무것도 받지 못한 세션을 끊는 감시자. TCP는 끊김을 알려 주지 않아 좀비 유저가 오프라인 적립을 되살린다(이슈 #10).
// 송신은 살아 있음의 증거가 아니라 수신 시각만 본다 → Server/docs/세션-감시.md
public interface ISessionWatchdog
{
    void Start(MikaServer server);
    void Stop();
}
public sealed class SessionWatchdog : ISessionWatchdog 
{
    private Timer? _timer;
    private MikaServer? _server;

    // 검사 주기(판정 시간이 아니다). ⚠️ 여기를 늘려 무응답 끊김을 줄이려 하지 않는다 — 좀비 수명만 길어진다(이슈 #19).
    // 여유를 만드는 값은 Global.SessionIdleTimeout이다 → Server/docs/세션-감시.md 2장
    internal static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private readonly ILogicExecutor _logicExecutor;
    
    public SessionWatchdog(ILogicExecutor logicExecutor)
    {
        _logicExecutor = logicExecutor;
    }
    
    public void Start(MikaServer server)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (_timer is not null)
        {
            return;
        }

        _server = server;

        // 타이머 스레드에서 게임 상태를 만지면 안 된다. 로직 스레드로 넘긴다 —
        // 세션을 끊으면 Disconnected가 발화해 User.Destroy로 이어진다.
        _timer = new Timer(_ => _logicExecutor.Post(() => Sweep(DateTime.UtcNow)),
                           null, Interval, Interval);

        ServerLog.Info("세션",
            $"감시자 시작 — 검사 {Interval.TotalSeconds}초, 무응답 판정 {Global.SessionIdleTimeout.TotalSeconds}초");
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>로직 스레드에서 실행된다.</summary>
    public void Sweep(DateTime now)
    {
        if (_server is null)
        {
            return;
        }

        try
        {
            var closed = _server.SweepIdle(now, Global.SessionIdleTimeout);
            if (closed > 0)
            {
                ServerLog.Warn("세션", $"무응답으로 {closed}개 세션을 끊었다 (판정 {Global.SessionIdleTimeout.TotalSeconds}초)");
            }
        }
        catch (Exception e)
        {
            // 한 번 실패했다고 감시가 멈추면 좀비가 영구히 남는다.
            ServerLog.Error("세션", "유휴 세션 정리 실패", e);
        }
    }
}
