using MikaNetwork.Server;
using MikaUtils;
using WSGameServer.User.WorkStation;

namespace WSGameServer.Common;

/// <summary>
/// 접속 중인 플레이어의 채취를 <b>주기적으로 정산해 밀어 주는</b> 스케줄러.
///
/// <para>
/// <b>초당 수십 번 도는 루프(30fps 등)를 두지 않는다.</b> 판정 간격이 30초인데 초당 30번 깨우면
/// 900틱 중 899틱이 헛돌고, 게임 로직 스레드가 단일이라 그 낭비가 그대로 패킷 처리를 밀어낸다.
/// 진행도는 <c>LastTickAt</c> 하나로 표현되므로 <b>깨어난 시점에 경과분을 한 번에 계산</b>하면 된다.
/// 이 타이머가 늦게 돌아도 결과는 같다 — 정산량은 주기가 아니라 경과 시각이 정한다.
/// </para>
///
/// <para>
/// 오프라인 정산과 같은 함수(<c>User.SettleWorkStation</c>)를 부른다. 이 타이머는
/// "언제 계산할지"만 정하고 "얼마나 나올지"에는 관여하지 않는다.
/// </para>
/// </summary>
public sealed class GatheringScheduler : Singleton<GatheringScheduler>
{
    private Timer? _timer;

    /// <summary>정산 주기. 채취 주기와 맞춘다.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(WorkStationSlot.CycleSeconds);

    public void Start()
    {
        if (_timer is not null)
            return;

        // 타이머 스레드에서 게임 상태를 직접 만지면 안 된다. 로직 스레드로 넘긴다.
        _timer = new Timer(_ => LogicExecutor.Instance.Post(Tick), null, Interval, Interval);
        Console.WriteLine($"[채취 스케줄러] {Interval.TotalSeconds}초 주기로 시작");
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>로직 스레드에서 실행된다.</summary>
    private static void Tick()
    {
        var now = DateTime.UtcNow;

        foreach (var user in User.UserManager.Instance.All)
        {
            // 한 유저의 예외가 나머지 유저의 정산을 막지 않게 한다.
            try
            {
                user.SettleWorkStation(now);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[채취 스케줄러] 정산 실패 Uid={user.Uid}: {e}");
            }
        }
    }
}
