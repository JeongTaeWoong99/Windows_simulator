using MikaNetwork.Server;
using MikaUtils;

namespace WSGameServer;

// 접속 중인 플레이어의 채취를 주기적으로 정산해 밀어 주는 스케줄러. 고빈도 루프를 두지 않는다 —
// 정산량은 주기가 아니라 경과 시각이 정하므로 늦게 돌아도 결과는 같다 → Server/docs/채취-정산.md 6장
public sealed class GatheringScheduler
{
    private Timer? _timer;

    // 푸시 해상도(채취 주기가 아니다). 체감에만 영향 — 1초면 카운트다운이 0에 닿고도 결과가 늦게 온다(이슈 #11).
    // ⚠️ 더 줄이려면 ConsumeJudgeCount의 밀리초 이월이 전제다 → Server/docs/채취-정산.md 4·6장
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.1);

    private readonly ILogicExecutor _logicExecutor;
    public GatheringScheduler(ILogicExecutor logicExecutor)
    {
        _logicExecutor = logicExecutor;
    }
    
    public void Start()
    {
        if (_timer is not null)
        {
            return;
        }

        // 타이머 스레드에서 게임 상태를 직접 만지면 안 된다. 로직 스레드로 넘긴다.
        // 시각과 대상 목록은 여기서 만들어 넣는다 — Tick 자신은 전역을 보지 않는다.
        _timer = new Timer(_ => _logicExecutor.Post(() => Tick(DateTime.UtcNow, UserManager.Instance.All)),
            null, Interval, Interval);
        ServerLog.Info("채취", $"스케줄러 시작 — 푸시 해상도 {Interval.TotalSeconds}초");
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>로직 스레드에서 실행된다. 시각·대상을 인자로 받는다 — 재화가 생기는 권위 루프라 밖에서 불러야 검증이 된다.</summary>
    public static void Tick(DateTime now, IEnumerable<User> users)
    {
        foreach (var user in users)
        {
            // 한 유저의 예외가 나머지 유저의 정산을 막지 않게 한다.
            try
            {
                user.SettleWorkStation(now);
            }
            catch (Exception e)
            {
                ServerLog.Error("채취", $"주기 정산 실패 Uid={user.Uid}", e);
            }
        }
    }
}
